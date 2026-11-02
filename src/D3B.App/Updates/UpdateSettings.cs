using D3B.Data.Releases;
using Microsoft.Extensions.Configuration;

namespace D3B.App.Updates;

/// <summary>Where the app looks for its newer versions, as its command line or environment says (D173).</summary>
internal static class UpdateSettings
{
    public const string SourceKey = "Updates:Source";

    /// <summary>Gets the repository whose releases ship the app.</summary>
    public static string DefaultSource => $"https://github.com/{ClientRelease.DefaultRepository}";

    /// <summary>Gets the source the configuration names, such as a folder of releases packed on this machine, else the repository.</summary>
    /// <param name="configuration">The app's configuration.</param>
    /// <returns>The source.</returns>
    public static string SourceIn(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration[SourceKey] is { Length: > 0 } source ? source.Trim() : DefaultSource;
    }
}
