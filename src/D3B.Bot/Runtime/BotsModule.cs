using D3B.Bot.Activities;
using D3B.Bot.Data;
using D3B.Bot.Fights;
using D3B.Bot.Hosting;
using D3B.Bot.Inspection;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Network;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.World;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>The bots' run time: one actor per character a game connection chose, fed by the game it observes (D9, D65).</summary>
/// <remarks>
/// Registers <see cref="Bots"/>, which every module reaches its bots through, as an <see cref="IGameObserver"/> of the
/// network module's game sessions, the <see cref="ActivityCatalog"/> of the activities modules register with
/// <see cref="ActivityServiceCollectionExtensions.AddActivity{TActivity}(IServiceCollection, string, LocalizedText)"/>, which the app starts, and the
/// <see cref="PageCatalog"/> of the pages they register with <see cref="PageServiceCollectionExtensions.AddPage"/>, which
/// the app shows, the <see cref="GameReferences"/> the activities' settings name, and the <see cref="GraphicSkills"/> every map
/// a bot enters teaches. The bots wait the <see cref="BotDelays"/> the configuration names, in seconds under
/// <c>Delays</c>. Requires the data module, whose maps behaviors walk, and the network module.
/// </remarks>
public sealed class BotsModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<DataModule>();
        bot.AddModule<NetworkModule>();

        // Read as the module is built, so that a delay below its measured value stops the host before it starts (D151).
        var delays = BotDelays.Read(bot.Configuration);

        bot.Services.AddSingleton(services => new Bots(
            services.GetRequiredService<GameMaps>(),
            services.GetRequiredService<GameWorld>(),
            services.GetRequiredService<GameTables>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILoggerFactory>())
        {
            Delays = delays,

            // Measuring the AI on fights the player starts, through D3B.Host, which has no card to switch it on (D198, D244).
            FightsAlone = bot.Configuration.GetValue<bool>("Fights:Alone"),
        });
        bot.Services.AddSingleton<IGameObserver>(services => services.GetRequiredService<Bots>());

        // Whatever the bots do, every map they enter teaches what its elements' graphics offer (D138).
        bot.Services.TryAddSingleton<GraphicSkills>();
        bot.Services.TryAddSingleton(services => new GraphicPlaces(services.GetRequiredService<GamePack>(), services.GetRequiredService<GameWorld>()));
        bot.Services.AddSingleton<IGameObserver>(services => new GraphicLearner(services.GetRequiredService<GameMaps>(), services.GetRequiredService<GraphicSkills>()));

        // Whoever fights, every fight begun teaches its placement cells and maps, which the server alone gives (D180, D275).
        bot.Services.TryAddSingleton<FightPlacements>();
        bot.Services.AddSingleton<IGameObserver>(services => new FightPlacementLearner(services.GetRequiredService<FightPlacements>(), services.GetRequiredService<GameWorld>(), services.GetRequiredService<TimeProvider>()));

        // Recording always, after the bots, so that the protocol inspector shows what came before it was opened (D115).
        bot.Services.AddSingleton(services => new FrameRecorder(services.GetRequiredService<TimeProvider>()));
        bot.Services.AddSingleton<IGameObserver>(services => services.GetRequiredService<FrameRecorder>());

        // Beside the host's other logs: the app's logs page reads what the host says of itself (D114).
        bot.Services.AddSingleton(services => new HostProblems(services.GetRequiredService<TimeProvider>()));
        bot.Services.AddSingleton<ILoggerProvider>(services => services.GetRequiredService<HostProblems>());
        bot.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        bot.Services.AddSingleton(services => new PageCatalog(services.GetServices<PageDefinition>()));

        // Read once something asks, the app's settings or a profile started: a host that starts no activity never reads them.
        bot.Services.AddSingleton(services =>
        {
            var tables = services.GetRequiredService<GameTables>();
            var world = services.GetRequiredService<GameWorld>();

            return new GameReferences(
                world.Atlas,
                tables.Jobs,
                tables.Items,
                world.Graph,
                services.GetRequiredService<GraphicPlaces>(),
                services.GetRequiredService<GraphicSkills>(),
                tables.Monsters);
        });
    }
}
