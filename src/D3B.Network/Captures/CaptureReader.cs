using System.Text;

namespace D3B.Network.Captures;

/// <summary>Reads a capture that <see cref="CaptureWriter"/> wrote: its header, then its records in order.</summary>
public sealed class CaptureReader : IDisposable
{
    private readonly BinaryReader _reader;

    /// <summary>Gets what the capture says of its connection.</summary>
    public CaptureHeader Header { get; }

    /// <summary>Gets the capture's format: 1, or 2 once captures record the frames D3B sends the server itself.</summary>
    public int FormatVersion { get; }

    /// <summary>Gets a value indicating whether the last record was cut short, as when the host writing it was killed.</summary>
    /// <remarks>Known once <see cref="TryRead"/> has returned <see langword="false"/>.</remarks>
    public bool IsTruncated { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="CaptureReader"/> class, reading the header at once.</summary>
    /// <param name="stream">The capture; the reader closes it.</param>
    /// <exception cref="CaptureFormatException">The stream is not a capture of this format.</exception>
    public CaptureReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _reader = new BinaryReader(stream, Encoding.UTF8, false);

        try
        {
            (Header, FormatVersion) = ReadHeader(_reader);
        }
        catch (Exception exception) when (exception is IOException or FormatException or ArgumentOutOfRangeException)
        {
            _reader.Dispose();

            throw exception as CaptureFormatException ?? new CaptureFormatException("The capture's header is cut short or damaged.", exception);
        }
    }

    /// <summary>Opens a capture file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The reader, past the header.</returns>
    /// <exception cref="CaptureFormatException">The file is not a capture of this format.</exception>
    public static CaptureReader Open(string path)
    {
        // A capture being written is shared for reading, so that it can be read while its connection is still open.
        return new CaptureReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
    }

    /// <summary>Reads the next record.</summary>
    /// <param name="record">The record.</param>
    /// <returns><see langword="false"/> at the end of the capture, or at a record cut short (<see cref="IsTruncated"/>).</returns>
    /// <exception cref="CaptureFormatException">A record is of no known kind, or longer than any is.</exception>
    public bool TryRead(out CaptureRecord record)
    {
        record = default;

        var first = _reader.BaseStream.ReadByte();

        if (first < 0)
            return false;

        var kind = (CaptureRecordKind)first;

        if (!Enum.IsDefined(kind) || (kind is CaptureRecordKind.Injected && FormatVersion < 2))
            throw new CaptureFormatException($"A record is of kind {first}, which format {FormatVersion} does not have.");

        try
        {
            var offset = TimeSpan.FromMicroseconds(_reader.Read7BitEncodedInt64());
            var data = ReadOnlyMemory<byte>.Empty;

            if (kind is CaptureRecordKind.FromClient or CaptureRecordKind.FromServer or CaptureRecordKind.Injected)
            {
                var length = _reader.Read7BitEncodedInt();

                if (length is < 0 or > CaptureLayout.MaxRecordLength)
                    throw new CaptureFormatException($"A record claims {length:N0} bytes, more than any holds.");

                var bytes = _reader.ReadBytes(length);

                if (bytes.Length < length)
                    throw new EndOfStreamException();

                data = bytes;
            }

            record = new CaptureRecord(kind, offset, data);

            return true;
        }
        catch (EndOfStreamException)
        {
            IsTruncated = true;

            return false;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new CaptureFormatException("A record's offset or length is damaged.", exception);
        }
    }

    /// <summary>Reads every record left, in order.</summary>
    /// <returns>The records.</returns>
    /// <exception cref="CaptureFormatException">A record is damaged.</exception>
    public IEnumerable<CaptureRecord> ReadAll()
    {
        while (TryRead(out var record))
            yield return record;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _reader.Dispose();
    }

    private static (CaptureHeader Header, int FormatVersion) ReadHeader(BinaryReader reader)
    {
        Span<byte> magic = stackalloc byte[CaptureLayout.Magic.Length];

        reader.BaseStream.ReadExactly(magic);

        if (!magic.SequenceEqual(CaptureLayout.Magic))
            throw new CaptureFormatException("The file is not a D3B capture.");

        var version = reader.ReadInt32();

        if (version is < CaptureLayout.OldestFormatVersion or > CaptureLayout.FormatVersion)
            throw new CaptureFormatException($"The capture is of format {version}; this version of D3B reads formats {CaptureLayout.OldestFormatVersion} to {CaptureLayout.FormatVersion}.");

        var header = new CaptureHeader
        {
            StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64()),
            ClientVersion = reader.ReadString(),
            Listener = reader.ReadString(),
            ServerHost = reader.ReadString(),
            Server = reader.ReadString(),
        };

        return (header, version);
    }
}
