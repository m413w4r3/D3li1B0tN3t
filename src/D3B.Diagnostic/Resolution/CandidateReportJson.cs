using System.Text.Json;

namespace D3B.Diagnostic.Resolution;

/// <summary>Exports resolver reports as deterministic compact JSON.</summary>
public static class CandidateReportJson
{
    /// <summary>Serializes a report to compact UTF-8 JSON bytes.</summary>
    public static byte[] SerializeToUtf8Bytes(CandidateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        CandidateReport ordered = Order(report);
        return JsonSerializer.SerializeToUtf8Bytes(ordered, CandidateReportJsonContext.Default.CandidateReport);
    }

    /// <summary>Serializes a report to compact JSON text.</summary>
    public static string Serialize(CandidateReport report)
    {
        return System.Text.Encoding.UTF8.GetString(SerializeToUtf8Bytes(report));
    }

    private static CandidateReport Order(CandidateReport report)
    {
        MappingCandidate[] candidates = report.Candidates
            .OrderBy(candidate => candidate.Token, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Readable, StringComparer.Ordinal)
            .Select(candidate => candidate with
            {
                Evidence = candidate.Evidence
                    .OrderBy(evidence => evidence.Source, StringComparer.Ordinal)
                    .ThenBy(evidence => evidence.Detail, StringComparer.Ordinal)
                    .ThenBy(evidence => evidence.Build, StringComparer.Ordinal)
                    .ThenBy(evidence => evidence.Provenance, StringComparer.Ordinal)
                    .ToArray(),
                Contradictions = candidate.Contradictions
                    .OrderBy(contradiction => contradiction.Code)
                    .ThenBy(contradiction => contradiction.Detail, StringComparer.Ordinal)
                    .ToArray(),
                Uncertainties = candidate.Uncertainties
                    .OrderBy(uncertainty => uncertainty.Code)
                    .ThenBy(uncertainty => uncertainty.Detail, StringComparer.Ordinal)
                    .ToArray()
            }).ToArray();

        return report with { Candidates = candidates };
    }
}
