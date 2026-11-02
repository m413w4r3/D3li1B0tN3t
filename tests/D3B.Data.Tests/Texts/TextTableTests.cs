using D3B.Data.Texts;

namespace D3B.Data.Tests.Texts;

public sealed class TextTableTests
{
    [Fact]
    public void ATextIsFoundByItsIdentifier()
    {
        var table = new TextTable(TextLanguages.English, [2, 7, 1_200_531], ["two", "seven", "last"]);

        Assert.True(table.TryGet(7, out var text));
        Assert.Equal("seven", text);
        Assert.True(table.TryGet(1_200_531, out text));
        Assert.Equal("last", text);
        Assert.False(table.TryGet(3, out text));
        Assert.Null(text);
    }

    [Fact]
    public void IdentifiersAreStrictlyAscending()
    {
        Assert.Throws<ArgumentException>(() => new TextTable(TextLanguages.French, [1, 1], ["a", "b"]));
        Assert.Throws<ArgumentException>(() => new TextTable(TextLanguages.French, [2, 1], ["a", "b"]));
    }

    [Fact]
    public void EachIdentifierHasOneText()
    {
        Assert.Throws<ArgumentException>(() => new TextTable(TextLanguages.French, [1, 2], ["a"]));
    }

    [Fact]
    public void EachLanguageHasItsOwnEntry()
    {
        Assert.Equal("texts/fr", TextTable.EntryName(TextLanguages.French));
        Assert.Equal("texts/en", TextTable.EntryName(TextLanguages.English));
    }
}
