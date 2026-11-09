using D3B.Bot.Characters;
using D3B.Gui.Safety;

namespace D3B.Bot.Interactives;

/// <summary>The passive evidence and decision for one supervised element-use target.</summary>
internal sealed record ElementUsePreparation
{
    /// <summary>Gets the preparation decision.</summary>
    public required ElementUsePreparationState State { get; init; }

    /// <summary>Gets the stable code naming the decision.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets the exact requested target.</summary>
    public ElementUseTarget Target { get; init; }

    /// <summary>Gets whether this host session's one attempt was already consumed.</summary>
    public bool AttemptConsumed { get; init; }

    /// <summary>Gets the bot session epoch observed during preparation.</summary>
    public long SessionEpoch { get; init; }

    /// <summary>Gets the character-state revision observed during preparation.</summary>
    public long StateRevision { get; init; }

    /// <summary>Gets whether the bot was connected during preparation.</summary>
    public bool Connected { get; init; }

    /// <summary>Gets whether the bot had an attached game session during preparation.</summary>
    public bool SessionAttached { get; init; }

    /// <summary>Gets whether the session's translation latch blocked requests.</summary>
    public bool TranslationBlocked { get; init; }

    /// <summary>Gets whether the player had paused the bot.</summary>
    public bool Paused { get; init; }

    /// <summary>Gets whether another behavior was acting for the bot.</summary>
    public bool Acting { get; init; }

    /// <summary>Gets the input mode observed on the attached session.</summary>
    public GameInputMode InputMode { get; init; }

    /// <summary>Gets the map in the character snapshot, when known.</summary>
    public long? MapId { get; init; }

    /// <summary>Gets the cell in the character snapshot, when known.</summary>
    public int? Cell { get; init; }

    /// <summary>Gets the provenance of the character's cell.</summary>
    public CellProvenance Provenance { get; init; }

    /// <summary>Gets whether the character's position is server-derived and trusted.</summary>
    public bool PositionTrusted { get; init; }

    /// <summary>Gets the element identifier resolved from the map and character state.</summary>
    public int? ElementId { get; init; }

    /// <summary>Gets the interactive identifier the server reported for the element.</summary>
    public int? InteractiveId { get; init; }

    /// <summary>Gets the element's cell from map data, when it has one.</summary>
    public int? ElementCell { get; init; }

    /// <summary>Gets the element's graphic identifier from map data.</summary>
    public int? GfxId { get; init; }

    /// <summary>Gets the enabled skill identifier resolved for the element.</summary>
    public int? SkillId { get; init; }

    /// <summary>Gets the server-provided skill instance identifier.</summary>
    public int? SkillInstanceId { get; init; }

    /// <summary>Gets the item identifier the catalog says the skill gathers.</summary>
    public int? ExpectedItemId { get; init; }

    /// <summary>Gets the catalog skill's minimum profession level.</summary>
    public int? SkillMinLevel { get; init; }

    /// <summary>Gets whether the current cell is one from which this element can be used.</summary>
    public bool UsesFromCurrentCell { get; init; }

    /// <summary>Gets whether this preparation allows the one request attempt.</summary>
    public bool CanAttempt => State is ElementUsePreparationState.Ready;
}
