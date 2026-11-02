using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Hosting;
using D3B.Bot.Interactives;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Tests;
using D3B.Storage;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using D3B.Storage.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class HarvestViewsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static int Icons(Window window)
    {
        // The pages' icons, the pack's: the title bar's logo is the window's own, outside its content (D231).
        return ((Visual)window.Content!).GetVisualDescendants().OfType<Image>().Count(image => image.IsEffectivelyVisible && image.Source is not null);
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HarvestViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
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
        builder.Services.AddSingleton(new GameReferences(_play.World.Atlas, _play.Tables.Jobs, _play.Tables.Items, _play.World.Graph));
        builder.Services.AddSingleton<TimeProvider>(_play.Time);
        HarvestingModule.Register(builder.Services);
        builder.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        builder.Services.AddSingleton(new PageCatalog([]));
        StorageModule.Register(builder.Services, new StorageSettings { Path = _directory.Combine("d3b.db") });

        var host = builder.Build();

        Assert.Null(await host.Services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken));

        return host;
    }

    [Fact]
    public async Task TheHarvestProfileShowsItsResourcesAsTilesItsZoneAsAMapAndItsFiguresInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var settings = new HarvestSettings { Resources = [TestPlay.Ash, TestPlay.Chestnut], SubAreas = [TestPlay.SubAreaId], Heal = true, Food = [TestPlay.Bread] };

        await host.Services.GetRequiredService<ProfileBook>().SaveAsync(0, "Ash and chestnut", HarvestingModule.ActivityId, definition.Settings.Write(settings), TestContext.Current.CancellationToken);

        // A bot met an ash elsewhere: the maps drawing its graphic are probable.
        host.Services.GetRequiredService<GraphicSkills>().Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        var dialogs = new List<bool>();
        var runner = await TestHosts.RunningAsync(host);
        var (english, french, zone, icons) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 2000 };

            dialogs.Clear();
            main.Refresh();
            window.Show();
            main.ShowProfilesCommand.Execute(null);
            main.Refresh();
            main.Profiles.Items.Single(profile => profile.Name == "Ash and chestnut").EditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var english = Texts(window);
            var zone = window.GetVisualDescendants().OfType<ZoneMapCanvas>().Single().Scene;
            var icons = Icons(window);

            Drawings.Draw(window, "profile-en");
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            Drawings.Draw(window, "profile-fr");

            // Its dialogs, over the editor: resources chosen several at once, a question, a new profile.
            var editing = main.Profiles.Editing!;

            editing.Editor!.Sections.SelectMany(section => section.Fields).OfType<PickedGamesFieldViewModel>().Single(field => field.IsVisible).ChooseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            dialogs.Add(window.GetVisualDescendants().OfType<GamePickerView>().Any(view => view.IsEffectivelyVisible));
            Drawings.Draw(window, "picker-fr");
            main.Dialogs.Close();
            editing.AskDeleteCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            dialogs.Add(window.GetVisualDescendants().OfType<QuestionView>().Any(view => view.IsEffectivelyVisible));
            Drawings.Draw(window, "question-fr");
            main.Dialogs.Close();
            Dispatcher.UIThread.RunJobs();
            dialogs.Add(main.Dialogs.IsOpen);
            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, zone, icons);
        });

        // Each tile's resource, and its profession beside the level.
        Assert.Equal(4, icons);
        Assert.Contains("Place", english);
        Assert.Contains("Ash", english);
        Assert.Contains("Chestnut", english);
        Assert.Contains("Lumberjack, level 1", english);
        Assert.Contains("Lumberjack, level 20", english);
        Assert.Contains("Preview", english);
        Assert.Contains("Level 10, 5 maps, Ash, Chestnut", english);
        Assert.Contains("maps to go through", english);
        Assert.Contains("probable maps", english);
        Assert.Contains("2 of 2", english);
        Assert.Contains("1 map", english);
        Assert.Contains("to the nearest bank", english);
        Assert.Contains("90%", english);
        Assert.Contains("What harvesting brings", english);
        Assert.Contains("Everything but what is worn", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Lieu", french);
        Assert.Equal([true, true, false], dialogs);
        Assert.Contains("Frêne", french);
        Assert.Contains("Bûcheron, niveau 20", french);
        Assert.Contains("Aperçu", french);
        Assert.Contains("cartes à parcourir", french);
        Assert.Contains("cartes probables", french);
        Assert.Contains("2 sur 2", french);
        Assert.Contains("90 %", french);
        Assert.Contains("Ce que la récolte rapporte", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));

        // The zone's three maps of the world map, the insides on their coordinates, and the bank in front of one.
        Assert.NotNull(zone);
        Assert.Equal(3, zone.Squares.Count(square => square.Kind is ZoneSquareKind.GroupA));
        Assert.Equal(2, zone.Squares.Count(square => square.IsProbable));
        Assert.Equal(new PixelPoint(4, -18), zone.Landmark);
    }

    [Fact]
    public async Task TheHarvestByProfessionShowsItsProfessionsAndThePlaceChosenForEachCharacterInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var ct = TestContext.Current.CancellationToken;
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var settings = new HarvestSettings
        {
            Mode = HarvestMode.ByProfession,
            Jobs = [TestPlay.Lumberjack],
            Excluded = [TestPlay.Chestnut],
            Order = HarvestOrder.HighestLevel,
            Place = HarvestPlace.Automatic,
        };
        var profiles = host.Services.GetRequiredService<ProfileBook>();
        var accounts = host.Services.GetRequiredService<AccountBook>();
        var profile = await profiles.SaveAsync(0, "By profession", HarvestingModule.ActivityId, definition.Settings.Write(settings), ct);

        await accounts.SeeAsync(TestPlay.Account, [(TestPlay.CharacterId, "Ortie", 60)], _play.Time.GetUtcNow(), ct);
        await accounts.AssignAsync(TestPlay.Key, profile.Id, ct);

        var runner = await TestHosts.RunningAsync(host);

        // Its bot plays it: the preview knows its levels.
        await _play.EnterAsync(433);

        var (english, french, notes, picker) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 1500 };

            main.Refresh();
            window.Show();
            main.ShowProfilesCommand.Execute(null);
            main.Refresh();
            main.Profiles.Items.Single(item => item.Name == "By profession").EditCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var english = Texts(window);
            var editor = main.Profiles.Editing!.Editor!;
            var notes = editor.PlayerNotes.ToList();

            Drawings.Draw(window, "profession-en");
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            Drawings.Draw(window, "profession-fr");
            editor.Sections.SelectMany(section => section.Fields).OfType<PickedGamesFieldViewModel>().Single(field => field.IsVisible).ChooseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picker = Texts(window);

            Drawings.Draw(window, "exclude-fr");
            main.Dialogs.Close();
            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, notes, picker);
        });

        Assert.Contains("By profession", english);
        Assert.Contains("All", english);
        Assert.Contains("Exclude resources", english);
        Assert.Contains("Chestnut", english);
        Assert.Contains("Highest level first", english);
        Assert.Contains("Automatic", english);
        Assert.Contains("Not above its level", english);
        Assert.Contains("Zaap if it saves at least", english);
        Assert.Contains("maps", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Par métier", french);
        Assert.Contains("Tous", french);
        Assert.Contains("Exclure des ressources", french);
        Assert.Contains("Plus haut niveau d'abord", french);
        Assert.Contains("Automatique", french);
        Assert.Contains("Zaap s'il évite au moins", french);
        Assert.Contains("cartes", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Exclure des ressources", picker);

        var note = Assert.Single(notes);

        Assert.Equal("Ortie", note.Name);
        Assert.NotNull(note.Note);

        // The harvest's presets were stored as the database opened, beside the profile made here.
        Assert.Equal(7, profiles.Profiles.Count);
    }

    [Fact]
    public async Task TheCardAndTheMapSayWhatTheHarvestDoesInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var runner = await TestHosts.RunningAsync(host);
        var (_, bot) = await _play.EnterAsync(433);
        var run = bot.StartActivity(definition.Create(host.Services, new HarvestSettings { Resources = [TestPlay.Ash, TestPlay.Chestnut], SubAreas = [TestPlay.SubAreaId] }));

        await Waiting.UntilAsync(() => run.Activity.Report?.Focus?.TargetElementId is not null, TimeSpan.FromSeconds(10));

        var (english, french, map, icons) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 900 };
            var english = new List<string>();
            var french = new List<string>();

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            english.AddRange(Texts(window));

            var icons = Icons(window);

            Drawings.Draw(window, "card-en");
            main.Bots.Items[0].ShowMapCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            english.AddRange(Texts(window));
            Drawings.Draw(window, "map-en");

            var map = (main.Map.Scene?.Activity?.Target, main.Map.Scene?.Activity?.Wanted.ToArray(), main.Map.Tour?.Squares.Length, main.Map.Scene?.Elements.Count(element => element.Icon is not null));

            main.Settings.UseFrenchCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            french.AddRange(Texts(window));
            main.ShowBotsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            french.AddRange(Texts(window));
            Drawings.Draw(window, "card-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, map, icons);
        });

        run.Stop();
        await run.Completion;

        Assert.Contains("Harvesting", english);
        Assert.Contains("Harvesting Ash", english);
        Assert.Contains("Tour 1, map 1 of 4", english);
        Assert.Contains("Ash 0", english);
        Assert.Equal(2, icons);
        Assert.Contains("Chestnut needs Lumberjack 20, the character is 1", english);
        Assert.Contains("Layers", english);
        Assert.Contains("The tour", english);
        Assert.Contains("Harvesting Ash · Tour 1, map 1 of 4", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Récolte : Frêne", french);
        Assert.Contains("Tournée 1, carte 1 sur 4", french);
        Assert.Contains("Frêne 0", french);
        Assert.Contains("Châtaignier demande Bûcheron 20, le perso est 1", french);
        Assert.Contains("Calques", french);
        Assert.Contains("La tournée", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Equal(TestPlay.TreeCell, map.Target);
        Assert.Equal([TestPlay.TreeCell], map.Item2);
        Assert.True(map.Item3 > 0);

        // The tree, drawn by its resource's icon; the door keeps its mark.
        Assert.Equal(1, map.Item4);
    }

    [Fact]
    public async Task TheStatisticsSayWhatTheRunsBroughtInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var now = new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

        _play.Time.SetUtcNow(now);

        var contexts = host.Services.GetRequiredService<IDbContextFactory<StorageContext>>();

        await using (var context = await contexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            context.Runs.Add(new RunRecord
            {
                AccountId = TestPlay.AccountId,
                CharacterId = TestPlay.CharacterId,
                CharacterName = "Character",
                Title = "Ortie",
                Activity = "Harvesting",
                StartedAt = now.AddHours(-4),
                LastAt = now.AddHours(-4).AddMinutes(112),
                EndedAt = now.AddHours(-4).AddMinutes(112),
                Outcome = ActivityOutcome.Stopped,
                Items = new() { [TestPlay.Ash] = 600, [TestPlay.Chestnut] = 106 },
                Jobs = [new RunJob(TestPlay.Lumberjack, 10, 11, 4_200)],
                Banks = 2,
                Protectors = 1,
            });
            context.Runs.Add(new RunRecord
            {
                AccountId = TestPlay.AccountId,
                CharacterId = TestPlay.CharacterId,
                CharacterName = "Character",
                Title = "Ortie",
                Activity = "Harvesting",
                StartedAt = now.AddMinutes(-58),
                LastAt = now,
                Items = new() { [TestPlay.Ash] = 100 },
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await host.Services.GetRequiredService<RunBook>().LoadAsync(TestContext.Current.CancellationToken);

        var runner = await TestHosts.RunningAsync(host);
        var (english, french, icons) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 900 };

            main.Refresh();
            window.Show();
            main.ShowStatisticsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var english = Texts(window);
            var icons = Icons(window);

            Drawings.Draw(window, "statistics-en");
            main.Settings.UseFrenchCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            Drawings.Draw(window, "statistics-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, icons);
        });

        // 806 resources over 2 h 50, the run going on counted to its last write.
        Assert.Contains("806", english);
        Assert.Contains("2 h 50", english);
        Assert.Contains("284/h", english);
        Assert.Contains("+1 level", english);
        Assert.Contains("Lumberjack 10 → 11", english);
        Assert.Contains("Ash", english);
        Assert.Contains("700", english);
        Assert.Contains("Character · Ortie", english);
        Assert.Contains("Running", english);
        Assert.Equal(2, icons);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Statistiques", french);
        Assert.Contains("Tous les persos", french);
        Assert.Contains("En cours", french);
        Assert.Contains("Frêne", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }
}
