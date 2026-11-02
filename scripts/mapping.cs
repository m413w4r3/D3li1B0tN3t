#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:project ../src/D3B.Network/D3B.Network.csproj
#:project ../src/D3B.Protocol/D3B.Protocol.csproj
#:include mapping/CaptureFrames.cs
#:include mapping/MappingCommand.cs
#:include mapping/PinProposals.cs
#:include unity/ClientPlatform.cs
#:include unity/ClientInstall.cs

using System.Globalization;
using D3B.Scripts.Mapping;

// Writes the mapping of the installed client's build: which obfuscated message, field and enum value each message of
// D3B's readable protocol is (D11). It matches the readable .proto files of src/D3B.Protocol with the build's schema,
// which scripts/schema.cs writes, from the build's pinned pairs, the anchors its captures give, the fields of every pair,
// and structural scores; then it audits the mapping against both, and writes it only when every readable message and
// field has a counterpart that fits (D43, D44).
// Usage: dotnet run --file scripts/mapping.cs -- [--client <dir> | --client-version <version>] [--data <dir>]
// [--captures <dir>...] [--without-captures] [--json]; --help describes the options.
// The mapping lands in <data>/<client version>/mapping-<format>.binpb, beside the build's pack and schema. It is derived
// from the client, so it is never committed: it ships as a release artifact (D11, D24, D45).
// Exit code: 0 when the mapping is written; 1 when the matching or the audit fails, and nothing is written; 2 when the
// client, the schema or the directory of pins is missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return MappingCommand.Create().Parse(args).Invoke();
