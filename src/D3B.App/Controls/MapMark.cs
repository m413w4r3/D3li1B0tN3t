namespace D3B.App.Controls;

/// <summary>What the map draws on a cell, each in its own shape and colour, as its legend shows them (D121).</summary>
internal enum MapMark
{
    /// <summary>The bot's character: a filled disc, ringed.</summary>
    Character,

    /// <summary>A resource whose harvest is offered now: a disc.</summary>
    Resource,

    /// <summary>A resource harvested, growing back: a dimmed disc.</summary>
    Harvested,

    /// <summary>An element the server described that is no resource, such as a zaap: a diamond.</summary>
    Element,

    /// <summary>A door of the world graph: a diamond.</summary>
    Door,

    /// <summary>An element of the pack the server did not describe: a small disc.</summary>
    Unknown,

    /// <summary>A non-player character: a triangle.</summary>
    Npc,

    /// <summary>Another bot of D3B on the same map: a ring.</summary>
    Bot,

    /// <summary>A cell that leaves the map: the cell, tinted and outlined.</summary>
    Exit,

    /// <summary>An ally in the character's fight, a summon of its team among them: a square.</summary>
    Ally,

    /// <summary>An enemy in the character's fight: a square.</summary>
    Enemy,

    /// <summary>A challengers' placement cell: the cell, tinted red (D125).</summary>
    ChallengerCell,

    /// <summary>A defenders' placement cell: the cell, tinted blue (D125).</summary>
    DefenderCell,
}
