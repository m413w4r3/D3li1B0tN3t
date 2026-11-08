using Microsoft.Win32.SafeHandles;
using System.Security;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightManifestStore
{
    private const int MaximumManifestBytes = 67_108_864;
    private const int PrivateFileMode = 0x180;

    public static PreflightManifest Read(string path, SafeFileHandle? sourceRoot = null)
    {
        try
        {
            string fullPath = PreflightPath.FullPath(path);
            string parentPath = Path.GetDirectoryName(fullPath) ?? "/";
            string fileName = Path.GetFileName(fullPath);
            using SafeFileHandle parent = PreflightUnix.OpenDirectoryPath(parentPath);

            if (sourceRoot is not null)
            {
                PreflightUnix.EnsureOutsideTree(parent, sourceRoot);
            }

            PreflightFileStatus before = PreflightUnix.StatAt(parent, PreflightUnix.EncodeName(fileName));

            if (before.Type != PreflightUnix.RegularFileType || before.Size > MaximumManifestBytes)
            {
                throw new PreflightBlockedException("manifest_file_invalid");
            }

            using SafeFileHandle file = PreflightUnix.OpenFileAt(parent, PreflightUnix.EncodeName(fileName));
            PreflightFileStatus opened = PreflightUnix.StatHandle(file);

            if (!PreflightUnix.IsSameObject(before, opened)
                || before.Size != opened.Size
                || opened.Type != PreflightUnix.RegularFileType)
            {
                throw new PreflightBlockedException("manifest_changed_during_read");
            }

            using var stream = new FileStream(file, FileAccess.Read, 131072, false);
            using var content = new MemoryStream((int)opened.Size);
            stream.CopyTo(content);

            if (content.Length != opened.Size || content.Length > MaximumManifestBytes)
            {
                throw new PreflightBlockedException("manifest_changed_during_read");
            }

            PreflightFileStatus after = PreflightUnix.StatHandle(file);

            if (after.Size != opened.Size
                || after.ChangeSeconds != opened.ChangeSeconds
                || after.ChangeNanoseconds != opened.ChangeNanoseconds)
            {
                throw new PreflightBlockedException("manifest_changed_during_read");
            }

            return PreflightManifestCodec.Parse(content.ToArray());
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

    public static void WriteAtomically(string path, PreflightManifest manifest, SafeFileHandle? sourceRoot = null)
    {
        PreflightUnix.EnsureLinux();
        PreflightManifestCodec.Validate(manifest);
        byte[] bytes = PreflightManifestCodec.Serialize(manifest);
        string fullPath = PreflightPath.FullPath(path);
        string parentPath = Path.GetDirectoryName(fullPath) ?? "/";
        string fileName = Path.GetFileName(fullPath);

        if (string.IsNullOrEmpty(fileName))
        {
            throw new PreflightBlockedException("manifest_path_invalid");
        }

        using SafeFileHandle parent = PreflightUnix.OpenDirectoryPath(
            parentPath,
            true,
            directory =>
            {
                PreflightUnix.EnsureNoRepositoryMarker(directory);

                if (sourceRoot is not null)
                {
                    PreflightUnix.EnsureOutsideTree(directory, sourceRoot);
                }
            });
        PreflightUnix.EnsurePrivateDirectory(parent);
        byte[] temporaryName = PreflightUnix.EncodeName(".preflight-" + Guid.NewGuid().ToString("N") + ".tmp");
        byte[] destinationName = PreflightUnix.EncodeName(fileName);

        try
        {
            using SafeFileHandle file = PreflightUnix.CreateFileAt(parent, temporaryName);
            using (var stream = new FileStream(file, FileAccess.Write, 131072, false))
            {
                stream.Write(bytes);
                stream.Flush(true);
                PreflightUnix.SetMode(file, PrivateFileMode);
                stream.Flush(true);
            }

            PreflightUnix.RenameAt(parent, temporaryName, parent, destinationName, true);
            PreflightUnix.SyncDirectory(parent);
        }
        catch
        {
            PreflightUnix.UnlinkAt(parent, temporaryName);
            throw;
        }
    }
}
