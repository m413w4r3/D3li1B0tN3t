using System.Collections.Concurrent;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Inspect;

/// <summary>Names the script of a MonoBehaviour from the MonoScript its type points to.</summary>
/// <remarks>
/// <para>
/// A serialized file lists the MonoScripts its MonoBehaviours use. Character bundles hold theirs; map, data and UI bundles
/// point to dedicated bundles whose names contain <c>monoscripts</c>, and UI Toolkit types point to the player's
/// <c>unity default resources</c>.
/// </para>
/// <para>
/// The player's own files carry no type tree, unlike the bundles, so the MonoScripts of <c>unity default resources</c>
/// are read with the MonoScript type tree of a bundle: both come from the same Unity minor version.
/// </para>
/// </remarks>
internal sealed class ScriptResolver
{
    private const string MonoScriptsMarker = "monoscripts";
    private const string DefaultResources = "unity default resources";

    private readonly Client _client;
    private readonly Lazy<Dictionary<(string File, long PathId), string>> _shared;
    private readonly ConcurrentDictionary<(string Bundle, string File, long PathId), string> _local = new();

    public ScriptResolver(Client client)
    {
        _client = client;
        _shared = new Lazy<Dictionary<(string File, long PathId), string>>(LoadSharedScripts);
    }

    private static string NameOf(AssetTypeValueField script)
    {
        var name = script["m_ClassName"].AsString;
        var space = script["m_Namespace"].AsString;

        return space.Length is 0 ? name : $"{space}.{name}";
    }

    private static string FileNameOf(string externalPath)
    {
        // Bundled files are named archive:/CAB-hash/CAB-hash, player files by their path under Dofus_Data.
        return externalPath[(externalPath.LastIndexOf('/') + 1)..];
    }

    private static void AddScripts(OpenedBundle opened, Dictionary<(string File, long PathId), string> scripts, ref TypeTreeType? monoScriptType)
    {
        foreach (var file in opened.Files)
        {
            foreach (var info in file.file.GetAssetsOfType(AssetClassID.MonoScript))
                scripts.TryAdd((file.name, info.PathId), NameOf(opened.Manager.GetBaseField(file, info)));

            monoScriptType ??= file.file.Metadata.FindTypeTreeTypeByID((int)AssetClassID.MonoScript);
        }
    }

    private static string ReadLocal(OpenedBundle opened, AssetsFileInstance file, long pathId)
    {
        var info = file.file.GetAssetInfo(pathId);

        return info is null ? $"{file.name}:{pathId}" : NameOf(opened.Manager.GetBaseField(file, info));
    }

    /// <summary>The script of a MonoBehaviour, as <c>Namespace.Class</c>, or where its MonoScript should be.</summary>
    public string Resolve(OpenedBundle opened, AssetsFileInstance file, AssetFileInfo info)
    {
        return Resolve(opened, file, info.GetScriptIndex(file.file));
    }

    /// <summary>The script at <paramref name="index"/> in a file's list of script types.</summary>
    public string Resolve(OpenedBundle opened, AssetsFileInstance file, ushort index)
    {
        var scriptTypes = file.file.Metadata.ScriptTypes;

        if (index >= scriptTypes.Count)
            return "unknown script";

        var pointer = scriptTypes[index];

        if (pointer.FileId is 0)
            return _local.GetOrAdd((opened.Bundle.RelativePath, file.name, pointer.PathId), _ => ReadLocal(opened, file, pointer.PathId));

        var external = FileNameOf(file.file.Metadata.Externals[pointer.FileId - 1].PathName);

        return _shared.Value.TryGetValue((external, pointer.PathId), out var name) ? name : $"{external}:{pointer.PathId}";
    }

    private Dictionary<(string File, long PathId), string> LoadSharedScripts()
    {
        var scripts = new Dictionary<(string File, long PathId), string>();
        TypeTreeType? monoScriptType = null;

        foreach (var bundle in _client.Bundles(MonoScriptsMarker))
        {
            // A lazy value that throws throws again on every read, so a bad bundle here would fail every bundle that
            // uses a shared script; its scripts are left unnamed instead.
            try
            {
                using var opened = new OpenedBundle(bundle);

                AddScripts(opened, scripts, ref monoScriptType);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"{bundle.RelativePath}: its scripts stay unnamed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        var defaultResources = Path.Combine(_client.DataDirectory, "Resources", DefaultResources);

        if (monoScriptType is null || !File.Exists(defaultResources))
            return scripts;

        var manager = new AssetsManager();

        try
        {
            var file = manager.LoadAssetsFile(defaultResources, false);
            var template = new AssetTypeTemplateField();

            template.FromTypeTree(monoScriptType);

            foreach (var info in file.file.GetAssetsOfType(AssetClassID.MonoScript))
                scripts.TryAdd((DefaultResources, info.PathId), NameOf(template.MakeValue(file.file.Reader, info.GetAbsoluteByteOffset(file.file), null)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"{DefaultResources}: its scripts stay unnamed: {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            manager.UnloadAll(true);
        }

        return scripts;
    }
}
