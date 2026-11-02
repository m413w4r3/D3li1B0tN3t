namespace D3B.Scripts.Capture;

/// <summary>A complete game frame and the envelope message it carries, without any decoded field values.</summary>
internal sealed class GameMessage
{
    /// <summary>Gets the envelope member, such as request, response or event.</summary>
    public required string Member { get; init; }

    /// <summary>Gets the Any type URL exactly as it appeared in the frame, or null if the frame had none.</summary>
    public string? TypeUrl { get; init; }

    /// <summary>Gets the type name with the capture protocol's URL prefix removed.</summary>
    public string? TypeName { get; init; }

    /// <summary>Gets the Any message content for in-memory structural inspection.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Gets the record time at which the frame became complete.</summary>
    public TimeSpan Offset { get; init; }

    /// <summary>Gets the zero-based frame index within its side.</summary>
    public long FrameIndex { get; init; }

    /// <summary>Gets the zero-based capture record that completed the frame.</summary>
    public long RecordIndex { get; init; }

    /// <summary>Gets the byte offset of the frame within its side's byte stream.</summary>
    public long ByteOffset { get; init; }

    /// <summary>Gets the side: client, server or injected.</summary>
    public required string Side { get; init; }

    /// <summary>Gets the side's stable ordering when records share a timestamp.</summary>
    public int SideOrder { get; init; }

    /// <summary>Gets the zero-based message index after frames are merged by timestamp and side.</summary>
    public long MessageIndex { get; init; }
}
