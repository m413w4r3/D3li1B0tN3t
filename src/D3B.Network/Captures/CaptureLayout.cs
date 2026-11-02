namespace D3B.Network.Captures;

/// <summary>The layout of a capture file, which <see cref="CaptureWriter"/> writes and <see cref="CaptureReader"/> reads.</summary>
/// <remarks>
/// <para>
/// A header: the magic <c>D3B-CAPT</c>, the format version as a little-endian <c>int32</c>, when it started as Unix
/// milliseconds in a little-endian <c>int64</c>, then the client's version, the listener, the server's host and the server,
/// each a UTF-8 string after its 7-bit encoded length.
/// </para>
/// <para>
/// Then records, appended as the connection goes: a <see cref="CaptureRecordKind"/> byte, the microseconds since the
/// start as a 7-bit encoded <c>int64</c>, and for bytes a side sent, their count as a 7-bit encoded <c>int32</c> and the
/// bytes. Nothing follows the last record, so a capture whose writing was cut short reads up to its last whole record.
/// </para>
/// <para>
/// Format 2 adds <see cref="CaptureRecordKind.Injected"/>, the frames D3B sends the server itself: the server's bytes are
/// then the client's and those, in the order of their records. A format 1 capture has none, and reads as it did.
/// </para>
/// </remarks>
internal static class CaptureLayout
{
    public const int FormatVersion = 2;

    /// <summary>The oldest format this version of D3B still reads.</summary>
    public const int OldestFormatVersion = 1;

    /// <summary>The most bytes one record holds, a bound that only a damaged file reaches.</summary>
    public const int MaxRecordLength = 64 * 1024 * 1024;

    public static ReadOnlySpan<byte> Magic => "D3B-CAPT"u8;
}
