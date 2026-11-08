using System.Collections.Immutable;
using D3B.Gui.Calibration;

namespace D3B.Gui.Safety;

/// <summary>An immutable protocol view used to compare GUI analysis with the game state.</summary>
public sealed record GuiWorldSnapshot(
    long SessionEpoch,
    long StateRevision,
    DateTimeOffset CapturedAt,
    long? MapId,
    LogicalCell? CellId,
    GuiCellProvenance? CellProvenance,
    bool IsConnected,
    bool IsFighting,
    bool IsWalking,
    bool IsUsing,
    bool IsInDialog,
    bool HasAmbiguousMovement,
    ImmutableArray<GuiKnownElement> KnownElements,
    ProtocolObservationStatus ProtocolObservationStatus)
{
    /// <summary>Gets whether the player has taken control from the bot.</summary>
    public bool IsUserInterrupted { get; init; }
}
