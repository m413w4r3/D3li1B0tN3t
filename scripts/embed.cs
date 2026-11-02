#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:project ../src/D3B.Protocol/D3B.Protocol.csproj
#:include embed/EmbedCommand.cs
#:include unity/GitHubCli.cs

using System.Globalization;
using D3B.Scripts.Embed;

// Puts the pack, the obfuscated schema and the protocol mapping of a client build beside a published app, in
// <output>/<version>/, where the app looks once a file is not on this machine (D172). They come from the release
// client-<version>, each checked against its SHA-256, or with --packs from a directory such as %LOCALAPPDATA%\D3B\data.
// Either way they are checked as the host checks them: the pack opens as the build's, and the mapping holds against the
// schema (D48).
// Usage: dotnet run --file scripts/embed.cs -- --client-version <version> --output <dir> [--packs <dir>]; --help describes
// the options. Without --packs it runs the GitHub CLI, gh, which reads the releases with its own credentials or GH_TOKEN.
// Exit code: 0 once the three files are in place; 1 when one is not the build's, does not hold, or gh failed; 2 when one is
// missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return EmbedCommand.Create().Parse(args).Invoke();
