using D3B.Gui.Actions;
using D3B.Gui.Calibration;

namespace D3B.Bot.Gui;

/// <summary>A protocol-neutral observation of one server-side action outcome.</summary>
public sealed record GuiServerOutcomeObservation
{
    /// <summary>Gets the session epoch where the outcome was observed.</summary>
    public long SessionEpoch { get; }

    /// <summary>Gets the map identifier where the outcome was observed.</summary>
    public long MapId { get; }

    /// <summary>Gets the server outcome category.</summary>
    public GuiServerOutcome Outcome { get; }

    /// <summary>Gets the confirmed target cell when the outcome exposes one.</summary>
    public LogicalCell? TargetCellId { get; }

    /// <summary>Gets the confirmed element identifier when the outcome exposes one.</summary>
    public int? TargetElementId { get; }

    /// <summary>Creates a neutral server outcome observation for correlation.</summary>
    public GuiServerOutcomeObservation(
        long sessionEpoch,
        long mapId,
        GuiServerOutcome outcome,
        LogicalCell? targetCellId,
        int? targetElementId)
    {
        SessionEpoch = sessionEpoch;
        MapId = mapId;
        Outcome = outcome;
        TargetCellId = targetCellId;
        TargetElementId = targetElementId;
    }
}
