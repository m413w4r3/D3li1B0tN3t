namespace D3B.Scripts.Inspect;

/// <summary>A bundle of the official client.</summary>
/// <param name="FullPath">Where the bundle is on disk.</param>
/// <param name="RelativePath">Its path under <c>StreamingAssets</c>, with forward slashes.</param>
/// <param name="Area">
/// The directory that groups it: <c>Map/Data</c> for <c>Content/Map/Data</c>, or <c>aa</c> for the Addressables bundles.
/// </param>
internal sealed record BundlePath(string FullPath, string RelativePath, string Area)
{
    public string FileName => Path.GetFileName(RelativePath);
}
