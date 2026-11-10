using D3B.Bot.Characters;
using D3B.Data;
using D3B.Data.Jobs;

namespace D3B.Bot.Harvesting;

/// <summary>A passive check that a known character job meets a resource skill's required level.</summary>
internal sealed record HarvestJob
{
    public int JobId { get; init; }

    public int JobLevel { get; init; }

    public int RequiredLevel { get; init; }

    public int SkillId { get; init; }

    public bool SkillEnabled { get; init; }

    public bool SkillEnabledKnown { get; init; }

    public string Status { get; init; } = "CATALOG_UNKNOWN";

    public string Provenance { get; init; } = "catalog";

    public static HarvestJob? Check(CharacterState state, GameTables tables, int skillId, bool skillEnabled, string provenance)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(provenance);

        if (!tables.Jobs.TryGetSkill(skillId, out var skill) || skill.GatheredItemId == Skill.NoItem)
        {
            return new HarvestJob
            {
                SkillId = skillId,
                SkillEnabled = skillEnabled,
                SkillEnabledKnown = true,
                Status = "CATALOG_UNKNOWN",
                Provenance = provenance,
            };
        }

        return ForSkill(state, skill, skillEnabled, skillEnabledKnown: true, provenance);
    }

    public static HarvestJob? CheckFromCatalog(CharacterState state, GameTables tables, int wantedItemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tables);

        var skills = tables.Jobs.Skills.ToArray()
            .Where(skill => skill.GatheredItemId == wantedItemId)
            .DistinctBy(skill => (skill.JobId, skill.Id))
            .ToArray();

        if (skills.Length == 0)
        {
            return new HarvestJob
            {
                Status = "CATALOG_UNKNOWN",
                Provenance = "catalog",
            };
        }

        if (skills.Length > 1)
        {
            return new HarvestJob
            {
                Status = "CATALOG_AMBIGUOUS",
                Provenance = "catalog",
            };
        }

        return ForSkill(state, skills[0], skillEnabled: false, skillEnabledKnown: false, provenance: "catalog");
    }

    private static HarvestJob ForSkill(CharacterState state, Skill skill, bool skillEnabled, bool skillEnabledKnown, string provenance)
    {
        var jobLevel = 0;
        var status = "JOB_UNKNOWN";

        if (state.Jobs.TryGetValue(skill.JobId, out var job) && job is not null)
        {
            jobLevel = job.Level;
            status = !job.LevelKnown
                ? "JOB_LEVEL_UNKNOWN"
                : job.Level < skill.MinLevel
                    ? "LEVEL_TOO_LOW"
                    : skillEnabledKnown && !skillEnabled
                        ? "SKILL_DISABLED"
                        : "OK";
        }

        return new HarvestJob
        {
            JobId = skill.JobId,
            JobLevel = jobLevel,
            RequiredLevel = skill.MinLevel,
            SkillId = skill.Id,
            SkillEnabled = skillEnabled,
            SkillEnabledKnown = skillEnabledKnown,
            Status = status,
            Provenance = provenance,
        };
    }
}
