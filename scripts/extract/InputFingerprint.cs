using System.Security.Cryptography;
using System.Text;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>The fingerprint of the client files a pack is extracted from (D24).</summary>
/// <remarks>
/// The SHA-256 of one line per file, <c>path TAB sha1</c>, in ordinal order of the paths. It changes when any file read
/// changes, or when the extractor reads another set of files, and it costs no more than reading the launcher's manifest.
/// </remarks>
internal static class InputFingerprint
{
    public static string Compute(ClientInstall client, LauncherManifest manifest, IEnumerable<string> fullPaths)
    {
        var lines = new StringBuilder();

        foreach (var path in fullPaths.Order(StringComparer.Ordinal))
            lines.Append(client.RelativePath(path)).Append('\t').Append(manifest.Hash(client, path)).Append('\n');

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString())));
    }
}
