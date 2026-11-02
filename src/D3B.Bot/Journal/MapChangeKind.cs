namespace D3B.Bot.Journal;

/// <summary>How a map change goes to the next map.</summary>
public enum MapChangeKind
{
    /// <summary>By a side of the map, to its neighbour.</summary>
    Scroll,

    /// <summary>By walking onto a map action's cell.</summary>
    MapAction,

    /// <summary>By using a door.</summary>
    Door,

    /// <summary>By teleporting to a zaap.</summary>
    Zaap,

    /// <summary>By leaving the haven bag, back to the map it was entered from.</summary>
    HavenBag,

    /// <summary>Through an NPC's dialog, such as Incarnam's portal (D253).</summary>
    Npc,
}
