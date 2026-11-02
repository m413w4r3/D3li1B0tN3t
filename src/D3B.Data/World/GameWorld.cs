using D3B.Data.Packs;

namespace D3B.Data.World;

/// <summary>The world of a pack: its graph and its atlas, each read once, when first asked for, then kept.</summary>
public sealed class GameWorld
{
    private readonly Lazy<WorldGraph> _graph;
    private readonly Lazy<WorldAtlas> _atlas;

    /// <summary>Gets the world graph.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no world graph.</exception>
    /// <exception cref="PackFormatException">Its entry is damaged.</exception>
    public WorldGraph Graph => _graph.Value;

    /// <summary>Gets the atlas: maps' positions, sub-areas, areas and zaaps.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no atlas.</exception>
    /// <exception cref="PackFormatException">Its entry is damaged.</exception>
    public WorldAtlas Atlas => _atlas.Value;

    /// <summary>Initializes a new instance of the <see cref="GameWorld"/> class.</summary>
    /// <param name="pack">The pack holding the world.</param>
    public GameWorld(GamePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        // Publication only: a failed read is not remembered, so that a later call reads again.
        _graph = new Lazy<WorldGraph>(() => pack.Read<WorldGraph>(WorldGraph.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _atlas = new Lazy<WorldAtlas>(() => pack.Read<WorldAtlas>(WorldAtlas.EntryName), LazyThreadSafetyMode.PublicationOnly);
    }
}
