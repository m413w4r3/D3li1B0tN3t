using System.Collections.Concurrent;
using System.Threading.Channels;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>Passes selected game frames to the isolated diagnostic sidecar.</summary>
public sealed partial class DiagnosticObserver : IGameObserver, IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);
    private readonly IDiagnosticSessionFactory? _sessions;
    private readonly bool _includeReadable;
    private readonly ILogger<DiagnosticObserver> _log;
    private readonly int _queueCapacity;
    private readonly long _queueByteCapacity;
    private readonly IReadOnlySet<string> _schemaMessageNames;
    private readonly ConcurrentDictionary<GameSession, SessionObservation> _observations = new();
    private readonly object _shutdownGate = new();
    private Task? _shutdownTask;
    private int _disposed;

    /// <summary>Creates an observer for diagnostic sessions.</summary>
    public DiagnosticObserver(IDiagnosticSessionFactory sessions, bool includeReadable, ILogger<DiagnosticObserver> log, IReadOnlySet<string> schemaMessageNames, int queueCapacity = 256, long queueByteCapacity = 16L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(schemaMessageNames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueByteCapacity);
        _sessions = sessions;
        _includeReadable = includeReadable;
        _log = log;
        _queueCapacity = queueCapacity;
        _queueByteCapacity = queueByteCapacity;
        _schemaMessageNames = schemaMessageNames;
        if (_includeReadable)
        {
            LogReadablePayloadsEnabled(_log);
        }
    }

    internal DiagnosticObserver(ILogger<DiagnosticObserver> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        _schemaMessageNames = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            BeginShutdown().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await BeginShutdown().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    /// <inheritdoc />
    public void OnSessionStarted(GameSession session)
    {
        if (_sessions is null || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        lock (_shutdownGate)
        {
            if (_disposed != 0)
            {
                return;
            }

            try
            {
                var observation = new SessionObservation(session, _sessions.CreateSession(), _queueCapacity, _queueByteCapacity);
                if (!_observations.TryAdd(session, observation))
                {
                    observation.Complete();
                    _ = DisposeOrphanSinkAsync(observation.Sink);
                    return;
                }

                observation.Pump = Task.Run(() => PumpAsync(observation));
                session.Closed.Register(() => Complete(observation));
            }
            catch (Exception)
            {
                try
                {
                    LogSessionStartFailed(_log);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    /// <inheritdoc />
    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        SessionObservation? observation = null;
        try
        {
            if (_sessions is null || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (!_observations.TryGetValue(session, out observation))
            {
                return;
            }

            if (!frame.IsGameTypeUrl || frame.CarriedType is null || !_schemaMessageNames.Contains(frame.CarriedType))
            {
                return;
            }

            bool isUnknownCarriedType = frame.ProblemKind is TranslationProblemKind.UnknownCarriedType;
            if (!isUnknownCarriedType && (!_includeReadable || !frame.IsRead))
            {
                return;
            }

            if (GetAny(frame.Envelope) is not { } any)
            {
                return;
            }

            if (any.Value.Length > DiagnosticProtocol.FramePayloadLimit)
            {
                Interlocked.Increment(ref observation.DroppedFrames);
                observation.LogDropOnce(_log);
                return;
            }

            var diagnosticFrame = new DiagnosticFrame(
                Interlocked.Increment(ref observation.Sequence),
                direction is RelayDirection.FromClient ? "client" : "server",
                any.TypeUrl,
                frame.CarriedType,
                any.Value.ToByteArray());

            if (!observation.TryReserveBytes(diagnosticFrame.Payload.Length))
            {
                Interlocked.Increment(ref observation.DroppedFrames);
                observation.LogDropOnce(_log);
                return;
            }

            if (!observation.Queue.Writer.TryWrite(diagnosticFrame))
            {
                Interlocked.Add(ref observation.QueuedBytes, -diagnosticFrame.Payload.Length);
                Interlocked.Increment(ref observation.DroppedFrames);
                observation.LogDropOnce(_log);
            }
        }
        catch (Exception)
        {
            if (observation is not null)
            {
                Interlocked.Increment(ref observation.DroppedFrames);
                observation.LogDropOnce(_log);
            }
        }
    }

    private static Any? GetAny(GameMessage? envelope)
    {
        if (envelope is null)
        {
            return null;
        }

        return envelope.ContentCase switch
        {
            GameMessage.ContentOneofCase.Request => envelope.Request.Content,
            GameMessage.ContentOneofCase.Response => envelope.Response.Content,
            GameMessage.ContentOneofCase.Event => envelope.Event.Content,
            _ => null,
        };
    }

    private static void Complete(SessionObservation observation)
    {
        observation.Complete();
    }

    private async Task PumpAsync(SessionObservation observation)
    {
        try
        {
            await foreach (DiagnosticFrame frame in observation.Queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                Interlocked.Add(ref observation.QueuedBytes, -frame.Payload.Length);
                long dropped = Interlocked.Exchange(ref observation.DroppedFrames, 0);
                if (dropped > 0)
                {
                    observation.Sink.ReportDropped(dropped);
                }

                bool enqueued;
                try
                {
                    enqueued = observation.Sink.TryEnqueue(frame);
                }
                catch
                {
                    Interlocked.Increment(ref observation.DroppedFrames);
                    throw;
                }

                if (!enqueued)
                {
                    observation.LogDropOnce(_log);
                }
            }
        }
        catch (Exception)
        {
            observation.LogFailure(_log);
        }
        finally
        {
            while (observation.Queue.Reader.TryRead(out DiagnosticFrame? pending))
            {
                Interlocked.Add(ref observation.QueuedBytes, -pending.Payload.Length);
                Interlocked.Increment(ref observation.DroppedFrames);
            }

            long dropped = Interlocked.Exchange(ref observation.DroppedFrames, 0);
            if (dropped > 0)
            {
                try
                {
                    observation.Sink.ReportDropped(dropped);
                }
                catch (Exception)
                {
                }
            }

            try
            {
                observation.Sink.Complete();
            }
            catch (Exception)
            {
                observation.LogFailure(_log);
            }

            try
            {
                await observation.Sink.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                observation.LogFailure(_log);
            }
            finally
            {
                _observations.TryRemove(observation.Session, out _);
            }
        }
    }

    private void LogFailure(SessionObservation observation)
    {
        observation.LogFailure(_log);
    }

    private Task BeginShutdown()
    {
        lock (_shutdownGate)
        {
            if (_shutdownTask is not null)
            {
                return _shutdownTask;
            }

            Volatile.Write(ref _disposed, 1);
            SessionObservation[] observations = _observations.Values.ToArray();
            foreach (SessionObservation observation in observations)
            {
                observation.Complete();
            }

            Task[] pumps = observations
                .Select(observation => observation.Pump)
                .Where(pump => pump is not null)
                .Cast<Task>()
                .ToArray();
            _shutdownTask = WaitForShutdownAsync(pumps);
            return _shutdownTask;
        }
    }

    private async Task WaitForShutdownAsync(Task[] pumps)
    {
        try
        {
            await Task.WhenAll(pumps).WaitAsync(_shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            try
            {
                LogPartialShutdown(_log);
            }
            catch (Exception)
            {
            }
        }
        catch (Exception)
        {
        }
    }

    private static async Task DisposeOrphanSinkAsync(IDiagnosticSink sink)
    {
        try
        {
            sink.Complete();
        }
        catch (Exception)
        {
        }

        try
        {
            await sink.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Diagnostic observation could not start for a game session.")]
    private static partial void LogSessionStartFailed(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Diagnostic observation is partial; frames were dropped.")]
    private static partial void LogFramesDropped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Diagnostic observation is partial. Error code: {ErrorCode}; dropped frames: {DroppedFrames}.")]
    private static partial void LogObservationFailure(ILogger logger, string errorCode, long droppedFrames);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Diagnostic:IncludeReadable is enabled; readable payloads may contain sensitive data.")]
    private static partial void LogReadablePayloadsEnabled(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Diagnostic observer shutdown was partial; some sessions did not finish within the shutdown bound.")]
    private static partial void LogPartialShutdown(ILogger logger);

    private sealed class SessionObservation
    {
        public long Sequence = -1;

        public long DroppedFrames;

        public long QueuedBytes;

        private readonly long _byteCapacity;

        public Channel<DiagnosticFrame> Queue { get; }

        public Task? Pump { get; set; }

        public IDiagnosticSink Sink { get; }

        public GameSession Session { get; }

        public SessionObservation(GameSession session, IDiagnosticSink sink, int capacity, long byteCapacity)
        {
            Session = session;
            Sink = sink;
            _byteCapacity = byteCapacity;
            Queue = Channel.CreateBounded<DiagnosticFrame>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public bool TryReserveBytes(int bytes)
        {
            while (true)
            {
                long queued = Interlocked.Read(ref QueuedBytes);
                if (bytes > _byteCapacity - queued)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref QueuedBytes, queued + bytes, queued) == queued)
                {
                    return true;
                }
            }
        }

        public void Complete()
        {
            Queue.Writer.TryComplete();
        }

        public void LogDropOnce(ILogger logger)
        {
            if (Interlocked.Exchange(ref _dropLogged, 1) is 0)
            {
                try
                {
                    LogFramesDropped(logger);
                }
                catch (Exception)
                {
                }
            }
        }

        public void LogFailure(ILogger logger)
        {
            if (Interlocked.Exchange(ref _failureLogged, 1) is not 0)
            {
                return;
            }

            try
            {
                var snapshot = Sink.Snapshot;
                LogObservationFailure(logger,
                    snapshot.ErrorCode?.ToString() ?? "none", snapshot.DroppedFrames + Interlocked.Read(ref DroppedFrames));
            }
            catch (Exception)
            {
            }
        }

        private int _failureLogged;

        private int _dropLogged;
    }
}
