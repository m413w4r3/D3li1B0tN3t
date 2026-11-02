using D3B.Bot.Network;
using D3B.Data.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Interactives;

/// <summary>Teaches <see cref="GraphicSkills"/> what each map's information says of its elements, on every game connection (D138).</summary>
/// <remarks>
/// <para>
/// The server describes the elements of the map a character enters, by their identifiers on the map, which the pack gives
/// a graphic. Only the elements it lists teach: measured on client 3.7.12.12, it leaves out a third of the pack's elements
/// of the maps it describes, some of them drawn with a resource's graphic elsewhere, so an element it does not list says
/// nothing of its graphic. Neither do the neighbouring maps' elements it lists, whose identifiers are their own map's.
/// </para>
/// <para>
/// It sees the frames on the relay's read loops: a map is read from the pack's cache, and the graphics learned are told
/// to whoever keeps them, which must not wait.
/// </para>
/// </remarks>
internal sealed class GraphicLearner : IGameObserver
{
    private readonly GameMaps _maps;
    private readonly GraphicSkills _graphics;

    public GraphicLearner(GameMaps maps, GraphicSkills graphics)
    {
        _maps = maps;
        _graphics = graphics;
    }

    public void OnSessionStarted(GameSession session)
    {
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (direction is not RelayDirection.FromServer || !frame.IsRead || frame.Content is not MapInformationEvent information
            || !_maps.TryGet(information.MapId, out var map))
        {
            return;
        }

        foreach (var element in information.InteractiveElements)
        {
            if (!element.OnCurrentMap || !map.TryGetInteractive(element.ElementId, out var drawn))
                continue;

            _graphics.Learn(drawn.GfxId, [.. element.EnabledSkills.Concat(element.DisabledSkills).Select(skill => skill.SkillId).Distinct()]);
        }
    }
}
