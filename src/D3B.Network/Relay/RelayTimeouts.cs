using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace D3B.Network.Relay;

/// <summary>How long the relay waits for a server, a side or a frame: each a fixed value, D3B's by default, which a user may raise (D151).</summary>
/// <remarks>
/// Read once, when the relay is built: a change takes effect from the host's next start. In the host's configuration,
/// in seconds (<c>Network:Timeouts:Connect</c> and the others).
/// </remarks>
public sealed record RelayTimeouts
{
    /// <summary>The setting naming how long the relay tries each address of the server.</summary>
    public const string ConnectKey = "Network:Timeouts:Connect";

    /// <summary>The setting naming how long a side may stay open once the other has closed.</summary>
    public const string CloseKey = "Network:Timeouts:Close";

    /// <summary>The setting naming how long the relay holds a frame an interceptor asked for.</summary>
    public const string HoldKey = "Network:Timeouts:Hold";

    /// <summary>The setting naming how long a server selection waits for the client's game connection.</summary>
    public const string SelectionKey = "Network:Timeouts:Selection";

    /// <summary>Gets D3B's timeouts, which are each the least.</summary>
    public static RelayTimeouts Default { get; } = new();

    /// <summary>Gets the most each timeout may be raised to.</summary>
    public static RelayTimeouts Most { get; } = new()
    {
        Connect = TimeSpan.FromSeconds(60),
        Close = TimeSpan.FromSeconds(30),
        Hold = TimeSpan.FromSeconds(30),
        Selection = TimeSpan.FromMinutes(10),
    };

    /// <summary>Gets how long the relay tries each address of the server.</summary>
    public TimeSpan Connect { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long a side may stay open once the other has closed, before both are cut.</summary>
    public TimeSpan Close { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets how long the relay holds a frame an interceptor asked for, before forwarding what came as it came.</summary>
    public TimeSpan Hold { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets how long a server selection waits for the client's game connection before it is forgotten.</summary>
    public TimeSpan Selection { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Reads the timeouts from a configuration, in seconds, D3B's for what it does not set.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The timeouts.</returns>
    /// <exception cref="InvalidOperationException">A timeout is not a number of seconds from D3B's value to its most.</exception>
    public static RelayTimeouts Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new RelayTimeouts
        {
            Connect = ReadTimeout(configuration, ConnectKey, Default.Connect, Most.Connect),
            Close = ReadTimeout(configuration, CloseKey, Default.Close, Most.Close),
            Hold = ReadTimeout(configuration, HoldKey, Default.Hold, Most.Hold),
            Selection = ReadTimeout(configuration, SelectionKey, Default.Selection, Most.Selection),
        };
    }

    /// <summary>Gets the configuration these timeouts read back from, in seconds: a timeout left as D3B's is not set.</summary>
    /// <returns>Each timeout's setting, with no value for one left as D3B's.</returns>
    public IReadOnlyDictionary<string, string?> ToConfiguration()
    {
        return new Dictionary<string, string?>
        {
            [ConnectKey] = Seconds(Connect, Default.Connect),
            [CloseKey] = Seconds(Close, Default.Close),
            [HoldKey] = Seconds(Hold, Default.Hold),
            [SelectionKey] = Seconds(Selection, Default.Selection),
        };
    }

    private static TimeSpan ReadTimeout(IConfiguration configuration, string key, TimeSpan least, TimeSpan most)
    {
        if (configuration[key] is not { Length: > 0 } value)
            return least;

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !(seconds >= least.TotalSeconds && seconds <= most.TotalSeconds))
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{key} is '{value}', which is not a number of seconds from {least.TotalSeconds} to {most.TotalSeconds}."));

        return TimeSpan.FromSeconds(seconds);
    }

    private static string? Seconds(TimeSpan timeout, TimeSpan least)
    {
        return timeout == least ? null : timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture);
    }
}
