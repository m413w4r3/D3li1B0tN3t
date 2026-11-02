#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package System.CommandLine
#:project ../src/D3B.Network/D3B.Network.csproj
#:project ../src/D3B.Protocol/D3B.Protocol.csproj
#:include capture/CaptureCommand.cs
#:include capture/GameMessages.cs
#:include capture/ProtobufShape.cs
#:include capture/SchemaMatch.cs

using System.Globalization;
using D3B.Scripts.Capture;

// Describes the captures the host records of relayed connections (D37) by their structure: reads, frames and their
// sizes, whether the bytes are TLS, and with --frames the protobuf shape of each frame. With --schema, a schema
// scripts/schema.cs wrote, it says which of the schema's messages read every frame, and names each field. With --messages,
// it lists the messages a game connection's frames carry, in order and with their time, which --mapping names as the
// readable protocol does (D178). Never a value but a type URL: a capture holds an account's login ticket, and what this
// tool prints may go into docs/client.md.
// Usage: dotnet run --file scripts/capture.cs -- [<file or directory>...] [--frames] [--schema <file>] [--messages]
// [--mapping <file>]; --help describes the options.
// Exit code: 0 when every capture was read; 1 when one could not be; 2 when there is none.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return CaptureCommand.Create().Parse(args).Invoke();
