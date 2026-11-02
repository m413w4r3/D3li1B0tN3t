using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Storage.Tests.Accounts;

public sealed class AccountBookTests : IAsyncDisposable
{
    private const long OtherCharacterId = 70_002;

    private readonly TestStorage _storage = new();

    private static CharacterListEvent List(string otherName = "Mule", int otherLevel = 54)
    {
        return new CharacterListEvent
        {
            Characters =
            {
                new Character { Id = TestPlay.CharacterId, Basics = new CharacterBasics { Name = "Character", Level = 200 } },
                new Character { Id = OtherCharacterId, Basics = new CharacterBasics { Name = otherName, Level = otherLevel } },
            },
        };
    }

    public ValueTask DisposeAsync()
    {
        return _storage.DisposeAsync();
    }

    [Fact]
    public async Task TheCharactersTheServerListsAreRecordedWithTheirAccount()
    {
        await _storage.StartedAsync();

        var version = _storage.Accounts.Version;
        var connection = _storage.Play.Connect(TestPlay.SubscribedAccount);

        connection.Server(List());
        await _storage.RecordedAsync();

        var account = Assert.Single(_storage.Accounts.Accounts);

        Assert.Equal((TestPlay.AccountId, "nickname", "tag", DateTimeOffset.MaxValue), (account.Id, account.Nickname, account.Tag, account.SubscriptionEnd));
        Assert.Equal((_storage.Time.GetUtcNow(), _storage.Time.GetUtcNow()), (account.FirstSeen, account.LastSeen));
        Assert.Equal(
            [(TestPlay.CharacterId, "Character", (int?)200), (OtherCharacterId, "Mule", 54)],
            _storage.Accounts.Characters.Select(character => (character.Id, character.Name, character.Level)));
        Assert.All(_storage.Accounts.Characters, character => Assert.Null(character.LastPlayed));
        Assert.True(_storage.Accounts.Version > version);
    }

    [Fact]
    public async Task TheCharacterTheClientChoosesIsRecordedAsPlayedWhenTheRelaySawIt()
    {
        await _storage.StartedAsync();

        var chosen = _storage.Time.GetUtcNow();

        // The play moves the clock on once the character is placed, before the queue may write.
        await _storage.Play.EnterAsync(300);
        await _storage.RecordedAsync();

        var character = Assert.Single(_storage.Accounts.Characters);

        Assert.Equal(TestPlay.Key, character.Key);
        Assert.Equal(chosen, character.LastPlayed);
        Assert.Same(character, _storage.Accounts.Find(TestPlay.Key));
        Assert.Null(_storage.Accounts.Find(new(TestPlay.AccountId, 1)));
    }

    [Fact]
    public async Task ACharacterListedAgainKeepsItsProfileAndTakesItsNewName()
    {
        await _storage.StartedAsync();

        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(List());
        await _storage.RecordedAsync();

        var profile = await _storage.Profiles.SaveAsync(0, "Lumberjack", TestStorage.PlainActivityId, "{}", TestContext.Current.CancellationToken);
        var other = new D3B.Bot.Characters.CharacterKey(TestPlay.AccountId, OtherCharacterId);

        await _storage.Accounts.AssignAsync(other, profile.Id, TestContext.Current.CancellationToken);
        _storage.Time.Advance(TimeSpan.FromHours(1));
        connection.Server(List("Mulette", 60));
        await _storage.RecordedAsync();

        var account = Assert.Single(_storage.Accounts.Accounts);
        var character = _storage.Accounts.Find(other);

        Assert.Equal(_storage.Time.GetUtcNow() - TimeSpan.FromHours(1), account.FirstSeen);
        Assert.Equal(_storage.Time.GetUtcNow(), account.LastSeen);
        Assert.Equal(("Mulette", (int?)60, (int?)profile.Id), (character?.Name, character?.Level, character?.ProfileId));
    }

    [Fact]
    public async Task AGameConnectionOfNoKnownAccountRecordsNothing()
    {
        await _storage.StartedAsync();

        var connection = _storage.Play.Connect(null);

        connection.Server(List());
        await _storage.RecordedAsync();

        Assert.Empty(_storage.Accounts.Accounts);
    }

    [Fact]
    public async Task OnlyACharacterWithAProfileStartsOnItsConnection()
    {
        await _storage.StartedAsync();
        await _storage.Play.EnterAsync(300);
        await _storage.RecordedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = await _storage.Profiles.SaveAsync(0, "Stroll", TestStorage.PlainActivityId, "{}", cancellationToken);

        await _storage.Accounts.SetStartsOnConnectionAsync(TestPlay.Key, true, cancellationToken);

        Assert.False(_storage.Accounts.Find(TestPlay.Key)?.StartsOnConnection);

        await _storage.Accounts.AssignAsync(TestPlay.Key, profile.Id, cancellationToken);
        await _storage.Accounts.SetStartsOnConnectionAsync(TestPlay.Key, true, cancellationToken);

        Assert.True(_storage.Accounts.Find(TestPlay.Key)?.StartsOnConnection);

        await _storage.Accounts.AssignAsync(TestPlay.Key, null, cancellationToken);

        Assert.Equal((null, false), (_storage.Accounts.Find(TestPlay.Key)?.ProfileId, _storage.Accounts.Find(TestPlay.Key)?.StartsOnConnection));
    }

    [Fact]
    public async Task ForgettingAnAccountForgetsItsCharactersUntilTheyAreListedAgain()
    {
        await _storage.StartedAsync();

        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(List());
        await _storage.RecordedAsync();
        await _storage.Accounts.ForgetAsync(TestPlay.AccountId, TestContext.Current.CancellationToken);

        Assert.Empty(_storage.Accounts.Accounts);
        Assert.Empty(_storage.Accounts.Characters);

        connection.Server(List());
        await _storage.RecordedAsync();

        Assert.Equal(2, _storage.Accounts.Characters.Count);
    }

    [Fact]
    public async Task WhatWasRecordedIsReadBackByTheNextRun()
    {
        await _storage.StartedAsync();

        var connection = _storage.Play.Connect(TestPlay.Account);

        connection.Server(List());
        await _storage.RecordedAsync();
        await _storage.Profiles.SaveAsync(0, "Stroll", TestStorage.PlainActivityId, "{}", TestContext.Current.CancellationToken);

        await using var next = new TestStorage(_storage.Settings.Path);

        await next.StartedAsync();

        Assert.Equal(["nickname"], next.Accounts.Accounts.Select(account => account.Nickname));
        Assert.Equal(2, next.Accounts.Characters.Count);
        Assert.Equal(["Stroll"], next.Profiles.Profiles.Select(profile => profile.Name));
    }
}
