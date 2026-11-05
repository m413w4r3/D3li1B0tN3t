using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Reporting;
using D3B.Protocol.Mapping;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Resolution;

/// <summary>Resolves a redacted diagnostic report against one exact build schema.</summary>
public static class DiagnosticReportResolver
{
    private const string InvalidReportMessage = "diagnostic report is invalid for the build schema";
    private const string BuildMismatchMessage = "diagnostic report does not match the build schema";

    /// <summary>Resolves the report against read-only references and build pins.</summary>
    public static CandidateReport Resolve(
        DiagnosticSchema buildSchema,
        LoadedDiagnosticReport report,
        IEnumerable<ReferenceCorpus> references,
        IEnumerable<PriorMapping> pins)
    {
        return Resolve(buildSchema, report, references, pins, ReadableProtocol.Files);
    }

    internal static CandidateReport Resolve(
        DiagnosticSchema buildSchema,
        LoadedDiagnosticReport report,
        IEnumerable<ReferenceCorpus> references,
        IEnumerable<PriorMapping> pins,
        IReadOnlyList<FileDescriptor> readableFiles)
    {
        ArgumentNullException.ThrowIfNull(buildSchema);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(readableFiles);

        DiagnosticReport source = report.Report;
        if (!DiagnosticReportJson.IsValidReportObject(source)
            || !IsValidReportEnvelope(source)
            || !IsSha256(report.Sha256))
        {
            throw new DiagnosticReportResolutionException(InvalidReportMessage);
        }

        if (!string.Equals(source.ClientVersion, buildSchema.ClientVersion, StringComparison.Ordinal)
            || !string.Equals(source.SchemaSha256, buildSchema.SchemaSha256, StringComparison.Ordinal))
        {
            throw new DiagnosticReportResolutionException(BuildMismatchMessage);
        }

        Dictionary<string, DiagnosticMessage> schemaMessages = buildSchema.Messages
            .GroupBy(message => message.Name, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        if (schemaMessages.Count != buildSchema.Messages.Count)
        {
            throw new DiagnosticReportResolutionException(InvalidReportMessage);
        }

        HashSet<string> tokens = new(StringComparer.Ordinal);
        foreach (DiagnosticReportType? type in source.Types)
        {
            if (type is null || !tokens.Add(type.Token) || FindSchemaMessage(schemaMessages, type.Token) is null)
            {
                throw new DiagnosticReportResolutionException(InvalidReportMessage);
            }
        }

        bool truncated = HasReportTruncation(source);
        string effectiveCoverage = source.Coverage;
        if (source.Session.State != DiagnosticReportSessionState.Completed
            || source.Session.ErrorCode is not null
            || truncated
            || !DiagnosticReportJson.HasMatchingCompleteFrameAccounting(source)
            || HasIncompleteCoverage(source))
        {
            effectiveCoverage = "partial";
        }

        ResolverObservation[] projected = source.Types.Select(type => new ResolverObservation
        {
            Token = type.Token,
            Occurrences = type.Occurrences,
            Directions = type.Directions.Select(direction => new ResolverObservationDirection
            {
                Direction = direction.Direction,
                Occurrences = direction.Occurrences
            }).ToArray(),
            Statuses = type.Statuses.Select(status => new ResolverObservationStatus
            {
                Status = status.Status,
                Occurrences = status.Occurrences
            }).ToArray(),
            TypeUrlOkOccurrences = type.TypeUrlOkOccurrences,
            Problems = type.Problems.Select(problem => new ResolverObservationProblem
            {
                Code = problem.Code,
                FieldPath = problem.FieldPath,
                Occurrences = problem.Occurrences
            }).ToArray(),
            ReportContradictions = type.Contradictions
                .Where(contradiction => IsHardReportContradiction(contradiction.Code))
                .Select(contradiction => new ResolverObservationReportContradiction
                {
                    Code = contradiction.Code,
                    Occurrences = contradiction.Occurrences
                }).ToArray(),
            Truncated = truncated
                || type.DetailTruncated
                || type.Contradictions.Any(contradiction => contradiction.Code == DiagnosticReportContradictionCode.DetailTruncated)
        }).ToArray();

        CandidateObservationSummary summary = new()
        {
            Frames = source.Session.AnalyzedFrames,
            Coverage = effectiveCoverage,
            Dropped = source.Session.DroppedFrames
        };
        CandidateReport resolved = new ProtocolResolver(readableFiles, references, pins)
            .Resolve(buildSchema, Array.Empty<DiagnosticAnalysis>(), null, new DiagnosticSummary
            {
                Frames = summary.Frames,
                Coverage = summary.Coverage,
                Dropped = summary.Dropped
            }, projected);

        return resolved with
        {
            Provenance = new ResolutionProvenance
            {
                Source = "diagnostic-report",
                ReportSha256 = report.Sha256,
                ReportVersion = source.ReportVersion,
                ReportProtocolVersion = source.ProtocolVersion,
                Engine = source.Engine.Engine,
                EngineVersion = source.Engine.EngineVersion,
                SnsCommit = source.Engine.SnsCommit,
                ObservationSource = source.Provenance.Source,
                ObservationCoverage = effectiveCoverage,
                ObservationDroppedFrames = source.Session.DroppedFrames,
                ReportTruncated = truncated
            }
        };
    }

    private static bool IsValidReportEnvelope(DiagnosticReport report)
    {
        return report is not null
            && report.Kind == "diagnostic-report"
            && report.ReportVersion == 1
            && report.ProtocolVersion == DiagnosticProtocol.ProtocolVersion
            && report.Coverage is "complete" or "partial"
            && report.ClientVersion is { Length: > 0 }
            && DiagnosticReportJson.IsSafeClientVersion(report.ClientVersion)
            && IsSha256(report.SchemaSha256)
            && report.Engine is not null
            && DiagnosticReportJson.IsSafeEngine(report.Engine.Engine)
            && DiagnosticReportJson.IsSafeEngineVersion(report.Engine.EngineVersion)
            && DiagnosticReportJson.IsSafeSnsCommit(report.Engine.SnsCommit)
            && report.Provenance is not null
            && report.Provenance.Source is "live" or "synthetic"
            && DiagnosticReportJson.IsSafeProducer(report.Provenance.Producer)
            && DiagnosticReportJson.IsSafeProducerVersion(report.Provenance.ProducerVersion)
            && report.Session is not null
            && report.Truncation is not null
            && report.Types is not null
            && DiagnosticReportJson.HasValidOccurrenceAccounting(report);
    }

    private static DiagnosticMessage? FindSchemaMessage(
        IReadOnlyDictionary<string, DiagnosticMessage> schemaMessages,
        string token)
    {
        if (schemaMessages.TryGetValue(token, out DiagnosticMessage? exact))
        {
            return exact;
        }

        string simpleName = SimpleName(token);
        DiagnosticMessage[] matches = schemaMessages.Values
            .Where(message => string.Equals(SimpleName(message.Name), simpleName, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string SimpleName(string value)
    {
        int separator = value.LastIndexOf('.');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }

    private static bool HasReportTruncation(DiagnosticReport report)
    {
        DiagnosticReportTruncation truncation = report.Truncation;
        return truncation.OutputTruncated
            || truncation.TypesOmitted > 0
            || truncation.FieldsOmitted > 0
            || truncation.NestedFieldsOmitted > 0
            || truncation.VariantsOmitted > 0
            || truncation.ProblemsOmitted > 0
            || truncation.ShapeValuesOmitted > 0
            || truncation.Reasons.Any(reason => reason is "type-limit" or "field-limit" or "nested-limit" or "variant-limit"
                or "problem-limit" or "shape-limit" or "detail-truncated" or "output-size")
            || report.Types.Any(type => type.DetailTruncated
                || type.Contradictions.Any(contradiction => contradiction.Code == DiagnosticReportContradictionCode.DetailTruncated));
    }

    private static bool HasIncompleteCoverage(DiagnosticReport report)
    {
        return report.Session.UnattributedAnalyses > 0
            || report.Session.DroppedFrames > 0
            || report.Truncation.Reasons.Any(reason => reason is "unattributed-analysis" or "summary-mismatch"
                or "aggregation-failed" or "dropped-frame" or "session-incomplete");
    }

    private static bool IsHardReportContradiction(DiagnosticReportContradictionCode code)
    {
        return code is DiagnosticReportContradictionCode.TypeUrlMismatch
            or DiagnosticReportContradictionCode.MessageMismatch
            or DiagnosticReportContradictionCode.FieldDeclarationMismatch;
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
