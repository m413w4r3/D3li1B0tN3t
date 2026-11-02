namespace D3B.Scripts.Capture;

/// <summary>Counts frames with the same envelope member and Any type URL.</summary>
public sealed class CaptureDiagnosticMessageCount
{
    /// <summary>Gets the envelope member.</summary>
    public required string Member { get; init; }

    /// <summary>Gets the full Any type URL, or <c>(none)</c> when the frame has no type URL.</summary>
    public required string TypeUrl { get; init; }

    /// <summary>Gets the number of frames with this member and type URL.</summary>
    public long Count { get; init; }
}
