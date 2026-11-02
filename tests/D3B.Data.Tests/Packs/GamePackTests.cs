using System.Buffers.Binary;
using D3B.Data.Packs;
using D3B.Data.Texts;

namespace D3B.Data.Tests.Packs;

public sealed class GamePackTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void AWrittenPackReadsBackItsInfoAndEntries()
    {
        var bytes = TestPacks.Write(writer =>
        {
            writer.Add("records/1", TestPacks.Record(1));
            writer.Add(TextTable.EntryName(TextLanguages.French), new TextTable(TextLanguages.French, [1, 5], ["un", "cinq"]));
        });

        using var pack = TestPacks.Open(_directory, bytes);

        Assert.Equal(TestPacks.Info.ClientVersion, pack.Info.ClientVersion);
        Assert.Equal(TestPacks.Info.ClientBuildDate, pack.Info.ClientBuildDate);
        Assert.Equal(TestPacks.Info.Fingerprint, pack.Info.Fingerprint);
        Assert.Equal(TestPacks.Info.ExtractorVersion, pack.Info.ExtractorVersion);
        Assert.Equal(["records/1", "texts/fr"], pack.EntryNames);
        Assert.True(pack.Contains("records/1"));
        Assert.False(pack.Contains("records/2"));

        var record = pack.Read<SampleRecord>("records/1");
        var texts = pack.Read<TextTable>("texts/fr");

        Assert.Equal(TestPacks.Record(1).Name, record.Name);
        Assert.Equal(1, record.Id);
        Assert.Equal(2, texts.Count);
        Assert.True(texts.TryGet(5, out var text));
        Assert.Equal("cinq", text);
    }

    [Fact]
    public void AnEntryIsReadOnlyWhenAskedFor()
    {
        var bytes = TestPacks.Write(writer =>
        {
            writer.Add("records/1", TestPacks.Record(1));
            writer.Add("records/2", TestPacks.Record(2));
        });

        using (var intact = TestPacks.Open(_directory, bytes))
        {
            var second = intact.Entry("records/2");

            bytes[second.Offset + (second.CompressedLength / 2)] ^= 0xFF;
        }

        using var pack = TestPacks.Open(_directory, bytes);

        Assert.Equal(1, pack.Read<SampleRecord>("records/1").Id);
        Assert.Throws<PackFormatException>(() => pack.Read<SampleRecord>("records/2"));
    }

    [Fact]
    public void EachReadGivesANewInstance()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        Assert.NotSame(pack.Read<SampleRecord>("records/1"), pack.Read<SampleRecord>("records/1"));
    }

    [Fact]
    public void TheSameEntriesGiveTheSameBytes()
    {
        static void Add(PackWriter writer)
        {
            writer.Add("records/1", TestPacks.Record(1));
            writer.Add("records/2", TestPacks.Record(2));
        }

        Assert.Equal(TestPacks.Write(Add), TestPacks.Write(Add));
    }

    [Fact]
    public void TheBytesDoNotDependOnHowManyEntriesAreCompressedAtOnce()
    {
        static byte[] Write(int parallelism)
        {
            using var stream = new MemoryStream();

            using (var writer = new PackWriter(stream, TestPacks.Info, TestPacks.Level, parallelism))
            {
                for (var id = 1; id <= 100; id++)
                    writer.Add($"records/{id}", TestPacks.Record(id));

                writer.Complete();
            }

            return stream.ToArray();
        }

        var one = Write(1);

        Assert.Equal(one, Write(8));

        using var pack = TestPacks.Open(_directory, one);

        Assert.Equal(Enumerable.Range(1, 100).Select(id => $"records/{id}"), pack.EntryNames);
        Assert.All(Enumerable.Range(1, 100), id => Assert.Equal(id, pack.Read<SampleRecord>($"records/{id}").Id));
    }

    [Fact]
    public void AFileThatIsNotAPackIsRejected()
    {
        var bytes = new byte[256];

        new Random(42).NextBytes(bytes);

        var exception = Assert.Throws<PackFormatException>(() => TestPacks.Open(_directory, bytes));

        Assert.Contains("not a D3B pack", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileTooShortToBeAPackIsRejected()
    {
        Assert.Throws<PackFormatException>(() => TestPacks.Open(_directory, "D3B-PACK"u8.ToArray()));
    }

    [Fact]
    public void APackOfAnotherFormatIsRejectedAsSuch()
    {
        var bytes = TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1)));

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), GamePack.FormatVersion + 1);

        var exception = Assert.Throws<PackFormatException>(() => TestPacks.Open(_directory, bytes));

        Assert.Contains($"format {GamePack.FormatVersion + 1}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APackWhoseWritingWasCutShortIsRejected()
    {
        using var stream = new MemoryStream();

        // Entries are written once compressed, a few behind the last added: enough of them for some to be on disk.
        using (var writer = new PackWriter(stream, TestPacks.Info, TestPacks.Level, 1))
        {
            for (var id = 1; id <= 10; id++)
                writer.Add($"records/{id}", TestPacks.Record(id));
        }

        Assert.True(stream.Length > PackLayout.HeaderLength + PackLayout.TrailerLength);

        var exception = Assert.Throws<PackFormatException>(() => TestPacks.Open(_directory, stream.ToArray()));

        Assert.Contains("has no index", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADamagedIndexIsRejected()
    {
        var bytes = TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1)));
        var indexOffset = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(bytes.Length - 24));

        bytes[indexOffset + 4] ^= 0xFF;

        Assert.Throws<PackFormatException>(() => TestPacks.Open(_directory, bytes));
    }

    [Fact]
    public void ReadingAMissingEntryThrows()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        Assert.Throws<KeyNotFoundException>(() => pack.Read<SampleRecord>("records/2"));
    }

    [Fact]
    public void AnEntryIsReadOnlyAsTheTypeItWasWrittenAs()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        var exception = Assert.Throws<PackFormatException>(() => pack.Read<TextTable>("records/1"));

        Assert.Contains(typeof(SampleRecord).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryNameIsUnique()
    {
        using var writer = new PackWriter(Stream.Null, TestPacks.Info, TestPacks.Level);

        writer.Add("records/1", TestPacks.Record(1));

        Assert.Throws<ArgumentException>(() => writer.Add("records/1", TestPacks.Record(2)));
    }

    [Fact]
    public void ACompletePackTakesNoMoreEntries()
    {
        using var writer = new PackWriter(Stream.Null, TestPacks.Info, TestPacks.Level);

        writer.Complete();

        Assert.Throws<InvalidOperationException>(() => writer.Add("records/1", TestPacks.Record(1)));
        Assert.Throws<InvalidOperationException>(writer.Complete);
    }

    [Fact]
    public void ADisposedPackIsNotRead()
    {
        var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        pack.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pack.Read<SampleRecord>("records/1"));
    }
}
