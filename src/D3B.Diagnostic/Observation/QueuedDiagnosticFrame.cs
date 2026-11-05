using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Observation;

internal sealed record QueuedDiagnosticFrame(long Ticket, DiagnosticFrame Frame);
