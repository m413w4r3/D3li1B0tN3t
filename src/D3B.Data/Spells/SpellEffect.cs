using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>What a spell level does: one of its effects, from the client's <c>EffectInstanceDice</c>.</summary>
/// <remarks>
/// <para>
/// The effect itself is <see cref="EffectId"/>, an effect of <see cref="Effects.EffectCatalog"/>: the client calls it the
/// action, and the server announces what happens in a fight by the same id (D178). Its target mask and triggers are text,
/// held once in <see cref="SpellCatalog.Strings"/>, as they come: <c>D3B.Engine</c> parses the masks (D187), and 9d the triggers.
/// </para>
/// <para>
/// The dice are also ids, as the effect says: a monster and its grade for a summon, a spell and its grade for an effect
/// that casts one, a state for an effect that sets one (client.md). Left out, since every effect of client 3.7.12.12 holds
/// the same: the base effect, the target and the modifier, all 0, whether to show 0, never, and the spell, its level's.
/// </para>
/// <para>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellEffect
{
    /// <summary>Gets the effect's own identifier, unique among the spells' effects.</summary>
    public int Uid { get; init; }

    /// <summary>Gets the effect, in <see cref="Effects.EffectCatalog"/>.</summary>
    public ushort EffectId { get; init; }

    /// <summary>Gets the client's <c>order</c>: the effects of a level are listed in ascending order of it.</summary>
    public int Order { get; init; }

    /// <summary>Gets the index of the effect's target mask in <see cref="SpellCatalog.Strings"/>: which fighters it reaches.</summary>
    public int TargetMask { get; init; }

    /// <summary>Gets how many turns the effect lasts: 0 at once, -1 for the whole fight.</summary>
    public sbyte Duration { get; init; }

    /// <summary>Gets the effect's weight when its group is drawn at random, or 0 when it always applies.</summary>
    public float Random { get; init; }

    /// <summary>Gets the group of effects drawn together at random.</summary>
    public short Group { get; init; }

    /// <summary>Gets how the effect is dispelled: 1, 2 or 3 in client 3.7.12.12.</summary>
    public byte Dispellable { get; init; }

    /// <summary>Gets how many turns pass before the effect applies.</summary>
    public byte Delay { get; init; }

    /// <summary>Gets the index of the effect's triggers in <see cref="SpellCatalog.Strings"/>: <c>I</c> at once, or the events that apply it.</summary>
    public int Triggers { get; init; }

    /// <summary>Gets the client's <c>effectElement</c>: -1 for none, 0 to 5 otherwise.</summary>
    public sbyte Element { get; init; }

    /// <summary>Gets the client's <c>effectTriggerDuration</c>: 0 on most effects, from -1 to 63.</summary>
    public int TriggerDuration { get; init; }

    /// <summary>Gets the client's <c>value</c>.</summary>
    public int Value { get; init; }

    /// <summary>Gets the client's <c>diceNum</c>: the least roll, or an id the effect names.</summary>
    public int DiceNum { get; init; }

    /// <summary>Gets the client's <c>diceSide</c>: the most roll, or a grade the effect names.</summary>
    public int DiceSide { get; init; }

    /// <summary>Gets the effect's properties.</summary>
    public SpellEffectProperties Properties { get; init; }

    /// <summary>Gets the cells the effect reaches.</summary>
    public SpellZone Zone { get; init; }
}
