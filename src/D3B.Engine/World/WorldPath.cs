using D3B.Data.World;

namespace D3B.Engine.World;

/// <summary>A path through the world: the map changes that lead from a vertex of the graph to one of the maps sought.</summary>
public sealed class WorldPath
{
    private readonly WorldStep[] _steps;

    /// <summary>Gets the vertex the path starts from, by index in <see cref="WorldGraph.Vertices"/>.</summary>
    public int From { get; }

    /// <summary>Gets the vertex it ends on: <see cref="From"/> when it has no step.</summary>
    public int To => _steps.Length is 0 ? From : _steps[^1].To;

    /// <summary>Gets its steps, in order, each leaving the vertex the one before reached.</summary>
    public IReadOnlyList<WorldStep> Steps => _steps;

    /// <summary>Initializes a new instance of the <see cref="WorldPath"/> class.</summary>
    /// <param name="from">The vertex it starts from.</param>
    /// <param name="steps">Its steps, each leaving the vertex the one before reached.</param>
    /// <exception cref="ArgumentException">A step does not leave the vertex the one before reached.</exception>
    public WorldPath(int from, IReadOnlyList<WorldStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var at = from;

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].From != at)
                throw new ArgumentException($"Step {i} leaves vertex {steps[i].From}, not {at}, where the path stands.", nameof(steps));

            at = steps[i].To;
        }

        From = from;
        _steps = [.. steps];
    }
}
