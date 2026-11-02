namespace D3B.Engine.Tactics;

/// <summary>How a fight looks as it starts: the turns each side would take to beat the other, each dealing its best each turn.</summary>
/// <param name="TurnsToWin">The turns the character's side takes to take the enemies' life, at the damage it deals a turn.</param>
/// <param name="TurnsToLose">The turns the enemies take to take the character's side's life.</param>
/// <remarks>A side that deals nothing takes forever, <see cref="double.PositiveInfinity"/>. The owner's choice (D198): a fight judged too risky is fought prudently.</remarks>
public readonly record struct FightOdds(double TurnsToWin, double TurnsToLose)
{
    /// <summary>Gets whether the enemies would beat the character's side first.</summary>
    public bool IsRisky => TurnsToLose < TurnsToWin;
}
