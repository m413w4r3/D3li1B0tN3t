#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package System.CommandLine
#:property PublishAot=false
#:project ../src/D3B.Bot/D3B.Bot.csproj
#:project ../src/D3B.Diagnostic/D3B.Diagnostic.csproj
#:project ../src/D3B.Protocol/D3B.Protocol.csproj
#:include diagnose/DiagnoseCommand.cs
#:include diagnose/DiagnoseIO.cs
#:include diagnose/SchemaCommand.cs
#:include diagnose/ReplayCommand.cs
#:include diagnose/ReplayEngineFallback.cs
#:include diagnose/TypesCommand.cs

using System.Globalization;
using D3B.Scripts.Diagnose;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return DiagnoseCommand.Create().Parse(args).Invoke();
