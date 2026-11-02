namespace D3B.Bot.Localization;

/// <summary>What a value of a <see cref="GameText"/> names, which the app says in its language.</summary>
public enum GameTextKind
{
    /// <summary>A number, written as the app's language writes it.</summary>
    Number,

    /// <summary>A text written as it is, such as a character's name.</summary>
    Text,

    /// <summary>An item of the game, by its id, named from the pack's texts.</summary>
    Item,

    /// <summary>A map of the game, by its id, said by its coordinates.</summary>
    Map,

    /// <summary>A sub-area of the world, by its id, named from the pack's texts.</summary>
    SubArea,

    /// <summary>A profession, by its id, named from the pack's texts.</summary>
    Job,
}
