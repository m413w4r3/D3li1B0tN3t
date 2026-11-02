namespace D3B.Storage.Profiles;

/// <summary>A preset an activity gives, once stored as a profile: never stored again, even once its profile is deleted (D168).</summary>
public sealed class StoredPreset
{
    /// <summary>Gets the id of the activity that gives it.</summary>
    public string ActivityId { get; init; } = "";

    /// <summary>Gets what names it among the activity's presets.</summary>
    public string Key { get; init; } = "";

    /// <summary>Gets when it was stored.</summary>
    public DateTimeOffset Stored { get; init; }
}
