using System.Runtime.InteropServices;

namespace D3B.Data.Jobs;

/// <summary>A skill, what a character does with an interactive element, such as cutting an ash, from the client's <c>SkillsDataRoot</c>.</summary>
/// <remarks>
/// The client's data does not say which interactive offers which skill: the server tells, for each element of the map a
/// character stands on (client.md). Unmanaged and packed, as <see cref="Job"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Skill
{
    /// <summary>The <see cref="GatheredItemId"/> of a skill that gathers nothing.</summary>
    public const int NoItem = -1;

    /// <summary>Gets the skill's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier, such as Couper.</summary>
    public int NameId { get; }

    /// <summary>Gets the job it belongs to, or <see cref="Job.Base"/> for none.</summary>
    public int JobId { get; }

    /// <summary>Gets the item it gathers, or <see cref="NoItem"/>.</summary>
    public int GatheredItemId { get; }

    /// <summary>Gets the job level it needs, from 0 to 200.</summary>
    public int MinLevel { get; }

    /// <summary>Gets the client's <c>range</c>: 1 for most skills, 10 for the fisherman's; how the client uses it is not measured.</summary>
    public int Range { get; }

    /// <summary>Gets the client's <c>useRangeInClient</c>, set on 29 skills of client 3.7.12.12, none of the fisherman's; what it means is not measured.</summary>
    public bool UseRangeInClient { get; }

    /// <summary>Initializes a new instance of the <see cref="Skill"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="jobId">Its job, or <see cref="Job.Base"/>.</param>
    /// <param name="gatheredItemId">The item it gathers, or <see cref="NoItem"/>.</param>
    /// <param name="minLevel">The job level it needs.</param>
    /// <param name="range">The client's range.</param>
    /// <param name="useRangeInClient">The client's useRangeInClient.</param>
    /// <exception cref="ArgumentOutOfRangeException">The gathered item is negative but for <see cref="NoItem"/>.</exception>
    public Skill(int id, int nameId, int jobId, int gatheredItemId, int minLevel, int range, bool useRangeInClient)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gatheredItemId, NoItem);

        Id = id;
        NameId = nameId;
        JobId = jobId;
        GatheredItemId = gatheredItemId;
        MinLevel = minLevel;
        Range = range;
        UseRangeInClient = useRangeInClient;
    }
}
