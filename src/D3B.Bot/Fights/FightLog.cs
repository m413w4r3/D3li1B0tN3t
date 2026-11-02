using D3B.Bot.Runtime;
using D3B.Engine.Tactics;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Fights;

/// <summary>The fight AI's log messages, each naming its bot by its character's id.</summary>
internal static partial class FightLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plays its fight, {TurnsToWin:F1} turns to win against {TurnsToLose:F1} to lose, prudent {Prudent}")]
    public static partial void Judged(ILogger logger, long character, double turnsToWin, double turnsToLose, bool prudent);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: placed on cell {Cell}")]
    public static partial void Placed(ILogger logger, long character, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plans {Actions} actions, worth {Score:F0} against {Standing:F0} ending now, {Weighed} weighed in {Milliseconds} ms")]
    public static partial void Planned(ILogger logger, long character, int actions, double score, double standing, int weighed, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: casts spell {Spell} on cell {Cell}, {Outcome}")]
    public static partial void Cast(ILogger logger, long character, int spell, int cell, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the server refused spell {Spell} on cell {Cell}, text {Text}")]
    public static partial void Refused(ILogger logger, long character, int spell, int cell, int text);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walks to cell {Cell}, {Steps} steps, {Outcome}")]
    public static partial void Walked(ILogger logger, long character, int cell, int steps, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: ends its turn after {Actions} actions")]
    public static partial void TurnEnded(ILogger logger, long character, int actions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: the client did not show action {Action} within its delay")]
    public static partial void NotShown(ILogger logger, long character, TacticActionKind action);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: the fight's AI failed, the fight is left to the player")]
    public static partial void Failed(ILogger logger, long character, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the fight's AI stopped, {Reason}")]
    public static partial void Stopped(ILogger logger, long character, Interruption reason);
}
