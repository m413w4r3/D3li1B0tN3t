namespace D3B.Scripts.Tests.Preflight;

internal sealed class PreflightTestDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("D3B.Preflight.Tests.").FullName;

    public string Root
    {
        get
        {
            string root = Combine("tree");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public string Combine(string name)
    {
        return System.IO.Path.Combine(Path, name);
    }

    public string WriteFile(string root, string name, byte[] bytes)
    {
        string fullRoot = System.IO.Path.GetFullPath(root);

        if (!System.IO.Path.GetFullPath(Path).Equals(fullRoot, StringComparison.Ordinal)
            && !fullRoot.StartsWith(System.IO.Path.GetFullPath(Path) + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Synthetic fixtures must remain inside their temporary directory.");
        }

        string path = System.IO.Path.Combine(root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
