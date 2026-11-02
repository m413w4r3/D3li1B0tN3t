namespace D3B.Bot.Settings;

/// <summary>Why a setting's value does not hold, which the app says in its own language.</summary>
public enum SettingProblemKind
{
    /// <summary>The setting has no value, and needs one.</summary>
    Required,

    /// <summary>The number is out of its bounds.</summary>
    OutOfRange,

    /// <summary>The text is longer than its bound.</summary>
    TooLong,

    /// <summary>A value names nothing: no such value of the enumeration, or nothing of the game's with that id.</summary>
    Unknown,

    /// <summary>A list holds a value twice.</summary>
    Repeated,

    /// <summary>A value of the game is not within the one the setting is sought within, such as a sub-area of another area.</summary>
    NotWithin,

    /// <summary>A rule of the activity's does not hold, which says why.</summary>
    Rule,
}
