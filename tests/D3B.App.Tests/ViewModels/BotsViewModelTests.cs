using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.App.Tests.ViewModels;

public sealed class BotsViewModelTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task ABotShowsOnceAClientChoseItsCharacter()
    {
        using var host = TestHosts.Of(_play);
        var page = new BotsViewModel(_localizer);

        page.Refresh(host.Services);

        Assert.True(page.IsEmpty);

        var (_, bot) = await _play.EnterAsync(300);

        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);

        Assert.False(page.IsEmpty);
        Assert.Equal(1, page.Count);
        Assert.Same(bot, item.Bot);
        Assert.Equal("Character", item.Name);
        Assert.Equal("CH", item.Initials);
        Assert.Equal("Level 200", item.Level);
        Assert.True(item.IsConnected);
        Assert.Equal("Connected", item.Connection);
        Assert.Equal(Tone.Success, item.ConnectionTone);
        Assert.False(item.IsPaused);
        Assert.Equal(Tone.Info, item.Tone);
        Assert.Equal("4,-18", item.Coordinates);
        Assert.Equal($"Sub-area {TestPlay.SubAreaId}, cell 300", item.Place);
        Assert.Equal($"Map {TestPlay.MapId}, cell 300", item.MapDetail);
        Assert.Equal("Standing still", item.Doing);
        Assert.Equal("—", item.Load);
        Assert.Equal("", item.LoadPercentText);
        Assert.Equal("0 kamas", item.Kamas);
        Assert.False(item.HasRun);
        Assert.False(item.HasActivities);
        Assert.False(item.IsChoosing);
        Assert.True(item.HasNothingToChoose);
    }

    [Fact]
    public async Task WhatTheBotShowsFollowsItsCharacterAndTheLanguage()
    {
        using var host = TestHosts.Of(_play);
        var page = new BotsViewModel(_localizer);
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Server(new InventoryWeightEvent { Weight = 1_234, MaxWeight = 7_590 });
        connection.Server(new KamasUpdateEvent { Kamas = 25_000 });
        connection.Close();
        await bot.ProcessedAsync();
        _localizer.Use(Localizer.French);
        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);
        var separator = Localizer.French.NumberFormat.NumberGroupSeparator;

        Assert.Equal("Niveau 200", item.Level);
        Assert.False(item.IsConnected);
        Assert.Equal("Déconnecté", item.Connection);
        Assert.Equal("", item.Doing);
        Assert.False(item.IsNearlyFull);
        Assert.Equal(Tone.Neutral, item.ConnectionTone);
        Assert.Equal(Tone.Neutral, item.Tone);
        Assert.Equal($"Sous-zone {TestPlay.SubAreaId}, cellule 300", item.Place);
        Assert.Equal($"Carte {TestPlay.MapId}, cellule 300", item.MapDetail);
        Assert.Equal($"1{separator}234 / 7{separator}590 pods", item.Load);
        Assert.Equal(1_234 * 100.0 / 7_590, item.LoadPercent, 6);
        Assert.Equal(string.Format(Localizer.French, "{0:P0}", 1_234.0 / 7_590), item.LoadPercentText);
        Assert.Equal($"25{separator}000 kamas", item.Kamas);
    }

    [Fact]
    public async Task TheActivityChosenStartsFromTheAppThenStops()
    {
        var activity = new TestActivity();

        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => activity));
        var page = new BotsViewModel(_localizer);
        var (_, bot) = await _play.EnterAsync(300);

        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);

        Assert.True(item.HasActivities);
        Assert.True(item.IsChoosing);
        Assert.False(item.HasNothingToChoose);
        Assert.Equal("patrol", item.SelectedActivity?.Definition?.Id);
        Assert.True(item.StartCommand.CanExecute(null));
        Assert.False(item.StopCommand.CanExecute(null));

        item.StartCommand.Execute(null);
        await activity.SteppingAsync(bot);
        page.Refresh(host.Services);

        Assert.Same(activity, bot.Activity?.Activity);
        Assert.True(item.IsActivityRunning);
        Assert.False(item.IsChoosing);
        Assert.True(item.HasRun);
        Assert.Equal("Patrol", item.ActivityName);
        Assert.Equal("Running", item.ActivityText);
        Assert.Equal(Tone.Success, item.ActivityTone);
        Assert.Equal("0 steps", item.ActivityDetail);
        Assert.Equal(Tone.Success, item.Tone);
        Assert.False(item.StartCommand.CanExecute(null));
        Assert.True(item.StopCommand.CanExecute(null));

        item.StopCommand.Execute(null);

        Assert.Equal(ActivityOutcome.Stopped, await bot.Activity!.Completion);

        page.Refresh(host.Services);

        Assert.False(item.IsActivityRunning);
        Assert.True(item.IsChoosing);
        Assert.Equal("Stopped", item.ActivityText);
        Assert.Equal(Tone.Neutral, item.ActivityTone);
        Assert.True(item.StartCommand.CanExecute(null));
        Assert.False(item.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task ABotThePlayerTookOverIsResumedFromTheApp()
    {
        var activity = new TestActivity();

        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => activity));
        var page = new BotsViewModel(_localizer);
        var (connection, bot) = await _play.EnterAsync(300);

        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);

        item.StartCommand.Execute(null);
        await activity.SteppingAsync(bot);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await bot.ProcessedAsync();
        await Waiting.UntilAsync(() => bot.Activity?.Status is ActivityStatus.Waiting, TimeSpan.FromSeconds(10));
        page.Refresh(host.Services);

        Assert.True(item.IsPaused);
        Assert.Equal(Tone.Warning, item.Tone);
        Assert.Equal("Waiting", item.ActivityText);
        Assert.Equal(Tone.Warning, item.ActivityTone);
        Assert.Equal("0 steps, last interrupted by the player", item.ActivityDetail);
        Assert.True(item.ResumeCommand.CanExecute(null));

        item.ResumeCommand.Execute(null);
        await bot.ProcessedAsync();
        page.Refresh(host.Services);

        Assert.False(bot.IsPaused);
        Assert.False(item.IsPaused);
        Assert.False(item.ResumeCommand.CanExecute(null));
    }

    [Fact]
    public async Task WhatABotMeetsIsToastedOnceAndWhatItAlreadyWasIsNot()
    {
        var activity = new TestActivity();

        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => activity));
        var toasts = new Toasts();
        var page = new BotsViewModel(_localizer, toasts: toasts);
        var (connection, bot) = await _play.EnterAsync(300);

        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);

        item.StartCommand.Execute(null);
        await activity.SteppingAsync(bot);
        page.Refresh(host.Services);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await bot.ProcessedAsync();
        await Waiting.UntilAsync(() => bot.Activity?.Status is ActivityStatus.Waiting, TimeSpan.FromSeconds(10));
        page.Refresh(host.Services);
        page.Refresh(host.Services);

        var toast = Assert.Single(toasts.Recent);

        Assert.Equal(("Character: paused, you are playing it", Tone.Warning), (toast.Text, toast.Tone));

        // A page that meets the bot paused already says nothing of it.
        var later = new Toasts();

        new BotsViewModel(_localizer, toasts: later).Refresh(host.Services);

        Assert.Empty(later.Recent);

        // Stopped from the app, the activity ended as the owner asked: nothing to say.
        item.ResumeCommand.Execute(null);
        await bot.ProcessedAsync();
        item.StopCommand.Execute(null);
        await bot.Activity!.Completion;
        page.Refresh(host.Services);

        Assert.Single(toasts.Recent);
    }

    [Fact]
    public async Task AnActivityThatCannotBeMadeSaysWhy()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("broken", "Broken", _ => throw new InvalidOperationException("The module's activity failed.")));
        var page = new BotsViewModel(_localizer);
        var (_, bot) = await _play.EnterAsync(300);

        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);

        item.StartCommand.Execute(null);

        Assert.Null(bot.Activity);
        Assert.Equal("Could not start: The module's activity failed.", item.Error);
    }

    [Fact]
    public async Task AnActivitysErrorShowsOnItsBotsCard()
    {
        using var host = TestHosts.Of(_play);
        var page = new BotsViewModel(_localizer);
        var (_, bot) = await _play.EnterAsync(300);

        await bot.StartActivity(new TestActivity { Throws = true }).Completion;
        page.Refresh(host.Services);

        var item = Assert.Single(page.Items);

        Assert.Equal("Failed", item.ActivityText);
        Assert.Equal(Tone.Danger, item.ActivityTone);
        Assert.Equal("The step failed.", item.ActivityProblem);
    }

    [Fact]
    public async Task AnActivityStartedByOtherCodeGoesByItsOwnName()
    {
        using var host = TestHosts.Of(_play);
        var page = new BotsViewModel(_localizer);
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();

        bot.StartActivity(activity);
        await activity.SteppingAsync(bot);
        page.Refresh(host.Services);

        Assert.Equal(nameof(TestActivity), Assert.Single(page.Items).ActivityName);
    }
}
