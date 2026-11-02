using System.Collections.Frozen;
using D3B.Bot.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Monsters;
using D3B.Data.World;

namespace D3B.Bot.Settings;

/// <summary>The game's values the settings name, read from the installed build's pack: what the app offers, and what a check accepts (D98).</summary>
/// <remarks>
/// <para>
/// Professions are every one but the base, which every character has. A profession's resources are the items its skills
/// gather, each at the lowest level a skill gathers it from; a sub-area's level is the pack's. The resources are theirs and
/// those only skills of <c>Base</c> gather, such as drinking water from a well, with no level (D150).
/// </para>
/// <para>
/// Monsters are those a sub-area lists, but bombs and those the bestiary hides, each of its super race and at its first
/// grade's level (D236).
/// </para>
/// <para>Each list is read once, then shared: the pack does not change while the host runs.</para>
/// </remarks>
public sealed class GameReferences
{
    private readonly WorldAtlas _atlas;
    private readonly GraphicPlaces? _places;
    private readonly GraphicSkills? _graphics;
    private readonly Lazy<IReadOnlyList<GameReference>> _jobs;
    private readonly Lazy<IReadOnlyList<GameReference>> _gathering;
    private readonly Lazy<IReadOnlyList<GameReference>> _areas;
    private readonly Lazy<IReadOnlyList<GameReference>> _subAreas;
    private readonly Lazy<IReadOnlyList<GameReference>> _resources;
    private readonly Lazy<IReadOnlyList<GameReference>> _items;
    private readonly Lazy<Dictionary<int, int>> _itemTypes;
    private readonly Lazy<ILookup<int, GameReference>> _resourcesByJob;
    private readonly Lazy<Dictionary<int, ResourceSource>> _sources;
    private readonly Lazy<FrozenDictionary<int, FrozenSet<int>>> _baseSkills;
    private readonly Lazy<ILookup<int, long>> _maps;
    private readonly Lazy<Dictionary<long, int>> _subAreaOf;
    private readonly Lazy<IReadOnlyList<GameReference>> _superRaces;
    private readonly Lazy<IReadOnlyList<GameReference>> _families;
    private readonly Lazy<Dictionary<int, int>> _familyOfMonster;
    private readonly Lazy<Dictionary<int, int>> _superRaceOfFamily;
    private readonly Lazy<Dictionary<int, int>> _superRaceOfMonster;
    private readonly Lazy<IReadOnlyList<GameReference>> _monsters;

    /// <summary>Gets the professions whose skills gather a resource, <c>Base</c> last, whose resources no profession needs (D150, D165).</summary>
    public IReadOnlyList<GameReference> GatheringJobs => _gathering.Value;

    /// <summary>Gets the monsters' super races, whose monsters a monster setting offers (D236).</summary>
    public IReadOnlyList<GameReference> SuperRaces => _superRaces.Value;

    /// <summary>Initializes a new instance of the <see cref="GameReferences"/> class.</summary>
    /// <param name="atlas">The world's areas and sub-areas.</param>
    /// <param name="jobs">The professions and their skills.</param>
    /// <param name="items">The items, which name the resources.</param>
    /// <param name="graph">The world graph, which says the maps a character may travel to; without it, every map of the atlas counts.</param>
    /// <param name="places">Where each graphic is drawn, which places the resources no sub-area lists (D150); without it, they are placed nowhere.</param>
    /// <param name="graphics">What the host learned of the graphics, with <paramref name="places"/>.</param>
    /// <param name="monsters">The monsters and their races; without them, no monster is offered.</param>
    public GameReferences(
        WorldAtlas atlas,
        JobCatalog jobs,
        ItemCatalog items,
        WorldGraph? graph = null,
        GraphicPlaces? places = null,
        GraphicSkills? graphics = null,
        MonsterCatalog? monsters = null)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(items);

        _atlas = atlas;
        _places = places;
        _graphics = graphics;
        _maps = new Lazy<ILookup<int, long>>(() => atlas.Positions.ToArray()
            .Where(position => graph?.Contains(position.MapId) ?? true)
            .ToLookup(position => position.SubAreaId, position => position.MapId));
        _subAreaOf = new Lazy<Dictionary<long, int>>(() => atlas.Positions.ToArray().ToDictionary(position => position.MapId, position => position.SubAreaId));
        _sources = new Lazy<Dictionary<int, ResourceSource>>(() => jobs.Skills.ToArray()
            .Where(skill => skill.GatheredItemId != Skill.NoItem && skill.JobId != Job.Base)
            .GroupBy(skill => skill.GatheredItemId)
            .ToDictionary(skills => skills.Key, skills => skills.MinBy(skill => skill.MinLevel) is var lowest ? new ResourceSource(lowest.JobId, lowest.MinLevel) : default));
        _baseSkills = new Lazy<FrozenDictionary<int, FrozenSet<int>>>(() => jobs.Skills.ToArray()
            .Where(skill => skill.GatheredItemId != Skill.NoItem && skill.JobId == Job.Base && !_sources.Value.ContainsKey(skill.GatheredItemId))
            .GroupBy(skill => skill.GatheredItemId)
            .ToFrozenDictionary(skills => skills.Key, skills => skills.Select(skill => skill.Id).ToFrozenSet()));
        _jobs = new Lazy<IReadOnlyList<GameReference>>(() => JobsOf(jobs));
        _gathering = new Lazy<IReadOnlyList<GameReference>>(() => GatheringOf(jobs));
        _areas = new Lazy<IReadOnlyList<GameReference>>(() => [.. atlas.Areas.ToArray().Select(area => new GameReference(area.Id, area.NameId, null))]);
        _subAreas = new Lazy<IReadOnlyList<GameReference>>(() => [.. atlas.SubAreas.ToArray().Select(subArea => new GameReference(subArea.Id, subArea.NameId, subArea.Level))]);
        _resourcesByJob = new Lazy<ILookup<int, GameReference>>(() => ResourcesByJob(jobs, items));
        _items = new Lazy<IReadOnlyList<GameReference>>(() => [.. items.Items.ToArray().Select(item => new GameReference(item.Id, item.NameId, item.Level))]);
        _itemTypes = new Lazy<Dictionary<int, int>>(() => items.Items.ToArray().ToDictionary(item => item.Id, item => (int)item.TypeId));
        _superRaces = new Lazy<IReadOnlyList<GameReference>>(() => monsters is null ? [] : [.. monsters.SuperRaces.ToArray().Select(race => new GameReference(race.Id, race.NameId, null))]);
        _superRaceOfMonster = new Lazy<Dictionary<int, int>>(() => SuperRacesOfMonsters(monsters, atlas));
        _monsters = new Lazy<IReadOnlyList<GameReference>>(() => monsters is null ? [] : [.. _superRaceOfMonster.Value.Keys.Order().Select(id => MonsterOf(monsters, id))]);

        // A family, a race, a hunter meets a monster of: 208 of the client's 265, measured on 3.7.12.12 (D247).
        _familyOfMonster = new Lazy<Dictionary<int, int>>(() => monsters is null ? [] : _superRaceOfMonster.Value.Keys.ToDictionary(id => id, id => monsters.TryGetMonster(id, out var monster) ? (int)monster.Race : 0));
        _superRaceOfFamily = new Lazy<Dictionary<int, int>>(() => monsters is null ? [] : monsters.Races.ToArray().ToDictionary(race => race.Id, race => race.SuperRaceId));
        _families = new Lazy<IReadOnlyList<GameReference>>(() =>
        {
            var met = _familyOfMonster.Value.Values.ToHashSet();

            return monsters is null ? [] : [.. monsters.Races.ToArray().Where(race => met.Contains(race.Id)).Select(race => new GameReference(race.Id, race.NameId, null))];
        });

        // Base's own resources have no profession, so no level (D150).
        _resources = new Lazy<IReadOnlyList<GameReference>>(() => [.. _resourcesByJob.Value
            .SelectMany(resources => resources)
            .GroupBy(resource => resource.Id)
            .Select(resource => resource.MinBy(gathered => gathered.Level))
            .Concat(_baseSkills.Value.Keys.Select(item => new GameReference(item, items.TryGetItem(item, out var found) ? found.NameId : 0, null)))
            .OrderBy(resource => resource.Id)]);
    }

    private static Dictionary<int, int> SuperRacesOfMonsters(MonsterCatalog? monsters, WorldAtlas atlas)
    {
        var found = new Dictionary<int, int>();

        if (monsters is null)
            return found;

        // A monster no sub-area lists is never met on a map: an event's, a quest's, a dungeon's boss (D236).
        var living = atlas.Monsters.ToArray().ToHashSet();

        foreach (var monster in monsters.Monsters.Span)
        {
            if (!living.Contains(monster.Id))
                continue;

            // Bombs and what the bestiary hides are never met on a map's groups; UseSummonSlot is set on most monsters, met or not.
            if (monster.GradeCount == 0 || (monster.Properties & (MonsterProperties.UseBombSlot | MonsterProperties.HideInBestiary)) is not 0)
                continue;

            found[monster.Id] = monsters.TryGetRace(monster.Race, out var race) ? race.SuperRaceId : MonsterRace.NoSuperRace;
        }

        return found;
    }

    private static GameReference MonsterOf(MonsterCatalog monsters, int id)
    {
        return monsters.TryGetMonster(id, out var monster) && monsters.GradesOf(monster) is { Length: > 0 } grades
            ? new GameReference(id, monster.NameId, grades[0].Level)
            : new GameReference(id, 0, null);
    }

    private static List<GameReference> JobsOf(JobCatalog jobs)
    {
        return [.. jobs.Jobs.ToArray().Where(job => job.Id != Job.Base).Select(job => new GameReference(job.Id, job.NameId, null))];
    }

    private static List<GameReference> GatheringOf(JobCatalog jobs)
    {
        HashSet<int> gathering = [.. jobs.Skills.ToArray().Where(skill => skill.GatheredItemId != Skill.NoItem).Select(skill => skill.JobId)];

        return [.. jobs.Jobs.ToArray()
            .Where(job => gathering.Contains(job.Id))
            .OrderBy(job => job.Id == Job.Base)
            .Select(job => new GameReference(job.Id, job.NameId, null))];
    }

    private static ILookup<int, GameReference> ResourcesByJob(JobCatalog jobs, ItemCatalog items)
    {
        return jobs.Skills.ToArray()
            .Where(skill => skill.GatheredItemId != Skill.NoItem && skill.JobId != Job.Base)
            .GroupBy(skill => (skill.JobId, skill.GatheredItemId))
            .Select(skills => new
            {
                skills.Key.JobId,
                Resource = new GameReference(
                    skills.Key.GatheredItemId,
                    items.TryGetItem(skills.Key.GatheredItemId, out var item) ? item.NameId : 0,
                    skills.Min(skill => skill.MinLevel)),
            })
            .OrderBy(gathered => gathered.Resource.Level)
            .ThenBy(gathered => gathered.Resource.Id)
            .ToLookup(gathered => gathered.JobId, gathered => gathered.Resource);
    }

    /// <summary>Gets the values a setting of a kind offers.</summary>
    /// <param name="kind">A kind of the game, such as <see cref="SettingKind.SubArea"/> or <see cref="SettingKind.Monster"/>.</param>
    /// <param name="within">The area of the sub-areas, the profession of the resources or the super race of the monsters, to offer those within it alone.</param>
    /// <returns>The values; a profession's resources by the level it gathers them from.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind is none of the game's.</exception>
    public IReadOnlyList<GameReference> Options(SettingKind kind, int? within = null)
    {
        return (kind, within) switch
        {
            (SettingKind.Job, _) => _jobs.Value,
            (SettingKind.Area, _) => _areas.Value,
            (SettingKind.SubArea, null) => _subAreas.Value,
            (SettingKind.SubArea, { } area) => [.. _subAreas.Value.Where(subArea => IsWithin(kind, subArea.Id, area))],
            (SettingKind.Resource, null) => _resources.Value,
            (SettingKind.Resource, { } job) => [.. _resourcesByJob.Value[job]],
            (SettingKind.Item, _) => _items.Value,
            (SettingKind.MonsterFamily, _) => _families.Value,
            (SettingKind.Monster, null) => _monsters.Value,
            (SettingKind.Monster, { } superRace) => [.. _monsters.Value.Where(monster => _superRaceOfMonster.Value[monster.Id] == superRace)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind names nothing of the game's."),
        };
    }

    /// <summary>Tells whether the game has a value of a kind.</summary>
    /// <param name="kind">A kind of the game.</param>
    /// <param name="id">The value's id.</param>
    /// <returns>Whether it has.</returns>
    public bool Exists(SettingKind kind, int id)
    {
        return kind switch
        {
            SettingKind.Job => _jobs.Value.Any(job => job.Id == id),
            SettingKind.Area => _atlas.TryGetArea(id, out _),
            SettingKind.SubArea => _atlas.TryGetSubArea(id, out _),
            SettingKind.Resource => _resources.Value.Any(resource => resource.Id == id),
            SettingKind.Item => _itemTypes.Value.ContainsKey(id),
            SettingKind.Monster => _superRaceOfMonster.Value.ContainsKey(id),
            SettingKind.MonsterFamily => _families.Value.Any(family => family.Id == id),
            _ => false,
        };
    }

    /// <summary>Tells whether a profession's skills gather a resource, <c>Base</c> among them (D165).</summary>
    /// <param name="job">The profession.</param>
    /// <returns>Whether they do.</returns>
    public bool IsGathering(int job)
    {
        return _gathering.Value.Any(gathering => gathering.Id == job);
    }

    /// <summary>Finds who gathers a resource.</summary>
    /// <param name="resource">The resource's item.</param>
    /// <param name="source">The profession that gathers it, at its lowest level.</param>
    /// <returns>Whether a profession gathers it.</returns>
    public bool TryGetSource(int resource, out ResourceSource source)
    {
        return _sources.Value.TryGetValue(resource, out source);
    }

    /// <summary>Gets the monsters of a family a hunter meets (D247).</summary>
    /// <param name="family">The family, a race.</param>
    /// <returns>Its monsters, by id.</returns>
    public IReadOnlyList<int> MonstersOfFamily(int family)
    {
        return [.. _familyOfMonster.Value.Where(pair => pair.Value == family).Select(pair => pair.Key).Order()];
    }

    /// <summary>Finds the super race a family belongs to, which names where its monsters come from (D247).</summary>
    /// <param name="family">The family, a race.</param>
    /// <param name="superRace">Its super race.</param>
    /// <returns>Whether the family is one of the game's.</returns>
    public bool TryGetSuperRaceOfFamily(int family, out int superRace)
    {
        return _superRaceOfFamily.Value.TryGetValue(family, out superRace);
    }

    /// <summary>Finds a monster's super race (D236).</summary>
    /// <param name="monster">The monster.</param>
    /// <param name="superRace">Its super race, <see cref="MonsterRace.NoSuperRace"/> when its race has none.</param>
    /// <returns>Whether the monster is one a setting offers.</returns>
    public bool TryGetSuperRace(int monster, out int superRace)
    {
        return _superRaceOfMonster.Value.TryGetValue(monster, out superRace);
    }

    /// <summary>Gets the items of some types, such as food's, by their id (D148).</summary>
    /// <param name="types">The item types.</param>
    /// <returns>The items, with their levels.</returns>
    public IReadOnlyList<GameReference> ItemsOf(IReadOnlySet<int> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        return [.. _items.Value.Where(item => _itemTypes.Value.TryGetValue(item.Id, out var type) && types.Contains(type))];
    }

    /// <summary>Tells whether an item is of some types.</summary>
    /// <param name="item">The item.</param>
    /// <param name="types">The item types.</param>
    /// <returns>Whether it is.</returns>
    public bool IsItemOf(int item, IReadOnlySet<int> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        return _itemTypes.Value.TryGetValue(item, out var type) && types.Contains(type);
    }

    /// <summary>Tells whether a resource is one only skills of <c>Base</c> gather, which no profession needs and no sub-area lists (D150).</summary>
    /// <param name="resource">The resource's item.</param>
    /// <returns>Whether it is.</returns>
    public bool IsBase(int resource)
    {
        return _baseSkills.Value.ContainsKey(resource);
    }

    /// <summary>Finds the maps the host's learned graphics place a resource of <c>Base</c> on (D150).</summary>
    /// <param name="resource">The resource's item.</param>
    /// <returns>The maps, none while nothing learned places it; <see langword="null"/> for a resource a profession gathers, or a host that does not learn.</returns>
    public IReadOnlySet<long>? PlacesOf(int resource)
    {
        if (_places is null || _graphics is null || !_baseSkills.Value.TryGetValue(resource, out var skills))
            return null;

        return _places.MapsOffering(skills, _graphics);
    }

    /// <summary>Gets the maps of a sub-area a character may travel to.</summary>
    /// <param name="subArea">The sub-area.</param>
    /// <returns>Its maps, in the atlas's order; none for a sub-area the atlas lacks.</returns>
    public IEnumerable<long> MapsOf(int subArea)
    {
        return _maps.Value[subArea];
    }

    /// <summary>Gets the sub-areas where resources grow, the most of them first, then the lowest (D127).</summary>
    /// <param name="resources">The resources asked for, in order.</param>
    /// <param name="within">The area to seek them in alone, if any.</param>
    /// <returns>
    /// Each sub-area listing at least one of them, or holding a map the learned graphics place a resource of <c>Base</c> on
    /// (D150), with its maps and those it holds.
    /// </returns>
    public IReadOnlyList<SubAreaResources> SubAreasHolding(IReadOnlyList<int> resources, int? within = null)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var found = new List<SubAreaResources>();
        var placed = resources.Distinct().Select(resource => (resource, maps: PlacesOf(resource))).Where(pair => pair.maps is { Count: > 0 })
            .ToDictionary(pair => pair.resource, pair => pair.maps!.Select(map => _subAreaOf.Value.GetValueOrDefault(map, -1)).ToHashSet());

        foreach (var subArea in _atlas.SubAreas.Span)
        {
            if (within is { } area && subArea.AreaId != area)
                continue;

            var listed = _atlas.HarvestablesOf(subArea);
            var held = new List<int>();

            foreach (var resource in resources)
            {
                if (listed.Contains(resource) || (placed.TryGetValue(resource, out var subAreas) && subAreas.Contains(subArea.Id)))
                    held.Add(resource);
            }

            if (held.Count > 0)
                found.Add(new SubAreaResources(new GameReference(subArea.Id, subArea.NameId, subArea.Level), _maps.Value[subArea.Id].Count(), held));
        }

        return [.. found.OrderByDescending(subArea => subArea.Resources.Count).ThenBy(subArea => subArea.SubArea.Level).ThenBy(subArea => subArea.SubArea.Id)];
    }

    /// <summary>Gets the sub-areas where monsters live, the most of them first, then the lowest (D245).</summary>
    /// <param name="monsters">The monsters asked for, in order.</param>
    /// <param name="within">The area to seek them in alone, if any.</param>
    /// <returns>Each sub-area the client lists at least one of them in (D238), with its maps and those it holds.</returns>
    public IReadOnlyList<SubAreaMonsters> SubAreasLiving(IReadOnlyList<int> monsters, int? within = null)
    {
        ArgumentNullException.ThrowIfNull(monsters);

        var found = new List<SubAreaMonsters>();

        foreach (var subArea in _atlas.SubAreas.Span)
        {
            if (within is { } area && subArea.AreaId != area)
                continue;

            var living = _atlas.MonstersOf(subArea);
            var held = new List<int>();

            foreach (var monster in monsters)
            {
                if (living.Contains(monster) && !held.Contains(monster))
                    held.Add(monster);
            }

            if (held.Count > 0)
                found.Add(new SubAreaMonsters(new GameReference(subArea.Id, subArea.NameId, subArea.Level), _maps.Value[subArea.Id].Count(), held));
        }

        return [.. found.OrderByDescending(subArea => subArea.Monsters.Count).ThenBy(subArea => subArea.SubArea.Level).ThenBy(subArea => subArea.SubArea.Id)];
    }

    /// <summary>Tells whether a value of the game is within another: a sub-area within an area, a resource within a profession.</summary>
    /// <param name="kind"><see cref="SettingKind.SubArea"/> or <see cref="SettingKind.Resource"/>.</param>
    /// <param name="id">The value's id.</param>
    /// <param name="within">The area's or the profession's id.</param>
    /// <returns>Whether it is.</returns>
    public bool IsWithin(SettingKind kind, int id, int within)
    {
        return kind switch
        {
            SettingKind.SubArea => _atlas.TryGetSubArea(id, out var subArea) && subArea.AreaId == within,
            SettingKind.Resource => _resourcesByJob.Value[within].Any(resource => resource.Id == id),
            _ => false,
        };
    }
}
