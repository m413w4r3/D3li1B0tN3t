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
        MappingCandidate? reportCandidate = report.Candidates.FirstOrDefault(candidateInReport =>
            string.Equals(candidateInReport.Token, candidate.Token, StringComparison.Ordinal)
            && string.Equals(candidateInReport.Readable, candidate.Readable, StringComparison.Ordinal));

        if (reportCandidate is null
            || reportCandidate.Classification != CandidateClassification.Verified
            || !reportCandidate.ExactShape
            || !reportCandidate.UniqueShape
            || !reportCandidate.HasValidObservation
            || !HasRequiredVerifiedEvidence(reportCandidate))
        {
            refusals.Add("NOT_VERIFIED");
        }

        if (reportCandidate is not null && reportCandidate.Contradictions.Count > 0)
        {
            refusals.Add("CONTRADICTION");
        }

        if (reportCandidate is null
            || !reportCandidate.BuildExact
            || !string.Equals(reportCandidate.ClientVersion, context.TargetClientVersion, StringComparison.Ordinal)
            || !string.Equals(reportCandidate.SchemaSha256, context.TargetSchemaSha256, StringComparison.Ordinal)
            || !string.Equals(report.ClientVersion, context.TargetClientVersion, StringComparison.Ordinal)
            || !string.Equals(report.SchemaSha256, context.TargetSchemaSha256, StringComparison.Ordinal))
        {
            refusals.Add("BUILD_MISMATCH");
        }

        if (report.Dropped > 0 || !string.Equals(report.Coverage, "complete", StringComparison.Ordinal))
        {
            refusals.Add("PARTIAL_OBSERVATION");
        }

        if (reportCandidate is null
            || reportCandidate.FixtureProposal is null
            || !reportCandidate.FixtureProposal.Synthetic
            || !context.FixtureRedGreenPassed)
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
            reportCandidate?.Token ?? candidate.Token,
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

    private static bool HasRequiredVerifiedEvidence(MappingCandidate candidate)
    {
        HashSet<string> verifiedSources = candidate.Evidence
            .Where(evidence => evidence.Verified)
            .Select(evidence => evidence.Source)
            .ToHashSet(StringComparer.Ordinal);

        return verifiedSources.Count >= 2
            && (verifiedSources.Contains("schema") || verifiedSources.Contains("readable"));
    }
}
