namespace D3B.Diagnostic.Contract;

/// <summary>Aggregate replay results.</summary>
public sealed record DiagnosticSummary
{
    /// <summary>The protocol discriminator.</summary>
    public string Kind { get; init; } = "summary";
    /// <summary>Supported protocol version.</summary>
    public int ProtocolVersion { get; init; }
    /// <summary>Client build version.</summary>
    public string ClientVersion { get; init; } = string.Empty;
    /// <summary>Source schema fingerprint.</summary>
    public string SchemaSha256 { get; init; } = string.Empty;
    /// <summary>Number of frames analyzed.</summary>
    public long Frames { get; init; }
    /// <summary>Counts by analysis status.</summary>
    public IReadOnlyDictionary<DiagnosticStatus, long> Statuses { get; init; } = new Dictionary<DiagnosticStatus, long>();
    /// <summary>Counts by problem code.</summary>
    public IReadOnlyDictionary<DiagnosticProblemCode, long> Problems { get; init; } = new Dictionary<DiagnosticProblemCode, long>();
    /// <summary>Number of dropped frames.</summary>
    public long Dropped { get; init; }
    /// <summary>Observation coverage.</summary>
    public string Coverage { get; init; } = "complete";
}
