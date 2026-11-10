using D3B.Bot.Interactives;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.Texts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>Adds the opt-in supervised element-use reader to a host, disabled unless explicitly enabled.</summary>
/// <remarks>
/// The module is added by <c>D3B.Host</c> alone, not by the default modules, so the desktop app never reads standard
/// input. Even enabled, a use happens only after an explicit supervisor command and never on a game connection.
/// </remarks>
public sealed class SupervisedElementUseModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();

        var settings = SupervisedElementUseSettings.Read(bot.Configuration);

        bot.Services.AddSingleton(services => new SupervisedElementUseSession(
            services.GetRequiredService<Bots>(),
            services.GetRequiredService<GameTables>(),
            services.GetRequiredService<GraphicSkills>(),
            ItemNames(services)));

        if (!settings.Enabled)
            return;

        bot.Services.AddSingleton(services => new SupervisedElementUseConsole(
            services.GetRequiredService<SupervisedElementUseSession>(),
            services.GetRequiredService<ILoggerFactory>())
        {
            ActionEnabled = settings.ActionEnabled,
        });
        bot.Services.AddHostedService(services => services.GetRequiredService<SupervisedElementUseConsole>());
    }

    private static Func<int, string?> ItemNames(IServiceProvider services)
    {
        var texts = services.GetRequiredService<GameTexts>();
        var pack = services.GetRequiredService<GamePack>();
        var tables = services.GetRequiredService<GameTables>();
        var languages = TextLanguages.All
            .Where(language => pack.Contains(TextTable.EntryName(language)))
            .ToArray();

        return itemId =>
        {
            try
            {
                if (!tables.Items.TryGetItem(itemId, out var item))
                    return null;

                foreach (var language in languages)
                {
                    if (texts.TryGet(language, item.NameId, out var name))
                        return name;
                }
            }
            catch (Exception exception) when (exception is KeyNotFoundException or PackFormatException)
            {
                return null;
            }

            return null;
        };
    }
}
