using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Reporting;
using D3B.Diagnostic.Resolution;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Tests.Resolution;

public sealed class DiagnosticReportResolverTests
{
    private static readonly string[] _incompleteReasons =
    [
        "unattributed-analysis", "summary-mismatch", "aggregation-failed", "dropped-frame", "session-incomplete"
    ];

    [Fact]
    public void FullyValidExactBuildReportWithoutSemanticProofRemainsCandidate()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok");
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport resolved = Resolve(schema, report, readable);
        MappingCandidate candidate = Assert.Single(resolved.Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.True(candidate.ExactShape);
        Assert.True(candidate.UniqueShape);
        Assert.True(candidate.HasValidObservation);
        Assert.Null(candidate.FixtureProposal);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
        Assert.NotNull(resolved.Provenance);
        Assert.Equal("diagnostic-report", resolved.Provenance.Source);
        Assert.Contains(candidate.Evidence, evidence => evidence.Source == "observation" && evidence.Detail.Contains("1 observations", StringComparison.Ordinal));
        Assert.DoesNotContain("frame", candidate.Evidence.Single(evidence => evidence.Source == "observation").Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OversizedDirectReportIsRejectedBeforeResolution()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport valid = LoadReport("lok");
        DiagnosticReportType type = Assert.Single(valid.Report.Types);
        int[] shapeValues = Enumerable.Range(0, 4_096).ToArray();
        DiagnosticReport oversizedReport = valid.Report with
        {
            Types =
            [
                type with
                {
                    Fields =
                    [
                        new DiagnosticReportField
                        {
                            Number = 1,
                            Occurrences = 1,
                            WireTypes = [DiagnosticWireType.Varint],
                            Observed = new DiagnosticObservedShape
                            {
                                VarintBytes = shapeValues,
                                FixedLength = shapeValues,
                                Length = shapeValues,
                                FieldCount = shapeValues,
                            },
                        },
                    ],
                },
            ],
        };
        LoadedDiagnosticReport oversized = valid with { Report = oversizedReport };
        DiagnosticReportOptions small = new() { MaxBytes = 65_536, MaxShapeValues = 4_096 };
        DiagnosticReportOptions sufficient = small with { MaxBytes = 4 * 1024 * 1024 };
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        Assert.False(DiagnosticReportJson.IsValidReportObject(oversizedReport, small));
        Assert.True(DiagnosticReportJson.IsValidReportObject(oversizedReport, sufficient));
        DiagnosticReportResolutionException exception = Assert.Throws<DiagnosticReportResolutionException>(
            () => DiagnosticReportResolver.Resolve(schema, oversized, [], [], [readable], small));
        Assert.Equal("diagnostic report is invalid for the build schema", exception.Message);

        CandidateReport resolved = DiagnosticReportResolver.Resolve(schema, oversized, [], [], [readable], sufficient);
        MappingCandidate candidate = Assert.Single(resolved.Candidates);
        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
    }

    [Fact]
    public void NearShapeResemblanceRemainsCandidate()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok");
        FileDescriptor readable = ResolverTestData.PartialReadableFile("StatusMessage", 1, FieldDescriptorProto.Types.Type.Int64);

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable).Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialReadable);
    }

    [Fact]
    public void PartialDroppedOrTruncatedReportNeverVerifies()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        LoadedDiagnosticReport[] reports =
        [
            LoadReport("lok", droppedFrames: 1),
            LoadReport("lok", report => report with { Coverage = "partial" }),
            LoadReport("lok", report => report with
            {
                Coverage = "partial",
                Truncation = report.Truncation with { OutputTruncated = true }
            }),
        ];

        foreach (LoadedDiagnosticReport report in reports)
        {
            MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable).Candidates);

            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialObservation);
        }
    }

    [Fact]
    public void UnattributedAnalysesAndIncompleteReasonsForcePartialEffectiveCoverage()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        LoadedDiagnosticReport complete = LoadReport("lok");
        LoadedDiagnosticReport[] reports =
        [
            complete with { Report = complete.Report with { Session = complete.Report.Session with { UnattributedAnalyses = 1 } } },
            complete with { Report = complete.Report with { Session = complete.Report.Session with { DroppedFrames = 1 } } },
            .. _incompleteReasons.Select(reason => complete with
                {
                    Report = complete.Report with
                    {
                        Truncation = complete.Report.Truncation with { Reasons = [reason] }
                    }
                })
        ];

        foreach (LoadedDiagnosticReport report in reports)
        {
            CandidateReport resolved = Resolve(schema, report, readable);
            MappingCandidate candidate = Assert.Single(resolved.Candidates);

            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialObservation);
            Assert.Equal("partial", resolved.Provenance!.ObservationCoverage);
        }
    }

    [Fact]
    public void EnqueuedAndAnalyzedFrameMismatchForcesPartialEffectiveCoverage()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        var aggregator = new DiagnosticReportAggregator(schema, new DiagnosticReportOptions(), new DiagnosticReportBuildContext
        {
            ClientVersion = ResolverTestData.ClientVersion,
            SchemaSha256 = ResolverTestData.SchemaHash,
            Engine = new DiagnosticEngineHello { ProtocolVersion = 1, Engine = "d3b-diagnostic", EngineVersion = "1.0.0", SnsCommit = "abcdef0" },
            ProducerVersion = "1.0.0",
            StartedAtUtc = DateTimeOffset.UnixEpoch,
        });
        aggregator.Observe(new DiagnosticAnalysis
        {
            CarriedType = "lok",
            Message = "lok",
            Direction = "server",
            Status = DiagnosticStatus.Valid,
            TypeUrlOk = true,
            Fields = [new DiagnosticFieldAnalysis { Number = 1, Name = "obfuscated", DeclaredKind = DiagnosticFieldKind.SignedInteger64 }],
        });
        DiagnosticReport report = aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = "complete",
            EnqueuedFrames = 1,
            AnalyzedFrames = 1,
            CompletedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(1),
        });
        LoadedDiagnosticReport loaded = DiagnosticReportJson.Load(DiagnosticReportJson.SerializeToUtf8Bytes(report, new DiagnosticReportOptions()));
        LoadedDiagnosticReport inconsistent = loaded with
        {
            Report = loaded.Report with { Session = loaded.Report.Session with { EnqueuedFrames = 2 } }
        };
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport resolved = Resolve(schema, inconsistent, readable);
        MappingCandidate candidate = Assert.Single(resolved.Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialObservation);
        Assert.Equal("partial", resolved.Provenance!.ObservationCoverage);
    }

    [Fact]
    public void MixedStatusesNeverVerifyAndHardStatusesConflict()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        LoadedDiagnosticReport mixed = LoadReport("lok", statuses: [DiagnosticStatus.Valid, DiagnosticStatus.Truncated]);
        LoadedDiagnosticReport hard = LoadReport("lok", statuses: [DiagnosticStatus.Valid, DiagnosticStatus.SchemaMismatch]);

        MappingCandidate partial = Assert.Single(Resolve(schema, mixed, readable).Candidates);
        MappingCandidate conflict = Assert.Single(Resolve(schema, hard, readable).Candidates);

        Assert.Equal(CandidateClassification.Candidate, partial.Classification);
        Assert.Contains(partial.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.MixedObservationStatus);
        Assert.Equal(CandidateClassification.Conflict, conflict.Classification);
        Assert.Contains(conflict.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.ObservationStatusMismatch);
    }

    [Theory]
    [InlineData(DiagnosticProblemCode.WireTypeMismatch)]
    [InlineData(DiagnosticProblemCode.UnknownFieldNumber)]
    public void HardSchemaProblemForcesConflict(DiagnosticProblemCode problem)
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", problems: [problem]);
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable).Candidates);

        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.ObservationProblem);
    }

    [Theory]
    [InlineData(DiagnosticProblemCode.DepthExceeded)]
    [InlineData(DiagnosticProblemCode.Truncated)]
    [InlineData(DiagnosticProblemCode.LimitExceeded)]
    public void SoftSchemaProblemWithValidCompleteObservationNeverVerifies(DiagnosticProblemCode problem)
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", problems: [problem]);
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable).Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.ObservationTruncated);
    }

    [Fact]
    public void TypeUrlMismatchForcesConflict()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", typeUrlOk: false);
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable).Candidates);

        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.ObservationTypeUrlMismatch);
        ResolverContradiction reportContradiction = Assert.Single(candidate.Contradictions,
            contradiction => contradiction.Code == ResolverContradictionCode.ObservationReportContradiction);
        Assert.Contains("TYPE_URL_MISMATCH", reportContradiction.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageMismatchInACompleteValidReportForcesConflict()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        var aggregator = new DiagnosticReportAggregator(schema, new DiagnosticReportOptions(), new DiagnosticReportBuildContext
        {
            ClientVersion = ResolverTestData.ClientVersion,
            SchemaSha256 = ResolverTestData.SchemaHash,
            Engine = new DiagnosticEngineHello { ProtocolVersion = 1, Engine = "d3b-diagnostic", EngineVersion = "1.0.0", SnsCommit = "abcdef0" },
            ProducerVersion = "1.0.0",
            StartedAtUtc = DateTimeOffset.UnixEpoch,
        });
        aggregator.Observe(new DiagnosticAnalysis
        {
            CarriedType = "lok",
            Message = "outside.schema.UnrelatedMessage",
            Direction = "server",
            Status = DiagnosticStatus.Valid,
            TypeUrlOk = true,
            Fields = [new DiagnosticFieldAnalysis { Number = 1, Name = "obfuscated", DeclaredKind = DiagnosticFieldKind.SignedInteger64 }],
        });
        DiagnosticReport report = aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = "complete",
            EnqueuedFrames = 1,
            AnalyzedFrames = 1,
            CompletedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(1),
        });
        LoadedDiagnosticReport loaded = DiagnosticReportJson.Load(DiagnosticReportJson.SerializeToUtf8Bytes(report, new DiagnosticReportOptions()));
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        MappingCandidate candidate = Assert.Single(Resolve(schema, loaded, readable).Candidates);

        Assert.Equal("complete", loaded.Report.Coverage);
        Assert.Contains(Assert.Single(loaded.Report.Types).Contradictions,
            contradiction => contradiction.Code == DiagnosticReportContradictionCode.MessageMismatch);
        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        ResolverContradiction resolverContradiction = Assert.Single(candidate.Contradictions,
            contradiction => contradiction.Code == ResolverContradictionCode.ObservationReportContradiction);
        Assert.Contains("MESSAGE_MISMATCH", resolverContradiction.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("outside.schema.UnrelatedMessage", resolverContradiction.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleObservationDirectionsRemainAmbiguous()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", statuses: [DiagnosticStatus.Valid, DiagnosticStatus.Valid], directions: ["client", "server"]);
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable).Candidates);
        CandidateEvidence evidence = Assert.Single(candidate.Evidence, item => item.Source == "observation");

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Null(candidate.Direction);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.AmbiguousObservationDirection);
        Assert.Contains("client=1", evidence.Detail, StringComparison.Ordinal);
        Assert.Contains("server=1", evidence.Detail, StringComparison.Ordinal);
        Assert.Contains("VALID=2", evidence.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("client status", evidence.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnmatchedReportObservationSurfacesAsUnknown()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));

        CandidateReport resolved = Resolve(schema, LoadReport("lok"), ResolverTestData.ReadableFile());

        MappingCandidate candidate = Assert.Single(resolved.Candidates);
        Assert.Equal("lok", candidate.Token);
        Assert.Equal(CandidateClassification.Unknown, candidate.Classification);
        Assert.Contains(candidate.Evidence, evidence => evidence.Source == "observation" && evidence.Verified);
    }

    [Fact]
    public void JpeReportRemainsConflict()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("jpe", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger32));
        LoadedDiagnosticReport report = LoadReport("jpe");
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("MapInformationRequest", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus sns = new()
        {
            Source = "sns",
            Messages = [new ReferenceMessage { Token = "jpe", Name = "d3b.game.MapInformationRequest", Source = "sns" }],
        };

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable, [sns]).Candidates);

        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.FieldTypeMismatch);
    }

    [Fact]
    public void KjtReportRemainsAmbiguousCandidate()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("kjt", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("kjt");
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("FirstMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)),
            ResolverTestData.Message("SecondMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        PriorMapping pin = new() { ObfuscatedToken = "kjt", ReadableFullName = "d3b.game.FirstMessage", Build = ResolverTestData.ClientVersion, BuildExact = true };

        CandidateReport resolved = Resolve(schema, report, readable, pins: [pin]);

        Assert.Equal(2, resolved.Candidates.Count);
        Assert.All(resolved.Candidates, candidate =>
        {
            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.AmbiguousReadableShape);
        });
    }

    [Fact]
    public void CrossBuildReferenceRemainsUnverified()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok");
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = new()
        {
            Source = "fixture-corpus",
            Build = "3.7.12.99",
            BuildExact = true,
            Messages = [new ReferenceMessage { Token = "lok", Name = "d3b.game.StatusMessage", Source = "fixture-corpus", Build = "3.7.12.99", BuildExact = true }],
        };

        MappingCandidate candidate = Assert.Single(Resolve(schema, report, readable, [reference]).Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.BuildMismatchReference);
    }

    [Fact]
    public void ReportFromAnotherBuildIsRejectedBeforeResolution()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport wrongVersion = LoadReport("lok", report => report with { ClientVersion = "3.7.12.99" });
        LoadedDiagnosticReport wrongSchema = LoadReport("lok", report => report with { SchemaSha256 = new string('b', 64) });

        foreach (LoadedDiagnosticReport report in new[] { wrongVersion, wrongSchema })
        {
            DiagnosticReportResolutionException exception = Assert.Throws<DiagnosticReportResolutionException>(
                () => Resolve(schema, report, ResolverTestData.ReadableFile()));
            Assert.Equal("diagnostic report does not match the build schema", exception.Message);
        }
    }

    [Fact]
    public void FieldDeclarationMismatchIsAReportContradiction()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", value => value with
        {
            Types = value.Types.Select(type => type with
            {
                Contradictions = [new DiagnosticReportContradiction
                {
                    Code = DiagnosticReportContradictionCode.FieldDeclarationMismatch,
                    Detail = "FIELD_DECLARATION_MISMATCH field=1 count=1",
                    Occurrences = 1
                }]
            }).ToArray()
        });
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport resolved = Resolve(schema, report, readable);
        MappingCandidate candidate = Assert.Single(resolved.Candidates);

        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        ResolverContradiction contradiction = Assert.Single(candidate.Contradictions,
            item => item.Code == ResolverContradictionCode.ObservationReportContradiction);
        Assert.Contains("FIELD_DECLARATION_MISMATCH", contradiction.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.ObservationTruncated);
        Assert.Equal("complete", resolved.Provenance!.ObservationCoverage);
        Assert.False(resolved.Provenance.ReportTruncated);
    }

    [Fact]
    public void DetailTruncatedContradictionMakesCoveragePartial()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", value => value with
        {
            Coverage = "partial",
            Truncation = value.Truncation with { Reasons = ["detail-truncated"] },
            Types = value.Types.Select(type => type with
            {
                Contradictions = [new DiagnosticReportContradiction
                {
                    Code = DiagnosticReportContradictionCode.DetailTruncated,
                    Detail = "DETAIL_TRUNCATED count=1",
                    Occurrences = 1
                }]
            }).ToArray()
        });
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport resolved = Resolve(schema, report, readable);
        MappingCandidate candidate = Assert.Single(resolved.Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.ObservationTruncated);
        Assert.Equal("partial", resolved.Provenance!.ObservationCoverage);
        Assert.True(resolved.Provenance.ReportTruncated);
    }

    [Fact]
    public void ConflictingBuildExactPinPreventsAlternativeExactShapeFromVerifying()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok");
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("PinnedName", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.String)),
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        PriorMapping pin = new()
        {
            ObfuscatedToken = "lok",
            ReadableFullName = "d3b.game.PinnedName",
            Build = ResolverTestData.ClientVersion,
            BuildExact = true,
        };

        CandidateReport resolved = Resolve(schema, report, readable, pins: [pin]);
        MappingCandidate alternative = Assert.Single(resolved.Candidates, candidate => candidate.Readable == "d3b.game.StatusMessage");

        Assert.Equal(CandidateClassification.Candidate, alternative.Classification);
        Assert.Contains(alternative.Uncertainties,
            uncertainty => uncertainty.Code == ResolverUncertaintyCode.ConflictingPriorMapping);
    }

    [Fact]
    public void DirectResolverRejectsOverflowingCompleteFrameAccounting()
    {
        const long maximum = long.MaxValue;
        LoadedDiagnosticReport valid = LoadReport("lok");
        DiagnosticReportType type = WithOccurrenceCount(Assert.Single(valid.Report.Types), maximum);
        DiagnosticReportType otherType = type with { Token = "jot", Message = "jot" };
        LoadedDiagnosticReport overflowing = valid with
        {
            Report = valid.Report with
            {
                Session = valid.Report.Session with { EnqueuedFrames = maximum, AnalyzedFrames = maximum },
                Types = [type, otherType],
            },
        };
        DiagnosticSchema schema = new()
        {
            ClientVersion = ResolverTestData.ClientVersion,
            SchemaSha256 = ResolverTestData.SchemaHash,
            Messages =
            [
                new DiagnosticMessage { Name = "lok", Fields = [ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)] },
                new DiagnosticMessage { Name = "jot", Fields = [ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)] },
            ],
        };

        DiagnosticReportResolutionException exception = Assert.Throws<DiagnosticReportResolutionException>(
            () => Resolve(schema, overflowing, ResolverTestData.ReadableFile(
                ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)))));

        Assert.Equal("diagnostic report is invalid for the build schema", exception.Message);
    }

    [Fact]
    public void PublicResolverRevalidatesEveryIdentityAndSourceField()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport valid = LoadReport("lok");
        LoadedDiagnosticReport[] invalid =
        [
            valid with { Report = valid.Report with { ClientVersion = "/home/user" } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { Engine = "/home/user" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { Engine = "sns" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { EngineVersion = "user@host" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { EngineVersion = "1.2" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { EngineVersion = "1.2.3+192.0.2.1" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { EngineVersion = "1.2.3-192.0.2.1" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { EngineVersion = "1.2.3-a192.0.2.1b" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { EngineVersion = $"1.2.3-{new string('a', 123)}" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { SnsCommit = "user@host" } } },
            valid with { Report = valid.Report with { Engine = valid.Report.Engine with { SnsCommit = "synthetic" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { Producer = "/home/user" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { ProducerVersion = "user@host" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { ProducerVersion = "1.2.3+192.0.2.1" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { ProducerVersion = "1.2.3-192.0.2.1" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { ProducerVersion = "1.2.3-a192.0.2.1b" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { Producer = "192.0.2.1" } } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { ProducerVersion = "192.0.2.1" } } },
            valid with { Report = valid.Report with { ClientVersion = "3.7.preview" } },
            valid with { Report = valid.Report with { Provenance = valid.Report.Provenance with { Source = "local/path" } } },
        ];
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        foreach (LoadedDiagnosticReport report in invalid)
        {
            DiagnosticReportResolutionException exception = Assert.Throws<DiagnosticReportResolutionException>(
                () => Resolve(schema, report, readable));

            Assert.Equal("diagnostic report is invalid for the build schema", exception.Message);
            Assert.DoesNotContain("/home/user", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("user@host", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublicResolverRejectsUndefinedReportProblemCodes()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport valid = LoadReport("lok");
        DiagnosticReportType type = Assert.Single(valid.Report.Types);
        LoadedDiagnosticReport invalid = valid with
        {
            Report = valid.Report with
            {
                Types =
                [
                    type with
                    {
                        Problems = [new DiagnosticReportProblemCount { Code = (DiagnosticProblemCode)999, Occurrences = 1 }]
                    }
                ]
            }
        };
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        DiagnosticReportResolutionException exception = Assert.Throws<DiagnosticReportResolutionException>(
            () => Resolve(schema, invalid, readable));

        Assert.Equal("diagnostic report is invalid for the build schema", exception.Message);
        Assert.DoesNotContain("999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeEngineIdentityValuesAreAbsentFromReportResolutionProvenance()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", engine: new DiagnosticEngineHello
        {
            ProtocolVersion = 1,
            Engine = "d3b-diagnostic",
            EngineVersion = "/private/engine",
            SnsCommit = "user@host",
        });
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport resolved = Resolve(schema, report, readable);
        string json = CandidateReportJson.Serialize(resolved);

        Assert.Equal(string.Empty, report.Report.Engine.EngineVersion);
        Assert.Equal(string.Empty, report.Report.Engine.SnsCommit);
        Assert.Equal(string.Empty, resolved.Provenance!.EngineVersion);
        Assert.Equal(string.Empty, resolved.Provenance.SnsCommit);
        Assert.DoesNotContain("/private/engine", json, StringComparison.Ordinal);
        Assert.DoesNotContain("user@host", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownTokenInReportIsRejected()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("unknown");

        DiagnosticReportResolutionException exception = Assert.Throws<DiagnosticReportResolutionException>(
            () => Resolve(schema, report, ResolverTestData.ReadableFile()));

        Assert.Equal("diagnostic report is invalid for the build schema", exception.Message);
        Assert.DoesNotContain("unknown", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoPromotionOccursFromReportResolution()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok", droppedFrames: 1);
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        CandidateReport resolved = Resolve(schema, report, readable);
        MappingCandidate candidate = Assert.Single(resolved.Candidates);
        PromotionDecision decision = PromotionGate.Evaluate(candidate, resolved, new PromotionContext());
        string mappingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mapping.json");

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.False(decision.Allowed);
        Assert.Contains("NOT_VERIFIED", decision.RefusalCodes);
        Assert.False(File.Exists(mappingPath));
    }

    [Fact]
    public void ResolutionProvenanceIsDeterministicAndReportShaIsExact()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        LoadedDiagnosticReport report = LoadReport("lok");
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport first = Resolve(schema, report, readable);
        CandidateReport second = Resolve(schema, report, readable);

        Assert.Equal(report.Sha256, first.Provenance!.ReportSha256);
        Assert.Equal(CandidateReportJson.Serialize(first), CandidateReportJson.Serialize(second));
        Assert.Equal("d3b-diagnostic", first.Provenance.Engine);
        Assert.Equal("1.0.0", first.Provenance.EngineVersion);
        Assert.Equal("abcdef0", first.Provenance.SnsCommit);
        Assert.Equal("live", first.Provenance.ObservationSource);
    }

    [Fact]
    public void DirectResolverBehaviorIsUnchanged()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        DiagnosticAnalysis[] analyses = [ResolverTestData.Analysis("lok", "server")];
        ProtocolResolver resolver = ResolverTestData.Resolver(ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64))));

        CandidateReport original = resolver.Resolve(schema, analyses, "server");
        CandidateReport withNullObservations = resolver.Resolve(schema, analyses, "server", null, null);

        Assert.Equal(CandidateReportJson.Serialize(original), CandidateReportJson.Serialize(withNullObservations));
    }

    private static CandidateReport Resolve(
        DiagnosticSchema schema,
        LoadedDiagnosticReport report,
        FileDescriptor readable,
        IEnumerable<ReferenceCorpus>? references = null,
        IEnumerable<PriorMapping>? pins = null)
    {
        return DiagnosticReportResolver.Resolve(schema, report, references ?? [], pins ?? [], [readable]);
    }

    private static LoadedDiagnosticReport LoadReport(
        string token,
        Func<DiagnosticReport, DiagnosticReport>? transform = null,
        IReadOnlyList<DiagnosticStatus>? statuses = null,
        IReadOnlyList<DiagnosticProblemCode>? problems = null,
        bool typeUrlOk = true,
        long droppedFrames = 0,
        IReadOnlyList<string>? directions = null,
        DiagnosticEngineHello? engine = null)
    {
        DiagnosticField[] fields = [ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)];
        DiagnosticSchema schema = ResolverTestData.Schema(token, fields);
        DiagnosticReportAggregator aggregator = new(schema, new DiagnosticReportOptions(), new DiagnosticReportBuildContext
        {
            ClientVersion = ResolverTestData.ClientVersion,
            SchemaSha256 = ResolverTestData.SchemaHash,
            Engine = engine ?? new DiagnosticEngineHello { ProtocolVersion = 1, Engine = "d3b-diagnostic", EngineVersion = "1.0.0", SnsCommit = "abcdef0" },
            ProducerVersion = "1.0.0",
        });
        DiagnosticStatus[] statusValues = (statuses ?? [DiagnosticStatus.Valid]).ToArray();
        string[] directionValues = (directions ?? Enumerable.Repeat("server", statusValues.Length).ToArray()).ToArray();
        if (directionValues.Length != statusValues.Length)
        {
            throw new ArgumentException("One direction is required for each status.", nameof(directions));
        }

        for (int index = 0; index < statusValues.Length; index++)
        {
            aggregator.Observe(new DiagnosticAnalysis
            {
                CarriedType = token,
                Message = token,
                Direction = directionValues[index],
                Status = statusValues[index],
                TypeUrlOk = typeUrlOk,
                Problems = (problems ?? []).Select(problem => new DiagnosticProblem { Code = problem, FieldPath = "1" }).ToArray(),
            });
        }

        DiagnosticReport report = aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = droppedFrames > 0 ? "partial" : "complete",
            EnqueuedFrames = statusValues.Length,
            AnalyzedFrames = statusValues.Length,
            DroppedFrames = droppedFrames,
            CompletedAtUtc = DateTimeOffset.Parse("2026-01-01T00:01:00Z", System.Globalization.CultureInfo.InvariantCulture),
        });
        if (transform is not null)
        {
            report = transform(report);
        }

        byte[] bytes = DiagnosticReportJson.SerializeToUtf8Bytes(report, new DiagnosticReportOptions());
        return DiagnosticReportJson.Load(bytes, new DiagnosticReportOptions());
    }

    private static DiagnosticReportType WithOccurrenceCount(DiagnosticReportType type, long count)
    {
        return type with
        {
            Occurrences = count,
            Directions = type.Directions.Select(direction => direction with { Occurrences = count }).ToArray(),
            Statuses = type.Statuses.Select(status => status with { Occurrences = count }).ToArray(),
            TypeUrlOkOccurrences = count,
            TypeUrlMismatchOccurrences = 0,
            Variants = type.Variants.Select(variant => variant with
            {
                Occurrences = count,
                Statuses = variant.Statuses.Select(status => status with { Occurrences = count }).ToArray(),
            }).ToArray(),
        };
    }
}
