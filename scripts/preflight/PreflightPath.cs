using System.Text;

namespace D3B.Scripts.Preflight;

internal static class PreflightPath
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public static string FullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public static byte[] EncodePath(string path)
    {
        byte[] bytes = _strictUtf8.GetBytes(path);
        ValidateRelativePath(bytes);
        return bytes;
    }

    public static byte[] DecodePath(string base64)
    {
        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new PreflightBlockedException("manifest_path_invalid");
        }

        ValidateRelativePath(bytes);
        return bytes;
    }

    public static string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return _strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new PreflightBlockedException("path_encoding_unsupported");
        }
    }

    public static void ValidateRelativePath(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[0] == (byte)'/' || bytes.Contains((byte)0))
        {
            throw new PreflightBlockedException("manifest_path_invalid");
        }

        string value = DecodeUtf8(bytes);
        string[] segments = value.Split('/');

        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new PreflightBlockedException("manifest_path_invalid");
        }
    }

    public static bool IsWithin(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static void EnsureNoRepositoryMarker(string path)
    {
        string fullPath = FullPath(path);
        string? directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "D3B.slnx")))
            {
                throw new PreflightBlockedException("repository_output_forbidden");
            }

            directory = Directory.GetParent(directory)?.FullName;
        }
    }

    public static void EnsureOutsideTree(string root, string path)
    {
        string fullRoot = FullPath(root);
        string fullPath = FullPath(path);

        if (IsWithin(fullRoot, fullPath))
        {
            throw new PreflightBlockedException("output_inside_source_tree");
        }
    }
}
