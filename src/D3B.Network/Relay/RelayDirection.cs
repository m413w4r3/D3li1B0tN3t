namespace D3B.Network.Relay;

/// <summary>Which side of a relayed connection sent bytes.</summary>
public enum RelayDirection
{
    /// <summary>The official client, towards the server.</summary>
    FromClient,

    /// <summary>The server, towards the client.</summary>
    FromServer,
}
