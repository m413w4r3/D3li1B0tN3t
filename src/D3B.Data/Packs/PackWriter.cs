using System.Buffers.Binary;
using System.Collections.Concurrent;
using MemoryPack;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace D3B.Data.Packs;

/// <summary>Writes a pack: entries one after another, then the index once <see cref="Complete"/> is called.</summary>
/// <remarks>
/// <para>
/// Entries are serialized as they are added, then compressed several at once, each as its own zstd frame on one
/// thread, and written in the order they were added. The same entries, added in the same order at the same level,
/// therefore give the same bytes whatever the parallelism: a pack can be compared with an earlier extraction by its
/// hash (D25, D28).
/// </para>
/// <para>
/// One thread adds entries; compression runs on the thread pool. <see cref="Add"/> waits once enough entries are
/// pending, so that memory stays bounded.
/// </para>
/// </remarks>
public sealed class PackWriter : IDisposable
{
    /// <summary>The zstd level used when none is given: packs are written once per build and read many times.</summary>
    public const int DefaultCompressionLevel = 19;

    private readonly Stream _output;
    private readonly PackInfo _info;
    private readonly int _level;
    private readonly int _window;
    private readonly ConcurrentBag<Compressor> _compressors = [];
    private readonly Queue<PendingEntry> _pending = new();
    private readonly List<PackEntry> _entries = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private long _position;
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="PackWriter"/> class, and writes the pack's header.</summary>
    /// <param name="output">Where to write the pack. The writer does not dispose it.</param>
    /// <param name="info">What the pack is made from.</param>
    /// <param name="compressionLevel">The zstd level of every entry.</param>
    /// <param name="maxParallelism">How many entries may be compressed at once; by default, one per processor.</param>
    public PackWriter(Stream output, PackInfo info, int compressionLevel = DefaultCompressionLevel, int maxParallelism = 0)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentOutOfRangeException.ThrowIfNegative(maxParallelism);

        _output = output;
        _info = info;
        _level = compressionLevel;

        // Entries wait to be written in order behind the one being compressed, so the window is wider than the
        // parallelism: a large entry does not stall every thread behind it.
        _window = 4 * (maxParallelism is 0 ? Environment.ProcessorCount : maxParallelism);

        // The level is checked here rather than on the first entry, where it would fail on a thread of the pool.
        _compressors.Add(NewCompressor());

        Span<byte> header = stackalloc byte[PackLayout.HeaderLength];

        PackLayout.Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[PackLayout.Magic.Length..], GamePack.FormatVersion);
        Write(header);
    }

    /// <summary>Adds an entry.</summary>
    /// <typeparam name="T">The type the entry is written as, and read back as.</typeparam>
    /// <param name="name">The entry's name, unique in the pack, such as <c>texts/fr</c>.</param>
    /// <param name="value">The entry, serialized before the call returns: it may change afterwards.</param>
    /// <exception cref="ArgumentException">The pack already holds an entry of that name.</exception>
    /// <exception cref="InvalidOperationException">The pack is complete.</exception>
    public void Add<T>(string name, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);

        if (_completed)
            throw new InvalidOperationException("The pack is complete: no entry can be added.");

        if (!_names.Add(name))
            throw new ArgumentException($"The pack already holds an entry named '{name}'.", nameof(name));

        var serialized = MemoryPackSerializer.Serialize(value);

        _pending.Enqueue(new PendingEntry
        {
            Name = name,
            Type = typeof(T).FullName!,
            Length = serialized.Length,
            Compressed = Task.Run(() => Compress(serialized)),
        });

        while (_pending.Count > _window)
            WriteOldest();
    }

    /// <summary>Writes the index and the trailer. A pack is not readable before.</summary>
    /// <exception cref="InvalidOperationException">The pack is already complete.</exception>
    public void Complete()
    {
        if (_completed)
            throw new InvalidOperationException("The pack is already complete.");

        _completed = true;

        while (_pending.Count > 0)
            WriteOldest();

        var index = MemoryPackSerializer.Serialize(new PackIndex { Info = _info, Entries = [.. _entries] });
        var compressed = Compress(index);
        var offset = _position;

        Write(compressed);

        Span<byte> trailer = stackalloc byte[PackLayout.TrailerLength];

        BinaryPrimitives.WriteInt64LittleEndian(trailer, offset);
        BinaryPrimitives.WriteInt32LittleEndian(trailer[8..], compressed.Length);
        BinaryPrimitives.WriteInt32LittleEndian(trailer[12..], index.Length);
        PackLayout.Magic.CopyTo(trailer[16..]);
        Write(trailer);
        _output.Flush();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // A pack abandoned midway may still be compressing: its compressors are released once nothing uses them.
        foreach (var pending in _pending)
        {
            try
            {
                pending.Compressed.Wait();
            }
            catch (AggregateException)
            {
                // The pack is abandoned: whatever made it so is the error its writer reports.
            }
        }

        _pending.Clear();

        while (_compressors.TryTake(out var compressor))
            compressor.Dispose();
    }

    private void WriteOldest()
    {
        var pending = _pending.Dequeue();
        var compressed = pending.Compressed.GetAwaiter().GetResult();

        _entries.Add(new PackEntry
        {
            Name = pending.Name,
            Type = pending.Type,
            Offset = _position,
            CompressedLength = compressed.Length,
            Length = pending.Length,
        });
        Write(compressed);
    }

    private byte[] Compress(byte[] source)
    {
        if (!_compressors.TryTake(out var compressor))
            compressor = NewCompressor();

        try
        {
            return compressor.Wrap(source).ToArray();
        }
        finally
        {
            _compressors.Add(compressor);
        }
    }

    private Compressor NewCompressor()
    {
        var compressor = new Compressor(_level);

        compressor.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 1);

        return compressor;
    }

    private void Write(ReadOnlySpan<byte> bytes)
    {
        _output.Write(bytes);
        _position += bytes.Length;
    }

    private sealed class PendingEntry
    {
        public required string Name { get; init; }

        public required string Type { get; init; }

        public required int Length { get; init; }

        public required Task<byte[]> Compressed { get; init; }
    }
}
