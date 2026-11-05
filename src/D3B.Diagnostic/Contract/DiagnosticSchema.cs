using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>A deterministic schema export for one client build.</summary>
public sealed record DiagnosticSchema
{
    /// <summary>Gets the protocol discriminator.</summary>
    public string Kind { get; init; } = "schema";

    /// <summary>The client build version.</summary>
    [JsonIgnore]
    public string ClientVersion { get; init; } = string.Empty;

    /// <summary>SHA-256 fingerprint of the source descriptor set.</summary>
    [JsonIgnore]
    public string SchemaSha256 { get; init; } = string.Empty;

    /// <summary>Exported messages in ordinal name order.</summary>
    public IReadOnlyList<DiagnosticMessage> Messages { get; init; } = Array.Empty<DiagnosticMessage>();

    /// <summary>Serializes the schema as compact UTF-8 JSON.</summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DiagnosticJsonContext.Protocol.DiagnosticSchema);
    }

    /// <summary>Writes the compact schema JSON to a stream.</summary>
    public void Write(Stream stream)
    {
        JsonSerializer.Serialize(stream, this, DiagnosticJsonContext.Protocol.DiagnosticSchema);
    }
}
