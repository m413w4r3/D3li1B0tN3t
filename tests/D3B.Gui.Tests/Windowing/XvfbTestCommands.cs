using System.Globalization;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Windowing;

internal static class XvfbTestCommands
{
    private const uint RegularFileType = 0x8000;
    private const uint SocketFileType = 0xC000;

    public static string? ResolveExecutable(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string executable = Path.GetFullPath(Path.Combine(directory, command));

                if (File.Exists(executable))
                {
                    return executable;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
        }

        return null;
    }

    public static async Task<CommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        XvfbTestDisplay display,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string?> environment = new Dictionary<string, string?>
        {
            ["DISPLAY"] = display.DisplayName
        };
        return await ProcessCommandRunner.RunAsync(
            executable,
            arguments,
            TimeSpan.FromSeconds(3),
            environment,
            cancellationToken).ConfigureAwait(false);
    }

    public static void AssertCommandSucceeded(CommandResult result, string command)
    {
        Assert.True(result.Succeeded, $"The Xvfb command '{command}' failed: {result.Failure ?? result.StandardError}");
    }

    public static void WriteExecutable(string path, string contents)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Xvfb tests require Linux executable permissions.");
        }

        File.WriteAllText(path, contents);
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public static async Task<(string UserId, string LockIdentity, string SocketIdentity)> CaptureOwnedDisplayFilesAsync(
        int displayNumber,
        int serverProcessId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayNumber);

        string stat = ResolveExecutable("stat") ?? throw new InvalidOperationException("The X11 tests require the installed 'stat' executable.");
        string? processIdentity = await ReadFileIdentityAsync(stat, $"/proc/{serverProcessId}").ConfigureAwait(false);
        string? lockIdentity = await ReadFileIdentityAsync(stat, GetLockFilePath(displayNumber)).ConfigureAwait(false);
        string? socketIdentity = await ReadFileIdentityAsync(stat, GetSocketFilePath(displayNumber)).ConfigureAwait(false);

        if (processIdentity is null || lockIdentity is null || socketIdentity is null)
        {
            throw new InvalidOperationException("Xvfb did not create its expected display lock and socket files.");
        }

        string userId = GetUserId(processIdentity);

        if (!string.Equals(GetUserId(lockIdentity), userId, StringComparison.Ordinal)
            || !string.Equals(GetUserId(socketIdentity), userId, StringComparison.Ordinal)
            || !HasFileType(lockIdentity, RegularFileType)
            || !HasFileType(socketIdentity, SocketFileType))
        {
            throw new InvalidOperationException("The Xvfb display files are not owned by the started server.");
        }

        string lockContents = (await File.ReadAllTextAsync(GetLockFilePath(displayNumber)).ConfigureAwait(false)).Trim();

        if (!int.TryParse(lockContents, NumberStyles.None, CultureInfo.InvariantCulture, out int lockProcessId)
            || lockProcessId != serverProcessId)
        {
            throw new InvalidOperationException("The Xvfb display lock does not name the started server.");
        }

        return (userId, lockIdentity, socketIdentity);
    }

    public static async Task<(string UserId, string? LockIdentity, string? SocketIdentity)?> TryCapturePartialDisplayFilesAsync(
        int displayNumber,
        int serverProcessId)
    {
        if (displayNumber <= 0)
        {
            return null;
        }

        string? stat = ResolveExecutable("stat");

        if (stat is null)
        {
            return null;
        }

        string? processIdentity = await ReadFileIdentityAsync(stat, "/proc/self").ConfigureAwait(false);
        string? lockIdentity = await ReadFileIdentityAsync(stat, GetLockFilePath(displayNumber)).ConfigureAwait(false);

        if (processIdentity is null || lockIdentity is null || !HasFileType(lockIdentity, RegularFileType))
        {
            return null;
        }

        string userId = GetUserId(processIdentity);

        if (!string.Equals(GetUserId(lockIdentity), userId, StringComparison.Ordinal))
        {
            return null;
        }

        string lockContents;

        try
        {
            lockContents = (await File.ReadAllTextAsync(GetLockFilePath(displayNumber)).ConfigureAwait(false)).Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (!string.Equals(lockContents, serverProcessId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return null;
        }

        string? socketIdentity = await ReadFileIdentityAsync(stat, GetSocketFilePath(displayNumber)).ConfigureAwait(false);

        if (socketIdentity is not null
            && (!string.Equals(GetUserId(socketIdentity), userId, StringComparison.Ordinal)
                || !HasFileType(socketIdentity, SocketFileType)))
        {
            socketIdentity = null;
        }

        return (userId, lockIdentity, socketIdentity);
    }

    public static async Task DeleteOwnedDisplayFilesAsync(
        int displayNumber,
        int serverProcessId,
        string userId,
        string? lockIdentity,
        string? socketIdentity)
    {
        if (displayNumber <= 0)
        {
            return;
        }

        string? stat = ResolveExecutable("stat");

        if (stat is null)
        {
            return;
        }

        if (socketIdentity is not null)
        {
            await DeleteIfUnchangedAsync(
                stat,
                GetSocketFilePath(displayNumber),
                userId,
                socketIdentity,
                SocketFileType).ConfigureAwait(false);
        }

        if (lockIdentity is not null)
        {
            await DeleteIfUnchangedAsync(
                stat,
                GetLockFilePath(displayNumber),
                userId,
                lockIdentity,
                RegularFileType,
                serverProcessId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        }
    }

    public static async Task<bool> IsProcessStoppedAsync(int processId, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            string statPath = $"/proc/{processId}/stat";

            if (!File.Exists(statPath))
            {
                return true;
            }

            string stat = await File.ReadAllTextAsync(statPath).ConfigureAwait(false);
            int commandEnd = stat.LastIndexOf(')');

            if (commandEnd >= 0 && commandEnd + 2 < stat.Length && stat[commandEnd + 2] == 'Z')
            {
                return true;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        return false;
    }

    public static string GetLockFilePath(int displayNumber)
    {
        return $"/tmp/.X{displayNumber}-lock";
    }

    public static string GetSocketFilePath(int displayNumber)
    {
        return $"/tmp/.X11-unix/X{displayNumber}";
    }

    private static async Task DeleteIfUnchangedAsync(
        string stat,
        string path,
        string userId,
        string identity,
        uint fileType,
        string? expectedContents = null)
    {
        string? currentIdentity = await ReadFileIdentityAsync(stat, path).ConfigureAwait(false);

        if (currentIdentity is not null
            && string.Equals(currentIdentity, identity, StringComparison.Ordinal)
            && string.Equals(GetUserId(currentIdentity), userId, StringComparison.Ordinal)
            && HasFileType(currentIdentity, fileType))
        {
            if (expectedContents is not null)
            {
                string contents = (await File.ReadAllTextAsync(path).ConfigureAwait(false)).Trim();

                if (!string.Equals(contents, expectedContents, StringComparison.Ordinal))
                {
                    return;
                }
            }

            File.Delete(path);
        }
    }

    private static async Task<string?> ReadFileIdentityAsync(string stat, string path)
    {
        CommandResult result = await ProcessCommandRunner.RunAsync(
            stat,
            ["--format=%u:%d:%i:%f", "--", path],
            TimeSpan.FromSeconds(2),
            environment: null,
            CancellationToken.None).ConfigureAwait(false);

        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    private static string GetUserId(string identity)
    {
        return identity.Split(':', StringSplitOptions.None)[0];
    }

    private static bool HasFileType(string identity, uint expectedFileType)
    {
        string[] parts = identity.Split(':', StringSplitOptions.None);
        return parts.Length == 4
            && uint.TryParse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint mode)
            && (mode & 0xF000) == expectedFileType;
    }
}
