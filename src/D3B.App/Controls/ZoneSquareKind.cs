namespace D3B.App.Controls;

/// <summary>What a map of a zone's mini-map is, which gives its colour (D127, D131).</summary>
internal enum ZoneSquareKind
{
    /// <summary>A map around the zone, outside it, drawn dim for its place.</summary>
    Context,

    /// <summary>A map of the zone's first part, such as its first sub-area, and of every other part but the second's.</summary>
    GroupA,

    /// <summary>A map of the zone's second part.</summary>
    GroupB,

    /// <summary>A map the tour went through.</summary>
    Visited,

    /// <summary>A map the tour still goes through, what it holds not known.</summary>
    ToVisit,

    /// <summary>A map seen holding nothing the activity wants, or whose graphics all offer none of it, which the tour leaves out.</summary>
    Empty,

    /// <summary>A map the way to failed.</summary>
    Unreachable,

    /// <summary>A map the tour still goes through, likely holding a resource wanted (D138).</summary>
    Probable,

    /// <summary>A map reached only far round, which the tour leaves out unless the settings take it (D149).</summary>
    Isolated,

    /// <summary>A map the tour went through, whose resources grow back (D145).</summary>
    Growing,
}
