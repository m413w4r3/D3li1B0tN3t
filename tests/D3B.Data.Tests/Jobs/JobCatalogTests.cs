using D3B.Data.Jobs;

namespace D3B.Data.Tests.Jobs;

public sealed class JobCatalogTests
{
    [Fact]
    public void ASkillNamesItsJobAndWhatItGathers()
    {
        var catalog = TestTables.Jobs();

        Assert.True(catalog.TryGetSkill(6, out var cut));
        Assert.Equal((2, 303, 1), (cut.JobId, cut.GatheredItemId, cut.MinLevel));
        Assert.True(catalog.TryGetJob(cut.JobId, out var lumberjack));
        Assert.Equal((3002, 1), (lumberjack.NameId, lumberjack.IconId));
        Assert.True(catalog.TryGetJob(Job.Base, out var none));
        Assert.Equal(Job.NoIcon, none.IconId);
        Assert.True(catalog.TryGetSkill(184, out var zaap));
        Assert.Equal((Job.Base, Skill.NoItem), (zaap.JobId, zaap.GatheredItemId));
        Assert.False(catalog.TryGetSkill(7, out _));
    }

    [Fact]
    public void ATableOutOfOrderIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new JobCatalog(new Job[] { new(2, 1, 1), new(Job.Base, 1, Job.NoIcon) }, ReadOnlyMemory<Skill>.Empty));
        Assert.Throws<ArgumentException>(() => new JobCatalog(ReadOnlyMemory<Job>.Empty, new Skill[] { new(6, 1, 2, 303, 1, 1, false), new(6, 1, 2, 303, 1, 1, false) }));
    }

    [Fact]
    public void AGatheredItemBelowNoItemIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Skill(6, 1, 2, -2, 1, 1, false));
    }
}
