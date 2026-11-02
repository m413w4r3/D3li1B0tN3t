using D3B.Bot.Data;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Interactives;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Mapping;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests;

/// <summary>An installed client and a packs directory of a test's own, deleted with everything in them once it ends.</summary>
internal sealed class TestInstall : IDisposable
{
    public const string Version = "3.7.12.12";
    public const long MapId = 191_104_002;
    public const int AshNameId = 10;

    public string Root { get; }

    public string ClientDirectory => Path.Combine(Root, "client");

    public string PacksDirectory => Path.Combine(Root, "data");

    /// <summary>Gets where a published app's own files would be, beside its executable.</summary>
    public string BundledDirectory => Path.Combine(Root, "app", "data");

    public string PackPath => PackLocations.PathOf(PacksDirectory, new ClientBuild(Version, null));

    public string SchemaPath => SchemaFile.PathOf(PacksDirectory, Version);

    public string MappingPath => MappingFile.PathOf(PacksDirectory, Version);

    public string BundledPackPath => PackLocations.PathOf(BundledDirectory, new ClientBuild(Version, null));

    public string BundledMappingPath => MappingFile.PathOf(BundledDirectory, Version);

    /// <summary>Gets the data settings pointing at them, the app's own files included.</summary>
    public DataSettings Settings => new() { ClientDirectory = ClientDirectory, PacksDirectory = PacksDirectory, BundledDirectory = BundledDirectory };

    public string RedirectDirectory => Path.Combine(Root, "redirect");

    public string VersionFile => Path.Combine(ClientDirectory, "Dofus_Data", "StreamingAssets", "version");

    /// <summary>Gets the URL of the client's configuration, a local file the version file names, as Ankama's would.</summary>
    public string ConfigUrl => new Uri(Path.Combine(Root, "ankama-config.json")).AbsoluteUri;

    /// <summary>Gets the settings that point a host at them, as command-line arguments.</summary>
    /// <remarks>The relay listens on a port of its own, so that tests never take 5555 or 443 from a host running beside them.</remarks>
    public string[] Arguments => ["--Client:Directory", ClientDirectory, "--Data:Directory", PacksDirectory, "--Network:Ports:0", "0", "--Network:Game:Ports:0", "0", "--Network:Redirect:Directory", RedirectDirectory];

    public TestInstall()
    {
        Root = Path.Combine(Path.GetTempPath(), "D3B.Tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.GetDirectoryName(VersionFile)!);
        File.WriteAllText(VersionFile, $"Version={Version}\nBuildDate=2026-10-19T15:11:45+00:00\nConfigUrl={ConfigUrl}\n");
        File.WriteAllText(Path.Combine(Root, "ankama-config.json"), """{"connectionHosts":["Test:localhost:5555,443"]}""");
    }

    /// <summary>A pack holding a text, a map, a world of one vertex and the interactives, extracted from a client version.</summary>
    public static byte[] Pack(string clientVersion = Version)
    {
        var info = new PackInfo { ClientVersion = clientVersion, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };

        using var stream = new MemoryStream();

        // A low level keeps the tests fast; the format does not depend on it.
        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(TextTable.EntryName(TextLanguages.French), new TextTable(TextLanguages.French, [AshNameId], ["Frêne"]));
            writer.Add(MapData.EntryName(MapId), new MapData(MapId, 0, 0, 0, 0, new MapCell[MapData.CellCount], Array.Empty<MapInteractive>()));
            writer.Add(WorldGraph.EntryName, new WorldGraph(new WorldVertex[] { new(MapId, 1) }, Array.Empty<WorldEdge>(), Array.Empty<WorldTransition>(), Array.Empty<string>()));
            writer.Add(InteractiveCatalog.EntryName, new InteractiveCatalog(new Interactive[] { new(1, AshNameId) }));
            writer.Complete();
        }

        return stream.ToArray();
    }

    /// <summary>The schema and mapping of a build: the readable protocol obfuscated as a client's is, then matched from its structure and the pins of the game.</summary>
    /// <param name="clientVersion">The build the mapping names.</param>
    /// <param name="edit">Changes the mapping before it is serialized, such as to break it.</param>
    public static (byte[] Schema, byte[] Mapping) Protocol(string clientVersion = Version, Action<ProtocolMapping>? edit = null)
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var bytes = obfuscation.SchemaBytes();
        var schema = ProtocolSchema.Load(bytes);
        var mapping = new ProtocolMatcher(ReadableProtocol.Files, schema, ReadableProtocol.Scopes).Match(obfuscation.Pins(Obfuscation.GamePayloads()), [])
            .ToMapping(clientVersion, schema.Sha256, ReadableProtocol.Digest, "0.0.0-test");

        edit?.Invoke(mapping);

        return (bytes, mapping.ToByteArray());
    }

    /// <summary>A bot pointed at them, which reads neither the machine's environment nor its configuration files.</summary>
    public BotBuilder Bot(params string[] arguments)
    {
        return new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, Args = [.. Arguments, .. arguments] });
    }

    /// <summary>Writes a pack where the extraction tool would, or among the app's own files when <paramref name="bundled"/>.</summary>
    public void WritePack(string clientVersion = Version, bool bundled = false)
    {
        var path = bundled ? BundledPackPath : PackPath;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Pack(clientVersion));
    }

    /// <summary>Writes a schema and mapping where the mapping tools would, or among the app's own files when <paramref name="bundled"/>.</summary>
    public void WriteProtocol(string clientVersion = Version, Action<ProtocolMapping>? edit = null, bool bundled = false)
    {
        var (schema, mapping) = Protocol(clientVersion, edit);
        var root = bundled ? BundledDirectory : PacksDirectory;
        var schemaPath = SchemaFile.PathOf(root, Version);

        Directory.CreateDirectory(Path.GetDirectoryName(schemaPath)!);
        File.WriteAllBytes(schemaPath, schema);
        File.WriteAllBytes(MappingFile.PathOf(root, Version), mapping);
    }

    public void Dispose()
    {
        Directory.Delete(Root, true);
    }
}
