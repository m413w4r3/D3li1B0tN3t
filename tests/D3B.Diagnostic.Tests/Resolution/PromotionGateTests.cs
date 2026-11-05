using D3B.Diagnostic.Resolution;

namespace D3B.Diagnostic.Tests.Resolution;

public sealed class PromotionGateTests
{
    [Theory]
    [InlineData("NOT_VERIFIED")]
    [InlineData("CONTRADICTION")]
    [InlineData("BUILD_MISMATCH")]
    [InlineData("PARTIAL_OBSERVATION")]
    [InlineData("NO_FIXTURE")]
    [InlineData("NO_AUDIT")]
    [InlineData("RENUMBERED_CONTRACT")]
    [InlineData("NO_OWNER_APPROVAL")]
    [InlineData("LIVE_SESSION")]
    public void RefusesEachPromotionGateFailure(string expectedCode)
    {
        MappingCandidate candidate = CreateCandidate();
        CandidateReport report = CreateReport(candidate);
        PromotionContext context = CreateContext();

        switch (expectedCode)
        {
            case "NOT_VERIFIED":
                candidate = candidate with { Classification = CandidateClassification.Candidate };
                report = ReplaceCandidate(report, candidate);
                break;
            case "CONTRADICTION":
                candidate = candidate with
                {
                    Contradictions = new[]
                    {
                        new ResolverContradiction { Code = ResolverContradictionCode.FieldTypeMismatch }
                    }
                };
                report = ReplaceCandidate(report, candidate);
                break;
            case "BUILD_MISMATCH":
                report = report with { SchemaSha256 = "different-schema" };
                break;
            case "PARTIAL_OBSERVATION":
                report = report with
                {
                    Observations = new CandidateObservationSummary { Coverage = "partial", Dropped = 1 }
                };
                break;
            case "NO_FIXTURE":
                candidate = candidate with { FixtureProposal = null };
                report = ReplaceCandidate(report, candidate);
                break;
            case "NO_AUDIT":
                context = context with { ContractAuditFailures = null };
                break;
            case "RENUMBERED_CONTRACT":
                context = context with { RenumberedFields = new[] { "MapInformationRequest.map_id" } };
                break;
            case "NO_OWNER_APPROVAL":
                context = context with { OwnerApprovalToken = null };
                break;
            case "LIVE_SESSION":
                context = context with { LiveSessionActive = true };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedCode));
        }

        PromotionDecision decision = PromotionGate.Evaluate(candidate, report, context);

        Assert.False(decision.Allowed);
        Assert.Equal(expectedCode, Assert.Single(decision.RefusalCodes));
    }

    [Fact]
    public void AllowsPromotionWhenEveryGatePasses()
    {
        MappingCandidate candidate = CreateCandidate();
        CandidateReport report = CreateReport(candidate);
        PromotionContext context = CreateContext();

        PromotionDecision decision = PromotionGate.Evaluate(candidate, report, context);

        Assert.True(decision.Allowed);
        Assert.Empty(decision.RefusalCodes);
    }

    [Fact]
    public void RefusesDetachedCandidateNotPresentInReport()
    {
        MappingCandidate candidate = CreateCandidate() with { Token = "other" };

        PromotionDecision decision = PromotionGate.Evaluate(candidate, CreateReport(CreateCandidate()), CreateContext());

        Assert.False(decision.Allowed);
        Assert.Contains("NOT_VERIFIED", decision.RefusalCodes);
    }

    [Fact]
    public void DoesNotTrustDetachedCandidateFlagsWhenReportCandidateIsUnverified()
    {
        MappingCandidate reportCandidate = CreateCandidate() with
        {
            Classification = CandidateClassification.Candidate,
            ExactShape = false,
            UniqueShape = false,
            HasValidObservation = false
        };
        MappingCandidate detachedCandidate = CreateCandidate();

        PromotionDecision decision = PromotionGate.Evaluate(detachedCandidate, CreateReport(reportCandidate), CreateContext());

        Assert.False(decision.Allowed);
        Assert.Contains("NOT_VERIFIED", decision.RefusalCodes);
    }

    [Fact]
    public void RefusesEvidenceWithoutDistinctSchemaOrReadableSource()
    {
        MappingCandidate candidate = CreateCandidate() with
        {
            Evidence = new[]
            {
                new CandidateEvidence { Source = "reference", Verified = true },
                new CandidateEvidence { Source = "prior_mapping", Verified = true }
            }
        };

        PromotionDecision decision = PromotionGate.Evaluate(candidate, CreateReport(candidate), CreateContext());

        Assert.False(decision.Allowed);
        Assert.Contains("NOT_VERIFIED", decision.RefusalCodes);
    }

    [Fact]
    public void AllowsReportCandidateWithDistinctVerifiedSchemaAndReadableEvidence()
    {
        MappingCandidate candidate = CreateCandidate() with
        {
            Evidence = new[]
            {
                new CandidateEvidence { Source = "schema", Verified = true },
                new CandidateEvidence { Source = "readable", Verified = true }
            }
        };

        PromotionDecision decision = PromotionGate.Evaluate(candidate, CreateReport(candidate), CreateContext());

        Assert.True(decision.Allowed);
        Assert.Empty(decision.RefusalCodes);
    }

    private static MappingCandidate CreateCandidate()
    {
        return new MappingCandidate
        {
            Token = "lok",
            Readable = "d3b.game.MapInformationRequest",
            Classification = CandidateClassification.Verified,
            BuildExact = true,
            ExactShape = true,
            UniqueShape = true,
            HasValidObservation = true,
            Evidence = new[]
            {
                new CandidateEvidence { Source = "schema", Verified = true },
                new CandidateEvidence { Source = "observation", Verified = true }
            },
            ClientVersion = "3.7.13.17",
            SchemaSha256 = new string('a', 64),
            FixtureProposal = new SyntheticFixtureProposal
            {
                SchemaMessage = "lok",
                ExpectedStatus = "VALID",
                ExpectedFieldNumbers = new[] { 1 },
                Synthetic = true,
                PayloadHex = "0800"
            }
        };
    }

    private static CandidateReport CreateReport(MappingCandidate candidate)
    {
        return new CandidateReport
        {
            ClientVersion = candidate.ClientVersion,
            SchemaSha256 = candidate.SchemaSha256,
            Observations = new CandidateObservationSummary { Frames = 1, Coverage = "complete", Dropped = 0 },
            Candidates = new[] { candidate }
        };
    }

    private static CandidateReport ReplaceCandidate(CandidateReport report, MappingCandidate candidate)
    {
        return report with { Candidates = new[] { candidate } };
    }

    private static PromotionContext CreateContext()
    {
        return new PromotionContext
        {
            TargetClientVersion = "3.7.13.17",
            TargetSchemaSha256 = new string('a', 64),
            FixtureRedGreenPassed = true,
            ContractAuditFailures = Array.Empty<string>(),
            RenumberedFields = Array.Empty<string>(),
            OwnerApprovalToken = "P2K2-PROMOTE:lok:3.7.13.17",
            LiveSessionActive = false
        };
    }
}
