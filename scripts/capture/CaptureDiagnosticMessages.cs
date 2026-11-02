namespace D3B.Scripts.Capture;

/// <summary>Counts game frames and the messages identified inside them.</summary>
public sealed class CaptureDiagnosticMessages
{
    /// <summary>Gets the total number of complete game frames.</summary>
    public long Total { get; init; }

    /// <summary>Gets frame counts keyed by envelope member.</summary>
    public required IReadOnlyDictionary<string, long> ByMember { get; init; }

    /// <summary>Gets frame counts keyed by side: client, server or injected.</summary>
    public required IReadOnlyDictionary<string, long> BySide { get; init; }

    /// <summary>Gets frame counts keyed by the full Any type URL, or <c>(none)</c>.</summary>
    public required IReadOnlyDictionary<string, long> ByTypeUrl { get; init; }

    /// <summary>Gets counts grouped by both envelope member and Any type URL.</summary>
    public required IReadOnlyList<CaptureDiagnosticMessageCount> ByMemberAndTypeUrl { get; init; }

    /// <summary>Gets the number of message frames found in the provided schema.</summary>
    public long Known { get; init; }

    /// <summary>Gets the number of message frames whose type URL is absent from the provided schema.</summary>
    public long Unknown { get; init; }

    /// <summary>Gets the number of message frames present in the provided mapping.</summary>
    public long Mapped { get; init; }

    /// <summary>Gets the number of message frames absent from the provided mapping.</summary>
    public long Unmapped { get; init; }
}
