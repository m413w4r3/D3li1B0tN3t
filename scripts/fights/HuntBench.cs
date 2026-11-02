using System.Globalization;
using D3B.Bot.Characters;
using D3B.Bot.Fighting;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using D3B.Network.Captures;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Scripts.Fights;

/// <summary>Counts what came of the attacks on monster groups in the captures, the fighting activity's and the player's: the walks before each, the server's answer, the AI's judgement of the group against the fight's outcome, and the pace between fights (D245).</summary>
/// <remarks>
/// An attack D3B sent is an <see cref="CaptureRecordKind.Injected"/> frame, the player's the client's. One is accepted when a
/// fight starts within the server's answer delay, 3 s (D152), unanswered otherwise. Its group is judged as the fighting
/// activity judges it (<see cref="GroupJudge.Beats"/>), the character from the characteristics the server last gave it out of fights (D263),
/// by levels before. It prints counts and times, never a value of one fight.
/// </remarks>
internal sealed class HuntBench
{
    private const string Bot = "D3B's";
    private const string Player = "the player's";

    private static readonly TimeSpan _answer = TimeSpan.FromSeconds(3);

    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly FightTactics _tactics;
    private readonly List<double> _toAttack = [];
    private readonly List<double> _toFight = [];
    private TimeSpan _hunting;
    private int _fights;

    public Tally Tally { get; } = new();

    public HuntBench(GameTables tables, GameMaps maps)
    {
        _tables = tables;
        _maps = maps;
        _tactics = new FightTactics(tables);
    }

    private static string Spread(List<double> values, string unit)
    {
        if (values.Count is 0)
            return "none";

        var sorted = values.Order().ToList();

        return string.Create(CultureInfo.InvariantCulture, $"{values.Count}, halfway {sorted[sorted.Count / 2]:0.##} {unit}, 90 % {sorted[Math.Min(sorted.Count - 1, sorted.Count * 9 / 10)]:0.##} {unit}, most {sorted[^1]:0.##} {unit}");
    }

    public void Replay(List<(TimeSpan Offset, CaptureRecordKind Kind, IMessage Message)> messages)
    {
        CharacterState? state = null;
        Attack? attack = null;
        Attack? fought = null;
        TimeSpan? ended = null;
        TimeSpan? first = null;
        TimeSpan? last = null;
        var walks = 0;

        foreach (var (offset, kind, message) in messages)
        {
            if (kind is CaptureRecordKind.FromServer && message is CharacterSelectionEvent { Success.Character: { } chosen })
                state = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);

            if (state is null)
                continue;

            if (kind is not CaptureRecordKind.FromServer)
            {
                if (state.Fight is not null)
                    continue;

                if (kind is CaptureRecordKind.Injected)
                    Tally.Count($"D3B's requests out of fights: {message.Descriptor.Name}");

                if (kind is CaptureRecordKind.Injected && message is MapMovementRequest)
                    walks++;

                if (message is AttackMonsterRequest request)
                {
                    attack = Attacked(state, request.MonsterGroupId, kind, offset, walks);
                    walks = 0;

                    if (kind is CaptureRecordKind.Injected)
                    {
                        first ??= offset;
                        last = offset;

                        if (ended is { } end)
                            _toAttack.Add((offset - end).TotalSeconds);

                        ended = null;
                    }
                }

                continue;
            }

            var map = state.MapId;

            state = CharacterEvents.Apply(state, RelayDirection.FromServer, message);

            if (state.MapId != map)
                walks = 0;

            if (attack is { } pending)
            {
                if (message is FightStartingEvent)
                {
                    Tally.Count($"attacks: {pending.Who}, accepted");

                    if (pending.Who is Bot)
                        _toFight.Add((offset - pending.Offset).TotalMilliseconds);

                    fought = pending;
                    attack = null;
                }
                else if (offset - pending.Offset > _answer)
                {
                    Tally.Count($"attacks: {pending.Who}, unanswered");
                    attack = null;
                }
            }

            if (message is FightEndEvent)
            {
                if (fought is { } done)
                {
                    var outcome = state.Fight?.End?.Outcome switch
                    {
                        FightOutcome.Won => "won",
                        FightOutcome.Lost => "lost",
                        _ => "ended otherwise",
                    };

                    Tally.Count($"attacked groups: {done.Who}, judged {done.Judged}, {outcome}");
                    Tally.Count($"attacked groups by odds: {done.Who}, {done.Bucket}, {outcome}");

                    if (done.Who is Bot)
                    {
                        _fights++;
                        ended = offset;
                    }
                }

                fought = null;
            }
        }

        if (attack is { } left)
            Tally.Count($"attacks: {left.Who}, unanswered as the capture ends");

        if (first is { } from && last is { } to)
            _hunting += to - from;
    }

    public void Write(TextWriter writer)
    {
        Tally.Write(writer);
        writer.WriteLine("pace");
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  D3B's fights from its attacks: {_fights}, over {_hunting.TotalHours:0.##} h from its first attack to its last, {(_hunting.TotalHours > 0 ? _fights / _hunting.TotalHours : 0):0.#} an hour"));
        writer.WriteLine($"  from the end of D3B's fight to its next attack: {Spread(_toAttack, "s")}");
        writer.WriteLine($"  from D3B's attack to the fight's start: {Spread(_toFight, "ms")}");
    }

    private Attack Attacked(CharacterState state, long groupId, CaptureRecordKind kind, TimeSpan offset, int walks)
    {
        var who = kind is CaptureRecordKind.Injected ? Bot : Player;

        Tally.Count($"attacks: {who}, sent");

        if (who is Bot)
            Tally.Count($"attacks: D3B's, after {(walks > 3 ? "4 or more" : walks.ToString(CultureInfo.InvariantCulture))} of its walks on the map");

        if (state.SubAreaId is { } subArea)
            Tally.Count($"attacks by sub-area: {who}, in sub-area {subArea}");

        if (!state.MonsterGroups.TryGetValue(groupId, out var group))
            return new Attack(who, offset, "on a group unseen", "unseen");

        Tally.Count($"attacked groups: {who}, of {group.Monsters.Length} monsters");

        if (state.Level is { } level && group.Monsters.Any(monster => monster.Level > level))
            Tally.Count($"attacked groups: {who}, a monster above the character's level");

        if (state.MapId is not { } mapId || !_maps.TryGet(mapId, out var data))
            return new Attack(who, offset, "without its map", "without its map");

        var beats = GroupJudge.Beats(group, state, FightMap.Of(data), _tables, _tactics);
        var ratio = GroupJudge.OddsOf(group, state, FightMap.Of(data), _tables, _tactics) is { } odds ? odds.TurnsToLose / odds.TurnsToWin : (double?)null;
        var bucket = ratio switch
        {
            null => "by levels",
            < 0.25 => "losing under a quarter of the turns to win",
            < 0.5 => "losing under half",
            < 0.75 => "losing under three quarters",
            < 1 => "losing before winning",
            _ => "winning first",
        };

        return new Attack(who, offset, (ratio is null ? "by levels " : "") + (beats ? "winnable" : "risky"), bucket);
    }

    private sealed record Attack(string Who, TimeSpan Offset, string Judged, string Bucket);
}
