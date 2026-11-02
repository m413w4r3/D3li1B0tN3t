namespace D3B.Protocol.Mapping;

/// <summary>Where a client build's obfuscated schema is kept: beside the build's pack and mapping, one file per schema format (D39).</summary>
/// <remarks>
/// The schema is a <c>FileDescriptorSet</c>, protobuf's own description of protobuf files, serialized, which
/// <c>scripts/schema.cs</c> writes and the host audits a mapping against. It is derived from the client, so it is never
/// committed: it ships as an asset of the build's release, as packs do (D24).
/// </remarks>
public static class SchemaFile
{
    /// <summary>The format <c>scripts/schema.cs</c> writes. It changes whenever the tool would describe a client published before otherwise.</summary>
    public const int FormatVersion = 1;

    /// <summary>Gets the file name of a schema of <see cref="FormatVersion"/>.</summary>
    public static string FileName => $"schema-{FormatVersion}.binpb";

    /// <summary>Gets the path of the schema of a client build under a root directory, such as where packs are kept.</summary>
    /// <param name="root">The directory, one subdirectory per client version.</param>
    /// <param name="clientVersion">The build's version, as its version file names it.</param>
    public static string PathOf(string root, string clientVersion)
    {
        return Path.Combine(root, clientVersion, FileName);
    }
}
