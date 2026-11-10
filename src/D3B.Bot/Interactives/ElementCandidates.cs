using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Bot.Interactives;

/// <summary>Builds passive, deterministic diagnostics for possible harvest elements.</summary>
internal static class ElementCandidates
{
    public static ElementCandidateReport Capture(
        BotActor bot,
        GameTables tables,
        GraphicSkills? graphics = null,
        Func<int, string?>? itemName = null,
        bool attemptConsumed = false,
        bool actionEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(tables);

        return Build(SupervisedElementUse.Capture(bot), tables, graphics, itemName, attemptConsumed, actionEnabled);
    }

    public static ElementCandidateReport Build(
        ElementUseSnapshot snapshot,
        GameTables tables,
        GraphicSkills? graphics,
        Func<int, string?>? itemName,
        bool attemptConsumed = false,
        bool actionEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(tables);

        var state = snapshot.State;
        var map = snapshot.Map;
        var elementIds = new SortedSet<int>(state.Elements.Keys);

        if (map is not null)
        {
            foreach (var interactive in map.Interactives.Span)
                elementIds.Add(interactive.ElementId);
        }

        var candidates = ImmutableArray.CreateBuilder<ElementCandidate>();

        foreach (var elementId in elementIds)
        {
            MapElement? element = state.Elements.TryGetValue(elementId, out var foundElement) ? foundElement : null;
            var serverKnown = element is not null;
            MapInteractive interactive = default;
            var packKnown = map is not null && map.TryGetInteractive(elementId, out interactive);
            var serverSkills = new List<(int SkillId, int InstanceId, bool Enabled)>();

            if (element is not null)
            {
                foreach (var skill in element.EnabledSkills)
                {
                    if (!serverSkills.Any(existing => existing.SkillId == skill.SkillId))
                        serverSkills.Add((skill.SkillId, skill.InstanceId, true));
                }

                foreach (var skill in element.DisabledSkills)
                {
                    if (!serverSkills.Any(existing => existing.SkillId == skill.SkillId))
                        serverSkills.Add((skill.SkillId, skill.InstanceId, false));
                }

                serverSkills.Sort(static (left, right) => left.SkillId.CompareTo(right.SkillId));
            }

            var graphicKnown = false;
            ImmutableArray<int> hintSkillIds = [];

            if (graphics is not null && packKnown)
            {
                if (graphics.TryGet(interactive.GfxId, out var knownSkills) && knownSkills is not null)
                {
                    graphicKnown = true;
                    hintSkillIds = [.. knownSkills.Order()];
                }
            }

            var hasServerEvidence = serverSkills.Count > 0;
            var usesHintEvidence = !hasServerEvidence && !hintSkillIds.IsDefaultOrEmpty;
            var evidenceSkillIds = hasServerEvidence
                ? serverSkills.Select(skill => skill.SkillId).ToImmutableArray()
                : hintSkillIds;
            var skillFacts = new Dictionary<int, (bool InCatalog, bool Gathers, int? ItemId, int? MinLevel)>();

            foreach (var evidenceSkillId in evidenceSkillIds)
            {
                var inCatalog = tables.Jobs.TryGetSkill(evidenceSkillId, out var catalogSkill);
                var gathers = inCatalog
                    && catalogSkill.GatheredItemId != Skill.NoItem
                    && tables.Items.TryGetItem(catalogSkill.GatheredItemId, out _);
                int? itemId = gathers ? catalogSkill.GatheredItemId : null;
                int? minLevel = inCatalog ? catalogSkill.MinLevel : null;
                skillFacts.Add(evidenceSkillId, (inCatalog, gathers, itemId, minLevel));
            }

            var catalogMatchCount = skillFacts.Values.Count(fact => fact.InCatalog);
            var catalog = evidenceSkillIds.IsDefaultOrEmpty
                ? ElementCandidateCatalog.Unknown
                : catalogMatchCount is 0
                    ? ElementCandidateCatalog.Unmatched
                    : catalogMatchCount == evidenceSkillIds.Length
                        ? ElementCandidateCatalog.Matched
                        : ElementCandidateCatalog.Partial;
            var possibleItemIds = skillFacts.Values
                .Where(fact => fact.Gathers)
                .Select(fact => fact.ItemId!.Value)
                .Distinct()
                .Order()
                .ToImmutableArray();
            var hasUnknownSkill = evidenceSkillIds.Any(skillId => !skillFacts[skillId].InCatalog);
            var hasGatheringSkill = skillFacts.Values.Any(fact => fact.Gathers);
            var include = evidenceSkillIds.IsDefaultOrEmpty
                ? !graphicKnown || (element?.State is 1 or 2)
                : hasGatheringSkill || hasUnknownSkill;

            if (!include)
                continue;

            var serverCell = element?.Cell;
            var packCell = packKnown && interactive.CellId is not MapInteractive.NoCell
                ? (int?)interactive.CellId
                : null;
            var cellConflict = serverCell is { } statedCell && packCell is { } drawnCell && statedCell != drawnCell;
            var identity = possibleItemIds.Length > 1 || cellConflict
                || (hasUnknownSkill && possibleItemIds.Length > 0)
                || (hasServerEvidence && catalogMatchCount is 0 && hintSkillIds.Length > 0)
                    ? ElementCandidateIdentity.Ambiguous
                    : possibleItemIds.Length == 1
                        ? hasServerEvidence ? ElementCandidateIdentity.Verified : ElementCandidateIdentity.Inferred
                        : ElementCandidateIdentity.Unknown;
            var identitySource = IdentitySource(
                identity,
                hasServerEvidence,
                usesHintEvidence,
                serverKnown,
                catalogMatchCount);
            var skillIdAndInstance = SelectServerSkill(serverSkills, skillFacts, possibleItemIds.Length > 1);
            int? skillId = skillIdAndInstance?.SkillId;
            int? skillInstanceId = skillIdAndInstance?.InstanceId;
            bool? skillEnabled = skillIdAndInstance?.Enabled;

            if (skillId is null && !hasServerEvidence && possibleItemIds.Length <= 1)
            {
                skillId = hintSkillIds
                    .Where(hintSkillId => skillFacts[hintSkillId].Gathers)
                    .Select(hintSkillId => (int?)hintSkillId)
                    .FirstOrDefault()
                    ?? hintSkillIds.Select(hintSkillId => (int?)hintSkillId).FirstOrDefault();
            }

            var skillMinLevel = skillId is { } selectedSkillId && skillFacts.TryGetValue(selectedSkillId, out var selectedFact)
                ? selectedFact.MinLevel
                : null;
            var gatheredItemId = possibleItemIds.Length == 1
                && (identity is ElementCandidateIdentity.Verified or ElementCandidateIdentity.Inferred || cellConflict)
                    ? (int?)possibleItemIds[0]
                    : null;
            var gatheredItemName = gatheredItemId is { } namedItemId ? itemName?.Invoke(namedItemId) : null;
            var usesFromCurrentCell = UsesFromCurrentCell(state, map, packCell, cellConflict);
            var (preparationState, preparationReason) = PrepareCandidate(
                snapshot,
                tables,
                elementId,
                serverKnown,
                packKnown,
                identity,
                cellConflict,
                skillId,
                attemptConsumed);

            candidates.Add(new ElementCandidate
            {
                ElementId = elementId,
                MapId = state.MapId,
                ServerKnown = serverKnown,
                PackKnown = packKnown,
                InteractiveId = element?.InteractiveId,
                ServerCell = serverCell,
                PackCell = packCell,
                GfxId = packKnown ? interactive.GfxId : null,
                ElementState = element?.State,
                SkillId = skillId,
                SkillInstanceId = skillInstanceId,
                SkillIds = evidenceSkillIds,
                SkillEnabled = skillEnabled,
                Catalog = catalog,
                GatheredItemId = gatheredItemId,
                GatheredItemName = gatheredItemName,
                PossibleItemIds = possibleItemIds,
                SkillMinLevel = skillMinLevel,
                UsesFromCurrentCell = usesFromCurrentCell,
                Identity = identity,
                IdentitySource = identitySource,
                CellConflict = cellConflict,
                PreparationState = preparationState,
                PreparationReason = preparationReason,
            });
        }

        var candidateArray = candidates.ToImmutable();
        var target = TargetVerdict(candidateArray);
        var (action, actionReason) = ActionVerdict(snapshot, candidateArray, target, attemptConsumed, actionEnabled);

        return new ElementCandidateReport
        {
            Snapshot = snapshot,
            Reason = "READY",
            Candidates = candidateArray,
            Target = target,
            Action = action,
            ActionReason = actionReason,
            AttemptConsumed = attemptConsumed,
            ActionEnabled = actionEnabled,
        };
    }

    private static string IdentitySource(
        ElementCandidateIdentity identity,
        bool hasServerEvidence,
        bool usesHintEvidence,
        bool serverKnown,
        int catalogMatchCount)
    {
        if (hasServerEvidence && (identity is ElementCandidateIdentity.Verified or ElementCandidateIdentity.Ambiguous || catalogMatchCount > 0))
            return "server+catalog";

        if (usesHintEvidence && (identity is ElementCandidateIdentity.Inferred or ElementCandidateIdentity.Ambiguous || catalogMatchCount > 0))
            return "pack+graphics+catalog";

        if (identity is ElementCandidateIdentity.Ambiguous && serverKnown)
            return "server+catalog";

        if (hasServerEvidence)
            return "server";

        if (usesHintEvidence)
            return "pack+graphics";

        return serverKnown ? "server" : "pack";
    }

    private static (int SkillId, int InstanceId, bool Enabled)? SelectServerSkill(
        List<(int SkillId, int InstanceId, bool Enabled)> serverSkills,
        Dictionary<int, (bool InCatalog, bool Gathers, int? ItemId, int? MinLevel)> skillFacts,
        bool multipleItems)
    {
        if (multipleItems)
            return null;

        foreach (var skill in serverSkills)
        {
            if (skill.Enabled && skillFacts[skill.SkillId].Gathers)
                return skill;
        }

        foreach (var skill in serverSkills)
        {
            if (!skill.Enabled && skillFacts[skill.SkillId].Gathers)
                return skill;
        }

        foreach (var skill in serverSkills)
        {
            if (skillFacts[skill.SkillId].InCatalog)
                return skill;
        }

        return serverSkills.Count > 0 ? serverSkills[0] : null;
    }

    private static bool? UsesFromCurrentCell(CharacterState state, MapData? map, int? packCell, bool cellConflict)
    {
        if (!state.IsPositionTrusted
            || state.Cell is not { } currentCell
            || packCell is not { } elementCell
            || map is null
            || cellConflict
            || currentCell is < 0 or >= MapData.CellCount
            || elementCell is < 0 or >= MapData.CellCount)
        {
            return null;
        }

        var cells = map.Cells.Span;

        if (cells.Length != MapData.CellCount)
            return null;

        return ElementCells.UsedFrom(cells, elementCell, cells[currentCell].Zone).Contains(currentCell);
    }

    private static (ElementUsePreparationState State, string Reason) PrepareCandidate(
        ElementUseSnapshot snapshot,
        GameTables tables,
        int elementId,
        bool serverKnown,
        bool packKnown,
        ElementCandidateIdentity identity,
        bool cellConflict,
        int? skillId,
        bool attemptConsumed)
    {
        if (identity is ElementCandidateIdentity.Ambiguous)
        {
            return cellConflict
                ? (ElementUsePreparationState.Blocked, "ELEMENT_CELL_CONFLICT")
                : (ElementUsePreparationState.Blocked, "TARGET_AMBIGUOUS");
        }

        if (skillId is not null && snapshot.State.MapId is { } mapId && snapshot.State.Cell is { } cell)
        {
            var preparation = SupervisedElementUse.Prepare(
                snapshot,
                tables,
                new ElementUseTarget(mapId, cell, elementId, skillId.Value),
                attemptConsumed);

            if (preparation.State is ElementUsePreparationState.Ready && identity is not ElementCandidateIdentity.Verified)
                return (ElementUsePreparationState.Blocked, "TARGET_AMBIGUOUS");

            return (preparation.State, preparation.Reason);
        }

        if (!packKnown && serverKnown)
            return (ElementUsePreparationState.Unavailable, "ELEMENT_NOT_ON_MAP");

        if (!serverKnown)
            return (ElementUsePreparationState.Unavailable, "ELEMENT_NOT_KNOWN");

        return (
            ElementUsePreparationState.Blocked,
            SupervisedElementUse.SessionBlocker(snapshot, attemptConsumed) ?? "TARGET_UNKNOWN");
    }

    private static ElementCandidateTargetVerdict TargetVerdict(ImmutableArray<ElementCandidate> candidates)
    {
        if (candidates.IsDefaultOrEmpty)
            return ElementCandidateTargetVerdict.Unknown;

        if (candidates.Length == 1 && candidates[0].Identity is ElementCandidateIdentity.Verified)
            return ElementCandidateTargetVerdict.Identified;

        return ElementCandidateTargetVerdict.Ambiguous;
    }

    private static (ElementCandidateActionVerdict Action, string Reason) ActionVerdict(
        ElementUseSnapshot snapshot,
        ImmutableArray<ElementCandidate> candidates,
        ElementCandidateTargetVerdict target,
        bool attemptConsumed,
        bool actionEnabled)
    {
        if (SupervisedElementUse.SessionBlocker(snapshot, attemptConsumed) is { } blocker)
            return (ElementCandidateActionVerdict.Blocked, blocker);

        if (candidates.IsDefaultOrEmpty)
            return (ElementCandidateActionVerdict.Blocked, "TARGET_UNKNOWN");

        if (target is ElementCandidateTargetVerdict.Ambiguous)
        {
            return candidates.Length == 1
                ? (ElementCandidateActionVerdict.Blocked, candidates[0].PreparationReason)
                : (ElementCandidateActionVerdict.Blocked, "TARGET_AMBIGUOUS");
        }

        var candidate = candidates[0];

        if (candidate.PreparationState is not ElementUsePreparationState.Ready)
            return (ElementCandidateActionVerdict.Blocked, candidate.PreparationReason);

        if (!actionEnabled)
            return (ElementCandidateActionVerdict.Blocked, "ACTION_DISABLED");

        return (ElementCandidateActionVerdict.Ready, "READY");
    }
}
