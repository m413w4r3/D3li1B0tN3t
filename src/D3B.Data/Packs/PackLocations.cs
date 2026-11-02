namespace D3B.Data.Packs;

/// <summary>Where packs are kept on a machine: one directory per client version, one file per pack format (D24).</summary>
/// <remarks>
/// The extraction tool writes there by default and the host looks there first, so a pack extracted locally is used over
/// the one the app ships (D172). The format is in the file's name, so that versions of D3B reading different formats keep
/// their packs side by side.
/// </remarks>
public static class PackLocations
{
    /// <summary>Gets the directory packs are kept in by default: <c>%LOCALAPPDATA%\D3B\data</c> on Windows.</summary>
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "data");

    /// <summary>Gets the file name of a pack of <see cref="GamePack.FormatVersion"/>.</summary>
    public static string FileName => $"data-{GamePack.FormatVersion}.pack";

    /// <summary>Gets the path of the pack of a client build under a root directory.</summary>
    /// <param name="root">The directory packs are kept in, such as <see cref="DefaultRoot"/>.</param>
    /// <param name="build">The client build.</param>
    /// <returns>The pack's path.</returns>
    public static string PathOf(string root, ClientBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        return Path.Combine(root, build.Version, FileName);
    }
}
