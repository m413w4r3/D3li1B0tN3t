using System.CommandLine;
using D3B.Diagnostic.Schema;
using D3B.Protocol.Mapping;

namespace D3B.Scripts.Diagnose;

internal static class SchemaCommand
{
    public static Command Create()
    {
        var command = new Command("schema", "Export a compact redacted diagnostic schema.");
        var schema = new Option<FileInfo>("--schema") { Required = true, Description = "The installed build's schema-1.binpb." };
        var version = new Option<string>("--client-version") { Required = true, Description = "Client build version associated with the schema." };
        var output = new Option<FileInfo?>("--out") { Description = "Write compact schema JSON to this path; stdout by default." };
        command.Options.Add(schema);
        command.Options.Add(version);
        command.Options.Add(output);
        command.SetAction(result => DiagnoseIO.Run(() =>
        {
            var schemaFile = result.GetValue(schema)!;
            if (!schemaFile.Exists) throw new DiagnoseIO.DiagnoseFailure("schema missing");
            var projection = DiagnoseIO.Stage("schema invalid", () => DiagnosticSchemaExporter.Export(result.GetValue(version)!, ProtocolSchema.Load(schemaFile.FullName)));
            DiagnoseIO.Write(result.GetValue(output), projection.ToJson());
        }));
        return command;
    }
}
