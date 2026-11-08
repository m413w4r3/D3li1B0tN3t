using D3B.Gui.Actions;
using D3B.Gui.Calibration;

namespace D3B.Bot.Gui;

/// <summary>A protocol-neutral observation of one client-originated request.</summary>
public sealed record GuiClientRequestObservation
{
    /// <summary>Gets the session epoch where the request was observed.</summary>
    public long SessionEpoch { get; }

    /// <summary>Gets the map identifier where the request was observed.</summary>
    public long MapId { get; }

    /// <summary>Gets the client request category.</summary>
    public GuiClientAction Action { get; }

    /// <summary>Gets the requested cell when the request exposes one.</summary>
    public LogicalCell? TargetCellId { get; }

    /// <summary>Gets the interactive element identifier when the request exposes one.</summary>
    public int? TargetElementId { get; }

    /// <summary>Gets whether independent input observation reports a simultaneous human action.</summary>
    /// <remarks>Packet fields cannot cryptographically distinguish a matching human click from GUI input.</remarks>
    public bool PotentiallyConcurrentHumanInput { get; }

    /// <summary>Creates a neutral request observation for correlation.</summary>
    public GuiClientRequestObservation(
        long sessionEpoch,
        long mapId,
        GuiClientAction action,
        LogicalCell? targetCellId,
        int? targetElementId,
        bool potentiallyConcurrentHumanInput = false)
    {
        SessionEpoch = sessionEpoch;
        MapId = mapId;
        Action = action;
        TargetCellId = targetCellId;
        TargetElementId = targetElementId;
        PotentiallyConcurrentHumanInput = potentiallyConcurrentHumanInput;
    }
}
