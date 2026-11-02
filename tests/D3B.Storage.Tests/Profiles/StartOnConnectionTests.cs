using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Protocol.Game;
using D3B.Storage.Profiles;

namespace D3B.Storage.Tests.Profiles;

public sealed class StartOnConnectionTests : IAsyncDisposable
{
    private readonly TestStorage _storage = new();

    public ValueTask DisposeAsync()
    {
        return _storage.DisposeAsync();
    }

    [Fact]
    public async Task ACharacterAskingForItStartsItsProfileOnItsFirstConnection()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(new CharacterListEvent { Characters = { TestPlay.Chosen().Success.Character } });
        await _storage.RecordedAsync();

        var profile = await _storage.Profiles.SaveAsync(0, "Walker", TestStorage.SettingsActivityId, """{"Walks":4}""", cancellationToken);

        await _storage.Accounts.AssignAsync(TestPlay.Key, profile.Id, cancellationToken);
        await _storage.Accounts.SetStartsOnConnectionAsync(TestPlay.Key, true, cancellationToken);

        var (_, bot) = await _storage.Play.EnterAsync(300);

        await Waiting.UntilAsync(() => bot.Activity is not null);

        Assert.Equal(4, Assert.IsType<SettingsActivity>(bot.Activity!.Activity).Settings.Walks);
        await Waiting.UntilAsync(() => _storage.Log.GetSnapshot().Any(record => record.Message == $"Bot {TestPlay.CharacterId}: started profile Walker on its connection"));
        bot.Activity.Stop();
    }

    [Fact]
    public async Task ACharacterNotAskingForItStartsNothingOnItsConnection()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(new CharacterListEvent { Characters = { TestPlay.Chosen().Success.Character } });
        await _storage.RecordedAsync();

        var profile = await _storage.Profiles.SaveAsync(0, "Walker", TestStorage.SettingsActivityId, "{}", cancellationToken);

        await _storage.Accounts.AssignAsync(TestPlay.Key, profile.Id, cancellationToken);

        // The bot is made, and the character's choice read, before the play goes on: nothing was started meanwhile.
        var (_, bot) = await _storage.Play.EnterAsync(300);

        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task AProfileThatDoesNotHoldOnConnectionDoesNotStartAndSaysWhyInTheJournal()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(new CharacterListEvent { Characters = { TestPlay.Chosen().Success.Character } });
        await _storage.RecordedAsync();

        var profile = await _storage.Profiles.SaveAsync(0, "Broken", "gone", "{}", cancellationToken);

        await _storage.Accounts.AssignAsync(TestPlay.Key, profile.Id, cancellationToken);
        await _storage.Accounts.SetStartsOnConnectionAsync(TestPlay.Key, true, cancellationToken);

        var (_, bot) = await _storage.Play.EnterAsync(300);

        // The journal is written once the host's log was.
        await Waiting.UntilAsync(() => bot.Journal.All().OfType<ProfileNotStartedEntry>().Any());

        var entry = bot.Journal.All().OfType<ProfileNotStartedEntry>().Single();

        Assert.Null(bot.Activity);
        Assert.Contains(_storage.Log.GetSnapshot(), record => record.Message.StartsWith($"Bot {TestPlay.CharacterId}: profile Broken did not start on its connection, ActivityMissing", StringComparison.Ordinal));
        Assert.Equal(new ProfileNotStartedEntry(entry.At, "Broken", "gone", ProfileStartFailure.ActivityMissing, null), entry);
    }
}
