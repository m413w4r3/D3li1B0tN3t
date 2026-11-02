namespace D3B.Bot.Harvesting;

/// <summary>How the harvesting activity chooses its resources (D160, D165).</summary>
public enum HarvestMode
{
    /// <summary>Every resource of the professions chosen, but those left out, each character's own levels deciding.</summary>
    ByProfession,

    /// <summary>The resources listed, of any profession, the first harvested first on each map.</summary>
    List,
}
