using System.Reflection;

namespace D3B.App.Updates;

/// <summary>What this build of the app is: its version, and the client build whose files it ships (D172).</summary>
internal static class AppRelease
{
    /// <summary>Gets the app's version, as its tag names it, without the commit.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Gets the client build whose pack, schema and mapping the published app ships.</summary>
    public static string ClientVersion { get; } = ReadMetadata("ClientVersion") ?? "?";

    private static string ReadVersion()
    {
        var version = typeof(AppRelease).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        var metadata = version.IndexOf('+', StringComparison.Ordinal);

        return metadata < 0 ? version : version[..metadata];
    }

    private static string? ReadMetadata(string key)
    {
        return typeof(AppRelease).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == key)?.Value;
    }
}
