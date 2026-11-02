using D3B.Data;
using D3B.Data.Packs;

namespace D3B.Bot.Data;

/// <summary>The pack of the installed client's build: extracted on this machine, else the one the app ships (D24, D172).</summary>
/// <remarks>
/// <para>
/// A pack extracted on this machine is found first, so that the maintainer's extraction of a build is used. Otherwise the
/// pack the published app ships with its build's release is. The pack of another build is never used: the data, and the
/// protocol mapping beside it, must match the client.
/// </para>
/// <para>
/// Every failure is a <see cref="DataUnavailableException"/> whose message says which build lacks its pack and how to
/// extract it.
/// </para>
/// </remarks>
public sealed class PackSource
{
    private readonly DataSettings _settings;
    private readonly Lazy<ClientBuild> _build;

    /// <summary>Gets the build of the installed client.</summary>
    /// <exception cref="DataUnavailableException">No client is installed where the configuration says.</exception>
    public ClientBuild Build => _build.Value;

    /// <summary>Gets where the pack of the installed build is: on this machine, else among the app's own.</summary>
    /// <exception cref="DataUnavailableException">No client is installed where the configuration says.</exception>
    public string PackPath => _settings.Locate(root => PackLocations.PathOf(root, Build));

    internal PackSource(DataSettings settings)
    {
        _settings = settings;

        // Publication only: a failed read is not remembered, so that a client installed meanwhile is found.
        _build = new Lazy<ClientBuild>(ReadBuild, LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>Opens the pack of the installed build.</summary>
    /// <returns>The open pack, which the caller disposes.</returns>
    /// <exception cref="DataUnavailableException">No client is installed, or its build's pack is missing, unreadable or of another build.</exception>
    public GamePack Open()
    {
        var build = Build;
        var path = PackPath;

        if (!File.Exists(path))
        {
            var bundled = PackLocations.PathOf(_settings.BundledDirectory, build);

            throw new DataUnavailableException($"No pack for client {build.Version}: none at {path}, nor at {bundled} among the app's own. {ExtractHint(false)}")
            {
                MissingBuild = build.Version,
            };
        }

        GamePack pack;

        try
        {
            pack = GamePack.Open(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DataUnavailableException($"The pack of client {build.Version} cannot be read: {exception.Message} {ExtractHint(true)}", exception);
        }

        if (pack.Info.ClientVersion != build.Version)
        {
            var other = pack.Info.ClientVersion;

            pack.Dispose();

            throw new DataUnavailableException($"The pack in {path} was extracted from client {other}, not from {build.Version}, the one installed. {ExtractHint(true)}");
        }

        return pack;
    }

    private ClientBuild ReadBuild()
    {
        try
        {
            return ClientBuild.Read(_settings.ClientDirectory);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new DataUnavailableException($"No client build in {_settings.ClientDirectory}: {exception.Message} {DataSettings.ClientDirectoryKey} names the client's installation directory.", exception);
        }
    }

    private string ExtractHint(bool force)
    {
        var options = new List<string>();

        if (!string.Equals(_settings.ClientDirectory, DataSettings.DefaultClientDirectory, StringComparison.OrdinalIgnoreCase))
            options.Add($"--client \"{_settings.ClientDirectory}\"");

        if (!string.Equals(_settings.PacksDirectory, PackLocations.DefaultRoot, StringComparison.OrdinalIgnoreCase))
            options.Add($"--output \"{_settings.PacksDirectory}\"");

        if (force)
            options.Add("--force");

        var arguments = options.Count > 0 ? " -- " + string.Join(' ', options) : string.Empty;

        return $"Extract it from the installed client with: dotnet run --file scripts/extract.cs{arguments}";
    }
}
