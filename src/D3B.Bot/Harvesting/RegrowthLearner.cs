using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using D3B.Bot.Network;
using D3B.Data;
using D3B.Data.Jobs;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Harvesting;

/// <summary>Teaches <see cref="Regrowths"/> how long resources take to grow back, from what the server shows on every game connection (D145).</summary>
/// <remarks>
/// <para>
/// A harvest of any actor on the map a connection's character is on is remembered as it ends. The element offering its
/// skill again, while the character stayed on the map since before that end, gives the whole time; found offering it on a
/// later visit, by the map's information, the time is a bound from above only, another player having maybe harvested it
/// meanwhile unseen. An element still harvested on a visit says nothing for the same reason.
/// </para>
/// <para>
/// It sees the frames on the relay's read loops: what it keeps of a connection, its map and since when, is that
/// connection's alone, and goes with it.
/// </para>
/// </remarks>
internal sealed class RegrowthLearner : IGameObserver
{
    private readonly Regrowths _regrowths;
    private readonly TimeProvider _time;
    private readonly Lazy<FrozenSet<int>> _gathering;
    private readonly ConditionalWeakTable<GameSession, Presence> _presences = [];

    public RegrowthLearner(Regrowths regrowths, GameTables tables, TimeProvider time)
    {
        _regrowths = regrowths;
        _time = time;
        _gathering = new Lazy<FrozenSet<int>>(() => tables.Jobs.Skills.ToArray().Where(skill => skill.GatheredItemId != Skill.NoItem).Select(skill => skill.Id).ToFrozenSet());
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
        var now = _time.GetUtcNow();

        lock (presence)
        {
            switch (frame.Content)
            {
                case MapCurrentEvent current when current.MapId != presence.MapId:
                    presence.MapId = current.MapId;
                    presence.Since = now;
                    break;
                case InteractiveUseEndedEvent ended when presence.MapId is { } mapId && _gathering.Value.Contains(ended.SkillId):
                    _regrowths.Harvested(mapId, ended.ElementId, ended.SkillId, now);
                    break;
                case InteractiveElementUpdatedEvent { Element: { OnCurrentMap: true } element } when presence.MapId is { } mapId:
                    GrownBack(mapId, element, now, presence.Since);
                    break;
                case MapInformationEvent information:
                    // The information of a map entered: what grew back was not watched whole.
                    foreach (var element in information.InteractiveElements.Where(element => element.OnCurrentMap))
                        GrownBack(information.MapId, element, now, now);

                    break;
            }
        }
    }

    private void GrownBack(long mapId, InteractiveElement element, DateTimeOffset at, DateTimeOffset watchedSince)
    {
        foreach (var skill in element.EnabledSkills)
        {
            if (_gathering.Value.Contains(skill.SkillId))
                _regrowths.GrownBack(mapId, element.ElementId, skill.SkillId, at, watchedSince);
        }
    }

    private sealed class Presence
    {
        public long? MapId { get; set; }

        public DateTimeOffset Since { get; set; }
    }
}
