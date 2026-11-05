namespace D3B.Diagnostic.Resolution;

/// <summary>Inputs required to evaluate whether a verified candidate may be promoted.</summary>
public sealed record PromotionContext
{
    /// <summary>The client version for the target build.</summary>
    public string TargetClientVersion { get; init; } = string.Empty;

    /// <summary>The schema fingerprint for the target build.</summary>
    public string TargetSchemaSha256 { get; init; } = string.Empty;

    /// <summary>Whether the reproducible synthetic RED/GREEN fixture passed.</summary>
    public bool FixtureRedGreenPassed { get; init; }

    /// <summary>Contract audit failures, or null when no audit was run.</summary>
    public IReadOnlyList<string>? ContractAuditFailures { get; init; }

    /// <summary>Readable field or enum identifiers that would be renumbered.</summary>
    public IReadOnlyList<string> RenumberedFields { get; init; } = Array.Empty<string>();

    /// <summary>The explicit owner approval token, if supplied.</summary>
    public string? OwnerApprovalToken { get; init; }

    /// <summary>Whether a live session is active.</summary>
    public bool LiveSessionActive { get; init; }
}
