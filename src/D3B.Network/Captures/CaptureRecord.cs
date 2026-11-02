namespace D3B.Network.Captures;

/// <summary>A record of a capture: bytes one side sent, or that side closing, and when.</summary>
/// <param name="Kind">What the record holds.</param>
/// <param name="Offset">When it happened, from the start of the capture.</param>
/// <param name="Data">The bytes, empty when a side closed.</param>
public readonly record struct CaptureRecord(CaptureRecordKind Kind, TimeSpan Offset, ReadOnlyMemory<byte> Data);
