using AssetsTools.NET.Extra;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Inspect;

/// <summary>A bundle opened through AssetsTools.NET, with its serialized files loaded.</summary>
/// <remarks>
/// An LZMA bundle is decompressed into memory as it opens; an LZ4 bundle is read block by block. The files of one bundle
/// share one reader, so a bundle is read by one thread at a time, while different bundles can be read in parallel.
/// </remarks>
internal sealed class OpenedBundle : IDisposable
{
    private readonly UnityBundle _bundle;

    public BundlePath Bundle { get; }

    public AssetsManager Manager => _bundle.Manager;

    /// <summary>The serialized files of the bundle, usually one.</summary>
    public IReadOnlyList<AssetsFileInstance> Files => _bundle.Files;

    public OpenedBundle(BundlePath bundle)
    {
        Bundle = bundle;
        _bundle = new UnityBundle(bundle.FullPath);
    }

    public void Dispose()
    {
        _bundle.Dispose();
    }
}
