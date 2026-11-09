#!/usr/bin/env dotnet
#:package System.CommandLine
#:include preflight/PreflightBlockedException.cs
#:include preflight/PreflightBlockedDetail.cs
#:include preflight/PreflightCommand.cs
#:include preflight/PreflightManifest.cs
#:include preflight/PreflightManifestCodec.cs
#:include preflight/PreflightManifestEntry.cs
#:include preflight/PreflightManifestJsonContext.cs
#:include preflight/PreflightManifestStore.cs
#:include preflight/PreflightFileStatus.cs
#:include preflight/PreflightOperations.cs
#:include preflight/PreflightPath.cs
#:include preflight/PreflightSnapshot.cs
#:include preflight/PreflightUnix.cs

using D3B.Scripts.Preflight;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("ERROR: Linux is required.");
    return 2;
}

return PreflightCommand.Run(args);
