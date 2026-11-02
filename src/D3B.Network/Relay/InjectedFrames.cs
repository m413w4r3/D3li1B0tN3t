using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using D3B.Network.Captures;
using D3B.Network.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Relay;

/// <summary>The frames D3B sends a connection's server itself, each put between two of the client's frames (D61).</summary>
/// <remarks>
/// <para>
/// The client's side writes to the server's pipe from its own read loop, and a frame of D3B's from whoever sends it: a
/// gate keeps them apart, the read loop holding it from a read's bytes to their flush. A frame goes at once when what went
/// to the server ends on one of the client's frames, else once the client's frame in progress has gone whole, so that the
/// server reads every frame whole. Until the client's side is watched, frames wait.
/// </para>
/// <para>
/// Each frame is recorded in the capture as it goes. Once the client's side closes, or its bytes turn out to be no frames,
/// every frame not gone fails with a <see cref="RelayInjectionException"/>.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "The gate holds a handle only once its AvailableWaitHandle is read, which nothing does; disposed, a send coming late would fail as disposed instead of as a frame that did not go.")]
internal sealed class InjectedFrames
{
    private readonly string _connectionId;
    private readonly PipeWriter _output;
    private readonly CaptureWriter? _capture;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<Frame> _waiting = new();
    private readonly List<Frame> _written = [];
    private InterceptedSide? _side;
    private string? _closed;

    /// <summary>Gets how many frames went to the server.</summary>
    public int Sent { get; private set; }

    public InjectedFrames(string connectionId, PipeWriter output, CaptureWriter? capture, ILogger log)
    {
        _connectionId = connectionId;
        _output = output;
        _capture = capture;
        _log = log;
    }

    /// <summary>Sends a frame, as <see cref="IRelayConnection.SendToServerAsync"/> describes.</summary>
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame.Length, FrameReader.DefaultMaxLength, nameof(frame));

        var writer = new ArrayBufferWriter<byte>(FrameWriter.LengthSize(frame.Length) + frame.Length);

        FrameWriter.Write(writer, frame.Span);

        var injected = new Frame(writer.WrittenSpan.ToArray());

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_closed is not null)
                throw new RelayInjectionException($"The frame did not go: {_closed}.");

            _waiting.Enqueue(injected);

            if (Drain())
                await FlushAsync();
        }
        finally
        {
            _gate.Release();
        }

        using (cancellationToken.Register(static state => ((Frame)state!).Cancel(), injected))
            await injected.Gone;
    }

    /// <summary>Starts putting frames between the client's, once its side is read as frames.</summary>
    /// <param name="side">The client's side, or null when the connection is not watched, which no frame can go between.</param>
    public async Task WatchAsync(InterceptedSide? side)
    {
        await _gate.WaitAsync();

        try
        {
            if (side is null)
            {
                Close("the connection is not watched, so its frames are not known");

                return;
            }

            _side = side;

            if (Drain())
                await FlushAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Takes the gate, for the client's read loop to write a read's bytes and flush them.</summary>
    public Task EnterAsync()
    {
        return _gate.WaitAsync();
    }

    /// <summary>Gives the gate back.</summary>
    public void Exit()
    {
        _gate.Release();
    }

    /// <summary>Writes the frames waiting, when what went to the server ends on a frame; the gate is held.</summary>
    /// <returns>Whether a frame was written, which a flush then completes.</returns>
    public bool Drain()
    {
        if (_waiting.Count is 0 || _side is null)
            return false;

        if (!_side.IsWatched)
        {
            Close("the client's bytes are no frames to put it between");

            return false;
        }

        if (!_side.IsBetweenFrames)
            return false;

        var wrote = false;

        while (_waiting.TryDequeue(out var frame))
        {
            if (!frame.TryClaim())
                continue;

            _output.Write(frame.Bytes);
            _capture?.Write(CaptureRecordKind.Injected, new ReadOnlySequence<byte>(frame.Bytes));
            _written.Add(frame);
            Sent++;
            RelayLog.Injected(_log, _connectionId, frame.Bytes.Length);
            wrote = true;
        }

        return wrote;
    }

    /// <summary>Completes the frames written once the client's read loop flushed them; the gate is held.</summary>
    /// <param name="result">The flush's result.</param>
    public void Flushed(FlushResult result)
    {
        if (result.IsCompleted || result.IsCanceled)
        {
            Close("the server's side closed");

            return;
        }

        foreach (var frame in _written)
            frame.Succeed();

        _written.Clear();
    }

    /// <summary>Fails every frame not gone, and every frame sent from now on; the gate is held.</summary>
    /// <param name="reason">Why, as the exception says it.</param>
    public void Close(string reason)
    {
        _closed ??= reason;

        foreach (var frame in _written.Concat(_waiting))
            frame.Fail(_closed);

        _written.Clear();
        _waiting.Clear();
    }

    /// <summary>Fails every frame not gone, the client's side being done.</summary>
    public async Task CloseAsync(string reason)
    {
        await _gate.WaitAsync();

        try
        {
            Close(reason);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task FlushAsync()
    {
        try
        {
            Flushed(await _output.FlushAsync());
        }
        catch (Exception exception) when (exception is IOException or ConnectionAbortedException or ConnectionResetException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // The client's read loop meets the same failure, and ends its side.
            Close("the server's side failed");
        }
    }

    /// <summary>A frame of D3B's, with its length, and whether it went.</summary>
    private sealed class Frame
    {
        private const int Waiting = 0;
        private const int Claimed = 1;
        private const int Cancelled = 2;

        private readonly TaskCompletionSource _gone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _state;

        public byte[] Bytes { get; }

        public Task Gone => _gone.Task;

        public Frame(byte[] bytes)
        {
            Bytes = bytes;
        }

        /// <summary>Takes the frame to write it, unless its sender stopped waiting first.</summary>
        public bool TryClaim()
        {
            return Interlocked.CompareExchange(ref _state, Claimed, Waiting) is Waiting;
        }

        /// <summary>Withdraws the frame, unless it is already being written.</summary>
        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref _state, Cancelled, Waiting) is Waiting)
                _gone.TrySetCanceled();
        }

        public void Succeed()
        {
            _gone.TrySetResult();
        }

        public void Fail(string reason)
        {
            _gone.TrySetException(new RelayInjectionException($"The frame did not go: {reason}."));
        }
    }
}
