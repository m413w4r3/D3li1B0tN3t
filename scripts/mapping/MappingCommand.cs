using System.Buffers;
using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using D3B.Protocol.Readable;
using D3B.Protocol.Translation;
using D3B.Scripts.Unity;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Scripts.Mapping;

/// <summary>The command that matches D3B's readable protocol with a build's schema, audits the result and writes the mapping.</summary>
internal static class MappingCommand
{
    public static RootCommand Create()
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = @"The client whose build to map. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
        };
        var version = new Option<string?>("--client-version")
        {
            Description = "The client version to map, instead of the installed client's.",
        };
        var data = new Option<DirectoryInfo?>("--data")
        {
            Description = @"The directory packs, schemas and mappings are kept in, one directory per client version. Defaults to %LOCALAPPDATA%\D3B\data.",
        };
        var pins = new Option<DirectoryInfo?>("--pins")
        {
            Description = "The directory of pinned pairs, one <client version>.json per build. Defaults to src/D3B.Protocol/Pins.",
        };
        var captures = new Option<string[]>("--captures")
        {
            Description = @"Captures of the build, which the anchors are measured in: directories as the host writes them, the game's in their game directory, or files. Defaults to %LOCALAPPDATA%\D3B\captures.",
            AllowMultipleArgumentsPerToken = true,
        };
        var withoutCaptures = new Option<bool>("--without-captures")
        {
            Description = "Matches without anchors, from pins and structure alone: measures how far they go.",
        };
        var json = new Option<bool>("--json")
        {
            Description = "Also prints the mapping as JSON, to read it. Derived from the client: never commit it.",
        };
        var command = new RootCommand("Matches D3B's readable protocol with a Dofus 3 build's obfuscated schema and writes the build's mapping.")
        {
            client,
            version,
            data,
            pins,
            captures,
            withoutCaptures,
            json,
        };

        command.SetAction(result => Run(
            result.GetValue(client),
            result.GetValue(version),
            result.GetValue(data)?.FullName ?? PackLocations.DefaultRoot,
            result.GetValue(pins)?.FullName ?? DefaultPins(),
            result.GetValue(withoutCaptures) ? [] : result.GetValue(captures) is { Length: > 0 } paths ? paths : [RelaySettings.DefaultCapturesDirectory],
            result.GetValue(json)));

        return command;
    }

    private static string DefaultPins()
    {
        var scripts = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Path.Combine(Directory.GetCurrentDirectory(), "scripts");

        return Path.GetFullPath(Path.Combine(scripts, "..", "src", "D3B.Protocol", "Pins"));
    }

    private static int Run(DirectoryInfo? clientDirectory, string? version, string root, string pinsDirectory, string[] capturePaths, bool json)
    {
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

        var schemaPath = SchemaFile.PathOf(root, build.Version);

        if (!File.Exists(schemaPath))
        {
            Console.Error.WriteLine($"No schema of client {build.Version} in {schemaPath}: write it with dotnet run --file scripts/schema.cs.");

            return 2;
        }

        // Without its directory, the pins a build needs would go unread, and a field they pair be paired by its number.
        if (!Directory.Exists(pinsDirectory))
        {
            Console.Error.WriteLine($"No directory of pins in {pinsDirectory}: name it with --pins.");

            return 2;
        }

        try
        {
            return Map(build, root, schemaPath, pinsDirectory, capturePaths, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidProtocolBufferException or DescriptorValidationException or ArgumentException)
        {
            Console.Error.WriteLine($"The mapping could not be made: {exception.Message}");

            return 1;
        }
    }

    private static int Map(ClientBuild build, string root, string schemaPath, string pinsDirectory, string[] capturePaths, bool json)
    {
        var stopwatch = Stopwatch.StartNew();
        var schema = ProtocolSchema.Load(schemaPath);
        var pinsPath = PinnedPairsFile.PathOf(pinsDirectory, build.Version);
        var pins = File.Exists(pinsPath) ? PinnedPairsFile.Read(pinsPath, build.Version) : null;
        var failed = false;

        Console.WriteLine($"Client {build.Version}; schema {schemaPath}, SHA-256 {Convert.ToHexStringLower([.. schema.Sha256])}");
        Console.WriteLine(pins is null
            ? $"Pins: none, no {pinsPath}"
            : $"Pins: {pinsPath}, {pins.Messages.Count} messages and {pins.Enums.Count} enums");

        var anchors = new List<Anchor>();
        var payloads = new List<CapturedPayload>();
        var game = new List<CapturedConnection>();

        if (capturePaths.Length is 0)
        {
            Console.WriteLine("Captures: not read; matching from pins and structure alone");
        }
        else
        {
            var (connection, captured) = CaptureFrames.Read(capturePaths, build.Version, out var skipped);

            Console.WriteLine($"Captures: {connection.Count} of the connection phase and {captured.Count} of the game, of client {build.Version}; {skipped} skipped");
            failed |= !Measure("Connection phase", connection, schema, ReadableProtocol.ConnectionSchemaFile, ReadableProtocol.ConnectionAnchors, anchors, payloads);
            failed |= !Measure("Game", captured, schema, ReadableProtocol.GameSchemaFile, ReadableProtocol.GameAnchors, anchors, payloads);
            game = captured;
        }

        var matcher = new ProtocolMatcher(ReadableProtocol.Files, schema, ReadableProtocol.Scopes);
        var result = matcher.Match(pins, anchors);

        Console.WriteLine($"Matching: {result.Messages.Count} messages paired, {Evidence(result.Messages.Select(match => match.Evidence))}; {result.Enums.Count} enums, {Evidence(result.Enums.Select(match => match.Evidence))}");

        foreach (var match in result.Messages.Where(match => match.Score is not null))
            Console.WriteLine($"  scored: {match.Readable.FullName} is .{match.Obfuscated.FullName}, {match.Score:F2}");

        foreach (var match in result.Enums.Where(match => match.Score is not null))
            Console.WriteLine($"  scored: {match.Readable.FullName} is .{match.Obfuscated.FullName}, {match.Score:F2}");

        foreach (var proposal in PinProposals.For(result, schema, payloads))
            Console.WriteLine($"  unpaired: {proposal}");

        foreach (var conflict in result.Conflicts)
            Console.WriteLine($"  conflict: {conflict}");

        failed |= result.Conflicts.Count > 0;

        var tool = typeof(ProtocolMatcher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var mapping = result.ToMapping(build.Version, schema.Sha256, ReadableProtocol.Digest, tool);
        var audit = ContractAudit.Audit(ReadableProtocol.Files, schema, mapping);

        Console.WriteLine($"Audit: {audit.Messages} messages, {audit.Fields} fields, {audit.Enums} enums and {audit.Values} values checked; {audit.Failures.Count} failures, {audit.Notes.Count} notes, {audit.Unread.Count} partial messages");

        foreach (var failure in audit.Failures)
            Console.WriteLine($"  failure: {failure}");

        foreach (var note in audit.Notes)
            Console.WriteLine($"  note: {note}");

        foreach (var line in audit.Unread)
            Console.WriteLine($"  partial: {line}");

        // The bot builds what the client sends, whole: a message the captures show the client sending cannot be partial.
        var carriedByClient = payloads.Where(payload => payload.FromClient > 0).Select(payload => payload.Message).ToHashSet();

        foreach (var match in result.Messages.Where(match => carriedByClient.Contains(match.Obfuscated) && match.Readable.GetOptions()?.GetExtension(OptionsExtensions.Partial) is true))
        {
            Console.WriteLine($"  failure: {match.Readable.FullName} is partial, and the captures show the client sending it.");
            failed = true;
        }

        // The host refuses these too (D47): a mapping it would not start on is not written.
        var renumbered = ProtocolTranslation.Renumbered(mapping);

        Console.WriteLine($"Numbers: {(renumbered.Count is 0 ? "every readable field and value has its own number on the wire" : $"{renumbered.Count} renumbered, which D3B does not translate")}");

        foreach (var line in renumbered)
            Console.WriteLine($"  renumbered: {line}");

        failed |= renumbered.Count > 0;

        if (json)
            Console.WriteLine(new JsonFormatter(JsonFormatter.Settings.Default.WithIndentation()).Format(mapping));

        if (failed || !audit.Passed)
        {
            Console.Error.WriteLine("The mapping does not hold, and was not written: pin what the structure leaves open, or fix the readable .proto files.");

            return 1;
        }

        var path = MappingFile.PathOf(root, build.Version);
        var bytes = MappingFile.Write(path, mapping);

        Console.WriteLine($"Mapping: {path}, {bytes.Length:N0} bytes, format {MappingFile.FormatVersion}, in {stopwatch.Elapsed.TotalSeconds:F1} s");
        Console.WriteLine($"SHA-256: {Convert.ToHexStringLower(SHA256.HashData(bytes))}");

        if (game.Count > 0)
            Translate(ProtocolTranslation.Create(schema, mapping), game);

        return 0;
    }

    /// <summary>Measures the anchors of one connection's captures, and says whether nothing contradicts the plan.</summary>
    private static bool Measure(string title, List<CapturedConnection> connections, ProtocolSchema schema, string fileName, AnchorPlan plan, List<Anchor> anchors, List<CapturedPayload> payloads)
    {
        var file = schema.FindFile(fileName) ?? throw new InvalidDataException($"The schema has no {fileName}.");
        var measurement = CaptureAnchors.Measure(connections, file, plan);

        Console.WriteLine($"  {title}: {connections.Count} captures, {measurement.Frames} frames");

        if (measurement.Root is not null)
        {
            Console.WriteLine($"    root: .{measurement.Root.FullName} reads every frame and types {measurement.RootTyped} fields, the next {(measurement.NextTyped < 0 ? "none" : measurement.NextTyped.ToString(CultureInfo.InvariantCulture))}");
            Console.WriteLine($"    anchors: {measurement.Anchors.Count} messages, {measurement.Anchors.Sum(anchor => anchor.Fields.Count)} fields: {string.Join(", ", measurement.Anchors.Select(anchor => anchor.Readable.Name))}");
        }

        if (measurement.Payloads.Count > 0)
            Console.WriteLine($"    carried: {measurement.Payloads.Count} messages in an Any, {measurement.Payloads.Count(payload => payload.FromClient > 0)} by the client and {measurement.Payloads.Count(payload => payload.FromServer > 0)} by the server, each read whole as its type URL names it");

        foreach (var problem in measurement.Problems)
            Console.WriteLine($"    problem: {problem}");

        anchors.AddRange(measurement.Anchors);
        payloads.AddRange(measurement.Payloads);

        // No frame means no anchor, which matching does without; a frame contradicting the plan means the plan or the
        // client changed, and must be looked at before a mapping is trusted.
        return measurement.Frames is 0 || measurement.Problems.Count is 0;
    }

    /// <summary>Decodes every frame of the game's captures through the mapping just written, as the host would (D47).</summary>
    private static void Translate(ProtocolTranslation translation, List<CapturedConnection> game)
    {
        var frames = game.SelectMany(connection => connection.ClientFrames.Concat(connection.ServerFrames)).ToList();
        var decoded = frames.Select(frame => translation.DecodeGame(new ReadOnlySequence<byte>(frame))).ToList();
        var understood = decoded.Count(frame => frame.IsUnderstood);
        var read = decoded.Count(frame => frame.IsRead && !frame.IsUnderstood);
        var lacking = decoded.Where(frame => frame.CarriedType is not null && frame.ReadableType is null).Select(frame => frame.CarriedType).ToList();
        var misread = decoded.Where(frame => !frame.IsUnderstood && !frame.IsRead && (frame.CarriedType is null || frame.ReadableType is not null)).ToList();

        Console.WriteLine($"Translation: {frames.Count} game frames, {understood} understood whole, {read} more read, {lacking.Count} carrying one of {lacking.Distinct().Count()} messages the readable protocol lacks, {misread.Count} not understood");

        foreach (var problem in misread.Select(frame => frame.Problem).Distinct().Take(10))
            Console.WriteLine($"  not understood: {problem}");
    }

    private static string Evidence(IEnumerable<MatchEvidence> evidence)
    {
        var counts = evidence.GroupBy(kind => kind).OrderBy(group => group.Key).Select(group => $"{group.Count()} by {Name(group.Key)}").ToList();

        return counts.Count is 0 ? "none" : string.Join(", ", counts);
    }

    private static string Name(MatchEvidence evidence)
    {
        return evidence switch
        {
            MatchEvidence.Pin => "pin",
            MatchEvidence.Capture => "capture",
            MatchEvidence.Field => "field",
            MatchEvidence.Score => "score",
            _ => "nothing",
        };
    }
}
