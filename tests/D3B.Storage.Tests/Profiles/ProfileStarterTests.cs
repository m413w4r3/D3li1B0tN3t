using D3B.Bot.Settings;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Settings;
using D3B.Storage.Profiles;

namespace D3B.Storage.Tests.Profiles;

public sealed class ProfileStarterTests : IAsyncDisposable
{
    private readonly TestStorage _storage = new();

    private static Profile ProfileOf(string activityId, string settings)
    {
        return new Profile { Id = 1, Name = "Profile", ActivityId = activityId, Settings = settings };
    }

    public ValueTask DisposeAsync()
    {
        return _storage.DisposeAsync();
    }

    [Fact]
    public async Task AProfileStartsItsActivityWithItsSettings()
    {
        var (_, bot) = await _storage.Play.EnterAsync(300);
        var start = _storage.Starter.Start(bot, ProfileOf(TestStorage.SettingsActivityId, $$"""{"Walks":7,"Job":{{TestReferences.Lumberjack}},"Resources":[{{TestReferences.Chestnut}}]}"""));

        Assert.Null(start.Failure);
        Assert.Same(bot.Activity, start.Run);

        var activity = Assert.IsType<SettingsActivity>(start.Run!.Activity);

        Assert.Equal((7, (int?)TestReferences.Lumberjack), (activity.Settings.Walks, activity.Settings.Job));
        Assert.Equal([TestReferences.Chestnut], activity.Settings.Resources);
    }

    [Fact]
    public async Task AProfileWhoseActivityIsGoneDoesNotStart()
    {
        var (_, bot) = await _storage.Play.EnterAsync(300);
        var start = _storage.Starter.Start(bot, ProfileOf("gone", "{}"));

        Assert.Equal(ProfileStartFailure.ActivityMissing, start.Failure);
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task AProfileWhoseStoredSettingNoLongerReadsIsLookedAtFirst()
    {
        var (_, bot) = await _storage.Play.EnterAsync(300);
        var start = _storage.Starter.Start(bot, ProfileOf(TestStorage.SettingsActivityId, """{"Walks":"many"}"""));

        Assert.Equal(ProfileStartFailure.SettingsReset, start.Failure);
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task AProfileWhoseSettingsDoNotHoldInThePackDoesNotStart()
    {
        var (_, bot) = await _storage.Play.EnterAsync(300);
        var start = _storage.Starter.Start(bot, ProfileOf(TestStorage.SettingsActivityId, $$"""{"Job":{{TestReferences.Miner}},"Resources":[{{TestReferences.Ash}}]}"""));

        Assert.Equal(ProfileStartFailure.SettingsInvalid, start.Failure);
        Assert.Equal([("Resources", SettingProblemKind.NotWithin)], start.Problems.Select(problem => (problem.Setting?.Key, problem.Kind)));
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task ABotAlreadyRunningAnActivityDoesNotStartAProfile()
    {
        var (_, bot) = await _storage.Play.EnterAsync(300);
        var running = bot.StartActivity(new TestActivity());
        var start = _storage.Starter.Start(bot, ProfileOf(TestStorage.PlainActivityId, "{}"));

        Assert.Equal(ProfileStartFailure.NotStarted, start.Failure);
        Assert.Equal("The bot already runs an activity.", start.Error);
        Assert.Same(running, bot.Activity);
        running.Stop();
    }
}
