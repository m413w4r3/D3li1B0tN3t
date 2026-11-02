namespace D3B.App.ViewModels.Logs;

/// <summary>What a line of the journal is about, at a glance: the icon it shows, <c>Icon</c> and its name in <c>Themes/Icons.axaml</c>.</summary>
internal enum LogMark
{
    /// <summary>A walk on a map.</summary>
    Walk,

    /// <summary>A map change by a side, a map action or a door.</summary>
    Map,

    /// <summary>A teleport to a zaap.</summary>
    Zaap,

    /// <summary>Leaving the haven bag.</summary>
    Home,

    /// <summary>A use of an element, such as a harvest.</summary>
    Leaf,

    /// <summary>A travel through the world.</summary>
    Route,

    /// <summary>Banking.</summary>
    Bank,

    /// <summary>An activity starting, or the bot resumed.</summary>
    Play,

    /// <summary>An activity interrupted.</summary>
    Pause,

    /// <summary>An activity over.</summary>
    Stop,

    /// <summary>An activity unloading the character.</summary>
    Bag,

    /// <summary>The player taking the character over.</summary>
    Hand,

    /// <summary>A game connection playing the character, or closing.</summary>
    Plug,

    /// <summary>A fight beginning.</summary>
    Swords,

    /// <summary>A problem: an activity failed, or the host's warning or error.</summary>
    Alert,
}
