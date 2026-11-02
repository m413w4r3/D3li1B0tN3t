namespace D3B.Bot.Harvesting;

/// <summary>What a map not seen yet likely holds, from the graphics of its elements (D138).</summary>
public enum MapForecast
{
    /// <summary>Some of its graphics are not known yet.</summary>
    Unknown,

    /// <summary>One of its graphics offers a skill the zone uses, elsewhere: it likely holds a resource wanted.</summary>
    Probable,

    /// <summary>Every graphic is known, and none offers a skill the zone uses: it likely holds none.</summary>
    Without,
}
