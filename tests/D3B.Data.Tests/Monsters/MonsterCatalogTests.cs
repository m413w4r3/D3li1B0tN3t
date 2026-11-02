using D3B.Data.Monsters;

namespace D3B.Data.Tests.Monsters;

public sealed class MonsterCatalogTests
{
    [Fact]
    public void AFighterFindsItsMonstersGrade()
    {
        var catalog = TestTables.Monsters();

        Assert.True(catalog.TryGetGrade(31, 2, out var grade));
        Assert.Equal((2, 30, 7, 1001), ((int)grade.Level, grade.Characteristics.LifePoints, (int)grade.Characteristics.ActionPoints, grade.StartingSpellLevelId));
        Assert.False(catalog.TryGetGrade(31, 3, out _));
        Assert.False(catalog.TryGetGrade(31, 0, out _));
        Assert.False(catalog.TryGetGrade(32, 1, out _));
        Assert.True(catalog.TryGetGrade(TestTables.Bomb, 1, out var bomb));
        Assert.Equal(10, bomb.Characteristics.LifePoints);
    }

    [Fact]
    public void AMonsterCastsEachSpellAtTheGradeItsOwnGradeSays()
    {
        var catalog = TestTables.Monsters();

        Assert.True(catalog.TryGetMonster(31, out var monster));

        var spell = catalog.SpellsOf(monster)[0];

        Assert.Equal(100, spell.SpellId);
        Assert.Equal(1, catalog.SpellGradeOf(spell, 1));
        Assert.Equal(2, catalog.SpellGradeOf(spell, 2));
        Assert.Equal(0, catalog.SpellGradeOf(spell, 3));
        Assert.Equal([(0, 1.5f)], catalog.RatiosOf(monster).ToArray().Select(ratio => (ratio.CharacteristicId, ratio.Ratio)));
        Assert.True(catalog.TryGetMonster(TestTables.Bomb, out var bomb));
        Assert.True(catalog.SpellsOf(bomb).IsEmpty);
    }

    [Fact]
    public void ARaceNamesItsSuperRace()
    {
        var catalog = TestTables.Monsters();

        Assert.True(catalog.TryGetMonster(31, out var monster));
        Assert.True(catalog.TryGetRace(monster.Race, out var race));
        Assert.True(catalog.TryGetSuperRace(race.SuperRaceId, out var superRace));
        Assert.Equal(11_002, superRace.NameId);
        Assert.False(catalog.TryGetRace(2, out _));
    }

    [Fact]
    public void GradesThatDoNotFollowTheirMonstersAreRejected()
    {
        var catalog = TestTables.Monsters();

        Assert.Throws<ArgumentException>(() => new MonsterCatalog(catalog.Monsters, catalog.Grades[..2], catalog.Spells, catalog.SpellGrades, catalog.Ratios, catalog.Races, catalog.SuperRaces));
        Assert.Throws<ArgumentException>(() => new MonsterCatalog(catalog.Monsters, catalog.Grades, catalog.Spells, catalog.SpellGrades[..1], catalog.Ratios, catalog.Races, catalog.SuperRaces));
    }
}
