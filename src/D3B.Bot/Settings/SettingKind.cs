namespace D3B.Bot.Settings;

/// <summary>What a setting holds, which says how the app edits it and how it is checked (D98).</summary>
/// <remarks>
/// The kinds are D3B's: each has its editor in the app, and the game's kinds a picker reading the pack, named in the app's
/// language. A kind of the game is one value or several (<see cref="SettingDefinition.IsList"/>).
/// </remarks>
public enum SettingKind
{
    /// <summary>On or off: a <see cref="bool"/>.</summary>
    Flag,

    /// <summary>A whole number between two bounds: an <see cref="int"/>.</summary>
    Number,

    /// <summary>A text of bounded length: a <see cref="string"/>.</summary>
    Text,

    /// <summary>One of an enumeration's values, or several, each named in the app's language.</summary>
    Choice,

    /// <summary>A profession of the game, by its id.</summary>
    Job,

    /// <summary>An area of the world, by its id.</summary>
    Area,

    /// <summary>A sub-area of the world, by its id, which may be sought within an area.</summary>
    SubArea,

    /// <summary>A resource a profession gathers, by its item's id, which may be sought within a profession.</summary>
    Resource,

    /// <summary>An item of the game, by its id, which the setting may restrict to some types, such as food (D148).</summary>
    Item,

    /// <summary>A monster of the game, by its id, which may be sought within a super race (D236).</summary>
    Monster,

    /// <summary>A family of monsters, a race as the bestiary names them, such as the bouftous, by its id; its super race groups it (D247).</summary>
    MonsterFamily,
}
