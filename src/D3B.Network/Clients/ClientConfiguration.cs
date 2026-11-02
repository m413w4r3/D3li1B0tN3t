using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace D3B.Network.Clients;

/// <summary>The configuration the client reads at start, a JSON file its version file names (client.md, Connection).</summary>
/// <remarks>
/// The client takes its connection hosts from <c>connectionHosts</c>; every other key, the chat's host, Haapi's URLs and
/// the rest, D3B leaves as Ankama wrote it.
/// </remarks>
public sealed class ClientConfiguration
{
    private readonly JsonObject _json;

    /// <summary>Gets the connection hosts, in the configuration's order.</summary>
    public IReadOnlyList<ConnectionHost> ConnectionHosts { get; }

    private ClientConfiguration(JsonObject json, IReadOnlyList<ConnectionHost> connectionHosts)
    {
        _json = json;
        ConnectionHosts = connectionHosts;
    }

    /// <summary>Parses a configuration.</summary>
    /// <param name="json">The configuration's JSON.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="InvalidDataException">The JSON is no object, or lists no connection host in the form the client reads.</exception>
    public static ClientConfiguration Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root || root["connectionHosts"] is not JsonArray hosts)
                throw new InvalidDataException("The client's configuration has no connectionHosts list.");

            return new ClientConfiguration(root, [.. hosts.Select(host => ConnectionHost.Parse(host?.GetValue<string>() ?? string.Empty))]);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("The client's configuration is not the JSON D3B reads.", exception);
        }
    }

    /// <summary>Gets the configuration's text with every connection host sent to an address, the rest as it was.</summary>
    /// <param name="address">D3B's address.</param>
    /// <returns>The JSON.</returns>
    public string RedirectedTo(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var json = _json.DeepClone().AsObject();

        json["connectionHosts"] = new JsonArray([.. ConnectionHosts.Select(host => JsonValue.Create((host with { Host = address.ToString() }).ToString()))]);

        return json.ToJsonString();
    }
}
