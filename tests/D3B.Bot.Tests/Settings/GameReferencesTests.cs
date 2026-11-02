using D3B.Bot.Interactives;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Jobs;
using D3B.Data.Monsters;

namespace D3B.Bot.Tests.Settings;

public sealed class GameReferencesTests
{
    private readonly GameReferences _game = TestReferences.Game();

    [Fact]
    public void TheProfessionsAreEveryOneButTheBase()
    {
        Assert.Equal([new(TestReferences.Lumberjack, 202, null), new GameReference(TestReferences.Miner, 224, null)], _game.Options(SettingKind.Job));
        Assert.True(_game.Exists(SettingKind.Job, TestReferences.Miner));
        Assert.False(_game.Exists(SettingKind.Job, 1));
    }

    [Fact]
    public void SubAreasAreOfferedWithinTheirArea()
    {
        Assert.Equal([TestReferences.Amakna, TestReferences.Astrub], _game.Options(SettingKind.Area).Select(area => area.Id));
        Assert.Equal([TestReferences.Forest, TestReferences.Hills, TestReferences.City], _game.Options(SettingKind.SubArea).Select(subArea => subArea.Id));
        Assert.Equal([new(TestReferences.Forest, 110, 5), new GameReference(TestReferences.Hills, 111, 20)], _game.Options(SettingKind.SubArea, TestReferences.Amakna));
        Assert.True(_game.IsWithin(SettingKind.SubArea, TestReferences.City, TestReferences.Astrub));
        Assert.False(_game.IsWithin(SettingKind.SubArea, TestReferences.City, TestReferences.Amakna));
        Assert.False(_game.Exists(SettingKind.SubArea, 404));
    }

    [Fact]
    public void AProfessionsResourcesAreTheItemsItsSkillsGatherByLevel()
    {
        Assert.Equal(
            [new(TestReferences.Ash, 403, 1), new GameReference(TestReferences.Chestnut, 404, 10)],
            _game.Options(SettingKind.Resource, TestReferences.Lumberjack));
        Assert.Equal([TestReferences.Ash, TestReferences.Chestnut, TestReferences.Iron], _game.Options(SettingKind.Resource).Select(resource => resource.Id));
        Assert.True(_game.IsWithin(SettingKind.Resource, TestReferences.Iron, TestReferences.Miner));
        Assert.False(_game.IsWithin(SettingKind.Resource, TestReferences.Iron, TestReferences.Lumberjack));
        Assert.Empty(_game.Options(SettingKind.Resource, 404));
    }

    [Fact]
    public void TheSubAreasWhereResourcesGrowComeTheMostFirstThenTheLowest()
    {
        var holding = _game.SubAreasHolding([TestReferences.Chestnut, TestReferences.Ash]);

        Assert.Equal([TestReferences.Forest, TestReferences.Hills], holding.Select(subArea => subArea.SubArea.Id));
        Assert.Equal([TestReferences.Chestnut, TestReferences.Ash], holding[0].Resources);
        Assert.Equal([2, 1], holding.Select(subArea => subArea.Maps));
        Assert.Empty(_game.SubAreasHolding([TestReferences.Ash], TestReferences.Astrub));
        Assert.Equal([1_001L, 1_002L], _game.MapsOf(TestReferences.Forest));
    }

    [Fact]
    public void TheSubAreasWhereMonstersLiveComeTheMostFirstThenTheLowest()
    {
        var living = _game.SubAreasLiving([TestReferences.Larva, TestReferences.Tofu, TestReferences.Larva]);

        Assert.Equal([TestReferences.Forest, TestReferences.Hills], living.Select(subArea => subArea.SubArea.Id));
        Assert.Equal([TestReferences.Larva, TestReferences.Tofu], living[0].Monsters);
        Assert.Equal([2, 1], living.Select(subArea => subArea.Maps));
        Assert.Equal([TestReferences.Hills], _game.SubAreasLiving([TestReferences.Larva], TestReferences.Amakna).Skip(1).Select(subArea => subArea.SubArea.Id));
        Assert.Empty(_game.SubAreasLiving([TestReferences.Larva], TestReferences.Astrub));
        Assert.Empty(_game.SubAreasLiving([]));
    }

    [Fact]
    public void TheFamiliesOfferedAreThoseAHunterMeetsAMonsterOf()
    {
        // Tofus are of family 10, larvae of family 20, of super races 1 and 2; family 30's monsters live nowhere.
        var monsters = new MonsterCatalog(
            new Monster[] { new() { Id = TestReferences.Tofu, Race = 10, FirstGrade = 0, GradeCount = 1 }, new() { Id = TestReferences.Larva, Race = 20, FirstGrade = 1, GradeCount = 1 } },
            new MonsterGrade[] { new() { Grade = 1, Level = 5 }, new() { Grade = 1, Level = 10 } },
            Array.Empty<MonsterSpell>(),
            Array.Empty<byte>(),
            Array.Empty<MonsterRatio>(),
            new[] { new MonsterRace(10, 1, 110), new MonsterRace(20, 2, 120), new MonsterRace(30, 2, 130) },
            new[] { new MonsterRace(1, MonsterRace.NoSuperRace, 101), new MonsterRace(2, MonsterRace.NoSuperRace, 102) });
        var game = new GameReferences(TestReferences.Atlas(), TestReferences.Jobs(), TestReferences.Items(), monsters: monsters);

        Assert.Equal([new(10, 110, null), new GameReference(20, 120, null)], game.Options(SettingKind.MonsterFamily));
        Assert.True(game.Exists(SettingKind.MonsterFamily, 20));
        Assert.False(game.Exists(SettingKind.MonsterFamily, 30));
        Assert.Equal([TestReferences.Larva], game.MonstersOfFamily(20));
        Assert.True(game.TryGetSuperRaceOfFamily(20, out var superRace));
        Assert.Equal(2, superRace);
        Assert.Equal([TestReferences.Larva], game.Options(SettingKind.Monster, 2).Select(monster => monster.Id));
    }

    [Fact]
    public void AResourcesSourceIsTheProfessionThatGathersItAtItsLowestLevel()
    {
        Assert.True(_game.TryGetSource(TestReferences.Chestnut, out var source));
        Assert.Equal(new ResourceSource(TestReferences.Lumberjack, 10), source);
        Assert.False(_game.TryGetSource(9_999, out _));
    }

    [Fact]
    public void OnlyTheGamesKindsHaveOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _game.Options(SettingKind.Number));
        Assert.False(_game.Exists(SettingKind.Flag, 1));
        Assert.False(_game.IsWithin(SettingKind.Area, TestReferences.Amakna, TestReferences.Amakna));
    }

    [Fact]
    public async Task BasesOwnResourcesAreOfferedWithoutALevelAndPlacedByTheLearnedGraphics()
    {
        const int well = 102;
        const int water = 311;
        await using var play = new TestPlay();
        var jobs = new JobCatalog(play.Tables.Jobs.Jobs, play.Tables.Jobs.Skills.ToArray().Append(new Skill(well, 0, Job.Base, water, 1, 1, false)).OrderBy(skill => skill.Id).ToArray());
        var graphics = new GraphicSkills();
        var game = new GameReferences(play.World.Atlas, jobs, play.Tables.Items, play.World.Graph, play.Places, graphics);

        Assert.Contains(game.Options(SettingKind.Resource), resource => resource.Id == water && resource.Level is null);
        Assert.True(game.Exists(SettingKind.Resource, water));
        Assert.True(game.IsBase(water));
        Assert.False(game.IsBase(TestPlay.Ash));
        Assert.False(game.TryGetSource(water, out _));
        Assert.Empty(game.PlacesOf(water)!);
        Assert.Null(game.PlacesOf(TestPlay.Ash));
        Assert.Empty(game.SubAreasHolding([water]));

        graphics.Learn(TestPlay.TreeGfxId, [well]);

        Assert.Contains(TestPlay.MapId, game.PlacesOf(water)!);
        Assert.Equal([TestPlay.SubAreaId], game.SubAreasHolding([water]).Select(subArea => subArea.SubArea.Id));
        Assert.Equal([water], game.SubAreasHolding([water]).Single().Resources);
    }

    [Fact]
    public void ItemsAreOfferedWholeOrOfSomeTypes()
    {
        var iron = new HashSet<int> { 39 };

        Assert.Equal([TestReferences.Ash, TestReferences.Chestnut, TestReferences.Iron], _game.Options(SettingKind.Item).Select(item => item.Id));
        Assert.Equal([new GameReference(TestReferences.Iron, 412, 1)], _game.ItemsOf(iron));
        Assert.True(_game.Exists(SettingKind.Item, TestReferences.Ash));
        Assert.False(_game.Exists(SettingKind.Item, 9_999));
        Assert.True(_game.IsItemOf(TestReferences.Iron, iron));
        Assert.False(_game.IsItemOf(TestReferences.Ash, iron));
    }
}
