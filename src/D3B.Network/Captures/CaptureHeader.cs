namespace D3B.Network.Captures;

/// <summary>What a capture says of its connection, before its records.</summary>
public sealed record CaptureHeader
{
    /// <summary>Gets when the connection was relayed.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets the version of the client installed when it was captured, empty when unknown: decoding needs its mapping.</summary>
    public required string ClientVersion { get; init; }

    /// <summary>Gets the address and port D3B accepted the client's connection on.</summary>
    public required string Listener { get; init; }

    /// <summary>Gets the name of the server's host, as the client asked for it.</summary>
    public required string ServerHost { get; init; }

    /// <summary>Gets the address and port D3B connected to for the client.</summary>
    public required string Server { get; init; }
}
