using System.CommandLine;
using System.Security.Cryptography;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.Releases;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using D3B.Scripts.Unity;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Scripts.Embed;

/// <summary>The command that puts a client build's pack, schema and mapping beside a published app (D172).</summary>
internal static class EmbedCommand
{
    public static RootCommand Create()
    {
        var version = new Option<string>("--client-version")
        {
            Description = "The client build whose files the app ships, the ClientVersion property of src/D3B.App.",
            Required = true,
        };
        var output = new Option<DirectoryInfo>("--output")
        {
            Description = "The published app's data directory, beside its executable: the files go in a directory named after the build under it.",
            Required = true,
        };
        var packs = new Option<DirectoryInfo?>("--packs")
        {
            Description = @"Takes the files from this directory, one directory per client version, such as %LOCALAPPDATA%\D3B\data, instead of the build's release.",
        };
        var repository = new Option<string>("--repository")
        {
            Description = "The repository whose releases ship the files, as owner/name.",
            DefaultValueFactory = _ => ClientRelease.DefaultRepository,
        };
        var command = new RootCommand("Puts the pack, schema and mapping of a Dofus 3 client build beside the published D3B app.")
        {
            version,
            output,
            packs,
            repository,
        };

        command.SetAction(result => Run(
            result.GetValue(version)!,
            result.GetValue(output)!.FullName,
            result.GetValue(packs)?.FullName,
            result.GetValue(repository)!));

        return command;
    }

    private static int Run(string version, string output, string? packs, string repository)
    {
        ClientBuild build;

        try
        {
            build = new ClientBuild(version, null);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }

        if (!ClientRelease.IsRepository(repository))
        {
            Console.Error.WriteLine($"'{repository}' does not name a GitHub repository as owner/name.");

            return 1;
        }

        string[] names = [PackLocations.FileName, SchemaFile.FileName, MappingFile.FileName];
        var download = packs is null ? Directory.CreateTempSubdirectory("d3b-embed-") : null;

        try
        {
            var source = download?.FullName ?? Path.Combine(packs!, build.Version);

            if (download is not null)
                Download(build, repository, names, source);

            foreach (var name in names.Where(name => !File.Exists(Path.Combine(source, name))))
            {
                Console.Error.WriteLine($"No {name} for client {build.Version} in {source}.");

                return 2;
            }

            Check(build, source);

            var target = Path.Combine(output, build.Version);

            Directory.CreateDirectory(target);

            foreach (var name in names)
                File.Copy(Path.Combine(source, name), Path.Combine(target, name), true);

            Console.WriteLine($"Embedded {string.Join(", ", names)} of client {build.Version} in {target}.");

            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException
            or InvalidProtocolBufferException or DescriptorValidationException or ArgumentException or ProtocolMismatchException)
        {
            Console.Error.WriteLine($"The files of client {build.Version} cannot be embedded: {exception.Message}");

            return 1;
        }
        finally
        {
            download?.Delete(true);
        }
    }

    /// <summary>Downloads the files and their checksums from the build's release, and checks each against its SHA-256 (D24).</summary>
    private static void Download(ClientBuild build, string repository, string[] names, string directory)
    {
        var tag = ClientRelease.TagOf(build);
        var patterns = names.SelectMany(name => new[] { "--pattern", name, "--pattern", ClientRelease.ChecksumNameOf(name) });
        var (exitCode, _, error) = GitHubCli.Run(["release", "download", tag, "--repo", repository, "--dir", directory, .. patterns]);

        if (exitCode is not 0)
            throw new InvalidOperationException($"gh release download {tag} failed: {error.Trim()}");

        foreach (var name in names)
        {
            var path = Path.Combine(directory, name);
            var checksum = Path.Combine(directory, ClientRelease.ChecksumNameOf(name));

            if (!File.Exists(path) || !File.Exists(checksum))
                throw new InvalidDataException($"release {tag} of {repository} lacks {name} or its checksum.");

            var expected = ClientRelease.ParseChecksum(File.ReadAllText(checksum), name);
            string actual;

            using (var stream = File.OpenRead(path))
                actual = Convert.ToHexStringLower(SHA256.HashData(stream));

            if (actual != expected)
                throw new InvalidDataException($"{name} of release {tag} has SHA-256 {actual}, not {expected} as published.");
        }
    }

    /// <summary>Checks the files as the host does before it starts: the pack is the build's, and the mapping holds (D31, D48).</summary>
    private static void Check(ClientBuild build, string directory)
    {
        PackInfo info;

        using (var pack = GamePack.Open(Path.Combine(directory, PackLocations.FileName)))
            info = pack.Info;

        if (info.ClientVersion != build.Version)
            throw new InvalidDataException($"its {PackLocations.FileName} was extracted from client {info.ClientVersion}.");

        var schema = ProtocolSchema.Load(Path.Combine(directory, SchemaFile.FileName));
        var mapping = MappingFile.Read(Path.Combine(directory, MappingFile.FileName));

        if (mapping.ClientVersion != build.Version)
            throw new InvalidDataException($"its {MappingFile.FileName} maps client {mapping.ClientVersion}.");

        var audit = ProtocolTranslation.Create(schema, mapping).Audit;

        Console.WriteLine($"Pack: format {GamePack.FormatVersion}, extractor {info.ExtractorVersion}, fingerprint {info.Fingerprint}");
        Console.WriteLine($"Mapping: format {MappingFile.FormatVersion}, tool {mapping.ToolVersion}; {audit.Messages} messages, {audit.Fields} fields, {audit.Enums} enums and {audit.Values} values audited against the schema");
    }
}
