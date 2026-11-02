using D3B.App.Hosting;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Pages;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Storage;
using D3B.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests;

/// <summary>Hosts holding a test's bots, as the app's window reads them.</summary>
internal static class TestHosts
{
    /// <summary>A host of the bots a test plays, with its activities, never started: the window only reads its services.</summary>
    public static IHost Of(TestPlay play, params ActivityDefinition[] activities)
    {
        return Of(play, [], activities);
    }

    /// <summary>A host of the bots a test plays, with its pages and activities, never started.</summary>
    public static IHost Of(TestPlay play, PageDefinition[] pages, params ActivityDefinition[] activities)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Register(builder.Services, play);
        builder.Services.AddSingleton(new ActivityCatalog(activities));
        builder.Services.AddSingleton(new PageCatalog(pages));

        return builder.Build();
    }

    /// <summary>A host of the bots a test plays, with its activities, the game's values of a small world and a database of the test's own, opened.</summary>
    public static async Task<IHost> WithStorageAsync(TestPlay play, string path, TestGameData game, params ActivityDefinition[] activities)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Register(builder.Services, play);
        builder.Services.AddSingleton(new ActivityCatalog(activities));
        builder.Services.AddSingleton(new PageCatalog([]));
        builder.Services.AddSingleton(game.References);
        builder.Services.AddSingleton(game.Texts);
        builder.Services.AddSingleton<TimeProvider>(play.Time);
        StorageModule.Register(builder.Services, new StorageSettings { Path = path });

        var host = builder.Build();

        Assert.Null(await host.Services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken));

        return host;
    }

    /// <summary>The app's runner of a host that started.</summary>
    public static async Task<HostRunner> RunningAsync(IHost host)
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(host, null)));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is HostStatus.Running, TimeSpan.FromSeconds(10));

        return runner;
    }

    private static void Register(IServiceCollection services, TestPlay play)
    {
        services.AddSingleton(play.Bots);
        services.AddSingleton(play.World);
        services.AddSingleton(new ClientBuild(TestInstall.Version, null));
    }
}
