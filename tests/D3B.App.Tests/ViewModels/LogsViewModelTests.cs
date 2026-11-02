using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Logs;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.App.Tests.ViewModels;

public sealed class LogsViewModelTests : IAsyncDisposable
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 3, 17, 19, TimeSpan.Zero);

    private readonly TestPlay _play = new();
    private readonly HostProblems _problems = new(TimeProvider.System);
    private readonly IHost _host;
    private readonly Localizer _french = new(Localizer.French);

    public LogsViewModelTests()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(_problems);
        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        await _play.DisposeAsync();
    }

    private static void Write(BotActor bot)
    {
        bot.Journal.Add(new WalkEntry(_at, 0, TestPlay.MapId, 388, WalkOutcome.Arrived, TimeSpan.FromMilliseconds(900)));
        bot.Journal.Add(new MapChangeEntry(_at, 1, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.NorthMapId, MapChangeKind.Scroll, MapChangeOutcome.Changed, TimeSpan.FromMilliseconds(1_400)));
        bot.Journal.Add(new TravelEntry(_at, 0, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.NorthMapId, 1, TravelOutcome.Arrived, TimeSpan.FromMilliseconds(3_300)));
        bot.Journal.Add(new ActivityOverEntry(_at, "patrouille", 1, ActivityOutcome.Failed, "The step failed.", false));
    }

    [Fact]
    public async Task ABotsJournalIsSaidInTheAppsLanguageEachActionOnALine()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice(), new Toasts());

        Write(bot);
        page.Refresh(_host.Services);
        page.Show(bot.Key);

        Assert.Equal(
            [
                ("Joue par la connexion de jeu", null),
                ("Marche vers la case 388", "arrivé · 900 ms"),
                ("Change de carte : 4,-18 → 4,-19, par le haut", "changé · 1,4 s"),
                ("Voyage vers 4,-19", "arrivé · 1 carte · 3,3 s"),
                ("« patrouille » a échoué : The step failed.", "1 étape"),
            ],
            page.Lines.Select(line => (line.Text, line.Detail)));
        Assert.Equal([Tone.Success, Tone.Success, Tone.Success, Tone.Success, Tone.Danger], page.Lines.Select(line => line.Tone));
        Assert.Equal([0.0, 0, 20, 0, 0], page.Lines.Select(line => line.Indent));
        Assert.All(page.Lines, line => Assert.Null(line.Source));
        Assert.Equal("5 lignes gardées", page.Summary);
    }

    [Fact]
    public async Task WhatComesSinceIsAddedAtEachRefresh()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        page.Refresh(_host.Services);

        Assert.Single(page.Lines);

        bot.Journal.Add(new BotEventEntry(_at, BotEvent.PausedByPlayer));
        page.Refresh(_host.Services);

        Assert.Equal(["Joue par la connexion de jeu", "Le joueur a pris la main : le bot est en pause"], page.Lines.Select(line => line.Text));
        Assert.Equal(Tone.Warning, page.Lines[^1].Tone);
    }

    [Fact]
    public async Task AFightsEndSaysItsOutcomeAndGains()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        bot.Journal.Add(new FightEndEntry(_at, FightOutcome.Won, 77, 7, 1));
        bot.Journal.Add(new FightEndEntry(_at, FightOutcome.Lost, 0, 0, 0));
        page.Refresh(_host.Services);

        Assert.Equal(("Combat gagné", "77 XP · 7 kamas · 1 objet", Tone.Success), (page.Lines[^2].Text, page.Lines[^2].Detail, page.Lines[^2].Tone));
        Assert.Equal(("Combat perdu", "0 XP · 0 kamas · 0 objet", Tone.Danger), (page.Lines[^1].Text, page.Lines[^1].Detail, page.Lines[^1].Tone));
    }

    [Fact]
    public async Task AProfileThatDidNotStartOnConnectionSaysWhy()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        bot.Journal.Add(new ProfileNotStartedEntry(_at, "Promenade", "script:promenade.lua", ProfileStartFailure.ActivityMissing, null));
        page.Refresh(_host.Services);

        var line = page.Lines[^1];

        Assert.Equal(("Le profil « Promenade » n'a pas démarré à la connexion", "Activité disparue : script:promenade.lua", Tone.Warning), (line.Text, line.Detail, line.Tone));
    }

    [Fact]
    public async Task TheLevelAndTheTextSoughtFilterTheLines()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        Write(bot);
        bot.Journal.Add(new WalkEntry(_at, 0, TestPlay.MapId, 12, WalkOutcome.Refused, TimeSpan.FromMilliseconds(100)));
        page.Refresh(_host.Services);
        page.ShowProblemsCommand.Execute(null);

        Assert.Equal(["« patrouille » a échoué : The step failed.", "Marche vers la case 12"], page.Lines.Select(line => line.Text));

        page.ShowErrorsCommand.Execute(null);

        Assert.Single(page.Lines);

        page.ShowAllLevelsCommand.Execute(null);
        page.Search = "CASE";

        Assert.Equal(["Marche vers la case 388", "Marche vers la case 12"], page.Lines.Select(line => line.Text));
        Assert.Equal("2 lignes affichées sur 6", page.Summary);
    }

    [Fact]
    public async Task EverySourceNamesEachLinesAndTheHostShowsItsProblems()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice(), new Toasts());

        _problems.Problems.Add(new HostProblem(_at, LogLevel.Warning, "D3B.Bot.Runtime.Bots", "Game connection 7: its account is not known, so no bot plays its character", null));
        page.Refresh(_host.Services);

        Assert.True(page.ShowsSources);
        Assert.Equal(["Toutes les sources", "Hôte", "Character"], page.Sources.Select(source => source.Name));
        Assert.Equal(["Character", "Hôte"], page.Lines.Select(line => line.Source).Order(StringComparer.Ordinal));

        page.Sources[1].SelectCommand.Execute(null);

        var problem = Assert.Single(page.Lines);

        Assert.Equal(("Game connection 7: its account is not known, so no bot plays its character", "Bots", Tone.Warning), (problem.Text, problem.Detail, problem.Tone));
        Assert.False(page.ShowsSources);
    }

    [Fact]
    public async Task ANewLanguageSaysTheLinesAgain()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        page.Refresh(_host.Services);
        _french.Use(Localizer.English);
        page.Refresh(_host.Services);

        Assert.Equal(["Plays through the game connection"], page.Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task TheBotLookedAtIsTheOneTheInspectorLooksAt()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var choice = new BotChoice();
        var page = new LogsViewModel(_french, choice, new Toasts());

        page.Refresh(_host.Services);
        page.Sources[2].SelectCommand.Execute(null);

        Assert.Equal(bot.Key, choice.Key);
    }
}
