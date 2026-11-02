using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>The game sessions' log messages.</summary>
internal static partial class GameLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId}: the {Side} sent {Message}, {Length} bytes, {State}")]
    public static partial void Decoded(ILogger logger, string connectionId, string side, string message, long length, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: D3B sent {Message}, {Length} bytes")]
    public static partial void Sent(ILogger logger, string connectionId, string message, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Connection {ConnectionId}: the game observer {Observer} failed")]
    public static partial void ObserverFailed(ILogger logger, string connectionId, Type observer, Exception exception);
}
