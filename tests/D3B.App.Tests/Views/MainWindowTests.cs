using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.Tests.Pages;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Inspection;
using D3B.Bot.Pages;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class MainWindowTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheWindowShowsTheBotsAndFollowsAChangeOfLanguage()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowTests).Assembly);

        // The views bind to the app's own texts, which only this test changes, and gives back.
        var (english, french) = await session.Dispatch(
            () =>
            {
                var localizer = Localizer.Instance;

                localizer.Use(Localizer.English);

                var main = new MainViewModel(runner, localizer);
                var window = new MainWindow { DataContext = main };

                main.Refresh();
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var english = Texts(window);

                Drawings.Draw(window, "bots-en");
                main.Settings.UseFrenchCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                var french = Texts(window);

                Drawings.Draw(window, "bots-fr");
                localizer.Use(Localizer.English);
                window.Close();

                return (english, french);
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("Bots", english);
        Assert.Contains("Character", english);
        Assert.Contains("Level 200", english);
        // Connected goes without saying: only a bot that is not says it.
        Assert.DoesNotContain("Connected", english);
        Assert.Contains("Patrol", english);
        Assert.Contains("Start", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Niveau 200", french);
        Assert.Contains("Démarrer", french);
        Assert.Contains("En marche", french);
        Assert.DoesNotContain("Start", french);
    }

    [Fact]
    public async Task ASidebarItemCheckedShowsItsPageAndAPageShownChecksItsItem()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        var (shown, checkedItem) = await OnTheUIThreadAsync(() =>
        {
            var main = new MainViewModel(runner, Localizer.Instance);
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var items = window.GetVisualDescendants().OfType<ShadUI.SidebarItem>().ToList();

            // Checked as a key or a screen reader checks it, with no click: the page shows all the same.
            items.Single(item => item.Route == "accounts").IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            var shown = (main.IsAccountsPage, main.CurrentRoute);

            main.ShowLogsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var checkedItem = items.Single(item => item.IsChecked == true).Route;

            window.Close();

            return (shown, checkedItem);
        });

        Assert.Equal((true, "accounts"), shown);
        Assert.Equal("logs", checkedItem);
    }

    [Fact]
    public async Task AModulesPageShowsInTheRailInTheAppsLanguage()
    {
        using var host = TestHosts.Of(_play, [TestPage.Definition()], new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (titles, english, french, shown) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();

            var page = Assert.Single(main.Pages);
            var titles = new List<string> { page.Title };

            page.ShowCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var control = Assert.Single(window.GetVisualDescendants().OfType<TestPage>());
            var english = Texts(window);
            var shown = new List<bool> { window.GetVisualDescendants().Contains(control) };

            Drawings.Draw(window, "module-en");

            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            titles.Add(page.Title);

            var french = Texts(window);

            main.ShowBotsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            shown.Add(window.GetVisualDescendants().Contains(control));
            localizer.Use(Localizer.English);
            window.Close();

            return (titles, english, french, shown);
        });

        Assert.Equal(["Greetings", "Salutations"], titles);
        Assert.Contains("Hello from a module", english);
        Assert.Contains("1 bot in the host", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Bonjour d'un module", french);
        Assert.Contains("1 bot dans l'hôte", french);
        Assert.DoesNotContain("Hello from a module", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Equal([true, false], shown);
    }

    [Fact]
    public async Task TheAccountsAndProfilesPagesShowInTheRailInBothLanguages()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await store.Profiles.SaveAsync(0, "Walker", TestStore.HarvestId, TestStore.Settings(new() { Job = D3B.Bot.Tests.Settings.TestReferences.Lumberjack }), ct);

        await store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));
        await store.Accounts.AssignAsync(TestPlay.Key, walker.Id, ct);

        var runner = await TestHosts.RunningAsync(store.Host);
        var (english, french) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };
            var texts = new List<string>();

            main.Refresh();
            window.Show();
            main.ShowAccountsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "accounts-en");
            main.ShowProfilesCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            Drawings.Draw(window, "profiles-en");
            main.Profiles.Items.Single(profile => profile.Name == "Walker").EditCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));

            var english = texts.ToList();

            texts.Clear();
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "profiles-edit-fr");
            main.ShowAccountsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "accounts-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, texts);
        });

        Assert.Contains("Accounts", english);
        Assert.Contains("Aerafa", english);
        Assert.Contains("Walker", english);
        Assert.Contains("On connection", english);
        Assert.Contains("Profiles", english);
        Assert.Contains("New profile", english);
        Assert.Contains(english, text => text.Contains("Lumberjack", StringComparison.Ordinal));
        Assert.Contains("Save", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Comptes", french);
        Assert.Contains("Profils", french);
        Assert.Contains("Nouveau profil", french);
        Assert.Contains(french, text => text.Contains("Bûcheron", StringComparison.Ordinal));
        Assert.Contains("Enregistrer", french);
        Assert.Contains("À la connexion", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }

    [Fact]
    public async Task TheLogsAndTheInspectorShowInTheRailInBothLanguagesAndACardOpensItsBotsJournal()
    {
        var recorder = new FrameRecorder(TimeProvider.System);

        await using var play = new TestPlay([recorder]);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(play.Bots);
        builder.Services.AddSingleton(play.World);
        builder.Services.AddSingleton(recorder);
        builder.Services.AddSingleton(new HostProblems(TimeProvider.System));
        builder.Services.AddSingleton(new ActivityCatalog([]));
        builder.Services.AddSingleton(new PageCatalog([]));

        using var host = builder.Build();
        var runner = await TestHosts.RunningAsync(host);

        await play.EnterAsync(300);

        var (english, french, journal) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };
            var texts = new List<string>();

            main.Refresh();
            window.Show();
            main.Bots.Items[0].ShowJournalCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var journal = (main.IsLogsPage, main.Logs.Source?.Name);

            texts.AddRange(Texts(window));
            Drawings.Draw(window, "logs-en");
            main.ShowInspectorCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "inspector-en");

            var english = texts.ToList();

            texts.Clear();
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "inspector-fr");
            main.ShowLogsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "logs-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, texts, journal);
        });

        Assert.Equal((true, "Character"), journal);
        Assert.Contains("Logs", english);
        Assert.Contains("Plays through the game connection", english);
        Assert.Contains("Warnings", english);
        Assert.Contains("Inspector", english);
        Assert.Contains(nameof(AuthenticationRequest), english);
        Assert.Contains("understood", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Inspecteur", french);
        Assert.Contains("compris", french);
        Assert.Contains("Journaux", french);
        Assert.Contains("Joue par la connexion de jeu", french);
        Assert.Contains("Alertes", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }

    [Fact]
    public async Task TheMapShowsInTheRailInBothLanguagesAndACardOpensItsBotsMap()
    {
        using var game = new TestGameData();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(game.Texts);
        builder.Services.AddSingleton(game.Tables);
        builder.Services.AddSingleton(new ActivityCatalog([]));
        builder.Services.AddSingleton(new PageCatalog([]));

        using var host = builder.Build();
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (english, french, map) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();
            main.Bots.Items[0].ShowMapCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var map = (main.IsMapPage, main.IsBotsPage, main.Map.Bot?.Name, main.Map.Scene?.Character);
            var english = Texts(window);

            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, map);
        });

        Assert.Equal((true, false, "Character", 300), map);
        Assert.Contains("Map", english);
        Assert.Contains("4,-18", english);
        Assert.Contains("Sub-area 440, cell 300", english);
        Assert.Contains("Layers", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Carte", french);
        Assert.Contains("Sous-zone 440, cellule 300", french);
        Assert.Contains("Calques", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }
}
