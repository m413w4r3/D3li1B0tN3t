using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>The supervised element-use log messages, free of message payloads and connection secrets.</summary>
internal static partial class SupervisedElementUseLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised element use enabled: waiting for an explicit command on standard input")]
    public static partial void Enabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised element use preparation: {Preparation}")]
    public static partial void Preparation(ILogger logger, string preparation);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised element use result: {Result}")]
    public static partial void Result(ILogger logger, string result);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised element use candidates: {Candidates}")]
    public static partial void Candidates(ILogger logger, string candidates);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised harvest plan: {Plan}")]
    public static partial void Plan(ILogger logger, string plan);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Supervised element use command refused: {Problem}")]
    public static partial void CommandRefused(ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Supervised element use command reader stopped on {Error}")]
    public static partial void ReaderFailed(ILogger logger, string error);
}
