using System.Collections.Immutable;
using D3B.Bot.Interactives;

namespace D3B.Bot.Harvesting;

/// <summary>An exact, server-available resource target identified from passive element evidence.</summary>
internal sealed record HarvestResourceTarget
{
    public long? MapId { get; init; }

    public int Cell { get; init; }

    public int ElementId { get; init; }

    public int? InteractiveId { get; init; }

    public int? GfxId { get; init; }

    public int SkillId { get; init; }

    public int SkillInstanceId { get; init; }

    public int ExpectedItemId { get; init; }

    public ElementCandidateIdentity Identity { get; init; }

    public string Provenance { get; init; } = "unknown";

    public int ElementState { get; init; }

    public static HarvestResourceTargetSelection Select(ElementCandidateReport report, int wantedItemId)
    {
        ArgumentNullException.ThrowIfNull(report);

        var eligible = ImmutableArray.CreateBuilder<HarvestResourceTarget>();
        var rejected = ImmutableArray.CreateBuilder<HarvestResourceTargetRejection>();
        var ambiguousEvidence = false;

        foreach (var candidate in report.Candidates)
        {
            var possibleContainsItem = candidate.PossibleItemIds.Contains(wantedItemId);
            var exactItemEvidence = candidate.PossibleItemIds.Length == 1 && possibleContainsItem;
            var exactIdentity = candidate.Identity is ElementCandidateIdentity.Verified
                && candidate.GatheredItemId == wantedItemId
                && exactItemEvidence;

            if (!exactIdentity)
            {
                var ambiguous = possibleContainsItem && (candidate.Identity is not ElementCandidateIdentity.Verified || !exactItemEvidence);
                if (ambiguous)
                    ambiguousEvidence = true;

                rejected.Add(new HarvestResourceTargetRejection(candidate, ambiguous ? "AMBIGUOUS_EVIDENCE" : "WRONG_ITEM_OR_IDENTITY"));
                continue;
            }

            if (candidate.SkillId is not { } skillId)
            {
                rejected.Add(new HarvestResourceTargetRejection(candidate, "SKILL_UNKNOWN"));
                continue;
            }

            if (candidate.SkillInstanceId is not { } skillInstanceId)
            {
                rejected.Add(new HarvestResourceTargetRejection(candidate, "SKILL_INSTANCE_UNKNOWN"));
                continue;
            }

            if (candidate.ServerCell is not { } cell)
            {
                rejected.Add(new HarvestResourceTargetRejection(candidate, "CELL_UNKNOWN"));
                continue;
            }

            if (candidate.SkillEnabled is not true)
            {
                rejected.Add(new HarvestResourceTargetRejection(candidate, "SKILL_DISABLED_OR_UNKNOWN"));
                continue;
            }

            if (candidate.ElementState is not 0)
            {
                rejected.Add(new HarvestResourceTargetRejection(candidate, "ELEMENT_UNAVAILABLE"));
                continue;
            }

            eligible.Add(new HarvestResourceTarget
            {
                MapId = candidate.MapId,
                Cell = cell,
                ElementId = candidate.ElementId,
                InteractiveId = candidate.InteractiveId,
                GfxId = candidate.GfxId,
                SkillId = skillId,
                SkillInstanceId = skillInstanceId,
                ExpectedItemId = wantedItemId,
                Identity = candidate.Identity,
                Provenance = candidate.IdentitySource,
                ElementState = candidate.ElementState.Value,
            });
        }

        var eligibleTargets = eligible.ToImmutable();
        var verdict = eligibleTargets.Length switch
        {
            1 => HarvestResourceTargetVerdict.Identified,
            > 1 => HarvestResourceTargetVerdict.Multiple,
            _ when ambiguousEvidence => HarvestResourceTargetVerdict.Ambiguous,
            _ => HarvestResourceTargetVerdict.NotFound,
        };

        return new HarvestResourceTargetSelection
        {
            Verdict = verdict,
            EligibleTargets = eligibleTargets,
            RejectedCandidates = rejected.ToImmutable(),
        };
    }
}
