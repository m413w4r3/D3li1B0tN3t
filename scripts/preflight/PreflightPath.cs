using Microsoft.Win32.SafeHandles;
using System.Runtime.Versioning;
using System.Text;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightPath
{
    public static string FullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public static byte[] EncodePath(string path)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(path);
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

        if (Convert.ToBase64String(bytes) != base64)
        {
            throw new PreflightBlockedException("manifest_path_invalid");
        }

        ValidateRelativePath(bytes);
        return bytes;
    }

    public static void ValidateRelativePath(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[0] == (byte)'/' || bytes.Contains((byte)0))
        {
            throw new PreflightBlockedException("manifest_path_invalid");
        }

        int segmentStart = 0;

        for (int index = 0; index <= bytes.Length; index++)
        {
            if (index < bytes.Length && bytes[index] != (byte)'/')
            {
                continue;
            }

            int segmentLength = index - segmentStart;

            if (segmentLength == 0
                || segmentLength == 1 && bytes[segmentStart] == (byte)'.'
                || segmentLength == 2 && bytes[segmentStart] == (byte)'.' && bytes[segmentStart + 1] == (byte)'.')
            {
                throw new PreflightBlockedException("manifest_path_invalid");
            }

            segmentStart = index + 1;
        }
    }

    public static List<byte[]> SplitRelativePath(ReadOnlySpan<byte> path)
    {
        ValidateRelativePath(path);
        var segments = new List<byte[]>();
        int segmentStart = 0;

        for (int index = 0; index <= path.Length; index++)
        {
            if (index < path.Length && path[index] != (byte)'/')
            {
                continue;
            }

            segments.Add(path[segmentStart..index].ToArray());
            segmentStart = index + 1;
        }

        return segments;
    }

    public static byte[] JoinRelativePath(ReadOnlySpan<byte> parent, ReadOnlySpan<byte> name)
    {
        var result = new byte[parent.Length + (parent.Length == 0 ? 0 : 1) + name.Length];
        parent.CopyTo(result);
        int offset = parent.Length;

        if (offset != 0)
        {
            result[offset++] = (byte)'/';
        }

        name.CopyTo(result.AsSpan(offset));
        return result;
    }

    public static void EnsureNoRepositoryMarker(string path)
    {
        string fullPath = FullPath(path);
        string checkPath = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath) ?? "/";

        using SafeFileHandle check = PreflightUnix.OpenDirectoryPath(checkPath);
        PreflightUnix.EnsureNoRepositoryMarker(check);
    }

    public static void EnsureOutsideTree(string root, string path)
    {
        using SafeFileHandle source = PreflightUnix.OpenDirectoryPath(root);
        PreflightDirectoryIndex sourceIndex = PreflightDirectoryIndex.Capture(source);
        string parentPath = Path.GetDirectoryName(FullPath(path)) ?? "/";
        using SafeFileHandle destinationParent = PreflightUnix.OpenDirectoryPath(parentPath);
        PreflightUnix.EnsureOutsideTree(destinationParent, sourceIndex);
    }
}
