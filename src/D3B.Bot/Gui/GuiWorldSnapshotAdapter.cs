using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Safety;

namespace D3B.Bot.Gui;

/// <summary>Builds immutable GUI snapshots from the bot's atomically published protocol view.</summary>
public sealed class GuiWorldSnapshotAdapter
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the adapter with the clock used to timestamp snapshots.</summary>
    /// <param name="timeProvider">The clock to use.</param>
    public GuiWorldSnapshotAdapter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Captures the actor's current published state as a GUI snapshot.</summary>
    /// <param name="bot">The actor whose state to capture.</param>
    /// <returns>An immutable snapshot from one published actor view.</returns>
    public GuiWorldSnapshot Capture(BotActor bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var translationBlockedBeforeView = bot.IsMovementTranslationBlocked;
        var view = bot.View;
        var translationBlockedAfterView = bot.IsMovementTranslationBlocked;
        var state = view.State;
        // The relay can close its latch outside the bot loop, so a close racing the view read must fail closed.
        var translationBlocked = translationBlockedBeforeView || view.IsTranslationBlocked || translationBlockedAfterView;

        return new GuiWorldSnapshot(
            view.SessionEpoch,
            view.Revision,
            _timeProvider.GetUtcNow(),
            state.MapId,
            ToLogicalCell(state.Cell),
            ToGuiProvenance(state.Provenance),
            view.IsConnected,
            state.IsFighting,
            state.IsWalking,
            state.IsUsing,
            state.IsInDialog,
            state.MovementAnswerAmbiguous || state.IsWalkHeld,
            state.Elements.Values.Select(ToKnownElement).ToImmutableArray(),
            GetObservationStatus(state, view.IsConnected, translationBlocked))
        {
            IsUserInterrupted = view.IsPaused,
        };
    }

    private static GuiKnownElement ToKnownElement(MapElement element)
    {
        var enabledSkillIds = element.EnabledSkills.IsDefaultOrEmpty
            ? ImmutableArray<int>.Empty
            : element.EnabledSkills.Select(skill => skill.SkillId).ToImmutableArray();

        return new GuiKnownElement(
            element.ElementId,
            ToLogicalCell(element.Cell),
            element.InteractiveId,
            element.State,
            enabledSkillIds);
    }

    private static LogicalCell? ToLogicalCell(int? cell)
    {
        if (cell is not { } value || value < 0 || value >= MapData.CellCount)
            return null;

        return new LogicalCell(value);
    }

    private static GuiCellProvenance? ToGuiProvenance(CellProvenance provenance)
    {
        return provenance switch
        {
            CellProvenance.ClientReported => GuiCellProvenance.ClientReported,
            CellProvenance.Estimated => GuiCellProvenance.Estimated,
            CellProvenance.ServerPlaced => GuiCellProvenance.ServerPlaced,
            CellProvenance.ServerConfirmed => GuiCellProvenance.ServerConfirmed,
            CellProvenance.ServerRefused => GuiCellProvenance.ServerRefused,
            _ => null,
        };
    }

    private static ProtocolObservationStatus GetObservationStatus(CharacterState state, bool isConnected, bool translationBlocked)
    {
        if (translationBlocked)
            return ProtocolObservationStatus.TranslationBlocked;

        if (state.MovementAnswerAmbiguous)
            return ProtocolObservationStatus.Contradictory;

        if (state.MapId is null || ToLogicalCell(state.Cell) is null)
            return ProtocolObservationStatus.Partial;

        if (!isConnected)
            return ProtocolObservationStatus.Unknown;

        return ProtocolObservationStatus.Complete;
    }
}
