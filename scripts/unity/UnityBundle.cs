using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Unity;

/// <summary>A bundle opened through AssetsTools.NET: its serialized files, the raw bytes of objects, and its <c>.resS</c> streams.</summary>
/// <remarks>One thread reads a bundle at a time; different bundles are read in parallel, one manager each.</remarks>
internal sealed class UnityBundle : IDisposable
{
    private readonly BundleFileInstance _bundle;

    public AssetsManager Manager { get; } = new();

    /// <summary>The bundle's full path.</summary>
    public string Path { get; }

    public IReadOnlyList<AssetsFileInstance> Files { get; }

    public UnityBundle(string path)
    {
        Path = path;

        try
        {
            _bundle = Manager.LoadBundleFile(path, true);

            var entries = _bundle.file.BlockAndDirInfo.DirectoryInfos;
            var files = new List<AssetsFileInstance>();

            // The directory flags say which entries are serialized files; AssetsTools.NET's own guess takes some .resS
            // files for serialized ones (client.md).
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsSerialized)
                    files.Add(Manager.LoadAssetsFileFromBundle(_bundle, i, false));
            }

            Files = files;
        }
        catch
        {
            Manager.UnloadAll(true);

            throw;
        }
    }

    /// <summary>Reads the bytes of an object, as its type tree lays them out.</summary>
    public static byte[] ReadObject(AssetsFileInstance file, AssetFileInfo info)
    {
        var reader = file.file.Reader;

        reader.Position = info.GetAbsoluteByteOffset(file.file);

        return reader.ReadBytes((int)info.ByteSize);
    }

    /// <summary>Reads a range of a stream file of the bundle, such as the <c>.resS</c> that holds texels.</summary>
    /// <param name="streamPath">The path a <c>m_StreamData</c> names, such as <c>archive:/CAB-x/CAB-x.resS</c>.</param>
    /// <param name="offset">Where the range starts in the stream file.</param>
    /// <param name="size">The length of the range.</param>
    public byte[] ReadStream(string streamPath, long offset, int size)
    {
        var name = streamPath[(streamPath.LastIndexOf('/') + 1)..];
        var entries = _bundle.file.BlockAndDirInfo.DirectoryInfos;
        var index = entries.FindIndex(entry => entry.Name == name);

        if (index is -1)
            throw new InvalidDataException($"The bundle holds no {name}.");

        _bundle.file.GetFileRange(index, out var start, out var length);

        if (offset < 0 || offset + size > length)
            throw new InvalidDataException($"{name} holds {length} bytes, not {offset + size}.");

        var reader = _bundle.file.DataReader;

        reader.Position = start + offset;

        return reader.ReadBytes(size);
    }

    public void Dispose()
    {
        Manager.UnloadAll(true);
    }
}
