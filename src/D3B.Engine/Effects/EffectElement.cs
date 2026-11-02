namespace D3B.Engine.Effects;

/// <summary>The element of damage, a steal or a heal: each value the pack's and the server's number for it.</summary>
/// <remarks>The server names the element of each loss of life by the same numbers, -1 for a push's (client.md).</remarks>
public enum EffectElement
{
    /// <summary>No element: a push's damage, or an effect that is no damage.</summary>
    None = -1,

    /// <summary>Neutral, raised by strength.</summary>
    Neutral = 0,

    /// <summary>Earth, raised by strength.</summary>
    Earth = 1,

    /// <summary>Fire, raised by intelligence.</summary>
    Fire = 2,

    /// <summary>Water, raised by chance.</summary>
    Water = 3,

    /// <summary>Air, raised by agility.</summary>
    Air = 4,

    /// <summary>The caster's best element: the one its characteristics raise most.</summary>
    Best = 5,

    /// <summary>The caster's worst element.</summary>
    Worst = 6,
}
