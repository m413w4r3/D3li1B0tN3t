#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package Samboy063.Cpp2IL.Core
#:package System.CommandLine
#:property PublishAot=false
#:project ../src/D3B.Data/D3B.Data.csproj
#:project ../src/D3B.Protocol/D3B.Protocol.csproj
#:include schema/ClientCode.cs
#:include schema/CallRegisters.cs
#:include schema/FieldEncoding.cs
#:include schema/FieldKind.cs
#:include schema/IsilCall.cs
#:include schema/IsilCalls.cs
#:include schema/LayoutField.cs
#:include schema/LayoutOneof.cs
#:include schema/MessageLayout.cs
#:include schema/ProtocolReader.cs
#:include schema/ProtocolTypes.cs
#:include schema/ProtoText.cs
#:include schema/SchemaCommand.cs
#:include schema/SchemaSummary.cs
#:include schema/SilentLogWriter.cs
#:include schema/WireEncodings.cs
#:include unity/ClientPlatform.cs
#:include unity/ClientInstall.cs

using System.Globalization;
using D3B.Scripts.Schema;

// Reads the protocol of the installed client from its IL2CPP code with Cpp2IL (D11, D15) and writes its obfuscated
// schema: every message of Ankama.Dofus.Protocol.Connection and Ankama.Dofus.Protocol.Game, its fields, their numbers and
// types, its oneofs and maps, and the enums, as a protobuf FileDescriptorSet. The metadata gives the structure, and each
// message's machine code the exact encoding of every field, which checks it.
// Usage: dotnet run --file scripts/schema.cs -- [--client <dir>] [--output <dir>] [--proto <dir>]
// [--unity-version <version>]; --help describes the options.
// The schema lands in <output>/<client version>/schema-<format>.binpb, beside the build's pack. It is derived from the
// client, so it is never committed: it ships as a release artifact (D11, D24).
// Exit code: 0 on success; 1 when the client's code does not read as protobuf's generated code; 2 when the client is
// missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return SchemaCommand.Create().Parse(args).Invoke();
