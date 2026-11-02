using MemoryPack;

namespace D3B.Data.Icons;

/// <summary>Icons of one kind, such as items', professions' or spells', by the client's icon identifier: their pixels, ready to draw.</summary>
/// <remarks>
/// <para>
/// Every icon of a table is <see cref="Size"/> pixels square, a size each table has of its own, 8 bits a channel in the
/// order red, green, blue, alpha, the alpha straight rather than premultiplied, rows from the top: decoded from the
/// client's BC7 textures by the extraction tool, so that nothing reading the pack needs a decoder (D126).
/// </para>
/// <para>
/// The icons' identifiers are sorted, and their pixels follow one another in one array, <see cref="IconLength"/> bytes
/// each, in the same order: MemoryPack copies both as raw bytes, and the zstd frame of the pack's entry compresses them.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class IconTable
{
    /// <summary>Gets the width and height of every icon of the table, in pixels.</summary>
    public int Size { get; }

    /// <summary>Gets the icons' identifiers, in ascending order.</summary>
    public ReadOnlyMemory<int> Ids { get; }

    /// <summary>Gets the icons' pixels, <see cref="IconLength"/> bytes each, in the order of <see cref="Ids"/>.</summary>
    public ReadOnlyMemory<byte> Pixels { get; }

    /// <summary>Gets the bytes of one icon's pixels: <see cref="Size"/> rows of <see cref="Size"/> pixels, 4 bytes each.</summary>
    [MemoryPackIgnore]
    public int IconLength => Size * Size * 4;

    /// <summary>Gets how many icons the table holds.</summary>
    [MemoryPackIgnore]
    public int Count => Ids.Length;

    /// <summary>Initializes a new instance of the <see cref="IconTable"/> class.</summary>
    /// <param name="size">The width and height of every icon, in pixels.</param>
    /// <param name="ids">The icons' identifiers, in strictly ascending order.</param>
    /// <param name="pixels">Their pixels, <paramref name="size"/> squared times 4 bytes each, in the same order.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive, or an icon of that size has more bytes than an array holds.</exception>
    /// <exception cref="ArgumentException">The identifiers are not in strictly ascending order, or the pixels are not those of as many icons.</exception>
    [MemoryPackConstructor]
    public IconTable(int size, ReadOnlyMemory<int> ids, ReadOnlyMemory<byte> pixels)
    {
        // An icon is a slice of the pixels, its bytes counted in an int (IconLength).
        if (size <= 0 || (long)size * size * 4 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(size), size, "An icon is at least 1 pixel square, and its bytes fit an array.");

        Tables.CheckAscending(ids.Span, static id => id, nameof(ids));

        var length = (long)ids.Length * (size * size * 4);

        if (pixels.Length != length)
            throw new ArgumentException($"{ids.Length} icons of {size} pixels square take {length} bytes of pixels, not {pixels.Length}.", nameof(pixels));

        Size = size;
        Ids = ids;
        Pixels = pixels;
    }

    /// <summary>Finds an icon's pixels.</summary>
    /// <param name="iconId">The icon's identifier, as an item, a job or a spell names it.</param>
    /// <param name="pixels">Its <see cref="IconLength"/> bytes of pixels, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGet(int iconId, out ReadOnlyMemory<byte> pixels)
    {
        var index = Ids.Span.BinarySearch(iconId);

        if (index < 0)
        {
            pixels = default;

            return false;
        }

        pixels = Pixels.Slice(index * IconLength, IconLength);

        return true;
    }
}
