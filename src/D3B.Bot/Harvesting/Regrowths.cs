using System.Collections.Concurrent;

namespace D3B.Bot.Harvesting;

/// <summary>How long each resource takes to grow back, and when the elements the host's bots saw harvested were (D145).</summary>
/// <remarks>
/// <para>
/// The server never says when an element will grow back: it shows it once it has, by the element offering its skill again,
/// and on entering a map. Measured on client 3.7.12.12 before 6c, bounds alone: nettle grown back at most 445 s after its
/// harvest, trout not before 297 s and at most 564 s after.
/// </para>
/// <para>
/// One per host, which <see cref="RegrowthLearner"/> teaches from every map any bot is on, whatever its activity, and every
/// actor's harvests there; D3B's database keeps the times from one run to the next, by skill, a resource's skill being the
/// client's. The harvests themselves stay in memory: an element's identifier is its map's.
/// </para>
/// </remarks>
public sealed class Regrowths
{
    private readonly ConcurrentDictionary<int, Regrowth> _skills = new();
    private readonly ConcurrentDictionary<(long MapId, int ElementId), (DateTimeOffset At, int SkillId)> _harvests = new();

    /// <summary>Raised when what a skill's resource takes is learned or changes, with the skill: <see cref="TryGet"/> says what it is now.</summary>
    /// <remarks>Raised on the thread that learned it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<int>? Learned;

    /// <summary>Gets how many skills' growing back is known.</summary>
    public int Count => _skills.Count;

    /// <summary>Takes back what was learned before, such as what the database kept, without raising <see cref="Learned"/>.</summary>
    /// <param name="skillId">The skill that gathers the resource.</param>
    /// <param name="regrowth">What it takes.</param>
    public void Remember(int skillId, Regrowth regrowth)
    {
        _skills[skillId] = regrowth;
    }

    /// <summary>Finds what a skill's resource takes to grow back.</summary>
    /// <param name="skillId">The skill that gathers it.</param>
    /// <param name="regrowth">What it takes.</param>
    /// <returns>Whether any growing back of it was seen.</returns>
    public bool TryGet(int skillId, out Regrowth regrowth)
    {
        return _skills.TryGetValue(skillId, out regrowth);
    }

    /// <summary>Gets how long a tour reckons a skill's resource takes to grow back.</summary>
    /// <param name="skillId">The skill that gathers it.</param>
    /// <returns>The time, or <see langword="null"/> while it is not known.</returns>
    public TimeSpan? Reckoned(int skillId)
    {
        return _skills.TryGetValue(skillId, out var regrowth) ? regrowth.Reckoned : null;
    }

    /// <summary>Finds when an element of a map was last seen harvested.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elementId">The element's identifier on it.</param>
    /// <param name="at">When its harvest ended.</param>
    /// <returns>Whether a bot saw it, and has not seen it grown back since.</returns>
    public bool TryGetHarvest(long mapId, int elementId, out DateTimeOffset at)
    {
        var found = _harvests.TryGetValue((mapId, elementId), out var harvest);

        at = harvest.At;

        return found;
    }

    /// <summary>Remembers a harvest a bot saw end.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elementId">The element's identifier on it.</param>
    /// <param name="skillId">The skill used.</param>
    /// <param name="at">When it ended.</param>
    public void Harvested(long mapId, int elementId, int skillId, DateTimeOffset at)
    {
        _harvests[(mapId, elementId)] = (at, skillId);
    }

    /// <summary>Learns from an element seen offering a skill again, if a bot saw it harvested.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elementId">The element's identifier on it.</param>
    /// <param name="skillId">The skill it offers.</param>
    /// <param name="at">When.</param>
    /// <param name="watchedSince">Since when a bot has been on the map without leaving it: if since before the harvest's end, the growing back was seen whole.</param>
    /// <returns>Whether it taught anything.</returns>
    public bool GrownBack(long mapId, int elementId, int skillId, DateTimeOffset at, DateTimeOffset watchedSince)
    {
        if (!_harvests.TryGetValue((mapId, elementId), out var harvest) || harvest.SkillId != skillId || !_harvests.TryRemove(KeyValuePair.Create((mapId, elementId), harvest)))
            return false;

        var took = at - harvest.At;
        var whole = watchedSince <= harvest.At;

        while (true)
        {
            var known = _skills.TryGetValue(skillId, out var regrowth);
            var learned = whole
                ? regrowth with { Longest = regrowth.Longest is { } longest && longest >= took ? longest : took, Seen = regrowth.Seen + 1 }
                : regrowth with { AtMost = regrowth.AtMost is { } most && most <= took ? most : took };

            if (learned == regrowth)
                return false;

            // Another bot may have taught the skill meanwhile: the update holds only over what was read.
            if (known ? _skills.TryUpdate(skillId, learned, regrowth) : _skills.TryAdd(skillId, learned))
                break;
        }

        Learned?.Invoke(skillId);

        return true;
    }
}
