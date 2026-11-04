using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>Adds the opt-in supervised one-step move reader to a host, disabled unless <c>Supervised:Move:Enabled</c> is true.</summary>
/// <remarks>
/// The module is added by <c>D3B.Host</c> alone, not by the default modules, so the desktop app never reads standard input.
/// Even enabled, it reacts only to an explicit supervisor line and never to a game connection; see
/// <see cref="SupervisedMoveConsole"/>.
/// </remarks>
public sealed class SupervisedMoveModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();

        var settings = SupervisedMoveSettings.Read(bot.Configuration);

        bot.Services.AddSingleton(services => new SupervisedMoveSession(services.GetRequiredService<Bots>()));

        if (!settings.Enabled)
            return;

        bot.Services.AddSingleton(services => new SupervisedMoveConsole(
            services.GetRequiredService<SupervisedMoveSession>(),
            services.GetRequiredService<ILoggerFactory>()));
        bot.Services.AddHostedService(services => services.GetRequiredService<SupervisedMoveConsole>());
    }
}
