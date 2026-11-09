using System.CommandLine;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightCommand
{
    private const int Success = 0;
    private const int Blocked = 1;
    private const int Error = 2;

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("ERROR: command required.");
            return Error;
        }

        RootCommand root = Create();
        ParseResult parse = root.Parse(args);

        if (parse.Errors.Count > 0)
        {
            Console.Error.WriteLine("ERROR: invalid command line.");
            return Error;
        }

        return parse.Invoke();
    }

    public static RootCommand Create()
    {
        var root = new RootCommand("Create, verify, compare, back up, and restore an offline filesystem preflight manifest.");

        Option<string?> snapshotRoot = CreateOption("--root", "Directory tree to snapshot.");
        Option<string?> snapshotOutput = CreateOption("--output", "Private manifest file to create.");
        var snapshot = new Command("snapshot", "Write a deterministic manifest for a tree.");
        snapshot.Options.Add(snapshotRoot);
        snapshot.Options.Add(snapshotOutput);
        snapshot.SetAction(result =>
        {
            return ExecuteSnapshot(result.GetValue(snapshotRoot)!, result.GetValue(snapshotOutput)!);
        });
        root.Subcommands.Add(snapshot);

        Option<string?> verifyRoot = CreateOption("--root", "Directory tree to verify.");
        Option<string?> verifyManifest = CreateOption("--manifest", "Private manifest file to verify against.");
        var verify = new Command("verify", "Verify exact paths, bytes, kinds, modes, and link targets.");
        verify.Options.Add(verifyRoot);
        verify.Options.Add(verifyManifest);
        verify.SetAction(result =>
        {
            return ExecuteVerify(result.GetValue(verifyRoot)!, result.GetValue(verifyManifest)!);
        });
        root.Subcommands.Add(verify);

        Option<string?> compareRoot = CreateOption("--root", "Directory tree to compare.");
        Option<string?> before = CreateOption("--before", "Manifest created before the change.");
        Option<string?> afterOutput = CreateOption("--after-output", "Private manifest file for the current tree.");
        var compare = new Command("compare", "Write the current manifest and report exact equality with a prior one.");
        compare.Options.Add(compareRoot);
        compare.Options.Add(before);
        compare.Options.Add(afterOutput);
        compare.SetAction(result =>
        {
            return ExecuteCompare(result.GetValue(compareRoot)!, result.GetValue(before)!, result.GetValue(afterOutput)!);
        });
        root.Subcommands.Add(compare);

        Option<string?> backupRoot = CreateOption("--root", "Directory tree to back up.");
        Option<string?> backupOutput = CreateOption("--output", "New private backup directory.");
        var backup = new Command("backup", "Create and verify a byte-exact private backup.");
        backup.Options.Add(backupRoot);
        backup.Options.Add(backupOutput);
        backup.SetAction(result =>
        {
            return ExecuteBackup(result.GetValue(backupRoot)!, result.GetValue(backupOutput)!);
        });
        root.Subcommands.Add(backup);

        Option<string?> restoreBackup = CreateOption("--backup", "Verified private backup directory.");
        Option<string?> restoreManifest = CreateOption("--manifest", "Manifest for the backup tree.");
        Option<string?> restoreRoot = CreateOption("--root", "Directory tree to restore.");
        var restore = new Command("restore", "Restore a backup and verify its exact manifest.");
        restore.Options.Add(restoreBackup);
        restore.Options.Add(restoreManifest);
        restore.Options.Add(restoreRoot);
        restore.SetAction(result =>
        {
            return ExecuteRestore(result.GetValue(restoreBackup)!, result.GetValue(restoreManifest)!, result.GetValue(restoreRoot)!);
        });
        root.Subcommands.Add(restore);

        return root;
    }

    private static Option<string?> CreateOption(string name, string description)
    {
        return new Option<string?>(name)
        {
            Description = description,
            Required = true,
            Recursive = true,
        };
    }

    private static int ExecuteSnapshot(string root, string output)
    {
        return RunOperation(() =>
        {
            PreflightManifest manifest = PreflightOperations.SnapshotToFile(root, output);
            Console.WriteLine($"OK snapshot entries={manifest.Entries.Count}");
        });
    }

    private static int ExecuteVerify(string root, string manifestPath)
    {
        return RunOperation(() =>
        {
            PreflightOperations.Verify(root, manifestPath);
            Console.WriteLine("OK verify");
        });
    }

    private static int ExecuteCompare(string root, string before, string afterOutput)
    {
        return RunOperation(() =>
        {
            bool equal = PreflightOperations.Compare(root, before, afterOutput);

            if (equal)
            {
                Console.WriteLine("OK compare");
            }
            else
            {
                Console.Error.WriteLine("BLOCKED: tree_mismatch");
            }

            return equal ? Success : Blocked;
        });
    }

    private static int ExecuteBackup(string root, string output)
    {
        return RunOperation(() =>
        {
            string manifestPath = PreflightOperations.Backup(root, output);
            PreflightManifest manifest = PreflightManifestStore.Read(manifestPath);
            Console.WriteLine($"OK backup entries={manifest.Entries.Count}");
        });
    }

    private static int ExecuteRestore(string backup, string manifest, string root)
    {
        return RunOperation(() =>
        {
            string? superseded = PreflightOperations.RestoreWithResult(backup, manifest, root);

            if (superseded is null)
            {
                Console.WriteLine("OK restore");
            }
            else
            {
                Console.WriteLine($"OK restore superseded={superseded}");
            }
        });
    }

    private static int RunOperation(Func<int> operation)
    {
        try
        {
            return operation();
        }
        catch (PreflightBlockedException exception)
        {
            WriteBlocked(exception);
            return Blocked;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            Console.Error.WriteLine("ERROR: I/O operation failed.");
            return Error;
        }
    }

    private static int RunOperation(Action operation)
    {
        try
        {
            operation();
            return Success;
        }
        catch (PreflightBlockedException exception)
        {
            WriteBlocked(exception);
            return Blocked;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            Console.Error.WriteLine("ERROR: I/O operation failed.");
            return Error;
        }
    }

    private static void WriteBlocked(PreflightBlockedException exception)
    {
        Console.Error.WriteLine($"BLOCKED: {exception.Code}");

        if (exception.Detail is not null)
        {
            Console.Error.WriteLine(exception.Detail.FormatSafeLine());
        }
    }
}
