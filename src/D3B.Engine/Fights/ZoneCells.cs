using D3B.Data.Spells;

namespace D3B.Engine.Fights;

/// <summary>The cells zones cover, as <see cref="SpellZones.Cells"/> gives them, kept for the zones that depend on no fighter.</summary>
/// <remarks>
/// A search foresees thousands of casts a turn, from a few cells onto a few cells (D200): a zone's cells depend on its shape,
/// sizes and forced direction, the caster's cell and the cell cast on, and not on the map, but for a zone keeping to the line
/// of sight, one stopping at a fighter, or a custom one, which are never kept. Each thread keeps its own, at most
/// <see cref="Capacity"/> zones, all let go of once full.
/// </remarks>
public static class ZoneCells
{
    /// <summary>The most zones a thread keeps.</summary>
    public const int Capacity = 50_000;

    [ThreadStatic]
    private static Dictionary<long, CellSet>? _kept;

    /// <summary>Gets the cells a zone covers, as <see cref="SpellZones.Cells"/> does.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="listed">The cells it lists, for a custom zone.</param>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">The cells fighters stand on.</param>
    /// <param name="caster">The caster's cell.</param>
    /// <param name="centre">The cell cast on.</param>
    /// <returns>The cells of the map the zone covers.</returns>
    public static CellSet Of(in SpellZone zone, ReadOnlySpan<int> listed, FightMap map, in CellSet fighters, int caster, int centre)
    {
        if (caster < 0 || centre < 0 || zone.OnlyInLineOfSight || zone.Shape is SpellZoneShape.Custom || (zone.Shape is SpellZoneShape.LineFromCaster && zone.StopsAtTarget))
            return SpellZones.Cells(zone, listed, map, fighters, caster, centre);

        var key = (long)zone.Shape | ((long)zone.Size << 8) | ((long)zone.MinSize << 16) | ((long)(byte)zone.ForcedDirection << 24) | ((long)caster << 32) | ((long)centre << 48);
        var kept = _kept ??= [];

        if (kept.TryGetValue(key, out var cells))
            return cells;

        if (kept.Count >= Capacity)
            kept.Clear();

        cells = SpellZones.Cells(zone, listed, map, fighters, caster, centre);
        kept[key] = cells;

        return cells;
    }

    /// <summary>Lets go of every zone this thread keeps.</summary>
    public static void Clear()
    {
        _kept?.Clear();
    }
}
