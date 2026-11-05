using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>The game sessions' log messages.</summary>
internal static partial class GameLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId}: the {Side} sent {Message}, {Length} bytes, {State}")]
    public static partial void Decoded(ILogger logger, string connectionId, string side, string message, long length, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: the {Side} sent partial readable {Message}, relayed as it is: {Category}")]
    public static partial void Partial(ILogger logger, string connectionId, string side, string message, string category);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection {ConnectionId}: the {Side} sent {Message}, not understood and relayed as it is: {Category}")]
    public static partial void NotUnderstood(ILogger logger, string connectionId, string side, string message, string category);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId}: repeated partial readable translation problem for {Message} from the {Side}; seen {Count} times ({Category})")]
    public static partial void RepeatedPartial(ILogger logger, string connectionId, string side, string message, int count, string category);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId}: repeated unread translation problem for {Message} from the {Side}; seen {Count} times ({Category})")]
    public static partial void RepeatedUnread(ILogger logger, string connectionId, string side, string message, int count, string category);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: the {Side} sent {Message}, a proven non-position message of this build, relayed as it is: this frame does not close the movement latch ({Category})")]
    public static partial void NonPosition(ILogger logger, string connectionId, string side, string message, string category);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId}: repeated non-position translation problem for {Message} from the {Side}; seen {Count} times ({Category})")]
    public static partial void RepeatedNonPosition(ILogger logger, string connectionId, string side, string message, int count, string category);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId}: D3B sent {Message}, {Length} bytes")]
    public static partial void Sent(ILogger logger, string connectionId, string message, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Connection {ConnectionId}: the game observer {Observer} failed")]
    public static partial void ObserverFailed(ILogger logger, string connectionId, Type observer, Exception exception);

    internal static string ProblemCategory(DecodedGameFrame frame)
    {
        if (frame.IsRead)
            return "readable payload contains fields this schema leaves out";

        if (frame.ReadableType is not null)
            return "readable message payload could not be parsed";

        if (frame.CarriedType is null && frame.Envelope is { ContentCase: not GameMessage.ContentOneofCase.None })
            return "game envelope length mismatch";

        if (frame.CarriedType is not null)
            return "readable protocol lacks the carried type";

        if (frame.Envelope is not null)
            return "game envelope length mismatch";

        return "malformed game envelope";
    }
}
