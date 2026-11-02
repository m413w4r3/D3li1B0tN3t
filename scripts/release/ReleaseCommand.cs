using System.CommandLine;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.Releases;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using D3B.Scripts.Unity;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Scripts.Release;

/// <summary>The command that publishes the pack, schema and mapping of a client build to the release of that build.</summary>
internal static class ReleaseCommand
{
    public static RootCommand Create()
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = @"The client whose build's files to publish. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
        };
        var version = new Option<string?>("--client-version")
        {
            Description = "The client version whose files to publish, instead of the installed client's.",
        };
        var packs = new Option<DirectoryInfo?>("--packs")
        {
            Description = @"The directory packs, schemas and mappings are kept in, one directory per client version. Defaults to %LOCALAPPDATA%\D3B\data, where the tools write.",
        };
        var repository = new Option<string>("--repository")
        {
            Description = "The repository whose releases ship the files, as owner/name.",
            DefaultValueFactory = _ => ClientRelease.DefaultRepository,
        };
        var publish = new Option<bool>("--publish")
        {
            Description = "Creates the release, or uploads the assets it lacks. Without it, nothing changes on GitHub.",
        };
        var command = new RootCommand("Publishes the pack, schema and mapping of a Dofus 3 client build to the GitHub release of that build.")
        {
            client,
            version,
            packs,
            repository,
            publish,
        };

        command.SetAction(result => Run(
            result.GetValue(client),
            result.GetValue(version),
            result.GetValue(packs)?.FullName ?? PackLocations.DefaultRoot,
            result.GetValue(repository)!,
            result.GetValue(publish)));

        return command;
    }

    private static int Run(DirectoryInfo? clientDirectory, string? version, string root, string repository, bool publish)
    {
        if (!ClientRelease.IsRepository(repository))
        {
            Console.Error.WriteLine($"'{repository}' does not name a GitHub repository as owner/name.");

            return 1;
        }

        ClientBuild build;

        try
        {
            build = version is null ? ClientBuild.Read(ClientInstall.Locate(clientDirectory).Root) : new ClientBuild(version, null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }

        (string What, string Path, string Tool)[] files =
        [
            ("pack", PackLocations.PathOf(root, build), "extract.cs"),
            ("schema", SchemaFile.PathOf(root, build.Version), "schema.cs"),
            ("mapping", MappingFile.PathOf(root, build.Version), "mapping.cs"),
        ];

        // A host needs all three to start (D31, D48): a release is published with all of them, or not at all.
        foreach (var (what, path, tool) in files.Where(file => !File.Exists(file.Path)))
        {
            Console.Error.WriteLine($"No {what} for client {build.Version} at {path}. Write it first: dotnet run --file scripts/{tool}");

            return 2;
        }

        try
        {
            return Release(build, root, repository, publish);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException
            or InvalidProtocolBufferException or DescriptorValidationException or ArgumentException or ProtocolMismatchException)
        {
            Console.Error.WriteLine($"The release failed: {exception.Message}");

            return 1;
        }
    }

    private static int Release(ClientBuild build, string root, string repository, bool publish)
    {
        var packPath = PackLocations.PathOf(root, build);
        var schemaPath = SchemaFile.PathOf(root, build.Version);
        var mappingPath = MappingFile.PathOf(root, build.Version);
        PackInfo info;

        // Opening checks the format and that the pack is whole; the version check catches a pack filed under another build.
        using (var pack = GamePack.Open(packPath))
            info = pack.Info;

        if (info.ClientVersion != build.Version)
            throw new InvalidDataException($"{packPath} was extracted from client {info.ClientVersion}, not {build.Version}.");

        var schema = ProtocolSchema.Load(schemaPath);
        var mapping = MappingFile.Read(mappingPath);

        if (mapping.ClientVersion != build.Version)
            throw new InvalidDataException($"{mappingPath} maps client {mapping.ClientVersion}, not {build.Version}.");

        // The host refuses a mapping that does not hold (D48): so does the release, before anything is published.
        var audit = ProtocolTranslation.Create(schema, mapping).Audit;

        ReleaseAsset[] assets =
        [
            ReleaseAsset.Hash(packPath, $"game data, pack format {GamePack.FormatVersion}, extracted by D3B.Data {info.ExtractorVersion} from client files of fingerprint `{info.Fingerprint}`"),
            ReleaseAsset.Hash(schemaPath, $"the protocol's obfuscated schema, format {SchemaFile.FormatVersion}, read from the client's code by `scripts/schema.cs`"),
            ReleaseAsset.Hash(mappingPath, $"the protocol mapping, format {MappingFile.FormatVersion}, matched with that schema by D3B.Protocol {mapping.ToolVersion}"),
        ];

        Console.WriteLine($"Pack: format {GamePack.FormatVersion}, client {info.ClientVersion}, extractor {info.ExtractorVersion}, fingerprint {info.Fingerprint}");
        Console.WriteLine($"Mapping: format {MappingFile.FormatVersion}, tool {mapping.ToolVersion}; {audit.Messages} messages, {audit.Fields} fields, {audit.Enums} enums and {audit.Values} values audited against the schema");

        foreach (var asset in assets)
            Console.WriteLine($"{asset.Name}: {asset.Path}, {asset.Length:N0} bytes, SHA-256 {asset.Sha256}, written to {asset.ChecksumPath}");

        var tag = ClientRelease.TagOf(build);
        var published = GitHubCli.Assets(repository, tag);
        List<ReleaseAsset> uploads;
        List<string> arguments;

        if (published is null)
        {
            uploads = [.. assets];
            arguments = ["release", "create", tag, .. Files(uploads), "--repo", repository, "--title", $"Client {build.Version}", "--notes", Notes(build, info, assets), "--latest=false"];
            Console.WriteLine($"Release {tag} of {repository} does not exist: it is to be created with {Names(uploads)}.");
        }
        else
        {
            // Every asset is checked before anything is uploaded: one that conflicts stops them all.
            uploads = [.. assets.Where(asset => IsMissing(asset, published, tag))];

            if (uploads.Count is 0)
            {
                Console.WriteLine($"Up to date: release {tag} of {repository} already holds {Names(assets)}.");

                return 0;
            }

            arguments = ["release", "upload", tag, .. Files(uploads), "--repo", repository];
            Console.WriteLine($"Release {tag} of {repository} exists without {Names(uploads)}: they are to be uploaded to it.");
        }

        if (!publish)
        {
            Console.WriteLine($"Dry run, nothing changed on GitHub. --publish runs: gh {string.Join(' ', arguments.Select(Quote))}");

            return 0;
        }

        var (exitCode, output, error) = GitHubCli.Run(arguments);

        Console.Write(output);

        if (exitCode is not 0)
            throw new InvalidOperationException($"gh {arguments[0]} {arguments[1]} failed: {error.Trim()}");

        var now = GitHubCli.Assets(repository, tag);

        foreach (var asset in uploads)
        {
            if (now is null || !now.TryGetValue(asset.Name, out var uploaded) || uploaded != $"sha256:{asset.Sha256}" || !now.ContainsKey(asset.ChecksumName))
                throw new InvalidOperationException($"Release {tag} does not hold the {asset.Name} just uploaded, by GitHub's digest: check it by hand.");
        }

        Console.WriteLine($"Published {Names(uploads)} to release {tag} of {repository}.");

        return 0;
    }

    /// <summary>Tells whether a release lacks an asset, and refuses one it holds otherwise.</summary>
    /// <exception cref="InvalidOperationException">The release holds another file under the asset's name, or one of the asset and its checksum without the other.</exception>
    private static bool IsMissing(ReleaseAsset asset, Dictionary<string, string?> published, string tag)
    {
        if (!published.TryGetValue(asset.Name, out var digest))
        {
            if (published.ContainsKey(asset.ChecksumName))
                throw new InvalidOperationException($"Release {tag} holds {asset.ChecksumName} without {asset.Name}. Fix the release by hand.");

            return true;
        }

        if (!published.ContainsKey(asset.ChecksumName))
            throw new InvalidOperationException($"Release {tag} holds {asset.Name} without {asset.ChecksumName}: hosts cannot check it. Fix the release by hand.");

        if (digest == $"sha256:{asset.Sha256}")
            return false;

        // D33: hosts that downloaded the published file keep it, so a replacement would leave two files under one name.
        throw new InvalidOperationException(
            $"Release {tag} already holds another {asset.Name} ({digest ?? "no digest"}). A published asset is never replaced (D33): "
            + "a change in what a tool writes ships under a new format, and a client changed under its version needs its fingerprint in the build's identity first.");
    }

    private static IEnumerable<string> Files(List<ReleaseAsset> assets)
    {
        return assets.SelectMany(asset => new[] { asset.Path, asset.ChecksumPath });
    }

    private static string Names(IEnumerable<ReleaseAsset> assets)
    {
        return string.Join(", ", assets.Select(asset => $"{asset.Name} and {asset.ChecksumName}"));
    }

    private static string Notes(ClientBuild build, PackInfo info, ReleaseAsset[] assets)
    {
        var built = info.ClientBuildDate is { } date ? $", built on {date:yyyy-MM-dd}" : string.Empty;
        var lines = string.Join('\n', assets.Select(asset => $"- `{asset.Name}`: SHA-256 `{asset.Sha256}`, {asset.Provenance}."));

        return $"""
            Files made from the Dofus 3 client {build.Version}{built}, for D3B: its game data, its protocol's obfuscated schema, and the mapping of D3B's readable protocol to it. A host reading format N of a file downloads the asset of that format; each asset's SHA-256 is in the asset of the same name ending in `.sha256`.

            {lines}

            Derived from the official client by the tools in `scripts/`. D3B is not affiliated with Ankama.
            """;
    }

    private static string Quote(string argument)
    {
        // The notes span several lines: their first stands for them.
        if (argument.Contains('\n', StringComparison.Ordinal))
            return $"\"{argument.Split('\n')[0]}...\"";

        return argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
    }
}
