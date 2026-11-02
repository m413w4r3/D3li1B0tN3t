using D3B.Data.Characteristics;

namespace D3B.Data.Tests.Characteristics;

public sealed class CharacteristicCatalogTests
{
    [Fact]
    public void ACharacteristicHasItsKeywordAndItsScalingFormula()
    {
        var catalog = TestTables.Characteristics();

        Assert.True(catalog.TryGetCharacteristic(0, out var life));
        Assert.Equal("hitPoints", catalog.KeywordOf(life.Id));
        Assert.Equal("unknown", catalog.KeywordOf(-1));
        Assert.Contains("function main()", catalog.FormulaOf(life.ScaleFormulaId), StringComparison.Ordinal);
        Assert.True(catalog.TryGetCharacteristic(1, out var ap));
        Assert.Equal(Characteristic.NoFormula, ap.ScaleFormulaId);
        Assert.Null(catalog.FormulaOf(ap.ScaleFormulaId));
        Assert.Null(catalog.KeywordOf(2));
    }

    [Fact]
    public void KeywordsOrFormulasOfAnotherCountAreRejected()
    {
        var catalog = TestTables.Characteristics();

        Assert.Throws<ArgumentException>(() => new CharacteristicCatalog(catalog.Characteristics, catalog.Keywords[..2], catalog.FormulaIds, catalog.Formulas));
        Assert.Throws<ArgumentException>(() => new CharacteristicCatalog(catalog.Characteristics, catalog.Keywords, catalog.FormulaIds, ReadOnlyMemory<string>.Empty));
    }
}
