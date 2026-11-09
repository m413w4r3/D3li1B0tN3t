using System.ComponentModel;
using System.Diagnostics;

namespace D3B.Scripts.Tests.Preflight;

public sealed class PreflightBindMountTests
{
    private const int ScenarioTimeoutSeconds = 120;
    private static readonly Lazy<string> _repositoryRoot = new(FindRepositoryRoot);
    private static readonly Lazy<string> _preflightAppHost = new(FindOrBuildPreflightAppHost);
    private static readonly Lazy<CapabilityProbe> _capabilityProbe = new(ProbeCapability);

    [Fact]
    public async Task SnapshotRejectsSourceDescendantBindMountAlias()
    {
        EnsureLinuxAndNamespaceCapability();
        string script = CreateBlockedScenarioScript(
            "source=\"$tmp/source\"; mkdir -p \"$source/deep\"; printf 'synthetic\\n' > \"$source/deep/file\"",
            "$source/deep",
            "$app snapshot --root \"$tmp/source\" --output \"$alias/manifest.json\"",
            "destination_aliases_source",
            "test ! -e \"$tmp/source/deep/manifest.json\"");

        ProcessResult result = await RunScenarioAsync(script);

        AssertBlockedScenario(result, "destination_aliases_source");
    }

    [Fact]
    public async Task SnapshotRejectsSourceDescendantAliasBeforeCreatingMissingOutputParents()
    {
        EnsureLinuxAndNamespaceCapability();
        string script = CreateBlockedScenarioScript(
            "source=\"$tmp/source\"; mkdir -p \"$source/deep\"; printf 'synthetic\\n' > \"$source/deep/file\"",
            "$source/deep",
            "$app snapshot --root \"$tmp/source\" --output \"$alias/nested/new/manifest.json\"",
            "destination_aliases_source",
            "test ! -e \"$tmp/source/deep/nested\"");

        ProcessResult result = await RunScenarioAsync(script);

        AssertBlockedScenario(result, "destination_aliases_source");
    }

    [Fact]
    public async Task SnapshotRejectsSourceRootBindMountAlias()
    {
        EnsureLinuxAndNamespaceCapability();
        string script = CreateBlockedScenarioScript(
            "source=\"$tmp/source\"; mkdir -p \"$source/deep\"; printf 'synthetic\\n' > \"$source/deep/file\"",
            "$source",
            "$app snapshot --root \"$tmp/source\" --output \"$alias/manifest.json\"",
            "destination_aliases_source",
            "test ! -e \"$tmp/source/manifest.json\"");

        ProcessResult result = await RunScenarioAsync(script);

        AssertBlockedScenario(result, "destination_aliases_source");
    }

    [Fact]
    public async Task SnapshotRejectsRepositoryDescendantBindMountAlias()
    {
        EnsureLinuxAndNamespaceCapability();
        string fixture = Path.Combine(_repositoryRoot.Value, "artifacts");
        string script = CreateBlockedScenarioScript(
            "source=\"$tmp/snapshot-source\"; mkdir -p \"$source\"; printf 'synthetic\\n' > \"$source/file\"",
            "$fixture",
            "$app snapshot --root \"$tmp/snapshot-source\" --output \"$alias/preflight-bind-check-manifest.json\"",
            "repository_output_forbidden",
            "test ! -e \"$fixture/preflight-bind-check-manifest.json\"");

        ProcessResult result = await RunScenarioAsync(script, fixture);

        AssertBlockedScenario(result, "repository_output_forbidden");
    }

    [Fact]
    public async Task RestoreRejectsDestinationUnderBackupBindMountAlias()
    {
        EnsureLinuxAndNamespaceCapability();
        string script = CreateRestoreOverlapScenarioScript(
            "$alias/destination",
            "test ! -e \"$alias/destination\" && test -z \"$(find \"$tmp/backup/tree\" -mindepth 1 -maxdepth 1 -type d -name '.preflight-stage-*' -print -quit)\"");

        ProcessResult result = await RunScenarioAsync(script);

        AssertBlockedScenario(result, "restore_paths_overlap");
    }

    [Fact]
    public async Task RestoreRejectsDestinationUnderDeepBackupBindMountAlias()
    {
        EnsureLinuxAndNamespaceCapability();
        string script = CreateRestoreOverlapScenarioScript(
            "$alias/nested/destination2",
            "test ! -e \"$alias/nested/destination2\" && test -z \"$(find \"$alias/nested\" -mindepth 1 -maxdepth 1 -type d -name '.preflight-stage-*' -print -quit)\"");

        ProcessResult result = await RunScenarioAsync(script);

        AssertBlockedScenario(result, "restore_paths_overlap");
    }

    [Fact]
    public async Task SnapshotAndRestoreSucceedWithoutBindMountAliases()
    {
        EnsureLinuxAndNamespaceCapability();
        string script = """
            set -eu
            repo=$1
            app=$2
            fixture=$3
            umask 077
            tmp=$(mktemp -d /tmp/d3b-preflight-bind.XXXXXX)
            alias="$tmp/alias"
            trap 'umount "$alias" 2>/dev/null || true; rm -rf "$tmp";' EXIT
            mkdir -p "$alias" "$tmp/source/nested" "$tmp/private" "$tmp/restore-parent"
            printf 'synthetic control\n' > "$tmp/source/nested/file"
            "$app" snapshot --root "$tmp/source" --output "$tmp/private/snapshot.json"
            "$app" verify --root "$tmp/source" --manifest "$tmp/private/snapshot.json"
            "$app" backup --root "$tmp/source" --output "$tmp/private/backup"
            "$app" restore --backup "$tmp/private/backup" --manifest "$tmp/private/backup/manifest.json" --root "$tmp/restore-parent/restored"
            "$app" verify --root "$tmp/restore-parent/restored" --manifest "$tmp/private/backup/manifest.json"
            """;

        ProcessResult result = await RunScenarioAsync(script);

        Assert.False(result.TimedOut, result.FormatFailure("Normal-path control timed out."));
        Assert.True(result.ExitCode == 0, result.FormatFailure("Normal-path snapshot and restore did not succeed."));
        Assert.Contains("OK snapshot entries=", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("OK backup entries=", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("OK restore", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(2, CountLines(result.StandardOutput, "OK verify"));
        Assert.Empty(result.StandardError);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "D3B.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate D3B.slnx from the test output directory.");
    }

    private static string FindOrBuildPreflightAppHost()
    {
        string repositoryRoot = _repositoryRoot.Value;
        string appHost = Path.Combine(repositoryRoot, "artifacts", "bin", "preflight.cs", "debug", "preflight");

        if (File.Exists(appHost) && !IsPreflightAppHostStale(repositoryRoot, appHost))
        {
            return appHost;
        }

        ProcessResult result = RunProcessAsync(
            GetDotnetExecutable(),
            ["build", "scripts/preflight.cs", "-p:NuGetAudit=false", "-v:q"],
            repositoryRoot,
            TimeSpan.FromSeconds(ScenarioTimeoutSeconds)).GetAwaiter().GetResult();

        if (result.TimedOut || result.ExitCode != 0 || !File.Exists(appHost))
        {
            throw new InvalidOperationException(result.FormatFailure("Building scripts/preflight.cs did not produce the expected apphost."));
        }

        return appHost;
    }

    private static bool IsPreflightAppHostStale(string repositoryRoot, string appHost)
    {
        DateTime appHostLastWriteTimeUtc = File.GetLastWriteTimeUtc(appHost);
        string preflightDirectory = Path.Combine(repositoryRoot, "scripts", "preflight");
        string entryPoint = Path.Combine(repositoryRoot, "scripts", "preflight.cs");

        if (File.GetLastWriteTimeUtc(entryPoint) > appHostLastWriteTimeUtc)
        {
            return true;
        }

        foreach (string sourceFile in Directory.EnumerateFiles(preflightDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (File.GetLastWriteTimeUtc(sourceFile) > appHostLastWriteTimeUtc)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDotnetExecutable()
    {
        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        if (!string.IsNullOrWhiteSpace(hostPath))
        {
            return hostPath;
        }

        DirectoryInfo? runtimeDirectory = new(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        DirectoryInfo? dotnetRoot = runtimeDirectory?.Parent?.Parent?.Parent;

        if (dotnetRoot is not null)
        {
            string candidate = Path.Combine(dotnetRoot.FullName, "dotnet");

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "dotnet";
    }

    private static CapabilityProbe ProbeCapability()
    {
        if (!OperatingSystem.IsLinux())
        {
            return new CapabilityProbe(false, "Linux user and mount namespaces are required.");
        }

        if (FindExecutable("mount") is null)
        {
            return new CapabilityProbe(false, "mount is not available in PATH.");
        }

        ProcessResult result = RunProcessAsync(
            "unshare",
            ["--user", "--map-root-user", "--mount", "true"],
            _repositoryRoot.Value,
            TimeSpan.FromSeconds(ScenarioTimeoutSeconds)).GetAwaiter().GetResult();

        if (result.TimedOut)
        {
            return new CapabilityProbe(false, result.FormatFailure("unshare --user --map-root-user --mount true timed out."));
        }

        if (result.StartError is not null)
        {
            return new CapabilityProbe(false, $"Could not start unshare --user --map-root-user --mount true: {result.StartError}");
        }

        if (result.ExitCode != 0)
        {
            return new CapabilityProbe(false, result.FormatFailure("unshare --user --map-root-user --mount true failed."));
        }

        return new CapabilityProbe(true, string.Empty);
    }

    private static void EnsureLinuxAndNamespaceCapability()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Linux user and mount namespaces are required.");
        }

        CapabilityProbe probe = _capabilityProbe.Value;

        if (!probe.Available)
        {
            Assert.Skip(probe.Reason);
        }
    }

    private static string? FindExecutable(string name)
    {
        string? searchPath = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(searchPath))
        {
            return null;
        }

        foreach (string directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, name);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string CreateBlockedScenarioScript(string setup, string mountSource, string command, string code, string noWriteCheck)
    {
        return $$"""
            set -eu
            repo=$1
            app=$2
            fixture=$3
            umask 077
            tmp=$(mktemp -d /tmp/d3b-preflight-bind.XXXXXX)
            alias="$tmp/alias"
            trap 'umount "$alias" 2>/dev/null || true; rm -rf "$tmp";' EXIT
            mkdir -p "$alias"
            {{setup}}
            mount --make-rprivate / > "$tmp/private-mount.log" 2>&1 || {
                status=$?
                cat "$tmp/private-mount.log" >&2
                printf 'CAPABILITY_UNAVAILABLE: mount --make-rprivate / exited %s\n' "$status" >&2
                exit 125
            }
            mount --bind {{mountSource}} "$alias" > "$tmp/bind-mount.log" 2>&1 || {
                status=$?
                cat "$tmp/bind-mount.log" >&2
                printf 'CAPABILITY_UNAVAILABLE: mount --bind exited %s\n' "$status" >&2
                exit 125
            }
            set +e
            {{command}} > "$tmp/cli.stdout" 2> "$tmp/cli.stderr"
            status=$?
            set -e
            cat "$tmp/cli.stdout"
            cat "$tmp/cli.stderr" >&2
            if ( {{noWriteCheck}} ); then
                printf 'NO_ALIAS_WRITE\n'
            else
                printf 'ALIAS_WRITE_DETECTED\n'
            fi
            exit "$status"
            """;
    }

    private static string CreateRestoreOverlapScenarioScript(string destination, string noWriteCheck)
    {
        return $$"""
            set -eu
            repo=$1
            app=$2
            fixture=$3
            umask 077
            tmp=$(mktemp -d /tmp/d3b-preflight-bind.XXXXXX)
            alias="$tmp/alias"
            trap 'umount "$alias" 2>/dev/null || true; rm -rf "$tmp";' EXIT
            mkdir -p "$alias" "$tmp/source/nested"
            printf 'synthetic restore\n' > "$tmp/source/nested/file"
            "$app" backup --root "$tmp/source" --output "$tmp/backup"
            mount --make-rprivate / > "$tmp/private-mount.log" 2>&1 || {
                status=$?
                cat "$tmp/private-mount.log" >&2
                printf 'CAPABILITY_UNAVAILABLE: mount --make-rprivate / exited %s\n' "$status" >&2
                exit 125
            }
            mount --bind "$tmp/backup/tree" "$alias" > "$tmp/bind-mount.log" 2>&1 || {
                status=$?
                cat "$tmp/bind-mount.log" >&2
                printf 'CAPABILITY_UNAVAILABLE: mount --bind exited %s\n' "$status" >&2
                exit 125
            }
            set +e
            "$app" restore --backup "$tmp/backup" --manifest "$tmp/backup/manifest.json" --root {{destination}} > "$tmp/cli.stdout" 2> "$tmp/cli.stderr"
            status=$?
            set -e
            cat "$tmp/cli.stdout"
            cat "$tmp/cli.stderr" >&2
            if ( {{noWriteCheck}} ); then
                printf 'NO_ALIAS_WRITE\n'
            else
                printf 'ALIAS_WRITE_DETECTED\n'
            fi
            exit "$status"
            """;
    }

    private static async Task<ProcessResult> RunScenarioAsync(string script, string fixture = "")
    {
        string repositoryRoot = _repositoryRoot.Value;
        string appHost = _preflightAppHost.Value;
        ProcessResult result = await RunProcessAsync(
            "unshare",
            [
                "--user",
                "--map-root-user",
                "--mount",
                "--fork",
                "/bin/sh",
                "-c",
                script,
                "preflight-bind-mount",
                repositoryRoot,
                appHost,
                fixture,
            ],
            repositoryRoot,
            TimeSpan.FromSeconds(ScenarioTimeoutSeconds));

        if (result.StartError is not null)
        {
            Assert.Skip($"Could not start unshare scenario: {result.StartError}");
        }

        if (result.TimedOut)
        {
            Assert.Fail(result.FormatFailure("The namespace scenario timed out."));
        }

        if (result.ExitCode == 125 && result.StandardError.Contains("CAPABILITY_UNAVAILABLE:", StringComparison.Ordinal))
        {
            if (IsPrivilegeFailure(result.StandardError))
            {
                Assert.Skip("Bind-mount capability is unavailable: " + result.StandardError.Trim());
            }

            Assert.Fail(result.FormatFailure("The scenario could not set up its bind mount."));
        }

        if (result.ExitCode != 0 && IsPrivilegeFailure(result.StandardError))
        {
            Assert.Skip("Namespace capability became unavailable: " + result.StandardError.Trim());
        }

        return result;
    }

    private static void AssertBlockedScenario(ProcessResult result, string code)
    {
        Assert.False(result.TimedOut, result.FormatFailure("The blocked scenario timed out."));
        Assert.True(result.ExitCode == 1, $"Expected exit code 1 for BLOCKED: {code}.{Environment.NewLine}Full stderr:{Environment.NewLine}{result.StandardError}");
        string expectedBlockedLine = "BLOCKED: " + code;
        bool hasExpectedBlockedLine = result.StandardError
            .Split('\n')
            .Any(line => line.TrimEnd('\r').Equals(expectedBlockedLine, StringComparison.Ordinal));
        Assert.True(hasExpectedBlockedLine, $"Expected {expectedBlockedLine}.{Environment.NewLine}Full stderr:{Environment.NewLine}{result.StandardError}");
        Assert.Contains("NO_ALIAS_WRITE", result.StandardOutput.Split('\n'), StringComparer.Ordinal);
        Assert.DoesNotContain("ALIAS_WRITE_DETECTED", result.StandardOutput, StringComparison.Ordinal);
    }

    private static bool IsPrivilegeFailure(string output)
    {
        return output.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase)
            || output.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Function not implemented", StringComparison.OrdinalIgnoreCase)
            || output.Contains("EPERM", StringComparison.OrdinalIgnoreCase)
            || output.Contains("ENOSYS", StringComparison.OrdinalIgnoreCase)
            || output.Contains("not supported", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountLines(string value, string expected)
    {
        int count = 0;

        foreach (string line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.TrimEnd('\r').Equals(expected, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static async Task<ProcessResult> RunProcessAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new ProcessResult(-1, string.Empty, string.Empty, false, "Process.Start returned false.");
            }
        }
        catch (Win32Exception exception)
        {
            return new ProcessResult(-1, string.Empty, string.Empty, false, exception.Message);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool timedOut = false;

        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            timedOut = true;

            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            await process.WaitForExitAsync();
        }

        await Task.WhenAll(standardOutput, standardError);
        return new ProcessResult(process.ExitCode, standardOutput.Result, standardError.Result, timedOut, null);
    }

    private sealed record CapabilityProbe(bool Available, string Reason);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, string? StartError)
    {
        public string FormatFailure(string description)
        {
            return $"{description}{Environment.NewLine}exit code: {ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}{Environment.NewLine}start error: {StartError}";
        }
    }
}
