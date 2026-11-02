using D3B.Data.Banks;
using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Effects;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Data.Tests.Packs;

namespace D3B.Data.Tests;

public sealed class GameTablesTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void EachTableReadsBackFromAPackOnce()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer =>
        {
            writer.Add(ItemCatalog.EntryName, TestTables.Items());
            writer.Add(WeaponCatalog.EntryName, TestTables.Weapons());
            writer.Add(JobCatalog.EntryName, TestTables.Jobs());
            writer.Add(InteractiveCatalog.EntryName, TestTables.Interactives());
            writer.Add(BankCatalog.EntryName, TestTables.Banks());
            writer.Add(IconCatalog.EntryName, TestTables.Icons());
            writer.Add(SpellCatalog.EntryName, TestTables.Spells());
            writer.Add(SpellStateCatalog.EntryName, TestTables.States());
            writer.Add(EffectCatalog.EntryName, TestTables.Effects());
            writer.Add(MonsterCatalog.EntryName, TestTables.Monsters());
            writer.Add(BreedCatalog.EntryName, TestTables.Breeds());
            writer.Add(CharacteristicCatalog.EntryName, TestTables.Characteristics());
        }));

        var tables = new GameTables(pack);

        Assert.Same(tables.Items, tables.Items);
        Assert.Same(tables.Weapons, tables.Weapons);
        Assert.Same(tables.Jobs, tables.Jobs);
        Assert.Same(tables.Interactives, tables.Interactives);
        Assert.Same(tables.Banks, tables.Banks);
        Assert.Same(tables.Icons, tables.Icons);
        Assert.Same(tables.Spells, tables.Spells);
        Assert.Same(tables.States, tables.States);
        Assert.Same(tables.Effects, tables.Effects);
        Assert.Same(tables.Monsters, tables.Monsters);
        Assert.Same(tables.Breeds, tables.Breeds);
        Assert.Same(tables.Characteristics, tables.Characteristics);
        Assert.Equal(TestTables.Items().Items.ToArray(), tables.Items.Items.ToArray());
        Assert.Equal(TestTables.Items().Types.ToArray(), tables.Items.Types.ToArray());
        Assert.Equal(TestTables.Weapons().Weapons.ToArray(), tables.Weapons.Weapons.ToArray());
        Assert.Equal(TestTables.Weapons().Effects.ToArray(), tables.Weapons.Effects.ToArray());
        Assert.Equal(TestTables.Weapons().Types.ToArray(), tables.Weapons.Types.ToArray());
        Assert.Equal(TestTables.Weapons().Zones.ToArray(), tables.Weapons.Zones.ToArray());
        Assert.Equal(TestTables.Jobs().Jobs.ToArray(), tables.Jobs.Jobs.ToArray());
        Assert.Equal(TestTables.Jobs().Skills.ToArray(), tables.Jobs.Skills.ToArray());
        Assert.Equal(TestTables.Interactives().Interactives.ToArray(), tables.Interactives.Interactives.ToArray());
        Assert.Equal(TestTables.Banks().Banks.ToArray(), tables.Banks.Banks.ToArray());
        Assert.Equal(TestTables.Banks().Bankers.ToArray(), tables.Banks.Bankers.ToArray());
        Assert.Equal(TestTables.Banks().Replies.ToArray(), tables.Banks.Replies.ToArray());
        Assert.Equal(TestTables.Icons().Items.Ids.ToArray(), tables.Icons.Items.Ids.ToArray());
        Assert.Equal(TestTables.Icons().Items.Pixels.ToArray(), tables.Icons.Items.Pixels.ToArray());
        Assert.Equal(TestTables.Icons().Jobs.Ids.ToArray(), tables.Icons.Jobs.Ids.ToArray());
        Assert.Equal(TestTables.Icons().Jobs.Pixels.ToArray(), tables.Icons.Jobs.Pixels.ToArray());
        Assert.Equal(48, tables.Icons.Spells.Size);
        Assert.Equal(TestTables.Icons().Spells.Ids.ToArray(), tables.Icons.Spells.Ids.ToArray());
        Assert.Equal(TestTables.Icons().Spells.Pixels.ToArray(), tables.Icons.Spells.Pixels.ToArray());
        Assert.Equal(64, tables.Icons.Monsters.Size);
        Assert.Equal(TestTables.Icons().Monsters.Ids.ToArray(), tables.Icons.Monsters.Ids.ToArray());
        Assert.Equal(TestTables.Icons().Monsters.Pixels.ToArray(), tables.Icons.Monsters.Pixels.ToArray());
        Assert.Equal(TestTables.Spells().Spells.ToArray(), tables.Spells.Spells.ToArray());
        Assert.Equal(TestTables.Spells().Levels.ToArray(), tables.Spells.Levels.ToArray());
        Assert.Equal(TestTables.Spells().Effects.ToArray(), tables.Spells.Effects.ToArray());
        Assert.Equal(TestTables.Spells().PreviewZones.ToArray(), tables.Spells.PreviewZones.ToArray());
        Assert.Equal(TestTables.Spells().Strings.ToArray(), tables.Spells.Strings.ToArray());
        Assert.Equal(TestTables.Spells().Variants.ToArray(), tables.Spells.Variants.ToArray());
        Assert.Equal(TestTables.Spells().Bombs.ToArray(), tables.Spells.Bombs.ToArray());
        Assert.Equal(TestTables.States().States.ToArray(), tables.States.States.ToArray());
        Assert.Equal(TestTables.Effects().Effects.ToArray(), tables.Effects.Effects.ToArray());
        Assert.Equal(TestTables.Monsters().Monsters.ToArray(), tables.Monsters.Monsters.ToArray());
        Assert.Equal(TestTables.Monsters().Grades.ToArray(), tables.Monsters.Grades.ToArray());
        Assert.Equal(TestTables.Monsters().SpellGrades.ToArray(), tables.Monsters.SpellGrades.ToArray());
        Assert.Equal(TestTables.Breeds().StatCosts.ToArray(), tables.Breeds.StatCosts.ToArray());
        Assert.Equal(TestTables.Characteristics().Formulas.ToArray(), tables.Characteristics.Formulas.ToArray());
    }

    [Fact]
    public void AMissingTableIsReportedEachTimeItIsAskedFor()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        var tables = new GameTables(pack);

        Assert.Throws<KeyNotFoundException>(() => tables.Items);
        Assert.Throws<KeyNotFoundException>(() => tables.Items);
        Assert.Throws<KeyNotFoundException>(() => tables.Weapons);
        Assert.Throws<KeyNotFoundException>(() => tables.Jobs);
        Assert.Throws<KeyNotFoundException>(() => tables.Interactives);
        Assert.Throws<KeyNotFoundException>(() => tables.Banks);
        Assert.Throws<KeyNotFoundException>(() => tables.Icons);
        Assert.Throws<KeyNotFoundException>(() => tables.Spells);
        Assert.Throws<KeyNotFoundException>(() => tables.States);
        Assert.Throws<KeyNotFoundException>(() => tables.Effects);
        Assert.Throws<KeyNotFoundException>(() => tables.Monsters);
        Assert.Throws<KeyNotFoundException>(() => tables.Breeds);
        Assert.Throws<KeyNotFoundException>(() => tables.Characteristics);
    }
}
