using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Resolution;
using D3B.Protocol.Mapping;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Tests.Resolution;

public sealed class ProtocolResolverTests
{
    [Fact]
    public void JpeTypeMismatchConflictsDespiteHighReferenceScore()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("MapInformationRequest",
                ResolverTestData.ReadableField("map_id", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = BillBotMappingLoader.LoadJson(
            """
            {
              ".com.ankama.dofus.server.game.protocol.gamemap.MapInformationRequest": {
                "full_non_obf_msg_namespace": ".com.ankama.dofus.server.game.protocol.gamemap.MapInformationRequest",
                "full_obf_msg_namespace": "jpe",
                "field_mapping": { "unrelated": { "name": "JunkRequest", "token": "junk" } }
              }
            }
            """);

        CandidateReport report = ResolverTestData.Resolver(readable, [reference])
            .Resolve(ResolverTestData.Schema("jpe", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger32)),
                [ResolverTestData.Analysis("jpe", "client")], "client");

        MappingCandidate candidate = report.Candidates.Single();
        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.FieldTypeMismatch);
        Assert.Equal("heuristic", candidate.Score.Kind);
        Assert.Equal(1, candidate.Score.Value);
    }

    [Fact]
    public void KjtIdenticalReadableShapesRemainAmbiguousWithPinAndReference()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("MapInformationRequest",
                ResolverTestData.ReadableField("map_id", 1, FieldDescriptorProto.Types.Type.Int64)),
            ResolverTestData.Message("ContextReadyRequest",
                ResolverTestData.ReadableField("context_id", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = BillBotMappingLoader.LoadJson(
            """
            {
              ".com.ankama.dofus.server.game.protocol.context.ContextReadyRequest": {
                "full_obf_msg_namespace": "com.ankama.dofus.server.game.protocol.context.kjt"
              }
            }
            """);
        PriorMapping pin = new()
        {
            ReadableFullName = "d3b.game.MapInformationRequest",
            ObfuscatedToken = "kjt",
            Build = ResolverTestData.ClientVersion,
            BuildExact = true
        };

        CandidateReport report = ResolverTestData.Resolver(readable, [reference], [pin])
            .Resolve(ResolverTestData.Schema("kjt", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("kjt", "client")], "client");

        Assert.Equal(2, report.Candidates.Count);
        Assert.All(report.Candidates, candidate => Assert.Equal(CandidateClassification.Candidate, candidate.Classification));
        Assert.All(report.Candidates, candidate => Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.AmbiguousReadableShape));
        Assert.Contains(report.Candidates, candidate => candidate.Readable == "d3b.game.ContextReadyRequest");
        Assert.Contains(report.Candidates.SelectMany(candidate => candidate.Evidence), evidence =>
            evidence.Source == "reference" && evidence.Provenance == "billbot:game_mappings_detailed.json");
        Assert.Contains(report.Candidates.SelectMany(candidate => candidate.Evidence), evidence => evidence.Source == "prior_mapping");
    }

    [Fact]
    public void ExactObservedShapeAndShapeOnlyPinRemainsCandidateWithoutSemanticProof()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        PriorMapping pin = new()
        {
            ReadableFullName = "d3b.game.StatusEvent",
            ObfuscatedToken = "lok",
            Build = ResolverTestData.ClientVersion,
            BuildExact = true
        };

        CandidateReport report = ResolverTestData.Resolver(readable, pins: [pin])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server");

        MappingCandidate candidate = report.Candidates.Single();
        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Null(candidate.FixtureProposal);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
        Assert.Contains(candidate.Evidence, evidence => evidence.Source == "prior_mapping" && evidence.Verified);
    }

    [Theory]
    [InlineData("IL2CPP")]
    [InlineData("BEHAVIORAL")]
    [InlineData("AUDITED_PIN")]
    [InlineData("AUDITED_REFERENCE")]
    public void ValidMatchingSemanticProofVerifiesExactObservedShape(string kind)
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = CorpusWithProof(kind);

        CandidateReport report = ResolverTestData.Resolver(readable, [reference])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server");

        MappingCandidate candidate = Assert.Single(report.Candidates);
        Assert.Equal(CandidateClassification.Verified, candidate.Classification);
        Assert.NotNull(candidate.FixtureProposal);
        Assert.Equal("d3b.game.StatusEvent", GetCandidateProperty(candidate, "SemanticIdentity"));
        object? proof = GetCandidateProperty(candidate, "Proof");
        Assert.NotNull(proof);
        Assert.Equal(kind, GetProofKind(proof!));
    }

    [Theory]
    [InlineData("kind", "UNKNOWN")]
    [InlineData("kind", "")]
    [InlineData("source", "")]
    [InlineData("artifactSha256", "not-a-digest")]
    [InlineData("token", "other")]
    [InlineData("readable", "d3b.game.OtherEvent")]
    [InlineData("build", "3.7.12.99")]
    [InlineData("schemaSha256", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void InvalidSemanticProofFailsClosed(string property, string value)
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = CorpusWithProof("IL2CPP", property, value);

        MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, [reference])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server")
            .Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Null(candidate.FixtureProposal);
        Assert.Null(GetCandidateProperty(candidate, "SemanticIdentity"));
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofInvalid");
    }

    [Fact]
    public void OneInvalidProofCannotBeOverriddenByAnotherMatchingProof()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus valid = CorpusWithProof("AUDITED_REFERENCE");
        ReferenceCorpus invalid = CorpusWithProof("IL2CPP", "readable", "d3b.game.OtherEvent");

        MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, [valid, invalid])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server")
            .Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofInvalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameSourceCorpusProofsFailClosedRegardlessOfOrder(bool invalidFirst)
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        const string token = "lok";
        const string readableName = "d3b.game.StatusEvent";
        const string source = "same-source";
        var validProof = new
        {
            token,
            readable = readableName,
            build = ResolverTestData.ClientVersion,
            schemaSha256 = ResolverTestData.SchemaHash,
            kind = "AUDITED_REFERENCE",
            source = "resolver-test",
            artifactSha256 = new string('b', 64)
        };
        var invalidProof = new
        {
            token,
            readable = "d3b.game.OtherEvent",
            build = ResolverTestData.ClientVersion,
            schemaSha256 = ResolverTestData.SchemaHash,
            kind = "AUDITED_REFERENCE",
            source = "resolver-test",
            artifactSha256 = new string('b', 64)
        };
        object[] entries = invalidFirst
            ? [
                new { token, name = readableName, source, semanticProof = invalidProof },
                new { token, name = readableName, source, semanticProof = validProof }
            ]
            : [
                new { token, name = readableName, source, semanticProof = validProof },
                new { token, name = readableName, source, semanticProof = invalidProof }
            ];
        ReferenceCorpus reference = ReferenceCorpusJsonLoader.Load(System.Text.Json.JsonSerializer.Serialize(new
        {
            source,
            build = ResolverTestData.ClientVersion,
            buildExact = true,
            messages = entries
        }));

        MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, [reference])
            .Resolve(ResolverTestData.Schema(token, ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis(token, "server")], "server")
            .Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.SemanticProofInvalid);
        Assert.Null(candidate.FixtureProposal);
    }

    [Fact]
    public void PinSemanticProofFieldsAreConvertedToCandidateProof()
    {
        using TemporaryDirectory directory = new();
        string path = Path.Combine(directory.Path, "pins.json");
        File.WriteAllText(path, $$"""
            {
              "clientVersion": "{{ResolverTestData.ClientVersion}}",
              "messages": [{
                "readable": "d3b.game.StatusEvent",
                "obfuscated": "lok",
                "semanticProofKind": "AUDITED_PIN",
                "semanticProofSource": "resolver-test",
                "semanticProofSchemaSha256": "{{ResolverTestData.SchemaHash}}",
                "semanticProofArtifactSha256": "{{new string('b', 64)}}"
              }]
            }
            """);
        PinnedPairs pins = PinnedPairsFile.Read(path, ResolverTestData.ClientVersion);
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, pins: PriorMapping.From(pins))
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server")
            .Candidates);

        Assert.Equal(CandidateClassification.Verified, candidate.Classification);
        Assert.NotNull(candidate.FixtureProposal);
        object? proof = GetCandidateProperty(candidate, "Proof");
        Assert.NotNull(proof);
        Assert.Equal("AUDITED_PIN", GetProofKind(proof!));
    }

    [Fact]
    public void CandidateJsonIncludesSemanticFlagsAndProofWithoutObservationDetails()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        DiagnosticAnalysis analysis = ResolverTestData.Analysis("lok", "server") with
        {
            Problems = [new DiagnosticProblem { Code = DiagnosticProblemCode.UnknownType, Detail = "PRIVATE_PAYLOAD_SENTINEL" }]
        };
        CandidateReport report = ResolverTestData.Resolver(readable, [CorpusWithProof("BEHAVIORAL")])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)), [analysis], "server");
        string json = CandidateReportJson.Serialize(report);
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement root = document.RootElement;
        System.Text.Json.JsonElement candidate = root.GetProperty("candidates")[0];

        Assert.Equal(2, root.GetProperty("protocolVersion").GetInt32());
        Assert.True(candidate.GetProperty("exactShape").GetBoolean());
        Assert.True(candidate.GetProperty("uniqueShape").GetBoolean());
        Assert.False(candidate.GetProperty("hasValidObservation").GetBoolean());
        Assert.Equal("d3b.game.StatusEvent", candidate.GetProperty("semanticIdentity").GetString());
        Assert.Equal("BEHAVIORAL", candidate.GetProperty("proof").GetProperty("kind").GetString());
        Assert.DoesNotContain("PRIVATE_PAYLOAD_SENTINEL", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseShapeAmbiguityCoversTheObservedPlacementCellsAliases()
    {
        CandidateReport report = ResolveAliases(["hgf", "imf"], "PlacementCells");

        Assert.Equal(2, report.Candidates.Count);
        Assert.All(report.Candidates, candidate =>
        {
            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.True(candidate.ExactShape);
            Assert.False(candidate.UniqueShape);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "AmbiguousSchemaToken");
        });
    }

    [Fact]
    public void ReverseShapeAmbiguityCoversTheObservedThreeTokenFightTurnAliases()
    {
        CandidateReport report = ResolveAliases(["kkn", "klu", "kmj"], "FightTurnFinishRequest");

        Assert.Equal(3, report.Candidates.Count);
        Assert.All(report.Candidates, candidate =>
        {
            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.False(candidate.UniqueShape);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "AmbiguousSchemaToken");
        });
    }

    [Fact]
    public void UnobservedExactSchemaCompetitorBlocksObservedCandidateUniqueness()
    {
        CandidateReport report = ResolveAliases(["observed", "unobserved"], "StatusEvent");
        MappingCandidate observed = Assert.Single(report.Candidates, candidate => candidate.Token == "observed");

        Assert.Equal(CandidateClassification.Candidate, observed.Classification);
        Assert.False(observed.UniqueShape);
        Assert.Contains(observed.Uncertainties, uncertainty => uncertainty.Code.ToString() == "AmbiguousSchemaToken");
    }

    [Fact]
    public void ShapeOnlyBillBotAndSnsHintsNeverSupplySemanticProof()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        DiagnosticSchema schema = ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64));
        ReferenceCorpus[] references =
        [
            BillBotMappingLoader.LoadJson("{\"d3b.game.StatusEvent\":\"lok\"}"),
            SnsMappingLoader.LoadJson("{\"lok\":\"d3b.game.StatusEvent\"}")
        ];

        foreach (ReferenceCorpus reference in references)
        {
            MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, [reference])
                .Resolve(schema, [ResolverTestData.Analysis("lok", "server")], "server")
                .Candidates);
            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
            Assert.Null(GetCandidateProperty(candidate, "Proof"));
        }
    }

    [Fact]
    public void NormalizedReferenceWithoutSemanticProofRemainsCandidate()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = ReferenceCorpusJsonLoader.Load("""
            {
              "source": "normalized-test",
              "build": "3.7.13.17",
              "buildExact": true,
              "messages": [{ "token": "lok", "name": "d3b.game.StatusEvent" }]
            }
            """);

        MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, [reference])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server")
            .Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
        Assert.Null(GetCandidateProperty(candidate, "Proof"));
    }

    private static CandidateReport ResolveAliases(string[] tokens, string readableName)
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message(readableName,
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        DiagnosticField[] fields = [ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)];
        DiagnosticSchema schema = ResolverTestData.Schema(tokens[0], fields) with
        {
            Messages = tokens.Select(token => new DiagnosticMessage { Name = token, Fields = fields }).ToArray()
        };

        return ResolverTestData.Resolver(readable)
            .Resolve(schema, [ResolverTestData.Analysis(tokens[0], readableName.EndsWith("Request", StringComparison.Ordinal) ? "client" : "server")],
                readableName.EndsWith("Request", StringComparison.Ordinal) ? "client" : "server");
    }

    private static ReferenceCorpus CorpusWithProof(
        string kind,
        string? changedProperty = null,
        string? changedValue = null,
        string token = "lok",
        string readable = "d3b.game.StatusEvent")
    {
        Dictionary<string, object?> proof = new(StringComparer.Ordinal)
        {
            ["kind"] = kind,
            ["token"] = token,
            ["readable"] = readable,
            ["build"] = ResolverTestData.ClientVersion,
            ["schemaSha256"] = ResolverTestData.SchemaHash,
            ["source"] = "resolver-test-" + kind + "-" + (changedProperty ?? "matching"),
            ["artifactSha256"] = new string('b', 64)
        };
        if (changedProperty is not null)
        {
            proof[changedProperty] = changedValue;
        }

        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            source = "resolver-proof-test-" + kind + "-" + (changedProperty ?? "matching"),
            build = ResolverTestData.ClientVersion,
            buildExact = true,
            messages = new[]
            {
                new { token, name = readable, semanticProof = proof }
            }
        });
        return ReferenceCorpusJsonLoader.Load(json);
    }

    private static object? GetCandidateProperty(MappingCandidate candidate, string name)
    {
        return typeof(MappingCandidate).GetProperty(name)?.GetValue(candidate);
    }

    private static string GetProofKind(object proof)
    {
        string? kind = proof.GetType().GetProperty("Kind")?.GetValue(proof)?.ToString();
        return kind switch
        {
            "Il2Cpp" => "IL2CPP",
            "Behavioral" => "BEHAVIORAL",
            "AuditedPin" => "AUDITED_PIN",
            "AuditedReference" => "AUDITED_REFERENCE",
            _ => string.Empty
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d3b-p2m-resolver-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    [Fact]
    public void ConflictingBuildExactPinKeepsAlternativeExactShapeAsCandidate()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("PinnedName",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.String)),
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        PriorMapping pin = new()
        {
            ReadableFullName = "d3b.game.PinnedName",
            ObfuscatedToken = "lok",
            Build = ResolverTestData.ClientVersion,
            BuildExact = true
        };

        CandidateReport report = ResolverTestData.Resolver(readable, pins: [pin])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server");
        MappingCandidate alternative = Assert.Single(report.Candidates, candidate => candidate.Readable == "d3b.game.StatusEvent");

        Assert.Equal(CandidateClassification.Candidate, alternative.Classification);
        Assert.Contains(alternative.Uncertainties,
            uncertainty => uncertainty.Code == ResolverUncertaintyCode.ConflictingPriorMapping);
    }

    [Fact]
    public void ExactShapeWithoutValidObservationIsOnlyCandidate()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)), [], "server");

        Assert.Equal(CandidateClassification.Candidate, report.Candidates.Single().Classification);
    }

    [Fact]
    public void PartialReadableCanNeverBeVerified()
    {
        FileDescriptor readable = ResolverTestData.PartialReadableFile("StatusEvent", 1, FieldDescriptorProto.Types.Type.Int64);

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server");

        MappingCandidate candidate = report.Candidates.Single();
        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialReadable);
    }

    [Fact]
    public void ReferenceFromAnotherBuildCannotVerifyByName()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus reference = new()
        {
            Source = "sns",
            Build = "other/unknown",
            Messages = [new ReferenceMessage { Token = "lok", Name = "StatusEvent", Source = "sns", Build = "other/unknown" }]
        };

        CandidateReport report = ResolverTestData.Resolver(readable, [reference])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)), [], "server");

        MappingCandidate candidate = report.Candidates.Single();
        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.BuildMismatchReference);
    }

    [Fact]
    public void ConflictingReferencesAreHardContradiction()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("AlphaRequest", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)),
            ResolverTestData.Message("BetaRequest", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus first = new() { Source = "one", Messages = [new ReferenceMessage { Token = "kjt", Name = "AlphaRequest", Source = "one" }] };
        ReferenceCorpus second = new() { Source = "two", Messages = [new ReferenceMessage { Token = "kjt", Name = "BetaRequest", Source = "two" }] };

        CandidateReport report = ResolverTestData.Resolver(readable, [first, second])
            .Resolve(ResolverTestData.Schema("kjt", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)), [], "client");

        Assert.All(report.Candidates, candidate =>
        {
            Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
            Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.ConflictingReferences);
        });
    }

    [Fact]
    public void NoReadableCandidateProducesUnknown()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("UnrelatedEvent", ResolverTestData.ReadableField("value", 9, FieldDescriptorProto.Types.Type.String)));

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(ResolverTestData.Schema("new_token", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger32)),
                [ResolverTestData.Analysis("new_token", "server")], "server");

        Assert.Equal(CandidateClassification.Unknown, report.Candidates.Single().Classification);
    }

    [Fact]
    public void WrongDirectionIsAConflict()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("MapInformationRequest",
                ResolverTestData.ReadableField("map_id", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(ResolverTestData.Schema("jpe", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("jpe", "server")], "server");

        MappingCandidate candidate = report.Candidates.Single();
        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.DirectionContradiction);
    }

    [Fact]
    public void RotatedExternalReferenceRemainsUnverified()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent",
                ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        ReferenceCorpus oldBuild = new()
        {
            Source = "old-build",
            Build = "3.7.12.12",
            BuildExact = true,
            Messages = [new ReferenceMessage { Token = "lok", Name = "StatusEvent", Source = "old-build", Build = "3.7.12.12", BuildExact = true }]
        };

        CandidateReport report = ResolverTestData.Resolver(readable, [oldBuild])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server");

        Assert.Equal(CandidateClassification.Candidate, report.Candidates.Single().Classification);
        Assert.Contains(report.Candidates.Single().Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.BuildMismatchReference);
    }

    [Fact]
    public void NestedMessageShapeIsComparedRecursively()
    {
        DescriptorProto inner = ResolverTestData.Message(
            "Inner",
            ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int32));
        DescriptorProto outer = ResolverTestData.Message(
            "EnvelopeEvent",
            ResolverTestData.ReadableField("inner", 1, FieldDescriptorProto.Types.Type.Message, messageType: ".d3b.game.Inner"));
        FileDescriptor readable = ResolverTestData.ReadableFile(inner, outer);
        DiagnosticSchema schema = ResolverTestData.Schema("envelope",
            ResolverTestData.Field(1, DiagnosticFieldKind.Message, messageType: "inner")) with
        {
            Messages =
            [
                new DiagnosticMessage
                {
                    Name = "inner",
                    Fields = [ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger32)]
                },
                new DiagnosticMessage
                {
                    Name = "envelope",
                    Fields = [ResolverTestData.Field(1, DiagnosticFieldKind.Message, messageType: "inner")]
                }
            ]
        };

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(schema, [ResolverTestData.Analysis("envelope", "server")], "server");

        MappingCandidate candidate = report.Candidates.Single(item => item.Token == "envelope" && item.Readable == "d3b.game.EnvelopeEvent");
        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
    }

    [Fact]
    public void NestedKindMismatchIsAConflict()
    {
        DescriptorProto inner = ResolverTestData.Message(
            "Inner",
            ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int32));
        DescriptorProto outer = ResolverTestData.Message(
            "EnvelopeEvent",
            ResolverTestData.ReadableField("inner", 1, FieldDescriptorProto.Types.Type.Message, messageType: ".d3b.game.Inner"));
        FileDescriptor readable = ResolverTestData.ReadableFile(inner, outer);
        DiagnosticSchema schema = ResolverTestData.Schema("envelope",
            ResolverTestData.Field(1, DiagnosticFieldKind.Message, messageType: "inner")) with
        {
            Messages =
            [
                new DiagnosticMessage
                {
                    Name = "inner",
                    Fields = [ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)]
                },
                new DiagnosticMessage
                {
                    Name = "envelope",
                    Fields = [ResolverTestData.Field(1, DiagnosticFieldKind.Message, messageType: "inner")]
                }
            ]
        };

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(schema, [ResolverTestData.Analysis("envelope", "server")], "server");

        MappingCandidate candidate = report.Candidates.Single(item => item.Token == "envelope" && item.Readable == "d3b.game.EnvelopeEvent");
        Assert.Equal(CandidateClassification.Conflict, candidate.Classification);
        Assert.Contains(candidate.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.NestedShapeMismatch);
    }

    [Fact]
    public void MapEntryMessagesAreExcludedFromCandidates()
    {
        DiagnosticSchema schema = ResolverTestData.Schema("map_entry") with
        {
            Messages = [new DiagnosticMessage { Name = "map_entry", MapEntry = true }]
        };
        FileDescriptor readable = ResolverTestData.ReadableFile();

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(schema, [], null);

        Assert.Empty(report.Candidates);
    }

    [Fact]
    public void SyntheticFixtureUsesOneRepeatedEmptyFieldAndFixedWidthZeros()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("FixtureEvent",
                ResolverTestData.ReadableField("labels", 1, FieldDescriptorProto.Types.Type.String, repeated: true),
                ResolverTestData.ReadableField("fixed", 2, FieldDescriptorProto.Types.Type.Fixed32)));

        CandidateReport report = ResolverTestData.Resolver(readable, [CorpusWithProof("BEHAVIORAL", token: "fixture", readable: "d3b.game.FixtureEvent")])
            .Resolve(ResolverTestData.Schema("fixture",
                ResolverTestData.Field(1, DiagnosticFieldKind.Utf8String, repeated: true),
                ResolverTestData.Field(2, DiagnosticFieldKind.Fixed32)),
                [ResolverTestData.Analysis("fixture", "server")], "server");

        Assert.Equal("0a001500000000", report.Candidates.Single().FixtureProposal!.PayloadHex);
    }

    [Fact]
    public void PartialObservationCannotVerify()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        DiagnosticSummary summary = new() { ClientVersion = ResolverTestData.ClientVersion, SchemaSha256 = ResolverTestData.SchemaHash, Frames = 1, Dropped = 1, Coverage = "partial" };

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server")], "server", summary);

        Assert.Equal(CandidateClassification.Candidate, report.Candidates.Single().Classification);
        Assert.Contains(report.Candidates.Single().Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialObservation);
    }

    [Fact]
    public void ValidStatusWithInvalidTypeUrlCannotVerify()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));

        CandidateReport report = ResolverTestData.Resolver(readable)
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)),
                [ResolverTestData.Analysis("lok", "server") with { TypeUrlOk = false }], "server");

        MappingCandidate candidate = report.Candidates.Single();
        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.False(candidate.HasValidObservation);
        Assert.DoesNotContain(candidate.Evidence, evidence => evidence.Source == "observation" && evidence.Verified);
    }

    [Fact]
    public void OneInvalidAnalysisPreventsACompleteValidObservation()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        DiagnosticAnalysis[] analyses =
        [
            ResolverTestData.Analysis("lok", "server"),
            ResolverTestData.Analysis("lok", "server") with { Index = 1, TypeUrlOk = false }
        ];

        MappingCandidate candidate = Assert.Single(ResolverTestData.Resolver(readable, [CorpusWithProof("BEHAVIORAL")])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)), analyses, "server")
            .Candidates);

        Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
        Assert.False(candidate.HasValidObservation);
        Assert.Null(candidate.FixtureProposal);
    }

    [Fact]
    public void CandidateReportIsDeterministicAndContainsOnlySyntheticFixtureHex()
    {
        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("StatusEvent", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)));
        DiagnosticAnalysis analysis = ResolverTestData.Analysis("lok", "server");
        CandidateReport report = ResolverTestData.Resolver(readable, [CorpusWithProof("BEHAVIORAL")])
            .Resolve(ResolverTestData.Schema("lok", ResolverTestData.Field(1, DiagnosticFieldKind.SignedInteger64)), [analysis], "server");

        byte[] first = CandidateReportJson.SerializeToUtf8Bytes(report);
        byte[] second = CandidateReportJson.SerializeToUtf8Bytes(report);
        string json = System.Text.Encoding.UTF8.GetString(first);

        Assert.Equal(first, second);
        Assert.DoesNotContain("numericValue", json, StringComparison.Ordinal);
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement fixture = document.RootElement.GetProperty("candidates")[0].GetProperty("fixture");
        Assert.True(fixture.GetProperty("synthetic").GetBoolean());
        Assert.False(fixture.TryGetProperty("payloadHex", out _));
        Assert.DoesNotContain("0800", json, StringComparison.Ordinal);

        MappingCandidate nonSynthetic = report.Candidates.Single() with
        {
            FixtureProposal = report.Candidates.Single().FixtureProposal! with { Synthetic = false, PayloadHex = "privatehex" }
        };
        string nonSyntheticJson = CandidateReportJson.Serialize(report with { Candidates = [nonSynthetic] });
        Assert.DoesNotContain("privatehex", nonSyntheticJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceLoadersPreserveNamesAsNonExactHints()
    {
        ReferenceCorpus sns = SnsMappingLoader.LoadJson("{\"jpe\":\"com.ankama.game.MapInformationRequest\"}");
        ReferenceCorpus billBot = BillBotMappingLoader.LoadJson("{\"d3b.game.MapInformationRequest\":\"jpe\"}");
        ReferenceCorpus normalized = ReferenceCorpusJsonLoader.Load("{\"source\":\"fixture\",\"build\":\"3.7.13.17\",\"buildExact\":true,\"messages\":[{\"token\":\"lok\",\"name\":\"d3b.game.StatusEvent\",\"fields\":[{\"number\":1,\"kind\":\"INT64\",\"repeated\":false}]}]}");

        Assert.False(sns.Messages.Single().BuildExact);
        Assert.Equal("other/unknown", sns.Messages.Single().Build);
        Assert.Equal("MapInformationRequest", sns.Messages.Single().Name.Split('.').Last());
        Assert.Equal("jpe", billBot.Messages.Single().Token);
        Assert.False(billBot.Messages.Single().BuildExact);
        Assert.True(normalized.Messages.Single().BuildExact);
        Assert.Equal(DiagnosticFieldKind.SignedInteger64, normalized.Messages.Single().Fields.Single().Kind);
    }

    [Fact]
    public void BillBotLoaderParsesRealNamespaceMappingEntries()
    {
        ReferenceCorpus corpus = BillBotMappingLoader.LoadJson(
            """
            {
              ".com.ankama.dofus.server.game.protocol.gamemap.MapInformationRequest": {
                "full_non_obf_msg_namespace": ".com.ankama.dofus.server.game.protocol.gamemap.MapInformationRequest",
                "full_obf_msg_namespace": "com.ankama.dofus.server.game.protocol.jpe",
                "obf_msg_namespace": "jpe",
                "field_mapping": { "1": { "name": "JunkRequest", "token": "junk" } }
              },
              ".com.ankama.dofus.server.game.protocol.context.ContextReadyRequest": {
                "obf_msg_namespace": "com.ankama.dofus.server.game.protocol.kjt"
              }
            }
            """);

        Assert.Equal(2, corpus.Messages.Count);
        Assert.Equal("jpe", corpus.Messages[0].Token);
        Assert.Equal(".com.ankama.dofus.server.game.protocol.gamemap.MapInformationRequest", corpus.Messages[0].Name);
        Assert.Equal("kjt", corpus.Messages[1].Token);
        Assert.Equal(".com.ankama.dofus.server.game.protocol.context.ContextReadyRequest", corpus.Messages[1].Name);
        Assert.All(corpus.Messages, message =>
        {
            Assert.Equal("billbot:game_mappings_detailed.json", message.Source);
            Assert.Equal("other/unknown", message.Build);
            Assert.False(message.BuildExact);
            Assert.Empty(message.Fields);
        });
    }
}
