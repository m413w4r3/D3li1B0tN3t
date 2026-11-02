using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace D3B.Network.Relay;

/// <summary>Where the relay listens, where it keeps the client's configuration, whether it records captures, and how long it waits (D35 to D37, 3f, D151).</summary>
public sealed class RelaySettings
{
    /// <summary>The setting naming D3B's loopback address.</summary>
    public const string AddressKey = "Network:Address";

    /// <summary>The setting listing the ports D3B listens on there.</summary>
    public const string PortsKey = "Network:Ports";

    /// <summary>The setting listing the ports D3B's game listener takes there, which the server selection's answer is rewritten to.</summary>
    public const string GamePortsKey = "Network:Game:Ports";

    /// <summary>The setting naming the directory where D3B keeps the configuration it gives the client.</summary>
    public const string RedirectDirectoryKey = "Network:Redirect:Directory";

    /// <summary>The setting that turns the recording of captures on.</summary>
    public const string CapturesEnabledKey = "Network:Captures:Enabled";

    /// <summary>The setting naming the directory captures are written to.</summary>
    public const string CapturesDirectoryKey = "Network:Captures:Directory";

    /// <summary>Gets D3B's default loopback address, 127.0.13.59, whose last two bytes read <c>0x0D3B</c>.</summary>
    /// <remarks>Not 127.0.0.1, so that D3B takes no port another program listens on there (D34).</remarks>
    public static IPAddress DefaultAddress { get; } = new([127, 0, 13, 59]);

    /// <summary>Gets the ports the client's configuration lists for its connection host, which the launcher chooses among.</summary>
    public static IReadOnlyList<int> DefaultPorts { get; } = [5555, 443];

    /// <summary>Gets the port of D3B's game listener by default: one, since the client connects to the first port it is sent.</summary>
    public static IReadOnlyList<int> DefaultGamePorts { get; } = [5556];

    /// <summary>Gets where captures are written by default: <c>%LOCALAPPDATA%\D3B\captures</c>.</summary>
    public static string DefaultCapturesDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "captures");

    /// <summary>Gets where D3B keeps the client's configuration by default: <c>%LOCALAPPDATA%\D3B\client</c>.</summary>
    public static string DefaultRedirectDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "client");

    /// <summary>Gets the loopback address D3B listens on, which the configuration it gives the client names.</summary>
    public required IPAddress Address { get; init; }

    /// <summary>Gets the ports D3B listens on; the relay connects to the server on the port the client chose.</summary>
    public required IReadOnlyList<int> Ports { get; init; }

    /// <summary>Gets the ports D3B's game listener takes on <see cref="Address"/>; the client's game connection comes there.</summary>
    public required IReadOnlyList<int> GamePorts { get; init; }

    /// <summary>Gets the installed client's directory, whose version file names the client's configuration.</summary>
    public required string ClientDirectory { get; init; }

    /// <summary>Gets where D3B keeps the configuration it gives the client, with Ankama's URL and configuration.</summary>
    public required string RedirectDirectory { get; init; }

    /// <summary>Gets the directory captures are written to, or null when none is recorded.</summary>
    /// <remarks>
    /// The game connection's captures go to its <see cref="GameCapturesDirectory"/>, apart from the connection phase's,
    /// which the mapping tool reads (D43).
    /// </remarks>
    public string? CapturesDirectory { get; init; }

    /// <summary>Gets the directory the game connection's captures are written to, or null when none is recorded.</summary>
    public string? GameCapturesDirectory => CapturesDirectory is { } directory ? Path.Combine(directory, "game") : null;

    /// <summary>Gets how long the relay tries each address of the server.</summary>
    public TimeSpan ConnectTimeout { get; init; } = RelayTimeouts.Default.Connect;

    /// <summary>Gets how long a side may stay open once the other has closed, before both are cut.</summary>
    public TimeSpan CloseTimeout { get; init; } = RelayTimeouts.Default.Close;

    /// <summary>Gets how long the relay holds a frame an interceptor asked for, before forwarding what came as it came.</summary>
    public TimeSpan HoldTimeout { get; init; } = RelayTimeouts.Default.Hold;

    /// <summary>Gets the longest frame the relay holds for an interceptor; a longer one is forwarded as it comes.</summary>
    public int MaxHeldLength { get; init; } = 64 * 1024;

    /// <summary>Gets how long a server selection waits for the client's game connection before it is forgotten.</summary>
    public TimeSpan SelectionLifetime { get; init; } = RelayTimeouts.Default.Selection;

    /// <summary>Tells whether an address can be D3B's: in 127.0.0.0/8, and not 127.0.0.1 (D34).</summary>
    /// <param name="address">The address.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    public static bool IsRelayAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return address.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork
            && IPAddress.IsLoopback(address)
            && !address.Equals(IPAddress.Loopback);
    }

    /// <summary>Reads the settings from a configuration, with the defaults for what it does not set.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="clientDirectory">The installed client's directory, as the data module finds it.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="InvalidOperationException">A setting is not a value the relay accepts, such as a timeout below D3B's (<see cref="RelayTimeouts"/>).</exception>
    public static RelaySettings Read(IConfiguration configuration, string clientDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientDirectory);

        var address = DefaultAddress;

        if (configuration[AddressKey] is { Length: > 0 } configured && (!IPAddress.TryParse(configured, out address!) || !IsRelayAddress(address)))
            throw new InvalidOperationException($"{AddressKey} is '{configured}', which is not an address of 127.0.0.0/8 other than 127.0.0.1.");

        var ports = ReadPorts(configuration, PortsKey);
        var gamePorts = ReadPorts(configuration, GamePortsKey);
        var capturesEnabled = configuration[CapturesEnabledKey] is { } enabled && bool.TryParse(enabled, out var value) && value;
        var timeouts = RelayTimeouts.Read(configuration);

        return new RelaySettings
        {
            Address = address,
            Ports = ports.Count is 0 ? DefaultPorts : ports,
            GamePorts = gamePorts.Count is 0 ? DefaultGamePorts : gamePorts,
            ClientDirectory = clientDirectory,
            RedirectDirectory = PathOr(configuration[RedirectDirectoryKey], DefaultRedirectDirectory),
            CapturesDirectory = capturesEnabled ? PathOr(configuration[CapturesDirectoryKey], DefaultCapturesDirectory) : null,
            ConnectTimeout = timeouts.Connect,
            CloseTimeout = timeouts.Close,
            HoldTimeout = timeouts.Hold,
            SelectionLifetime = timeouts.Selection,
        };
    }

    private static List<int> ReadPorts(IConfiguration configuration, string key)
    {
        var ports = new List<int>();

        foreach (var value in configuration.GetSection(key).GetChildren().Select(port => port.Value))
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port > IPEndPoint.MaxPort)
                throw new InvalidOperationException($"{key} lists '{value}', which is not a port.");

            ports.Add(port);
        }

        return ports;
    }

    private static string PathOr(string? configured, string fallback)
    {
        // As for the data module, a relative path starts from the host, never from the directory it was started in.
        return string.IsNullOrWhiteSpace(configured) ? fallback : Path.GetFullPath(configured, AppContext.BaseDirectory);
    }
}
