using System.Resources;
using Avalonia.Controls;
using Avalonia.Headless;
using D3B.App.Localization;
using D3B.App.Tests.Pages;
using D3B.App.ViewModels;
using D3B.Bot.Localization;
using D3B.Bot.Pages;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class ModulePagesTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly List<IHost> _hosts = [];

    private static PageDefinition Page(string id, Func<PageContext, object> create, string? icon = null)
    {
        return new PageDefinition(id, LocalizedText.Invariant(id), icon, create);
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        // Controls and geometries belong to Avalonia's UI thread, as the window's refresh does.
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ModulePagesTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
            host.Dispose();

        await _play.DisposeAsync();
    }

    private async Task<MainViewModel> RunningAsync(params PageDefinition[] pages)
    {
        var host = TestHosts.Of(_play, pages);

        _hosts.Add(host);

        return new MainViewModel(await TestHosts.RunningAsync(host), _localizer);
    }

    [Fact]
    public async Task TheRailListsTheModulesPagesOnceTheHostRuns()
    {
        var main = await RunningAsync(Page("first", _ => new Border(), "M4 4 H20 V20 H4 Z"), Page("second", _ => new Border(), "not a path"));

        var (ids, titles, icons, contents) = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();

            return (
                main.Pages.Select(page => page.Id).ToList(),
                main.Pages.Select(page => page.Title).ToList(),
                main.Pages.Select(page => page.Icon is not null).ToList(),
                main.Pages.Select(page => page.Content).ToList());
        });

        Assert.Equal(["first", "second"], ids);
        Assert.Equal(["first", "second"], titles);
        Assert.Equal([true, false], icons);
        Assert.All(contents, Assert.Null);
        Assert.True(main.HasPages);
        Assert.True(main.IsBotsPage);
        Assert.Null(main.SelectedPage);
    }

    [Fact]
    public async Task APageIsMadeTheFirstTimeItShowsKeptThenRefreshedWhileItShows()
    {
        var made = 0;
        var main = await RunningAsync(TestPage.Definition(() => made++));

        var (shown, shownRefreshes, hiddenRefreshes, shownAgain) = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();

            var page = Assert.Single(main.Pages);
            var unmade = page.Content;

            page.ShowCommand.Execute(null);
            main.Refresh();

            var control = Assert.IsType<TestPage>(page.Content);
            var shown = (unmade is null, main.IsBotsPage, main.SelectedPage == page, page.IsSelected);
            var shownRefreshes = control.Refreshes;

            main.ShowBotsCommand.Execute(null);
            main.Refresh();

            var hiddenRefreshes = control.Refreshes;

            page.ShowCommand.Execute(null);

            return (shown, shownRefreshes, hiddenRefreshes, (page.IsSelected, page.Content == control, control.Refreshes));
        });

        Assert.Equal((true, false, true, true), shown);
        Assert.Equal(2, shownRefreshes);
        Assert.Equal(2, hiddenRefreshes);
        Assert.Equal((true, true, 3), shownAgain);
        Assert.Equal(1, made);
        Assert.False(main.IsBotsPage);
    }

    [Fact]
    public async Task APageReadsTheHostAsItRefreshesAndFollowsTheAppsLanguage()
    {
        var main = await RunningAsync(TestPage.Definition());

        await _play.EnterAsync(300);

        var (english, french) = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();

            var page = Assert.Single(main.Pages);

            page.ShowCommand.Execute(null);

            var control = Assert.IsType<TestPage>(page.Content);
            var english = (page.Title, control.Greeting, control.Count);

            main.Settings.UseFrenchCommand.Execute(null);

            return (english, (page.Title, control.Greeting, control.Count));
        });

        Assert.Equal(("Greetings", "Hello from a module", "1 bot in the host"), english);
        Assert.Equal(("Salutations", "Bonjour d'un module", "1 bot dans l'hôte"), french);
    }

    [Fact]
    public async Task APageThatThrowsAsItIsMadeShowsWhatItThrewAndIsNotMadeAgain()
    {
        var made = 0;
        var main = await RunningAsync(Page("page", _ =>
        {
            made++;

            throw new InvalidOperationException("The page broke.");
        }));

        var content = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();
            main.Pages[0].ShowCommand.Execute(null);
            main.Refresh();

            return main.Pages[0].Content;
        });

        Assert.Equal("System.InvalidOperationException: The page broke.", Assert.IsType<PageFailureViewModel>(content).Message);
        Assert.Equal(1, made);
    }

    [Fact]
    public async Task APageThatIsNotAControlShowsWhatItIs()
    {
        var main = await RunningAsync(Page("page", _ => "a page"));

        var content = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();
            main.Pages[0].ShowCommand.Execute(null);

            return main.Pages[0].Content;
        });

        Assert.Equal("System.InvalidOperationException: The page page is not an Avalonia control, but System.String.", Assert.IsType<PageFailureViewModel>(content).Message);
    }

    [Fact]
    public async Task APageThatThrowsAsItRefreshesOrFollowsTheLanguageShowsWhatItThrewAndRunsNoMore()
    {
        var followed = 0;
        var main = await RunningAsync(
            Page("refresh", context =>
            {
                context.Refreshing += (_, _) => throw new InvalidOperationException("The refresh broke.");
                context.CultureChanged += (_, _) => followed++;

                return new Border();
            }),
            Page("language", context =>
            {
                context.CultureChanged += (_, _) => throw new InvalidOperationException("The language broke.");

                return new Border();
            }));

        var (refresh, language) = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();
            main.Pages[0].ShowCommand.Execute(null);
            main.Pages[1].ShowCommand.Execute(null);
            main.Settings.UseFrenchCommand.Execute(null);

            return (main.Pages[0].Content, main.Pages[1].Content);
        });

        Assert.Equal("System.InvalidOperationException: The refresh broke.", Assert.IsType<PageFailureViewModel>(refresh).Message);
        Assert.Equal("System.InvalidOperationException: The language broke.", Assert.IsType<PageFailureViewModel>(language).Message);
        Assert.Equal(Localizer.French, _localizer.Culture);
        Assert.Equal(0, followed);
    }

    [Fact]
    public async Task WhatAPagesOwnCodeThrowsOnTheUIThreadReplacesThatPageAlone()
    {
        static void Click()
        {
            throw new InvalidOperationException("The click broke.");
        }

        var main = await RunningAsync(Page("page", _ => new Border()));

        var (thrown, other, content) = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();
            main.Pages[0].ShowCommand.Execute(null);

            Exception? fault = null;

            try
            {
                Click();
            }
            catch (InvalidOperationException exception)
            {
                fault = exception;
            }

            // Never thrown, it has no stack: no page's code threw it, and the app's fault stays the app's.
            var other = main.FailPageThatThrew(new InvalidOperationException("The app broke."));

            return (main.FailPageThatThrew(fault!), other, main.Pages[0].Content);
        });

        Assert.True(thrown);
        Assert.False(other);
        Assert.Equal("System.InvalidOperationException: The click broke.", Assert.IsType<PageFailureViewModel>(content).Message);
    }

    [Fact]
    public async Task APageWhoseTextsCannotBeFoundShowsTheirKeys()
    {
        var resources = new ResourceManager("No.Such.Strings", typeof(ModulePagesTests).Assembly);
        var main = await RunningAsync(new PageDefinition("page", LocalizedText.From(resources, "Title"), null, _ => new Border()));

        var title = await OnTheUIThreadAsync(() =>
        {
            main.Refresh();

            return main.Pages[0].Title;
        });

        Assert.Equal("[Title]", title);
    }
}
