using System.Runtime.CompilerServices;
using D3B.Bot.Network;
using D3B.Data.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Fights;

/// <summary>Teaches <see cref="FightPlacements"/> each fight's placement cells, the map fought on and the one left, on every game connection (D275).</summary>
/// <remarks>
/// <para>
/// Measured (D275): as a fight begins, the server destroys the roleplay's context, creates the fight's, then names the map
/// fought on, another than the map left in 107 of 163 fights. The map left is the one the character was on as the context
/// was destroyed, the map fought on the one the server names last before the cells; the cells, the first it gives once a
/// fight starts, as the fight's state reads them (<see cref="FightEvents"/>). A fight counts once, whatever the server gives
/// again while the fighters place; one rejoined, its context never destroyed, was begun from the map it is fought on.
/// </para>
/// <para>It sees the frames on the relay's read loops: what it keeps of a connection is that connection's alone, and goes with it.</para>
/// </remarks>
internal sealed class FightPlacementLearner : IGameObserver
{
    private const int Cells = 560;

    private readonly FightPlacements _placements;
    private readonly GameWorld? _world;
    private readonly TimeProvider _time;
    private readonly ConditionalWeakTable<GameSession, Presence> _presences = [];

    public FightPlacementLearner(FightPlacements placements, GameWorld? world, TimeProvider time)
    {
        _placements = placements;
        _world = world;
        _time = time;
    }

    public void OnSessionStarted(GameSession session)
    {
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (direction is not RelayDirection.FromServer || !frame.IsRead)
            return;

        var presence = _presences.GetOrCreateValue(session);

        lock (presence)
        {
            switch (frame.Content)
            {
                case MapCurrentEvent current:
                    presence.MapId = current.MapId;
                    break;
                case ContextDestroyEvent:
                    presence.LeftMapId = presence.MapId;
                    break;
                case FightStartingEvent:
                    presence.Counted = false;
                    presence.FromMapId = presence.LeftMapId;
                    presence.LeftMapId = null;
                    break;
                case FightPlacementPossiblePositionsEvent { Cells: { } cells } when !presence.Counted && presence.MapId is { } mapId:
                    presence.Counted = true;
                    _placements.Seen(presence.FromMapId ?? mapId, mapId, cells.ChallengerCells.Where(IsCell), cells.DefenderCells.Where(IsCell), _world?.Atlas, _time.GetUtcNow());
                    break;
            }
        }
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < Cells;
    }

    private sealed class Presence
    {
        public long? MapId { get; set; }

        public long? LeftMapId { get; set; }

        public long? FromMapId { get; set; }

        public bool Counted { get; set; } = true;
    }
}
