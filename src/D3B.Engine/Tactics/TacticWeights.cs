namespace D3B.Engine.Tactics;

/// <summary>How much the AI weighs what it fears against what it wants: the damage the enemies may deal next, the damage it may deal next turn, its own life.</summary>
/// <remarks>
/// <see cref="Default"/> fights; <see cref="Prudent"/>, for a fight judged too risky, the owner's choice (D198), fears the
/// enemies' blows more and holds its own life dearer, so that it heals, shields and keeps its distance sooner.
/// </remarks>
public sealed record TacticWeights
{
    /// <summary>Gets the weights of a fight the AI expects to win.</summary>
    public static TacticWeights Default { get; } = new();

    /// <summary>Gets the weights of a fight judged too risky.</summary>
    public static TacticWeights Prudent { get; } = new() { Threat = 1.6, Survival = 3, Approach = 0.5 };

    /// <summary>Gets the weight of the life the enemies may take next, before the fighter playing plays again.</summary>
    public double Threat { get; init; } = 1;

    /// <summary>Gets the weight of the damage the fighter playing may deal next turn from where it ends this one.</summary>
    /// <remarks>
    /// Measured live on the owner's first fights (D203): at a quarter, the AI kept out of reach of enemies it beat, walking away
    /// and passing turns; next turn's damage is weighed nearly as an enemy's life is, so that it trades a blow for a stronger one.
    /// </remarks>
    public double Approach { get; init; } = 0.8;

    /// <summary>Gets how much dearer the fighter playing holds its own life than an ally's: its death ends its fight.</summary>
    public double Survival { get; init; } = 2;
}
