using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Hosting;

/// <summary>Whether the host reads the explicitly supervised element-use commands, off unless enabled.</summary>
internal sealed class SupervisedElementUseSettings
{
    /// <summary>The configuration key that enables the command reader; absent or false leaves it off.</summary>
    public const string EnabledKey = "Supervised:ElementUse:Enabled";

    /// <summary>The configuration key that enables explicit use actions; absent or false leaves them off.</summary>
    public const string ActionEnabledKey = "Supervised:ElementUse:ActionEnabled";

    /// <summary>Gets whether the supervised element-use command reader is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets whether explicit supervised use actions are enabled.</summary>
    public bool ActionEnabled { get; init; }

    /// <summary>Reads the setting.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The setting.</returns>
    public static SupervisedElementUseSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new SupervisedElementUseSettings
        {
            Enabled = configuration.GetValue<bool>(EnabledKey),
            ActionEnabled = configuration.GetValue<bool>(ActionEnabledKey),
        };
    }
}
