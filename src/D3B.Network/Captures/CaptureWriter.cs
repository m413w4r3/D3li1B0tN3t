using System.Buffers;
using System.Text;

namespace D3B.Network.Captures;

/// <summary>Records the raw bytes of a relayed connection, both ways, read by read, and the frames D3B sends the server itself, into a capture (D37).</summary>
/// <remarks>
/// <para>
/// Each record is flushed as it is written, so a capture whose host was killed still reads up to its last record. The two
/// directions of a connection write from their own tasks: a lock keeps their records whole and in order.
/// </para>
/// <para>
/// A write that fails, such as on a full disk, ends the capture there and sets <see cref="Failure"/>; later records are
/// dropped. The relay goes on: a capture is for reading a session again, never a reason to cut a player off.
/// </para>
/// <para>
/// A capture holds everything the client and the server sent, its account's login ticket among them. It stays on this
/// machine and is never committed or shared.
/// </para>
/// </remarks>
public sealed class CaptureWriter : IDisposable
{
    /// <summary>The extension of capture files.</summary>
    public const string FileExtension = ".d3bcap";

    private readonly Lock _lock = new();
    private readonly BinaryWriter _writer;
    private readonly TimeProvider _time;
    private readonly long _start;
    private bool _disposed;

    /// <summary>Gets the error that ended the capture early, or null while it records.</summary>
    public IOException? Failure { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="CaptureWriter"/> class, writing the header at once.</summary>
    /// <param name="stream">Where to write the capture; the writer closes it.</param>
    /// <param name="header">What the capture says of its connection.</param>
    /// <param name="time">The clock that times the records.</param>
    public CaptureWriter(Stream stream, CaptureHeader header, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(time);

        _writer = new BinaryWriter(stream, Encoding.UTF8, false);
        _time = time;

        _writer.Write(CaptureLayout.Magic);
        _writer.Write(CaptureLayout.FormatVersion);
        _writer.Write(header.StartedAt.ToUnixTimeMilliseconds());
        _writer.Write(header.ClientVersion);
        _writer.Write(header.Listener);
        _writer.Write(header.ServerHost);
        _writer.Write(header.Server);
        _writer.Flush();

        _start = time.GetTimestamp();
    }

    /// <summary>Creates a capture file in a directory, which is created when it does not exist.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file's name, without its extension.</param>
    /// <param name="header">What the capture says of its connection.</param>
    /// <param name="time">The clock that times the records.</param>
    /// <returns>The writer, and the file's path.</returns>
    public static (CaptureWriter Writer, string Path) Create(string directory, string name, CaptureHeader header, TimeProvider time)
    {
        Directory.CreateDirectory(directory);

        var path = System.IO.Path.Combine(directory, name + FileExtension);
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

        try
        {
            return (new CaptureWriter(stream, header, time), path);
        }
        catch
        {
            stream.Dispose();

            throw;
        }
    }

    /// <summary>Records bytes one side sent, or a frame D3B sent the server itself.</summary>
    /// <param name="kind"><see cref="CaptureRecordKind.FromClient"/>, <see cref="CaptureRecordKind.FromServer"/> or <see cref="CaptureRecordKind.Injected"/>.</param>
    /// <param name="data">The bytes, as one read gave them, or the frame D3B sent with its length.</param>
    public void Write(CaptureRecordKind kind, ReadOnlySequence<byte> data)
    {
        if (kind is not (CaptureRecordKind.FromClient or CaptureRecordKind.FromServer or CaptureRecordKind.Injected))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only bytes a side sent have data.");

        if (data.Length > CaptureLayout.MaxRecordLength)
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, $"A record holds at most {CaptureLayout.MaxRecordLength:N0} bytes.");

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Failure is not null)
                return;

            try
            {
                WriteStart(kind);
                _writer.Write7BitEncodedInt((int)data.Length);

                foreach (var segment in data)
                    _writer.Write(segment.Span);

                _writer.Flush();
            }
            catch (IOException exception)
            {
                Failure = exception;
            }
        }
    }

    /// <summary>Records a side closing.</summary>
    /// <param name="kind"><see cref="CaptureRecordKind.ClientClosed"/> or <see cref="CaptureRecordKind.ServerClosed"/>.</param>
    public void WriteClosed(CaptureRecordKind kind)
    {
        if (kind is not (CaptureRecordKind.ClientClosed or CaptureRecordKind.ServerClosed))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a side closing has no data.");

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Failure is not null)
                return;

            try
            {
                WriteStart(kind);
                _writer.Flush();
            }
            catch (IOException exception)
            {
                Failure = exception;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _writer.Dispose();
            }
            catch (IOException exception)
            {
                // Closing flushes nothing new: every record was flushed as written, so only the failure is worth keeping.
                Failure ??= exception;
            }
        }
    }

    private void WriteStart(CaptureRecordKind kind)
    {
        var offset = _time.GetElapsedTime(_start);

        _writer.Write((byte)kind);
        _writer.Write7BitEncodedInt64(offset.Ticks / TimeSpan.TicksPerMicrosecond);
    }
}
