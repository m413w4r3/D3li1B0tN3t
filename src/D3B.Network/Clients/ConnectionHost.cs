using System.Globalization;

namespace D3B.Network.Clients;

/// <summary>A connection host of the client's configuration: a name, the host, and the ports the launcher chooses among.</summary>
/// <param name="Name">The entry's name.</param>
/// <param name="Host">The host the client connects to first, the connection server's.</param>
/// <param name="Ports">The ports, 5555 and 443 as measured (client.md).</param>
public sealed record ConnectionHost(string Name, string Host, IReadOnlyList<int> Ports)
{
    /// <summary>Parses an entry of <c>connectionHosts</c>: <c>&lt;name&gt;:&lt;host&gt;:&lt;port&gt;,&lt;port&gt;</c>.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The connection host.</returns>
    /// <exception cref="InvalidDataException">The entry is not in that form.</exception>
    public static ConnectionHost Parse(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parts = entry.Split(':');

        if (parts.Length is not 3 || parts[0].Length is 0 || Uri.CheckHostName(parts[1]) is UriHostNameType.Unknown or UriHostNameType.Basic)
            throw new InvalidDataException($"The connection host '{entry}' is not <name>:<host>:<ports>.");

        var ports = new List<int>();

        foreach (var port in parts[2].Split(','))
        {
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value is 0 or > 65_535)
                throw new InvalidDataException($"The connection host '{entry}' lists '{port}', which is not a port.");

            ports.Add(value);
        }

        return new ConnectionHost(parts[0], parts[1], ports);
    }

    /// <summary>Formats the entry as the configuration lists it.</summary>
    /// <returns><c>&lt;name&gt;:&lt;host&gt;:&lt;port&gt;,&lt;port&gt;</c>.</returns>
    public override string ToString()
    {
        return $"{Name}:{Host}:{string.Join(',', Ports)}";
    }
}
