using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using D3B.Network.Captures;
using D3B.Network.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Relay;

/// <summary>Relays each client's connection to the real server, bytes as they are, both ways (D36).</summary>
/// <remarks>
/// <para>
/// Kestrel calls it once per accepted connection, from any number of clients at once, with the route its listener serves:
/// the connection phase, or the game (3f). It keeps no state between connections. The server's connection is a
/// <see cref="ConnectionContext"/> too, made by Kestrel's socket layer (D8), so both sides are pipes.
/// </para>
/// <para>
/// Each direction forwards what it reads as soon as it reads it, and records it when captures are on, the game's apart
/// from the connection phase's. With an <see cref="IRelayInterceptor"/>, it also reads the bytes as frames, each shown to
/// the connection's interceptor once whole, holds the frames it asks for (D49), and puts the frames D3B sends the server
/// between the client's (D61). A frame held for longer than
/// <see cref="RelaySettings.HoldTimeout"/> goes on as it came. When a side closes, the other's output is completed, which
/// closes it once its last bytes are sent. A side still open after <see cref="RelaySettings.CloseTimeout"/> is cut: the
/// relay keeps no half-open connection.
/// </para>
/// </remarks>
internal sealed class RelayConnectionHandler
{
    private readonly RelaySettings _settings;
    private readonly IUpstreamLocator _locator;
    private readonly SocketConnectionContextFactory _connections;
    private readonly TimeProvider _time;
    private readonly Func<string?> _clientVersion;
    private readonly IRelayInterceptor? _interceptor;
    private readonly ILogger _log;

    public RelayConnectionHandler(
        RelaySettings settings,
        IUpstreamLocator locator,
        SocketConnectionContextFactory connections,
        TimeProvider time,
        Func<string?> clientVersion,
        IRelayInterceptor? interceptor,
        ILogger<RelayConnectionHandler> log)
    {
        _settings = settings;
        _locator = locator;
        _connections = connections;
        _time = time;
        _clientVersion = clientVersion;
        _interceptor = interceptor;
        _log = log;
    }

    private static void Forward(PipeWriter output, ReadOnlySequence<byte> bytes)
    {
        foreach (var segment in bytes)
            output.Write(segment.Span);
    }

    /// <summary>Relays a connection a listener of the relay accepted.</summary>
    /// <param name="connection">The client's connection.</param>
    /// <param name="route">Which of the client's connections the listener serves.</param>
    /// <returns>A task that completes once both sides are closed.</returns>
    public async Task RelayAsync(ConnectionContext connection, RelayRoute route)
    {
        var started = _time.GetTimestamp();
        var listener = (IPEndPoint)connection.LocalEndPoint!;
        Upstream upstream;
        ConnectionContext? server;
        IPEndPoint? endPoint;

        // The client leaving, or the host stopping, ends the search for its server: neither waits for a timeout.
        var stopRequested = connection.Features.Get<IConnectionLifetimeNotificationFeature>()?.ConnectionClosedRequested ?? CancellationToken.None;

        using (var reaching = CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionClosed, stopRequested))
        {
            try
            {
                // The game's server is the one whose ticket the client presents first: the client speaks first there.
                var clientFrame = route is RelayRoute.Game ? await ReadFirstFrameAsync(connection.Transport.Input, reaching.Token) : ReadOnlyMemory<byte>.Empty;

                upstream = await _locator.LocateAsync(route, listener, clientFrame, reaching.Token);
                (server, endPoint) = await ConnectAsync(connection.ConnectionId, upstream, reaching.Token);
            }
            catch (UpstreamUnavailableException exception)
            {
                RelayLog.NoUpstream(_log, connection.ConnectionId, connection.RemoteEndPoint, exception.Message);

                return;
            }
            catch (OperationCanceledException) when (reaching.IsCancellationRequested)
            {
                // There is nothing to relay.
                return;
            }
        }

        if (server is null)
        {
            RelayLog.NoUpstream(_log, connection.ConnectionId, connection.RemoteEndPoint, $"No address of {upstream.Host} answered.");

            return;
        }

        await using (server)
        {
            RelayLog.Relaying(_log, connection.ConnectionId, connection.RemoteEndPoint, upstream.Host, endPoint!, route is RelayRoute.Game ? "game" : "connection phase");

            using var capture = StartCapture(connection, route, upstream, endPoint!);
            using var closed = new CancellationTokenSource();
            var injected = new InjectedFrames(connection.ConnectionId, server.Transport.Output, capture, _log);
            var interceptor = Intercept(new RelayConnection(connection.ConnectionId, route, injected, closed.Token));
            var clientSide = interceptor is null ? null : new InterceptedSide(connection.ConnectionId, RelayDirection.FromClient, interceptor, _settings.MaxHeldLength, _log);
            var serverSide = interceptor is null ? null : new InterceptedSide(connection.ConnectionId, RelayDirection.FromServer, interceptor, _settings.MaxHeldLength, _log);

            await injected.WatchAsync(clientSide);

            // The host stopping asks every connection to close: both sides are cut, as when the process ends.
            using var stopping = stopRequested.Register(() =>
            {
                connection.Abort();
                server.Abort();
            });

            var fromClient = PumpAsync(connection.Transport.Input, server.Transport.Output, capture, RelayDirection.FromClient, clientSide, injected);
            var fromServer = PumpAsync(server.Transport.Input, connection.Transport.Output, capture, RelayDirection.FromServer, serverSide, null);
            var first = await Task.WhenAny(fromClient, fromServer);
            var second = first == fromClient ? fromServer : fromClient;

            if (await Task.WhenAny(second, Task.Delay(_settings.CloseTimeout, _time)) != second)
            {
                connection.Abort();
                server.Abort();
            }

            var bytesFromClient = await fromClient;
            var bytesFromServer = await fromServer;
            var duration = _time.GetElapsedTime(started);

            await closed.CancelAsync();
            RelayLog.Closed(_log, connection.ConnectionId, duration, bytesFromClient, bytesFromServer, injected.Sent);

            if (capture?.Failure is { } failure)
                RelayLog.CaptureFailed(_log, connection.ConnectionId, failure.Message);
        }
    }

    /// <summary>Reads the client's first frame, leaving it in the pipe so that it is relayed and recorded first.</summary>
    /// <param name="input">The client's side.</param>
    /// <param name="cancellationToken">Stops the reading, such as when the client leaves.</param>
    /// <returns>A copy of the frame, its length already read.</returns>
    /// <exception cref="UpstreamUnavailableException">No whole frame came in time, or what came is none.</exception>
    private async Task<ReadOnlyMemory<byte>> ReadFirstFrameAsync(PipeReader input, CancellationToken cancellationToken)
    {
        // The way out, as for a frame an interceptor holds: what does not come whole in time ends the connection.
        using var expiry = new CancellationTokenSource(_settings.HoldTimeout, _time);
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);

        while (true)
        {
            ReadResult result;

            try
            {
                result = await input.ReadAsync(reading.Token);
            }
            catch (OperationCanceledException) when (expiry.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new UpstreamUnavailableException(string.Create(CultureInfo.InvariantCulture, $"The client sent no whole frame within {_settings.HoldTimeout.TotalSeconds:0.#} s, where its ticket is looked for."));
            }

            var buffer = result.Buffer;
            var reader = new SequenceReader<byte>(buffer);

            try
            {
                if (FrameReader.TryReadFrame(ref reader, out var frame, _settings.MaxHeldLength))
                {
                    var copy = frame.ToArray();

                    // Nothing consumed, nothing examined: the relay's first read gets the frame again at once.
                    input.AdvanceTo(buffer.Start);

                    return copy;
                }
            }
            catch (FrameFormatException exception)
            {
                throw new UpstreamUnavailableException($"The client's first bytes are no frame D3B reads a ticket in: {exception.Message}", exception);
            }

            if (result.IsCompleted)
                throw new UpstreamUnavailableException("The client closed before sending a whole frame, where its ticket is looked for.");

            input.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private IConnectionInterceptor? Intercept(RelayConnection connection)
    {
        try
        {
            return _interceptor?.Intercept(connection);
        }
        catch (Exception exception)
        {
            // The interceptor only watches: its failure must not cut the player off.
            RelayLog.InterceptorFailed(_log, connection.Id, "connection", exception);

            return null;
        }
    }

    private async Task<long> PumpAsync(PipeReader input, PipeWriter output, CaptureWriter? capture, RelayDirection direction, InterceptedSide? side, InjectedFrames? injected)
    {
        var kind = direction is RelayDirection.FromClient ? CaptureRecordKind.FromClient : CaptureRecordKind.FromServer;
        var bytes = 0L;

        // The way out of a frame held that does not come whole: the timer ends the read waiting for the rest.
        ITimer? hold = null;
        var timedHold = -1;
        var holdStarted = 0L;
        var timed = false;

        try
        {
            while (true)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;

                // D3B's own frames go between the client's: none while this read's bytes are being written.
                if (injected is not null)
                    await injected.EnterAsync();

                try
                {
                    if (!buffer.IsEmpty)
                    {
                        capture?.Write(kind, buffer);

                        if (side is null)
                            Forward(output, buffer);
                        else
                            side.Read(buffer, output);

                        bytes += buffer.Length;
                    }

                    input.AdvanceTo(buffer.End);

                    if (side is not null)
                    {
                        var held = _time.GetElapsedTime(holdStarted);

                        if (result.IsCompleted)
                            side.Release(output, "its side closed before it was whole");
                        else if (side.IsHolding && side.HoldNumber == timedHold && held >= _settings.HoldTimeout)
                            side.Release(output, string.Create(CultureInfo.InvariantCulture, $"it was not whole after {_settings.HoldTimeout.TotalSeconds:0.#} s"));

                        if (!side.IsHolding)
                        {
                            if (hold is not null)
                                await hold.DisposeAsync();

                            hold = null;
                        }
                        else if (side.HoldNumber != timedHold)
                        {
                            if (hold is not null)
                                await hold.DisposeAsync();

                            timedHold = side.HoldNumber;
                            holdStarted = _time.GetTimestamp();
                            hold = _time.CreateTimer(_ => input.CancelPendingRead(), null, _settings.HoldTimeout, Timeout.InfiniteTimeSpan);
                            timed = true;
                        }
                        else if (result.IsCanceled)
                        {
                            // A timer ending the read a little early, or an earlier frame's: the frame gets the rest of its time.
                            hold?.Change(_settings.HoldTimeout - held, Timeout.InfiniteTimeSpan);
                        }
                    }

                    injected?.Drain();

                    // A read the hold's timer ended is no end of the side: anything else cancelling it is.
                    if (result.IsCompleted || (result.IsCanceled && !timed))
                        break;

                    var flushed = await output.FlushAsync();

                    injected?.Flushed(flushed);

                    // Completed when the other side is gone: nothing more can reach it.
                    if (flushed.IsCompleted)
                        break;
                }
                finally
                {
                    injected?.Exit();
                }
            }

            if (injected is null)
            {
                await output.FlushAsync();
            }
            else
            {
                await injected.EnterAsync();

                try
                {
                    injected.Drain();
                    injected.Flushed(await output.FlushAsync());
                }
                finally
                {
                    injected.Exit();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ConnectionAbortedException or ConnectionResetException or SocketException or ObjectDisposedException)
        {
            // A connection reset or cut ends its direction like a close: the relay has nothing to answer it with.
        }
        finally
        {
            if (injected is not null)
                await injected.CloseAsync("the connection closed");

            if (hold is not null)
                await hold.DisposeAsync();

            capture?.WriteClosed(kind is CaptureRecordKind.FromClient ? CaptureRecordKind.ClientClosed : CaptureRecordKind.ServerClosed);

            await input.CompleteAsync();
            await output.CompleteAsync();
        }

        return bytes;
    }

    private async Task<(ConnectionContext? Server, IPEndPoint? EndPoint)> ConnectAsync(string connectionId, Upstream upstream, CancellationToken cancellationToken)
    {
        foreach (var endPoint in upstream.EndPoints)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(_settings.ConnectTimeout);
                await socket.ConnectAsync(endPoint, timeout.Token);

                return (_connections.Create(socket), endPoint);
            }
            catch (Exception exception) when (exception is SocketException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                socket.Dispose();
                RelayLog.ConnectFailed(_log, connectionId, endPoint, upstream.Host, exception is SocketException ? exception.Message : "It timed out.");
            }
            catch
            {
                socket.Dispose();

                throw;
            }
        }

        return (null, null);
    }

    private CaptureWriter? StartCapture(ConnectionContext connection, RelayRoute route, Upstream upstream, IPEndPoint server)
    {
        // Apart, so that the mapping tool, reading the connection phase's captures, never meets the game's (D43).
        if ((route is RelayRoute.Game ? _settings.GameCapturesDirectory : _settings.CapturesDirectory) is not { } directory)
            return null;

        var startedAt = _time.GetUtcNow();
        var header = new CaptureHeader
        {
            StartedAt = startedAt,
            ClientVersion = _clientVersion() ?? string.Empty,
            Listener = connection.LocalEndPoint?.ToString() ?? string.Empty,
            ServerHost = upstream.Host,
            Server = server.ToString(),
        };

        try
        {
            var (writer, path) = CaptureWriter.Create(directory, $"{startedAt:yyyyMMdd-HHmmss}-{connection.ConnectionId}", header, _time);

            RelayLog.Capturing(_log, connection.ConnectionId, path);

            return writer;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A capture is for reading the session again: losing it is no reason to cut the player off.
            RelayLog.CaptureFailed(_log, connection.ConnectionId, exception.Message);

            return null;
        }
    }
}
