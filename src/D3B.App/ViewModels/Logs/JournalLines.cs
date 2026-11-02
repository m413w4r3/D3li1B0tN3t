using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Hosting;
using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Protocol.Game;
using D3B.Storage.Profiles;
using Microsoft.Extensions.Logging;

namespace D3B.App.ViewModels.Logs;

/// <summary>Says a bot's journal entries and the host's problems in the app's language, the game's names read from the pack (D112, D114).</summary>
/// <remarks>
/// A map is named by its coordinates, as players give a place, and the sub-area beside it where the place is the point,
/// such as a travel's end; an element by what it is and the skill used. What the pack does not name is named by its id. No
/// line names a message of the protocol: that is the inspector's.
/// </remarks>
internal sealed class JournalLines
{
    private const string Separator = " · ";

    private readonly Localizer _localizer;
    private readonly GameWorld? _world;
    private readonly GameTexts? _texts;
    private readonly GameTables? _tables;
    private readonly GameTextFormatter _says;

    /// <summary>Initializes a new instance of the <see cref="JournalLines"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="world">The pack's world, which names the maps, if the host has one.</param>
    /// <param name="texts">The pack's texts, if the host has them.</param>
    /// <param name="tables">The pack's tables, which name the elements and skills, if the host has them.</param>
    public JournalLines(Localizer localizer, GameWorld? world, GameTexts? texts, GameTables? tables)
    {
        _localizer = localizer;
        _world = world;
        _texts = texts;
        _tables = tables;
        _says = new GameTextFormatter(localizer, world, texts, tables);
    }

    private static Tone ToneOf(bool done)
    {
        return done ? Tone.Success : Tone.Warning;
    }

    /// <summary>Says a bot's journal entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The bot's name, when the page shows every source.</param>
    /// <returns>The line.</returns>
    public LogLineViewModel Of(JournalEntry entry, string? source)
    {
        var (text, detail, tone, mark) = entry switch
        {
            WalkEntry walk => (Format("JournalWalk", walk.Cell), Ended(walk.Outcome.ToString(), walk.Duration), ToneOf(walk.Outcome is WalkOutcome.Arrived), LogMark.Walk),
            MapChangeEntry change => MapChange(change),
            UseEntry use => (Format("JournalUse", ElementName(use), SkillName(use.SkillId)), Ended(use.Outcome.ToString(), use.Duration), ToneOf(use.Outcome is UseOutcome.Used), LogMark.Leaf),
            TravelEntry travel => Travel(travel),
            BankEntry bank => Bank(bank),
            ActivityStartedEntry started => (Format("JournalActivityStarted", started.Activity), null, Tone.Info, LogMark.Play),
            ActivityInterruptedEntry interrupted => (Interrupted(interrupted), null, Tone.Warning, LogMark.Pause),
            ActivityUnloadingEntry unloading => (Format("JournalUnloading", unloading.Activity, unloading.Weight, unloading.MaxWeight), null, Tone.Info, LogMark.Bag),
            ActivityOverEntry over => Over(over),
            ProfileNotStartedEntry profile => (Format("JournalProfileNotStarted", profile.Profile), NotStarted(profile), Tone.Warning, LogMark.Alert),
            BotEventEntry bot => Event(bot.Event),
            FightEndEntry end => FightEnd(end),
            _ => (entry.GetType().Name, null, Tone.Neutral, LogMark.Alert),
        };

        return new LogLineViewModel(entry.At, Time(entry.At), source, text, detail, null, tone, mark, entry.Depth);
    }

    /// <summary>Says a problem of the host, in the host's own words.</summary>
    /// <param name="problem">The problem.</param>
    /// <param name="source">The host's name, when the page shows every source.</param>
    /// <returns>The line.</returns>
    public LogLineViewModel Of(HostProblem problem, string? source)
    {
        var category = problem.Category[(problem.Category.LastIndexOf('.') + 1)..];
        var tone = problem.Level is LogLevel.Warning ? Tone.Warning : Tone.Danger;
        var text = problem.Message.Split('\n', 2)[0].TrimEnd('\r');
        var more = problem.Exception is { } exception ? problem.Message + Environment.NewLine + exception : null;

        return new LogLineViewModel(problem.At, Time(problem.At), source, text, category, more, tone, LogMark.Alert, 0);
    }

    private (string, string?, Tone, LogMark) MapChange(MapChangeEntry change)
    {
        var from = Place(change.From);
        var to = change.To is { } sought ? Place(sought) : "";
        var text = change.Kind switch
        {
            MapChangeKind.Zaap => Format("JournalZaap", change.To is { } zaap ? PlaceNamed(zaap) : to),
            MapChangeKind.HavenBag => _localizer["JournalLeaveHavenBag"],
            MapChangeKind.Door => Format("JournalDoor", from, to),
            MapChangeKind.MapAction => Format("JournalMapAction", from, to),
            MapChangeKind.Npc => Format("JournalNpcWay", from, to),
            _ => Side(change.From, change.To) is { } side ? Format("JournalScroll", from, to, side) : Format("JournalMapChange", from, to),
        };
        var mark = change.Kind switch
        {
            MapChangeKind.Zaap => LogMark.Zaap,
            MapChangeKind.HavenBag => LogMark.Home,
            _ => LogMark.Map,
        };

        return (text, Ended(change.Outcome.ToString(), change.Duration), ToneOf(change.Outcome is MapChangeOutcome.Changed), mark);
    }

    private (string, string?, Tone, LogMark) Travel(TravelEntry travel)
    {
        var text = travel.Sought is { } sought ? Format("JournalTravel", PlaceNamed(sought)) : _localizer["JournalTravelNearest"];
        var detail = string.Join(Separator, _localizer["Outcome" + travel.Outcome], _localizer.Plural("Maps", travel.Changes), Duration(travel.Duration));

        return (text, detail, ToneOf(travel.Outcome is TravelOutcome.Arrived), LogMark.Route);
    }

    private (string, string?, Tone, LogMark) Bank(BankEntry bank)
    {
        var text = bank.Outcome is BankOutcome.Done
            ? Format("JournalBanked", _localizer.Plural("Stacks", bank.Stacks), Format("KamasOf", bank.Kamas))
            : _localizer["JournalBank"];

        return (text, Ended(bank.Outcome.ToString(), bank.Duration), ToneOf(bank.Outcome is BankOutcome.Done), LogMark.Bank);
    }

    private string Interrupted(ActivityInterruptedEntry interrupted)
    {
        // What the activity says of it, such as a fight's kind (D144), names it better than the bare reason.
        var reason = interrupted.Note is { } note ? _says.Say(note) : _localizer[interrupted.Reason switch
        {
            Interruption.Player => "InterruptedByPlayer",
            Interruption.Disconnected => "InterruptedByDisconnection",
            Interruption.Fight => "InterruptedByFight",
            _ => "InterruptedByStop",
        }];
        var key = interrupted.Response switch
        {
            InterruptionResponse.Stop => "JournalActivityInterruptedStops",
            InterruptionResponse.Pause => "JournalActivityInterruptedPauses",
            _ => "JournalActivityInterrupted",
        };

        return Format(key, interrupted.Activity, reason);
    }

    private (string, string?, Tone, LogMark) Over(ActivityOverEntry over)
    {
        var steps = _localizer.Plural("Steps", over.Steps);

        return over.Outcome switch
        {
            ActivityOutcome.Done => (Format("JournalActivityDone", over.Activity), steps, Tone.Success, LogMark.Stop),
            ActivityOutcome.Stopped => (Format("JournalActivityStopped", over.Activity), steps, Tone.Neutral, LogMark.Stop),
            _ when over.StillFull => (Format("JournalActivityStillFull", over.Activity), steps, Tone.Danger, LogMark.Alert),
            _ when over.Error is { } error => (Format("JournalActivityFailedWith", over.Activity, error), steps, Tone.Danger, LogMark.Alert),
            _ => (Format("JournalActivityFailed", over.Activity), steps, Tone.Danger, LogMark.Alert),
        };
    }

    private (string, string?, Tone, LogMark) FightEnd(FightEndEntry end)
    {
        var won = end.Outcome is FightOutcome.Won;
        var gains = string.Join(Separator, Format("JournalExperience", end.Experience), Format("KamasOf", end.Kamas), _localizer.Plural("Objects", end.Objects));

        return (_localizer[won ? "JournalFightWon" : "JournalFightLost"], gains, won ? Tone.Success : Tone.Danger, LogMark.Swords);
    }

    private string? NotStarted(ProfileNotStartedEntry profile)
    {
        return profile.Failure switch
        {
            ProfileStartFailure.ActivityMissing => Format("ActivityMissing", profile.ActivityId),
            ProfileStartFailure.SettingsReset => _localizer["ProfileNeedsReview"],
            ProfileStartFailure.SettingsInvalid => _localizer["SettingsDoNotHold"],
            _ => profile.Error,
        };
    }

    private (string, string?, Tone, LogMark) Event(BotEvent bot)
    {
        return bot switch
        {
            BotEvent.Connected => (_localizer["JournalConnected"], null, Tone.Success, LogMark.Plug),
            BotEvent.Disconnected => (_localizer["JournalDisconnected"], null, Tone.Neutral, LogMark.Plug),
            BotEvent.PausedByPlayer => (_localizer["JournalPaused"], null, Tone.Warning, LogMark.Hand),
            BotEvent.Resumed => (_localizer["JournalResumed"], null, Tone.Info, LogMark.Play),
            BotEvent.PausedAfterFight => (_localizer["JournalPausedAfterFight"], null, Tone.Warning, LogMark.Pause),
            _ => (_localizer["JournalFight"], null, Tone.Warning, LogMark.Swords),
        };
    }

    private string Ended(string outcome, TimeSpan duration)
    {
        return _localizer["Outcome" + outcome] + Separator + Duration(duration);
    }

    private string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1))
            return Format("DurationMilliseconds", (int)duration.TotalMilliseconds);

        return duration < TimeSpan.FromMinutes(1)
            ? Format("DurationSeconds", duration.TotalSeconds)
            : Format("DurationMinutes", (int)duration.TotalMinutes, duration.Seconds);
    }

    private string Time(DateTimeOffset at)
    {
        return at.ToLocalTime().ToString("HH:mm:ss", _localizer.Culture);
    }

    private string Format(string key, params object?[] values)
    {
        return _localizer.Format(key, values);
    }

    private string Place(long mapId)
    {
        return _world is not null && _world.Atlas.TryGetPosition(mapId, out var position)
            ? Format("Coordinates", position.X, position.Y)
            : Format("MapNumber", mapId);
    }

    private string PlaceNamed(long mapId)
    {
        if (_world is null || !_world.Atlas.TryGetPosition(mapId, out var position))
            return Format("MapNumber", mapId);

        var coordinates = Format("Coordinates", position.X, position.Y);

        return _texts is not null && _world.Atlas.TryGetSubArea(position.SubAreaId, out var subArea) && _texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name)
            ? Format("PlaceNamed", name, coordinates)
            : coordinates;
    }

    private string? Side(long from, long? to)
    {
        if (to is not { } toId || _world is null || !_world.Atlas.TryGetPosition(from, out var start) || !_world.Atlas.TryGetPosition(toId, out var end))
            return null;

        // Going north lowers the y coordinate on the world map.
        return (end.X - start.X, end.Y - start.Y) switch
        {
            (0, -1) => _localizer["SideTop"],
            (0, 1) => _localizer["SideBottom"],
            (-1, 0) => _localizer["SideLeft"],
            (1, 0) => _localizer["SideRight"],
            _ => null,
        };
    }

    private string ElementName(UseEntry use)
    {
        if (use.InteractiveId is { } id && _tables is not null && _texts is not null && _tables.Interactives.TryGet(id, out var interactive)
            && _texts.TryGet(_localizer.GameLanguage, interactive.NameId, out var name))
        {
            return name;
        }

        return Format("ElementNumber", use.ElementId);
    }

    private string SkillName(int skillId)
    {
        return _tables is not null && _texts is not null && _tables.Jobs.TryGetSkill(skillId, out var skill) && _texts.TryGet(_localizer.GameLanguage, skill.NameId, out var name)
            ? name
            : Format("SkillNumber", skillId);
    }
}
