using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Network;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.ViewModels;

public sealed class AccountsViewModelTests : IAsyncDisposable
{
    private static readonly GameAccount _mule = new(42, "Mule", "0042");

    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task EachAccountShowsItsSubscriptionWhenItWasSeenAndItsCharacters()
    {
        await _store.OpenedAsync();

        var time = _store.Play.Time;
        var ct = TestContext.Current.CancellationToken;

        await _store.SeeAsync(_mule, (80_001, "Minette", 54));
        await _store.Accounts.PlayAsync(_mule, 80_001, "Minette", 54, time.GetUtcNow(), ct);
        time.Advance(TimeSpan.FromDays(3));
        await _store.SeeAsync(TestPlay.SubscribedAccount, (TestPlay.CharacterId, "Character", 200), (70_002, "Kolossa", null));
        time.Advance(TimeSpan.FromMinutes(5));

        var page = new AccountsViewModel(_localizer, new Dialogs());

        page.Refresh(_store.Host.Services);

        Assert.True(page.IsAvailable);
        Assert.False(page.IsEmpty);
        Assert.Equal(["nickname", "Mule"], page.Items.Select(item => item.Nickname));

        var account = page.Items[0];

        Assert.Equal("#tag", account.Tag);
        Assert.Equal(Tone.Success, account.SubscriptionTone);
        Assert.StartsWith("Subscribed until ", account.Subscription, StringComparison.Ordinal);
        Assert.Equal("Seen 5 min ago", account.Seen);
        Assert.Equal(["Character", "Kolossa"], account.Characters.Select(character => character.Name));
        Assert.Equal("Level 200 · never played through D3B", account.Characters[0].Detail);
        Assert.Equal("never played through D3B", account.Characters[1].Detail);
        Assert.Equal(("Not subscribed", Tone.Neutral), (page.Items[1].Subscription, page.Items[1].SubscriptionTone));
        Assert.Equal("Seen 3 days ago", page.Items[1].Seen);
        Assert.Equal("Level 54 · played 3 days ago", page.Items[1].Characters[0].Detail);

        // A character its bot plays now says so, and so does its account.
        await _store.Play.EnterAsync(300);
        page.Refresh(_store.Host.Services);

        Assert.Equal("Connected", page.Items[0].Seen);
        Assert.Equal("Level 200 · connected", page.Items[0].Characters[0].Detail);
        Assert.Equal(Tone.Success, page.Items[0].Characters[0].Tone);

        _localizer.Use(Localizer.French);
        page.Refresh(_store.Host.Services);

        Assert.Equal("Vu il y a 3 j", page.Items[1].Seen);
    }

    [Fact]
    public async Task ACharacterIsGivenAProfileWhichMayStartOnItsConnection()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var stroll = await _store.Profiles.SaveAsync(0, "Stroll", TestStore.StrollId, "{}", ct);

        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200));

        var page = new AccountsViewModel(_localizer, new Dialogs());

        page.Refresh(_store.Host.Services);

        var row = page.Items[0].Characters[0];

        Assert.Equal(["No profile", "Stroll"], row.Profiles.Select(profile => profile.Name));
        Assert.Equal("No profile", row.SelectedProfile?.Name);
        Assert.False(row.HasProfile);

        var version = _store.Accounts.Version;

        row.SelectedProfile = row.Profiles[1];
        await Waiting.UntilAsync(() => _store.Accounts.Version > version, TimeSpan.FromSeconds(10));
        version = _store.Accounts.Version;
        row.StartsOnConnection = true;
        await Waiting.UntilAsync(() => _store.Accounts.Version > version, TimeSpan.FromSeconds(10));

        var character = _store.Accounts.Find(TestPlay.Key);

        Assert.Equal((stroll.Id, true), (character?.ProfileId, character?.StartsOnConnection));

        // The cards are made again from what was saved.
        page.Refresh(_store.Host.Services);

        row = page.Items[0].Characters[0];

        Assert.Equal("Stroll", row.SelectedProfile?.Name);
        Assert.True(row.StartsOnConnection);
        Assert.True(row.HasProfile);
    }

    [Fact]
    public async Task ForgettingAnAccountAsksFirst()
    {
        await _store.OpenedAsync();
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200));

        var page = new AccountsViewModel(_localizer, new Dialogs());

        page.Refresh(_store.Host.Services);

        var account = page.Items[0];

        account.AskForgetCommand.Execute(null);

        var question = Assert.IsType<QuestionViewModel>(page.Dialogs.Current);

        Assert.True(question.IsDestructive);

        question.CancelCommand.Execute(null);

        Assert.Null(page.Dialogs.Current);
        Assert.Single(page.Items);

        var version = _store.Accounts.Version;

        account.AskForgetCommand.Execute(null);
        Assert.IsType<QuestionViewModel>(page.Dialogs.Current).ConfirmCommand.Execute(null);
        await Waiting.UntilAsync(() => _store.Accounts.Version > version, TimeSpan.FromSeconds(10));
        page.Refresh(_store.Host.Services);

        Assert.Empty(page.Items);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public void APageOfAHostWithoutItsDatabaseSaysSo()
    {
        using var host = TestHosts.Of(_store.Play);
        var page = new AccountsViewModel(_localizer, new Dialogs());
        var profiles = new ProfilesViewModel(_localizer, new Dialogs(), new Toasts());

        page.Refresh(host.Services);
        profiles.Refresh(host.Services);

        Assert.False(page.IsAvailable);
        Assert.False(profiles.IsAvailable);
    }
}
