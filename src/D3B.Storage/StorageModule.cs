using D3B.Bot.Fights;
using D3B.Bot.Harvesting;
using D3B.Bot.Hosting;
using D3B.Bot.Interactives;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Profiles;
using D3B.Storage.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>D3B's database, SQLite through EF Core: the accounts and characters the relay saw, the profiles, and the maps' graphics the bots learned (D99, D100, D138).</summary>
/// <remarks>
/// <para>
/// The app and <c>D3B.Host</c> both add it to the host's composition, so both read and write one database, in
/// <c>Storage:Path</c> (<c>%LOCALAPPDATA%\D3B\d3b.db</c> by default). Its migrations apply before the host starts
/// (<see cref="IHostPreparation"/>): a database that does not open stops the host, which says why.
/// </para>
/// <para>
/// It registers the <see cref="AccountBook"/>, fed by the game connections it observes, the <see cref="ProfileBook"/>,
/// the <see cref="ProfileStarter"/> that starts a profile on a bot, and starts a character's profile on its connection,
/// when asked. Activities do not see it: profiles are D3B's, and only name the activities modules and scripts register.
/// The <see cref="GraphicRecorder"/> keeps what the bots learn of the graphics, which they share as <see cref="GraphicSkills"/>,
/// the <see cref="RegrowthRecorder"/> how long the resources take to grow back, as <see cref="Regrowths"/> (D145), the
/// <see cref="PlacementRecorder"/> every fight's map and placement cells, as <see cref="FightPlacements"/> (D275), and the
/// <see cref="RunRecorder"/> each run of the bots' activities, which the <see cref="RunBook"/> gives the statistics (D146).
/// </para>
/// </remarks>
public sealed class StorageModule : IModule
{
    internal static void Register(IServiceCollection services, StorageSettings settings)
    {
        services.AddSingleton(settings);
        services.AddDbContextFactory<StorageContext>(options => options.UseSqlite(settings.ConnectionString));
        services.AddSingleton<AccountBook>();
        services.AddSingleton<ProfileBook>();
        services.AddSingleton<ProfileStarter>();
        services.TryAddSingleton<GraphicSkills>();
        services.AddSingleton<GraphicRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<GraphicRecorder>());
        services.TryAddSingleton<Regrowths>();
        services.AddSingleton<RegrowthRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<RegrowthRecorder>());
        services.TryAddSingleton<FightPlacements>();
        services.AddSingleton<PlacementRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<PlacementRecorder>());
        services.AddSingleton<RunBook>();
        services.AddSingleton(provider => new RunRecorder(provider.GetRequiredService<IDbContextFactory<StorageContext>>(), provider.GetRequiredService<RunBook>(), provider, provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<RunRecorder>>()));
        services.AddHostedService(provider => provider.GetRequiredService<RunRecorder>());
        services.AddSingleton<AccountRecorder>();
        services.AddSingleton<IGameObserver>(provider => provider.GetRequiredService<AccountRecorder>());
        services.AddHostedService(provider => provider.GetRequiredService<AccountRecorder>());
        services.AddHostedService<StartOnConnection>();
        services.AddSingleton<IHostPreparation, StoragePreparation>();
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();

        Register(bot.Services, new StorageSettings { Path = Path.GetFullPath(bot.Configuration[StorageSettings.PathKey] ?? StorageSettings.DefaultPath) });
    }
}
