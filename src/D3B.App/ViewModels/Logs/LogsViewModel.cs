using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Characters;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Logs;

/// <summary>The logs page: a bot's journal, the host's problems, or both by time, filtered by tone and text (D111 to D114).</summary>
/// <remarks>
/// <para>
/// The journals and problems live in the host, in memory (D113): the page reads only what came since its last refresh,
/// every 250 ms on the UI's thread, and says each line once, in the app's language. A new language says them again.
/// </para>
/// <para>
/// The bot it shows is the one the inspector shows too (<see cref="BotChoice"/>): a bot's card opens its journal here.
/// </para>
/// </remarks>
internal sealed partial class LogsViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly BotChoice _choice;
    private readonly Toasts _toasts;
    private readonly List<LogLineViewModel> _lines = [];
    private readonly Dictionary<CharacterKey, long> _readOfBots = [];
    private long _readOfHost;
    private IServiceProvider? _services;
    private JournalLines? _say;
    private CultureInfo? _culture;

    /// <summary>Gets the sources: every one, the host, then each bot in the order the bots page shows them.</summary>
    public ObservableCollection<LogSourceViewModel> Sources { get; } = [];

    /// <summary>Gets the lines the filters let through, oldest first.</summary>
    public ObservableCollection<LogLineViewModel> Lines { get; } = [];

    /// <summary>Gets the source the page shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSources))]
    public partial LogSourceViewModel? Source { get; private set; }

    /// <summary>Gets or sets the source chosen in the page's selector: setting it chooses it, as a click on it does.</summary>
    public LogSourceViewModel? Chosen
    {
        get => Source;
        set
        {
            if (value is not null)
                Choose(value);
        }
    }

    /// <summary>Gets which lines the page shows, by their tone.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllLevels), nameof(IsProblems), nameof(IsErrors))]
    public partial LogLevelFilter Level { get; private set; }

    /// <summary>Gets or sets the text the lines shown say, whatever its case.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = "";

    /// <summary>Gets or sets whether the page scrolls to each new line.</summary>
    [ObservableProperty]
    public partial bool Follow { get; set; } = true;

    /// <summary>Gets whether no line passes the filters.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets how many lines are kept, and how many the filters let through.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets whether each line names its source: the page shows every source.</summary>
    public bool ShowsSources => Source?.Kind is LogSourceKind.All;

    /// <summary>Gets whether the page shows every line.</summary>
    public bool IsAllLevels => Level is LogLevelFilter.All;

    /// <summary>Gets whether the page shows warnings and errors.</summary>
    public bool IsProblems => Level is LogLevelFilter.Problems;

    /// <summary>Gets whether the page shows errors alone.</summary>
    public bool IsErrors => Level is LogLevelFilter.Errors;

    /// <summary>Initializes a new instance of the <see cref="LogsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="choice">The bot the logs page and the inspector look at.</param>
    /// <param name="toasts">What the window says in its toasts, such as the lines copied.</param>
    public LogsViewModel(Localizer localizer, BotChoice choice, Toasts toasts)
    {
        _localizer = localizer;
        _toasts = toasts;
        _choice = choice;
        Sources.Add(new LogSourceViewModel(LogSourceKind.All, null, Choose));
        Sources.Add(new LogSourceViewModel(LogSourceKind.Host, null, Choose));
    }

    /// <summary>Reads the bots and what came since, in the language chosen.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: what was read stays.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _services is null)
        {
            _services = services;
            _say = new JournalLines(_localizer, services.GetService<GameWorld>(), services.GetService<GameTexts>(), services.GetService<GameTables>());
        }

        Sources[0].Name = _localizer["LogsEverything"];
        Sources[1].Name = _localizer["LogsHost"];
        RefreshBots(services?.GetService<Bots>());

        if (Source is null)
            Choose(_choice.Key is { } key && Find(key) is { } chosen ? chosen : Sources[0]);

        if (!_localizer.Culture.Equals(_culture))
            Reload();
        else
            ReadMore();

        RefreshSummary();
    }

    /// <summary>Shows a bot's journal, as its card asks.</summary>
    /// <param name="key">The bot's character.</param>
    public void Show(CharacterKey key)
    {
        _choice.Key = key;

        if (Find(key) is { } source)
            Choose(source);
    }

    /// <summary>Gets the lines shown, as text, for the clipboard.</summary>
    /// <returns>A line each.</returns>
    public string Text()
    {
        return string.Join(Environment.NewLine, Lines);
    }

    partial void OnSearchChanged(string value)
    {
        Filter();
    }

    [RelayCommand]
    private void ShowAllLevels()
    {
        Level = LogLevelFilter.All;
        Filter();
    }

    [RelayCommand]
    private void ShowProblems()
    {
        Level = LogLevelFilter.Problems;
        Filter();
    }

    [RelayCommand]
    private void ShowErrors()
    {
        Level = LogLevelFilter.Errors;
        Filter();
    }

    private void RefreshBots(Bots? bots)
    {
        if (bots is null)
            return;

        foreach (var bot in bots.All.OrderBy(bot => bot.Key.AccountId).ThenBy(bot => bot.Key.CharacterId))
        {
            if (Find(bot.Key) is null)
                Sources.Add(new LogSourceViewModel(LogSourceKind.Bot, bot, Choose));
        }

        foreach (var source in Sources)
        {
            if (source.Bot is not { } bot)
                continue;

            source.Name = NameOf(bot);
            source.Tone = bot.IsPaused ? Tone.Warning : bot.IsConnected ? Tone.Success : Tone.Neutral;
        }
    }

    private void Choose(LogSourceViewModel source)
    {
        foreach (var other in Sources)
            other.IsSelected = other == source;

        if (source.Bot is { } bot)
            _choice.Key = bot.Key;

        if (Source == source)
            return;

        Source = source;
        Reload();
        RefreshSummary();
    }

    private void Reload()
    {
        _culture = _localizer.Culture;
        _lines.Clear();
        _readOfBots.Clear();
        _readOfHost = 0;
        Lines.Clear();
        ReadMore();
    }

    private void ReadMore()
    {
        if (_say is null || _services is null || Source is not { } source)
            return;

        var fresh = new List<LogLineViewModel>();
        var named = source.Kind is LogSourceKind.All;

        if (source.Kind is not LogSourceKind.Bot && _services.GetService<HostProblems>() is { } host)
        {
            foreach (var problem in host.Problems.After(ref _readOfHost))
                fresh.Add(_say.Of(problem, named ? _localizer["LogsHost"] : null));
        }

        IEnumerable<BotActor> bots = source.Kind switch
        {
            LogSourceKind.Bot => [source.Bot!],
            LogSourceKind.All => [.. Sources.Where(other => other.Bot is not null).Select(other => other.Bot!)],
            _ => [],
        };

        foreach (var bot in bots)
        {
            var read = _readOfBots.GetValueOrDefault(bot.Key);
            var name = named ? NameOf(bot) : null;

            foreach (var entry in bot.Journal.After(ref read))
                fresh.Add(_say.Of(entry, name));

            _readOfBots[bot.Key] = read;
        }

        if (fresh.Count is 0)
            return;

        // Sources read one after another give lines of the same moments: each refresh's come in time's order.
        var ordered = named ? fresh.OrderBy(line => line.At).ToList() : fresh;

        _lines.AddRange(ordered);

        foreach (var line in ordered)
        {
            if (Passes(line))
                Lines.Add(line);
        }

        Trim();
    }

    private void Trim()
    {
        // What the host no longer keeps, the page lets go of too.
        var most = Source?.Kind switch
        {
            LogSourceKind.Bot => BotActor.JournalCapacity,
            LogSourceKind.Host => HostProblems.Capacity,
            _ => HostProblems.Capacity + (BotActor.JournalCapacity * Sources.Count(source => source.IsBot)),
        };

        if (_lines.Count <= most)
            return;

        var dropped = _lines.Take(_lines.Count - most).ToHashSet();

        _lines.RemoveRange(0, dropped.Count);

        while (Lines.Count > 0 && dropped.Contains(Lines[0]))
            Lines.RemoveAt(0);
    }

    private void Filter()
    {
        Lines.Clear();

        foreach (var line in _lines.Where(Passes))
            Lines.Add(line);

        RefreshSummary();
    }

    private bool Passes(LogLineViewModel line)
    {
        var level = Level switch
        {
            LogLevelFilter.Problems => line.IsProblem,
            LogLevelFilter.Errors => line.IsError,
            _ => true,
        };

        return level && line.Says(Search.Trim());
    }

    /// <summary>Says the lines shown were copied.</summary>
    public void Copied()
    {
        _toasts.Show(new Toast(_localizer.Plural("LinesCopied", Lines.Count), Tone.Success));
    }

    private void RefreshSummary()
    {
        IsEmpty = Lines.Count is 0;
        Summary = Lines.Count == _lines.Count
            ? _localizer.Plural("LinesKept", _lines.Count)
            : _localizer.Format("LinesShown", _localizer.Plural("Lines", Lines.Count), _lines.Count);
    }

    private LogSourceViewModel? Find(CharacterKey key)
    {
        return Sources.FirstOrDefault(source => source.Bot?.Key == key);
    }

    private string NameOf(BotActor bot)
    {
        return bot.State.Name.Length > 0 ? bot.State.Name : _localizer.Format("CharacterNamed", bot.Key.CharacterId);
    }

    partial void OnSourceChanged(LogSourceViewModel? value)
    {
        OnPropertyChanged(nameof(Chosen));
    }
}
