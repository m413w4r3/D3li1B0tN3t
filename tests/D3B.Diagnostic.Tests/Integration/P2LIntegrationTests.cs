using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;
using D3B.Diagnostic.Reporting;
using D3B.Diagnostic.Resolution;
using D3B.Diagnostic.Schema;
using D3B.Diagnostic.Tests.Replay;
using D3B.Diagnostic.Tests.Resolution;
using D3B.Diagnostic.Tests.Schema;
using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Tests.Integration;

public sealed class P2LIntegrationTests
{
    private const string PayloadCanary = "PAYLOAD_CANARY_P2L_9D48";
    private static readonly DateTimeOffset _startedAtUtc = DateTimeOffset.Parse("2026-11-05T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset _completedAtUtc = DateTimeOffset.Parse("2026-11-05T10:00:05Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task DeterministicSyntheticChainWritesLoadsAndResolvesRedactedEvidence()
    {
        DiagnosticSchema schema = CreateChainSchema();
        DiagnosticReportOptions options = new();
        DiagnosticAnalysis[] analyses = CreateChainAnalyses(schema);
        DiagnosticFrame[] frames = analyses.Select(CreateFrame).ToArray();
        Assert.Equal(analyses.Length, frames.Length);
        Assert.All(analyses.Zip(frames), pair =>
        {
            Assert.Equal(pair.First.Index, pair.Second.Index);
            Assert.Equal(pair.First.Direction, pair.Second.Direction);
            Assert.Equal(pair.First.CarriedType, pair.Second.CarriedType);
        });

        DiagnosticReportAggregator aggregator = new(schema, options, CreateBuildContext(schema));
        foreach (DiagnosticAnalysis analysis in analyses)
        {
            aggregator.Observe(analysis);
        }

        DiagnosticReport report = MarkSynthetic(aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = "complete",
            EnqueuedFrames = frames.Length,
            AnalyzedFrames = analyses.Length,
            CompletedAtUtc = _completedAtUtc,
        }));
        byte[] expectedJson = DiagnosticReportJson.SerializeToUtf8Bytes(report, options);
        string jsonText = Encoding.UTF8.GetString(expectedJson);

        Assert.Equal("complete", report.Coverage);
        Assert.Equal("synthetic", report.Provenance.Source);
        Assert.True(expectedJson.Length <= options.MaxBytes);
        Assert.DoesNotContain(PayloadCanary, jsonText, StringComparison.Ordinal);
        Assert.Equal(2, report.Types.Single(type => type.Token == "fixture.Alpha").Occurrences);
        Assert.Equal("server", Assert.Single(report.Types.Single(type => type.Token == "fixture.Alpha").Directions).Direction);
        Assert.Contains("client", report.Types.SelectMany(type => type.Directions).Select(direction => direction.Direction));
        Assert.Contains("d3b", report.Types.SelectMany(type => type.Directions).Select(direction => direction.Direction));

        using TemporaryDirectory temporary = new();
        AtomicDiagnosticReportWriter writer = new(options, temporary.Path, TimeProvider.System);
        await writer.WriteAsync(report, TestContext.Current.CancellationToken);

        string reportPath = Assert.Single(Directory.GetFiles(temporary.Path));
        Assert.StartsWith("diagnostic-report-", Path.GetFileName(reportPath), StringComparison.Ordinal);
        Assert.Equal(".json", Path.GetExtension(reportPath));
        Assert.Equal(expectedJson, File.ReadAllBytes(reportPath));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(temporary.Path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(reportPath));
        }

        LoadedDiagnosticReport loaded = DiagnosticReportJson.LoadFile(reportPath, options);
        Assert.Equal("complete", loaded.Report.Coverage);
        Assert.Equal("synthetic", loaded.Report.Provenance.Source);
        Assert.Equal(schema.SchemaSha256, loaded.Report.SchemaSha256);
        Assert.DoesNotContain(PayloadCanary, File.ReadAllText(reportPath), StringComparison.Ordinal);

        FileDescriptor readable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("AlphaMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int64)),
            ResolverTestData.Message("ConflictMessage", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.String)),
            ResolverTestData.Message("AmbiguousFirst", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.String)),
            ResolverTestData.Message("AmbiguousSecond", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.String)));
        FileDescriptor nearReadable = ResolverTestData.PartialReadableFile("NearMessage", 1, FieldDescriptorProto.Types.Type.Int32);
        CandidateReport resolved = DiagnosticReportResolver.Resolve(schema, loaded, [], [], [readable, nearReadable]);
        CandidateReport resolvedAgain = DiagnosticReportResolver.Resolve(schema, loaded, [], [], [readable, nearReadable]);
        Assert.Equal(CandidateReportJson.Serialize(resolved), CandidateReportJson.Serialize(resolvedAgain));

        MappingCandidate verified = Assert.Single(resolved.Candidates, candidate =>
            candidate.Token == "fixture.Alpha" && candidate.Readable == "d3b.game.AlphaMessage");
        Assert.Equal(CandidateClassification.Candidate, verified.Classification);
        Assert.Contains(verified.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
        CandidateEvidence verifiedObservation = Assert.Single(verified.Evidence, evidence => evidence.Source == "observation");
        Assert.Contains("2 observations", verifiedObservation.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("frame", verifiedObservation.Detail, StringComparison.OrdinalIgnoreCase);

        MappingCandidate conflict = Assert.Single(resolved.Candidates, candidate =>
            candidate.Token == "fixture.Conflict" && candidate.Readable == "d3b.game.ConflictMessage");
        Assert.Equal(CandidateClassification.Conflict, conflict.Classification);
        Assert.Contains(conflict.Contradictions, contradiction => contradiction.Code == ResolverContradictionCode.FieldTypeMismatch);
        Assert.Contains(conflict.Evidence, evidence => evidence.Source == "schema");
        Assert.Contains(conflict.Evidence, evidence => evidence.Source == "readable");

        MappingCandidate near = Assert.Single(resolved.Candidates, candidate =>
            candidate.Token == "fixture.Near" && candidate.Readable == "d3b.game.NearMessage");
        Assert.Equal(CandidateClassification.Candidate, near.Classification);
        Assert.Contains(near.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialReadable);

        MappingCandidate[] ambiguous = resolved.Candidates
            .Where(candidate => candidate.Token == "fixture.Ambiguous"
                && candidate.Readable is "d3b.game.AmbiguousFirst" or "d3b.game.AmbiguousSecond")
            .ToArray();
        Assert.Equal(2, ambiguous.Length);
        Assert.All(ambiguous, candidate =>
        {
            Assert.Equal(CandidateClassification.Candidate, candidate.Classification);
            Assert.Contains(candidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.AmbiguousReadableShape);
            Assert.Contains(candidate.Evidence, evidence => evidence.Source == "observation");
        });

        MappingCandidate unknown = Assert.Single(resolved.Candidates, candidate =>
            candidate.Token == "fixture.Empty" && candidate.Readable is null);
        Assert.Equal(CandidateClassification.Unknown, unknown.Classification);
        Assert.Equal("diagnostic-report", resolved.Provenance!.Source);
        Assert.Equal("synthetic", resolved.Provenance.ObservationSource);
        Assert.Equal(analyses.Length, resolved.Observations.Frames);

        foreach (MappingCandidate candidate in resolved.Candidates.Where(item => item.Classification == CandidateClassification.Candidate))
        {
            PromotionDecision decision = PromotionGate.Evaluate(candidate, resolved, new PromotionContext
            {
                TargetClientVersion = schema.ClientVersion,
                TargetSchemaSha256 = schema.SchemaSha256,
            });

            Assert.False(decision.Allowed);
            Assert.Contains("NOT_VERIFIED", decision.RefusalCodes);
        }

        Assert.Single(Directory.GetFiles(temporary.Path));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(temporary.Path), path =>
            Path.GetFileName(path).Contains("mapping", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(path).Contains("pins", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RustReplayReportChainUsesAggregateEvidenceAndKeepsNearShapeUnverified()
    {
        string? executable = Environment.GetEnvironmentVariable("D3B_DIAGNOSTIC_ENGINE");
        if (!IsExecutable(executable))
        {
            Assert.Skip("D3B_DIAGNOSTIC_ENGINE is missing or does not point to an executable.");
        }

        DiagnosticSchema schema = ReplayTestData.CreateSchema();
        DiagnosticFrame frame = ReplayTestData.CreateFrame(index: 7);
        ReplayResult replay = await ReplayEngineProcess.AnalyzeAsync(
            executable!,
            ["replay"],
            schema,
            [frame],
            TestContext.Current.CancellationToken);

        DiagnosticAnalysis analysis = Assert.Single(replay.Analyses);
        Assert.Equal(DiagnosticStatus.Valid, analysis.Status);
        Assert.Equal(frame.Index, analysis.Index);

        DiagnosticReportOptions options = new();
        DiagnosticReportAggregator aggregator = new(schema, options, CreateBuildContext(schema, replay.EngineHello));
        foreach (DiagnosticAnalysis realAnalysis in replay.Analyses)
        {
            aggregator.Observe(realAnalysis);
        }

        DiagnosticReport report = MarkSynthetic(aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = replay.Summary.Coverage,
            EnqueuedFrames = replay.Summary.Frames,
            AnalyzedFrames = replay.Summary.Frames,
            DroppedFrames = replay.Summary.Dropped,
            CompletedAtUtc = _completedAtUtc,
        }));

        using TemporaryDirectory temporary = new();
        AtomicDiagnosticReportWriter writer = new(options, temporary.Path, TimeProvider.System);
        await writer.WriteAsync(report, TestContext.Current.CancellationToken);
        LoadedDiagnosticReport loaded = DiagnosticReportJson.LoadFile(Assert.Single(Directory.GetFiles(temporary.Path)), options);

        FileDescriptor exactReadable = ResolverTestData.ReadableFile(
            ResolverTestData.Message("ExactLok", ResolverTestData.ReadableField("value", 1, FieldDescriptorProto.Types.Type.Int32)));
        CandidateReport exactResolution = DiagnosticReportResolver.Resolve(schema, loaded, [], [], [exactReadable]);
        MappingCandidate exactCandidate = Assert.Single(exactResolution.Candidates, candidate =>
            candidate.Token == "lok" && candidate.Readable == "d3b.game.ExactLok");
        Assert.Equal(CandidateClassification.Candidate, exactCandidate.Classification);
        Assert.Contains(exactCandidate.Uncertainties, uncertainty => uncertainty.Code.ToString() == "SemanticProofMissing");
        CandidateEvidence aggregateEvidence = Assert.Single(exactCandidate.Evidence, evidence => evidence.Source == "observation");
        Assert.Contains("1 observations", aggregateEvidence.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("frame", aggregateEvidence.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("index", aggregateEvidence.Detail, StringComparison.OrdinalIgnoreCase);

        FileDescriptor nearReadable = ResolverTestData.PartialReadableFile("NearLok", 1, FieldDescriptorProto.Types.Type.Int32);
        CandidateReport nearResolution = DiagnosticReportResolver.Resolve(schema, loaded, [], [], [nearReadable]);
        MappingCandidate nearCandidate = Assert.Single(nearResolution.Candidates, candidate =>
            candidate.Token == "lok" && candidate.Readable == "d3b.game.NearLok");
        Assert.Equal(CandidateClassification.Candidate, nearCandidate.Classification);
        Assert.NotEqual(CandidateClassification.Verified, nearCandidate.Classification);
        Assert.Contains(nearCandidate.Uncertainties, uncertainty => uncertainty.Code == ResolverUncertaintyCode.PartialReadable);
    }

    [Fact]
    public void WritesCliDemoArtifactsWhenConfigured()
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable("D3B_P2L_DEMO_DIR");
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            Assert.Skip("D3B_P2L_DEMO_DIR is not set.");
        }

        string directory = Path.GetFullPath(configuredDirectory!);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(temporaryRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("P2L demo artifacts must be written below the system temporary directory.");
        }

        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        FileDescriptorSet descriptorSet = DiagnosticTestSchema.CreateDescriptorSet();
        byte[] schemaBytes = descriptorSet.ToByteArray();
        string clientVersion = "3.7.99.98";
        DiagnosticSchema schema = DiagnosticSchemaExporter.Export(clientVersion, ProtocolSchema.Load(schemaBytes));
        DiagnosticMessage message = schema.Messages.Single(item => item.Name == "fixture.Root.Inner");
        DiagnosticAnalysis analysis = CreateAnalysis(message, 0, "server", DiagnosticStatus.Valid);
        DiagnosticReportOptions options = new();
        DiagnosticReportAggregator aggregator = new(schema, options, CreateBuildContext(schema));
        aggregator.Observe(analysis);
        DiagnosticReport report = MarkSynthetic(aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = "complete",
            EnqueuedFrames = 1,
            AnalyzedFrames = 1,
            CompletedAtUtc = _completedAtUtc,
        }));

        string schemaPath = Path.Combine(directory, "schema-1.binpb");
        string reportPath = Path.Combine(directory, "report.json");
        string pinsPath = Path.Combine(directory, "pins.json");
        File.WriteAllBytes(schemaPath, schemaBytes);
        File.WriteAllBytes(reportPath, DiagnosticReportJson.SerializeToUtf8Bytes(report, options));
        string pinsJson = JsonSerializer.Serialize(new
        {
            clientVersion,
            messages = new[] { new { readable = "d3b.game.AuthenticationRequest", obfuscated = message.Name } },
            enums = Array.Empty<object>(),
        });
        File.WriteAllText(pinsPath, pinsJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(schemaPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(reportPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(pinsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        LoadedDiagnosticReport loaded = DiagnosticReportJson.LoadFile(reportPath, options);
        PinnedPairs pins = PinnedPairsFile.Read(pinsPath, clientVersion);
        Assert.Equal(schema.SchemaSha256, loaded.Report.SchemaSha256);
        Assert.Equal(schema.SchemaSha256, DiagnosticSchemaExporter.Export(clientVersion, ProtocolSchema.Load(File.ReadAllBytes(schemaPath))).SchemaSha256);
        Assert.Equal("synthetic", loaded.Report.Provenance.Source);
        Assert.Equal("fixture.Root.Inner", Assert.Single(pins.Messages).Obfuscated);
        Assert.Equal("d3b.game.AuthenticationRequest", Assert.Single(pins.Messages).Readable);
        Assert.Equal("complete", loaded.Report.Coverage);
        Assert.Equal(64, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(schemaPath))).Length);
    }

    private static DiagnosticSchema CreateChainSchema()
    {
        FileDescriptorProto file = new()
        {
            Name = "p2l_chain.proto",
            Package = "fixture",
            Syntax = "proto3",
            MessageType =
            {
                Message("Alpha", Field("value", 1, FieldDescriptorProto.Types.Type.Int64)),
                Message("Conflict", Field("value", 1, FieldDescriptorProto.Types.Type.Int32)),
                Message("Ambiguous", Field("value", 1, FieldDescriptorProto.Types.Type.String)),
                Message("Near", Field("value", 1, FieldDescriptorProto.Types.Type.Int32)),
                Message("Empty"),
            },
        };
        FileDescriptorSet descriptorSet = new();
        descriptorSet.File.Add(file);
        return DiagnosticSchemaExporter.Export("3.7.99.99", descriptorSet);
    }

    private static DiagnosticAnalysis[] CreateChainAnalyses(DiagnosticSchema schema)
    {
        return
        [
            CreateAnalysis(schema.Messages.Single(message => message.Name == "fixture.Alpha"), 0, "server", DiagnosticStatus.Valid),
            CreateAnalysis(schema.Messages.Single(message => message.Name == "fixture.Alpha"), 1, "server", DiagnosticStatus.Valid),
            CreateAnalysis(schema.Messages.Single(message => message.Name == "fixture.Conflict"), 2, "client", DiagnosticStatus.Valid),
            CreateAnalysis(schema.Messages.Single(message => message.Name == "fixture.Ambiguous"), 3, "d3b", DiagnosticStatus.Valid),
            CreateAnalysis(schema.Messages.Single(message => message.Name == "fixture.Near"), 4, "client", DiagnosticStatus.Valid),
            CreateAnalysis(schema.Messages.Single(message => message.Name == "fixture.Empty"), 5, "server", DiagnosticStatus.Malformed,
                [new DiagnosticProblem { Code = DiagnosticProblemCode.Malformed, Detail = PayloadCanary }]),
        ];
    }

    private static DiagnosticAnalysis CreateAnalysis(
        DiagnosticMessage message,
        long index,
        string direction,
        DiagnosticStatus status,
        IReadOnlyList<DiagnosticProblem>? problems = null)
    {
        return new DiagnosticAnalysis
        {
            Index = index,
            Direction = direction,
            CarriedType = message.Name,
            TypeUrlOk = true,
            Message = message.Name,
            Status = status,
            Fields = message.Fields.Select(field => new DiagnosticFieldAnalysis
            {
                Number = field.Number,
                Name = field.Name,
                DeclaredKind = field.Kind,
                Repeated = field.Repeated,
                Packed = field.Packed,
                Occurrences = 1,
                WireTypes = [WireType(field.Kind)],
                Observed = field.Kind is DiagnosticFieldKind.Utf8String or DiagnosticFieldKind.Bytes or DiagnosticFieldKind.Message
                    ? new DiagnosticObservedShape { Length = [1], Utf8 = field.Kind == DiagnosticFieldKind.Utf8String ? true : null }
                    : new DiagnosticObservedShape { VarintBytes = [1] },
            }).ToArray(),
            Problems = problems ?? Array.Empty<DiagnosticProblem>(),
        };
    }

    private static DiagnosticFrame CreateFrame(DiagnosticAnalysis analysis)
    {
        byte[] payload = analysis.CarriedType switch
        {
            "fixture.Alpha" or "fixture.Conflict" or "fixture.Near" => [0x08, 0x01],
            "fixture.Ambiguous" => [0x0a, 0x01, 0x78],
            "fixture.Empty" => analysis.Status == DiagnosticStatus.Malformed ? [0x80] : [],
            _ => [],
        };
        return new DiagnosticFrame(
            analysis.Index,
            analysis.Direction,
            "type.ankama.com/" + analysis.CarriedType,
            analysis.CarriedType,
            payload);
    }

    private static DiagnosticReportBuildContext CreateBuildContext(DiagnosticSchema schema, DiagnosticEngineHello? engine = null)
    {
        return new DiagnosticReportBuildContext
        {
            ClientVersion = schema.ClientVersion,
            SchemaSha256 = schema.SchemaSha256,
            Engine = engine ?? new DiagnosticEngineHello
            {
                ProtocolVersion = DiagnosticProtocol.ProtocolVersion,
                Engine = "d3b-diagnostic",
                EngineVersion = "0.1.0",
                SnsCommit = "abcdef0",
            },
            Producer = "D3B.Diagnostic",
            ProducerVersion = "1.0.0-p2l.synthetic",
            StartedAtUtc = _startedAtUtc,
            IncludeReadable = false,
            QueueCapacity = 16,
            QueueByteCapacity = 1_048_576,
        };
    }

    private static DiagnosticReport MarkSynthetic(DiagnosticReport report)
    {
        return report with { Provenance = report.Provenance with { Source = "synthetic" } };
    }

    private static bool IsExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        UnixFileMode executableBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & executableBits) != 0;
    }

    private static DescriptorProto Message(string name, params FieldDescriptorProto[] fields)
    {
        DescriptorProto message = new() { Name = name };
        message.Field.Add(fields);
        return message;
    }

    private static FieldDescriptorProto Field(string name, int number, FieldDescriptorProto.Types.Type type)
    {
        return new FieldDescriptorProto
        {
            Name = name,
            Number = number,
            Type = type,
            Label = FieldDescriptorProto.Types.Label.Optional,
        };
    }

    private static DiagnosticWireType WireType(DiagnosticFieldKind kind)
    {
        return kind switch
        {
            DiagnosticFieldKind.DoublePrecision or DiagnosticFieldKind.Fixed64 or DiagnosticFieldKind.SignedFixed64 => DiagnosticWireType.I64,
            DiagnosticFieldKind.SinglePrecision or DiagnosticFieldKind.Fixed32 or DiagnosticFieldKind.SignedFixed32 => DiagnosticWireType.I32,
            DiagnosticFieldKind.Utf8String or DiagnosticFieldKind.Bytes or DiagnosticFieldKind.Message => DiagnosticWireType.Len,
            DiagnosticFieldKind.Group => DiagnosticWireType.Group,
            _ => DiagnosticWireType.Varint,
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d3b-p2l-integration-" + Guid.NewGuid().ToString("N"));
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
}
