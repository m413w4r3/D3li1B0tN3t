namespace D3B.Storage.Profiles;

/// <summary>Why a profile did not start on a bot.</summary>
public enum ProfileStartFailure
{
    /// <summary>Nothing registers the profile's activity any more: a module disabled, or a Lua script of before D116.</summary>
    ActivityMissing,

    /// <summary>A stored setting no longer reads and took its default: the profile is to be looked at again.</summary>
    SettingsReset,

    /// <summary>The settings do not hold, such as a value the user must give, or a sub-area the pack no longer has.</summary>
    SettingsInvalid,

    /// <summary>The activity was not made, or the bot did not start it, such as one already running.</summary>
    NotStarted,
}
