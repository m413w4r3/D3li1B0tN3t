using D3B.Data.Packs;

namespace D3B.Data.Tests.Packs;

internal static class TestPacks
{
    // A low level keeps the tests fast; the format does not depend on it.
    public const int Level = 3;

    public static PackInfo Info { get; } = new()
    {
        ClientVersion = "3.7.12.12",
        ClientBuildDate = new DateTimeOffset(2026, 10, 19, 15, 11, 45, TimeSpan.Zero),
        Fingerprint = "0123456789abcdef",
        ExtractorVersion = "0.0.0-test",
    };

    public static SampleRecord Record(int id)
    {
        return new SampleRecord { Id = id, Name = $"Record {id} " + new string('x', 200) };
    }

    public static byte[] Write(Action<PackWriter> add)
    {
        using var stream = new MemoryStream();

        using (var writer = new PackWriter(stream, Info, Level))
        {
            add(writer);
            writer.Complete();
        }

        return stream.ToArray();
    }

    public static GamePack Open(TemporaryDirectory directory, byte[] pack)
    {
        var path = directory.Combine($"{Guid.NewGuid():N}.pack");

        File.WriteAllBytes(path, pack);

        return GamePack.Open(path);
    }
}
