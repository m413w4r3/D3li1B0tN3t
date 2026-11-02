using D3B.Data.Icons;

namespace D3B.Data.Tests.Icons;

public sealed class IconTableTests
{
    private static readonly int[] _descending = [2, 1];
    private static readonly int[] _twice = [1, 1];
    private static readonly int[] _two = [1, 2];
    private static readonly int[] _one = [1];

    [Fact]
    public void AnIconIsFoundByItsIdentifierWithItsOwnPixels()
    {
        var table = TestTables.Icons().Items;

        Assert.Equal(2, table.Count);
        Assert.True(table.TryGet(38_017, out var ash));
        Assert.Equal(table.IconLength, ash.Length);
        Assert.All(ash.ToArray(), value => Assert.Equal(2, value));
        Assert.True(table.TryGet(34_009, out var wheat));
        Assert.All(wheat.ToArray(), value => Assert.Equal(1, value));
        Assert.False(table.TryGet(38_018, out _));
    }

    [Fact]
    public void EachTableHasItsOwnSize()
    {
        var icons = TestTables.Icons();

        Assert.Equal(64, icons.Items.Size);
        Assert.Equal(64 * 64 * 4, icons.Items.IconLength);
        Assert.Equal(48, icons.Spells.Size);
        Assert.Equal(48 * 48 * 4, icons.Spells.IconLength);
        Assert.True(icons.Spells.TryGet(11_556, out var spell));
        Assert.Equal(48 * 48 * 4, spell.Length);
        Assert.All(spell.ToArray(), value => Assert.Equal(4, value));
    }

    [Fact]
    public void IdentifiersOutOfOrderAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new IconTable(2, _descending, new byte[2 * 16]));
        Assert.Throws<ArgumentException>(() => new IconTable(2, _twice, new byte[2 * 16]));
    }

    [Fact]
    public void PixelsThatAreNotThoseOfEveryIconAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new IconTable(2, _two, new byte[16]));
        Assert.Throws<ArgumentException>(() => new IconTable(2, _one, new byte[16 + 1]));
        Assert.Throws<ArgumentException>(() => new IconTable(3, _one, new byte[16]));
    }

    [Fact]
    public void ASizeAnIconCannotHaveIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IconTable(0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IconTable(-1, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IconTable(23_171, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty));
    }
}
