using System.Security.Cryptography;
using System.Text;
using D3B.Scripts.Unity;

// This linked-source project is a mission-specific exception to D10's usual no-tests-for-scripts convention.
namespace D3B.Scripts.Tests.Unity;

public sealed class ClientInstallTests
{
    [Fact]
    public void AWindowsClientResolvesItsPathsAndBuild()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Windows);

            var client = ClientInstall.Locate(new DirectoryInfo(root));

            Assert.Equal(root, client.Root);
            Assert.Equal(Path.Combine(root, "Dofus_Data", "StreamingAssets"), client.StreamingAssets);
            Assert.Equal(Path.Combine(client.StreamingAssets, "version"), client.VersionFile);
            Assert.Equal(Path.Combine(root, "GameAssembly.dll"), client.GameAssembly);
            Assert.Equal(Path.Combine(root, "UnityPlayer.dll"), client.UnityPlayer);
            Assert.Equal(Path.Combine(root, "Dofus_Data", "il2cpp_data", "Metadata", "global-metadata.dat"), client.Metadata);
            Assert.Equal(ClientPlatform.Windows, client.Platform);
            Assert.Equal("StandaloneWindows64", client.AddressablesPlatform);
            Assert.Equal("3.7.12.12", client.Build.Version);
            Assert.DoesNotContain(".so", client.GameAssembly, StringComparison.Ordinal);
            Assert.DoesNotContain(".so", client.UnityPlayer, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ALinuxClientResolvesItsPathsAndBuild()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Linux);

            var client = ClientInstall.Locate(new DirectoryInfo(root));

            Assert.Equal(Path.Combine(root, "GameAssembly.so"), client.GameAssembly);
            Assert.Equal(Path.Combine(root, "UnityPlayer.so"), client.UnityPlayer);
            Assert.Equal(ClientPlatform.Linux, client.Platform);
            Assert.Equal("StandaloneLinux64", client.AddressablesPlatform);
            Assert.Equal("3.7.12.12", client.Build.Version);
            Assert.DoesNotContain(".dll", client.GameAssembly, StringComparison.Ordinal);
            Assert.DoesNotContain(".dll", client.UnityPlayer, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AClientWithoutStreamingAssetsNamesTheMissingPath()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var streamingAssets = Path.Combine(root, "Dofus_Data", "StreamingAssets");
            var exception = Assert.Throws<DirectoryNotFoundException>(() => ClientInstall.Locate(new DirectoryInfo(root)));

            Assert.Equal($"No client in {root}: {streamingAssets} does not exist.", exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AnInvalidVersionNamesTheBadValue()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Linux);
            File.WriteAllText(Path.Combine(root, "Dofus_Data", "StreamingAssets", "version"), "Version=3.7.12-beta\n");

            var client = ClientInstall.Locate(new DirectoryInfo(root));
            var exception = Assert.Throws<InvalidDataException>(() => client.Build);

            Assert.Contains("3.7.12-beta", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MetadataIsOptionalUntilItIsRequired()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Linux, includeMetadata: false);

            var client = ClientInstall.Locate(new DirectoryInfo(root));

            Assert.Equal(ClientPlatform.Linux, client.Platform);
            Assert.Equal("3.7.12.12", client.Build.Version);

            var exception = Assert.Throws<FileNotFoundException>(() => client.RequireMetadata());

            Assert.Contains(client.Metadata, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AClientFingerprintIsDeterministicAndChangesWithABinary()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Linux);

            var client = ClientInstall.Locate(new DirectoryInfo(root));
            var first = client.ComputeFingerprint();

            Assert.Equal(first, client.ComputeFingerprint());
            Assert.Contains("3.7.12.12", GetFingerprintInput(client), StringComparison.Ordinal);
            Assert.Contains("Platform=Linux", GetFingerprintInput(client), StringComparison.Ordinal);

            File.WriteAllText(client.GameAssembly, "changed game assembly");

            Assert.NotEqual(first, client.ComputeFingerprint());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AClientFingerprintMatchesTheDocumentedSha256Input()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Windows);

            var client = ClientInstall.Locate(new DirectoryInfo(root));
            var expectedInput = $"Version=3.7.12.12\nPlatform=Windows\nGameAssembly={HashText("game assembly")}\nUnityPlayer={HashText("unity player")}\nMetadata={HashText("metadata")}\n";
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedInput))).ToLowerInvariant();

            Assert.Equal(expected, client.ComputeFingerprint());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PlatformDependentDataRejectsBothClientPlatforms()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Windows);
            File.WriteAllText(Path.Combine(root, "GameAssembly.so"), "linux game assembly");

            var client = ClientInstall.Locate(new DirectoryInfo(root));
            var exception = Assert.Throws<InvalidOperationException>(() => client.Platform);

            Assert.Contains("GameAssembly.dll", exception.Message, StringComparison.Ordinal);
            Assert.Contains("GameAssembly.so", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PlatformDependentDataRejectsAnUnrecognizedClientPlatform()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            CreateFixture(root, ClientPlatform.Windows);
            File.Delete(Path.Combine(root, "GameAssembly.dll"));

            var client = ClientInstall.Locate(new DirectoryInfo(root));
            var exception = Assert.Throws<InvalidOperationException>(() => client.Platform);

            Assert.Contains("GameAssembly.dll", exception.Message, StringComparison.Ordinal);
            Assert.Contains("GameAssembly.so", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    private static void CreateFixture(string root, ClientPlatform platform, bool includeMetadata = true)
    {
        var streamingAssets = Path.Combine(root, "Dofus_Data", "StreamingAssets");
        Directory.CreateDirectory(streamingAssets);
        File.WriteAllText(Path.Combine(streamingAssets, "version"), "Version=3.7.12.12\n");

        var extension = platform == ClientPlatform.Windows ? ".dll" : ".so";
        File.WriteAllText(Path.Combine(root, $"GameAssembly{extension}"), "game assembly");
        File.WriteAllText(Path.Combine(root, $"UnityPlayer{extension}"), "unity player");

        if (includeMetadata)
        {
            var metadataDirectory = Path.Combine(root, "Dofus_Data", "il2cpp_data", "Metadata");
            Directory.CreateDirectory(metadataDirectory);
            File.WriteAllText(Path.Combine(metadataDirectory, "global-metadata.dat"), "metadata");
        }
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string GetFingerprintInput(ClientInstall client)
    {
        return $"Version={client.Build.Version}\nPlatform={client.Platform}\nGameAssembly={HashFile(client.GameAssembly)}\nUnityPlayer={HashFile(client.UnityPlayer)}\nMetadata={HashFile(client.Metadata)}\n";
    }

    private static string HashFile(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
}
