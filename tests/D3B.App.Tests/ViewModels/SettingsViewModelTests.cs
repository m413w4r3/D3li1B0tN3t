using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Network.Relay;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class SettingsViewModelTests : IAsyncDisposable
{
    // The page is tested on the bots' delays as measured, the wait between two steps included.
    private readonly TestPlay _play = new() { Bots = { Delays = BotDelays.Measured } };
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly List<IHost> _hosts = [];

    private string PreferencesPath => _directory.Combine("app.json");

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
            host.Dispose();

        await _play.DisposeAsync();
        _directory.Dispose();
    }

    private IHost TestHost()
    {
        var host = TestHosts.Of(_play);

        _hosts.Add(host);

        return host;
    }

    private async Task<MainViewModel> RunningAsync(AppPreferences preferences)
    {
        var main = new MainViewModel(await TestHosts.RunningAsync(TestHost()), _localizer, preferences);

        main.Refresh();

        return main;
    }

    [Fact]
    public async Task TheBotsWaitTheDelaysChangedOnThePageAtOnceAndTheAppRemembersThemForTheHost()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var settings = (await RunningAsync(preferences)).Settings;
        var answer = settings.BotWaits[0];

        Assert.Equal(0.5m, settings.Display.Value);
        Assert.Equal("Measured: 0.5 s", settings.Display.DefaultText);
        Assert.Equal("Up to 3 s", settings.Display.MostText);
        Assert.False(settings.HasRaisedDelays);

        settings.Display.Value = 1.5m;
        answer.Value = 6m;

        Assert.Equal(BotDelays.Measured with { Display = TimeSpan.FromSeconds(1.5), Answer = TimeSpan.FromSeconds(6) }, _play.Bots.Delays);
        Assert.True(answer.IsRaised);
        Assert.True(settings.HasRaisedDelays);
        Assert.Equal("1.5 s", settings.Display.ValueText);
        Assert.Equal(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "1.5", [BotDelays.AnswerKey] = "6" }, AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public async Task ADelayIsNeverLoweredBelowItsDefaultNorRaisedPastItsMost()
    {
        var settings = (await RunningAsync(AppPreferences.Load(PreferencesPath))).Settings;

        settings.Display.Value = 0.1m;

        Assert.Equal(BotDelays.Measured, _play.Bots.Delays);

        settings.Display.Value = 9m;

        Assert.Equal(BotDelays.Most.Display, _play.Bots.Delays.Display);
        Assert.Equal((0.5m, 3m, 0.1m), (settings.Display.Minimum, settings.Display.Maximum, settings.Display.Increment));
    }

    [Fact]
    public async Task RestoringTheDelaysGivesBackTheDefaultsAndForgetsThemButKeepsTheDisplayTime()
    {
        var preferences = AppPreferences.Load(PreferencesPath);
        var settings = (await RunningAsync(preferences)).Settings;
        var hold = settings.RelayWaits[2];

        settings.Display.Value = 2m;
        settings.BotWaits[0].Value = 6m;
        hold.Value = 10m;

        Assert.Equal("Default: 5 s", hold.DefaultText);
        Assert.True(settings.RestoreDelaysCommand.CanExecute(null));

        settings.RestoreDelaysCommand.Execute(null);

        Assert.Equal(BotDelays.Measured with { Display = TimeSpan.FromSeconds(2) }, _play.Bots.Delays);
        Assert.Equal((3m, 5m), (settings.BotWaits[0].Value, hold.Value));
        Assert.False(settings.RestoreDelaysCommand.CanExecute(null));
        Assert.Equal(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "2" }, AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public async Task ThePageShowsTheDelaysTheBotsWaitOnceTheHostRuns()
    {
        var preferences = AppPreferences.Load(PreferencesPath);

        preferences.RememberHost(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "1", [RelayTimeouts.ConnectKey] = "20" });

        // As the host's command line would have raised it.
        _play.Bots.Delays = BotDelays.Measured with { Display = TimeSpan.FromSeconds(2.5) };

        var settings = (await RunningAsync(preferences)).Settings;

        Assert.Equal(2.5m, settings.Display.Value);
        Assert.Equal(20m, settings.RelayWaits[0].Value);
        Assert.Equal(TimeSpan.FromSeconds(2.5), _play.Bots.Delays.Display);
    }

    [Fact]
    public void ADelayChangedBeforeTheHostRanIsGivenToItsBots()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer, AppPreferences.Load(PreferencesPath));
        var settings = main.Settings;

        main.Refresh();
        settings.Display.Value = 1m;
        Assert.Equal("Known once the host runs", settings.PackPath);

        settings.Refresh(TestHost().Services);

        Assert.Equal(TimeSpan.FromSeconds(1), _play.Bots.Delays.Display);
        Assert.Equal(1m, settings.Display.Value);
    }

    [Fact]
    public void AFileEditedToADelayOutOfItsBoundsLeavesThePageOnTheDefaults()
    {
        var preferences = AppPreferences.Load(PreferencesPath);

        preferences.RememberHost(new Dictionary<string, string?> { [BotDelays.DisplayKey] = "0.1", [RelayTimeouts.HoldKey] = "600" });

        var settings = new SettingsViewModel(_localizer, AppPreferences.Load(PreferencesPath), _ => { }, new Toasts());

        Assert.Equal(0.5m, settings.Display.Value);
        Assert.Equal(5m, settings.RelayWaits[2].Value);
    }

    [Fact]
    public async Task CapturesAreRememberedForTheNextStart()
    {
        var settings = (await RunningAsync(AppPreferences.Load(PreferencesPath))).Settings;

        Assert.False(settings.RecordsCaptures);
        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);

        settings.RecordsCaptures = true;

        Assert.Equal("True", AppPreferences.Load(PreferencesPath).Host[RelaySettings.CapturesEnabledKey]);
        Assert.True(new SettingsViewModel(_localizer, AppPreferences.Load(PreferencesPath), _ => { }, new Toasts()).RecordsCaptures);

        settings.RecordsCaptures = false;

        Assert.Empty(AppPreferences.Load(PreferencesPath).Host);
    }

    [Fact]
    public async Task TheLanguageAndThemeChangeFromThePageAsFromTheTitleBar()
    {
        var main = await RunningAsync(AppPreferences.Load(PreferencesPath));
        var settings = main.Settings;

        Assert.True(settings.IsEnglish);
        Assert.True(settings.IsDark);

        settings.UseFrenchCommand.Execute(null);
        settings.UseLightCommand.Execute(null);

        Assert.True(settings.IsFrench);
        Assert.False(settings.IsDark);
        Assert.Equal("Temps d'affichage", settings.Display.Label);
        Assert.Equal("Mesuré : 0,5 s", settings.Display.DefaultText);
        Assert.Equal(("fr", AppPreferences.Light), (AppPreferences.Load(PreferencesPath).Language, AppPreferences.Load(PreferencesPath).Theme));

        settings.UseEnglishCommand.Execute(null);
        settings.UseDarkCommand.Execute(null);

        Assert.True(settings.IsEnglish);
        Assert.True(settings.IsDark);
        Assert.Equal("Display time", settings.Display.Label);
    }

    [Fact]
    public async Task TheSettingsPageShowsWhetherTheHostRunsOrNot()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);

        main.ShowSettingsCommand.Execute(null);

        Assert.True(main.IsSettingsPage);
        Assert.False(main.IsBotsPage);

        main.ShowBotsCommand.Execute(null);

        Assert.False(main.IsSettingsPage);

        var running = await RunningAsync(AppPreferences.Load(PreferencesPath));

        running.ShowSettingsCommand.Execute(null);
        running.ShowLogsCommand.Execute(null);

        Assert.False(running.IsSettingsPage);
        Assert.True(running.IsLogsPage);
    }
}
