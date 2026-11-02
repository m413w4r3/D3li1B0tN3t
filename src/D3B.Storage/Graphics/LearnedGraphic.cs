namespace D3B.Storage.Graphics;

/// <summary>A graphic of the maps' elements, and the skills the server said the elements drawn with it offer (D138).</summary>
public sealed class LearnedGraphic
{
    /// <summary>Gets the graphic, as the pack's maps name it.</summary>
    public int GfxId { get; init; }

    /// <summary>Gets or sets the skills seen offered on its elements, enabled or not, in ascending order; empty when they offer none.</summary>
    public List<int> Skills { get; set; } = [];
}
