using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Windowing;

internal static class XvfbTestCommands
{
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
}
