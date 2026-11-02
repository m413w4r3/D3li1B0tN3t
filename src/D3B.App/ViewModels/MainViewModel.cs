using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.Updates;
using D3B.App.ViewModels.Inspector;
using D3B.App.ViewModels.Logs;
using D3B.App.ViewModels.Statistics;
using D3B.App.ViewModels.Maps;
using D3B.Bot.Characters;
using D3B.Bot.Pages;
using D3B.Data;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The app's window: where the bots' host stands, its pages in the sidebar, its dialogs and toasts (D14, D91, D96, D101, D111, D120, D151, D159).</summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private readonly HostRunner _host;
    private readonly Localizer _localizer;
    private readonly AppPreferences? _preferences;
    private bool _pagesRead;

    /// <summary>Gets the dialog the window shows over its pages.</summary>
    public Dialogs Dialogs { get; } = new();

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; } = new();

    /// <summary>Gets the bots page.</summary>
    public BotsViewModel Bots { get; }

    /// <summary>Gets the accounts page.</summary>
    public AccountsViewModel Accounts { get; }

    /// <summary>Gets the profiles page.</summary>
    public ProfilesViewModel Profiles { get; }

    /// <summary>Gets the map page.</summary>
    public MapViewModel Map { get; }

    /// <summary>Gets the statistics page.</summary>
    public StatisticsViewModel Statistics { get; }

    /// <summary>Gets the logs page.</summary>
    public LogsViewModel Logs { get; }

    /// <summary>Gets the protocol inspector.</summary>
    public InspectorViewModel Inspector { get; }

    /// <summary>Gets the settings page, at the rail's foot, whose theme the window follows.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Gets the app's version and its update, in the window's banner and on the settings page (D173).</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Gets the pages the host's modules registered, in the rail after the app's own, once the host runs.</summary>
    public ObservableCollection<ModulePageViewModel> Pages { get; } = [];

    /// <summary>Gets whether the bots page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsBotsPage { get; private set; } = true;

    /// <summary>Gets whether the accounts page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsAccountsPage { get; private set; }

    /// <summary>Gets whether the profiles page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsProfilesPage { get; private set; }

    /// <summary>Gets whether the map page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsMapPage { get; private set; }

    /// <summary>Gets whether the statistics page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsStatisticsPage { get; private set; }

    /// <summary>Gets whether the logs page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsLogsPage { get; private set; }

    /// <summary>Gets whether the protocol inspector is the page the window shows.</summary>
    [ObservableProperty]
    public partial bool IsInspectorPage { get; private set; }

    /// <summary>Gets whether the settings page is the one the window shows, whether the host runs or not.</summary>
    [ObservableProperty]
    public partial bool IsSettingsPage { get; private set; }

    /// <summary>Gets the module's page the window shows, if it shows one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModulePage))]
    public partial ModulePageViewModel? SelectedPage { get; private set; }

    /// <summary>Gets whether any module registered a page.</summary>
    [ObservableProperty]
    public partial bool HasPages { get; private set; }

    /// <summary>Gets where the host stands, in a word.</summary>
    [ObservableProperty]
    public partial string HostTitle { get; private set; } = "";

    /// <summary>Gets what the host runs for, or what it does now.</summary>
    [ObservableProperty]
    public partial string HostDetail { get; private set; } = "";

    /// <summary>Gets the tone of where the host stands.</summary>
    [ObservableProperty]
    public partial Tone HostTone { get; private set; }

    /// <summary>Gets whether the host gets ready.</summary>
    [ObservableProperty]
    public partial bool IsStarting { get; private set; }

    /// <summary>Gets whether the host runs: its bots are shown.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets whether the host stops, or stopped.</summary>
    [ObservableProperty]
    public partial bool IsStopping { get; private set; }

    /// <summary>Gets whether the host did not start.</summary>
    [ObservableProperty]
    public partial bool IsFailed { get; private set; }

    /// <summary>Gets why the host did not start, and what to do about it, once it failed.</summary>
    [ObservableProperty]
    public partial string? HostFailure { get; private set; }

    /// <summary>Gets whether the installed app did not start for a client build it does not ship, which a newer version does (D172).</summary>
    [ObservableProperty]
    public partial bool IsMissingBuild { get; private set; }

    /// <summary>Gets what the window says of the client build the installed app does not ship.</summary>
    [ObservableProperty]
    public partial string MissingBuildText { get; private set; } = "";

    /// <summary>Gets or sets the page the sidebar marks: <c>bots</c>, <c>accounts</c>, <c>settings</c> and the others, or a module's page's route.</summary>
    [ObservableProperty]
    public partial string CurrentRoute { get; set; } = "bots";

    /// <summary>Gets whether the sidebar shows its pages' names, or their icons alone.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarToggleText), nameof(SidebarToggleIcon))]
    public partial bool IsSidebarExpanded { get; private set; } = true;

    /// <summary>Gets what the sidebar's button does, said.</summary>
    public string SidebarToggleText => _localizer[IsSidebarExpanded ? "CollapseSidebar" : "ExpandSidebar"];

    /// <summary>Gets the icon of the sidebar's button, by its name among the app's.</summary>
    public string SidebarToggleIcon => IsSidebarExpanded ? "SidebarClose" : "SidebarOpen";

    /// <summary>Gets whether a module's page is the one the window shows.</summary>
    public bool IsModulePage => SelectedPage is not null;

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="host">The bots' host in the app's process.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="preferences">What the app remembers of its own, its language, theme and the host's settings, or <see langword="null"/> to remember nothing.</param>
    /// <param name="updates">The app's updates, or <see langword="null"/> for an app that never updates.</param>
    /// <param name="close">Closes the app, as restarting it to install its update does.</param>
    public MainViewModel(HostRunner host, Localizer localizer, AppPreferences? preferences = null, AppUpdates? updates = null, Action? close = null)
    {
        _host = host;
        _localizer = localizer;
        _preferences = preferences;

        IsSidebarExpanded = !(preferences?.IsSidebarCollapsed ?? false);

        var choice = new BotChoice();

        Bots = new BotsViewModel(localizer, ShowJournalOf, ShowMapOf, Toasts);
        Accounts = new AccountsViewModel(localizer, Dialogs);
        Profiles = new ProfilesViewModel(localizer, Dialogs, Toasts);
        Map = new MapViewModel(localizer, choice);
        Statistics = new StatisticsViewModel(localizer);
        Logs = new LogsViewModel(localizer, choice, Toasts);
        Inspector = new InspectorViewModel(localizer, choice);
        Updates = new UpdatesViewModel(localizer, updates, Dialogs, () => IsRunning && Bots.HasItems, close ?? (() => { }));
        Settings = new SettingsViewModel(localizer, preferences, UseLanguage, Toasts, Updates);
    }

    /// <summary>Reads the host and its bots again, in the language chosen.</summary>
    public void Refresh()
    {
        var status = _host.Status;
        var services = _host.Services;

        IsStarting = status is HostStatus.Starting;
        IsRunning = status is HostStatus.Running;
        IsStopping = status is HostStatus.Stopping or HostStatus.Stopped;
        IsFailed = status is HostStatus.Failed;
        HostFailure = IsFailed ? _host.Failure : null;
        Updates.Refresh();

        // Run from its code, the app says what the maintainer does: extract and map the build; installed, that it updates.
        IsMissingBuild = IsFailed && _host.MissingBuild is not null && Updates.IsInstalled;
        MissingBuildText = IsMissingBuild ? _localizer.Format("MissingBuildText", _host.MissingBuild, Updates.Version, Updates.ClientVersion) : "";
        OnPropertyChanged(nameof(SidebarToggleText));
        (HostTitle, HostTone) = status switch
        {
            HostStatus.Starting => (_localizer["HostStatusStarting"], Tone.Info),
            HostStatus.Running => (_localizer["HostStatusRunning"], Tone.Success),
            HostStatus.Failed => (_localizer["HostStatusFailed"], Tone.Danger),
            HostStatus.Stopping => (_localizer["HostStatusStopping"], Tone.Warning),
            _ => (_localizer["HostStatusStopped"], Tone.Neutral),
        };
        HostDetail = status switch
        {
            HostStatus.Starting => _localizer["HostPreparing"],
            HostStatus.Running => _localizer.Format("HostClient", services?.GetService<ClientBuild>()?.Version ?? "?"),
            HostStatus.Failed => _localizer["HostFailedDetail"],
            _ => "",
        };

        // The profiles first: the bots' cards offer them, and the accounts name them.
        Profiles.Refresh(services);
        Accounts.Refresh(services);
        Bots.Refresh(services);

        // Only the page shown reads what came since: the others read it all once shown again.
        if (IsMapPage)
            Map.Refresh(services);

        if (IsStatisticsPage)
            Statistics.Refresh(services);

        if (IsLogsPage)
            Logs.Refresh(services);

        if (IsInspectorPage)
            Inspector.Refresh(services);

        // Always: the bots' delays the page shows are read once the host runs, whichever page shows.
        Settings.Refresh(services);
        RefreshPages(services);
    }

    /// <summary>Shows the page of a route, as the sidebar names it.</summary>
    /// <param name="route">The route: <c>bots</c>, <c>accounts</c> and the others, or a module's page's.</param>
    public void Navigate(string route)
    {
        ArgumentNullException.ThrowIfNull(route);

        switch (route)
        {
            case "bots":
                ShowBots();
                break;
            case "accounts":
                ShowAccounts();
                break;
            case "profiles":
                ShowProfiles();
                break;
            case "map":
                ShowMap();
                break;
            case "statistics":
                ShowStatistics();
                break;
            case "logs":
                ShowLogs();
                break;
            case "inspector":
                ShowInspector();
                break;
            case "settings":
                ShowSettings();
                break;
            default:
                Pages.FirstOrDefault(page => page.Route == route)?.ShowCommand.Execute(null);
                break;
        }
    }

    /// <summary>Replaces the module's page whose own code threw what nothing else caught on the UI's thread.</summary>
    /// <param name="exception">What was thrown.</param>
    /// <returns>Whether a page's code threw it, and the page now shows it instead: else the app's own fault, not handled.</returns>
    public bool FailPageThatThrew(Exception exception)
    {
        return Pages.Any(page => page.TryFailWith(exception));
    }

    private void RefreshPages(IServiceProvider? services)
    {
        // The catalogue is the host's: read once the host runs, and kept after it stops, as the bots shown are.
        if (!_pagesRead && IsRunning && services is not null)
        {
            foreach (var definition in services.GetService<PageCatalog>()?.All ?? [])
                Pages.Add(new ModulePageViewModel(definition, services, _localizer, Select));

            _pagesRead = true;
            HasPages = Pages.Count > 0;
        }

        foreach (var page in Pages)
            page.Refresh();
    }

    [RelayCommand]
    private void ShowBots()
    {
        Select(null);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        _preferences?.RememberSidebar(!IsSidebarExpanded);
    }

    [RelayCommand]
    private void ShowAccounts()
    {
        Select(null);
        IsBotsPage = false;
        IsAccountsPage = true;
        CurrentRoute = "accounts";
    }

    [RelayCommand]
    private void ShowProfiles()
    {
        Select(null);
        IsBotsPage = false;
        IsProfilesPage = true;
        CurrentRoute = "profiles";
    }

    [RelayCommand]
    private void ShowMap()
    {
        Select(null);
        IsBotsPage = false;
        IsMapPage = true;
        CurrentRoute = "map";
        Map.Refresh(_host.Services);
    }

    [RelayCommand]
    private void ShowStatistics()
    {
        Select(null);
        IsBotsPage = false;
        IsStatisticsPage = true;
        CurrentRoute = "statistics";
        Statistics.Refresh(_host.Services);
    }

    [RelayCommand]
    private void ShowLogs()
    {
        Select(null);
        IsBotsPage = false;
        IsLogsPage = true;
        CurrentRoute = "logs";
        Logs.Refresh(_host.Services);
    }

    [RelayCommand]
    private void ShowInspector()
    {
        Select(null);
        IsBotsPage = false;
        IsInspectorPage = true;
        CurrentRoute = "inspector";
        Inspector.Refresh(_host.Services);
    }

    [RelayCommand]
    private void ShowSettings()
    {
        Select(null);
        IsBotsPage = false;
        IsSettingsPage = true;
        CurrentRoute = "settings";
        Settings.Refresh(_host.Services);
    }

    private void ShowJournalOf(CharacterKey key)
    {
        ShowLogs();
        Logs.Show(key);
    }

    private void ShowMapOf(CharacterKey key)
    {
        ShowMap();
        Map.Show(key);
    }

    private void Select(ModulePageViewModel? page)
    {
        foreach (var other in Pages)
            other.IsSelected = other == page;

        SelectedPage = page;
        CurrentRoute = page?.Route ?? "bots";
        IsBotsPage = page is null;
        IsAccountsPage = false;
        IsProfilesPage = false;
        IsMapPage = false;
        IsStatisticsPage = false;
        IsLogsPage = false;
        IsInspectorPage = false;
        IsSettingsPage = false;
        page?.Refresh();
    }

    private void UseLanguage(CultureInfo culture)
    {
        _localizer.Use(culture);
        _preferences?.RememberLanguage(_localizer.Culture.TwoLetterISOLanguageName);
        Refresh();
    }
}
