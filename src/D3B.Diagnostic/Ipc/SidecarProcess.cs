using System.Diagnostics;
using System.Text;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Ipc;

/// <summary>Owns a diagnostic engine process and its private Unix socket path.</summary>
public sealed class SidecarProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task _stderrDrain;
    private readonly Task _stdoutDrain;
    private bool _disposed;

    /// <summary>The fresh Unix socket path assigned to this process.</summary>
    public string SocketPath { get; }

    private SidecarProcess(Process process, string socketPath)
    {
        _process = process;
        SocketPath = socketPath;
        _stderrDrain = DrainStandardErrorAsync(process.StandardError.BaseStream);
        _stdoutDrain = DrainStandardErrorAsync(process.StandardOutput.BaseStream);
    }

    /// <summary>Starts the configured engine without invoking a shell.</summary>
    public static async Task<SidecarProcessStartResult> StartAsync(
        SidecarOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(options.EnginePath) ||
            string.IsNullOrWhiteSpace(options.SocketDirectory))
        {
            return new SidecarProcessStartResult(null, DiagnosticErrorCode.UnsupportedPlatform);
        }

        string directory;
        string socketPath;
        bool processStarted = false;
        try
        {
            directory = Path.GetFullPath(options.SocketDirectory);
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
            }

            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            if (File.GetUnixFileMode(directory) != (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute))
            {
                return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
            }

            socketPath = Path.Combine(directory, $"d3b-diagnostic-{Guid.NewGuid():N}.sock");
            if (Encoding.UTF8.GetByteCount(socketPath) >= 108)
            {
                return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = options.EnginePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("serve");
        process.StartInfo.ArgumentList.Add("--socket");
        process.StartInfo.ArgumentList.Add(socketPath);

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return new SidecarProcessStartResult(null, DiagnosticErrorCode.IoError);
            }

            processStarted = true;

            var owner = new SidecarProcess(process, socketPath);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ConnectTimeout);
            while (!File.Exists(socketPath))
            {
                if (process.HasExited)
                {
                    await owner.DisposeAsync().ConfigureAwait(false);
                    return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
                }

                await Task.Delay(20, timeout.Token).ConfigureAwait(false);
            }

            File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (File.GetUnixFileMode(socketPath) != (UnixFileMode.UserRead | UnixFileMode.UserWrite))
            {
                await owner.DisposeAsync().ConfigureAwait(false);
                return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
            }

            return new SidecarProcessStartResult(owner, null);
        }
        catch (OperationCanceledException)
        {
            if (processStarted && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
            TryDeleteSocket(socketPath);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (processStarted && process.HasExited is false)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
            TryDeleteSocket(socketPath);
            return new SidecarProcessStartResult(null, DiagnosticErrorCode.SocketFailed);
        }
    }

    /// <summary>Stops the engine and removes the socket file.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }

            await _stderrDrain.ConfigureAwait(false);
            await _stdoutDrain.ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            TryDeleteSocket(SocketPath);
            _process.Dispose();
        }
    }

    private static async Task DrainStandardErrorAsync(Stream stream)
    {
        byte[] buffer = new byte[2048];
        while (await stream.ReadAsync(buffer).ConfigureAwait(false) != 0)
        {
        }
    }

    private static void TryDeleteSocket(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
