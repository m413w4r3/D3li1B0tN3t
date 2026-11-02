using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace D3B.Bot.Interactives;

/// <summary>What the host's bots learned of the maps' graphics: the skills the server says the elements drawn with each offer (D138).</summary>
/// <remarks>
/// <para>
/// The client's data never says which interactive an element is; the server says it of each element of the map a character
/// enters. Measured on client 3.7.12.12, the elements one graphic draws are mostly one resource: what the server says of
/// one tells of every map the graphic is on, such as all of the world's nettles once one is seen. Fishing spots share a
/// graphic between seven fish, so a graphic keeps every skill seen on it, not one interactive (client.md).
/// </para>
/// <para>
/// One per host, which every bot teaches through <see cref="GraphicLearner"/>, whatever its activity; D3B's database keeps
/// it from one run to the next, and from one client build to the next, a graphic being an asset of the client. A skill
/// seen once is never forgotten: a contradiction only adds one.
/// </para>
/// </remarks>
public sealed class GraphicSkills
{
    private readonly ConcurrentDictionary<int, FrozenSet<int>> _graphics = new();

    /// <summary>Raised when a graphic is learned, or offers a skill more, with the graphic: <see cref="TryGet"/> says what it offers now.</summary>
    /// <remarks>Raised on the thread that learned it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<int>? Learned;

    /// <summary>Gets how many graphics are known.</summary>
    public int Count => _graphics.Count;

    /// <summary>Takes back what was learned before, such as what the database kept, without raising <see cref="Learned"/>.</summary>
    /// <param name="gfxId">The graphic.</param>
    /// <param name="skills">The skills its elements were seen offering, none for an element that offers none.</param>
    public void Remember(int gfxId, IEnumerable<int> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        Add(gfxId, skills.ToFrozenSet());
    }

    /// <summary>Learns what an element drawn with a graphic offers.</summary>
    /// <param name="gfxId">The graphic.</param>
    /// <param name="skills">The skills the server says the element offers, enabled or not, none for one that offers none.</param>
    /// <returns>Whether it taught anything: a graphic not known, or a skill more.</returns>
    public bool Learn(int gfxId, IReadOnlyCollection<int> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        if (!Add(gfxId, skills))
            return false;

        Learned?.Invoke(gfxId);

        return true;
    }

    /// <summary>Finds what a graphic's elements offer.</summary>
    /// <param name="gfxId">The graphic.</param>
    /// <param name="skills">The skills seen offered on its elements, empty when they offer none.</param>
    /// <returns>Whether the graphic is known.</returns>
    public bool TryGet(int gfxId, [NotNullWhen(true)] out FrozenSet<int>? skills)
    {
        return _graphics.TryGetValue(gfxId, out skills);
    }

    private bool Add(int gfxId, IReadOnlyCollection<int> skills)
    {
        while (true)
        {
            if (!_graphics.TryGetValue(gfxId, out var known))
            {
                if (_graphics.TryAdd(gfxId, skills.ToFrozenSet()))
                    return true;

                continue;
            }

            if (skills.All(known.Contains))
                return false;

            // Another bot may have taught the graphic meanwhile: the update holds only over what was read.
            if (_graphics.TryUpdate(gfxId, known.Union(skills).ToFrozenSet(), known))
                return true;
        }
    }
}
