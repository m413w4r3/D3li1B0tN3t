namespace D3B.Diagnostic.Contract;

/// <summary>Redacted engine error response.</summary>
public sealed record DiagnosticError
{
    /// <summary>The protocol discriminator.</summary>
    public string Kind { get; init; } = "error";
    /// <summary>Stable problem code.</summary>
    public DiagnosticErrorCode Code { get; init; }
    /// <summary>Optional redacted explanation.</summary>
    public string? Detail { get; init; }
}
