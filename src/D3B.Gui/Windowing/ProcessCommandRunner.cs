using System.Diagnostics;
using System.Text;

namespace D3B.Gui.Windowing;

/// <summary>Runs bounded commands without invoking a shell.</summary>
internal static class ProcessCommandRunner
{
    private const int MaximumOutputBytes = 64 * 1024;

    /// <summary>Runs an absolute executable with argument-list quoting, bounded output, and a process-tree timeout.</summary>
    /// <param name="executablePath">An absolute path to the executable.</param>
    /// <param name="arguments">Arguments passed separately from the executable path.</param>
    /// <param name="timeout">The maximum time allowed for the process.</param>
    /// <param name="environment">Optional environment values applied to the child process.</param>
    /// <param name="cancellationToken">Cancels the command and kills its process tree.</param>
    /// <returns>A bounded command result. Startup and timeout errors are represented in the result.</returns>
    internal static async Task<CommandResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Path.IsPathFullyQualified(executablePath))
        {
            return new CommandResult(null, false, string.Empty, string.Empty, "The executable path must be absolute.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (KeyValuePair<string, string?> entry in environment)
            {
                process.StartInfo.Environment[entry.Key] = entry.Value;
            }
        }

        try
        {
            if (!process.Start())
            {
                return new CommandResult(null, false, string.Empty, string.Empty, "The process did not start.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return new CommandResult(null, false, string.Empty, string.Empty, Sanitize(exception.Message, environment));
        }

        using (process)
        using (CancellationTokenSource timeoutSource = new(timeout))
        using (CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
        {
            Task<byte[]> standardOutputTask = ReadBoundedAsync(process.StandardOutput.BaseStream, linkedSource.Token);
            Task<byte[]> standardErrorTask = ReadBoundedAsync(process.StandardError.BaseStream, linkedSource.Token);
            Task exitTask = process.WaitForExitAsync(linkedSource.Token);
            Task completionTask = Task.WhenAll(exitTask, standardOutputTask, standardErrorTask);
            ObserveFailure(completionTask);
            Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, linkedSource.Token);

            try
            {
                Task completedTask = await Task.WhenAny(completionTask, cancellationTask).ConfigureAwait(false);

                if (completedTask != completionTask)
                {
                    TerminateAndReleasePipes(process);

                    cancellationToken.ThrowIfCancellationRequested();

                    return new CommandResult(null, true, string.Empty, string.Empty, "The process timed out.");
                }

                await completionTask.ConfigureAwait(false);

                if (linkedSource.IsCancellationRequested)
                {
                    TerminateAndReleasePipes(process);

                    cancellationToken.ThrowIfCancellationRequested();

                    return new CommandResult(null, true, string.Empty, string.Empty, "The process timed out.");
                }

                byte[] output = await standardOutputTask.ConfigureAwait(false);
                byte[] error = await standardErrorTask.ConfigureAwait(false);

                return new CommandResult(
                    process.ExitCode,
                    false,
                    Encoding.UTF8.GetString(output),
                    Sanitize(Encoding.UTF8.GetString(error), environment),
                    null);
            }
            catch (OperationCanceledException)
            {
                TerminateAndReleasePipes(process);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return new CommandResult(
                    null,
                    true,
                    string.Empty,
                    string.Empty,
                    "The process timed out.");
            }
            catch (Exception exception)
            {
                TerminateAndReleasePipes(process);
                return new CommandResult(null, false, string.Empty, string.Empty, Sanitize(exception.Message, environment));
            }
        }
    }

    internal static string Sanitize(string value, IReadOnlyDictionary<string, string?>? environment)
    {
        string home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

        if (environment is not null && environment.TryGetValue("HOME", out string? providedHome) && !string.IsNullOrEmpty(providedHome))
        {
            home = providedHome;
        }

        if (!string.IsNullOrEmpty(home))
        {
            value = value.Replace(home, "~", StringComparison.Ordinal);
        }

        StringBuilder sanitized = new(value.Length);

        foreach (char character in value)
        {
            if (!char.IsControl(character))
            {
                sanitized.Append(character);
            }
            else if (character is '\r' or '\n' or '\t')
            {
                sanitized.Append(' ');
            }
        }

        return sanitized.ToString().Trim();
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        using MemoryStream captured = new();

        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            int remaining = MaximumOutputBytes - (int)captured.Length;

            if (remaining > 0)
            {
                captured.Write(buffer, 0, Math.Min(bytesRead, remaining));
            }
        }

        return captured.ToArray();
    }

    private static void TryKill(Process process)
    {
        // A descendant that has already been reparented may survive tree termination; closing our pipes still bounds this call.
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static void TerminateAndReleasePipes(Process process)
    {
        TryKill(process);
        DisposePipe(process.StandardOutput);
        DisposePipe(process.StandardError);
    }

    private static void DisposePipe(StreamReader reader)
    {
        try
        {
            reader.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void ObserveFailure(Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
