namespace D3B.App.Controls;

/// <summary>The layers of the map, each shown or hidden on its own (D121).</summary>
[Flags]
internal enum MapLayers
{
    /// <summary>Nothing but the cells walked on.</summary>
    None = 0,

    /// <summary>The grid of the cells walked on, and the obstacles.</summary>
    Cells = 1 << 0,

    /// <summary>The character, its path and where it ends.</summary>
    Character = 1 << 1,

    /// <summary>The map's interactive elements, by what the server says of them.</summary>
    Elements = 1 << 2,

    /// <summary>The non-player characters.</summary>
    Npcs = 1 << 3,

    /// <summary>The cells that leave the map, with the neighbours they lead to, map actions and doors.</summary>
    Exits = 1 << 4,

    /// <summary>The other bots of D3B on the map.</summary>
    Bots = 1 << 5,

    /// <summary>The map's zones, when it has several, and its stairs.</summary>
    Zones = 1 << 6,

    /// <summary>What the bot's activity looks at: the elements it wants, the one it goes for, and the exit it heads to (D131).</summary>
    Activity = 1 << 7,

    /// <summary>The character's fight: the teams' placement cells during the placement, then its allies and enemies (D125, D180).</summary>
    Fight = 1 << 8,

    /// <summary>Every layer but the zones, as the page first shows them.</summary>
    Default = Cells | Character | Elements | Npcs | Exits | Bots | Activity | Fight,
}
