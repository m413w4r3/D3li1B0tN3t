namespace D3B.Diagnostic.Contract;

/// <summary>End-of-input marker for replay.</summary>
public sealed record DiagnosticEnd
{
    /// <summary>The protocol discriminator.</summary>
    public string Kind { get; init; } = "end";
}
