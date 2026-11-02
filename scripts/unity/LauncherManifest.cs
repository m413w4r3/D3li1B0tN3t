using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

namespace D3B.Scripts.Unity;

/// <summary>The launcher's <c>manifest.json</c>: the SHA1 of every file of the client, which tells what a pack was made from.</summary>
/// <remarks>
/// The manifest is about 57 MB of JSON: one object per file, with its size, its hash and the hashes of its chunks. It is
/// read as a stream of tokens, keeping only the hash of each file.
/// </remarks>
internal sealed class LauncherManifest
{
    private readonly Dictionary<string, string> _hashes;

    public int Count => _hashes.Count;

    private LauncherManifest(Dictionary<string, string> hashes)
    {
        _hashes = hashes;
    }

    /// <summary>Reads the manifest of a client, or an empty one when the client has none.</summary>
    public static LauncherManifest Load(ClientInstall client)
    {
        var path = Path.Combine(client.Root, "manifest.json");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!File.Exists(path))
            return new LauncherManifest(hashes);

        var reader = new Utf8JsonReader(File.ReadAllBytes(path), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        if (!reader.Read() || reader.TokenType is not JsonTokenType.StartObject)
            throw new InvalidDataException($"{path} is not a JSON object.");

        while (reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
        {
            var file = reader.GetString()!;

            if (!reader.Read() || reader.TokenType is not JsonTokenType.StartObject)
                throw new InvalidDataException($"{path}: the entry of {file} is not an object.");

            var depth = reader.CurrentDepth;

            while (reader.Read() && !(reader.TokenType is JsonTokenType.EndObject && reader.CurrentDepth == depth))
            {
                if (reader.TokenType is JsonTokenType.PropertyName && reader.CurrentDepth == depth + 1 && reader.ValueTextEquals("hash"))
                {
                    reader.Read();
                    hashes[file] = reader.GetString() ?? string.Empty;
                }
                else if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                {
                    reader.Skip();
                }
            }
        }

        return new LauncherManifest(hashes);
    }

    /// <summary>The SHA1 of a file's content, in lowercase hexadecimal: the manifest's, else computed from the file.</summary>
    [SuppressMessage("Security", "CA5350", Justification = "SHA1 is what the launcher's manifest names files by; it identifies content, it protects nothing.")]
    public string Hash(ClientInstall client, string fullPath)
    {
        if (_hashes.TryGetValue(client.RelativePath(fullPath), out var hash) && hash.Length > 0)
            return hash.ToLowerInvariant();

        // A file the manifest does not list, such as in a copy of the client, is hashed here: a pack's fingerprint
        // must follow content, not dates.
        using var stream = File.OpenRead(fullPath);

        return Convert.ToHexStringLower(SHA1.HashData(stream));
    }
}
