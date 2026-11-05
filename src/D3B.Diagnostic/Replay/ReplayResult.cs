using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Replay;

/// <summary>The validated result of one replay run.</summary>
public sealed record ReplayResult(
    DiagnosticEngineHello EngineHello,
    IReadOnlyList<DiagnosticAnalysis> Analyses,
    DiagnosticSummary Summary);
