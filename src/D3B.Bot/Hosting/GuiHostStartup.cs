using D3B.Gui.Safety;
using D3B.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Hosting;

/// <summary>Validates GUI options at a host's startup boundary.</summary>
public static class GuiHostStartup
{
    /// <summary>Validates the Gui configuration section and returns the host's non-activating GUI capability.</summary>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The validated GUI capability.</returns>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentException">One or more GUI options violate the GUI safety policy.</exception>
    public static GuiHostCapability Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection("Gui").Get<GuiOptions>() ?? new GuiOptions();

        GuiOptionsValidator.ValidateOrThrow(options);

        return new GuiHostCapability(options.Enabled, options.Mode, options.AllowRealInput, options.InputMode);
    }

    /// <summary>Validates GUI configuration and wires the validated input mode into the host's bot module.</summary>
    /// <param name="bot">The host builder.</param>
    /// <returns>The validated GUI capability.</returns>
    public static GuiHostCapability Register(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        GuiHostCapability capability = Validate(bot.Configuration);
        bot.Services.AddSingleton(capability);
        bot.AddModule<GuiHostModule>();

        return capability;
    }
}
