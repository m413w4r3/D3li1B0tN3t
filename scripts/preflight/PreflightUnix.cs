using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightUnix
{
    private const int AtSymlinkNoFollow = 0x100;
    private const int AtEmptyPath = 0x1000;
    private const uint StatxBasicStats = 0x7ff;
    private const uint StatxMountId = 0x1000;
    private const int TypeMask = 0xf000;
    private const int RegularFile = 0x8000;
    private const int Directory = 0x4000;
    private const int SymbolicLink = 0xa000;
    private const int ModeMask = 0x0fff;
    private const int ReadOnly = 0;
    private const int WriteOnly = 1;
    private const int Create = 0x40;
    private const int Exclusive = 0x80;
    private const int NonBlocking = 0x800;
    private const int CloseOnExec = 0x80000;
    private const int NoFollow = 0x20000;
    private const int DirectoryFlag = 0x10000;
    private const int RenameNoReplace = 1;
    private const uint PrivateDirectoryMode = 0x1c0;
    private const uint PrivateFileMode = 0x180;
    private const int DirectoryNameOffset = 19;
    private const int MaximumLinkBytes = 1_048_576;

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

    public static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Preflight requires Linux filesystem metadata.");
        }
    }

    public static SafeFileHandle OpenDirectoryPath(
        string path,
        bool createMissing = false,
        Action<SafeFileHandle>? validateDirectory = null)
    {
        EnsureLinux();
        string fullPath = Path.GetFullPath(path);
        SafeFileHandle current = OpenDirectoryAtPathRoot();

        try
        {
            string[] segments = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                byte[] name = EncodeName(segment);
                validateDirectory?.Invoke(current);
                SafeFileHandle next;

                try
                {
                    next = OpenDirectoryAt(current, name);
                }
                catch (IOException) when (createMissing && Marshal.GetLastPInvokeError() == 2)
                {
                    try
                    {
                        CreateDirectoryAt(current, name);
                    }
                    catch (IOException) when (Marshal.GetLastPInvokeError() == 17)
                    {
                    }

                    next = OpenDirectoryAt(current, name);
                }

                current.Dispose();
                current = next;
            }

            validateDirectory?.Invoke(current);
            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    public static (SafeFileHandle Parent, byte[] Name, string NameText) OpenParentPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string name = Path.GetFileName(fullPath);

        if (string.IsNullOrEmpty(name) || name is "." or "..")
        {
            throw new PreflightBlockedException("path_final_component_invalid");
        }

        string parentPath = Path.GetDirectoryName(fullPath) ?? "/";
        return (OpenDirectoryPath(parentPath), EncodeName(name), name);
    }

    public static SafeFileHandle OpenDirectoryAt(SafeFileHandle parent, byte[] name)
    {
        int fileDescriptor = OpenAt(parent.DangerousGetHandle().ToInt32(), Terminate(name), ReadOnly | DirectoryFlag | NoFollow | CloseOnExec, 0);

        if (fileDescriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();

            if (error is 20 or 40)
            {
                throw new PreflightBlockedException("path_component_unsafe");
            }

            throw NativeIOException("The directory could not be opened without following links.");
        }

        return new SafeFileHandle(new IntPtr(fileDescriptor), true);
    }

    public static SafeFileHandle OpenFileAt(SafeFileHandle parent, byte[] name)
    {
        int fileDescriptor = OpenAt(parent.DangerousGetHandle().ToInt32(), Terminate(name), ReadOnly | NonBlocking | NoFollow | CloseOnExec, 0);

        if (fileDescriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();

            if (error == 40)
            {
                throw new PreflightBlockedException("path_component_unsafe");
            }

            throw NativeIOException("The file could not be opened without following links.");
        }

        return new SafeFileHandle(new IntPtr(fileDescriptor), true);
    }

    public static SafeFileHandle CreateFileAt(SafeFileHandle parent, byte[] name)
    {
        int fileDescriptor = OpenAt(parent.DangerousGetHandle().ToInt32(), Terminate(name), WriteOnly | Create | Exclusive | NoFollow | CloseOnExec, PrivateFileMode);

        if (fileDescriptor < 0)
        {
            throw NativeIOException("The private file could not be created.");
        }

        return new SafeFileHandle(new IntPtr(fileDescriptor), true);
    }

    public static PreflightFileStatus StatAt(SafeFileHandle parent, byte[] name)
    {
        return ReadStatus(parent.DangerousGetHandle().ToInt32(), Terminate(name), AtSymlinkNoFollow);
    }

    public static bool TryStatAt(SafeFileHandle parent, byte[] name, out PreflightFileStatus? status)
    {
        try
        {
            status = StatAt(parent, name);
            return true;
        }
        catch (IOException) when (Marshal.GetLastPInvokeError() == 2)
        {
            status = null;
            return false;
        }
    }

    public static PreflightFileStatus StatHandle(SafeFileHandle handle)
    {
        return ReadStatus(handle.DangerousGetHandle().ToInt32(), [0], AtEmptyPath | AtSymlinkNoFollow);
    }

    public static List<byte[]> EnumerateNames(SafeFileHandle directory)
    {
        int duplicate = Dup(directory.DangerousGetHandle().ToInt32());

        if (duplicate < 0)
        {
            throw NativeIOException("The directory could not be enumerated.");
        }

        nint stream = FdOpenDirectory(duplicate);

        if (stream == IntPtr.Zero)
        {
            if (Close(duplicate) != 0)
            {
                throw NativeIOException("The directory could not be enumerated.");
            }

            throw NativeIOException("The directory could not be enumerated.");
        }

        var names = new List<byte[]>();

        try
        {
            Marshal.WriteInt32(ErrnoLocation(), 0);

            while (true)
            {
                nint entry = ReadDirectory(stream);

                if (entry == IntPtr.Zero)
                {
                    int error = Marshal.ReadInt32(ErrnoLocation());

                    if (error != 0)
                    {
                        throw new IOException("The directory could not be enumerated.", error);
                    }

                    break;
                }

                var name = new List<byte>();

                for (int index = 0; ; index++)
                {
                    byte value = Marshal.ReadByte(entry, DirectoryNameOffset + index);

                    if (value == 0)
                    {
                        break;
                    }

                    name.Add(value);
                }

                if (name.Count > 0 && !(name.Count == 1 && name[0] == (byte)'.')
                    && !(name.Count == 2 && name[0] == (byte)'.' && name[1] == (byte)'.'))
                {
                    names.Add(name.ToArray());
                }
            }
        }
        finally
        {
            _ = CloseDirectory(stream);
        }

        names.Sort(CompareBytes);
        return names;
    }

    public static byte[] ReadLinkAt(SafeFileHandle parent, byte[] name)
    {
        var buffer = new byte[256];

        while (true)
        {
            nint length = ReadLinkAtNative(parent.DangerousGetHandle().ToInt32(), Terminate(name), buffer, (nuint)buffer.Length);

            if (length < 0)
            {
                throw NativeIOException("The symbolic link target could not be read.");
            }

            if (length < buffer.Length)
            {
                Array.Resize(ref buffer, (int)length);
                return buffer;
            }

            if (buffer.Length >= MaximumLinkBytes)
            {
                throw new IOException("The symbolic link target is too long.");
            }

            Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    public static void CreateDirectoryAt(SafeFileHandle parent, byte[] name)
    {
        if (MkdirAt(parent.DangerousGetHandle().ToInt32(), Terminate(name), PrivateDirectoryMode) != 0)
        {
            throw NativeIOException("The private directory could not be created.");
        }
    }

    public static void CreateSymbolicLinkAt(SafeFileHandle parent, byte[] name, byte[] target)
    {
        if (target.Length == 0 || target.Contains((byte)0))
        {
            throw new PreflightBlockedException("link_target_invalid");
        }

        if (SymlinkAt(Terminate(target), parent.DangerousGetHandle().ToInt32(), Terminate(name)) != 0)
        {
            throw NativeIOException("The symbolic link could not be created.");
        }
    }

    public static void SetMode(SafeFileHandle handle, int mode)
    {
        if (Fchmod(handle.DangerousGetHandle().ToInt32(), (uint)mode) != 0)
        {
            throw NativeIOException("The filesystem mode could not be set.");
        }
    }

    public static void RenameAt(
        SafeFileHandle sourceParent,
        byte[] sourceName,
        SafeFileHandle destinationParent,
        byte[] destinationName,
        bool replace = false)
    {
        if (RenameAt2(
                sourceParent.DangerousGetHandle().ToInt32(),
                Terminate(sourceName),
                destinationParent.DangerousGetHandle().ToInt32(),
                Terminate(destinationName),
                replace ? 0u : RenameNoReplace) != 0)
        {
            throw NativeIOException("The directory entry could not be renamed safely.");
        }
    }

    public static void UnlinkAt(SafeFileHandle parent, byte[] name)
    {
        if (UnlinkAtNative(parent.DangerousGetHandle().ToInt32(), Terminate(name), 0) != 0)
        {
            int error = Marshal.GetLastPInvokeError();

            if (error != 2)
            {
                throw new IOException("The temporary file could not be removed.", error);
            }
        }
    }

    public static void SyncDirectory(SafeFileHandle directory)
    {
        if (Fsync(directory.DangerousGetHandle().ToInt32()) != 0)
        {
            throw NativeIOException("The directory metadata could not be synchronized.");
        }
    }

    public static void EnsureSameMount(PreflightFileStatus entry, PreflightFileStatus root)
    {
        if (entry.MountId != root.MountId || entry.DeviceMajor != root.DeviceMajor || entry.DeviceMinor != root.DeviceMinor)
        {
            throw new PreflightBlockedException("mount_boundary_unsupported");
        }
    }

    public static bool IsSameObject(PreflightFileStatus left, PreflightFileStatus right)
    {
        return left.DeviceMajor == right.DeviceMajor && left.DeviceMinor == right.DeviceMinor && left.Inode == right.Inode;
    }

    public static SafeFileHandle DuplicateHandle(SafeFileHandle handle)
    {
        int duplicate = Dup(handle.DangerousGetHandle().ToInt32());

        if (duplicate < 0)
        {
            throw NativeIOException("The filesystem handle could not be duplicated.");
        }

        return new SafeFileHandle(new IntPtr(duplicate), true);
    }

    public static bool IsDescendantOrSelf(SafeFileHandle candidate, SafeFileHandle ancestor)
    {
        PreflightFileStatus ancestorStatus = StatHandle(ancestor);
        int duplicates = Dup(candidate.DangerousGetHandle().ToInt32());

        if (duplicates < 0)
        {
            throw NativeIOException("The directory ancestry could not be checked.");
        }

        SafeFileHandle current = new(new IntPtr(duplicates), true);

        try
        {
            while (true)
            {
                PreflightFileStatus currentStatus = StatHandle(current);

                if (IsSameObject(currentStatus, ancestorStatus))
                {
                    return true;
                }

                using SafeFileHandle parent = OpenDirectoryAt(current, [(byte)'.', (byte)'.']);
                PreflightFileStatus parentStatus = StatHandle(parent);

                if (IsSameObject(parentStatus, currentStatus))
                {
                    return false;
                }

                current.Dispose();
                int parentDuplicate = Dup(parent.DangerousGetHandle().ToInt32());

                if (parentDuplicate < 0)
                {
                    throw NativeIOException("The directory ancestry could not be checked.");
                }

                current = new SafeFileHandle(new IntPtr(parentDuplicate), true);
            }
        }
        finally
        {
            current.Dispose();
        }
    }

    public static void EnsureNoRepositoryMarker(SafeFileHandle directory)
    {
        WalkAncestors(directory, current =>
        {
            try
            {
                StatAt(current, Encoding.ASCII.GetBytes("D3B.slnx"));
                throw new PreflightBlockedException("repository_output_forbidden");
            }
            catch (IOException exception) when (Marshal.GetLastPInvokeError() == 2)
            {
                _ = exception;
            }
        });
    }

    public static void EnsureOutsideTree(SafeFileHandle directory, SafeFileHandle root)
    {
        if (IsDescendantOrSelf(directory, root))
        {
            throw new PreflightBlockedException("output_inside_source_tree");
        }
    }

    public static void EnsurePrivateDirectory(SafeFileHandle directory)
    {
        PreflightFileStatus status = StatHandle(directory);

        if (status.Type != Directory || status.Mode != (int)PrivateDirectoryMode)
        {
            throw new PreflightBlockedException("manifest_parent_not_private");
        }
    }

    public static byte[] EncodeName(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    public static int CompareBytes(byte[] left, byte[] right)
    {
        return left.AsSpan().SequenceCompareTo(right);
    }

    private static SafeFileHandle OpenDirectoryAtPathRoot()
    {
        int fileDescriptor = OpenAt(-100, [(byte)'/', 0], ReadOnly | DirectoryFlag | CloseOnExec, 0);

        if (fileDescriptor < 0)
        {
            throw NativeIOException("The filesystem root could not be opened.");
        }

        return new SafeFileHandle(new IntPtr(fileDescriptor), true);
    }

    private static PreflightFileStatus ReadStatus(int directoryFileDescriptor, byte[] path, int flags)
    {
        var buffer = new byte[256];

        if (Statx(directoryFileDescriptor, path, flags, StatxBasicStats | StatxMountId, buffer) != 0)
        {
            throw NativeIOException("The filesystem entry could not be inspected.");
        }

        if ((BitConverter.ToUInt32(buffer, 0) & StatxMountId) == 0)
        {
            throw new IOException("The filesystem does not report mount identity.");
        }

        int mode = BitConverter.ToUInt16(buffer, 28);
        ulong size = BitConverter.ToUInt64(buffer, 40);

        if (size > long.MaxValue)
        {
            throw new IOException("The filesystem entry is too large.");
        }

        return new PreflightFileStatus(
            mode & TypeMask,
            (long)size,
            mode & ModeMask,
            BitConverter.ToUInt64(buffer, 32),
            BitConverter.ToUInt32(buffer, 136),
            BitConverter.ToUInt32(buffer, 140),
            BitConverter.ToUInt64(buffer, 144),
            BitConverter.ToInt64(buffer, 96),
            BitConverter.ToUInt32(buffer, 104),
            BitConverter.ToInt64(buffer, 112),
            BitConverter.ToUInt32(buffer, 120));
    }

    private static void WalkAncestors(SafeFileHandle directory, Action<SafeFileHandle> action)
    {
        int descriptor = Dup(directory.DangerousGetHandle().ToInt32());

        if (descriptor < 0)
        {
            throw NativeIOException("The directory ancestry could not be checked.");
        }

        SafeFileHandle current = new(new IntPtr(descriptor), true);

        try
        {
            while (true)
            {
                action(current);
                PreflightFileStatus currentStatus = StatHandle(current);

                using SafeFileHandle parent = OpenDirectoryAt(current, [(byte)'.', (byte)'.']);
                PreflightFileStatus parentStatus = StatHandle(parent);

                if (IsSameObject(currentStatus, parentStatus))
                {
                    break;
                }

                int parentDescriptor = Dup(parent.DangerousGetHandle().ToInt32());

                if (parentDescriptor < 0)
                {
                    throw NativeIOException("The directory ancestry could not be checked.");
                }

                current.Dispose();
                current = new SafeFileHandle(new IntPtr(parentDescriptor), true);
            }
        }
        finally
        {
            current.Dispose();
        }
    }

    private static byte[] Terminate(byte[] bytes)
    {
        var result = new byte[bytes.Length + 1];
        bytes.CopyTo(result, 0);
        return result;
    }

    private static IOException NativeIOException(string message)
    {
        return new IOException(message, Marshal.GetLastPInvokeError());
    }

    private static nint ErrnoLocation()
    {
        return ErrnoLocationNative();
    }

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(int directoryFileDescriptor, byte[] path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int directoryFileDescriptor, byte[] path, int flags, uint mask, [Out] byte[] buffer);

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int Dup(int fileDescriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int fileDescriptor);

    [DllImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
    private static extern nint FdOpenDirectory(int fileDescriptor);

    [DllImport("libc", EntryPoint = "readdir", SetLastError = true)]
    private static extern nint ReadDirectory(nint directory);

    [DllImport("libc", EntryPoint = "closedir", SetLastError = true)]
    private static extern int CloseDirectory(nint directory);

    [DllImport("libc", EntryPoint = "__errno_location")]
    private static extern nint ErrnoLocationNative();

    [DllImport("libc", EntryPoint = "readlinkat", SetLastError = true)]
    private static extern nint ReadLinkAtNative(int directoryFileDescriptor, byte[] path, [Out] byte[] buffer, nuint bufferSize);

    [DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
    private static extern int MkdirAt(int directoryFileDescriptor, byte[] path, uint mode);

    [DllImport("libc", EntryPoint = "symlinkat", SetLastError = true)]
    private static extern int SymlinkAt(byte[] target, int directoryFileDescriptor, byte[] linkPath);

    [DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static extern int Fchmod(int fileDescriptor, uint mode);

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameAt2(int oldDirectoryFileDescriptor, byte[] oldPath, int newDirectoryFileDescriptor, byte[] newPath, uint flags);

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAtNative(int directoryFileDescriptor, byte[] path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(int fileDescriptor);
}
