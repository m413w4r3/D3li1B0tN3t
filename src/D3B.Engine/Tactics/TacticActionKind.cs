namespace D3B.Engine.Tactics;

/// <summary>What an action of the fighter playing is.</summary>
public enum TacticActionKind
{
    /// <summary>A walk to a cell, its MP spent and its tackles taken.</summary>
    Walk,

    /// <summary>A cast of a spell's level on a cell.</summary>
    Cast,
}
