using System.Security;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightManifestStore
{
    public static PreflightManifest Read(string path)
    {
        try
        {
            var status = PreflightUnix.Stat(path);

            if (status.Type != PreflightUnix.RegularFileType || status.Size > 67_108_864)
            {
                throw new PreflightBlockedException("manifest_file_invalid");
            }

            byte[] bytes = File.ReadAllBytes(path);
            return PreflightManifestCodec.Parse(bytes);
        }
        catch (PreflightBlockedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            throw new PreflightBlockedException("manifest_unreadable");
        }
    }

    public static void WriteAtomically(string path, PreflightManifest manifest)
    {
        PreflightUnix.EnsureLinux();
        string fullPath = PreflightPath.FullPath(path);
        PreflightPath.EnsureNoRepositoryMarker(fullPath);
        PreflightManifestCodec.Validate(manifest);
        byte[] bytes = PreflightManifestCodec.Serialize(manifest);
        string parent = Path.GetDirectoryName(fullPath) ?? throw new IOException("The manifest parent is unavailable.");
        EnsurePrivateParent(parent);
        string temporaryPath = Path.Combine(parent, ".preflight-" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            };

            using (var stream = new FileStream(temporaryPath, options))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }

            PreflightUnix.SetPrivateFileMode(temporaryPath);
            File.Move(temporaryPath, fullPath, true);
            PreflightUnix.SetPrivateFileMode(fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void EnsurePrivateParent(string parent)
    {
        bool existed = Directory.Exists(parent);

        if (!existed)
        {
            Directory.CreateDirectory(parent);
            PreflightUnix.SetPrivateDirectoryMode(parent);
        }

        var status = PreflightUnix.Stat(parent);

        if (status.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("manifest_parent_invalid");
        }

        UnixFileMode mode = File.GetUnixFileMode(parent);
        UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        if (mode != expected)
        {
            throw new PreflightBlockedException("manifest_parent_not_private");
        }
    }
}
