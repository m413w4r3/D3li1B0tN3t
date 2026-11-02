namespace D3B.Scripts.Capture;

/// <summary>A capture or protocol framing observation with a stable code.</summary>
public sealed class CaptureDiagnostic
{
    /// <summary>Gets the stable diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the severity.</summary>
    public CaptureDiagnosticSeverity Severity { get; init; }

    /// <summary>Gets the processing stage.</summary>
    public CaptureDiagnosticStage Stage { get; init; }

    /// <summary>Gets the affected direction, or null when the issue is capture-wide.</summary>
    public string? Direction { get; init; }

    /// <summary>Gets the zero-based record index, when known.</summary>
    public long? RecordIndex { get; init; }

    /// <summary>Gets the zero-based frame index, when known.</summary>
    public long? FrameIndex { get; init; }

    /// <summary>Gets the byte offset within the capture or direction, when known.</summary>
    public long? ByteOffset { get; init; }

    /// <summary>Gets a human-readable message without exception stack information.</summary>
    public required string Message { get; init; }

    /// <summary>Gets small supplemental values, or null when none are needed.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; init; }
}
