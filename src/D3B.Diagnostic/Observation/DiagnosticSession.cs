using System.Collections.Concurrent;
using System.Threading.Channels;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Replay;
using Microsoft.Extensions.Logging;

namespace D3B.Diagnostic.Observation;

/// <summary>A bounded, single-reader diagnostic pump for one game connection.</summary>
public sealed partial class DiagnosticSession : IDiagnosticSink, IAsyncDisposable
{
    private readonly Channel<QueuedDiagnosticFrame> _frames;
    private readonly ConcurrentDictionary<long, byte> _droppedTickets = new();
    private readonly SidecarOptions _options;
    private readonly DiagnosticSchema _schema;
    private readonly IDiagnosticSidecarFactory _sidecarFactory;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancel = new();
    private readonly Task _pump;
    private long _nextTicket;
    private long _enqueuedFrames;
    private long _analyzedFrames;
    private long _droppedFrames;
    private long _nextSequence;
    private int _complete;
    private int _failureRecorded;
    private DiagnosticErrorCode? _errorCode;
    private DiagnosticAnalysis? _lastAnalysis;
    private DiagnosticSummary? _summary;

    internal DiagnosticSession(
        SidecarOptions options,
        DiagnosticSchema schema,
        IDiagnosticSidecarFactory sidecarFactory,
        ILogger logger)
    {
        _options = options;
        _schema = schema;
        _sidecarFactory = sidecarFactory;
        _logger = logger;
        _frames = Channel.CreateBounded<QueuedDiagnosticFrame>(
            new BoundedChannelOptions(options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            },
            OnFrameDropped);
        _pump = Task.Run(PumpAsync);
    }

    /// <inheritdoc/>
    public DiagnosticSessionSnapshot Snapshot
    {
        get
        {
            var dropped = Interlocked.Read(ref _droppedFrames);
            var failure = Volatile.Read(ref _failureRecorded) != 0;
            var summary = Volatile.Read(ref _summary);
            var coverage = !failure && dropped == 0 && summary?.Coverage == "complete" ? "complete" : "partial";
            return new DiagnosticSessionSnapshot
            {
                EnqueuedFrames = Interlocked.Read(ref _enqueuedFrames),
                AnalyzedFrames = Interlocked.Read(ref _analyzedFrames),
                DroppedFrames = dropped,
                ErrorCode = _errorCode,
                Coverage = coverage,
                LastAnalysis = Volatile.Read(ref _lastAnalysis),
                Summary = summary,
            };
        }
    }

    /// <inheritdoc/>
    public bool TryEnqueue(DiagnosticFrame frame)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (Volatile.Read(ref _complete) != 0)
            {
                Interlocked.Increment(ref _droppedFrames);
                return false;
            }

            var ticket = Interlocked.Increment(ref _nextTicket);
            if (!_frames.Writer.TryWrite(new QueuedDiagnosticFrame(ticket, frame)))
            {
                Interlocked.Increment(ref _droppedFrames);
                return false;
            }

            if (_droppedTickets.TryRemove(ticket, out _))
            {
                return false;
            }

            Interlocked.Increment(ref _enqueuedFrames);
            return true;
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _droppedFrames);
            return false;
        }
    }

    /// <inheritdoc/>
    public void ReportDropped(long count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _droppedFrames, count);
        }
    }

    /// <inheritdoc/>
    public void Complete()
    {
        if (Interlocked.Exchange(ref _complete, 1) == 0)
        {
            _frames.Writer.TryComplete();
        }
    }

    /// <summary>Completes the queue and waits up to the configured response timeout for sidecar shutdown.</summary>
    public async ValueTask DisposeAsync()
    {
        Complete();
        try
        {
            await _pump.WaitAsync(_options.ResponseTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The pump completed as the shutdown timeout elapsed.
            }
        }
    }

    private async Task PumpAsync()
    {
        IDiagnosticSidecar? sidecar = null;
        long inFlight = 0;
        try
        {
            var start = await _sidecarFactory.StartAsync(_options, _cancel.Token).ConfigureAwait(false);
            if (!start.IsEnabled || start.Sidecar is null)
            {
                CountPendingFrames();
                RecordFailure(start.ErrorCode ?? DiagnosticErrorCode.SocketFailed);
                return;
            }

            sidecar = start.Sidecar;
            await WithTimeoutAsync(
                token => sidecar.StartAsync(_schema, token),
                _options.HandshakeTimeout,
                _cancel.Token).ConfigureAwait(false);

            while (await _frames.Reader.WaitToReadAsync(_cancel.Token).ConfigureAwait(false))
            {
                while (_frames.Reader.TryRead(out var queued))
                {
                    inFlight = 1;
                    var frame = WithSequence(queued.Frame, Interlocked.Increment(ref _nextSequence) - 1);
                    var analysis = await WithTimeoutAsync(
                        token => sidecar.AnalyzeAsync(frame, token),
                        _options.ResponseTimeout,
                        _cancel.Token).ConfigureAwait(false);
                    Volatile.Write(ref _lastAnalysis, analysis);
                    Interlocked.Increment(ref _analyzedFrames);
                    inFlight = 0;
                }
            }

            var summary = await WithTimeoutAsync(
                sidecar.CompleteAsync,
                _options.ResponseTimeout,
                _cancel.Token).ConfigureAwait(false);
            Volatile.Write(ref _summary, summary with
            {
                Dropped = Interlocked.Read(ref _droppedFrames),
                Coverage = Interlocked.Read(ref _droppedFrames) == 0 ? summary.Coverage : "partial",
            });
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            CountPendingFrames(inFlight);
            RecordFailure(DiagnosticErrorCode.IoError);
        }
        catch (Exception exception)
        {
            CountPendingFrames(inFlight);
            RecordFailure(ErrorCode(exception));
        }
        finally
        {
            if (sidecar is not null)
            {
                try
                {
                    await sidecar.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    RecordFailure(ErrorCode(exception));
                }
            }

            _cancel.Dispose();
        }
    }

    private void OnFrameDropped(QueuedDiagnosticFrame frame)
    {
        _droppedTickets.TryAdd(frame.Ticket, 0);
        Interlocked.Increment(ref _droppedFrames);
    }

    private void CountPendingFrames(long inFlight = 0)
    {
        if (inFlight > 0)
        {
            Interlocked.Add(ref _droppedFrames, inFlight);
        }

        while (_frames.Reader.TryRead(out _))
        {
            Interlocked.Increment(ref _droppedFrames);
        }
    }

    private void RecordFailure(DiagnosticErrorCode code)
    {
        if (Interlocked.Exchange(ref _failureRecorded, 1) == 0)
        {
            _errorCode = code;
            try
            {
                LogSidecarStopped(_logger, code, Interlocked.Read(ref _droppedFrames));
            }
            catch (Exception)
            {
                // Logging must not turn a diagnostic failure into a host failure.
            }
        }
    }

    private static DiagnosticErrorCode ErrorCode(Exception exception)
    {
        return exception switch
        {
            DiagnosticSidecarException sidecarException => sidecarException.ErrorCode,
            DiagnosticProtocolException => DiagnosticErrorCode.ContractInvalid,
            IOException => DiagnosticErrorCode.IoError,
            OperationCanceledException => DiagnosticErrorCode.IoError,
            _ => DiagnosticErrorCode.InternalError,
        };
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Diagnostic sidecar stopped with code {ErrorCode}; {DroppedFrames} frames were dropped or not analyzed.")]
    private static partial void LogSidecarStopped(ILogger logger, DiagnosticErrorCode errorCode, long droppedFrames);

    private static DiagnosticFrame WithSequence(DiagnosticFrame frame, long sequence)
    {
        return new DiagnosticFrame(sequence, frame.Direction, frame.TypeUrl, frame.CarriedType, frame.Payload);
    }

    private static async Task<T> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        return await action(timeoutSource.Token).ConfigureAwait(false);
    }
}
