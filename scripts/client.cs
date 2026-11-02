#!/usr/bin/env dotnet
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:project ../src/D3B.Network/D3B.Network.csproj
#:include client/ClientCommand.cs
#:include unity/ClientPlatform.cs
#:include unity/ClientInstall.cs

using System.Globalization;
using D3B.Scripts.Client;

// Checks where the client's configuration sends it, Ankama's or D3B's, and gives it Ankama's back (D35).
// Usage: dotnet run --file scripts/client.cs -- [check | restore] [--client <dir>] [--redirect-directory <dir>]
// [--address <address>]; --help describes the options.
// While D3B's host runs, the client's ConfigUrl names a copy of its configuration that sends it to D3B, and the host gives
// it Ankama's back when it stops. After a crash, restore does that by hand, so that clients play without D3B; the host
// sends them to D3B again at its next start.
// Exit code: 0 when the client can connect, or after a restore; 1 when it is sent to D3B and nothing listens there, or
// Ankama's URL is lost; 2 when the client or its version file cannot be read.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return ClientCommand.Create().Parse(args).Invoke();
