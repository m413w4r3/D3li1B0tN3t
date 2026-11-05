using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Tests.Reporting;

public sealed class DiagnosticReportTests
{
    [Fact]
    public void SuccessiveAnalysesForOneTypeAreAggregated()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "client", DiagnosticStatus.Valid, [Field(1, [DiagnosticWireType.Varint], [1])]),
             Analysis("Alpha", "server", DiagnosticStatus.Malformed, [Field(1, [DiagnosticWireType.Varint], [2])])]);

        DiagnosticReportType type = Assert.Single(report.Types);
        Assert.Equal(2, type.Occurrences);
        Assert.Equal([new DiagnosticReportDirectionCount { Direction = "client", Occurrences = 1 }, new DiagnosticReportDirectionCount { Direction = "server", Occurrences = 1 }], type.Directions);
        Assert.Equal(2, type.Statuses.Sum(item => item.Occurrences));
        Assert.Equal(2, Assert.Single(type.Fields).Occurrences);
        Assert.Equal([1, 2], Assert.Single(type.Fields).Observed.VarintBytes);
    }

    [Fact]
    public void MultipleTypesAndDirectionsRemainDistinct()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "client", DiagnosticStatus.Valid), Analysis("pkg.Beta", "d3b", DiagnosticStatus.Truncated)]);

        Assert.Equal(["pkg.Alpha", "pkg.Beta"], report.Types.Select(item => item.Token));
        Assert.Equal("client", Assert.Single(report.Types[0].Directions).Direction);
        Assert.Equal("d3b", Assert.Single(report.Types[1].Directions).Direction);
    }

    [Fact]
    public void SameTypeWithDifferentStructuresKeepsVariantsAndStatusCounts()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid, [Field(1, [DiagnosticWireType.Varint])]),
             Analysis("pkg.Alpha", "server", DiagnosticStatus.Malformed, [Field(2, [DiagnosticWireType.Len])])]);

        DiagnosticReportType type = Assert.Single(report.Types);
        Assert.Equal(2, type.Variants.Count);
        Assert.Equal(2, type.Variants.Sum(item => item.Occurrences));
        Assert.Contains(type.Contradictions, item => item.Code == DiagnosticReportContradictionCode.ShapeVariation);
    }

    [Fact]
    public void FieldShapesMergeDeterministicallyAndRespectShapeCap()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid, [Field(1, [DiagnosticWireType.Varint], [3, 1])]),
             Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid, [Field(1, [DiagnosticWireType.Varint], [2])])],
            new DiagnosticReportOptions { MaxShapeValues = 2 });

        DiagnosticReportField field = Assert.Single(Assert.Single(report.Types).Fields);
        Assert.Equal([1, 2], field.Observed.VarintBytes);
        Assert.Equal(1, report.Truncation.ShapeValuesOmitted);
        Assert.Equal("partial", report.Coverage);
    }

    [Fact]
    public void ProblemsAreAggregatedWithoutEngineDetailAndFieldPathsAreSanitized()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "server", DiagnosticStatus.Malformed, problems:
            [new DiagnosticProblem { Code = DiagnosticProblemCode.Malformed, FieldPath = "1.2", Detail = "detail canary" },
             new DiagnosticProblem { Code = DiagnosticProblemCode.Malformed, FieldPath = "../private", Detail = "other canary" }])]);

        string json = DiagnosticReportJson.Serialize(report, new DiagnosticReportOptions());
        Assert.DoesNotContain("canary", json, StringComparison.Ordinal);
        Assert.Contains(Assert.Single(report.Types).Problems, item => item.FieldPath == "1.2");
        Assert.Contains(Assert.Single(report.Types).Problems, item => item.FieldPath is null);
    }

    [Fact]
    public void TypeFieldNodeVariantAndProblemCapsMarkCoveragePartialAndCountOmissions()
    {
        DiagnosticReportOptions options = new()
        {
            MaxTypes = 1,
            MaxFieldsPerType = 1,
            MaxFieldNodesPerType = 1,
            MaxNestedDepth = 0,
            MaxVariantsPerType = 1,
            MaxProblemsPerType = 1,
        };
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid,
                [Field(1, [DiagnosticWireType.Varint]), Field(2, [DiagnosticWireType.Len], nested: [Field(1, [DiagnosticWireType.Len])])],
                [new DiagnosticProblem { Code = DiagnosticProblemCode.Malformed }, new DiagnosticProblem { Code = DiagnosticProblemCode.Truncated }]),
             Analysis("pkg.Alpha", "server", DiagnosticStatus.Malformed, [Field(3, [DiagnosticWireType.I32])]),
             Analysis("pkg.Beta", "server", DiagnosticStatus.Valid)], options);

        Assert.Equal(1, report.Truncation.TypesOmitted);
        Assert.Equal(2, report.Truncation.FieldsOmitted);
        Assert.True(report.Truncation.NestedFieldsOmitted > 0);
        Assert.True(report.Truncation.VariantsOmitted > 0);
        Assert.True(report.Truncation.ProblemsOmitted > 0);
        Assert.Equal("partial", report.Coverage);
    }

    [Fact]
    public void OutputSizeCapProducesValidBoundedJsonAndPartialCoverage()
    {
        DiagnosticMessage[] messages = Enumerable.Range(0, 2048)
            .Select(index => Message($"pkg.Type{index:D4}"))
            .ToArray();
        DiagnosticReport report = Build([.. messages.Select(message => Analysis(message.Name, "server", DiagnosticStatus.Valid))], schema: new DiagnosticSchema { Messages = messages });
        DiagnosticReportOptions options = new() { MaxBytes = 65_536 };

        byte[] bytes = DiagnosticReportJson.SerializeToUtf8Bytes(report, options);
        Assert.True(bytes.Length <= options.MaxBytes);
        Assert.Equal("partial", DiagnosticReportJson.Load(bytes, options).Report.Coverage);
        Assert.True(DiagnosticReportJson.Load(bytes, options).Report.Truncation.OutputTruncated);
    }

    [Fact]
    public void ReportJsonIsByteDeterministicAndRoundTrips()
    {
        DiagnosticReport report = Build([Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid, [Field(1, [DiagnosticWireType.Varint], [1, 2])])]);
        DiagnosticReportOptions options = new();

        byte[] first = DiagnosticReportJson.SerializeToUtf8Bytes(report, options);
        byte[] second = DiagnosticReportJson.SerializeToUtf8Bytes(report, options);
        LoadedDiagnosticReport loaded = DiagnosticReportJson.Load(first, options);

        Assert.Equal(first, second);
        Assert.Equal(first, DiagnosticReportJson.SerializeToUtf8Bytes(loaded.Report, options));
    }

    [Fact]
    public void LoaderRejectsWrongVersionOversizedMalformedUnsafeAndInconsistentReports()
    {
        DiagnosticReport report = Build([Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid)]);
        DiagnosticReportOptions options = new() { MaxBytes = 65_536 };
        string valid = DiagnosticReportJson.Serialize(report, options);
        Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load(Encoding.UTF8.GetBytes(valid.Replace("\"reportVersion\":1", "\"reportVersion\":2", StringComparison.Ordinal)), options));
        Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load(new byte[65_537], options));
        Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load("{"u8.ToArray(), options));
        Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load(Encoding.UTF8.GetBytes(valid.Replace("pkg.Alpha", "../unsafe", StringComparison.Ordinal)), options));
        JsonObject inconsistent = JsonNode.Parse(valid)!.AsObject();
        inconsistent["types"]![0]!["directions"]![0]!["occurrences"] = 3;
        Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load(Encoding.UTF8.GetBytes(inconsistent.ToJsonString()), options));
    }

    [Fact]
    public void LoaderRejectsCompleteCoverageWithIncompleteSessionAccounting()
    {
        string valid = DiagnosticReportJson.Serialize(Build([Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid)]), new DiagnosticReportOptions());

        AssertRejected(root => root["session"]!["unattributedAnalyses"] = 1);
        AssertRejected(root => root["session"]!["droppedFrames"] = 1);
        AssertRejected(root => root["session"]!["errorCode"] = "IO_ERROR");
        AssertRejected(root => root["session"]!["state"] = "FAILED");
        AssertRejected(root => root["truncation"]!["outputTruncated"] = true);
        AssertRejected(root => root["truncation"]!["typesOmitted"] = 1);
        AssertRejected(root => root["truncation"]!["reasons"]!.AsArray().Add("session-incomplete"));

        void AssertRejected(Action<JsonObject> mutate)
        {
            JsonObject candidate = JsonNode.Parse(valid)!.AsObject();
            mutate(candidate);
            Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load(Encoding.UTF8.GetBytes(candidate.ToJsonString())));
        }
    }

    [Fact]
    public void LoaderRejectsUnsafeEngineIdentitySeparators()
    {
        string valid = DiagnosticReportJson.Serialize(Build([Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid)]), new DiagnosticReportOptions());

        AssertRejected(root => root["engine"]!["engineVersion"] = "/private/engine");
        AssertRejected(root => root["engine"]!["snsCommit"] = "user@host");

        void AssertRejected(Action<JsonObject> mutate)
        {
            JsonObject candidate = JsonNode.Parse(valid)!.AsObject();
            mutate(candidate);
            Assert.Throws<DiagnosticReportException>(() => DiagnosticReportJson.Load(Encoding.UTF8.GetBytes(candidate.ToJsonString())));
        }
    }

    [Fact]
    public void UnattributedAndUnsafeEngineStringsNeverReachTheReport()
    {
        DiagnosticReport report = Build(
            [new DiagnosticAnalysis
            {
                CarriedType = "canary.Unknown",
                Message = "canary.Message",
                Direction = "server",
                Status = DiagnosticStatus.Malformed,
                Fields = [new DiagnosticFieldAnalysis { Number = 1, Name = "canaryField", Problems = [DiagnosticProblemCode.Malformed] }],
                Problems = [new DiagnosticProblem { Code = DiagnosticProblemCode.Malformed, Detail = "canaryDetail" }],
            }],
            context: Context() with { Engine = new DiagnosticEngineHello { Engine = "canary\nengine", EngineVersion = "safe?", SnsCommit = "raw\ncommit" } });

        string json = DiagnosticReportJson.Serialize(report, new DiagnosticReportOptions());
        Assert.Equal(1, report.Session.UnattributedAnalyses);
        Assert.Empty(report.Types);
        Assert.DoesNotContain("canary", json, StringComparison.Ordinal);
        Assert.DoesNotContain("safe?", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeEngineIdentitySeparatorsAreRemovedFromTheReport()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid)],
            context: Context() with
            {
                Engine = new DiagnosticEngineHello
                {
                    ProtocolVersion = 1,
                    Engine = "sns",
                    EngineVersion = "/private/engine",
                    SnsCommit = "user@host",
                },
            });

        string json = DiagnosticReportJson.Serialize(report, new DiagnosticReportOptions());

        Assert.Equal(string.Empty, report.Engine.EngineVersion);
        Assert.Equal(string.Empty, report.Engine.SnsCommit);
        Assert.DoesNotContain("/private/engine", json, StringComparison.Ordinal);
        Assert.DoesNotContain("user@host", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EngineIdentitySessionStateAndProvenanceArePreserved()
    {
        DiagnosticReport report = Build(
            [Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid)],
            context: Context() with
            {
                Engine = new DiagnosticEngineHello { ProtocolVersion = 1, Engine = "sns", EngineVersion = "1.2", SnsCommit = "abc123" },
                Producer = "D3B.Diagnostic",
                ProducerVersion = "0.4.0",
                IncludeReadable = true,
                QueueCapacity = 8,
                QueueByteCapacity = 4096,
            },
            outcome: Outcome() with { State = DiagnosticReportSessionState.Completed, EnqueuedFrames = 1, AnalyzedFrames = 1 });

        Assert.Equal("sns", report.Engine.Engine);
        Assert.Equal("abc123", report.Engine.SnsCommit);
        Assert.Equal("0.4.0", report.Provenance.ProducerVersion);
        Assert.True(report.Session.IncludeReadable);
        Assert.Equal(8, report.Session.QueueCapacity);
        Assert.Equal(DiagnosticReportSessionState.Completed, report.Session.State);
    }

    [Fact]
    public void FieldDeclarationMismatchDetailTruncatedAndTypeUrlMismatchBecomeContradictions()
    {
        DiagnosticReport report = Build(
            [new DiagnosticAnalysis
            {
                CarriedType = "pkg.Alpha",
                Message = "canary.Unknown",
                Direction = "server",
                Status = DiagnosticStatus.SchemaMismatch,
                TypeUrlOk = false,
                DetailTruncated = true,
                Fields = [new DiagnosticFieldAnalysis { Number = 1, Name = "not_count", DeclaredKind = DiagnosticFieldKind.Bytes }],
            }]);

        DiagnosticReportType type = Assert.Single(report.Types);
        Assert.Equal("count", Assert.Single(type.Fields).Name);
        Assert.DoesNotContain("not_count", DiagnosticReportJson.Serialize(report, new DiagnosticReportOptions()), StringComparison.Ordinal);
        Assert.Contains(type.Contradictions, item => item.Code == DiagnosticReportContradictionCode.TypeUrlMismatch);
        Assert.Contains(type.Contradictions, item => item.Code == DiagnosticReportContradictionCode.MessageMismatch);
        Assert.Contains(type.Contradictions, item => item.Code == DiagnosticReportContradictionCode.FieldDeclarationMismatch);
        Assert.Contains(type.Contradictions, item => item.Code == DiagnosticReportContradictionCode.DetailTruncated);
    }

    [Fact]
    public void EmptyObservationBuildsAValidCompleteReport()
    {
        DiagnosticReport report = Build([]);

        Assert.Empty(report.Types);
        Assert.Equal("complete", report.Coverage);
        Assert.Equal("diagnostic-report", DiagnosticReportJson.Load(DiagnosticReportJson.SerializeToUtf8Bytes(report, new DiagnosticReportOptions())).Report.Kind);
    }

    [Fact]
    public void LoaderComputesSha256OfExactBytes()
    {
        byte[] bytes = DiagnosticReportJson.SerializeToUtf8Bytes(Build([Analysis("pkg.Alpha", "server", DiagnosticStatus.Valid)]), new DiagnosticReportOptions());

        LoadedDiagnosticReport loaded = DiagnosticReportJson.Load(bytes);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), loaded.Sha256);
    }

    private static DiagnosticReport Build(
        IReadOnlyList<DiagnosticAnalysis> analyses,
        DiagnosticReportOptions? options = null,
        DiagnosticReportBuildContext? context = null,
        DiagnosticReportBuildOutcome? outcome = null,
        DiagnosticSchema? schema = null)
    {
        var aggregator = new DiagnosticReportAggregator(schema ?? CreateSchema(), options ?? new DiagnosticReportOptions(), context ?? Context());
        foreach (DiagnosticAnalysis analysis in analyses)
        {
            aggregator.Observe(analysis);
        }

        return aggregator.Build(outcome ?? Outcome() with { EnqueuedFrames = analyses.Count, AnalyzedFrames = analyses.Count });
    }

    private static DiagnosticSchema CreateSchema()
    {
        return new DiagnosticSchema
        {
            SchemaSha256 = new string('a', 64),
            Messages = [Message("pkg.Alpha"), Message("pkg.Beta")],
        };
    }

    private static DiagnosticMessage Message(string name)
    {
        return new DiagnosticMessage
        {
            Name = name,
            Fields = name == "pkg.Alpha"
                ? [new DiagnosticField { Number = 1, Name = "count", Kind = DiagnosticFieldKind.SignedInteger32 }, new DiagnosticField { Number = 2, Name = "child", Kind = DiagnosticFieldKind.Message, MessageType = "pkg.Child" }]
                : [new DiagnosticField { Number = 3, Name = "other", Kind = DiagnosticFieldKind.Utf8String }],
        };
    }

    private static DiagnosticAnalysis Analysis(
        string carriedType,
        string direction,
        DiagnosticStatus status,
        IReadOnlyList<DiagnosticFieldAnalysis>? fields = null,
        IReadOnlyList<DiagnosticProblem>? problems = null)
    {
        return new DiagnosticAnalysis
        {
            CarriedType = carriedType,
            Message = carriedType.StartsWith("Alpha", StringComparison.Ordinal) || carriedType == "pkg.Alpha" ? "pkg.Alpha" : carriedType,
            Direction = direction,
            Status = status,
            TypeUrlOk = true,
            Fields = fields ?? Array.Empty<DiagnosticFieldAnalysis>(),
            Problems = problems ?? Array.Empty<DiagnosticProblem>(),
        };
    }

    private static DiagnosticFieldAnalysis Field(int number, IReadOnlyList<DiagnosticWireType> wires, IReadOnlyList<int>? varintBytes = null, IReadOnlyList<DiagnosticFieldAnalysis>? nested = null)
    {
        return new DiagnosticFieldAnalysis
        {
            Number = number,
            Occurrences = 1,
            WireTypes = wires,
            Observed = new DiagnosticObservedShape { VarintBytes = varintBytes },
            Nested = nested,
        };
    }

    private static DiagnosticReportBuildContext Context()
    {
        return new DiagnosticReportBuildContext
        {
            ClientVersion = "3.7.12",
            SchemaSha256 = new string('a', 64),
            Engine = new DiagnosticEngineHello { ProtocolVersion = 1, Engine = "sns", EngineVersion = "1.0", SnsCommit = "abc" },
            ProducerVersion = "1.0.0",
            StartedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            QueueCapacity = 4,
            QueueByteCapacity = 4096,
        };
    }

    private static DiagnosticReportBuildOutcome Outcome()
    {
        return new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = "complete",
            CompletedAtUtc = DateTimeOffset.Parse("2026-01-01T00:01:00Z", System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
