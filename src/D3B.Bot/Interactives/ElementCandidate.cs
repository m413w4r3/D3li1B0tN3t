using System.Collections.Immutable;

namespace D3B.Bot.Interactives;

/// <summary>One passive candidate on the current map, with the provenance of every datum.</summary>
internal sealed record ElementCandidate
{
    public required int ElementId { get; init; }

    public long? MapId { get; init; }

    /// <summary>Whether the server reported this element and its current skills.</summary>
    public bool ServerKnown { get; init; }

    /// <summary>Whether the current map pack contains this element.</summary>
    public bool PackKnown { get; init; }

    public int? InteractiveId { get; init; }

    public int? ServerCell { get; init; }

    public int? PackCell { get; init; }

    public int? GfxId { get; init; }

    public int? ElementState { get; init; }

    public int? SkillId { get; init; }

    public int? SkillInstanceId { get; init; }

    public ImmutableArray<int> SkillIds { get; init; } = [];

    public bool? SkillEnabled { get; init; }

    /// <summary>How evidence skills match the job catalog and its gathered item table.</summary>
    public ElementCandidateCatalog Catalog { get; init; }

    public int? GatheredItemId { get; init; }

    public string? GatheredItemName { get; init; }

    public ImmutableArray<int> PossibleItemIds { get; init; } = [];

    public int? SkillMinLevel { get; init; }

    public bool? UsesFromCurrentCell { get; init; }

    /// <summary>Whether item identity is unknown, inferred from a graphic, verified by server skills, or ambiguous.</summary>
    public ElementCandidateIdentity Identity { get; init; }

    public string IdentitySource { get; init; } = "unknown";

    public bool CellConflict { get; init; }

    public ElementUsePreparationState PreparationState { get; init; }

    public string PreparationReason { get; init; } = "UNKNOWN";
}
