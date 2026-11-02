using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>The network module's log messages.</summary>
internal static partial class NetworkLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: the {Side} sent {Message}, {Length} bytes")]
    public static partial void Decoded(ILogger logger, string connectionId, string side, string message, long length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId}: the {Side} sent {Message}, {Length} bytes, not understood and relayed as it is: {Problem}")]
    public static partial void NotUnderstood(ILogger logger, string connectionId, string side, string message, long length, string problem);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: the server selection's answer now sends the client to D3B's game listener, {Address} on ports {Ports}")]
    public static partial void SelectionRewritten(ILogger logger, string connectionId, string address, string ports);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId}: the server selection's answer goes on as it came, and the game connection straight to the game server: {Reason}")]
    public static partial void SelectionNotRewritten(ILogger logger, string connectionId, string reason);
}
