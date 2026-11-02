using System.Collections.Immutable;
using D3B.Data.Monsters;
using D3B.Engine.Fights;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>The fight's fighters as <c>D3B.Engine</c>'s rules and effects see them, from what the server said of each.</summary>
/// <remarks>
/// The bot's fight state feeds the engine (D13, D185): each fighter's cell, team, AP and MP, life, characteristics as the
/// server gave them, states and spells' modifiers from its buffs and those buffs by the spell that gave each, and for a
/// monster its properties from the pack.
/// </remarks>
public static class FightCombatants
{
    private const int Range = 19;
    private const int MaxSummons = 26;
    private const int Dodge = 78;
    private const int Lock = 79;
    private const int MultiplyReceivedDamage = 1163;

    /// <summary>Gets every fighter of a fight as the engine sees it.</summary>
    /// <param name="fight">The fight's state.</param>
    /// <param name="monsters">The pack's monsters, which say whether a monster moves and tackles.</param>
    /// <returns>The fighters, in the order of their ids.</returns>
    public static ImmutableArray<Combatant> Of(FightState fight, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(monsters);

        return [.. fight.Fighters.Values.Select(fighter => Of(fighter, monsters))];
    }

    /// <summary>Gets a fighter as the engine sees it.</summary>
    /// <param name="fighter">The fighter, as the fight's state holds it.</param>
    /// <param name="monsters">The pack's monsters, which say whether a monster moves and tackles.</param>
    /// <returns>The fighter as a combatant.</returns>
    public static Combatant Of(Fighter fighter, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(fighter);
        ArgumentNullException.ThrowIfNull(monsters);

        var properties = fighter.MonsterId is { } id && monsters.TryGetMonster(id, out var monster) ? monster.Properties : default;
        var characteristics = fighter.Characteristics;

        return new Combatant
        {
            Id = fighter.Id,
            Team = (int)fighter.Team,
            Cell = fighter.IsAlive && fighter.Cell is { } cell ? cell : Combatant.NoCell,
            IsAlive = fighter.IsAlive,
            ActionPoints = fighter.ActionPoints,
            MovementPoints = fighter.MovementPoints,
            RangeBonus = characteristics.GetValueOrDefault(Range),
            Lock = characteristics.GetValueOrDefault(Lock),
            Dodge = characteristics.GetValueOrDefault(Dodge),
            Life = fighter.Life,
            MaxLife = fighter.MaxLife,
            MaxSummons = characteristics.TryGetValue(MaxSummons, out var summons) ? summons : null,
            BreedId = fighter.BreedId,
            MonsterId = fighter.MonsterId,
            Grade = fighter.Grade,
            MonsterProperties = properties,
            SummonerId = fighter.SummonerId,
            States = fighter.States,
            Modifiers = fighter.Modifiers,
            Buffs = [.. fighter.Buffs.Where(buff => buff.Kind is FightBuffKind.Boost or FightBuffKind.State or FightBuffKind.SpellModifier).Select(Held)],
            ReceivedDamageShares = [.. fighter.Buffs.Where(buff => buff.ActionId is MultiplyReceivedDamage).Select(buff => buff.Value)],
            Level = fighter.Level ?? 0,
            Characteristics = characteristics,
        };
    }

    private static HeldBuff Held(FightBuff buff)
    {
        // A state's buff names its state by its parameter (D193).
        return buff.Kind is FightBuffKind.State ? new HeldBuff(buff.SpellId, buff.ActionId, buff.Parameter, CasterId: buff.SourceId) : new HeldBuff(buff.SpellId, buff.ActionId, buff.Value, buff.Parameter, buff.SourceId);
    }
}
