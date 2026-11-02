using System.Net;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Relay;

/// <summary>The relay's log messages.</summary>
internal static partial class RelayLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Relaying the connection host on {EndPoint}")]
    public static partial void Listening(ILogger logger, IPEndPoint endPoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "Relaying the game servers on {EndPoint}")]
    public static partial void ListeningForGame(ILogger logger, IPEndPoint endPoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording captures in {Directory}; they hold accounts' login tickets: never share them")]
    public static partial void Recording(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId} from {Client} closed: no server to relay it to. {Reason}")]
    public static partial void NoUpstream(ILogger logger, string connectionId, EndPoint? client, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId}: {Server} of {Host} did not answer. {Reason}")]
    public static partial void ConnectFailed(ILogger logger, string connectionId, IPEndPoint server, string host, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId} from {Client} relayed to {Host} at {Server}: the {Route}")]
    public static partial void Relaying(ILogger logger, string connectionId, EndPoint? client, string host, IPEndPoint server, string route);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: recording {Path}")]
    public static partial void Capturing(ILogger logger, string connectionId, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId} is relayed without its capture. {Reason}")]
    public static partial void CaptureFailed(ILogger logger, string connectionId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId}: the {Side}'s bytes are no varint frames, and are relayed unwatched from here on. {Reason}")]
    public static partial void Unframed(ILogger logger, string connectionId, string side, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Connection {ConnectionId}: the {Side}'s frames are relayed unwatched from here on, the interceptor having failed")]
    public static partial void InterceptorFailed(ILogger logger, string connectionId, string side, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId} closed after {Duration}: {FromClient} bytes from the client, {FromServer} from the server, {Injected} frames from D3B")]
    public static partial void Closed(ILogger logger, string connectionId, TimeSpan duration, long fromClient, long fromServer, int injected);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId}: D3B sent the server a frame of its own, {Length} bytes with its length")]
    public static partial void Injected(ILogger logger, string connectionId, int length);
}
