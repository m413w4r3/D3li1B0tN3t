namespace D3B.Diagnostic.Resolution;

/// <summary>A redacted structural or provenance contradiction.</summary>
public sealed record ResolverContradiction
{
    /// <summary>Stable contradiction code.</summary>
    public ResolverContradictionCode Code { get; init; }

    /// <summary>Redacted explanation containing structure only.</summary>
    public string Detail { get; init; } = string.Empty;
}
