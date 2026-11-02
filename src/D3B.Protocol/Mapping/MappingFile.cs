using Google.Protobuf;

namespace D3B.Protocol.Mapping;

/// <summary>Where a client build's mapping is kept, and how it is written and read (D45).</summary>
/// <remarks>
/// Beside the build's pack and schema, one file per format, <c>mapping-&lt;format&gt;.binpb</c>: a serialized
/// <see cref="ProtocolMapping"/>. The format changes whenever <c>mapping.proto</c> or the readable protocol's contract
/// does, so that hosts of different versions of D3B keep the mappings they read side by side. It is derived from the
/// client, so it is never committed: it ships as an asset of the build's release (D24).
/// </remarks>
public static class MappingFile
{
    /// <summary>The format the mapping tool writes and the host reads.</summary>
    public const int FormatVersion = 15;

    /// <summary>Gets the file name of a mapping of <see cref="FormatVersion"/>.</summary>
    public static string FileName => $"mapping-{FormatVersion}.binpb";

    /// <summary>Gets the path of the mapping of a client build under a root directory, such as where packs are kept.</summary>
    /// <param name="root">The directory, one subdirectory per client version.</param>
    /// <param name="clientVersion">The build's version, as its version file names it.</param>
    public static string PathOf(string root, string clientVersion)
    {
        return Path.Combine(root, clientVersion, FileName);
    }

    /// <summary>Writes a mapping beside its path, then moves it there, so a mapping is never half written under its name.</summary>
    /// <returns>The bytes written.</returns>
    public static byte[] Write(string path, ProtocolMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var bytes = mapping.ToByteArray();
        var temporary = path + ".tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, true);

        return bytes;
    }

    /// <summary>Reads a mapping.</summary>
    /// <exception cref="InvalidProtocolBufferException">The file is not a mapping.</exception>
    public static ProtocolMapping Read(string path)
    {
        return ProtocolMapping.Parser.ParseFrom(File.ReadAllBytes(path));
    }
}
