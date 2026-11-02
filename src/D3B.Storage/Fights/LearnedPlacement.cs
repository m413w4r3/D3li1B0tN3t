namespace D3B.Storage.Fights;

/// <summary>A fight's placement cells, the map fought on and the one its fighters came from, as the bots saw fights begin (D275).</summary>
public sealed class LearnedPlacement
{
    /// <summary>Gets the map the character was on as the fight began.</summary>
    public long FromMapId { get; init; }

    /// <summary>Gets that map's area, as the pack places it, or null when it does not.</summary>
    public int? FromAreaId { get; init; }

    /// <summary>Gets that map's sub-area, or null.</summary>
    public int? FromSubAreaId { get; init; }

    /// <summary>Gets that map's horizontal coordinate on the world's map, or null.</summary>
    public int? PosX { get; init; }

    /// <summary>Gets that map's vertical coordinate on the world's map, or null.</summary>
    public int? PosY { get; init; }

    /// <summary>Gets the map fought on.</summary>
    public long MapId { get; init; }

    /// <summary>Gets the challengers' cells, red, in ascending order and separated by commas.</summary>
    public string RedCells { get; init; } = string.Empty;

    /// <summary>Gets the defenders' cells, blue, in ascending order and separated by commas.</summary>
    public string BlueCells { get; init; } = string.Empty;

    /// <summary>Gets or sets how many fights began with them.</summary>
    public int Seen { get; set; }

    /// <summary>Gets when the first did.</summary>
    public DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>Gets or sets when the last did.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
