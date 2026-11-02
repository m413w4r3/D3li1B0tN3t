using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace D3B.Scripts.Unity;

/// <summary>Runs the GitHub CLI, which holds the maintainer's credentials, so that the tool never handles a token.</summary>
internal static class GitHubCli
{
    /// <summary>Runs <c>gh</c> and waits for it.</summary>
    /// <exception cref="InvalidOperationException">gh is not installed.</exception>
    public static (int ExitCode, string Output, string Error) Run(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("gh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("gh did not start.");

            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return (process.ExitCode, output, error.Result);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("The GitHub CLI, gh, is not installed: https://cli.github.com.", exception);
        }
    }

    /// <summary>Gets the assets of a release, each with the digest GitHub computed, or null when there is no such release.</summary>
    /// <exception cref="InvalidOperationException">gh failed for another reason, or answered what this tool does not read.</exception>
    public static Dictionary<string, string?>? Assets(string repository, string tag)
    {
        var (exitCode, output, error) = Run(["release", "view", tag, "--repo", repository, "--json", "assets"]);

        if (exitCode is not 0)
        {
            if (error.Contains("release not found", StringComparison.OrdinalIgnoreCase))
                return null;

            throw new InvalidOperationException($"gh release view {tag} failed: {error.Trim()}");
        }

        try
        {
            using var json = JsonDocument.Parse(output);

            var assets = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
            {
                var digest = asset.TryGetProperty("digest", out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

                assets[asset.GetProperty("name").GetString()!] = digest;
            }

            return assets;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException($"gh described release {tag} in a form this tool does not read.", exception);
        }
    }
}
