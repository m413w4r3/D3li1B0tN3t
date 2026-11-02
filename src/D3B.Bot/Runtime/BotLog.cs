using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Data.World;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>The bots' log messages, each naming its bot by its character's id.</summary>
internal static partial class BotLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plays through game connection {ConnectionId}")]
    public static partial void Attached(ILogger logger, long character, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: game connection {ConnectionId} closed")]
    public static partial void Detached(ILogger logger, long character, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the player sent {Request}, the bot pauses until it is resumed")]
    public static partial void PausedByPlayer(ILogger logger, long character, string request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: resumed")]
    public static partial void Resumed(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: paused after a fight, until it is resumed")]
    public static partial void PausedAfterFight(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: its behavior was interrupted, {Reason}")]
    public static partial void Interrupted(ILogger logger, long character, Interruption reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: a message could not be handled")]
    public static partial void HandlerFailed(ILogger logger, long character, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Game connection {ConnectionId}: its account is not known, so no bot plays its character")]
    public static partial void AccountUnknown(ILogger logger, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walks from cell {From} to cell {To}, {Steps} steps")]
    public static partial void Walking(ILogger logger, long character, int from, int to, int steps);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walk to cell {To} over, {Outcome}")]
    public static partial void Walked(ILogger logger, long character, int to, WalkOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves map {From} for map {To} by a {Transition}, from cell {Cell}")]
    public static partial void ChangingMap(ILogger logger, long character, long from, long to, WorldTransitionType transition, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: teleports from map {From} to the zaap of map {To}")]
    public static partial void Teleporting(ILogger logger, long character, long from, long to);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves map {From} for map {To} through the dialog of NPC {Npc}")]
    public static partial void TalkingThrough(ILogger logger, long character, long from, long to, int npc);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves the haven bag of {Owner}")]
    public static partial void LeavingHavenBag(ILogger logger, long character, long owner);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: map change over on map {Map}, {Outcome}")]
    public static partial void ChangedMap(ILogger logger, long character, long map, MapChangeOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: uses element {Element} with skill {Skill}, from cell {Cell}")]
    public static partial void Using(ILogger logger, long character, int element, int skill, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: use of element {Element} with skill {Skill} over, {Outcome}")]
    public static partial void Used(ILogger logger, long character, int element, int skill, UseOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: travels from map {From}, {Changes} map changes away")]
    public static partial void Travelling(ILogger logger, long character, long from, int changes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: journey over on map {Map} after {Changes} map changes, {Outcome}")]
    public static partial void Travelled(ILogger logger, long character, long map, int changes, TravelOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: no banker stands in the bank's inside {Map}, which the host's bots no longer seek")]
    public static partial void NoBanker(ILogger logger, long character, long map);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: talks to banker {Npc} on map {Map}")]
    public static partial void OpeningBank(ILogger logger, long character, long map, int npc);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: opening the bank on map {Map} over, {Outcome}")]
    public static partial void OpenedBank(ILogger logger, long character, long map, BankOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: {Taken} of {Stacks} stacks and {Kamas} kamas put in the bank, {Outcome}")]
    public static partial void Deposited(ILogger logger, long character, int stacks, int taken, long kamas, BankOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: banking over on map {Map}, {Outcome}")]
    public static partial void Banked(ILogger logger, long character, long map, BankOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: a fight began, which interrupts its behavior")]
    public static partial void FightBegan(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} starts")]
    public static partial void ActivityStarted(ILogger logger, long character, string activity);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} interrupted, {Reason}; it will {Response}")]
    public static partial void ActivityInterrupted(ILogger logger, long character, string activity, Interruption reason, InterruptionResponse response);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} unloads the character, {Weight} of {MaxWeight} pods")]
    public static partial void ActivityUnloading(ILogger logger, long character, string activity, int weight, int maxWeight);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: activity {Activity} left the character full after unloading it")]
    public static partial void ActivityStillFull(ILogger logger, long character, string activity);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: activity {Activity} failed")]
    public static partial void ActivityFailed(ILogger logger, long character, string activity, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} over after {Steps} steps, {Outcome}")]
    public static partial void ActivityOver(ILogger logger, long character, string activity, int steps, ActivityOutcome outcome);
}
