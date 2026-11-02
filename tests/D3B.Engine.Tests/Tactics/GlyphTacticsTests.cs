using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>Glyphs as the AI imagines them: set by a cast, striking a fighter whose turn begins or ends in them, an aura striking who comes into it (9f4).</summary>
public sealed class GlyphTacticsTests
{
    private const int Glyph = 20;
    private const int Struck = 21;
    private const int Aura = 22;
    private const int Push = 23;
    private const int Defiance = 24;
    private const int Ended = 25;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(5, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(99, 0, 0, '\0', 0, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(401, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(402, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1091, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 20 sets, for 2 rounds, a glyph struck as a turn begins casting 21, 10 fire on an enemy, and a glyph-aura casting
    /// 22, 5 fire on an enemy, both on a cross of 1, as a Féca's glyphs; 23 pushes an enemy 2 cells; 24 sets, for 1 round, a
    /// glyph struck as a turn ends casting 25, 7 fire, on its cell alone.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "C", "a,A"];
        var point = Zone(SpellZoneShape.Point);
        var cross = Zone(SpellZoneShape.Cross, 1);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1020, SpellId = Glyph, Grade = 1, ApCost = 3, MinRange = 0, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 2 },
            new SpellLevel { Id = 1021, SpellId = Struck, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
            new SpellLevel { Id = 1022, SpellId = Aura, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 3, EffectCount = 1 },
            new SpellLevel { Id = 1023, SpellId = Push, Grade = 1, ApCost = 2, MinRange = 1, Range = 3, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 4, EffectCount = 1 },
            new SpellLevel { Id = 1024, SpellId = Defiance, Grade = 1, ApCost = 2, MinRange = 0, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 5, EffectCount = 1 },
            new SpellLevel { Id = 1025, SpellId = Ended, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 6, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 401, TargetMask = 0, Triggers = 2, Duration = 2, DiceNum = Struck, DiceSide = 1, Zone = cross },
            new SpellEffect { Uid = 2, EffectId = 1091, TargetMask = 4, Triggers = 2, Duration = 2, DiceNum = Aura, DiceSide = 1, Zone = cross },
            new SpellEffect { Uid = 3, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 10, DiceSide = 10, Zone = cross },
            new SpellEffect { Uid = 4, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 5, DiceSide = 5, Zone = cross },
            new SpellEffect { Uid = 5, EffectId = 5, TargetMask = 0, Triggers = 2, DiceNum = 2, Zone = point },
            new SpellEffect { Uid = 6, EffectId = 402, TargetMask = 0, Triggers = 2, Duration = 1, DiceNum = Ended, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 7, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 7, DiceSide = 7, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(params Combatant[] others)
    {
        return new ImaginedFight { Map = Open(), Fighters = [Caster(Off(0, 0)), .. others], ActorId = 1, Round = 1 };
    }

    [Fact]
    public void AGlyphCastSetsItsMarksOnTheCellsOfItsZoneItsAuraStrikingTheEnemiesThere()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Glyph), Off(3, 0));

        Assert.Equal([CellMarkKind.TurnStart, CellMarkKind.Aura], after.Marks.Select(mark => mark.Kind));
        Assert.All(after.Marks, mark => Assert.Equal((Off(3, 0), 5, 3, 1L), (mark.Centre, mark.Cells.Length, mark.EndRound, mark.AuthorId)));
        Assert.Equal(95, after.Find(enemy.Id)!.Life);
    }

    [Fact]
    public void AGlyphStrikesTheFighterWhoseTurnBeginsInItAloneThenGoesAsItsAuthorsTurnOfItsLastRoundBegins()
    {
        // Measured (9f4): the glyph's spell struck the fighter whose turn began alone, and the glyph went as its author's turn
        // of the round its duration names began.
        var first = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var second = Enemy(-2, Off(4, 0)) with { Life = 100 };
        var cast = _tactics.Imagination.Cast(Fight(first, second), Level(_spells, Glyph), Off(3, 0));

        var started = _tactics.Imagination.TurnStarted(cast, first.Id, 2);

        Assert.Equal(cast.Find(first.Id)!.Life - 10, started.Find(first.Id)!.Life);
        Assert.Equal(cast.Find(second.Id)!.Life, started.Find(second.Id)!.Life);
        Assert.Equal(2, _tactics.Imagination.TurnStarted(started, 1, 2).Marks.Length);
        Assert.Empty(_tactics.Imagination.TurnStarted(started, 1, 3).Marks);
    }

    [Fact]
    public void APushIntoAnAuraStrikesTheFighterItBrings()
    {
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 100 };
        var glyphed = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Glyph), Off(4, 0)) with { Fighters = [Caster(Off(0, 0)) with { ActionPoints = 6 }, enemy] };

        var pushed = _tactics.Imagination.Cast(glyphed, Level(_spells, Push), Off(1, 0));

        // Measured (9f4): a mark strikes its fighter whole, wherever in its zone it stands.
        Assert.Equal((Off(3, 0), 95), (pushed.Find(enemy.Id)!.Cell, pushed.Find(enemy.Id)!.Life));
    }

    [Fact]
    public void AGlyphStruckAsATurnEndsStrikesTheFighterWhoseTurnEndsThere()
    {
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 100 };
        var cast = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Defiance), Off(2, 0));

        Assert.Equal(100, _tactics.Imagination.TurnStarted(cast, enemy.Id, 2).Find(enemy.Id)!.Life);
        Assert.Equal(93, _tactics.Imagination.TurnEnded(cast, enemy.Id).Find(enemy.Id)!.Life);
    }

    [Fact]
    public void TheEnemiesReplyBeginsWithTheGlyphsOnTheirCellsAndTheJudgeCountsThemBefore()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var start = Fight(enemy);
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Glyph)]).Add(enemy.Id, []) };
        var judge = _tactics.JudgeOf(start, knowledge, TacticWeights.Default);
        var under = _tactics.Imagination.Cast(start, Level(_spells, Glyph), Off(3, 0));
        var away = _tactics.Imagination.Cast(start, Level(_spells, Glyph), Off(-3, 0));

        var replied = _tactics.Replies(under, knowledge, judge);

        Assert.Equal(85, replied.Find(enemy.Id)!.Life);
        Assert.True(judge.Judge(under) > judge.Judge(away) + 10);
    }
}
