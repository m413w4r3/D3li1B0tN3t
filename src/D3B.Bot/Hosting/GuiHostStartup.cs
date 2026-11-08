using D3B.Gui.Safety;
using Microsoft.Extensions.Configuration;

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

        return new GuiHostCapability(options.Enabled, options.Mode, options.AllowRealInput);
    }
}
