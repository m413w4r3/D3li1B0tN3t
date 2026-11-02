namespace D3B.App.Controls;

/// <summary>What a status says at a glance, drawn in the theme's colour for it.</summary>
internal enum Tone
{
    /// <summary>Nothing to watch: stopped, disconnected, not started.</summary>
    Neutral,

    /// <summary>Goes as it should: connected, stepping, done.</summary>
    Success,

    /// <summary>Waits for something, or for someone: paused, waiting.</summary>
    Warning,

    /// <summary>Busy with something on the way: getting ready, unloading, recovering.</summary>
    Info,

    /// <summary>Went wrong: did not start, failed.</summary>
    Danger,
}
