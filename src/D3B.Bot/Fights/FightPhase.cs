namespace D3B.Bot.Fights;

/// <summary>Where a fight stands, from its context's creation to the roleplay's.</summary>
public enum FightPhase
{
    /// <summary>The fighters are placed on their teams' cells, until the server starts the fight.</summary>
    Placement,

    /// <summary>The fighters play their turns.</summary>
    Fighting,

    /// <summary>The server sent the fight's results, before it creates the roleplay's context again.</summary>
    Ended,
}
