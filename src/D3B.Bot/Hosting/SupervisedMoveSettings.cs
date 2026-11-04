using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Hosting;

/// <summary>Whether the host reads the supervised one-step move commands, off unless the owner enables it.</summary>
internal sealed class SupervisedMoveSettings
{
    /// <summary>The configuration key that enables the command reader; absent or false leaves it off.</summary>
    public const string EnabledKey = "Supervised:Move:Enabled";

    /// <summary>Gets whether the supervised move command reader is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Reads the setting.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The setting.</returns>
    public static SupervisedMoveSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new SupervisedMoveSettings { Enabled = configuration.GetValue<bool>(EnabledKey) };
    }
}
