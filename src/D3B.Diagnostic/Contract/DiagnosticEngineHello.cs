namespace D3B.Diagnostic.Contract;

/// <summary>Engine identity returned during handshake.</summary>
public sealed record DiagnosticEngineHello
{
    /// <summary>The protocol discriminator.</summary>
    public string Kind { get; init; } = "hello";
    /// <summary>Supported protocol version.</summary>
    public int ProtocolVersion { get; init; }
    /// <summary>Engine name.</summary>
    public string Engine { get; init; } = string.Empty;
    /// <summary>Engine version.</summary>
    public string EngineVersion { get; init; } = string.Empty;
    /// <summary>Source revision of the engine.</summary>
    public string SnsCommit { get; init; } = string.Empty;
}
