namespace D3B.Storage.Profiles;

/// <summary>A profile: a name, an activity, and its settings, which characters share (D99).</summary>
public sealed class Profile
{
    /// <summary>The greatest length of a profile's name.</summary>
    public const int NameLength = 60;

    /// <summary>Gets the profile's id.</summary>
    public int Id { get; init; }

    /// <summary>Gets the profile's name, unique whatever its case.</summary>
    public string Name { get; init; } = "";

    /// <summary>Gets the id of the activity the profile runs, as its module registered it (D93).</summary>
    public string ActivityId { get; init; } = "";

    /// <summary>Gets the activity's settings, as JSON, a value per setting's key (D98).</summary>
    public string Settings { get; init; } = "{}";

    /// <summary>Gets when the profile was last saved.</summary>
    public DateTimeOffset Updated { get; init; }
}
