using D3B.Bot.Data;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Bot.Protocol;

/// <summary>The schema and mapping of the installed client's build: made on this machine, else the app's own (D48, D172).</summary>
/// <remarks>
/// <para>
/// As for the pack, files on this machine come first, so a build mapped here is used over the app's. <see cref="Load"/>
/// makes the build's translation, which audits the mapping (D44) and checks that it keeps the wire's numbers (D47): the
/// host's version gate (D11).
/// </para>
/// <para>
/// Every failure is a <see cref="ProtocolUnavailableException"/> whose message says which build lacks what, and how to
/// map it from its client.
/// </para>
/// </remarks>
public sealed class ProtocolSource
{
    private readonly DataSettings _settings;
    private readonly ClientBuild _build;

    /// <summary>Gets where the schema of the installed build is: beside its pack on this machine, else among the app's own.</summary>
    public string SchemaPath => _settings.Locate(root => SchemaFile.PathOf(root, _build.Version));

    /// <summary>Gets where the mapping of the installed build is: beside its pack on this machine, else among the app's own.</summary>
    public string MappingPath => _settings.Locate(root => MappingFile.PathOf(root, _build.Version));

    internal ProtocolSource(DataSettings settings, ClientBuild build)
    {
        _settings = settings;
        _build = build;
    }

    private static string Arguments(List<string> options)
    {
        return options.Count is 0 ? string.Empty : " -- " + string.Join(' ', options);
    }

    /// <summary>Loads the schema and mapping of the installed build, and makes its translation once the mapping holds.</summary>
    /// <returns>The build's translation.</returns>
    /// <exception cref="ProtocolUnavailableException">Either is missing or unreadable, the mapping is another build's, or it does not hold.</exception>
    public ProtocolTranslation Load()
    {
        var schema = Read("schema", SchemaPath, ProtocolSchema.Load);
        var mapping = Read("mapping", MappingPath, MappingFile.Read);

        if (mapping.ClientVersion != _build.Version)
            throw new ProtocolUnavailableException($"The mapping in {MappingPath} maps client {mapping.ClientVersion}, not {_build.Version}, the one installed. {MapHint()}");

        try
        {
            return ProtocolTranslation.Create(schema, mapping);
        }
        catch (ProtocolMismatchException exception)
        {
            throw new ProtocolUnavailableException(
                $"{exception.Message} The mapping is {MappingPath}, the schema {SchemaPath}. The mapping tool lists every failure: "
                + $"pin what the structure leaves open, or fix the readable .proto files, then map the build again. {MapHint()}",
                exception);
        }
    }

    private T Read<T>(string what, string path, Func<string, T> read)
    {
        if (!File.Exists(path))
            throw new ProtocolUnavailableException($"No {what} for client {_build.Version}: none at {path}, nor among the app's own in {_settings.BundledDirectory}. {MapHint()}") { MissingBuild = _build.Version };

        try
        {
            return read(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidProtocolBufferException or DescriptorValidationException or ArgumentException)
        {
            throw new ProtocolUnavailableException($"The {what} of client {_build.Version} cannot be read from {path}: {exception.Message} {MapHint()}", exception);
        }
    }

    private string MapHint()
    {
        var schema = new List<string>();
        var mapping = new List<string>();

        if (!string.Equals(_settings.ClientDirectory, DataSettings.DefaultClientDirectory, StringComparison.OrdinalIgnoreCase))
        {
            schema.Add($"--client \"{_settings.ClientDirectory}\"");
            mapping.Add($"--client \"{_settings.ClientDirectory}\"");
        }

        if (!string.Equals(_settings.PacksDirectory, PackLocations.DefaultRoot, StringComparison.OrdinalIgnoreCase))
        {
            schema.Add($"--output \"{_settings.PacksDirectory}\"");
            mapping.Add($"--data \"{_settings.PacksDirectory}\"");
        }

        // A new build's connection phase is mapped from its structure alone (D43): nothing has to be captured first.
        return $"Map the installed build with: dotnet run --file scripts/schema.cs{Arguments(schema)}, then dotnet run --file scripts/mapping.cs{Arguments(mapping)}; no capture is needed.";
    }
}
