using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Logs;
using D3B.Bot.Characters;
using D3B.Bot.Inspection;
using D3B.Bot.Runtime;
using D3B.Network.Captures;
using D3B.Network.Relay;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Inspector;

/// <summary>The protocol inspector: a bot's game frames decoded as they go, or a capture's replayed, filtered by sender and name (D111, D115).</summary>
/// <remarks>
/// <para>
/// Live, it reads the frames the host records for each bot (<see cref="FrameRecorder"/>), what came since its last refresh,
/// unless frozen. A capture of the game connection, from the captures' <c>game</c> folder, is read whole through the
/// installed build's translation (<see cref="CaptureFrames"/>); one of another build is refused, and says why.
/// </para>
/// <para>
/// The bot it shows is the one the logs page shows too (<see cref="BotChoice"/>). The ticket is hidden wherever it shows.
/// </para>
/// </remarks>
internal sealed partial class InspectorViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly BotChoice _choice;
    private readonly List<FrameRowViewModel> _rows = [];
    private IServiceProvider? _services;
    private long _read;
    private bool _capturesRead;
    private bool _choosing;
    private CultureInfo? _culture;

    /// <summary>Gets the bots whose frames the inspector shows live.</summary>
    public ObservableCollection<LogSourceViewModel> Bots { get; } = [];

    /// <summary>Gets the captures of the game connection, the last written first.</summary>
    public ObservableCollection<CaptureChoiceViewModel> Captures { get; } = [];

    /// <summary>Gets the frames the filters let through, oldest first.</summary>
    public ObservableCollection<FrameRowViewModel> Frames { get; } = [];

    /// <summary>Gets the bot whose frames the inspector shows live, if it shows a bot's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLive))]
    public partial LogSourceViewModel? Bot { get; private set; }

    /// <summary>Gets or sets the source chosen in the page's selector: setting it chooses it, as a click on it does.</summary>
    public LogSourceViewModel? Chosen
    {
        get => Bot;
        set
        {
            if (value is not null)
                Choose(value);
        }
    }

    /// <summary>Gets or sets the capture the inspector replays, if it replays one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLive))]
    public partial CaptureChoiceViewModel? Capture { get; set; }

    /// <summary>Gets why the capture chosen could not be read, if it could not.</summary>
    [ObservableProperty]
    public partial string? CaptureProblem { get; private set; }

    /// <summary>Gets or sets whether the client's frames show.</summary>
    [ObservableProperty]
    public partial bool ShowsClient { get; set; } = true;

    /// <summary>Gets or sets whether the server's frames show.</summary>
    [ObservableProperty]
    public partial bool ShowsServer { get; set; } = true;

    /// <summary>Gets or sets whether D3B's own frames show.</summary>
    [ObservableProperty]
    public partial bool ShowsD3B { get; set; } = true;

    /// <summary>Gets or sets whether frames the readable protocol does not read are hidden: three of four, measured (D58).</summary>
    [ObservableProperty]
    public partial bool HidesUnread { get; set; }

    /// <summary>Gets or sets the text the frames shown name, whatever its case.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = "";

    /// <summary>Gets or sets whether the list scrolls to each new frame.</summary>
    [ObservableProperty]
    public partial bool Follow { get; set; } = true;

    /// <summary>Gets or sets whether new frames wait, live, until it thaws.</summary>
    [ObservableProperty]
    public partial bool IsFrozen { get; set; }

    /// <summary>Gets whether no frame passes the filters.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether no bot has been made, so that nothing shows live.</summary>
    [ObservableProperty]
    public partial bool HasNoBot { get; private set; } = true;

    /// <summary>Gets how many frames there are, and how many the filters let through.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets whether the inspector shows a bot's frames as they go, rather than a capture's.</summary>
    public bool IsLive => Capture is null;

    /// <summary>Initializes a new instance of the <see cref="InspectorViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="choice">The bot the logs page and the inspector look at.</param>
    public InspectorViewModel(Localizer localizer, BotChoice choice)
    {
        _localizer = localizer;
        _choice = choice;
    }

    private static string GameCaptures(IServiceProvider services)
    {
        // Captures may be off now and read later: the folder they go to when on.
        var directory = services.GetService<RelaySettings>()?.CapturesDirectory
            ?? services.GetService<IConfiguration>()?[RelaySettings.CapturesDirectoryKey]
            ?? RelaySettings.DefaultCapturesDirectory;

        return Path.Combine(directory, "game");
    }

    /// <summary>Reads the bots, the captures and the frames that came since.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: what was read stays.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null)
            _services = services;

        RefreshBots(services?.GetService<Bots>());

        if (!_capturesRead)
            ReadCaptures();

        // Rows say who sent a frame and how far it was read in the language they were made in.
        if (!_localizer.Culture.Equals(_culture))
            Translate();

        // The bot chosen on the logs page, or on a card, is the one shown live.
        if (IsLive && _choice.Key is { } key && Bot?.Bot?.Key != key && Find(key) is { } chosen)
            Choose(chosen);
        else if (IsLive && Bot is null && Bots.Count > 0)
            Choose(Bots[0]);

        if (IsLive && !IsFrozen)
            ReadMore();

        RefreshSummary();
    }

    partial void OnCaptureChanged(CaptureChoiceViewModel? value)
    {
        if (value is null || _choosing)
            return;

        foreach (var bot in Bots)
            bot.IsSelected = false;

        Bot = null;
        Load(value);
    }

    partial void OnShowsClientChanged(bool value)
    {
        Filter();
    }

    partial void OnShowsServerChanged(bool value)
    {
        Filter();
    }

    partial void OnShowsD3BChanged(bool value)
    {
        Filter();
    }

    partial void OnHidesUnreadChanged(bool value)
    {
        Filter();
    }

    partial void OnSearchChanged(string value)
    {
        Filter();
    }

    [RelayCommand]
    private void Clear()
    {
        _rows.Clear();
        Frames.Clear();
        RefreshSummary();
    }

    private void RefreshBots(Bots? bots)
    {
        if (bots is null)
            return;

        foreach (var bot in bots.All.OrderBy(bot => bot.Key.AccountId).ThenBy(bot => bot.Key.CharacterId))
        {
            if (Find(bot.Key) is null)
                Bots.Add(new LogSourceViewModel(LogSourceKind.Bot, bot, Choose));
        }

        foreach (var source in Bots)
        {
            var bot = source.Bot!;

            source.Name = bot.State.Name.Length > 0 ? bot.State.Name : _localizer.Format("CharacterNamed", bot.Key.CharacterId);
            source.Tone = bot.IsPaused ? Tone.Warning : bot.IsConnected ? Tone.Success : Tone.Neutral;
        }

        HasNoBot = Bots.Count is 0;
    }

    /// <summary>Reads the captures' folder again, as its list opens: the relay may have written one since.</summary>
    public void ReadCaptures()
    {
        if (_services is null)
            return;

        _capturesRead = true;

        var directory = GameCaptures(_services);
        var files = Directory.Exists(directory)
            ? new DirectoryInfo(directory).EnumerateFiles("*.d3bcap").OrderByDescending(file => file.LastWriteTime).ToList()
            : [];

        if (files.Count == Captures.Count && files.Select(file => file.FullName).SequenceEqual(Captures.Select(capture => capture.Path)))
            return;

        var chosen = Capture?.Path;

        _choosing = true;
        Captures.Clear();

        foreach (var file in files)
        {
            var label = _localizer.Format("CaptureLabel", file.LastWriteTime.ToString("g", _localizer.Culture), _localizer.Format("Kilobytes", Math.Max(1, file.Length / 1024)));

            Captures.Add(new CaptureChoiceViewModel(file.FullName, file.LastWriteTime, label));
        }

        Capture = Captures.FirstOrDefault(capture => capture.Path == chosen);
        _choosing = false;
    }

    private void Translate()
    {
        _culture = _localizer.Culture;

        for (var index = 0; index < _rows.Count; index++)
            _rows[index] = new FrameRowViewModel(_rows[index].Frame, _localizer, StopFollowing);

        Filter();
    }

    private void Choose(LogSourceViewModel source)
    {
        foreach (var other in Bots)
            other.IsSelected = other == source;

        _choice.Key = source.Bot?.Key;

        if (Bot == source && IsLive)
            return;

        _choosing = true;
        Capture = null;
        _choosing = false;
        CaptureProblem = null;
        Bot = source;
        _read = 0;
        _rows.Clear();
        Frames.Clear();
        ReadMore();
        RefreshSummary();
    }

    private void ReadMore()
    {
        if (Bot?.Bot is not { } bot || _services?.GetService<FrameRecorder>() is not { } recorder || !recorder.TryGet(bot.Key, out var recorded))
            return;

        var fresh = recorded.After(ref _read);

        foreach (var frame in fresh)
        {
            var row = new FrameRowViewModel(frame, _localizer, StopFollowing);

            _rows.Add(row);

            if (Passes(row))
                Frames.Add(row);
        }

        // What the host no longer keeps, the page lets go of too.
        if (_rows.Count > FrameRecorder.Capacity)
        {
            var dropped = _rows.Take(_rows.Count - FrameRecorder.Capacity).ToHashSet();

            _rows.RemoveRange(0, dropped.Count);

            while (Frames.Count > 0 && dropped.Contains(Frames[0]))
                Frames.RemoveAt(0);
        }
    }

    private void Load(CaptureChoiceViewModel capture)
    {
        _rows.Clear();
        Frames.Clear();
        CaptureProblem = null;

        try
        {
            var translation = _services?.GetService<ProtocolTranslation>()
                ?? throw new InvalidOperationException(_localizer["CaptureNoTranslation"]);
            var game = CaptureFrames.Read(capture.Path, translation);

            foreach (var frame in game.Frames)
                _rows.Add(new FrameRowViewModel(frame, _localizer, StopFollowing));

            if (game.IsTruncated)
                CaptureProblem = _localizer["CaptureTruncated"];
        }
        catch (Exception exception) when (exception is CaptureFormatException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            CaptureProblem = exception.Message;
        }

        Filter();
    }

    private void Filter()
    {
        Frames.Clear();

        foreach (var row in _rows.Where(Passes))
            Frames.Add(row);

        RefreshSummary();
    }

    private bool Passes(FrameRowViewModel row)
    {
        var sender = row.Frame.Source switch
        {
            FrameSource.Client => ShowsClient,
            FrameSource.Server => ShowsServer,
            _ => ShowsD3B,
        };

        return sender && (!HidesUnread || row.IsRead) && row.Names(Search.Trim());
    }

    private void RefreshSummary()
    {
        IsEmpty = Frames.Count is 0;
        Summary = Frames.Count == _rows.Count
            ? _localizer.Plural("Frames", _rows.Count)
            : _localizer.Format("FramesShown", _localizer.Plural("Frames", Frames.Count), _rows.Count.ToString("N0", _localizer.Culture));
    }

    private LogSourceViewModel? Find(CharacterKey key)
    {
        return Bots.FirstOrDefault(source => source.Bot?.Key == key);
    }

    private void StopFollowing()
    {
        // A frame unfolded is being read: the frames that come must not scroll it away.
        Follow = false;
    }

    partial void OnBotChanged(LogSourceViewModel? value)
    {
        OnPropertyChanged(nameof(Chosen));
    }
}
