using System.CommandLine;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Engine.Tactics;
using D3B.Network.Captures;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;

namespace D3B.Scripts.Fights;

/// <summary>The command that replays the captured fights and compares what D3B foresees with what the server announced.</summary>
internal static class FightsCommand
{
    public static RootCommand Create()
    {
        var paths = new Argument<string[]>("paths")
        {
            Description = @"Game captures, or directories of them. Defaults to %LOCALAPPDATA%\D3B\captures\game, where the host records them.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var data = new Option<DirectoryInfo?>("--data")
        {
            Description = @"The directory of the packs, schemas and mappings, one subdirectory per client version. Defaults to %LOCALAPPDATA%\D3B\data.",
        };
        var version = new Option<string?>("--client-version")
        {
            Description = "The client build the captures are of. Defaults to the installed client's.",
        };
        var details = new Option<bool>("--details")
        {
            Description = "Also lists, for each outcome the formulas do not explain, the spells and effects it came from, by id.",
        };
        var turns = new Option<bool>("--turns")
        {
            Description = "Replays every turn the characters played through the fight's AI instead: its plan against the turn played, and what its judge foresaw against what came (D206).",
        };
        var live = new Option<bool>("--live")
        {
            Description = "Counts what D3B did in the fights instead: who played each, how it ended, and what came of the AI's requests (D206).",
        };
        var command = new RootCommand("Replays the captured fights and compares what D3B.Engine foresees with what the server announced, action by action.")
        {
            paths,
            data,
            version,
            details,
            turns,
            live,
        };

        command.SetAction(result => Run(result.GetValue(paths) ?? [], result.GetValue(data)?.FullName ?? PackLocations.DefaultRoot, result.GetValue(version), result.GetValue(details), result.GetValue(turns), result.GetValue(live)));

        return command;
    }

    private static int Run(string[] paths, string root, string? version, bool details, bool turns, bool live)
    {
        var client = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ankama", "Dofus-dofus3");
        var build = version is null ? ClientBuild.Read(client) : ClientBuild.Parse(version);
        var packPath = PackLocations.PathOf(root, build);
        var schemaPath = SchemaFile.PathOf(root, build.Version);
        var mappingPath = MappingFile.PathOf(root, build.Version);

        foreach (var needed in new[] { packPath, schemaPath, mappingPath })
        {
            if (!File.Exists(needed))
            {
                Console.Error.WriteLine($"{needed} is missing: scripts/extract.cs, scripts/schema.cs and scripts/mapping.cs write the build's files.");

                return 2;
            }
        }

        var gameCaptures = Path.Combine(RelaySettings.DefaultCapturesDirectory, "game");
        var files = (paths.Length is 0 ? [gameCaptures] : paths)
            .SelectMany(path => Directory.Exists(path) ? Directory.GetFiles(path, "*" + CaptureWriter.FileExtension).Order(StringComparer.Ordinal).ToArray() : [path])
            .ToList();

        if (files.Count is 0)
        {
            Console.Error.WriteLine("No game capture to replay. The host records them with --Network:Captures:Enabled true.");

            return 2;
        }

        using var pack = GamePack.Open(packPath);
        var tables = new GameTables(pack);
        var maps = new GameMaps(pack);
        var translation = ProtocolTranslation.Create(ProtocolSchema.Load(schemaPath), MappingFile.Read(mappingPath));
        var bench = new CastBench(tables, maps, details ? Console.Out : null);
        var turnBench = new TurnBench(tables, maps, TacticWeights.Default, TurnSearchLimits.Default);
        var liveBench = new LiveBench();
        var huntBench = live ? new HuntBench(tables, maps) : null;
        var failed = 0;

        Console.WriteLine($"Client {build.Version}; pack format {GamePack.FormatVersion}, mapping format {MappingFile.FormatVersion}; {files.Count} game captures");

        foreach (var file in files)
        {
            try
            {
                if (live)
                {
                    var timed = CapturedFights.TimedMessages(translation, file);

                    liveBench.Replay([.. timed.Select(message => (message.Kind, message.Message))]);
                    huntBench!.Replay(timed);

                    continue;
                }

                var messages = CapturedFights.ServerMessages(translation, file);

                if (turns)
                    turnBench.Replay(messages);
                else
                    bench.Replay(messages);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{file}: {exception.Message}");
                failed++;
            }
        }

        if (live)
        {
            liveBench.Tally.Write(Console.Out);
            huntBench!.Write(Console.Out);
        }
        else if (turns)
            turnBench.Write(Console.Out);
        else
            bench.Tally.Write(Console.Out);

        return failed > 0 ? 1 : 0;
    }
}
