using D3B.App.Localization;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;

namespace D3B.App.ViewModels.Maps;

/// <summary>What the map page names from the pack in the app's language: places, sub-areas, elements, skills, bankers and monsters (D121).</summary>
/// <remarks>Each name falls back on its id when the pack's texts or tables do not hold it, as the journal's do (D112).</remarks>
internal sealed class MapNames
{
    private readonly Localizer _localizer;
    private readonly GameWorld? _world;
    private readonly GameTexts? _texts;
    private readonly GameTables? _tables;

    /// <summary>Initializes a new instance of the <see cref="MapNames"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="world">The pack's world, for the maps' coordinates and sub-areas.</param>
    /// <param name="texts">The pack's texts.</param>
    /// <param name="tables">The pack's tables, for the elements, skills, bankers and monsters.</param>
    public MapNames(Localizer localizer, GameWorld? world, GameTexts? texts, GameTables? tables)
    {
        _localizer = localizer;
        _world = world;
        _texts = texts;
        _tables = tables;
    }

    /// <summary>Names where a map lies: its coordinates, an inside at them, or its id off the atlas.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Its place.</returns>
    public string Place(long mapId)
    {
        if (_world is null || !_world.Atlas.TryGetPosition(mapId, out var position))
            return _localizer.Format("MapNumber", mapId);

        var coordinates = _localizer.Format("Coordinates", position.X, position.Y);

        // An inside, such as a bank's, lies at the coordinates of its outside, on no world map (D79).
        return position.WorldMap < 0 ? _localizer.Format("MapInside", coordinates) : coordinates;
    }

    /// <summary>Names a sub-area.</summary>
    /// <param name="subAreaId">The sub-area.</param>
    /// <returns>Its name, or its id.</returns>
    public string SubArea(int subAreaId)
    {
        return _world is not null && _texts is not null && _world.Atlas.TryGetSubArea(subAreaId, out var subArea) && _texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name)
            ? name
            : _localizer.Format("SubArea", subAreaId);
    }

    /// <summary>Names an element by what the server says it is.</summary>
    /// <param name="elementId">The element's id on its map.</param>
    /// <param name="interactiveId">Which interactive it is, or -1 when the server did not say.</param>
    /// <returns>The interactive's name, or the element's id.</returns>
    public string Element(int elementId, int interactiveId)
    {
        return interactiveId >= 0 && _tables is not null && _texts is not null && _tables.Interactives.TryGet(interactiveId, out var interactive)
            && _texts.TryGet(_localizer.GameLanguage, interactive.NameId, out var name)
            ? name
            : _localizer.Format("MapElement", elementId);
    }

    /// <summary>Names a skill.</summary>
    /// <param name="skillId">The skill.</param>
    /// <returns>Its name, or its id.</returns>
    public string Skill(int skillId)
    {
        return _tables is not null && _texts is not null && _tables.Jobs.TryGetSkill(skillId, out var skill) && _texts.TryGet(_localizer.GameLanguage, skill.NameId, out var name)
            ? name
            : _localizer.Format("SkillNumber", skillId);
    }

    /// <summary>Tells whether a skill gathers a resource, as a harvest does.</summary>
    /// <param name="skillId">The skill.</param>
    /// <returns>Whether the pack's skill gathers an item; <see langword="false"/> without the pack's tables.</returns>
    public bool Gathers(int skillId)
    {
        return _tables is not null && _tables.Jobs.TryGetSkill(skillId, out var skill) && skill.GatheredItemId > 0;
    }

    /// <summary>Finds the resource a skill gathers.</summary>
    /// <param name="skillId">The skill.</param>
    /// <returns>The item it gathers, or <see langword="null"/> when it gathers none, or without the pack's tables.</returns>
    public int? Gathered(int skillId)
    {
        return _tables is not null && _tables.Jobs.TryGetSkill(skillId, out var skill) && skill.GatheredItemId > 0 ? skill.GatheredItemId : null;
    }

    /// <summary>Names a monster, such as a fighter the server names by it.</summary>
    /// <param name="monsterId">The monster.</param>
    /// <returns>Its name, or its id.</returns>
    public string Monster(int monsterId)
    {
        return _tables is not null && _texts is not null && _tables.Monsters.TryGetMonster(monsterId, out var monster) && _texts.TryGet(_localizer.GameLanguage, monster.NameId, out var name)
            ? name
            : _localizer.Format("MapMonsterNumber", monsterId);
    }

    /// <summary>Names a non-player character: a banker by the pack's name, any other by its id.</summary>
    /// <param name="npcId">Which NPC it is.</param>
    /// <returns>Its name.</returns>
    public string Npc(int npcId)
    {
        return _tables is not null && _texts is not null && _tables.Banks.TryGetBanker(npcId, out var banker) && _texts.TryGet(_localizer.GameLanguage, banker.NameId, out var name)
            ? name
            : _localizer.Format("MapNpc", npcId);
    }
}
