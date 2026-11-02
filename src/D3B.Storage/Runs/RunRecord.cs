using D3B.Bot.Activities;

namespace D3B.Storage.Runs;

/// <summary>A run of an activity, as D3B keeps it for the statistics: who ran it, when, and what it brought (D146).</summary>
public sealed class RunRecord
{
    /// <summary>Gets the run's identifier in the database.</summary>
    public int Id { get; init; }

    /// <summary>Gets the account of the character that ran it.</summary>
    public long AccountId { get; init; }

    /// <summary>Gets the character that ran it.</summary>
    public long CharacterId { get; init; }

    /// <summary>Gets or sets the character's name, as its bot last saw it.</summary>
    public string CharacterName { get; set; } = "";

    /// <summary>Gets the name the activity was started under, such as its profile's, if its starter gave one.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the activity's name.</summary>
    public string Activity { get; init; } = "";

    /// <summary>Gets when the run began.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets or sets when the run was last written: its end once over.</summary>
    public DateTimeOffset LastAt { get; set; }

    /// <summary>Gets or sets when the run ended, once it is over.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Gets or sets how the run ended, once it is over.</summary>
    public ActivityOutcome? Outcome { get; set; }

    /// <summary>Gets or sets the items the run brought, by item.</summary>
    public Dictionary<int, long> Items { get; set; } = [];

    /// <summary>Gets or sets the professions the character gained experience in.</summary>
    public List<RunJob> Jobs { get; set; } = [];

    /// <summary>Gets or sets how many times the run banked.</summary>
    public int Banks { get; set; }

    /// <summary>Gets or sets how many fights against a resource's protector cut it (D144).</summary>
    public int Protectors { get; set; }

    /// <summary>Gets or sets how many aggressions cut it (D144).</summary>
    public int Aggressions { get; set; }
}
