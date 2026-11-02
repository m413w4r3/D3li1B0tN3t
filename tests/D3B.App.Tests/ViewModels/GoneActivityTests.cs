using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

/// <summary>A profile saved while Lua scripts ran, naming one, in a host that registers no activity, as D3B's until step 6 (D116).</summary>
public sealed class GoneActivityTests : IAsyncDisposable
{
    private const string ScriptId = "script:promenade.lua";

    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.French);
    private IHost? _host;

    public async ValueTask DisposeAsync()
    {
        _host?.Dispose();
        await _play.DisposeAsync();
        _game.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public async Task AProfileNamingAScriptShowsItsActivityGoneAndCanBeDeleted()
    {
        var services = await OpenedAsync();
        var page = new ProfilesViewModel(_localizer, new Dialogs(), new Toasts());

        page.Refresh(services);

        var item = Assert.Single(page.Items);

        Assert.Empty(page.Activities);
        Assert.Equal(("Promenade", ScriptId, ""), (item.Name, item.ActivityName, item.Summary));
        Assert.Equal("Activité disparue : script:promenade.lua", item.Review);

        item.EditCommand.Execute(null);

        Assert.Null(item.EditActivity);
        Assert.Null(item.Editor);
        Assert.False(item.CanSave);

        item.AskDeleteCommand.Execute(null);
        await item.DeleteCommand.ExecuteAsync(null);
        page.Refresh(services);

        Assert.Empty(page.Items);
        Assert.Empty(services.GetRequiredService<ProfileBook>().Profiles);
    }

    [Fact]
    public async Task ABotsCardOffersTheProfileItsCharacterPlaysAndSaysItsActivityIsGone()
    {
        var services = await OpenedAsync();
        var (_, bot) = await _play.EnterAsync(300);
        var page = new BotsViewModel(_localizer);

        page.Refresh(services);

        var item = Assert.Single(page.Items);
        var choice = Assert.Single(item.Activities);

        Assert.Equal(("Promenade", "Activité disparue : script:promenade.lua"), (choice.Name, choice.Detail));
        Assert.Same(choice, item.SelectedActivity);

        item.StartCommand.Execute(null);

        Assert.Null(bot.Activity);
        Assert.Equal("L'activité de Promenade a disparu", item.Error);
    }

    private async Task<IServiceProvider> OpenedAsync()
    {
        _host = await TestHosts.WithStorageAsync(_play, _directory.Combine("d3b.db"), _game);

        var services = _host.Services;
        var ct = TestContext.Current.CancellationToken;
        var profile = await services.GetRequiredService<ProfileBook>().SaveAsync(0, "Promenade", ScriptId, """{"walks":3,"reach":4}""", ct);
        var accounts = services.GetRequiredService<AccountBook>();

        await accounts.SeeAsync(TestPlay.Account, [(TestPlay.CharacterId, "Character", 200)], _play.Time.GetUtcNow(), ct);
        await accounts.AssignAsync(TestPlay.Key, profile.Id, ct);

        return services;
    }
}
