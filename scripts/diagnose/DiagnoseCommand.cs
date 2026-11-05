using System.CommandLine;

namespace D3B.Scripts.Diagnose;

internal static class DiagnoseCommand
{
    public static RootCommand Create()
    {
        var root = new RootCommand("Offline, redacted protocol diagnostics.");
        root.Subcommands.Add(SchemaCommand.Create());
        root.Subcommands.Add(ReplayCommand.Create());
        root.Subcommands.Add(TypesCommand.Create());
        return root;
    }
}
