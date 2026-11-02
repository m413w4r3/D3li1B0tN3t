using System.Collections.Immutable;

namespace D3B.App.Controls;

/// <summary>What the bot's activity looks at on the map drawn, by cells (D131).</summary>
internal sealed record MapActivity
{
    /// <summary>Gets the cells of the elements the activity wants, such as the resources it harvests.</summary>
    public ImmutableArray<int> Wanted { get; init; } = [];

    /// <summary>Gets the cell of the element the activity goes for now.</summary>
    public int? Target { get; init; }

    /// <summary>Gets the cells of the exit the character heads to.</summary>
    public ImmutableArray<int> NextExit { get; init; } = [];
}
