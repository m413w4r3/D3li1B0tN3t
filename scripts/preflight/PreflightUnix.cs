using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightUnix
{
    private const int AtCurrentWorkingDirectory = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const uint StatxBasicStats = 0x7ff;
    private const int TypeMask = 0xf000;
    private const int RegularFile = 0x8000;
    private const int Directory = 0x4000;
    private const int SymbolicLink = 0xa000;
    private const int ModeMask = 0x0fff;

    public static (int Type, long Size, int Mode, ulong Inode, long ChangeSeconds, uint ChangeNanoseconds) Stat(string path)
    {
        EnsureLinux();
        var buffer = new byte[256];

        if (Statx(AtCurrentWorkingDirectory, ToNativePath(path), AtSymlinkNoFollow, StatxBasicStats, buffer) != 0)
        {
            throw new IOException("The filesystem entry could not be inspected.", Marshal.GetLastPInvokeError());
        }

        int mode = BitConverter.ToUInt16(buffer, 28);
        ulong size = BitConverter.ToUInt64(buffer, 40);
        ulong inode = BitConverter.ToUInt64(buffer, 32);
        long changeSeconds = BitConverter.ToInt64(buffer, 96);
        uint changeNanoseconds = BitConverter.ToUInt32(buffer, 104);

        if (size > long.MaxValue)
        {
            throw new IOException("The filesystem entry is too large.");
        }

        return (mode & TypeMask, (long)size, mode & ModeMask, inode, changeSeconds, changeNanoseconds);
    }

    public static byte[] ReadLink(string path)
    {
        EnsureLinux();
        var buffer = new byte[256];

        while (true)
        {
            nint length = ReadLinkNative(ToNativePath(path), buffer, (nuint)buffer.Length);

            if (length < 0)
            {
                throw new IOException("The symbolic link target could not be read.", Marshal.GetLastPInvokeError());
            }

            if (length < buffer.Length)
            {
                Array.Resize(ref buffer, (int)length);
                return buffer;
            }

            if (buffer.Length >= 1_048_576)
            {
                throw new IOException("The symbolic link target is too long.");
            }

            Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    public static void CreateSymbolicLink(string path, byte[] target)
    {
        EnsureLinux();

        if (target.Length == 0 || target.Contains((byte)0))
        {
            throw new PreflightBlockedException("link_target_invalid");
        }

        byte[] targetTerminated = AppendNull(target);
        byte[] pathTerminated = AppendNull(Encoding.UTF8.GetBytes(path));

        if (Symlink(targetTerminated, pathTerminated) != 0)
        {
            throw new IOException("The symbolic link could not be created.", Marshal.GetLastPInvokeError());
        }
    }

    public static void SetPrivateFileMode(string path)
    {
        EnsureLinux();
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static void SetPrivateDirectoryMode(string path)
    {
        EnsureLinux();
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Preflight requires Linux filesystem metadata.");
        }
    }

    public static int RegularFileType
    {
        get
        {
            return RegularFile;
        }
    }

    public static int DirectoryType
    {
        get
        {
            return Directory;
        }
    }

    public static int SymbolicLinkType
    {
        get
        {
            return SymbolicLink;
        }
    }

    private static byte[] AppendNull(byte[] bytes)
    {
        var result = new byte[bytes.Length + 1];
        bytes.CopyTo(result, 0);
        return result;
    }

    private static byte[] ToNativePath(string path)
    {
        return AppendNull(Encoding.UTF8.GetBytes(path));
    }

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(
        int directoryFileDescriptor,
        byte[] path,
        int flags,
        uint mask,
        [Out] byte[] buffer);

    [DllImport("libc", EntryPoint = "readlink", SetLastError = true)]
    private static extern nint ReadLinkNative(
        byte[] path,
        [Out] byte[] buffer,
        nuint bufferSize);

    [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
    private static extern int Symlink(byte[] target, byte[] linkPath);
}
