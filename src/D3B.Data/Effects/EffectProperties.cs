namespace D3B.Data.Effects;

/// <summary>What the client says of an effect, its booleans of <c>EffectData</c> that bear on fights, one bit each.</summary>
[Flags]
public enum EffectProperties : byte
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>The client's <c>boost</c>: set on 251 of the 884 effects of client 3.7.12.12.</summary>
    Boost = 1,

    /// <summary>The client's <c>active</c>.</summary>
    Active = 2,

    /// <summary>The client's <c>useInFight</c>: set on 31 effects.</summary>
    UseInFight = 4,

    /// <summary>The effect's value is rolled between its dice.</summary>
    UseDice = 8,

    /// <summary>The client's <c>forceMinMax</c>.</summary>
    ForceMinMax = 16,

    /// <summary>The effect's value is a percentage.</summary>
    IsInPercent = 32,
}
