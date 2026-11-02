using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Fighting;
using D3B.Bot.Hosting;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Monsters;
using D3B.Data.Tests;
using D3B.Data.World;
using D3B.Storage;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

/// <summary>The fighting activity as the app edits it, its preview, its monsters chosen among many super races, and the accounts page with no strategy (D236, D244, D245).</summary>
public sealed class FightViewsTests : IAsyncDisposable
{
    // As many super races as the client's monsters have, some 40, are too many for chips: the picker's filter is a drop-down.
    private const int SuperRaces = GamePickerViewModel.MostChips + 2;

    // More monsters than the picker shows at once, as "All" of the client's: some 1,600.
    private const int Monsters = GamePickerViewModel.MostShown + 10;

    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FightViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static GameReferences References(TestPlay play)
    {
        // Monsters of every super race, in turn, live in the sub-area the profile hunts in.
        var monsters = new MonsterCatalog(
            Enumerable.Range(0, Monsters).Select(index => new Monster { Id = 1_000 + index, Race = (short)(100 + (index % SuperRaces)), FirstGrade = index, GradeCount = 1 }).ToArray(),
            Enumerable.Range(0, Monsters).Select(index => new MonsterGrade { Grade = 1, Level = (ushort)(1 + index) }).ToArray(),
            Array.Empty<MonsterSpell>(),
            Array.Empty<byte>(),
            Array.Empty<MonsterRatio>(),
            Enumerable.Range(0, SuperRaces).Select(index => new MonsterRace(100 + index, 1 + index, 0)).ToArray(),
            Enumerable.Range(0, SuperRaces).Select(index => new MonsterRace(1 + index, MonsterRace.NoSuperRace, 0)).ToArray());
        var atlas = new WorldAtlas(
            new[] { new MapPosition(TestPlay.MapId, 4, -18, TestPlay.SubAreaId, 1, 0, 0) },
            new[] { new SubArea(TestPlay.SubAreaId, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, Monsters) },
            new[] { new Area(1, 0, 0) },
            Array.Empty<Waypoint>(),
            Array.Empty<int>(),
            Enumerable.Range(0, Monsters).Select(index => 1_000 + index).ToArray());

        return new GameReferences(atlas, play.Tables.Jobs, play.Tables.Items, monsters: monsters);
    }

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
        _game.Dispose();
        _directory.Dispose();
    }

    private async Task<IHost> HostAsync()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(_play.Tables);
        builder.Services.AddSingleton(_play.Places);
        builder.Services.AddSingleton(_game.Texts);
        builder.Services.AddSingleton(new ClientBuild(TestInstall.Version, null));
        builder.Services.AddSingleton(References(_play));
        builder.Services.AddSingleton<TimeProvider>(_play.Time);
        FightingModule.Register(builder.Services);
        builder.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        builder.Services.AddSingleton(new PageCatalog([]));
        StorageModule.Register(builder.Services, new StorageSettings { Path = _directory.Combine("d3b.db") });

        var host = builder.Build();

        Assert.Null(await host.Services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken));

        return host;
    }

    [Fact]
    public async Task AFightIsEditedItsMonstersChosenAndNoCharacterHasAStrategyInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var ct = TestContext.Current.CancellationToken;
        var profiles = host.Services.GetRequiredService<ProfileBook>();
        var accounts = host.Services.GetRequiredService<AccountBook>();

        await profiles.SaveAsync(0, "Larvae", FightingModule.ActivityId, """{"Mode":"Chosen","Place":"Chosen","SubAreas":[440]}""", ct);
        await accounts.SeeAsync(TestPlay.Account, [(TestPlay.CharacterId, "Character", 200)], _play.Time.GetUtcNow(), ct);

        var runner = await TestHosts.RunningAsync(host);
        var (englishTexts, fightTexts, accountTexts, picker) = await OnTheUIThreadAsync<(List<string>, List<string>, List<string>, GamePickerViewModel?)>(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 1800 };

            main.Refresh();
            window.Show();
            main.ShowProfilesCommand.Execute(null);
            main.Refresh();
            main.Profiles.Items.Single(profile => profile.Name == "Larvae").EditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var englishTexts = Texts(window);

            Drawings.Draw(window, "fight-en");
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var fightTexts = Texts(window);

            Drawings.Draw(window, "fight-fr");

            // As narrow as the owner's window, the sidebar folded: the settings and the preview side by side still.
            window.Width = 1040;
            window.Height = 900;
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "fight-narrow-fr");
            window.Width = 1280;
            window.Height = 1800;
            Dispatcher.UIThread.RunJobs();
            main.Profiles.Editing!.Editor!.Sections.SelectMany(section => section.Fields).OfType<PickedGamesFieldViewModel>().First(field => field.IsVisible).ChooseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picker = window.GetVisualDescendants().OfType<GamePickerView>().FirstOrDefault(view => view.IsEffectivelyVisible)?.DataContext as GamePickerViewModel;

            Drawings.Draw(window, "monsters-fr");
            main.Dialogs.Close();
            main.Profiles.Editing!.CloseCommand.Execute(null);
            main.ShowAccountsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var accountTexts = Texts(window);

            Drawings.Draw(window, "accounts-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (englishTexts, fightTexts, accountTexts, picker);
        });

        Assert.Contains("Monsters to fight", englishTexts);
        Assert.Contains("maps to go through", englishTexts);
        Assert.Contains("levels of the monsters", englishTexts);
        Assert.NotNull(picker);
        Assert.True(picker.IsCompact);
        Assert.Equal(SuperRaces + 1, picker.Groups.Count);
        Assert.Equal(GamePickerViewModel.MostShown, picker.Shown.Count);
        Assert.True(picker.HasMore);
        Assert.Equal($"Les {GamePickerViewModel.MostShown} premiers sur {Monsters} : cherche un nom ou filtre pour voir les autres.", picker.MoreText);
        Assert.Equal(OptionViewModel.PortraitSize, picker.Shown[0].Option.IconSize);

        picker.ChosenGroup = picker.Groups[3];

        Assert.Equal(Enumerable.Range(0, Monsters).Where(index => index % SuperRaces == 2).Select(index => 1_000 + index), picker.Shown.Select(choice => (int)choice.Option.Value));
        Assert.False(picker.HasMore);
        Assert.Contains("Monstres par groupe, au moins", fightTexts);
        Assert.Contains("Seulement les groupes gagnables", fightTexts);
        Assert.Contains("Character", accountTexts);
        Assert.DoesNotContain("Stratégie", accountTexts);
    }
}
