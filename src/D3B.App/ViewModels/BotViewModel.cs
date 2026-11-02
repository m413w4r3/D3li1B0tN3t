using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>A bot as the app shows it: its character's state, its activity, and the commands that start, stop and resume it (D87).</summary>
/// <remarks>
/// It reads the bot's snapshots each time the window refreshes (D9), on the UI's thread: the bot's loop never waits for
/// the app.
/// </remarks>
internal sealed partial class BotViewModel : ObservableObject
{
    private const string NoValue = "—";

    /// <summary>How full a character shows as nearly full, a hint only: each activity says when it unloads (D87).</summary>
    private const double NearlyFull = 0.9;

    private readonly IServiceProvider _services;
    private readonly Localizer _localizer;
    private readonly GameWorld? _world;
    private readonly GameTexts? _texts;
    private readonly ProfileBook? _profiles;
    private readonly AccountBook? _accounts;
    private readonly ProfileStarter? _starter;
    private readonly Action<CharacterKey>? _showJournal;
    private readonly Action<CharacterKey>? _showMap;
    private readonly Toasts? _toasts;
    private readonly GameTextFormatter _says;
    private readonly GameIcons? _icons;
    private readonly TimeProvider _time;
    private int _profilesVersion = -1;
    private ActivityRun? _startedRun;
    private ActivityChoiceViewModel? _startedChoice;
    private ActivityReport? _shownReport;
    private CultureInfo? _shownCulture;
    private bool _seen;
    private bool _wasPaused;
    private bool _wasConnected;
    private bool _wasFighting;
    private ActivityRun? _runningSeen;

    /// <summary>Gets the bot.</summary>
    public BotActor Bot { get; }

    /// <summary>Gets what the bot can be given: the profiles, by name, then the activities with their defaults (D99).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivities))]
    public partial IReadOnlyList<ActivityChoiceViewModel> Activities { get; private set; } = [];

    /// <summary>Gets whether anything can be given.</summary>
    public bool HasActivities => Activities.Count > 0;

    /// <summary>Gets whether the card opens the bot's journal on the logs page.</summary>
    public bool HasJournal => _showJournal is not null;

    /// <summary>Gets whether the card opens the bot's map on the map page.</summary>
    public bool HasMap => _showMap is not null;

    /// <summary>Gets the character's name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets the first letters of the character's name, for its avatar.</summary>
    [ObservableProperty]
    public partial string Initials { get; private set; } = "";

    /// <summary>Gets the character's level, when the server gave it.</summary>
    [ObservableProperty]
    public partial string Level { get; private set; } = "";

    /// <summary>Gets whether a game connection plays the character.</summary>
    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    /// <summary>Gets whether a game connection plays the character, said.</summary>
    [ObservableProperty]
    public partial string Connection { get; private set; } = "";

    /// <summary>Gets the tone of the connection.</summary>
    [ObservableProperty]
    public partial Tone ConnectionTone { get; private set; }

    /// <summary>Gets whether the player took the character over, so that the bot waits to be resumed (D66).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    public partial bool IsPaused { get; private set; }

    /// <summary>Gets the tone of what the bot is up to, for the dot on its avatar.</summary>
    [ObservableProperty]
    public partial Tone Tone { get; private set; }

    /// <summary>Gets the coordinates of the character's map on the world map.</summary>
    [ObservableProperty]
    public partial string Coordinates { get; private set; } = NoValue;

    /// <summary>Gets the sub-area of the character's map and its cell, or where it is when no coordinates say it.</summary>
    [ObservableProperty]
    public partial string Place { get; private set; } = "";

    /// <summary>Gets the character's map and cell, by their ids.</summary>
    [ObservableProperty]
    public partial string? MapDetail { get; private set; }

    /// <summary>Gets what the character is in the middle of.</summary>
    [ObservableProperty]
    public partial string Doing { get; private set; } = "";

    /// <summary>Gets the character's life, of its most, its regeneration reckoned, once the server said it (D148, D153).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLife))]
    public partial string? Life { get; private set; }

    /// <summary>Gets the character's life, from 0 to 100 of its most.</summary>
    [ObservableProperty]
    public partial double LifePercent { get; private set; }

    /// <summary>Gets the weight the character carries, of the most it can.</summary>
    [ObservableProperty]
    public partial string Load { get; private set; } = NoValue;

    /// <summary>Gets how full the character is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double LoadPercent { get; private set; }

    /// <summary>Gets how full the character is, said.</summary>
    [ObservableProperty]
    public partial string LoadPercentText { get; private set; } = "";

    /// <summary>Gets whether the server said the character's life.</summary>
    public bool HasLife => Life is not null;

    /// <summary>Gets whether the character is nearly full.</summary>
    [ObservableProperty]
    public partial bool IsNearlyFull { get; private set; }

    /// <summary>Gets the kamas the character carries.</summary>
    [ObservableProperty]
    public partial string Kamas { get; private set; } = NoValue;

    /// <summary>Gets whether the bot runs an activity, or ran one.</summary>
    [ObservableProperty]
    public partial bool HasRun { get; private set; }

    /// <summary>Gets the name of the activity the bot runs, or ran last.</summary>
    [ObservableProperty]
    public partial string ActivityName { get; private set; } = "";

    /// <summary>Gets what the activity does, or how it ended.</summary>
    [ObservableProperty]
    public partial string ActivityText { get; private set; } = "";

    /// <summary>Gets the tone of what the activity does, or of how it ended.</summary>
    [ObservableProperty]
    public partial Tone ActivityTone { get; private set; }

    /// <summary>Gets the activity's steps and last interruption.</summary>
    [ObservableProperty]
    public partial string ActivityDetail { get; private set; } = "";

    /// <summary>Gets the error that made the activity fail, as it threw it, or why it says it could not go on; empty otherwise.</summary>
    [ObservableProperty]
    public partial string ActivityProblem { get; private set; } = "";

    /// <summary>Gets what the activity says it does, if it reports it (D131).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDoing))]
    public partial string ActivityDoing { get; private set; } = "";

    /// <summary>Gets how far the activity says it is, such as its tour's map.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>Gets how far the activity says it is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets how long the activity has run.</summary>
    [ObservableProperty]
    public partial string Elapsed { get; private set; } = "";

    /// <summary>Gets what the activity counts, such as each resource harvested.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCounts))]
    public partial IReadOnlyList<ActivityCountViewModel> Counts { get; private set; } = [];

    /// <summary>Gets what the activity warns of, a line each.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>Gets whether the activity says what it does.</summary>
    public bool HasDoing => ActivityDoing.Length > 0;

    /// <summary>Gets whether the activity says how far it is.</summary>
    public bool HasProgress => ProgressText.Length > 0;

    /// <summary>Gets whether the activity counts something.</summary>
    public bool HasCounts => Counts.Count > 0;

    /// <summary>Gets whether the activity warns of something.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Gets whether the bot runs an activity that is not over.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    public partial bool IsActivityRunning { get; private set; }

    /// <summary>Gets whether an activity can be chosen and started: none runs, and some exist.</summary>
    [ObservableProperty]
    public partial bool IsChoosing { get; private set; }

    /// <summary>Gets whether no activity runs and none exists to start.</summary>
    [ObservableProperty]
    public partial bool HasNothingToChoose { get; private set; }

    /// <summary>Gets or sets the activity to start.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial ActivityChoiceViewModel? SelectedActivity { get; set; }

    /// <summary>Gets why the last start failed, if it did.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="BotViewModel"/> class.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="services">The host's services: the activities, the profiles, and the pack's world and texts.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="showJournal">Shows the bot's journal on the logs page, or <see langword="null"/> when no page shows it.</param>
    /// <param name="showMap">Shows the bot's map on the map page, or <see langword="null"/> when no page shows it.</param>
    /// <param name="toasts">What the window says in its toasts, of what the bot meets, or <see langword="null"/> to say nothing.</param>
    public BotViewModel(BotActor bot, IServiceProvider services, Localizer localizer, Action<CharacterKey>? showJournal = null, Action<CharacterKey>? showMap = null, Toasts? toasts = null)
    {
        Bot = bot;
        _toasts = toasts;
        _showJournal = showJournal;
        _showMap = showMap;
        _services = services;
        _localizer = localizer;
        _world = services.GetService<GameWorld>();
        _texts = services.GetService<GameTexts>();
        _profiles = services.GetService<ProfileBook>();
        _accounts = services.GetService<AccountBook>();
        _starter = services.GetService<ProfileStarter>();
        _says = new GameTextFormatter(localizer, _world, _texts, services.GetService<GameTables>());
        _icons = GameIcons.Of(services.GetService<GameTables>());
        _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        RefreshChoices();
    }

    private static bool Same(ActivityChoiceViewModel choice, ActivityChoiceViewModel other)
    {
        return choice.Profile is { } profile ? profile.Id == other.Profile?.Id : other.Profile is null && choice.Definition?.Id == other.Definition?.Id;
    }

    /// <summary>Gets what a character is in the middle of, as the key of the app's text that says it.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>The text's key.</returns>
    internal static string DoingOf(CharacterState state)
    {
        if (state.IsFighting)
            return "Fighting";

        if (state.IsInDialog)
            return "InDialog";

        if (state.IsUsing)
            return "Using";

        return state.IsWalking ? "Walking" : "Idle";
    }

    private static (string Key, Tone Tone) StatusOf(ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.Waiting => ("ActivityWaiting", Tone.Warning),
            ActivityStatus.Stepping => ("ActivityStepping", Tone.Success),
            ActivityStatus.Unloading => ("ActivityUnloading", Tone.Info),
            ActivityStatus.Recovering => ("ActivityRecovering", Tone.Info),
            _ => ("ActivityEnding", Tone.Neutral),
        };
    }

    private static (string Key, Tone Tone) OutcomeOf(ActivityOutcome outcome)
    {
        return outcome switch
        {
            ActivityOutcome.Done => ("ActivityDone", Tone.Success),
            ActivityOutcome.Failed => ("ActivityFailed", Tone.Danger),
            _ => ("ActivityStopped", Tone.Neutral),
        };
    }

    private static string InterruptionOf(Interruption interruption)
    {
        return interruption switch
        {
            Interruption.Player => "InterruptedByPlayer",
            Interruption.Disconnected => "InterruptedByDisconnection",
            Interruption.Fight => "InterruptedByFight",
            _ => "InterruptedByStop",
        };
    }

    /// <summary>Reads the bot again, in the language chosen.</summary>
    public void Refresh()
    {
        var state = Bot.State;

        Name = state.Name.Length > 0 ? state.Name : _localizer.Format("CharacterNamed", Bot.Key.CharacterId);
        Initials = AvatarInitials.Of(Name);
        Level = state.Level is { } level ? _localizer.Format("Level", level) : "";
        IsConnected = Bot.IsConnected;
        Connection = _localizer[IsConnected ? "Connected" : "Disconnected"];
        ConnectionTone = IsConnected ? Tone.Success : Tone.Neutral;
        IsPaused = Bot.IsPaused;
        Doing = IsConnected ? _localizer[DoingOf(state)] : "";
        Kamas = _localizer.Format("KamasOf", state.Kamas);

        if ((_profiles?.Version ?? 0) != _profilesVersion)
            RefreshChoices();

        foreach (var activity in Activities)
            activity.Refresh(_localizer.Culture);

        RefreshPlace(state);
        RefreshLife(state);
        RefreshLoad(state);
        RefreshActivity();
        RefreshTone();
        Announce(state);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (SelectedActivity is not { } choice)
            return;

        if (choice.Profile is { } profile)
            StartProfile(choice, profile);
        else if (choice.Definition is { } definition)
            StartActivity(choice, definition);

        Refresh();
    }

    private void StartProfile(ActivityChoiceViewModel choice, Profile profile)
    {
        if (_starter is null)
            return;

        var start = _starter.Start(Bot, profile);

        Error = start.Failure switch
        {
            null => null,
            ProfileStartFailure.ActivityMissing => _localizer.Format("ProfileActivityMissing", profile.Name),
            ProfileStartFailure.SettingsReset => _localizer.Format("ProfileToReview", profile.Name),
            ProfileStartFailure.SettingsInvalid => _localizer.Format("ProfileInvalid", profile.Name),
            _ => _localizer.Format("StartFailed", start.Error),
        };

        if (start.Run is { } run)
        {
            _startedRun = run;
            _startedChoice = choice;
        }
    }

    private void StartActivity(ActivityChoiceViewModel choice, ActivityDefinition definition)
    {
        var settings = definition.Settings.CreateDefault();

        // A value the user must give has no default: such an activity runs from a profile.
        if (definition.Settings.Check(settings, _services.GetService<GameReferences>()).Count > 0)
        {
            Error = _localizer.Format("ActivityNeedsProfile", choice.Name);

            return;
        }

        try
        {
            _startedRun = Bot.StartActivity(definition.Create(_services, settings));
            _startedChoice = choice;
            Error = null;
        }
        catch (Exception exception)
        {
            // An activity may fail as it is made; the bot may already run one, or be stopped with the host.
            Error = _localizer.Format("StartFailed", exception.Message);
        }
    }

    private void RefreshChoices()
    {
        var catalog = _services.GetService<ActivityCatalog>();
        var culture = _localizer.Culture;
        var selected = SelectedActivity;
        var choices = new List<ActivityChoiceViewModel>();

        _profilesVersion = _profiles?.Version ?? 0;

        foreach (var profile in _profiles?.Profiles ?? [])
        {
            ActivityDefinition? definition = null;

            catalog?.TryGet(profile.ActivityId, out definition);
            choices.Add(new ActivityChoiceViewModel(profile, definition, culture, language => definition?.Name.In(language) ?? _localizer.Format("ActivityMissing", profile.ActivityId)));
        }

        foreach (var definition in catalog?.All ?? [])
            choices.Add(new ActivityChoiceViewModel(definition, culture, _ => definition.Settings.IsEmpty ? null : _localizer["DefaultSettings"]));

        var assigned = _accounts?.Find(Bot.Key)?.ProfileId;

        Activities = choices;
        SelectedActivity = choices.FirstOrDefault(choice => selected is not null && Same(choice, selected))
            ?? choices.FirstOrDefault(choice => assigned is not null && choice.Profile?.Id == assigned)
            ?? (choices.Count == 1 ? choices[0] : null);
    }

    private bool CanStart()
    {
        return SelectedActivity is not null && !IsActivityRunning;
    }

    [RelayCommand(CanExecute = nameof(IsActivityRunning))]
    private void Stop()
    {
        Bot.Activity?.Stop();
    }

    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void Resume()
    {
        Bot.Resume();
    }

    [RelayCommand]
    private void ShowJournal()
    {
        _showJournal?.Invoke(Bot.Key);
    }

    [RelayCommand]
    private void ShowMap()
    {
        _showMap?.Invoke(Bot.Key);
    }

    private void RefreshPlace(CharacterState state)
    {
        if (state.MapId is not { } mapId)
        {
            Coordinates = NoValue;
            Place = _localizer["NotPlaced"];
            MapDetail = null;

            return;
        }

        var cell = state.Cell?.ToString(_localizer.Culture);

        MapDetail = _localizer.Format("MapCell", mapId, cell ?? NoValue);

        // A haven bag and a fight's map are off the atlas (D83, D88).
        if (state.IsInHavenBag || _world is null || !_world.Atlas.TryGetPosition(mapId, out var position))
        {
            Coordinates = NoValue;
            Place = _localizer[state.IsInHavenBag ? "InHavenBag" : "OffAtlas"];

            return;
        }

        var area = SubAreaName(position.SubAreaId);

        Coordinates = _localizer.Format("Coordinates", position.X, position.Y);
        Place = cell is null ? area : _localizer.Format("AreaCell", area, cell);
    }

    private void RefreshLife(CharacterState state)
    {
        if (Bot.LifeAt(_time.GetUtcNow()) is not { } life || state.MaxLife is not { } most || most <= 0)
        {
            Life = null;
            LifePercent = 0;

            return;
        }

        Life = _localizer.Format("LifeOf", life, most);
        LifePercent = Math.Clamp(life * 100.0 / most, 0, 100);
    }

    private void RefreshLoad(CharacterState state)
    {
        if (state.MaxWeight <= 0)
        {
            Load = NoValue;
            LoadPercent = 0;
            LoadPercentText = "";
            IsNearlyFull = false;

            return;
        }

        Load = _localizer.Format("LoadOf", state.Weight, state.MaxWeight);
        LoadPercent = Math.Clamp(state.Load * 100, 0, 100);
        LoadPercentText = _localizer.Format("LoadPercent", state.Load);
        IsNearlyFull = state.Load >= NearlyFull;
    }

    private void RefreshActivity()
    {
        var run = Bot.Activity;

        IsActivityRunning = run is { IsOver: false };
        IsChoosing = !IsActivityRunning && HasActivities;
        HasNothingToChoose = !IsActivityRunning && !HasActivities;
        HasRun = run is not null;

        if (run is null)
        {
            ActivityName = "";
            ActivityText = "";
            ActivityTone = Tone.Neutral;
            ActivityDetail = "";
            ActivityProblem = "";
            RefreshReport(null, null);

            return;
        }

        var (key, tone) = run.IsOver ? OutcomeOf(run.Completion.GetAwaiter().GetResult()) : StatusOf(run.Status);
        var steps = _localizer.Plural("Steps", run.Steps);

        // The name the app started it under, a profile's or an activity's; one started by other code, such as a profile on
        // connection, by the name it was given, else by its own.
        ActivityName = run == _startedRun && _startedChoice is { } choice ? choice.Name : run.Title ?? run.Activity.Name;
        ActivityText = _localizer[key];
        ActivityTone = tone;
        ActivityDetail = run.LastInterruption is { } interruption
            ? _localizer.Format("StepsInterrupted", steps, _localizer[InterruptionOf(interruption)])
            : steps;

        ActivityProblem = !run.IsOver ? ""
            : run.Failure is ActivityException { Reason: { } reason } ? _says.Say(reason)
            : run.IsStillFull ? _localizer["ActivityLeftFull"]
            : run.Failure?.Message ?? "";
        RefreshReport(run, run.Activity.Report);
    }

    private void Announce(CharacterState state)
    {
        var run = Bot.Activity;
        var running = run is { IsOver: false };

        // Only what changed under the app's eyes is said: a bot met for the first time already paused or done says nothing.
        if (_seen && _toasts is { } toasts)
        {
            if (run is { IsOver: true } && run == _runningSeen)
            {
                var outcome = run.Completion.GetAwaiter().GetResult();
                var (key, tone) = run.IsStillFull ? ("ToastStillFull", Tone.Warning)
                    : outcome is ActivityOutcome.Failed ? ("ToastFailed", Tone.Danger)
                    : outcome is ActivityOutcome.Done ? ("ToastDone", Tone.Success)
                    : (null, Tone.Neutral);

                if (key is not null)
                    toasts.Show(new Toast(_localizer.Format(key, Name), tone, _localizer["ShowJournal"], () => _showJournal?.Invoke(Bot.Key)));
            }

            if (IsPaused && !_wasPaused)
                toasts.Show(new Toast(_localizer.Format("ToastPaused", Name), Tone.Warning));

            if (!IsConnected && _wasConnected && (running || _runningSeen is not null))
                toasts.Show(new Toast(_localizer.Format("ToastDisconnected", Name), Tone.Warning, _localizer["ShowJournal"], () => _showJournal?.Invoke(Bot.Key)));

            if (state.IsFighting && !_wasFighting && running)
                toasts.Show(new Toast(_localizer.Format("ToastFight", Name), Tone.Warning, _localizer["ShowMap"], () => _showMap?.Invoke(Bot.Key)));
        }

        _seen = true;
        _wasPaused = IsPaused;
        _wasConnected = IsConnected;
        _wasFighting = state.IsFighting;
        _runningSeen = running ? run : null;
    }

    private void RefreshReport(ActivityRun? run, ActivityReport? report)
    {
        Elapsed = run is null ? "" : ElapsedOf((run.EndedAt ?? _time.GetUtcNow()) - run.StartedAt);

        if (ReferenceEquals(report, _shownReport) && _localizer.Culture.Equals(_shownCulture))
            return;

        _shownReport = report;
        _shownCulture = _localizer.Culture;

        // An activity over says how it ended: what it said it did is no longer true.
        ActivityDoing = report is null || run is { IsOver: true } ? "" : _says.Say(report.Doing);
        ProgressText = report?.Progress is { } progress && run is { IsOver: false } ? _says.Say(progress.Text) : "";
        ProgressPercent = report?.Progress is { Total: > 0 } shown ? Math.Clamp(100.0 * shown.Done / shown.Total, 0, 100) : 0;
        Counts = report is null ? [] : [.. report.Counts.Select(count => new ActivityCountViewModel(_says.Say(count.Text), count.ItemId is not null, count.ItemId is { } item ? _icons?.Item(item) : null))];
        Warnings = report is null || run is { IsOver: true } ? [] : [.. report.Warnings.Select(_says.Say)];
    }

    private string ElapsedOf(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed < TimeSpan.FromMinutes(1))
            return _localizer.Format("ElapsedSeconds", (int)elapsed.TotalSeconds);

        return elapsed < TimeSpan.FromHours(1)
            ? _localizer.Format("ElapsedMinutes", (int)elapsed.TotalMinutes)
            : _localizer.Format("ElapsedHours", (int)elapsed.TotalHours, elapsed.Minutes);
    }

    private void RefreshTone()
    {
        if (IsPaused)
            Tone = Tone.Warning;
        else if (!IsConnected)
            Tone = Tone.Neutral;
        else
            Tone = IsActivityRunning ? ActivityTone : Tone.Info;
    }

    private string SubAreaName(int subAreaId)
    {
        if (_world is not null && _texts is not null && _world.Atlas.TryGetSubArea(subAreaId, out var subArea) && _texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name))
            return name;

        return _localizer.Format("SubArea", subAreaId);
    }
}
