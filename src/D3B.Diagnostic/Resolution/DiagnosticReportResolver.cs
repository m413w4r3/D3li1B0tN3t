using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Resolution;

/// <summary>Resolves a redacted diagnostic report against one exact build schema.</summary>
public sealed class DiagnosticReportResolver
{
    /// <summary>Resolves the report against read-only references and build pins.</summary>
    public static CandidateReport Resolve(
        DiagnosticSchema buildSchema,
        LoadedDiagnosticReport report,
        IEnumerable<ReferenceCorpus> references,
        IEnumerable<PriorMapping> pins)
    {
        ArgumentNullException.ThrowIfNull(buildSchema);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(pins);
        return new CandidateReport { ClientVersion = buildSchema.ClientVersion, SchemaSha256 = buildSchema.SchemaSha256 };
    }
}
