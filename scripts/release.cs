#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:project ../src/D3B.Protocol/D3B.Protocol.csproj
#:include unity/GitHubCli.cs
#:include release/ReleaseAsset.cs
#:include release/ReleaseCommand.cs
#:include unity/ClientPlatform.cs
#:include unity/ClientInstall.cs

using System.Globalization;
using D3B.Scripts.Release;

// Publishes the pack, the obfuscated schema and the protocol mapping of a client build as assets of the release
// client-<version> (D24, D33, D50), each with its SHA-256 beside it: the release is created when it does not exist, and
// gains the assets it lacks. A published asset is never replaced. The mapping must hold against the schema, as the host
// requires (D48), before anything is published.
// Usage: dotnet run --file scripts/release.cs -- [--client <dir> | --client-version <version>] [--packs <dir>]
// [--publish]; --help describes the options.
// Without --publish it changes nothing on GitHub: it checks the files, writes their SHA-256 beside them, and says what it
// would run. It runs the GitHub CLI, gh, logged in to an account that can write to the repository.
// Exit code: 0 on success, when the release already holds these files, and after a dry run; 1 when a file is not one to
// publish, the release cannot take it, or gh failed; 2 when the client, the pack, the schema or the mapping is missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return ReleaseCommand.Create().Parse(args).Invoke();
