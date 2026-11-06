using D3B.Diagnostic.Contract;
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
        Assert.Contains(expectedCode, decision.RefusalCodes);
        if (expectedCode == "BUILD_MISMATCH")
        {
            Assert.Contains("NO_SEMANTIC_PROOF", decision.RefusalCodes);
        }
        else
        {
            Assert.Equal(expectedCode, Assert.Single(decision.RefusalCodes));
        }
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
    public void RefusesStructuralOnlyVerifiedCandidateWithNoSemanticProof()
    {
        MappingCandidate candidate = CreateCandidate();
        SetCandidateProperty(candidate, "Proof", null);
        SetCandidateProperty(candidate, "SemanticIdentity", null);

        PromotionDecision decision = PromotionGate.Evaluate(candidate, CreateReport(candidate), CreateContext());

        Assert.Contains("NO_SEMANTIC_PROOF", decision.RefusalCodes);
    }

    [Fact]
    public void DetachedCandidateProofCannotOverrideUnprovenReportCandidate()
    {
        MappingCandidate detachedCandidate = CreateCandidateWithReferenceProof();
        Assert.NotNull(GetCandidateProperty(detachedCandidate, "Proof"));
        MappingCandidate reportCandidate = detachedCandidate with { Classification = CandidateClassification.Candidate };
        SetCandidateProperty(reportCandidate, "Proof", null);
        SetCandidateProperty(reportCandidate, "SemanticIdentity", null);

        PromotionDecision decision = PromotionGate.Evaluate(detachedCandidate, CreateReport(reportCandidate), CreateContext());

        Assert.False(decision.Allowed);
        Assert.Contains("NO_SEMANTIC_PROOF", decision.RefusalCodes);
    }

    [Fact]
    public void DoesNotRequireDistinctSchemaOrReadableEvidenceWhenProofMatches()
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

        Assert.True(decision.Allowed);
        Assert.Empty(decision.RefusalCodes);
    }

    [Fact]
    public void DistinctSchemaAndReadableEvidenceCannotReplaceSemanticProof()
    {
        MappingCandidate candidate = CreateCandidate() with
        {
            Evidence = new[]
            {
                new CandidateEvidence { Source = "schema", Verified = true },
                new CandidateEvidence { Source = "readable", Verified = true }
            }
        };
        SetCandidateProperty(candidate, "Proof", null);
        SetCandidateProperty(candidate, "SemanticIdentity", null);

        PromotionDecision decision = PromotionGate.Evaluate(candidate, CreateReport(candidate), CreateContext());

        Assert.False(decision.Allowed);
        Assert.Contains("NO_SEMANTIC_PROOF", decision.RefusalCodes);
    }

    [Fact]
    public void MatchingProofAllowsPromotionWhenEveryOtherGatePasses()
    {
        MappingCandidate candidate = CreateCandidateWithReferenceProof();

        PromotionDecision decision = PromotionGate.Evaluate(candidate, CreateReport(candidate), CreateContext());

        Assert.True(decision.Allowed);
        Assert.Empty(decision.RefusalCodes);
    }

    private static MappingCandidate CreateCandidate()
    {
        return CreateCandidateWithReferenceProof();
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

    private static MappingCandidate CreateCandidateWithReferenceProof()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            source = "promotion-test",
            build = "3.7.13.17",
            buildExact = true,
            messages = new[]
            {
                new
                {
                    token = "lok",
                    name = "d3b.game.MapInformationRequest",
                    semanticProof = new
                    {
                        token = "lok",
                        readable = "d3b.game.MapInformationRequest",
                        build = "3.7.13.17",
                        schemaSha256 = new string('a', 64),
                        kind = "AUDITED_REFERENCE",
                        source = "promotion-test",
                        artifactSha256 = new string('b', 64)
                    }
                }
            }
        });
        Google.Protobuf.Reflection.FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("MapInformationRequest",
                ResolverTestData.ReadableField("value", 1, Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type.Int64)));
        CandidateReport report = ResolverTestData.Resolver(readable, [ReferenceCorpusJsonLoader.Load(json)])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "client")], "client");
        return Assert.Single(report.Candidates);
    }

    private static object? GetCandidateProperty(MappingCandidate candidate, string propertyName)
    {
        return typeof(MappingCandidate).GetProperty(propertyName)?.GetValue(candidate);
    }

    private static void SetCandidateProperty(MappingCandidate candidate, string propertyName, object? value)
    {
        typeof(MappingCandidate).GetProperty(propertyName)?.SetValue(candidate, value);
    }
}
