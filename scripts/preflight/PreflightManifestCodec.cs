using System.Text.Json;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightManifestCodec
{
    private const int SchemaVersion = 1;
    private const int MaximumManifestBytes = 67_108_864;

    private static readonly PreflightManifestJsonContext _jsonContext = new();

    public static byte[] Serialize(PreflightManifest manifest)
    {
        Validate(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(manifest, _jsonContext.PreflightManifest);
    }

    public static PreflightManifest Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumManifestBytes)
        {
            throw new PreflightBlockedException("manifest_size_invalid");
        }

        PreflightManifest manifest;

        try
        {
            manifest = JsonSerializer.Deserialize(bytes, _jsonContext.PreflightManifest)
                ?? throw new PreflightBlockedException("manifest_invalid");
        }
        catch (PreflightBlockedException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new PreflightBlockedException("manifest_invalid");
        }

        Validate(manifest);
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(manifest, _jsonContext.PreflightManifest);

        if (!bytes.SequenceEqual(canonical))
        {
            throw new PreflightBlockedException("manifest_not_canonical");
        }

        return manifest;
    }

    public static void Validate(PreflightManifest manifest)
    {
        if (manifest.SchemaVersion != SchemaVersion || manifest.Entries is null)
        {
            throw new PreflightBlockedException("manifest_version_or_entries_invalid");
        }

        byte[]? previousPath = null;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var nonDirectoryPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (PreflightManifestEntry entry in manifest.Entries)
        {
            if (entry is null || entry.PathBase64 is null || entry.Kind is null)
            {
                throw new PreflightBlockedException("manifest_entry_invalid");
            }

            byte[] path = PreflightPath.DecodePath(entry.PathBase64);
            if (!paths.Add(entry.PathBase64))
            {
                throw new PreflightBlockedException("manifest_duplicate_path");
            }

            if (previousPath is not null && previousPath.AsSpan().SequenceCompareTo(path) >= 0)
            {
                throw new PreflightBlockedException("manifest_path_order_invalid");
            }

            previousPath = path;

            if (entry.Size < 0 || entry.Mode < 0 || entry.Mode > 0x0fff)
            {
                throw new PreflightBlockedException("manifest_entry_metadata_invalid");
            }

            if (entry.Kind == "file")
            {
                if (entry.Sha256 is null || entry.Sha256.Length != 64 || !entry.Sha256.All(IsLowerHex) || entry.LinkTargetBase64 is not null)
                {
                    throw new PreflightBlockedException("manifest_file_invalid");
                }
            }
            else if (entry.Kind == "symlink")
            {
                if (entry.Sha256 is not null || entry.LinkTargetBase64 is null)
                {
                    throw new PreflightBlockedException("manifest_link_invalid");
                }

                byte[] linkTarget = DecodeBase64(entry.LinkTargetBase64);

                if (linkTarget.Length == 0 || linkTarget.Contains((byte)0) || entry.Size != linkTarget.Length)
                {
                    throw new PreflightBlockedException("manifest_link_invalid");
                }
            }
            else
            {
                throw new PreflightBlockedException("manifest_kind_invalid");
            }

            nonDirectoryPaths.Add(entry.PathBase64);
        }

        foreach (string encodedPath in nonDirectoryPaths)
        {
            byte[] path = PreflightPath.DecodePath(encodedPath);

            for (int index = 0; index < path.Length; index++)
            {
                if (path[index] == (byte)'/'
                    && nonDirectoryPaths.Contains(Convert.ToBase64String(path.AsSpan(0, index))))
                {
                    throw new PreflightBlockedException("manifest_path_conflict");
                }
            }
        }
    }

    private static byte[] DecodeBase64(string value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value);

            if (Convert.ToBase64String(bytes) != value)
            {
                throw new PreflightBlockedException("manifest_link_target_invalid");
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new PreflightBlockedException("manifest_link_target_invalid");
        }
    }

    private static bool IsLowerHex(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f';
    }
}
