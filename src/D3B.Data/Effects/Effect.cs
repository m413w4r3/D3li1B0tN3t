using System.Runtime.InteropServices;

namespace D3B.Data.Effects;

/// <summary>An effect, from the client's <c>EffectsDataRoot</c>: what a spell's effect of its identifier is.</summary>
/// <remarks>
/// <para>
/// Its identifier is the action a spell's effect names, and the one the server announces a fight's actions by (D178): the
/// client's code names 1,029 such actions, and 408 are a spell's effect in client 3.7.12.12 (client.md). The client's
/// description names the effect in each language; its icons, tooltips and the patterns of its theoretical description
/// are left out.
/// </para>
/// <para>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Effect
{
    /// <summary>Gets the effect's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text describing it, by identifier, with the placeholders the client fills; 0 for 27 effects, which have none.</summary>
    public int DescriptionId { get; }

    /// <summary>Gets the characteristic it changes, by identifier, or 0.</summary>
    public int Characteristic { get; }

    /// <summary>Gets how it changes the characteristic: <c>+</c>, <c>-</c> or <c>/</c>, or 0 for none.</summary>
    public char Operator { get; }

    /// <summary>Gets the client's <c>category</c>, from 0 to 9.</summary>
    public byte Category { get; }

    /// <summary>Gets the client's <c>elementId</c>: -1 for none, 0 to 5 otherwise.</summary>
    public sbyte ElementId { get; }

    /// <summary>Gets the client's <c>bonusType</c>: -1, 0 or 1.</summary>
    public sbyte BonusType { get; }

    /// <summary>Gets the client's <c>effectPriority</c>.</summary>
    public int Priority { get; }

    /// <summary>Gets the client's <c>effectPowerRate</c>.</summary>
    public float PowerRate { get; }

    /// <summary>Gets the effect that undoes it, or 0: set on 146 effects.</summary>
    public int OppositeId { get; }

    /// <summary>Gets what the client says of it.</summary>
    public EffectProperties Properties { get; }

    /// <summary>Initializes a new instance of the <see cref="Effect"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="descriptionId">The text describing it.</param>
    /// <param name="characteristic">The characteristic it changes.</param>
    /// <param name="operator">How it changes it.</param>
    /// <param name="category">The client's category.</param>
    /// <param name="elementId">The client's elementId.</param>
    /// <param name="bonusType">The client's bonusType.</param>
    /// <param name="priority">The client's effectPriority.</param>
    /// <param name="powerRate">The client's effectPowerRate.</param>
    /// <param name="oppositeId">The effect that undoes it.</param>
    /// <param name="properties">What the client says of it.</param>
    public Effect(int id, int descriptionId, int characteristic, char @operator, byte category, sbyte elementId, sbyte bonusType, int priority, float powerRate, int oppositeId, EffectProperties properties)
    {
        Id = id;
        DescriptionId = descriptionId;
        Characteristic = characteristic;
        Operator = @operator;
        Category = category;
        ElementId = elementId;
        BonusType = bonusType;
        Priority = priority;
        PowerRate = powerRate;
        OppositeId = oppositeId;
        Properties = properties;
    }
}
