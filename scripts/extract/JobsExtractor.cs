using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the jobs and the skills, checked against the items they gather.</summary>
/// <remarks>
/// A harvesting bot relies on a profession's skill naming a job and a resource the client holds, with a type: the
/// extraction fails otherwise. The skills of no profession, <see cref="Job.Base"/>, may name none (client.md).
/// </remarks>
internal static class JobsExtractor
{
    private static readonly string[] _tables = ["jobs", "skills"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static JobCatalog Read(ClientInstall client, ItemCatalog items)
    {
        try
        {
            var jobs = UnityDataTable.ReadById(client, "jobs").Select(record => new Job(record.Int32("id"), record.Int32("nameId"), record.Int32("iconId"))).OrderBy(job => job.Id).ToArray();

            if (!jobs.Any(job => job.Id is Job.Base))
                throw new InvalidDataException($"The client has no job {Job.Base}, which the extractor takes for the job of the skills of no profession.");

            var skills = new List<Skill>();
            var unnamed = 0;

            foreach (var record in UnityDataTable.ReadById(client, "skills"))
            {
                var id = record.Int32("id");
                var job = record.Int32("parentJobId");

                if (!jobs.Any(known => known.Id == job))
                    throw new InvalidDataException($"Skill {id} belongs to job {job}, which the client does not hold.");

                var gathered = GatheredItem(id, job, record.Int32("gatheredRessourceItem"), items, ref unnamed);

                skills.Add(new Skill(id, record.Int32("nameId"), job, gathered, record.Int32("levelMin"), record.Int32("range"), record.Boolean("useRangeInClient")));
            }

            var catalog = new JobCatalog(jobs, skills.OrderBy(skill => skill.Id).ToArray());
            var professions = skills.Count(skill => skill.JobId is not Job.Base && skill.GatheredItemId is not Skill.NoItem);

            Console.WriteLine($"Jobs: {jobs.Length:N0}, {skills.Count:N0} skills, {professions:N0} of which gather a profession's resource; skills of no profession naming an item the client lacks, kept as gathering nothing: {unnamed:N0}");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The jobs' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static int GatheredItem(int skill, int job, int item, ItemCatalog items, ref int unnamed)
    {
        // Both mean nothing in client 3.7.12.12: -1 on 240 skills, 0 on 8 of no profession.
        if (item is Skill.NoItem or 0)
            return Skill.NoItem;

        if (items.TryGetItem(item, out var found))
        {
            if (job is not Job.Base && !items.TryGetType(found.TypeId, out _))
                throw new InvalidDataException($"Skill {skill} of job {job} gathers item {item}, of type {found.TypeId}, which the client does not hold.");

            return item;
        }

        // Skill 260 of client 3.7.12.12, of no profession, names an item the client lacks. A profession's skill naming one
        // would gather what a bot cannot name or place in its sub-areas.
        if (job is not Job.Base)
            throw new InvalidDataException($"Skill {skill} of job {job} gathers item {item}, which the client does not hold.");

        unnamed++;

        return Skill.NoItem;
    }
}
