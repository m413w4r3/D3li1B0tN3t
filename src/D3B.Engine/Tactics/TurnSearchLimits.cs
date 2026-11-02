namespace D3B.Engine.Tactics;

/// <summary>How far a turn's search goes: how many sequences it keeps at each step, how many casts deep, for how long.</summary>
/// <remarks>A turn lasts 30 seconds (client.md), and the bot searches again after each answer of the server (D201): each search is short.</remarks>
public sealed record TurnSearchLimits
{
    /// <summary>Gets the limits the bot searches with.</summary>
    public static TurnSearchLimits Default { get; } = new();

    /// <summary>Gets how many sequences each step keeps, the best ones.</summary>
    public int Width { get; init; } = 8;

    /// <summary>Gets the most casts a sequence holds.</summary>
    public int Depth { get; init; } = 6;

    /// <summary>Gets the most cells a fighter weighs walking to before a cast on each target cell.</summary>
    public int WalksPerTarget { get; init; } = 2;

    /// <summary>Gets the most cells a level is weighed cast on.</summary>
    public int CellsPerLevel { get; init; } = 24;

    /// <summary>Gets how long a search may last.</summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromMilliseconds(800);
}
