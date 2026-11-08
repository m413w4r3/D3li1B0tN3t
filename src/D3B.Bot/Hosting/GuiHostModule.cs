using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Hosting;

/// <summary>Passes validated host GUI input configuration to the bot runtime without registering GUI actions.</summary>
public sealed class GuiHostModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.Services.AddSingleton(services => new GuiHostInputMode(services.GetRequiredService<GuiHostCapability>().InputMode));
    }
}
