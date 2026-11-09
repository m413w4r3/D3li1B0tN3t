using D3B.Gui.Actions;
using D3B.Gui.Vision;

namespace D3B.Bot.Gui;

/// <summary>Returns the terminal action result and the map, cell, intent, and projection it concerned.</summary>
public sealed class GuiWalkToCellResult
{
    /// <summary>Gets the existing GUI action outcome.</summary>
    public GuiActionOutcome Outcome { get; }

    /// <summary>Gets the registered intention identifier, when an intention was created.</summary>
    public Guid? IntentId { get; }

    /// <summary>Gets the observed map identifier, when protocol state supplied one.</summary>
    public long? MapId { get; }

    /// <summary>Gets the requested destination, when a reachable plan supplied one.</summary>
    public int? TargetCellId { get; }

    /// <summary>Gets the projected target evidence, when localization ran.</summary>
    public TargetRegion? Target { get; }

    /// <summary>Creates a result for one coordinator attempt.</summary>
    public GuiWalkToCellResult(
        GuiActionOutcome outcome,
        Guid? intentId,
        long? mapId,
        int? targetCellId,
        TargetRegion? target)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        Outcome = outcome;
        IntentId = intentId;
        MapId = mapId;
        TargetCellId = targetCellId;
        Target = target;
    }
}
