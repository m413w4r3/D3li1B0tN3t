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
    private const uint StatxType = 0x1;
    private const uint StatxInode = 0x100;
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
    private const int RenameExchange = 2;
    private const uint PrivateDirectoryMode = 0x1c0;
    private const uint PrivateFileMode = 0x180;
    private const int DirectoryNameOffset = 19;
    private const int MaximumLinkBytes = 1_048_576;
    private const string RepositoryMarkerName = "D3B.slnx";
    private static readonly byte[] _repositoryMarkerNameBytes = Encoding.ASCII.GetBytes(RepositoryMarkerName);
    private static readonly Lazy<IReadOnlyList<PreflightDirectoryIndex>> _repositoryDirectoryIndexes = new(DiscoverRepositoryDirectoryIndexes);

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
        int duplicate = OpenAt(
            directory.DangerousGetHandle().ToInt32(),
            [(byte)'.', 0],
            ReadOnly | DirectoryFlag | CloseOnExec,
            0);

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

    public static void ExchangeAt(
        SafeFileHandle firstParent,
        byte[] firstName,
        SafeFileHandle secondParent,
        byte[] secondName)
    {
        if (RenameAt2(
                firstParent.DangerousGetHandle().ToInt32(),
                Terminate(firstName),
                secondParent.DangerousGetHandle().ToInt32(),
                Terminate(secondName),
                RenameExchange) != 0)
        {
            throw NativeIOException("The directory entries could not be exchanged safely.");
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

    public static void SyncTree(SafeFileHandle root, Action? afterDirectorySynced = null)
    {
        PreflightFileStatus rootStatus = StatHandle(root);

        if (rootStatus.Type != Directory)
        {
            throw new PreflightBlockedException("sync_root_invalid");
        }

        SyncDirectoryTree(root, rootStatus, afterDirectorySynced);
    }

    public static void EnsureAncestrySameMount(PreflightFileStatus child, PreflightFileStatus parent)
    {
        EnsureSameMount(parent, child);
    }

    public static void EnsureSameMount(PreflightFileStatus entry, PreflightFileStatus root)
    {
        EnsureSameMount(entry, root, null);
    }

    public static void EnsureSameMount(
        PreflightFileStatus entry,
        PreflightFileStatus root,
        byte[]? relativePath)
    {
        if (entry.MountId != root.MountId || entry.DeviceMajor != root.DeviceMajor || entry.DeviceMinor != root.DeviceMinor)
        {
            if (relativePath is not null)
            {
                PreflightBlockedDetail detail = PreflightBlockedDetail.MountBoundary(relativePath, root, entry);
                throw new PreflightBlockedException("mount_boundary_unsupported", detail);
            }

            throw new PreflightBlockedException("mount_boundary_unsupported");
        }
    }

    public static bool IsSameObject(PreflightFileStatus left, PreflightFileStatus right)
    {
        return left.DeviceMajor == right.DeviceMajor && left.DeviceMinor == right.DeviceMinor && left.Inode == right.Inode;
    }

    public static bool IsStableStatus(PreflightFileStatus before, PreflightFileStatus after)
    {
        return before.Type == after.Type
            && before.Size == after.Size
            && before.Mode == after.Mode
            && before.Inode == after.Inode
            && before.DeviceMajor == after.DeviceMajor
            && before.DeviceMinor == after.DeviceMinor
            && before.MountId == after.MountId
            && before.ChangeSeconds == after.ChangeSeconds
            && before.ChangeNanoseconds == after.ChangeNanoseconds
            && before.ModifiedSeconds == after.ModifiedSeconds
            && before.ModifiedNanoseconds == after.ModifiedNanoseconds;
    }

    public static bool IsSameEntry(PreflightFileStatus left, PreflightFileStatus right)
    {
        return left.Type == right.Type
            && left.DeviceMajor == right.DeviceMajor
            && left.DeviceMinor == right.DeviceMinor
            && left.Inode == right.Inode
            && left.MountId == right.MountId;
    }

    public static bool IsSameObjectOnSameMount(PreflightFileStatus left, PreflightFileStatus right)
    {
        if (!IsSameObject(left, right))
        {
            return false;
        }

        EnsureSameMount(left, right);
        return true;
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
        return WalkAncestors(candidate, status => IsSameObject(status, ancestorStatus));
    }

    public static void EnsureNoRepositoryMarker(SafeFileHandle directory)
    {
        IReadOnlyList<PreflightDirectoryIndex> repositoryIndexes = _repositoryDirectoryIndexes.Value;
        WalkAncestors(
            directory,
            status =>
            {
                if (IsRepositoryIdentity(status, repositoryIndexes))
                {
                    throw new PreflightBlockedException("repository_output_forbidden");
                }

                return false;
            },
            current =>
            {
                if (TryStatAt(current, _repositoryMarkerNameBytes, out _))
                {
                    throw new PreflightBlockedException("repository_output_forbidden");
                }

                return false;
            });
    }

    internal static void EnsureNoRepositoryIdentity(
        IEnumerable<PreflightFileStatus> ancestors,
        IReadOnlyList<PreflightDirectoryIndex> repositoryIndexes)
    {
        if (WalkAncestors(ancestors, status => IsRepositoryIdentity(status, repositoryIndexes)))
        {
            throw new PreflightBlockedException("repository_output_forbidden");
        }
    }

    public static void EnsureOutsideTree(SafeFileHandle directory, PreflightDirectoryIndex sourceIndex)
    {
        bool aliasesSource = false;
        WalkAncestors(directory, status =>
        {
            aliasesSource |= IsSourceDescendantStatus(status, sourceIndex);
            return false;
        });

        if (aliasesSource)
        {
            throw new PreflightBlockedException("destination_aliases_source");
        }
    }

    public static void EnsureOutsideTree(IEnumerable<PreflightFileStatus> ancestors, PreflightDirectoryIndex sourceIndex)
    {
        bool aliasesSource = false;
        WalkAncestors(ancestors, status =>
        {
            aliasesSource |= IsSourceDescendantStatus(status, sourceIndex);
            return false;
        });

        if (aliasesSource)
        {
            throw new PreflightBlockedException("destination_aliases_source");
        }
    }

    public static bool HasAncestorInIndex(SafeFileHandle directory, PreflightDirectoryIndex sourceIndex)
    {
        return WalkAncestors(directory, sourceIndex.Contains);
    }

    internal static bool HasAncestorInIndex(IEnumerable<PreflightFileStatus> ancestors, PreflightDirectoryIndex sourceIndex)
    {
        return WalkAncestors(ancestors, sourceIndex.Contains);
    }

    internal static IReadOnlyList<PreflightFileStatus> GetAncestorStatuses(SafeFileHandle directory)
    {
        var ancestors = new List<PreflightFileStatus>();
        WalkAncestors(directory, status =>
        {
            ancestors.Add(status);
            return false;
        });
        return ancestors;
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

        uint mask = BitConverter.ToUInt32(buffer, 0);

        if ((mask & StatxMountId) == 0)
        {
            throw new PreflightBlockedException("mount_identity_unavailable");
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
            BitConverter.ToUInt32(buffer, 120),
            (mask & (StatxType | StatxInode)) == (StatxType | StatxInode));
    }

    private static bool WalkAncestors(SafeFileHandle directory, Func<PreflightFileStatus, bool> action)
    {
        return WalkAncestors(directory, action, null);
    }

    private static bool WalkAncestors(
        SafeFileHandle directory,
        Func<PreflightFileStatus, bool> action,
        Func<SafeFileHandle, bool>? handleAction)
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
                PreflightFileStatus currentStatus = StatHandle(current);

                if (action(currentStatus))
                {
                    return true;
                }

                if (handleAction is not null && handleAction(current))
                {
                    return true;
                }

                using SafeFileHandle parent = OpenDirectoryAt(current, [(byte)'.', (byte)'.']);
                PreflightFileStatus parentStatus = StatHandle(parent);

                if (IsSameObject(currentStatus, parentStatus))
                {
                    return false;
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

    private static bool WalkAncestors(IEnumerable<PreflightFileStatus> ancestors, Func<PreflightFileStatus, bool> action)
    {
        foreach (PreflightFileStatus status in ancestors)
        {
            if (action(status))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSourceDescendantStatus(PreflightFileStatus status, PreflightDirectoryIndex sourceIndex)
    {
        PreflightDirectoryIdentity identity = PreflightDirectoryIdentity.FromStatus(status);

        if (identity == sourceIndex.RootIdentity)
        {
            if (status.MountId == sourceIndex.RootStatus.MountId)
            {
                throw new PreflightBlockedException("output_inside_source_tree");
            }

            return true;
        }

        return sourceIndex.Contains(status);
    }

    private static bool IsRepositoryIdentity(PreflightFileStatus status, IReadOnlyList<PreflightDirectoryIndex> repositoryIndexes)
    {
        foreach (PreflightDirectoryIndex repositoryIndex in repositoryIndexes)
        {
            if (repositoryIndex.Contains(status))
            {
                return true;
            }
        }

        return false;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<PreflightDirectoryIndex> DiscoverRepositoryDirectoryIndexes()
    {
        var repositoryIndexes = new List<PreflightDirectoryIndex>();
        string[] anchors = [System.IO.Directory.GetCurrentDirectory(), AppContext.BaseDirectory];

        foreach (string anchor in anchors)
        {
            using SafeFileHandle anchorDirectory = OpenDirectoryPath(anchor);
            using SafeFileHandle? repositoryRoot = FindRepositoryRoot(anchorDirectory);

            if (repositoryRoot is null)
            {
                continue;
            }

            PreflightDirectoryIndex repositoryIndex;

            try
            {
                repositoryIndex = PreflightDirectoryIndex.Capture(repositoryRoot, true);
            }
            catch (Exception exception)
            {
                throw new PreflightBlockedException("repository_index_unavailable", exception);
            }

            if (!repositoryIndexes.Exists(index => index.RootIdentity == repositoryIndex.RootIdentity))
            {
                repositoryIndexes.Add(repositoryIndex);
            }
        }

        return repositoryIndexes.AsReadOnly();
    }

    private static SafeFileHandle? FindRepositoryRoot(SafeFileHandle anchorDirectory)
    {
        SafeFileHandle? repositoryRoot = null;
        WalkAncestors(
            anchorDirectory,
            _ => false,
            current =>
            {
                if (!TryStatAt(current, _repositoryMarkerNameBytes, out _))
                {
                    return false;
                }

                repositoryRoot = DuplicateHandle(current);
                return true;
            });
        return repositoryRoot;
    }

    private static void SyncDirectoryTree(
        SafeFileHandle directory,
        PreflightFileStatus rootStatus,
        Action? afterDirectorySynced)
    {
        foreach (byte[] name in EnumerateNames(directory))
        {
            PreflightFileStatus entry = StatAt(directory, name);
            EnsureSameMount(entry, rootStatus);

            if (entry.Type == Directory)
            {
                using SafeFileHandle child = OpenDirectoryAt(directory, name);
                PreflightFileStatus opened = StatHandle(child);

                if (!IsSameObject(entry, opened))
                {
                    throw new PreflightBlockedException("sync_entry_changed");
                }

                EnsureSameMount(opened, rootStatus);
                SyncDirectoryTree(child, rootStatus, afterDirectorySynced);
                continue;
            }

            if (entry.Type == RegularFile)
            {
                using SafeFileHandle file = OpenFileAt(directory, name);
                PreflightFileStatus opened = StatHandle(file);

                if (!IsSameObject(entry, opened))
                {
                    throw new PreflightBlockedException("sync_entry_changed");
                }

                EnsureSameMount(opened, rootStatus);

                if (Fsync(file.DangerousGetHandle().ToInt32()) != 0)
                {
                    throw NativeIOException("The file data could not be synchronized.");
                }

                continue;
            }

            if (entry.Type != SymbolicLink)
            {
                throw new PreflightBlockedException("sync_special_file_unsupported");
            }
        }

        SyncDirectory(directory);
        afterDirectorySynced?.Invoke();
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
