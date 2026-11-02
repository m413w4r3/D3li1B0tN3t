using System.CommandLine;
using System.Diagnostics;
using System.Security.Cryptography;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Protocol.Mapping;
using D3B.Scripts.Unity;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Scripts.Schema;

/// <summary>The command that reads the installed client's protocol and writes its obfuscated schema.</summary>
internal static class SchemaCommand
{
    private static readonly (string Assembly, string File)[] _protocols =
    [
        ("Ankama.Dofus.Protocol.Connection", "connection.proto"),
        ("Ankama.Dofus.Protocol.Game", "game.proto"),
    ];

    public static RootCommand Create()
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = @"The client's directory. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
        };
        var output = new Option<DirectoryInfo?>("--output")
        {
            Description = @"The directory schemas are kept in, one directory per client version, beside the packs. Defaults to %LOCALAPPDATA%\D3B\data.",
        };
        var proto = new Option<DirectoryInfo?>("--proto")
        {
            Description = "Also writes the schema as .proto text into this directory, to read it. Derived from the client: never commit it.",
        };
        var unityVersion = new Option<string?>("--unity-version")
        {
            Description = "The Unity version used to build the client. Defaults to Cpp2IL detection.",
        };
        var command = new RootCommand("Reads the protocol of the installed Dofus 3 client with Cpp2IL and writes its obfuscated schema.")
        {
            client,
            output,
            proto,
            unityVersion,
        };

        command.SetAction(result => Run(
            result.GetValue(client),
            result.GetValue(output)?.FullName ?? PackLocations.DefaultRoot,
            result.GetValue(proto)?.FullName,
            result.GetValue(unityVersion)));

        return command;
    }

    private static int Run(DirectoryInfo? clientDirectory, string root, string? protoDirectory, string? unityVersion)
    {
        ClientInstall client;

        try
        {
            client = ClientInstall.Locate(clientDirectory);
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }

        try
        {
            return Write(client, root, protoDirectory, unityVersion);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or DescriptorValidationException)
        {
            Console.Error.WriteLine($"The schema could not be read: {exception.Message}");

            return 1;
        }
    }

    private static int Write(ClientInstall client, string root, string? protoDirectory, string? unityVersion)
    {
        var stopwatch = Stopwatch.StartNew();
        var build = ClientBuild.Read(client.Root);

        Console.WriteLine($"Client: {client.Root}, version {build}");

        using var code = ClientCode.Open(client, unityVersion);

        Console.WriteLine($"Unity {code.UnityVersion}, IL2CPP metadata version {code.MetadataVersion}, opened in {stopwatch.Elapsed.TotalSeconds:F1} s");

        var reading = Stopwatch.StartNew();
        var files = _protocols.Select(protocol => ProtocolReader.Read(code.Assembly(protocol.Assembly), protocol.File, code.Platform)).ToList();
        var set = new FileDescriptorSet();

        if (files.Any(file => file.Dependency.Contains(ProtocolTypes.AnyFile)))
            set.File.Add(AnyReflection.Descriptor.ToProto());

        set.File.AddRange(files);

        // Building the descriptors links every type name to its type and refuses a name declared twice: a schema protobuf
        // cannot load is never written.
        FileDescriptor.BuildFromByteStrings(set.File.Select(file => file.ToByteString()));

        Console.WriteLine($"Read in {reading.Elapsed.TotalSeconds:F1} s");

        var types = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var (file, protocol) in files.Zip(_protocols))
        {
            var summary = SchemaSummary.Of(file);

            Console.WriteLine($"{file.Name}, from {protocol.Assembly}: {summary}");

            foreach (var (type, count) in summary.Types)
                types[type] = types.GetValueOrDefault(type) + count;
        }

        Console.WriteLine($"Field types: {string.Join(", ", types.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"))}");

        var path = SchemaFile.PathOf(root, build.Version);
        var bytes = set.ToByteArray();
        var temporary = path + ".tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written aside, then moved: a schema is never half written under its name.
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, true);

        Console.WriteLine($"Schema: {path}, {bytes.Length:N0} bytes, format {SchemaFile.FormatVersion}, in {stopwatch.Elapsed.TotalSeconds:F1} s");
        Console.WriteLine($"SHA-256: {Convert.ToHexStringLower(SHA256.HashData(bytes))}");

        if (protoDirectory is not null)
        {
            Directory.CreateDirectory(protoDirectory);

            foreach (var (file, protocol) in files.Zip(_protocols))
            {
                using var writer = new StreamWriter(Path.Combine(protoDirectory, file.Name));

                ProtoText.Write(writer, file, $"The obfuscated protocol of client {build.Version}, read from {protocol.Assembly} by scripts/schema.cs. Never commit it.");
            }

            Console.WriteLine($"Text: {string.Join(", ", files.Select(file => Path.Combine(protoDirectory, file.Name)))}");
        }

        return 0;
    }
}
