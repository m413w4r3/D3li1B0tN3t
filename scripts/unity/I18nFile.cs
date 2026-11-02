using System.Text;

namespace D3B.Scripts.Unity;

/// <summary>A text file of the client, <c>Content/I18n/&lt;language&gt;.bin</c>: an Ankama format, not a Unity one.</summary>
/// <remarks>
/// Little-endian throughout. A language code, as a string prefixed by its 7-bit encoded length: <c>fr</c> in every file
/// whatever its language, perhaps the language the texts were written in. An <c>int32</c> count,
/// then that many pairs of an <c>int32</c> text identifier and an <c>int32</c> offset. A second <c>int32</c> count, then
/// that many pairs of a 64-bit key hash and an <c>int32</c> offset. Then the texts, each a UTF-8 string prefixed by its
/// 7-bit encoded length, which the offsets point to from the start of the file. Several entries may share a text.
/// </remarks>
internal sealed class I18nFile
{
    private readonly byte[] _data;

    /// <summary>The language code of the header, the same in every file: the file's own language is its name.</summary>
    public string HeaderLanguage { get; }

    /// <summary>The offset of each text identifier's text, in file order.</summary>
    public IReadOnlyList<(int Id, int Offset)> Texts { get; }

    /// <summary>The offset of each hashed key's text, in file order.</summary>
    public IReadOnlyList<(ulong Hash, int Offset)> Keys { get; }

    /// <summary>Where the texts start: the end of the two tables.</summary>
    public int TextsOffset { get; }

    public int Length => _data.Length;

    private I18nFile(byte[] data, string language, List<(int Id, int Offset)> texts, List<(ulong Hash, int Offset)> keys, int textsOffset)
    {
        _data = data;
        HeaderLanguage = language;
        Texts = texts;
        Keys = keys;
        TextsOffset = textsOffset;
    }

    public static I18nFile Read(string path)
    {
        var data = File.ReadAllBytes(path);

        using var reader = new BinaryReader(new MemoryStream(data, false), Encoding.UTF8);

        var language = reader.ReadString();
        var texts = new List<(int Id, int Offset)>(ReadCount(reader, sizeof(int) + sizeof(int)));

        for (var i = 0; i < texts.Capacity; i++)
            texts.Add((reader.ReadInt32(), reader.ReadInt32()));

        var keys = new List<(ulong Hash, int Offset)>(ReadCount(reader, sizeof(ulong) + sizeof(int)));

        for (var i = 0; i < keys.Capacity; i++)
            keys.Add((reader.ReadUInt64(), reader.ReadInt32()));

        return new I18nFile(data, language, texts, keys, checked((int)reader.BaseStream.Position));
    }

    private static int ReadCount(BinaryReader reader, int entrySize)
    {
        var count = reader.ReadInt32();
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;

        // A count is checked against the bytes left before anything is allocated for it.
        if (count < 0 || count > remaining / entrySize)
            throw new InvalidDataException($"A table claims {count} entries of {entrySize} bytes, with {remaining} bytes left.");

        return count;
    }

    /// <summary>Where each text starts, found by reading the texts one after another.</summary>
    /// <exception cref="InvalidDataException">The texts do not end exactly where the file does.</exception>
    public HashSet<int> TextStarts()
    {
        var starts = new HashSet<int>();

        using var reader = new BinaryReader(new MemoryStream(_data, false), Encoding.UTF8);

        reader.BaseStream.Position = TextsOffset;

        try
        {
            while (reader.BaseStream.Position < _data.Length)
            {
                starts.Add((int)reader.BaseStream.Position);
                reader.ReadString();
            }
        }
        catch (Exception exception) when (exception is IOException or FormatException)
        {
            throw new InvalidDataException($"The text at {starts.Max()} runs past the end of the file.", exception);
        }

        return starts;
    }

    /// <summary>The text at <paramref name="offset"/>.</summary>
    /// <exception cref="InvalidDataException">The offset does not point to a text.</exception>
    public string TextAt(int offset)
    {
        if (offset < TextsOffset || offset >= _data.Length)
            throw new InvalidDataException($"Offset {offset} is outside the texts, which span {TextsOffset} to {_data.Length}.");

        using var reader = new BinaryReader(new MemoryStream(_data, offset, _data.Length - offset, false), Encoding.UTF8);

        return reader.ReadString();
    }
}
