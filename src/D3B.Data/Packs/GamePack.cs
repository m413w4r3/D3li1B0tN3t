using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using MemoryPack;
using Microsoft.Win32.SafeHandles;
using ZstdSharp;

namespace D3B.Data.Packs;

/// <summary>An open pack: its index is read when it opens, and each entry only when asked for.</summary>
/// <remarks>
/// Reads are positional, so any number of threads may read entries at once. Nothing read is kept: callers that read an
/// entry more than once keep it themselves, or go through a <see cref="PackCache"/>.
/// </remarks>
public sealed class GamePack : IDisposable
{
    /// <summary>The format this version of D3B writes and reads. A pack of any other format is rejected.</summary>
    /// <remarks>It changes whenever a pack written before would be misread: a new layout, or a changed entry type.</remarks>
    public const int FormatVersion = 8;

    private readonly SafeFileHandle _file;
    private readonly string _path;
    private readonly Dictionary<string, PackEntry> _entries;
    private readonly ConcurrentBag<Decompressor> _decompressors = [];
    private bool _disposed;

    /// <summary>Gets what the pack was made from.</summary>
    public PackInfo Info { get; }

    /// <summary>Gets the names of the pack's entries, in the order they were written.</summary>
    public IReadOnlyList<string> EntryNames { get; }

    private GamePack(SafeFileHandle file, string path, PackIndex index)
    {
        _file = file;
        _path = path;
        _entries = index.Entries.ToDictionary(entry => entry.Name, StringComparer.Ordinal);
        Info = index.Info;
        EntryNames = [.. index.Entries.Select(entry => entry.Name)];
    }

    /// <summary>Opens a pack and reads its index.</summary>
    /// <param name="path">The pack's path.</param>
    /// <returns>The open pack.</returns>
    /// <exception cref="PackFormatException">The file is not a complete pack of <see cref="FormatVersion"/>.</exception>
    public static GamePack Open(string path)
    {
        var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);

        try
        {
            return new GamePack(file, path, ReadIndex(file, path));
        }
        catch
        {
            file.Dispose();

            throw;
        }
    }

    private static PackIndex ReadIndex(SafeFileHandle file, string path)
    {
        var length = RandomAccess.GetLength(file);

        if (length < PackLayout.HeaderLength + PackLayout.TrailerLength)
            throw new PackFormatException($"'{path}' is too short to be a pack.");

        Span<byte> header = stackalloc byte[PackLayout.HeaderLength];

        ReadExactly(file, header, 0, path);

        if (!header[..PackLayout.Magic.Length].SequenceEqual(PackLayout.Magic))
            throw new PackFormatException($"'{path}' is not a D3B pack.");

        var format = BinaryPrimitives.ReadInt32LittleEndian(header[PackLayout.Magic.Length..]);

        if (format != FormatVersion)
            throw new PackFormatException($"'{path}' is a pack of format {format}; this version of D3B reads format {FormatVersion}.");

        Span<byte> trailer = stackalloc byte[PackLayout.TrailerLength];

        ReadExactly(file, trailer, length - PackLayout.TrailerLength, path);

        if (!trailer[16..].SequenceEqual(PackLayout.Magic))
            throw new PackFormatException($"'{path}' has no index: its writing was cut short, or it is damaged.");

        var index = new PackEntry
        {
            Name = "index",
            Type = typeof(PackIndex).FullName!,
            Offset = BinaryPrimitives.ReadInt64LittleEndian(trailer),
            CompressedLength = BinaryPrimitives.ReadInt32LittleEndian(trailer[8..]),
            Length = BinaryPrimitives.ReadInt32LittleEndian(trailer[12..]),
        };
        var entriesEnd = length - PackLayout.TrailerLength;

        CheckBounds(index, PackLayout.HeaderLength, entriesEnd, path);

        using var decompressor = new Decompressor();

        var read = Decode<PackIndex>(file, index, decompressor, path);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in read.Entries)
        {
            if (!names.Add(entry.Name))
                throw new PackFormatException($"'{path}' holds two entries named '{entry.Name}'.");

            CheckBounds(entry, PackLayout.HeaderLength, index.Offset, path);
        }

        return read;
    }

    private static void CheckBounds(PackEntry entry, long start, long end, string path)
    {
        if (entry.Offset < start || entry.CompressedLength <= 0 || entry.Length < 0 || entry.Offset > end - entry.CompressedLength)
            throw new PackFormatException($"'{path}': the {entry.Name} entry lies outside the pack.");
    }

    private static T Decode<T>(SafeFileHandle file, PackEntry entry, Decompressor decompressor, string path)
    {
        var compressed = ArrayPool<byte>.Shared.Rent(entry.CompressedLength);
        var decompressed = ArrayPool<byte>.Shared.Rent(entry.Length);

        try
        {
            ReadExactly(file, compressed.AsSpan(0, entry.CompressedLength), entry.Offset, path);

            int length;

            try
            {
                length = decompressor.Unwrap(compressed.AsSpan(0, entry.CompressedLength), decompressed.AsSpan(0, entry.Length));
            }
            catch (ZstdException exception)
            {
                throw new PackFormatException($"'{path}': the {entry.Name} entry is damaged.", exception);
            }

            if (length != entry.Length)
                throw new PackFormatException($"'{path}': the {entry.Name} entry holds {length} bytes, where its index says {entry.Length}.");

            try
            {
                return MemoryPackSerializer.Deserialize<T>(decompressed.AsSpan(0, length))
                    ?? throw new PackFormatException($"'{path}': the {entry.Name} entry is empty.");
            }
            catch (Exception exception) when (exception is MemoryPackSerializationException or ArgumentException)
            {
                // ArgumentException comes from the constructors of entry types, which check what they are given.
                throw new PackFormatException($"'{path}': the {entry.Name} entry is not a valid {typeof(T).Name}.", exception);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressed);
            ArrayPool<byte>.Shared.Return(decompressed);
        }
    }

    private static void ReadExactly(SafeFileHandle file, Span<byte> buffer, long offset, string path)
    {
        while (buffer.Length > 0)
        {
            var read = RandomAccess.Read(file, buffer, offset);

            if (read is 0)
                throw new PackFormatException($"'{path}' ends before its entries do.");

            buffer = buffer[read..];
            offset += read;
        }
    }

    /// <summary>Tells whether the pack holds an entry.</summary>
    /// <param name="name">The entry's name.</param>
    /// <returns>Whether it is there.</returns>
    public bool Contains(string name)
    {
        return _entries.ContainsKey(name);
    }

    /// <summary>Reads, decompresses and deserializes an entry.</summary>
    /// <typeparam name="T">The type the entry was written as.</typeparam>
    /// <param name="name">The entry's name.</param>
    /// <returns>The entry, a new instance at each call.</returns>
    /// <exception cref="KeyNotFoundException">The pack holds no entry of that name.</exception>
    /// <exception cref="PackFormatException">The entry was written as another type, or it is damaged.</exception>
    public T Read<T>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entry = Entry(name);

        if (entry.Type != typeof(T).FullName)
            throw new PackFormatException($"'{_path}': the {name} entry holds a {entry.Type}, not a {typeof(T).FullName}.");

        if (!_decompressors.TryTake(out var decompressor))
            decompressor = new Decompressor();

        try
        {
            return Decode<T>(_file, entry, decompressor, _path);
        }
        finally
        {
            _decompressors.Add(decompressor);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _file.Dispose();

        while (_decompressors.TryTake(out var decompressor))
            decompressor.Dispose();
    }

    /// <summary>Gets the length of an entry once decompressed, which a cache weighs it by.</summary>
    internal int LengthOf(string name)
    {
        return Entry(name).Length;
    }

    /// <summary>Gets where an entry lies in the pack.</summary>
    internal PackEntry Entry(string name)
    {
        return _entries.TryGetValue(name, out var entry)
            ? entry
            : throw new KeyNotFoundException($"'{_path}' holds no entry named '{name}'.");
    }
}
