using D3B.Data.Packs;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Data;

/// <summary>Where the data module finds the client and the files made from its build, as the host's configuration says.</summary>
internal sealed class DataSettings
{
    public const string ClientDirectoryKey = "Client:Directory";
    public const string PacksDirectoryKey = "Data:Directory";

    /// <summary>Gets where the Ankama launcher installs the client, as the extraction tool assumes too.</summary>
    public static string DefaultClientDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ankama", "Dofus-dofus3");

    /// <summary>Gets where a published app keeps the files it ships with: beside its executable (D172).</summary>
    public static string DefaultBundledDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    public required string ClientDirectory { get; init; }

    /// <summary>Gets where the tools write a build's files on this machine, looked in first.</summary>
    public required string PacksDirectory { get; init; }

    /// <summary>Gets where the app's own files are, looked in once a file is not on this machine.</summary>
    public string BundledDirectory { get; init; } = DefaultBundledDirectory;

    /// <summary>Gets the installed client's directory the configuration names, or where the launcher installs it.</summary>
    public static string ClientDirectoryIn(IConfiguration configuration)
    {
        return PathOr(configuration[ClientDirectoryKey], DefaultClientDirectory);
    }

    public static DataSettings Read(IConfiguration configuration)
    {
        return new DataSettings
        {
            ClientDirectory = ClientDirectoryIn(configuration),
            PacksDirectory = PathOr(configuration[PacksDirectoryKey], PackLocations.DefaultRoot),
        };
    }

    /// <summary>Gets where a file of the installed build is: on this machine, else among the app's own.</summary>
    /// <param name="pathUnder">The file's path under a root directory.</param>
    /// <returns>The first path holding the file; the one on this machine when neither does, where the tools would write it.</returns>
    public string Locate(Func<string, string> pathUnder)
    {
        // A file made on this machine wins, so that the maintainer's extraction or mapping of a build is used over the app's.
        var local = pathUnder(PacksDirectory);

        if (File.Exists(local))
            return local;

        var bundled = pathUnder(BundledDirectory);

        return File.Exists(bundled) ? bundled : local;
    }

    private static string PathOr(string? configured, string fallback)
    {
        // A relative path starts from the host, never from the directory it was started in.
        return string.IsNullOrWhiteSpace(configured) ? fallback : Path.GetFullPath(configured, AppContext.BaseDirectory);
    }
}
