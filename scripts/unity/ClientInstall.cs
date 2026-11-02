using System.Security.Cryptography;
using System.Text;
using D3B.Data;

namespace D3B.Scripts.Unity;

/// <summary>The installed official client, as the Ankama launcher lays it out.</summary>
internal sealed class ClientInstall
{
    /// <summary>The installation directory, which holds the launcher's <c>manifest.json</c>.</summary>
    public string Root { get; }

    /// <summary>The directory that holds every bundle.</summary>
    public string StreamingAssets => Path.Combine(Root, "Dofus_Data", "StreamingAssets");

    /// <summary>The client's version file.</summary>
    public string VersionFile => Path.Combine(StreamingAssets, "version");

    /// <summary>The selected platform's GameAssembly binary.</summary>
    public string GameAssembly => Platform == ClientPlatform.Windows
        ? Path.Combine(Root, "GameAssembly.dll")
        : Path.Combine(Root, "GameAssembly.so");

    /// <summary>The selected platform's Unity player binary.</summary>
    public string UnityPlayer => Platform == ClientPlatform.Windows
        ? Path.Combine(Root, "UnityPlayer.dll")
        : Path.Combine(Root, "UnityPlayer.so");

    /// <summary>The IL2CPP global metadata file.</summary>
    public string Metadata => Path.Combine(Root, "Dofus_Data", "il2cpp_data", "Metadata", "global-metadata.dat");

    /// <summary>The platform detected from the installed GameAssembly binary.</summary>
    public ClientPlatform Platform
    {
        get
        {
            var windowsGameAssembly = Path.Combine(Root, "GameAssembly.dll");
            var linuxGameAssembly = Path.Combine(Root, "GameAssembly.so");
            var hasWindowsGameAssembly = File.Exists(windowsGameAssembly);
            var hasLinuxGameAssembly = File.Exists(linuxGameAssembly);

            if (hasWindowsGameAssembly == hasLinuxGameAssembly)
            {
                throw new InvalidOperationException(
                    $"Cannot determine the client's platform: exactly one of '{windowsGameAssembly}' and '{linuxGameAssembly}' must exist.");
            }

            return hasWindowsGameAssembly ? ClientPlatform.Windows : ClientPlatform.Linux;
        }
    }

    /// <summary>The Addressables platform name for this client.</summary>
    public string AddressablesPlatform => Platform switch
    {
        ClientPlatform.Windows => "StandaloneWindows64",
        ClientPlatform.Linux => "StandaloneLinux64",
        _ => throw new InvalidOperationException($"Unsupported client platform '{Platform}'.")
    };

    /// <summary>The client build parsed from <see cref="VersionFile"/>.</summary>
    public ClientBuild Build => ClientBuild.Read(Root);

    private ClientInstall(string root)
    {
        Root = root;
    }

    /// <summary>Finds the client in <paramref name="directory"/>, or where the launcher installs it by default.</summary>
    /// <exception cref="DirectoryNotFoundException">No client is there.</exception>
    public static ClientInstall Locate(DirectoryInfo? directory)
    {
        var root = directory?.FullName;

        if (root is null)
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // The launcher installs the client for Windows only; elsewhere a copy must be named.
            if (!OperatingSystem.IsWindows() || localApplicationData.Length is 0)
                throw new DirectoryNotFoundException("Name the client's directory with --client.");

            root = Path.Combine(localApplicationData, "Ankama", "Dofus-dofus3");
        }

        var client = new ClientInstall(root);

        if (!Directory.Exists(client.StreamingAssets))
            throw new DirectoryNotFoundException($"No client in {root}: {client.StreamingAssets} does not exist.");

        return client;
    }

    /// <summary>Returns the metadata path when it exists.</summary>
    /// <exception cref="FileNotFoundException">The metadata file is missing.</exception>
    public string RequireMetadata()
    {
        if (!File.Exists(Metadata))
            throw new FileNotFoundException($"The client's metadata file does not exist at '{Metadata}'.", Metadata);

        return Metadata;
    }

    /// <summary>
    /// Computes a SHA-256 fingerprint from UTF-8 lines for Version and Platform, followed in fixed order by the
    /// lowercase SHA-256 of each present GameAssembly, UnityPlayer, and Metadata file. Missing files have no line.
    /// The resulting fingerprint is the lowercase hexadecimal SHA-256 of that text.
    /// </summary>
    public string ComputeFingerprint()
    {
        var input = new StringBuilder();
        input.Append("Version=").Append(Build.Version).Append('\n');
        input.Append("Platform=").Append(Platform).Append('\n');
        AppendFileFingerprint(input, "GameAssembly", GameAssembly);
        AppendFileFingerprint(input, "UnityPlayer", UnityPlayer);
        AppendFileFingerprint(input, "Metadata", Metadata);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString()))).ToLowerInvariant();
    }

    /// <summary>The bundles of a directory under <c>StreamingAssets/Content</c>, in ordinal order of their paths.</summary>
    public IReadOnlyList<string> Bundles(string area, string pattern = "*.bundle")
    {
        var directory = Path.Combine(StreamingAssets, "Content", area);

        return Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, pattern).Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>The path of a file under the installation directory, with forward slashes, as the manifest names it.</summary>
    public string RelativePath(string fullPath)
    {
        return Path.GetRelativePath(Root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static void AppendFileFingerprint(StringBuilder input, string label, string path)
    {
        if (!File.Exists(path))
            return;

        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        input.Append(label).Append('=').Append(hash).Append('\n');
    }
}
