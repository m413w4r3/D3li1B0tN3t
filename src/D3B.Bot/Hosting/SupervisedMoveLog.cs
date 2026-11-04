using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>The supervised one-step move's log messages, free of message payloads and connection secrets.</summary>
internal static partial class SupervisedMoveLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised move enabled: waiting for an explicit command on standard input")]
    public static partial void Enabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised move readiness: connected {Connected}, session {Session}, translation blocked {TranslationBlocked}, translation blocked at first placement {TranslationBlockedAtFirstPlacement}, guard {Guard}, map {Map}, cell {Cell}, provenance {Provenance}, trusted {Trusted}, reason {Reason}")]
    public static partial void Readiness(ILogger logger, bool connected, bool session, bool translationBlocked, string translationBlockedAtFirstPlacement, string guard, long? map, int? cell, string provenance, bool trusted, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised move plan: ready {Ready}, reason {Reason}, source {Source}, target {Target}, transition {Transition}, exit cell {ExitCell}, guard {Guard}, translation blocked {TranslationBlocked}")]
    public static partial void Plan(ILogger logger, bool ready, string reason, long source, long target, string transition, int? exitCell, string guard, bool translationBlocked);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supervised move result: {Outcome} {Reason}, source {Source}, target {Target}, transition {Transition}, end map {EndMap}, end cell {EndCell}, end provenance {EndProvenance}, end trusted {EndTrusted}")]
    public static partial void Result(ILogger logger, string outcome, string reason, long source, long target, string transition, long? endMap, int? endCell, string endProvenance, bool endTrusted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Supervised move command refused: {Problem}")]
    public static partial void CommandRefused(ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Supervised move command reader stopped on {Error}")]
    public static partial void ReaderFailed(ILogger logger, string error);
}
