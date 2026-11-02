namespace D3B.Scripts.Capture;

/// <summary>Record and byte totals for a capture.</summary>
public sealed class CaptureDiagnosticSummary
{
    /// <summary>Gets the total number of whole records read.</summary>
    public long Records { get; init; }

    /// <summary>Gets record counts keyed by stable record-kind names.</summary>
    public required IReadOnlyDictionary<string, long> RecordsByKind { get; init; }

    /// <summary>Gets data byte counts keyed by stable record-kind names.</summary>
    public required IReadOnlyDictionary<string, long> BytesByKind { get; init; }

    /// <summary>Gets read, byte, frame and close totals for each direction.</summary>
    public required CaptureDiagnosticDirections Directions { get; init; }

    /// <summary>Gets counts and schema/mapping classification for complete game frames.</summary>
    public required CaptureDiagnosticMessages Messages { get; init; }

    /// <summary>Gets whether the last record was cut short.</summary>
    public bool Truncated { get; init; }
}
