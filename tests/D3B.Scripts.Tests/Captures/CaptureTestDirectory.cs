namespace D3B.Scripts.Tests.Captures;

internal sealed class CaptureTestDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("D3B.Scripts.Tests.").FullName;

    public string Combine(string name)
    {
        return System.IO.Path.Combine(Path, name);
    }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
