using D3B.Bot.Characters;
using D3B.Gui.Safety;

namespace D3B.Bot.Interactives;

/// <summary>The preparation, single-request flags, server observations, and inventory proof for a supervised use.</summary>
internal sealed record SupervisedElementUseEvidence
{
    /// <summary>Gets the exact requested target.</summary>
    public ElementUseTarget Target { get; init; }

    /// <summary>Gets the bot session epoch from preparation.</summary>
    public long SessionEpoch { get; init; }

    /// <summary>Gets the character-state revision from preparation.</summary>
    public long StateRevision { get; init; }

    /// <summary>Gets the starting map from preparation, when known.</summary>
    public long? StartMapId { get; init; }

    /// <summary>Gets the starting cell from preparation, when known.</summary>
    public int? StartCell { get; init; }

    /// <summary>Gets the provenance of the starting cell.</summary>
    public CellProvenance StartProvenance { get; init; }

    /// <summary>Gets the element cell resolved during preparation, when known.</summary>
    public int? ElementCell { get; init; }

    /// <summary>Gets the skill instance resolved during preparation, when known.</summary>
    public int? SkillInstanceId { get; init; }

    /// <summary>Gets the item identifier the skill is expected to gather, when known.</summary>
    public int? ExpectedItemId { get; init; }

    /// <summary>Gets whether the translation latch was closed during preparation.</summary>
    public bool TranslationBlocked { get; init; }

    /// <summary>Gets the input mode observed during preparation.</summary>
    public GameInputMode InputMode { get; init; }

    /// <summary>Gets whether the request send completed successfully.</summary>
    public bool RequestSent { get; init; }

    /// <summary>Gets whether the request may have been sent before an uncertain failure.</summary>
    public bool RequestMayHaveBeenSent { get; init; }

    /// <summary>Gets whether the server's matching start event was observed.</summary>
    public bool InteractiveUsedObserved { get; init; }

    /// <summary>Gets whether the matching use-end event was observed.</summary>
    public bool InteractiveUseEndedObserved { get; init; }

    /// <summary>Gets the carried item count immediately before the request, when captured.</summary>
    public int? InventoryBefore { get; init; }

    /// <summary>Gets the carried item count after waiting for inventory proof, when captured.</summary>
    public int? InventoryAfter { get; init; }

    /// <summary>Gets the skill instance named by a server refusal, when present.</summary>
    public int? ErrorSkillInstanceId { get; init; }

    /// <summary>Gets the ending map from the bot's current state, when known.</summary>
    public long? EndMapId { get; init; }

    /// <summary>Gets the ending cell from the bot's current state, when known.</summary>
    public int? EndCell { get; init; }

    /// <summary>Gets whether the ending map and cell are trusted for actions.</summary>
    public bool EndPositionTrusted { get; init; }

    /// <summary>Gets the provenance of the ending cell.</summary>
    public CellProvenance EndProvenance { get; init; }

    /// <summary>Gets the inventory weight revision observed at the use request's send barrier.</summary>
    public long? WeightRevisionBefore { get; init; }
}
