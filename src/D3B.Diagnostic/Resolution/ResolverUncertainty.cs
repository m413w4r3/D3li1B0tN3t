namespace D3B.Diagnostic.Resolution;

/// <summary>A redacted reason a candidate remains unproven.</summary>
public sealed record ResolverUncertainty
{
    /// <summary>Stable uncertainty code.</summary>
    public ResolverUncertaintyCode Code { get; init; }

    /// <summary>Redacted explanation.</summary>
    public string Detail { get; init; } = string.Empty;
}
