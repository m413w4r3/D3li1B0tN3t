using D3B.Data.Packs;
using D3B.Data.Tests.Packs;
using D3B.Data.Texts;

namespace D3B.Data.Tests.Texts;

public sealed class GameTextsTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;

    public GameTextsTests()
    {
        _pack = TestPacks.Open(_directory, TestPacks.Write(writer =>
        {
            writer.Add(TextTable.EntryName(TextLanguages.French), new TextTable(TextLanguages.French, [10, 20], ["Frêne", "Blé"]));
            writer.Add(TextTable.EntryName(TextLanguages.English), new TextTable(TextLanguages.English, [10, 20], ["Ash", "Wheat"]));
        }));
    }

    public void Dispose()
    {
        _pack.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public void ATextIsReadInTheLanguageAskedFor()
    {
        var texts = new GameTexts(_pack);

        Assert.Equal("Frêne", texts.Get(TextLanguages.French, 10));
        Assert.Equal("Wheat", texts.Get(TextLanguages.English, 20));
    }

    [Fact]
    public void AMissingTextIsReported()
    {
        var texts = new GameTexts(_pack);

        Assert.False(texts.TryGet(TextLanguages.French, 30, out _));
        Assert.Throws<KeyNotFoundException>(() => texts.Get(TextLanguages.French, 30));
    }

    [Fact]
    public void ALanguageThePackLacksIsReported()
    {
        var texts = new GameTexts(_pack);

        Assert.Throws<KeyNotFoundException>(() => texts.Get("de", 10));
    }
}
