using System.Collections.Concurrent;
using D3B.Network.Relay;

namespace D3B.Bot.Tests.Network;

/// <summary>A connection as the relay shows it, keeping the frames sent to its server, or failing them once closed.</summary>
internal sealed class FakeRelayConnection : IRelayConnection, IRelayAdmissionConnection, IRelayAdmissionModeController, IDisposable
{
    private readonly CancellationTokenSource _closed = new();
    private readonly CancellationTokenSource _observationFailed = new();
    private readonly object _sendGate = new();
    private readonly ConcurrentQueue<byte[]> _sent = new();
    private AdmissionSnapshot _admission = new(0, false);
    private int _isObservationFailed;

    public Func<CancellationToken, Task> ObservationWait { get; set; } = _ => Task.CompletedTask;

    /// <summary>A test's wait before a frame is queued, to hold it while the session changes.</summary>
    public Func<CancellationToken, Task>? SendWait { get; set; }

    public event Action<int>? FrameSent;

    public string Id { get; }

    public RelayRoute Route { get; }

    public CancellationToken Closed => _closed.Token;

    public IReadOnlyList<byte[]> Sent => [.. _sent];

    public bool IsObservationFailed => Volatile.Read(ref _isObservationFailed) is not 0;

    public FakeRelayConnection(string id, RelayRoute route)
    {
        Id = id;
        Route = route;
    }

    void IRelayAdmissionModeController.SetAdmissionMode(long version, bool requiresTypedAdmission)
    {
        lock (_sendGate)
            Volatile.Write(ref _admission, new AdmissionSnapshot(version, requiresTypedAdmission));
    }

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        return SendCoreAsync(frame.ToArray(), admission: null, cancellationToken);
    }

    Task IRelayAdmissionConnection.SendAdmittedAsync(ReadOnlyMemory<byte> frame, RelaySendAdmission admission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);
        var snapshot = frame.ToArray();

        if (!admission.IsBoundTo(snapshot))
            throw new RelayInjectionException("The frame did not go: its admission capability belongs to another frame.");

        return SendCoreAsync(snapshot, admission, cancellationToken);
    }

    private async Task SendCoreAsync(byte[] frame, RelaySendAdmission? admission, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SendWait is { } wait)
            await wait(cancellationToken).WaitAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        int sentCount;

        lock (_sendGate)
        {
            if (_closed.IsCancellationRequested)
                throw new RelayInjectionException("The frame did not go: the client's side closed.");

            if (IsObservationFailed)
                throw new RelayInjectionException("The frame did not go: the connection's frames are no longer observable.");

            var current = Volatile.Read(ref _admission);

            if ((admission is not null && current.Version != admission.Version)
                || (current.RequiresTypedAdmission && admission is null)
                || (admission is not null && !admission.IsStillAdmitted()))
                throw new RelayInjectionException("The frame did not go: its game input admission changed before it was written.");

            _sent.Enqueue(frame);
            sentCount = _sent.Count;
        }

        FrameSent?.Invoke(sentCount);
    }

    public async Task WaitForObservedFramesAsync(CancellationToken cancellationToken = default)
    {
        if (_closed.IsCancellationRequested)
            throw new RelayInjectionException("The observation wait failed: the connection is closed.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closed.Token, _observationFailed.Token);

        try
        {
            await ObservationWait(linked.Token).WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (IsObservationFailed)
        {
            throw new RelayInjectionException("The observation wait failed: the connection's frames are no longer observable.");
        }
        catch (OperationCanceledException) when (_closed.IsCancellationRequested)
        {
            throw new RelayInjectionException("The observation wait failed: the connection is closed.");
        }
    }

    public void FailObservation()
    {
        lock (_sendGate)
            Volatile.Write(ref _isObservationFailed, 1);

        _observationFailed.Cancel();
    }

    public void Close()
    {
        _closed.Cancel();
    }

    public void Dispose()
    {
        _closed.Dispose();
        _observationFailed.Dispose();
    }

    private sealed class AdmissionSnapshot
    {
        public long Version { get; }

        public bool RequiresTypedAdmission { get; }

        public AdmissionSnapshot(long version, bool requiresTypedAdmission)
        {
            Version = version;
            RequiresTypedAdmission = requiresTypedAdmission;
        }
    }
}
