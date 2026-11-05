namespace D3B.Diagnostic.Resolution;

/// <summary>Evaluates promotion prerequisites without writing mappings or changing session state.</summary>
public static class PromotionGate
{
    /// <summary>Returns the stable refusal codes for an attempted candidate promotion.</summary>
    public static PromotionDecision Evaluate(
        MappingCandidate candidate,
        CandidateReport report,
        PromotionContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        List<string> refusals = new();

        if (candidate.Classification != CandidateClassification.Verified)
        {
            refusals.Add("NOT_VERIFIED");
        }

        if (candidate.Contradictions.Count > 0)
        {
            refusals.Add("CONTRADICTION");
        }

        if (!candidate.BuildExact
            || !string.Equals(candidate.ClientVersion, context.TargetClientVersion, StringComparison.Ordinal)
            || !string.Equals(candidate.SchemaSha256, context.TargetSchemaSha256, StringComparison.Ordinal)
            || !string.Equals(report.ClientVersion, context.TargetClientVersion, StringComparison.Ordinal)
            || !string.Equals(report.SchemaSha256, context.TargetSchemaSha256, StringComparison.Ordinal))
        {
            refusals.Add("BUILD_MISMATCH");
        }

        if (report.Dropped > 0 || !string.Equals(report.Coverage, "complete", StringComparison.Ordinal))
        {
            refusals.Add("PARTIAL_OBSERVATION");
        }

        if (candidate.FixtureProposal is null || !candidate.FixtureProposal.Synthetic || !context.FixtureRedGreenPassed)
        {
            refusals.Add("NO_FIXTURE");
        }

        if (context.ContractAuditFailures is null || context.ContractAuditFailures.Count > 0)
        {
            refusals.Add("NO_AUDIT");
        }

        if (context.RenumberedFields.Count > 0)
        {
            refusals.Add("RENUMBERED_CONTRACT");
        }

        string expectedApproval = string.Concat(
            "P2K2-PROMOTE:",
            candidate.Token,
            ":",
            context.TargetClientVersion);
        if (!string.Equals(context.OwnerApprovalToken, expectedApproval, StringComparison.Ordinal))
        {
            refusals.Add("NO_OWNER_APPROVAL");
        }

        if (context.LiveSessionActive)
        {
            refusals.Add("LIVE_SESSION");
        }

        return new PromotionDecision
        {
            Allowed = refusals.Count == 0,
            RefusalCodes = refusals.AsReadOnly()
        };
    }
}
