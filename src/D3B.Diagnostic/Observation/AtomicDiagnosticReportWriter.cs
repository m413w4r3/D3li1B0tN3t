using System.Globalization;
using System.Security.Cryptography;
using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Observation;

/// <summary>Writes bounded reports to private files using a temporary file and atomic rename.</summary>
public sealed class AtomicDiagnosticReportWriter : IDiagnosticReportWriter
{
    private readonly DiagnosticReportOptions _options;
    private readonly string _directory;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a writer for one private report directory.</summary>
    public AtomicDiagnosticReportWriter(DiagnosticReportOptions options, string directory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options.Validate();
        _options = options;
        _directory = Path.GetFullPath(directory);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(DiagnosticReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        string? temporaryPath = null;
        string? finalPath = null;
        bool temporaryCreated = false;
        bool finalCreated = false;
        try
        {
            EnsurePrivateDirectory();
            byte[] bytes = DiagnosticReportJson.SerializeToUtf8Bytes(report, _options);
            string fileName = CreateFileName();
            finalPath = Path.Combine(_directory, fileName);
            temporaryPath = Path.Combine(_directory, "." + fileName + ".tmp");
            UnixFileMode privateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            var fileOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                fileOptions.UnixCreateMode = privateFileMode;
            }

            await using (var stream = new FileStream(temporaryPath, fileOptions))
            {
                temporaryCreated = true;
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, overwrite: false);
            finalCreated = true;
            temporaryPath = null;
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(finalPath, privateFileMode);
            }
        }
        catch
        {
            if (temporaryCreated)
            {
                DeleteBestEffort(temporaryPath);
            }
            if (finalCreated)
            {
                DeleteBestEffort(finalPath);
            }

            throw;
        }
    }

    private void EnsurePrivateDirectory()
    {
        string? root = Path.GetPathRoot(_directory);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(_directory.TrimEnd(Path.DirectorySeparatorChar), root?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)
            || string.Equals(_directory.TrimEnd(Path.DirectorySeparatorChar), temporaryRoot, StringComparison.Ordinal))
        {
            throw new IOException("The diagnostic report directory is not private.");
        }

        if (!Directory.Exists(_directory) && !File.Exists(_directory))
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(_directory);
            }
            else
            {
                Directory.CreateDirectory(
                    _directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        FileAttributes attributes = File.GetAttributes(_directory);
        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException("The diagnostic report directory is not a private directory.");
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                _directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private string CreateFileName()
    {
        byte[] random = new byte[4];
        RandomNumberGenerator.Fill(random);
        string timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        return $"diagnostic-report-{timestamp}-{Convert.ToHexStringLower(random)}.json";
    }

    private static void DeleteBestEffort(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Cleanup is best effort; the report write failure is handled by the session.
        }
    }
}
