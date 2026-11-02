using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Localization;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Storage.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Statistics;

/// <summary>The statistics page: what the runs D3B kept brought, by character and period (D146).</summary>
/// <remarks>
/// Four figures, the resources, the time, the rate an hour and the levels gained; each resource with its icon and rate; and
/// the runs, the latest first. Made again only once the runs, the filters or the language changed.
/// </remarks>
internal sealed partial class StatisticsViewModel : ObservableObject
{
    private RunBook? _book;
    private GameTextFormatter? _says;
    private GameIcons? _icons;
    private TimeProvider _time = TimeProvider.System;
    private int _version = -1;
    private CultureInfo? _culture;
    private CharacterKey? _character;
    private bool _changed = true;
    private bool _rebuilding;

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets every character the runs name, every one first.</summary>
    public ObservableCollection<StatisticsCharacterViewModel> Characters { get; } = [];

    /// <summary>Gets the resources the runs counted brought, the most first.</summary>
    public ObservableCollection<StatisticsResourceViewModel> Resources { get; } = [];

    /// <summary>Gets the runs counted, the latest first.</summary>
    public ObservableCollection<StatisticsRunViewModel> Runs { get; } = [];

    /// <summary>Gets which runs the page counts, by when they began.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToday), nameof(IsWeek), nameof(IsAll))]
    public partial StatisticsPeriod Period { get; private set; } = StatisticsPeriod.Today;

    /// <summary>Gets or sets the character whose runs are counted, or every one.</summary>
    [ObservableProperty]
    public partial StatisticsCharacterViewModel? SelectedCharacter { get; set; }

    /// <summary>Gets how many resources the runs brought.</summary>
    [ObservableProperty]
    public partial string ResourcesValue { get; private set; } = "";

    /// <summary>Gets how long the runs ran.</summary>
    [ObservableProperty]
    public partial string TimeValue { get; private set; } = "";

    /// <summary>Gets how many resources an hour.</summary>
    [ObservableProperty]
    public partial string RateValue { get; private set; } = "";

    /// <summary>Gets how many levels the professions gained.</summary>
    [ObservableProperty]
    public partial string LevelsValue { get; private set; } = "";

    /// <summary>Gets which professions gained levels, from and to.</summary>
    [ObservableProperty]
    public partial string LevelsDetail { get; private set; } = "";

    /// <summary>Gets whether the host keeps runs: its database opened.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets whether no run is counted.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether the page counts today's runs.</summary>
    public bool IsToday => Period is StatisticsPeriod.Today;

    /// <summary>Gets whether the page counts the last 7 days' runs.</summary>
    public bool IsWeek => Period is StatisticsPeriod.Week;

    /// <summary>Gets whether the page counts every run.</summary>
    public bool IsAll => Period is StatisticsPeriod.All;

    /// <summary>Initializes a new instance of the <see cref="StatisticsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    public StatisticsViewModel(Localizer localizer)
    {
        Localizer = localizer;
    }

    /// <summary>Reads the runs again, in the language chosen.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _book is null)
        {
            _book = services.GetService<RunBook>();
            _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
            _says = new GameTextFormatter(Localizer, services.GetService<GameWorld>(), services.GetService<GameTexts>(), services.GetService<GameTables>());
            _icons = GameIcons.Of(services.GetService<GameTables>());
            IsAvailable = _book is not null;
        }

        if (_book is null || (!_changed && _book.Version == _version && Localizer.Culture.Equals(_culture)))
            return;

        _version = _book.Version;
        _culture = Localizer.Culture;
        _changed = false;
        Rebuild(_book.Runs);
    }

    private static long TotalOf(RunRecord run)
    {
        return run.Items.Values.Sum();
    }

    private static TimeSpan DurationOf(RunRecord run)
    {
        var duration = (run.EndedAt ?? run.LastAt) - run.StartedAt;

        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    private void Rebuild(IReadOnlyList<RunRecord> all)
    {
        RebuildCharacters(all);

        var now = _time.GetLocalNow();
        var runs = all.Where(run => (_character is not { } key || (run.AccountId == key.AccountId && run.CharacterId == key.CharacterId)) && IsIn(run, now)).ToList();
        var culture = Localizer.Culture;
        var total = runs.Sum(TotalOf);
        var time = TimeSpan.FromTicks(runs.Sum(run => DurationOf(run).Ticks));
        var hours = time.TotalHours;

        IsEmpty = runs.Count is 0;
        ResourcesValue = total.ToString("N0", culture);
        TimeValue = DurationText(time);
        RateValue = hours >= 1.0 / 60 ? Localizer.Format("StatisticsRate", Math.Round(total / hours).ToString("N0", culture)) : Localizer["StatisticsNone"];

        var jobs = runs.SelectMany(run => run.Jobs).GroupBy(job => job.JobId).Select(job => (JobId: job.Key, From: job.Min(gained => gained.FromLevel), To: job.Max(gained => gained.ToLevel))).Where(job => job.To > job.From).ToList();

        var levels = jobs.Sum(job => job.To - job.From);

        LevelsValue = Localizer.Format(levels == 1 ? "StatisticsLevelsOne" : "StatisticsLevels", levels);
        LevelsDetail = string.Join(", ", jobs.Select(job => Localizer.Format("StatisticsLevelsOf", _says?.Name(GameTextValue.Job(job.JobId)) ?? job.JobId.ToString(culture), job.From, job.To)));

        Resources.Clear();

        var counts = runs.SelectMany(run => run.Items).GroupBy(pair => pair.Key).Select(pair => (Item: pair.Key, Count: pair.Sum(each => each.Value))).OrderByDescending(pair => pair.Count).ThenBy(pair => pair.Item).ToList();
        var most = counts.Count > 0 ? counts[0].Count : 0;

        foreach (var (item, count) in counts)
        {
            var rate = hours >= 1.0 / 60 ? Localizer.Format("StatisticsRate", Math.Round(count / hours).ToString("N0", culture)) : null;

            Resources.Add(new StatisticsResourceViewModel(_icons?.Item(item), _says?.Item(item) ?? item.ToString(culture), count.ToString("N0", culture), rate, most > 0 ? 100.0 * count / most : 0));
        }

        Runs.Clear();

        // A run names who ran what once, above the runs that follow it of the same.
        string? previous = null;

        foreach (var run in runs)
        {
            var row = RunOf(run, now, previous);

            previous = row.Who;
            Runs.Add(row);
        }
    }

    private void RebuildCharacters(IReadOnlyList<RunRecord> all)
    {
        _rebuilding = true;

        try
        {
            Characters.Clear();
            Characters.Add(new StatisticsCharacterViewModel(null, Localizer["StatisticsEveryCharacter"], Select) { IsSelected = _character is null });

            foreach (var character in all.GroupBy(run => new CharacterKey(run.AccountId, run.CharacterId)).OrderBy(group => group.First().CharacterName, StringComparer.Create(Localizer.Culture, true)))
                Characters.Add(new StatisticsCharacterViewModel(character.Key, character.First().CharacterName, Select) { IsSelected = _character == character.Key });

            SelectedCharacter = Characters.FirstOrDefault(character => character.IsSelected);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private bool IsIn(RunRecord run, DateTimeOffset now)
    {
        var began = run.StartedAt.ToOffset(now.Offset);

        return Period switch
        {
            StatisticsPeriod.Today => began.Date == now.Date,
            StatisticsPeriod.Week => now - began <= TimeSpan.FromDays(7),
            _ => true,
        };
    }

    private StatisticsRunViewModel RunOf(RunRecord run, DateTimeOffset now, string? previous)
    {
        var culture = Localizer.Culture;
        var began = run.StartedAt.ToOffset(now.Offset);
        var when = began.Date == now.Date ? began.ToString("t", culture) : began.ToString("g", culture);
        var (outcome, tone) = run.Outcome switch
        {
            ActivityOutcome.Done => (Localizer["ActivityDone"], Tone.Success),
            ActivityOutcome.Failed => (Localizer["ActivityFailed"], Tone.Danger),
            ActivityOutcome.Stopped => (Localizer["ActivityStopped"], Tone.Neutral),
            _ => (Localizer["StatisticsOngoing"], Tone.Info),
        };
        var who = run.Title is { Length: > 0 } title ? $"{run.CharacterName} · {title}" : run.CharacterName;

        // A run stopped or done ended as runs do: only one going on, or failed, says so.
        return new StatisticsRunViewModel(when, who, who != previous, DurationText(DurationOf(run)), TotalOf(run).ToString("N0", culture), outcome, tone, run.Outcome is not (ActivityOutcome.Done or ActivityOutcome.Stopped));
    }

    private string DurationText(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
            return Localizer.Format("ElapsedSeconds", (int)duration.TotalSeconds);

        return duration < TimeSpan.FromHours(1)
            ? Localizer.Format("ElapsedMinutes", (int)duration.TotalMinutes)
            : Localizer.Format("ElapsedHours", (int)duration.TotalHours, duration.Minutes);
    }

    private void Select(StatisticsCharacterViewModel character)
    {
        _character = character.Key;
        _changed = true;
        Refresh(null);
    }

    private void Show(StatisticsPeriod period)
    {
        Period = period;
        _changed = true;
        Refresh(null);
    }

    partial void OnSelectedCharacterChanged(StatisticsCharacterViewModel? value)
    {
        if (!_rebuilding && value is not null && value.Key != _character)
            Select(value);
    }

    [RelayCommand]
    private void ShowToday()
    {
        Show(StatisticsPeriod.Today);
    }

    [RelayCommand]
    private void ShowWeek()
    {
        Show(StatisticsPeriod.Week);
    }

    [RelayCommand]
    private void ShowAll()
    {
        Show(StatisticsPeriod.All);
    }
}
