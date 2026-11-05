namespace D3B.Diagnostic.Resolution;

/// <summary>A ranking hint that is explicitly not proof.</summary>
public sealed record HeuristicScore
{
    /// <summary>Heuristic similarity value from zero through one.</summary>
    public double Value { get; init; }

    /// <summary>Fixed label preventing the score from being mistaken for proof.</summary>
    public string Kind { get; init; } = "heuristic";
}
