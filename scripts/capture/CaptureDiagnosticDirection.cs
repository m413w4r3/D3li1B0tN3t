namespace D3B.Scripts.Capture;

/// <summary>Read, byte, frame and close totals for one direction.</summary>
public sealed class CaptureDiagnosticDirection
{
    /// <summary>Gets the number of read records.</summary>
    public long Reads { get; init; }

    /// <summary>Gets the number of bytes in the read records.</summary>
    public long Bytes { get; init; }

    /// <summary>Gets the number of complete frames parsed from those bytes.</summary>
    public long Frames { get; init; }

    /// <summary>Gets the number of close records for the direction.</summary>
    public long Closed { get; init; }
}
