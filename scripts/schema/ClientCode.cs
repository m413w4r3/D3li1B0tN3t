using System.Buffers.Binary;
using Cpp2IL.Core;
using Cpp2IL.Core.Model.Contexts;
using D3B.Scripts.Unity;
using LibCpp2IL.Logging;
using CppUnityVersion = AssetRipper.Primitives.UnityVersion;

namespace D3B.Scripts.Schema;

/// <summary>The installed client's IL2CPP code, opened with Cpp2IL: its metadata, and its machine code lifted on demand.</summary>
/// <remarks>Cpp2IL keeps the client it opened in static state: a process opens one client at a time.</remarks>
internal sealed class ClientCode : IDisposable
{
    private const uint MetadataMagic = 0xFAB11BAF;

    private readonly ApplicationAnalysisContext _application;

    /// <summary>The Unity version the client was built with.</summary>
    public string UnityVersion { get; }

    /// <summary>The version of the client's <c>global-metadata.dat</c>, read from its header.</summary>
    public int MetadataVersion { get; }

    /// <summary>The platform of the client whose code was opened.</summary>
    public ClientPlatform Platform { get; }

    private ClientCode(ApplicationAnalysisContext application, string unityVersion, int metadataVersion, ClientPlatform platform)
    {
        _application = application;
        UnityVersion = unityVersion;
        MetadataVersion = metadataVersion;
        Platform = platform;
    }

    /// <summary>Opens the client's code and metadata.</summary>
    /// <exception cref="FileNotFoundException">The client lacks one of the files IL2CPP code is read from.</exception>
    /// <exception cref="InvalidDataException">The metadata is not IL2CPP metadata.</exception>
    public static ClientCode Open(ClientInstall client, string? unityVersionOverride = null)
    {
        var binary = client.GameAssembly;
        var metadata = client.RequireMetadata();
        var player = client.UnityPlayer;

        foreach (var file in new[] { binary, metadata, player })
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"The client has no {client.RelativePath(file)}.", file);
        }

        var metadataVersion = ReadMetadataVersion(metadata);

        LibLogger.Writer = new SilentLogWriter();

        // Cpp2IL loads plugins from a directory it is given, relative to the current one by default: the tool names one
        // of its own output, which holds none, so that no assembly lying where the tool is run from gets loaded.
        Cpp2IlApi.Init(Path.Combine(AppContext.BaseDirectory, "cpp2il-plugins"));

        CppUnityVersion unity;

        if (unityVersionOverride is null)
        {
            unity = Cpp2IlApi.DetermineUnityVersion(player, Path.Combine(client.Root, "Dofus_Data"));
        }
        else
        {
            try
            {
                unity = CppUnityVersion.Parse(unityVersionOverride);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Invalid Unity version '{unityVersionOverride}': {exception.Message}", exception);
            }
        }

        Cpp2IlApi.InitializeLibCpp2Il(binary, metadata, unity, false);

        var application = Cpp2IlApi.CurrentAppContext ?? throw new InvalidDataException("Cpp2IL opened the client but built no application.");

        return new ClientCode(application, unity.ToString(), metadataVersion, client.Platform);
    }

    private static int ReadMetadataVersion(string path)
    {
        Span<byte> header = stackalloc byte[8];

        using (var stream = File.OpenRead(path))
            stream.ReadExactly(header);

        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != MetadataMagic)
            throw new InvalidDataException($"{path} is not IL2CPP metadata.");

        return BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
    }

    /// <summary>Gets one of the client's assemblies by name.</summary>
    /// <exception cref="InvalidDataException">The client has no assembly of that name.</exception>
    public AssemblyAnalysisContext Assembly(string name)
    {
        return _application.GetAssemblyByName(name) ?? throw new InvalidDataException($"The client has no assembly {name}.");
    }

    public void Dispose()
    {
        Cpp2IlApi.ResetInternalState();
    }
}
