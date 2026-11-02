using MemoryPack;

namespace D3B.Data.Jobs;

/// <summary>The professions and the skills characters use interactive elements with.</summary>
/// <remarks>Each table is sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</remarks>
[MemoryPackable]
public sealed partial class JobCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "jobs/catalog";

    /// <summary>Gets the jobs, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Job> Jobs { get; }

    /// <summary>Gets the skills, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Skill> Skills { get; }

    /// <summary>Initializes a new instance of the <see cref="JobCatalog"/> class.</summary>
    /// <param name="jobs">The jobs, in strictly ascending order of identifier.</param>
    /// <param name="skills">The skills, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public JobCatalog(ReadOnlyMemory<Job> jobs, ReadOnlyMemory<Skill> skills)
    {
        Tables.CheckAscending(jobs.Span, static job => job.Id, nameof(jobs));
        Tables.CheckAscending(skills.Span, static skill => skill.Id, nameof(skills));

        Jobs = jobs;
        Skills = skills;
    }

    /// <summary>Finds a job.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="job">The job, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetJob(int id, out Job job)
    {
        return Tables.TryFind(Jobs.Span, id, static job => job.Id, out job);
    }

    /// <summary>Finds a skill, such as one the server says an element of the map offers.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="skill">The skill, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetSkill(int id, out Skill skill)
    {
        return Tables.TryFind(Skills.Span, id, static skill => skill.Id, out skill);
    }
}
