using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Hosting;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.ViewModels;

public sealed class MainViewModelTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public void TheWindowSaysTheHostGetsReady()
    {
        var main = new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), _localizer);

        main.Refresh();

        Assert.True(main.IsStarting);
        Assert.False(main.IsRunning);
        Assert.False(main.IsFailed);
        Assert.False(main.IsStopping);
        Assert.Equal("Starting", main.HostTitle);
        Assert.Equal("Getting the pack, the mapping and the relay ready", main.HostDetail);
        Assert.Equal(Tone.Info, main.HostTone);
        Assert.Null(main.HostFailure);
    }

    [Fact]
    public async Task TheWindowSaysWhyTheHostDidNotStart()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack for client 3.7.12.12.")));
        var main = new MainViewModel(runner, _localizer);

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is HostStatus.Failed, TimeSpan.FromSeconds(10));
        main.Refresh();

        Assert.True(main.IsFailed);
        Assert.False(main.IsStarting);
        Assert.Equal("Not started", main.HostTitle);
        Assert.Equal(Tone.Danger, main.HostTone);
        Assert.Equal("No pack for client 3.7.12.12.", main.HostFailure);
    }

    [Fact]
    public async Task TheWindowShowsTheRunningHostsBotsInTheLanguageChosen()
    {
        using var host = TestHosts.Of(_play);
        var main = new MainViewModel(await TestHosts.RunningAsync(host), _localizer);

        await _play.EnterAsync(300);
        main.Refresh();

        Assert.True(main.IsRunning);
        Assert.Equal("Running", main.HostTitle);
        Assert.Equal($"Client {TestInstall.Version}", main.HostDetail);
        Assert.Equal(Tone.Success, main.HostTone);
        Assert.Equal("Level 200", Assert.Single(main.Bots.Items).Level);
        Assert.Equal(1, main.Bots.Count);

        main.Settings.UseFrenchCommand.Execute(null);

        Assert.Equal(Localizer.French, _localizer.Culture);
        Assert.Equal("En marche", main.HostTitle);
        Assert.Equal("Niveau 200", Assert.Single(main.Bots.Items).Level);

        main.Settings.UseEnglishCommand.Execute(null);

        Assert.Equal(Localizer.English, _localizer.Culture);
    }
}
