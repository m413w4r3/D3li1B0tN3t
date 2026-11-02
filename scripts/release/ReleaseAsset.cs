using System.Security.Cryptography;
using D3B.Data.Releases;

namespace D3B.Scripts.Release;

/// <summary>A file made from a client build, to publish as an asset of its release with its SHA-256 beside it (D24).</summary>
internal sealed class ReleaseAsset
{
    /// <summary>Gets the asset's name, such as <c>data-3.pack</c>, which is also its file's.</summary>
    public string Name { get; }

    /// <summary>Gets the file's path.</summary>
    public string Path { get; }

    /// <summary>Gets the name of the asset holding its SHA-256.</summary>
    public string ChecksumName => ClientRelease.ChecksumNameOf(Name);

    /// <summary>Gets the path of the file holding its SHA-256, beside it.</summary>
    public string ChecksumPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, ChecksumName);

    /// <summary>Gets the file's SHA-256, in lowercase hexadecimal.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the file's length in bytes.</summary>
    public long Length { get; }

    /// <summary>Gets what the release's notes say of it, after its name and SHA-256.</summary>
    public string Provenance { get; }

    private ReleaseAsset(string path, string sha256, long length, string provenance)
    {
        Name = System.IO.Path.GetFileName(path);
        Path = path;
        Sha256 = sha256;
        Length = length;
        Provenance = provenance;
    }

    /// <summary>Hashes a file and writes its SHA-256 beside it, in the format the host reads (D31).</summary>
    /// <param name="path">The file, already checked.</param>
    /// <param name="provenance">What the release's notes say of it.</param>
    public static ReleaseAsset Hash(string path, string provenance)
    {
        byte[] hash;
        long length;

        using (var stream = File.OpenRead(path))
        {
            hash = SHA256.HashData(stream);
            length = stream.Length;
        }

        var asset = new ReleaseAsset(path, Convert.ToHexStringLower(hash), length, provenance);

        // Beside the file, outside the repository: it is uploaded as the asset's checksum, never committed.
        File.WriteAllText(asset.ChecksumPath, ClientRelease.FormatChecksum(hash, asset.Name));

        return asset;
    }
}
