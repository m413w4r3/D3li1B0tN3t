namespace D3B.Diagnostic.Contract;

/// <summary>Client hello sent to the diagnostic engine.</summary>
public sealed record DiagnosticHello
{
    /// <summary>The protocol discriminator.</summary>
    public string Kind { get; init; } = "hello";
    /// <summary>Supported protocol version.</summary>
    public int ProtocolVersion { get; init; } = DiagnosticProtocol.ProtocolVersion;
    /// <summary>Client build version.</summary>
    public string ClientVersion { get; init; } = string.Empty;
    /// <summary>Source schema fingerprint.</summary>
    public string SchemaSha256 { get; init; } = string.Empty;
}
