namespace D3B.Diagnostic.Resolution;

/// <summary>A pure promotion-gate result containing stable refusal codes.</summary>
public sealed record PromotionDecision
{
    /// <summary>Whether all promotion gates passed.</summary>
    public bool Allowed { get; init; }

    /// <summary>Stable refusal codes in deterministic gate order.</summary>
    public IReadOnlyList<string> RefusalCodes { get; init; } = Array.Empty<string>();
}
