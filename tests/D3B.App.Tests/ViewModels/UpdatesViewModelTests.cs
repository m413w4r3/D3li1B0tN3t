using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.Tests.Updates;
using D3B.App.Updates;
using D3B.App.ViewModels;
using D3B.Bot.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class UpdatesViewModelTests
{
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly Dialogs _dialogs = new();
    private int _closed;

    [Fact]
    public async Task AVersionDownloadedIsSaidAndOffersARestart()
    {
        var updates = await ReadyAsync(new FakeUpdateClient { Published = "0.2.0" });
        var view = View(updates, botsPlay: false);

        Assert.True(view.IsReady);
        Assert.Equal("D3B 0.2.0 is ready to install.", view.ReadyText);
        Assert.Equal("D3B 0.2.0 is downloaded: it installs when D3B restarts or closes.", view.StatusText);
        Assert.Equal(Tone.Success, view.StatusTone);
        Assert.False(view.CheckCommand.CanExecute(null));
        Assert.True(view.RestartCommand.CanExecute(null));
    }

    [Fact]
    public async Task RestartingWithoutBotsClosesTheAppAtOnce()
    {
        var client = new FakeUpdateClient { Published = "0.2.0" };
        var updates = await ReadyAsync(client);

        View(updates, botsPlay: false).RestartCommand.Execute(null);

        Assert.Equal(1, _closed);
        Assert.Null(_dialogs.Current);
        Assert.True(updates.RestartRequested);
    }

    [Fact]
    public async Task RestartingWhileBotsPlayAsksFirst()
    {
        var updates = await ReadyAsync(new FakeUpdateClient { Published = "0.2.0" });

        View(updates, botsPlay: true).RestartCommand.Execute(null);

        var question = Assert.IsType<QuestionViewModel>(_dialogs.Current);

        Assert.Equal("Restart D3B to install its update?", question.Title);
        Assert.Equal(0, _closed);

        question.ConfirmCommand.Execute(null);

        Assert.Equal(1, _closed);
        Assert.True(updates.RestartRequested);
    }

    [Fact]
    public async Task AFailedCheckIsSaidAndMayBeTriedAgain()
    {
        var updates = new AppUpdates(new FakeUpdateClient { CheckFails = new HttpRequestException("No such host is known.") });

        await updates.CheckAsync();

        var view = View(updates, botsPlay: false);

        Assert.Equal("The check failed: No such host is known.", view.StatusText);
        Assert.Equal(Tone.Warning, view.StatusTone);
        Assert.True(view.CheckCommand.CanExecute(null));
        Assert.False(view.IsReady);
    }

    [Fact]
    public void AnAppRunFromItsCodeSaysItDoesNotUpdate()
    {
        var view = View(new AppUpdates(new FakeUpdateClient { IsInstalled = false }), botsPlay: false);

        Assert.Equal("Run from its code, D3B does not update itself.", view.StatusText);
        Assert.False(view.CheckCommand.CanExecute(null));
        Assert.False(view.IsInstalled);
    }

    [Fact]
    public async Task AnInstalledAppSaysTheClientBuildItDoesNotShip()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack for client 3.7.13.1.", "3.7.13.1")));
        var main = new MainViewModel(runner, _localizer, null, new AppUpdates(new FakeUpdateClient()));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is HostStatus.Failed, TimeSpan.FromSeconds(10));
        main.Refresh();

        Assert.True(main.IsMissingBuild);
        Assert.StartsWith($"The launcher installed client 3.7.13.1; D3B {AppRelease.Version} ships the data of client {AppRelease.ClientVersion}.", main.MissingBuildText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAppRunFromItsCodeShowsWhatTheHostSaid()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack for client 3.7.13.1.", "3.7.13.1")));
        var main = new MainViewModel(runner, _localizer, null, new AppUpdates(new FakeUpdateClient { IsInstalled = false }));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is HostStatus.Failed, TimeSpan.FromSeconds(10));
        main.Refresh();

        Assert.False(main.IsMissingBuild);
        Assert.Equal("No pack for client 3.7.13.1.", main.HostFailure);
    }

    private static async Task<AppUpdates> ReadyAsync(FakeUpdateClient client)
    {
        var updates = new AppUpdates(client);

        await updates.CheckAsync();

        return updates;
    }

    private UpdatesViewModel View(AppUpdates updates, bool botsPlay)
    {
        return new UpdatesViewModel(_localizer, updates, _dialogs, () => botsPlay, () => _closed++);
    }
}
