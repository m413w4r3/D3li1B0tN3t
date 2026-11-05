using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>A redacted frame-level problem and its structural location.</summary>
public sealed record DiagnosticProblem
{
    /// <summary>The stable problem code.</summary>
    public DiagnosticProblemCode Code { get; init; }

    /// <summary>The protobuf field path, when available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FieldPath { get; init; }

    /// <summary>A redacted structural detail, when available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }
}
