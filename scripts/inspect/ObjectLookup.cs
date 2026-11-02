using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Inspect;

/// <summary>Finds one object across bundles, by path id or by name.</summary>
internal static class ObjectLookup
{
    /// <summary>
    /// Opens the bundles in order until one holds the object, then calls <paramref name="found"/> while the bundle is open.
    /// </summary>
    /// <param name="bundles">Where to look.</param>
    /// <param name="selector">A path id, or an object's exact name, compared without regard to case.</param>
    /// <param name="found">What to do with the object.</param>
    /// <returns>Whether the object was found.</returns>
    public static bool Find(IReadOnlyList<BundlePath> bundles, string selector, Action<OpenedBundle, AssetsFileInstance, AssetFileInfo> found)
    {
        var isPathId = long.TryParse(selector, out var pathId);

        foreach (var bundle in bundles)
        {
            using var opened = new OpenedBundle(bundle);

            foreach (var file in opened.Files)
            {
                // A number may be a path id or a name, such as the skin 10, so a name is tried when no path id matches.
                var info = (isPathId ? file.file.GetAssetInfo(pathId) : null)
                    ?? file.file.AssetInfos.FirstOrDefault(candidate =>
                        string.Equals(ObjectNames.Read(opened.Manager, file, candidate), selector, StringComparison.OrdinalIgnoreCase));

                if (info is null)
                    continue;

                found(opened, file, info);

                return true;
            }
        }

        return false;
    }
}
