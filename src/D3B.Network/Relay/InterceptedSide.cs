using System.Buffers;
using D3B.Network.Framing;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Relay;

/// <summary>One side of a relayed connection read as frames for its interceptor, which may have a frame held and rewritten (D49).</summary>
/// <remarks>
/// <para>
/// Bytes go on as they are read, but for a frame the interceptor holds: its bytes wait until it is whole, then what the
/// interceptor gives goes in its place. A frame is held only when nothing of it has gone yet, and only when its length is
/// within <see cref="RelaySettings.MaxHeldLength"/>. <see cref="Release"/> is the way out: what is held goes as it came,
/// and nothing more is held on this side.
/// </para>
/// <para>
/// A side whose bytes are no frames, or whose interceptor throws, is watched no more: its bytes go on as they are read.
/// </para>
/// </remarks>
internal sealed class InterceptedSide
{
    private readonly string _connectionId;
    private readonly RelayDirection _direction;
    private readonly IConnectionInterceptor _interceptor;
    private readonly int _maxHeldLength;
    private readonly ILogger _log;

    // The start of a frame not yet whole: already forwarded, kept to show the frame once whole; or held.
    private readonly ArrayBufferWriter<byte> _pending = new();
    private bool _pendingHeld;
    private bool _mayHold = true;

    /// <summary>Gets whether the interceptor still watches this side.</summary>
    public bool IsWatched { get; private set; } = true;

    /// <summary>Gets whether bytes are held, waiting for the rest of their frame.</summary>
    public bool IsHolding => _pendingHeld;

    /// <summary>Gets whether what has gone to the other side ends on a frame's end, so that a frame of D3B's own may follow it (D61).</summary>
    /// <remarks>A frame held has none of its bytes gone yet; a side no longer watched has no known frames.</remarks>
    public bool IsBetweenFrames => IsWatched && (_pending.WrittenCount is 0 || _pendingHeld);

    /// <summary>Gets a number that changes whenever another frame starts being held, so that each is timed on its own.</summary>
    public int HoldNumber { get; private set; }

    public InterceptedSide(string connectionId, RelayDirection direction, IConnectionInterceptor interceptor, int maxHeldLength, ILogger log)
    {
        _connectionId = connectionId;
        _direction = direction;
        _interceptor = interceptor;
        _maxHeldLength = maxHeldLength;
        _log = log;
    }

    private static void Forward(IBufferWriter<byte> output, ReadOnlySequence<byte> bytes)
    {
        foreach (var segment in bytes)
            output.Write(segment.Span);
    }

    /// <summary>Forwards what a read brought, but for a frame held, and shows the interceptor every frame now whole.</summary>
    /// <param name="read">The bytes the side sent.</param>
    /// <param name="output">Where the other side's bytes go.</param>
    public void Read(ReadOnlySequence<byte> read, IBufferWriter<byte> output)
    {
        if (!IsWatched)
        {
            Forward(output, read);

            return;
        }

        var bytes = read;

        // The bytes before it have gone, or were rewritten; none after it has: the pending start, unless it was held.
        var forwarded = 0L;

        if (_pending.WrittenCount > 0)
        {
            forwarded = _pendingHeld ? 0 : _pending.WrittenCount;

            foreach (var segment in read)
                _pending.Write(segment.Span);

            bytes = new ReadOnlySequence<byte>(_pending.WrittenMemory);
        }

        var reader = new SequenceReader<byte>(bytes);

        try
        {
            while (true)
            {
                var start = reader.Consumed;

                if (!FrameReader.TryReadFrame(ref reader, out var frame))
                    break;

                var end = reader.Consumed;

                if (start >= forwarded && _mayHold && _interceptor.Holds(_direction))
                {
                    // Nothing of it has gone: should the interceptor throw, it goes from its start as it came.
                    var replacement = _interceptor.OnFrame(_direction, frame, held: true);

                    if (replacement is null)
                        Forward(output, bytes.Slice(start, end - start));
                    else
                        FrameWriter.Write(output, replacement);

                    forwarded = end;
                }
                else
                {
                    var from = Math.Max(start, forwarded);

                    Forward(output, bytes.Slice(from, end - from));
                    forwarded = end;
                    _interceptor.OnFrame(_direction, frame, held: false);
                }
            }

            var rest = bytes.Slice(reader.Position);
            var held = reader.Consumed >= forwarded && _mayHold && IsWithinHeldLength(reader) && _interceptor.Holds(_direction);

            if (!held)
                Forward(output, bytes.Slice(forwarded));

            // Copied out first: the rest may lie in the very buffer it is written back to.
            var kept = rest.ToArray();

            // The frame held before is still the one held only if none came whole since.
            var sameFrame = _pendingHeld && reader.Consumed is 0;

            _pending.ResetWrittenCount();
            _pending.Write(kept);
            _pendingHeld = held && kept.Length > 0;

            if (_pendingHeld && !sameFrame)
                HoldNumber++;
        }
        catch (FrameFormatException exception)
        {
            RelayLog.Unframed(_log, _connectionId, Side(), exception.Message);
            StopWatching(output, bytes.Slice(forwarded));
        }
        catch (Exception exception)
        {
            // The interceptor only watches, and rewrites what it understands: its failure must not cut the player off.
            RelayLog.InterceptorFailed(_log, _connectionId, Side(), exception);
            StopWatching(output, bytes.Slice(forwarded));
        }
    }

    /// <summary>Forwards what is held as it came, and holds nothing more on this side: the way out of a frame that does not come whole.</summary>
    /// <param name="output">Where the other side's bytes go.</param>
    /// <param name="reason">Why, for the interceptor.</param>
    public void Release(IBufferWriter<byte> output, string reason)
    {
        _mayHold = false;

        if (!_pendingHeld)
            return;

        Forward(output, new ReadOnlySequence<byte>(_pending.WrittenMemory));
        _pendingHeld = false;

        try
        {
            _interceptor.OnHoldAbandoned(_direction, reason);
        }
        catch (Exception exception)
        {
            RelayLog.InterceptorFailed(_log, _connectionId, Side(), exception);
            IsWatched = false;
        }
    }

    private bool IsWithinHeldLength(SequenceReader<byte> reader)
    {
        // A length not yet whole is five bytes at most: held, until it says more.
        return !FrameReader.TryPeekLength(ref reader, out var length) || length <= _maxHeldLength;
    }

    private void StopWatching(IBufferWriter<byte> output, ReadOnlySequence<byte> unforwarded)
    {
        Forward(output, unforwarded);
        IsWatched = false;
        _pendingHeld = false;
        _pending.Clear();
    }

    private string Side()
    {
        return _direction is RelayDirection.FromClient ? "client" : "server";
    }
}
