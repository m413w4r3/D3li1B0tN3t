#!/usr/bin/env dotnet
#:package AssetsTools.NET
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:include inspect/BundleBlock.cs
#:include inspect/BundleCompression.cs
#:include inspect/BundleEntry.cs
#:include inspect/BundleHeader.cs
#:include inspect/BundlePath.cs
#:include inspect/BundleScan.cs
#:include inspect/BundlesCommand.cs
#:include inspect/Client.cs
#:include inspect/CommonOptions.cs
#:include inspect/DumpCommand.cs
#:include inspect/LayoutCommand.cs
#:include inspect/LayoutWriter.cs
#:include inspect/ObjectLookup.cs
#:include inspect/ObjectNames.cs
#:include inspect/ObjectsCommand.cs
#:include inspect/OpenedBundle.cs
#:include inspect/Platforms.cs
#:include inspect/ScriptResolver.cs
#:include inspect/Sizes.cs
#:include inspect/SummaryCommand.cs
#:include inspect/TextTable.cs
#:include inspect/TextsCommand.cs
#:include inspect/TypeNames.cs
#:include inspect/TypesCommand.cs
#:include inspect/ValueWriter.cs
#:include unity/ClientPlatform.cs
#:include unity/ClientInstall.cs
#:include unity/I18nFile.cs
#:include unity/UnityBundle.cs

using System.CommandLine;
using System.Globalization;
using System.Text;
using D3B.Scripts.Inspect;

// Describes the official client per area: bundles, types, objects, type layouts and texts.
// Usage: dotnet run --file scripts/inspect.cs -- <command> [arguments] [options]; --help describes each command.
// It only reads the client. Its output is derived from the client, so it is never committed: docs/client.md records
// the facts it measures.
// Exit code: 0 on success; 1 when a bundle or a text file could not be read, a text entry is misplaced, or an object
// was not found; 2 when the client is missing.

// Numbers read the same whatever the machine's language, and texts keep their accents whatever the console's code page.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = Encoding.UTF8;

var client = new Option<DirectoryInfo?>("--client")
{
    Description = @"The client's directory. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
    Recursive = true,
};

var root = new RootCommand("Inspects the official Dofus 3 client.")
{
    client,
    SummaryCommand.Create(client),
    BundlesCommand.Create(client),
    TypesCommand.Create(client),
    ObjectsCommand.Create(client),
    DumpCommand.Create(client),
    LayoutCommand.Create(client),
    TextsCommand.Create(client),
};

return root.Parse(args).Invoke();
