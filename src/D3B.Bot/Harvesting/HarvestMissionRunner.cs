using System.Collections.Immutable;
using System.Globalization;
using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;

namespace D3B.Bot.Harvesting;

internal sealed class HarvestMissionRunner
{
    private readonly SupervisedElementUseSession _useSession;
    private readonly GameTables _tables;
    private readonly GraphicSkills? _graphics;
    private readonly Func<int, string?>? _itemName;
    private int _consumed;

    public HarvestMissionRunner(
        SupervisedElementUseSession useSession,
        GameTables tables,
        GraphicSkills? graphics = null,
        Func<int, string?>? itemName = null)
    {
        ArgumentNullException.ThrowIfNull(useSession);
        ArgumentNullException.ThrowIfNull(tables);

        _useSession = useSession;
        _tables = tables;
        _graphics = graphics;
        _itemName = itemName;
    }

    public async Task<HarvestMissionResult> RunAsync(BotActor bot, HarvestMissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(request);

        if (Interlocked.CompareExchange(ref _consumed, 1, 0) is not 0)
        {
            var alreadyRunTrace = ImmutableArray.Create(Trace(
                HarvestMissionStage.CheckSession,
                HarvestMissionVerdict.Blocked,
                "ALREADY_RUN",
                ("destinationMapId", request.DestinationMapId)));

            return new HarvestMissionResult
            {
                Verdict = HarvestMissionVerdict.Blocked,
                Stage = HarvestMissionStage.CheckSession,
                ReasonCode = "ALREADY_RUN",
                StageTrace = alreadyRunTrace,
                DestinationMapId = request.DestinationMapId,
                ThresholdPercent = request.ThresholdPercent,
                StopReason = "ALREADY_RUN",
            };
        }

        var trace = new List<HarvestMissionTraceEntry>();
        var data = new HarvestMissionResult
        {
            DestinationMapId = request.DestinationMapId,
            ThresholdPercent = request.ThresholdPercent,
            StopReason = "UNKNOWN",
        };
        var stage = HarvestMissionStage.CheckSession;
        long? sessionEpoch = null;
        object? sessionIdentity = null;
        long preHarvestWeightRevision = 0;

        HarvestMissionResult Finish(HarvestMissionVerdict verdict, HarvestMissionStage resultStage, string reason)
        {
            return data with
            {
                Verdict = verdict,
                Stage = resultStage,
                ReasonCode = reason,
                StageTrace = trace.ToImmutableArray(),
                StopReason = reason,
            };
        }

        void Append(HarvestMissionStage traceStage, HarvestMissionVerdict verdict, string reason, params (string Key, object? Value)[] values)
        {
            trace.Add(Trace(traceStage, verdict, reason, values));
        }

        try
        {
            ValidateRequest(request);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            Append(stage, HarvestMissionVerdict.Blocked, "INVALID_REQUEST", ("parameter", exception.ParamName));

            return Finish(HarvestMissionVerdict.Blocked, stage, "INVALID_REQUEST");
        }

        using var timeout = new CancellationTokenSource(request.MaxDuration, bot.Time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var token = linked.Token;

        try
        {
            stage = HarvestMissionStage.CheckSession;
            var snapshot = SupervisedElementUse.Capture(bot);
            sessionEpoch = snapshot.SessionEpoch;
            sessionIdentity = bot.Session;
            data = data with
            {
                StartMapId = snapshot.State.MapId,
                SessionEpoch = snapshot.SessionEpoch,
                StateRevision = snapshot.Revision,
            };

            var sessionBlocker = SessionBlocker(snapshot);

            if (sessionBlocker is not null || sessionIdentity is null)
            {
                var reason = sessionBlocker ?? "SESSION_CLOSED";
                Append(stage, HarvestMissionVerdict.Blocked, reason,
                    ("connected", snapshot.Connected),
                    ("sessionAttached", snapshot.SessionAttached),
                    ("translationBlocked", snapshot.TranslationBlocked),
                    ("paused", snapshot.Paused),
                    ("acting", snapshot.Acting),
                    ("sessionEpoch", snapshot.SessionEpoch));

                return Finish(HarvestMissionVerdict.Blocked, stage, reason);
            }

            Append(stage, HarvestMissionVerdict.Pass, "READY",
                ("mapId", snapshot.State.MapId),
                ("cell", snapshot.State.Cell),
                ("sessionEpoch", snapshot.SessionEpoch),
                ("stateRevision", snapshot.Revision));

            stage = HarvestMissionStage.CheckPods;
            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out var captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason);

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            var pods = HarvestPods.From(snapshot.State, request.ThresholdPercent);
            data = data with
            {
                WeightBefore = pods.Weight,
                WeightRevisionBefore = pods.WeightRevision,
                StateRevision = snapshot.Revision,
            };

            if (pods.Blocker is { } podsBlocker)
            {
                Append(stage, HarvestMissionVerdict.Blocked, podsBlocker,
                    ("weight", pods.Weight),
                    ("maxWeight", pods.MaxWeight),
                    ("loadPercent", pods.LoadPercent),
                    ("weightRevision", pods.WeightRevision));

                return Finish(HarvestMissionVerdict.Blocked, stage, podsBlocker);
            }

            Append(stage, HarvestMissionVerdict.Pass, "PODS_READY",
                ("weight", pods.Weight),
                ("maxWeight", pods.MaxWeight),
                ("freePods", pods.FreePods),
                ("loadPercent", pods.LoadPercent),
                ("weightRevision", pods.WeightRevision));

            stage = HarvestMissionStage.CheckJob;
            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason);

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            var job = HarvestMissionPlan.JobFromSnapshot(snapshot, _tables, request.WantedItemId, _graphics, _itemName);
            data = data with { StateRevision = snapshot.Revision };

            if (job is null || job.Status is not "OK")
            {
                var reason = job?.Status ?? "JOB_UNKNOWN";
                Append(stage, HarvestMissionVerdict.Blocked, reason,
                    ("jobId", job?.JobId),
                    ("jobLevel", job?.JobLevel),
                    ("requiredLevel", job?.RequiredLevel),
                    ("skillId", job?.SkillId),
                    ("provenance", job?.Provenance));

                return Finish(HarvestMissionVerdict.Blocked, stage, reason);
            }

            Append(stage, HarvestMissionVerdict.Pass, "JOB_READY",
                ("jobId", job.JobId),
                ("jobLevel", job.JobLevel),
                ("requiredLevel", job.RequiredLevel),
                ("skillId", job.SkillId),
                ("provenance", job.Provenance));

            stage = HarvestMissionStage.PlanRoute;
            var plan = HarvestMissionPlan.Plan(bot, _tables, _graphics, _itemName, request);
            if (!SameSession(bot, sessionEpoch.Value, sessionIdentity))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, "SESSION_CHANGED");

                return Finish(HarvestMissionVerdict.Interrupted, stage, "SESSION_CHANGED");
            }

            data = data with
            {
                StartMapId = plan.StartMapId,
                StateRevision = plan.StateRevision,
                PlannedTransitionCount = plan.PlannedTransitionCount,
                PlannedTransitionTypes = plan.PlannedTransitionTypes,
            };

            if (plan.Verdict is not "PLAN_READY")
            {
                Append(stage, HarvestMissionVerdict.Blocked, plan.ReasonCode,
                    ("plannedTransitionCount", plan.PlannedTransitionCount),
                    ("plannedTransitionTypes", string.Join(',', plan.PlannedTransitionTypes)),
                    ("missing", string.Join(',', plan.Missing)));

                return Finish(HarvestMissionVerdict.Blocked, stage, plan.ReasonCode);
            }

            Append(stage, HarvestMissionVerdict.Pass, plan.Verdict,
                ("plannedTransitionCount", plan.PlannedTransitionCount),
                ("plannedTransitionTypes", string.Join(',', plan.PlannedTransitionTypes)),
                ("destinationMapId", request.DestinationMapId));

            stage = HarvestMissionStage.TravelToMap;
            if (plan.StartMapId == request.DestinationMapId)
            {
                Append(stage, HarvestMissionVerdict.Pass, "ALREADY_AT_DESTINATION",
                    ("mapId", plan.StartMapId),
                    ("transitionCount", 0));
            }
            else
            {
                var mark = bot.Journal.Written;
                TravelOutcome travelOutcome;

                try
                {
                    travelOutcome = await bot.TravelAsync(request.DestinationMapId, token).ConfigureAwait(false);
                }
                finally
                {
                    var entries = bot.Journal.After(ref mark).OfType<MapChangeEntry>().ToArray();
                    var transitions = entries.Select(entry => new HarvestMissionTransitionEvidence(
                        entry.From,
                        entry.To,
                        entry.Reached,
                        entry.Kind.ToString(),
                        entry.Outcome.ToString())).ToImmutableArray();
                    data = data with { TravelTransitions = transitions };
                }

                await bot.ProcessedAsync().ConfigureAwait(false);

                if (!SameSession(bot, sessionEpoch.Value, sessionIdentity))
                {
                    Append(stage, HarvestMissionVerdict.Interrupted, "SESSION_CHANGED",
                        ("transitionCount", data.TravelTransitions.Length));

                    return Finish(HarvestMissionVerdict.Interrupted, stage, "SESSION_CHANGED");
                }

                if (data.TravelTransitions.Length > request.MaxTransitions)
                {
                    Append(stage, HarvestMissionVerdict.Blocked, "TRANSITION_LIMIT",
                        ("transitionCount", data.TravelTransitions.Length),
                        ("maxTransitions", request.MaxTransitions));

                    return Finish(HarvestMissionVerdict.Blocked, stage, "TRANSITION_LIMIT");
                }

                if (travelOutcome is not TravelOutcome.Arrived)
                {
                    var reason = travelOutcome switch
                    {
                        TravelOutcome.Busy => "TRAVEL_BUSY",
                        TravelOutcome.NoPath => "TRAVEL_NO_PATH",
                        _ => "TRAVEL_FAILED",
                    };
                    Append(stage, HarvestMissionVerdict.Blocked, reason,
                        ("travelOutcome", travelOutcome),
                        ("transitionCount", data.TravelTransitions.Length),
                        ("transitions", TransitionSummary(data.TravelTransitions)));

                    return Finish(HarvestMissionVerdict.Blocked, stage, reason);
                }

                if (!JournalReachedDestination(data.StartMapId, data.TravelTransitions, request.DestinationMapId))
                {
                    Append(stage, HarvestMissionVerdict.Blocked, "TRAVEL_NOT_CONFIRMED",
                        ("transitionCount", data.TravelTransitions.Length),
                        ("transitions", TransitionSummary(data.TravelTransitions)));

                    return Finish(HarvestMissionVerdict.Blocked, stage, "TRAVEL_NOT_CONFIRMED");
                }

                if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
                {
                    Append(stage, HarvestMissionVerdict.Interrupted, captureReason,
                        ("transitionCount", data.TravelTransitions.Length));

                    return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
                }

                if (snapshot.State.MapId != request.DestinationMapId || !snapshot.State.IsPositionTrusted)
                {
                    Append(stage, HarvestMissionVerdict.Blocked, "TRAVEL_NOT_CONFIRMED",
                        ("mapId", snapshot.State.MapId),
                        ("positionTrusted", snapshot.State.IsPositionTrusted),
                        ("transitionCount", data.TravelTransitions.Length));

                    return Finish(HarvestMissionVerdict.Blocked, stage, "TRAVEL_NOT_CONFIRMED");
                }

                var arrivalReadiness = HarvestReadiness.CheckBeforeTravel(snapshot, _tables, request.WantedItemId, request.ThresholdPercent);
                var arrivalBlocker = SessionBlocker(snapshot)
                    ?? arrivalReadiness.Pods.Blocker
                    ?? (arrivalReadiness.Job is { Status: not "OK" } arrivalJob ? arrivalJob.Status : null);

                if (arrivalBlocker is not null)
                {
                    Append(stage, HarvestMissionVerdict.Blocked, arrivalBlocker,
                        ("mapId", snapshot.State.MapId),
                        ("pods", arrivalReadiness.Pods.Blocker),
                        ("job", arrivalReadiness.Job?.Status));

                    return Finish(HarvestMissionVerdict.Blocked, stage, arrivalBlocker);
                }

                Append(stage, HarvestMissionVerdict.Pass, "ARRIVED",
                    ("mapId", snapshot.State.MapId),
                    ("cell", snapshot.State.Cell),
                    ("transitionCount", data.TravelTransitions.Length),
                    ("transitions", TransitionSummary(data.TravelTransitions)));
            }

            stage = HarvestMissionStage.DiscoverTarget;
            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason);

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            if (snapshot.State.MapId != request.DestinationMapId || !snapshot.State.IsPositionTrusted)
            {
                Append(stage, HarvestMissionVerdict.Blocked, "DESTINATION_NOT_CONFIRMED",
                    ("mapId", snapshot.State.MapId),
                    ("positionTrusted", snapshot.State.IsPositionTrusted));

                return Finish(HarvestMissionVerdict.Blocked, stage, "DESTINATION_NOT_CONFIRMED");
            }

            var report = ElementCandidates.Build(snapshot, _tables, _graphics, _itemName);
            var selection = HarvestResourceTarget.Select(report, request.WantedItemId);
            var reachable = HarvestMissionPlan.ReachableTargets(snapshot, selection);

            if (reachable.Length is 0)
            {
                var reason = selection.Verdict switch
                {
                    HarvestResourceTargetVerdict.Ambiguous => "TARGET_AMBIGUOUS",
                    HarvestResourceTargetVerdict.Multiple => "NO_REACHABLE_TARGET",
                    _ => selection.EligibleTargets.IsEmpty ? "TARGET_NOT_FOUND" : "NO_REACHABLE_TARGET",
                };
                Append(stage, HarvestMissionVerdict.Blocked, reason,
                    ("candidateCount", report.Candidates.Length),
                    ("selection", selection.Verdict),
                    ("eligibleCount", selection.EligibleTargets.Length));

                return Finish(HarvestMissionVerdict.Blocked, stage, reason);
            }

            var target = reachable[0].Target;
            data = WithTarget(data, target) with { StateRevision = snapshot.Revision };
            Append(stage, HarvestMissionVerdict.Pass, "TARGET_SELECTED",
                ("candidateCount", report.Candidates.Length),
                ("selection", selection.Verdict),
                ("reachableTargetCount", reachable.Length),
                ("elementId", target.ElementId),
                ("cell", target.Cell),
                ("skillId", target.SkillId),
                ("skillInstanceId", target.SkillInstanceId));

            stage = HarvestMissionStage.PlanApproach;
            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason);

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            if (!TryCurrentTarget(snapshot, _tables, request.WantedItemId, target, out var currentTarget, out var targetReason))
            {
                Append(stage, HarvestMissionVerdict.Blocked, targetReason,
                    ("elementId", target.ElementId),
                    ("cell", target.Cell));

                return Finish(HarvestMissionVerdict.Blocked, stage, targetReason);
            }

            var approach = HarvestApproachPlanner.Plan(snapshot.State, snapshot.Map!, currentTarget.ElementId, currentTarget.Cell);
            data = WithTarget(data, currentTarget) with
            {
                ApproachPath = approach.Path,
                UseCell = approach.UseCell,
                StateRevision = snapshot.Revision,
            };

            if (approach.Status is not "CONFIRMED")
            {
                Append(stage, HarvestMissionVerdict.Blocked, approach.Reason,
                    ("elementId", currentTarget.ElementId),
                    ("elementCell", approach.ElementCell),
                    ("pathLength", approach.Path.Length));

                return Finish(HarvestMissionVerdict.Blocked, stage, approach.Reason);
            }

            Append(stage, HarvestMissionVerdict.Pass, "APPROACH_READY",
                ("elementId", currentTarget.ElementId),
                ("elementCell", approach.ElementCell),
                ("useCell", approach.UseCell),
                ("path", string.Join(',', approach.Path)));

            stage = HarvestMissionStage.WalkToUseCell;
            var walkOutcome = await bot.WalkAsync(approach.UseCell, token).ConfigureAwait(false);
            await bot.ProcessedAsync().ConfigureAwait(false);

            if (!SameSession(bot, sessionEpoch.Value, sessionIdentity))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, "SESSION_CHANGED",
                    ("walkOutcome", walkOutcome));

                return Finish(HarvestMissionVerdict.Interrupted, stage, "SESSION_CHANGED");
            }

            if (walkOutcome is not WalkOutcome.Arrived)
            {
                var reason = WalkReason(walkOutcome);
                Append(stage, HarvestMissionVerdict.Blocked, reason,
                    ("walkOutcome", walkOutcome),
                    ("useCell", approach.UseCell));

                return Finish(HarvestMissionVerdict.Blocked, stage, reason);
            }

            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason,
                    ("walkOutcome", walkOutcome));

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            if (snapshot.State.MapId != request.DestinationMapId
                || snapshot.State.Cell != approach.UseCell
                || !snapshot.State.IsPositionTrusted)
            {
                Append(stage, HarvestMissionVerdict.Blocked, "WALK_NOT_CONFIRMED",
                    ("mapId", snapshot.State.MapId),
                    ("cell", snapshot.State.Cell),
                    ("useCell", approach.UseCell),
                    ("positionTrusted", snapshot.State.IsPositionTrusted));

                return Finish(HarvestMissionVerdict.Blocked, stage, "WALK_NOT_CONFIRMED");
            }

            data = data with { StateRevision = snapshot.Revision };
            Append(stage, HarvestMissionVerdict.Pass, "ARRIVED_AT_USE_CELL",
                ("mapId", snapshot.State.MapId),
                ("cell", snapshot.State.Cell),
                ("positionTrusted", snapshot.State.IsPositionTrusted));

            stage = HarvestMissionStage.RevalidateAll;
            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason);

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            var readiness = HarvestReadiness.CheckAfterArrival(snapshot, _tables, request.WantedItemId, request.ThresholdPercent);
            var revalidationBlocker = SessionBlocker(snapshot);

            if (revalidationBlocker is not null)
            {
                Append(stage, HarvestMissionVerdict.Blocked, revalidationBlocker,
                    ("sessionEpoch", snapshot.SessionEpoch),
                    ("stateRevision", snapshot.Revision));

                return Finish(HarvestMissionVerdict.Blocked, stage, revalidationBlocker);
            }

            if (readiness.Pods.Blocker is { } postArrivalPodsBlocker)
            {
                Append(stage, HarvestMissionVerdict.Blocked, postArrivalPodsBlocker,
                    ("weight", readiness.Pods.Weight),
                    ("loadPercent", readiness.Pods.LoadPercent));

                return Finish(HarvestMissionVerdict.Blocked, stage, postArrivalPodsBlocker);
            }

            if (snapshot.State.MapId != request.DestinationMapId
                || snapshot.State.Cell != approach.UseCell
                || !snapshot.State.IsPositionTrusted)
            {
                Append(stage, HarvestMissionVerdict.Blocked, "POSITION_CHANGED",
                    ("mapId", snapshot.State.MapId),
                    ("cell", snapshot.State.Cell),
                    ("useCell", approach.UseCell),
                    ("positionTrusted", snapshot.State.IsPositionTrusted));

                return Finish(HarvestMissionVerdict.Blocked, stage, "POSITION_CHANGED");
            }

            if (!TryCurrentTarget(snapshot, _tables, request.WantedItemId, target, out currentTarget, out targetReason))
            {
                Append(stage, HarvestMissionVerdict.Blocked, targetReason,
                    ("elementId", target.ElementId),
                    ("cell", target.Cell));

                return Finish(HarvestMissionVerdict.Blocked, stage, targetReason);
            }

            var currentJob = HarvestJob.Check(snapshot.State, _tables, currentTarget.SkillId, true, "server+catalog");

            if (currentJob is null || currentJob.Status is not "OK")
            {
                var reason = currentJob?.Status ?? "JOB_UNKNOWN";
                Append(stage, HarvestMissionVerdict.Blocked, reason,
                    ("jobId", currentJob?.JobId),
                    ("jobLevel", currentJob?.JobLevel),
                    ("requiredLevel", currentJob?.RequiredLevel),
                    ("skillId", currentJob?.SkillId));

                return Finish(HarvestMissionVerdict.Blocked, stage, reason);
            }

            var elementUseTarget = new ElementUseTarget(request.DestinationMapId, approach.UseCell, currentTarget.ElementId, currentTarget.SkillId);
            var preparation = SupervisedElementUse.Prepare(snapshot, _tables, elementUseTarget);

            if (preparation.State is not ElementUsePreparationState.Ready)
            {
                Append(stage, HarvestMissionVerdict.Blocked, preparation.Reason,
                    ("preparationState", preparation.State),
                    ("stateRevision", preparation.StateRevision),
                    ("sessionEpoch", preparation.SessionEpoch));

                return Finish(HarvestMissionVerdict.Blocked, stage, preparation.Reason);
            }

            preHarvestWeightRevision = readiness.Pods.WeightRevision;
            data = WithTarget(data, currentTarget) with
            {
                WeightBefore = readiness.Pods.Weight,
                WeightRevisionBefore = readiness.Pods.WeightRevision,
                StateRevision = snapshot.Revision,
            };
            Append(stage, HarvestMissionVerdict.Pass, "REVALIDATION_READY",
                ("mapId", snapshot.State.MapId),
                ("cell", snapshot.State.Cell),
                ("elementId", currentTarget.ElementId),
                ("elementCell", currentTarget.Cell),
                ("skillId", currentTarget.SkillId),
                ("skillInstanceId", currentTarget.SkillInstanceId),
                ("usesFromCurrentCell", true),
                ("preparation", preparation.Reason),
                ("readiness", readiness.Verdict));

            stage = HarvestMissionStage.UseElement;
            var useResult = await _useSession.UseAsync(elementUseTarget, token).ConfigureAwait(false);
            await bot.ProcessedAsync().ConfigureAwait(false);
            var evidence = useResult.Evidence;
            preHarvestWeightRevision = evidence.WeightRevisionBefore ?? preHarvestWeightRevision;
            data = data with
            {
                UseRequestSent = evidence.RequestSent,
                UseStartedObserved = evidence.InteractiveUsedObserved,
                UseEndedObserved = evidence.InteractiveUseEndedObserved,
                WeightRevisionBefore = preHarvestWeightRevision,
                InventoryBefore = evidence.InventoryBefore,
                InventoryAfter = evidence.InventoryAfter,
                InventoryGain = evidence.InventoryBefore is { } before && evidence.InventoryAfter is { } after ? after - before : null,
                StateRevision = Math.Max(data.StateRevision, evidence.StateRevision),
            };

            if (!SameSession(bot, sessionEpoch.Value, sessionIdentity))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, "SESSION_CHANGED",
                    ("useOutcome", useResult.Outcome),
                    ("requestSent", evidence.RequestSent));

                return Finish(HarvestMissionVerdict.Interrupted, stage, "SESSION_CHANGED");
            }

            switch (useResult.Outcome)
            {
                case SupervisedElementUseOutcome.PassHarvest:
                    Append(stage, HarvestMissionVerdict.Pass, "USE_PASSED",
                        ("requestSent", evidence.RequestSent),
                        ("started", evidence.InteractiveUsedObserved),
                        ("ended", evidence.InteractiveUseEndedObserved),
                        ("inventoryBefore", evidence.InventoryBefore),
                        ("inventoryAfter", evidence.InventoryAfter));
                    break;
                case SupervisedElementUseOutcome.Refused:
                    Append(stage, HarvestMissionVerdict.Refused, useResult.Reason,
                        ("requestSent", evidence.RequestSent),
                        ("skillInstanceId", evidence.SkillInstanceId));

                    return Finish(HarvestMissionVerdict.Refused, stage, useResult.Reason);
                case SupervisedElementUseOutcome.Unconfirmed:
                    Append(stage, HarvestMissionVerdict.Blocked, "UNCONFIRMED",
                        ("useReason", useResult.Reason),
                        ("requestSent", evidence.RequestSent),
                        ("requestMayHaveBeenSent", evidence.RequestMayHaveBeenSent));

                    return Finish(HarvestMissionVerdict.Blocked, stage, "UNCONFIRMED");
                case SupervisedElementUseOutcome.Interrupted:
                    Append(stage, HarvestMissionVerdict.Interrupted, useResult.Reason,
                        ("requestSent", evidence.RequestSent),
                        ("requestMayHaveBeenSent", evidence.RequestMayHaveBeenSent));

                    return Finish(HarvestMissionVerdict.Interrupted, stage, useResult.Reason);
                default:
                    Append(stage, HarvestMissionVerdict.Blocked, useResult.Reason,
                        ("requestSent", evidence.RequestSent));

                    return Finish(HarvestMissionVerdict.Blocked, stage, useResult.Reason);
            }

            stage = HarvestMissionStage.VerifyHarvest;
            var verificationReason = VerifyEvidence(evidence, currentTarget, request, approach.UseCell, sessionEpoch.Value, bot, sessionIdentity);

            if (verificationReason is not null)
            {
                Append(stage, HarvestMissionVerdict.Blocked, verificationReason,
                    ("requestSent", evidence.RequestSent),
                    ("started", evidence.InteractiveUsedObserved),
                    ("ended", evidence.InteractiveUseEndedObserved),
                    ("expectedItemId", evidence.ExpectedItemId),
                    ("inventoryBefore", evidence.InventoryBefore),
                    ("inventoryAfter", evidence.InventoryAfter),
                    ("startMapId", evidence.StartMapId),
                    ("startCell", evidence.StartCell),
                    ("endMapId", evidence.EndMapId),
                    ("endCell", evidence.EndCell));

                return Finish(HarvestMissionVerdict.Blocked, stage, verificationReason);
            }

            Append(stage, HarvestMissionVerdict.Pass, "HARVEST_VERIFIED",
                ("requestSent", evidence.RequestSent),
                ("started", evidence.InteractiveUsedObserved),
                ("ended", evidence.InteractiveUseEndedObserved),
                ("itemId", evidence.ExpectedItemId),
                ("inventoryGain", data.InventoryGain),
                ("startMapId", evidence.StartMapId),
                ("startCell", evidence.StartCell),
                ("endMapId", evidence.EndMapId),
                ("endCell", evidence.EndCell));

            stage = HarvestMissionStage.CheckPodsAfter;
            if (!TryCapture(bot, sessionEpoch.Value, sessionIdentity, out snapshot, out captureReason))
            {
                Append(stage, HarvestMissionVerdict.Interrupted, captureReason);

                return Finish(HarvestMissionVerdict.Interrupted, stage, captureReason);
            }

            var podsAfter = HarvestPods.From(snapshot.State, request.ThresholdPercent);
            data = data with
            {
                WeightAfter = podsAfter.Weight,
                WeightRevisionAfter = podsAfter.WeightRevision,
                StateRevision = snapshot.Revision,
            };

            if (evidence.WeightRevisionBefore is null || !podsAfter.Known || podsAfter.WeightRevision <= preHarvestWeightRevision)
            {
                Append(stage, HarvestMissionVerdict.Blocked, "WEIGHT_NOT_REFRESHED",
                    ("weightBefore", data.WeightBefore),
                    ("weightAfter", podsAfter.Weight),
                    ("weightRevisionBefore", preHarvestWeightRevision),
                    ("weightRevisionAfter", podsAfter.WeightRevision),
                    ("weightProvenance", podsAfter.Provenance));

                return Finish(HarvestMissionVerdict.Blocked, stage, "WEIGHT_NOT_REFRESHED");
            }

            if (podsAfter.Blocker is { } afterBlocker && afterBlocker is not "PODS_THRESHOLD_REACHED")
            {
                Append(stage, HarvestMissionVerdict.Blocked, afterBlocker,
                    ("weight", podsAfter.Weight),
                    ("maxWeight", podsAfter.MaxWeight),
                    ("loadPercent", podsAfter.LoadPercent));

                return Finish(HarvestMissionVerdict.Blocked, stage, afterBlocker);
            }

            var stopReason = podsAfter.LoadPercent >= request.ThresholdPercent
                ? "PODS_THRESHOLD_REACHED"
                : "COMPLETE";
            Append(stage, HarvestMissionVerdict.Pass, stopReason,
                ("weight", podsAfter.Weight),
                ("maxWeight", podsAfter.MaxWeight),
                ("loadPercent", podsAfter.LoadPercent),
                ("weightRevisionBefore", preHarvestWeightRevision),
                ("weightRevisionAfter", podsAfter.WeightRevision));

            stage = HarvestMissionStage.Stop;
            Append(stage, HarvestMissionVerdict.Pass, stopReason,
                ("destinationMapId", request.DestinationMapId),
                ("targetElementId", currentTarget.ElementId),
                ("useCell", approach.UseCell));

            return Finish(HarvestMissionVerdict.Pass, stage, stopReason);
        }
        catch (BotInterruptedException)
        {
            var reason = SameSession(bot, sessionEpoch, sessionIdentity)
                ? "BOT_INTERRUPTED"
                : "SESSION_CHANGED";
            AppendIfCurrentStageMissing(trace, stage, HarvestMissionVerdict.Interrupted, reason);

            return Finish(HarvestMissionVerdict.Interrupted, stage, reason);
        }
        catch (OperationCanceledException)
        {
            var reason = !SameSession(bot, sessionEpoch, sessionIdentity)
                ? "SESSION_CHANGED"
                : timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                    ? "TIMEOUT"
                    : "CANCELLED";
            AppendIfCurrentStageMissing(trace, stage, HarvestMissionVerdict.Interrupted, reason);

            return Finish(HarvestMissionVerdict.Interrupted, stage, reason);
        }
        catch (Exception exception)
        {
            AppendIfCurrentStageMissing(trace, stage, HarvestMissionVerdict.Failed, "UNEXPECTED_ERROR", ("exceptionType", exception.GetType().Name));

            return Finish(HarvestMissionVerdict.Failed, stage, "UNEXPECTED_ERROR");
        }
    }

    internal static void ValidateRequest(HarvestMissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DestinationMapId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.WantedItemId);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.ThresholdPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.ThresholdPercent, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxTransitions);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.MaxDuration, TimeSpan.Zero);
    }

    private static HarvestMissionTraceEntry Trace(
        HarvestMissionStage stage,
        HarvestMissionVerdict verdict,
        string reason,
        params (string Key, object? Value)[] values)
    {
        var details = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);

        foreach (var pair in values)
        {
            details[pair.Key] = Format(pair.Value);
        }

        return new HarvestMissionTraceEntry(stage, verdict, reason, details.ToImmutable());
    }

    private static string? Format(object? value)
    {
        return value switch
        {
            null => null,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static string? SessionBlocker(ElementUseSnapshot snapshot)
    {
        if (!snapshot.Connected || !snapshot.SessionAttached)
        {
            return "SESSION_CLOSED";
        }

        if (snapshot.TranslationBlocked)
        {
            return "TRANSLATION_LATCH_CLOSED";
        }

        if (snapshot.Paused)
        {
            return "BOT_PAUSED";
        }

        if (snapshot.Acting)
        {
            return "BOT_ACTING";
        }

        return null;
    }

    private static bool TryCapture(
        BotActor bot,
        long expectedEpoch,
        object? expectedSession,
        out ElementUseSnapshot snapshot,
        out string reason)
    {
        snapshot = SupervisedElementUse.Capture(bot);

        if (snapshot.SessionEpoch != expectedEpoch || bot.View.SessionEpoch != expectedEpoch || !ReferenceEquals(bot.Session, expectedSession))
        {
            reason = "SESSION_CHANGED";

            return false;
        }

        reason = "READY";

        return true;
    }

    private static bool SameSession(BotActor bot, long? expectedEpoch, object? expectedSession)
    {
        if (expectedEpoch is not { } epoch)
        {
            return true;
        }

        return bot.View.SessionEpoch == epoch && ReferenceEquals(bot.Session, expectedSession);
    }

    private static bool JournalReachedDestination(
        long? startMapId,
        ImmutableArray<HarvestMissionTransitionEvidence> transitions,
        long destinationMapId)
    {
        if (transitions.IsDefaultOrEmpty || startMapId is not { } currentMap)
        {
            return false;
        }

        foreach (var transition in transitions)
        {
            if (transition.Outcome is not "Changed" || transition.ReachedMapId is not { } reached || transition.FromMapId != currentMap)
            {
                return false;
            }

            currentMap = reached;
        }

        return currentMap == destinationMapId;
    }

    private static string TransitionSummary(ImmutableArray<HarvestMissionTransitionEvidence> transitions)
    {
        return string.Join(',', transitions.Select(transition => $"{transition.Kind}:{transition.Outcome}:{transition.FromMapId}->{transition.ReachedMapId}"));
    }

    private static string WalkReason(WalkOutcome outcome)
    {
        return outcome switch
        {
            WalkOutcome.Busy => "WALK_BUSY",
            WalkOutcome.NoPath => "WALK_NO_PATH",
            WalkOutcome.Refused => "WALK_REFUSED",
            WalkOutcome.NoAnswer => "WALK_NO_ANSWER",
            WalkOutcome.NotConfirmed => "WALK_NOT_CONFIRMED",
            _ => "WALK_FAILED",
        };
    }

    private static bool TryCurrentTarget(
        ElementUseSnapshot snapshot,
        GameTables tables,
        int wantedItemId,
        HarvestResourceTarget expected,
        out HarvestResourceTarget current,
        out string reason)
    {
        current = expected;
        reason = "READY";

        if (snapshot.Map is null || snapshot.State.MapId != expected.MapId)
        {
            reason = "MAP_CHANGED";

            return false;
        }

        var report = ElementCandidates.Build(snapshot, tables, graphics: null, itemName: null);
        var selection = HarvestResourceTarget.Select(report, wantedItemId);
        var candidate = selection.EligibleTargets.FirstOrDefault(item => item.ElementId == expected.ElementId);

        if (candidate is null)
        {
            reason = selection.Verdict is HarvestResourceTargetVerdict.Ambiguous ? "TARGET_AMBIGUOUS" : "TARGET_NOT_SERVER_KNOWN";

            return false;
        }

        if (candidate.Identity is not ElementCandidateIdentity.Verified || candidate.ExpectedItemId != wantedItemId)
        {
            reason = "TARGET_IDENTITY_CHANGED";

            return false;
        }

        if (candidate.Cell != expected.Cell)
        {
            reason = "ELEMENT_CELL_CHANGED";

            return false;
        }

        if (candidate.SkillId != expected.SkillId)
        {
            reason = "SKILL_CHANGED";

            return false;
        }

        if (candidate.SkillInstanceId != expected.SkillInstanceId)
        {
            reason = "SKILL_INSTANCE_CHANGED";

            return false;
        }

        current = candidate;

        return true;
    }

    private static string? VerifyEvidence(
        SupervisedElementUseEvidence evidence,
        HarvestResourceTarget target,
        HarvestMissionRequest request,
        int useCell,
        long expectedSessionEpoch,
        BotActor bot,
        object? expectedSession)
    {
        if (!evidence.RequestSent)
        {
            return "REQUEST_NOT_SENT";
        }

        if (!evidence.InteractiveUsedObserved)
        {
            return "USE_START_NOT_OBSERVED";
        }

        if (!evidence.InteractiveUseEndedObserved)
        {
            return "USE_END_NOT_OBSERVED";
        }

        if (evidence.ExpectedItemId != request.WantedItemId)
        {
            return "EXPECTED_ITEM_MISMATCH";
        }

        if (evidence.InventoryBefore is not { } before || evidence.InventoryAfter is not { } after)
        {
            return "INVENTORY_EVIDENCE_MISSING";
        }

        if (after <= before)
        {
            return "ITEM_NOT_GAINED";
        }

        if (evidence.Target.MapId != request.DestinationMapId
            || evidence.Target.Cell != useCell
            || evidence.Target.ElementId != target.ElementId
            || evidence.Target.SkillId != target.SkillId)
        {
            return "USE_TARGET_MISMATCH";
        }

        if (evidence.StartMapId != request.DestinationMapId || evidence.StartCell != useCell)
        {
            return "USE_START_POSITION_MISMATCH";
        }

        if (evidence.EndMapId != request.DestinationMapId || evidence.EndCell != useCell)
        {
            return "USE_END_POSITION_MISMATCH";
        }

        if (!evidence.EndPositionTrusted)
        {
            return "POSITION_NOT_TRUSTED";
        }

        if (evidence.SessionEpoch != expectedSessionEpoch || !SameSession(bot, expectedSessionEpoch, expectedSession))
        {
            return "SESSION_CHANGED";
        }

        return null;
    }

    private static HarvestMissionResult WithTarget(HarvestMissionResult result, HarvestResourceTarget target)
    {
        return result with
        {
            TargetIdentity = target.Identity.ToString(),
            TargetCell = target.Cell,
            TargetElementId = target.ElementId,
            TargetInteractiveId = target.InteractiveId,
            TargetGfxId = target.GfxId,
            TargetSkillId = target.SkillId,
            TargetSkillInstanceId = target.SkillInstanceId,
            ExpectedItemId = target.ExpectedItemId,
        };
    }

    private static void AppendIfCurrentStageMissing(
        List<HarvestMissionTraceEntry> trace,
        HarvestMissionStage stage,
        HarvestMissionVerdict verdict,
        string reason,
        params (string Key, object? Value)[] values)
    {
        if (trace.Count is 0 || trace[^1].Stage != stage)
        {
            trace.Add(Trace(stage, verdict, reason, values));
        }
    }
}
