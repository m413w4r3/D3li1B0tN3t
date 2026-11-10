using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Gui.Safety;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Interactives;

/// <summary>Prepares and executes one fail-closed, supervised use of a map element.</summary>
internal static class SupervisedElementUse
{
    // Bounded settlement for a correction or removal that follows a gain; this is not a second harvest window.
    private static readonly TimeSpan _inventorySettle = TimeSpan.FromMilliseconds(100);

    /// <summary>Passively checks one target against a single bot snapshot and its current session state.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="tables">The installed build's game tables.</param>
    /// <param name="target">The exact map, cell, element and skill requested.</param>
    /// <param name="attemptConsumed">Whether this host session's one attempt was already spent.</param>
    /// <returns>The preparation and the first reason it is blocked or unavailable.</returns>
    public static ElementUsePreparation Prepare(BotActor bot, GameTables tables, ElementUseTarget target, bool attemptConsumed = false)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(tables);

        return Prepare(Capture(bot, failClosedTranslation: false), tables, target, attemptConsumed);
    }

    /// <summary>Captures the actor's published state and the current map for passive element diagnostics.</summary>
    internal static ElementUseSnapshot Capture(BotActor bot, bool failClosedTranslation = true)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var translationBlockedBeforeView = failClosedTranslation && bot.IsMovementTranslationBlocked;
        var view = bot.View;
        var translationBlockedAfterView = bot.IsMovementTranslationBlocked;
        var translationBlocked = failClosedTranslation
            ? translationBlockedBeforeView || view.IsTranslationBlocked || translationBlockedAfterView
            : translationBlockedAfterView;
        var state = view.State;
        var session = bot.Session;
        MapData? map = null;

        if (state.MapId is { } mapId && bot.Maps.TryGet(mapId, out var found))
            map = found;

        return new ElementUseSnapshot(
            state,
            map,
            view.Revision,
            view.SessionEpoch,
            view.IsConnected,
            session is not null,
            translationBlocked,
            view.IsPaused,
            bot.IsActing,
            session?.InputMode ?? GameInputMode.ProtocolInjection);
    }

    /// <summary>Prepares one exact target against a previously captured actor snapshot.</summary>
    internal static ElementUsePreparation Prepare(
        ElementUseSnapshot snapshot,
        GameTables tables,
        ElementUseTarget target,
        bool attemptConsumed = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(tables);

        var state = snapshot.State;
        var preparation = new ElementUsePreparation
        {
            State = ElementUsePreparationState.Ready,
            Reason = "READY",
            Target = target,
            AttemptConsumed = attemptConsumed,
            SessionEpoch = snapshot.SessionEpoch,
            StateRevision = snapshot.Revision,
            Connected = snapshot.Connected,
            SessionAttached = snapshot.SessionAttached,
            TranslationBlocked = snapshot.TranslationBlocked,
            Paused = snapshot.Paused,
            Acting = snapshot.Acting,
            InputMode = snapshot.InputMode,
            MapId = state.MapId,
            Cell = state.Cell,
            Provenance = state.Provenance,
            PositionTrusted = state.IsPositionTrusted,
        };

        if (SessionBlocker(snapshot, attemptConsumed) is { } blocker)
            return Decided(preparation, ElementUsePreparationState.Blocked, blocker);

        if (target.MapId <= 0 || target.Cell is < 0 or >= MapData.CellCount || target.ElementId < 0 || target.SkillId <= 0)
            return Decided(preparation, ElementUsePreparationState.Blocked, "INVALID_TARGET");

        if (state.MapId != target.MapId)
            return Decided(preparation, ElementUsePreparationState.Blocked, "MAP_CHANGED");

        if (state.Cell != target.Cell)
            return Decided(preparation, ElementUsePreparationState.Blocked, "CELL_CHANGED");

        var resolution = ResolveElement(snapshot, tables, target);
        preparation = WithResolution(preparation, resolution);

        if (resolution.FailureReason is { } reason)
            return Decided(preparation, resolution.FailureState, reason);

        return preparation;
    }

    /// <summary>Returns the first session or movement blocker for a passive snapshot.</summary>
    internal static string? SessionBlocker(ElementUseSnapshot snapshot, bool attemptConsumed)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var state = snapshot.State;

        if (!snapshot.Connected)
            return "DISCONNECTED";

        if (!snapshot.SessionAttached)
            return "NO_SESSION";

        if (snapshot.TranslationBlocked)
            return "TRANSLATION_LATCH_CLOSED";

        if (snapshot.Paused)
            return "BOT_PAUSED";

        if (snapshot.Acting)
            return "BOT_ACTING";

        if (attemptConsumed)
            return "ATTEMPT_ALREADY_CONSUMED";

        if (snapshot.InputMode is not GameInputMode.ProtocolInjection)
            return "INPUT_MODE_NOT_PROTOCOL_INJECTION";

        if (state.IsInHavenBag)
            return "HAVEN_BAG";

        var guard = MovementGuard.Refuse(state, snapshot.Connected);

        if (guard is not MovementRefusal.None)
            return MovementReadiness.CodeOf(guard);

        return null;
    }

    /// <summary>Runs the one request attempt described by a ready preparation.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="tables">The installed build's game tables.</param>
    /// <param name="preparation">The preparation to recheck and execute.</param>
    /// <param name="cancellationToken">Stops the attempt.</param>
    /// <returns>The verdict and evidence for the attempt.</returns>
    public static async Task<SupervisedElementUseResult> ExecuteAsync(
        BotActor bot,
        GameTables tables,
        ElementUsePreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(preparation);

        if (!preparation.CanAttempt)
            return Blocked(preparation, bot.State);

        // The session's compare-and-swap remains the atomic one-shot boundary.
        if (preparation.AttemptConsumed)
            return Blocked(preparation with { State = ElementUsePreparationState.Blocked, Reason = "ATTEMPT_ALREADY_CONSUMED" }, bot.State);

        if (bot.View.SessionEpoch != preparation.SessionEpoch)
            return Blocked(preparation with { State = ElementUsePreparationState.Blocked, Reason = "SESSION_CHANGED" }, bot.State);

        if (cancellationToken.IsCancellationRequested)
            return Result(SupervisedElementUseOutcome.Interrupted, "CANCELLED", preparation, new AttemptState(), bot.State);

        var attempt = new AttemptState();

        try
        {
            var verdict = await bot.RunAsync(async (context, token) =>
            {
                var initialReason = Recheck(context, bot, tables, preparation);

                if (initialReason is not null)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Blocked, initialReason);

                var state = context.State;
                var elementId = preparation.Target.ElementId;
                var skillId = preparation.SkillId!.Value;
                var skillInstanceId = preparation.SkillInstanceId!.Value;
                var expectedItemId = preparation.ExpectedItemId!.Value;
                var lastRecheckReason = "SEND_BLOCKED";
                var startObserved = false;
                // Events observed while the frontier is closed (the observation barrier or write) are ignored.
                var answer = context.Expect((side, message, _) =>
                {
                    if (side is not RelayDirection.FromServer)
                        return false;

                    if (!attempt.SendCompleted)
                        return false;

                    if (message is InteractiveUsedEvent used
                        && used.ActorId == state.Key.CharacterId
                        && used.ElementId == elementId
                        && used.SkillId == skillId)
                    {
                        startObserved = true;
                        return true;
                    }

                    // Exact skill-instance correlation keeps unrelated refusals from consuming the attempt.
                    return message is InteractiveUseErrorEvent error
                        && error.ElementId == elementId
                        && error.SkillInstanceId == skillInstanceId;
                });
                // The end still requires this attempt's start. Without a protocol attempt id, stale post-write matches
                // remain possible, so the quiet-session/UNCONFIRMED rule stays in force.
                var end = context.Expect((side, message, _) => side is RelayDirection.FromServer
                    && attempt.SendCompleted
                    && startObserved
                    && message is InteractiveUseEndedEvent ended
                    && ended.ElementId == elementId
                    && ended.SkillId == skillId);
                var request = ElementUse.Request(elementId, new ElementSkill(skillId, skillInstanceId));

                bool CanSend()
                {
                    var reason = Recheck(context, bot, tables, preparation);

                    if (reason is not null)
                    {
                        lastRecheckReason = reason;
                        return false;
                    }

                    attempt.InventoryBefore = Carried(context.State, expectedItemId);
                    attempt.WeightRevisionBefore = context.State.WeightRevision;
                    return true;
                }

                bool sent;

                try
                {
                    sent = await context.SendGuardedAsync(
                        request,
                        CanSend,
                        token,
                        () =>
                        {
                            // The acceptance frontier opens after the guarded write, not in this pre-write callback.
                            attempt.RequestMayHaveBeenSent = true;
                        }).ConfigureAwait(false);
                }
                catch
                {
                    answer.Cancel();
                    end.Cancel();
                    throw;
                }

                if (!sent)
                {
                    answer.Cancel();
                    end.Cancel();
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Blocked, lastRecheckReason);
                }

                attempt.RequestSent = true;
                // Events queued during the observation barrier or the write stay before this inbox barrier and cannot satisfy
                // the attempt. A genuine server response cannot precede the write; events after this point are post-write.
                await context.WaitUntilAsync(
                    _ =>
                    {
                        attempt.SendCompleted = true;
                        return true;
                    },
                    Timeout.InfiniteTimeSpan,
                    token).ConfigureAwait(false);
                attempt.WeightRevisionBefore = context.State.WeightRevision;
                BotLog.ElementUseFrontierOpen(context.Log, state.Key.CharacterId, elementId);

                var answered = await answer.WaitAsync(context.Delays.Answer, token).ConfigureAwait(false);

                if (answered is null)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "NO_ANSWER");

                if (answered is InteractiveUseErrorEvent error)
                {
                    attempt.ErrorSkillInstanceId = error.SkillInstanceId;
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Refused, "SERVER_REFUSED");
                }

                var started = (InteractiveUsedEvent)answered;
                attempt.InteractiveUsedObserved = true;

                if (started.Duration <= 0)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "USE_HAS_NO_DURATION");

                var ended = await end.WaitAsync(
                    TimeSpan.FromMilliseconds(100d * started.Duration) + context.Delays.UseEnd,
                    token).ConfigureAwait(false);

                if (ended is null)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "NO_END");

                attempt.InteractiveUseEndedObserved = true;
                var inventoryBefore = attempt.InventoryBefore!.Value;
                var itemGained = await context.WaitUntilAsync(
                    current => Carried(current, expectedItemId) > inventoryBefore,
                    TimeSpan.FromSeconds(1),
                    token).ConfigureAwait(false);
                attempt.InventoryAfter = Carried(context.State, expectedItemId);

                if (!itemGained)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "ITEM_NOT_GAINED");

                _ = await context.WaitUntilAsync(_ => false, _inventorySettle, token).ConfigureAwait(false);
                attempt.InventoryAfter = Carried(context.State, expectedItemId);

                if (attempt.InventoryBefore is not { } inventoryBeforeAtVerdict
                    || attempt.InventoryAfter is not { } inventoryAfterAtVerdict
                    || inventoryAfterAtVerdict <= inventoryBeforeAtVerdict)
                {
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "ITEM_GAIN_NOT_PRESENT_AT_VERDICT");
                }

                var finalState = context.State;

                if (finalState.MapId != preparation.MapId || finalState.Cell != preparation.Cell)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "POSITION_CHANGED_AFTER_SEND");

                if (!finalState.IsPositionTrusted)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "POSITION_NOT_TRUSTED_AFTER_SEND");

                if (bot.View.SessionEpoch != preparation.SessionEpoch)
                    return new BehaviorVerdict(SupervisedElementUseOutcome.Unconfirmed, "SESSION_CHANGED_AFTER_SEND");

                return new BehaviorVerdict(SupervisedElementUseOutcome.PassHarvest, "PASS");
            }, cancellationToken).ConfigureAwait(false);

            return Result(verdict.Outcome, verdict.Reason, preparation, attempt, bot.State, verdict.Detail);
        }
        catch (BotInterruptedException exception)
        {
            return Result(SupervisedElementUseOutcome.Interrupted, Interrupted(exception.Reason), preparation, attempt, bot.State);
        }
        catch (OperationCanceledException)
        {
            return Result(SupervisedElementUseOutcome.Interrupted, "CANCELLED", preparation, attempt, bot.State);
        }
        catch (ObjectDisposedException)
        {
            return Result(SupervisedElementUseOutcome.Interrupted, "INTERRUPTED_BOT_STOPPED", preparation, attempt, bot.State);
        }
        catch (RelayInjectionException)
        {
            attempt.RequestMayHaveBeenSent = false;
            var reason = bot.IsMovementTranslationBlocked ? "TRANSLATION_LATCH_CLOSED" : "SEND_BLOCKED";

            return Result(SupervisedElementUseOutcome.Blocked, reason, preparation, attempt, bot.State);
        }
        catch (InvalidOperationException)
        {
            // Admission and behavior start refuse before the relay write.
            attempt.RequestMayHaveBeenSent = false;
            return Result(SupervisedElementUseOutcome.Blocked, "BEHAVIOR_REFUSED", preparation, attempt, bot.State);
        }
        catch (Exception exception)
        {
            var outcome = attempt.RequestMayHaveBeenSent
                ? SupervisedElementUseOutcome.Unconfirmed
                : SupervisedElementUseOutcome.Blocked;
            var reason = attempt.RequestMayHaveBeenSent ? "SEND_UNCERTAIN" : "SEND_BLOCKED";

            return Result(outcome, reason, preparation, attempt, bot.State, exception.GetType().Name);
        }
    }

    /// <summary>Creates an evidence-light blocked preparation when the session has no single connected bot.</summary>
    /// <param name="target">The exact requested target.</param>
    /// <param name="reason">The blocker code.</param>
    /// <returns>A blocked preparation retaining the requested target.</returns>
    internal static ElementUsePreparation BlockedPreparation(ElementUseTarget target, string reason)
    {
        return new ElementUsePreparation
        {
            State = ElementUsePreparationState.Blocked,
            Reason = reason,
            Target = target,
            Provenance = CellProvenance.Unknown,
        };
    }

    /// <summary>Builds a blocked result from a preparation without claiming that an attempt began.</summary>
    /// <param name="preparation">The preparation that was blocked.</param>
    /// <param name="endState">The current end state, when a bot was available.</param>
    /// <returns>A blocked result with no request or observation flags set.</returns>
    internal static SupervisedElementUseResult Blocked(ElementUsePreparation preparation, CharacterState? endState = null)
    {
        return Result(SupervisedElementUseOutcome.Blocked, preparation.Reason, preparation, new AttemptState(), endState);
    }

    private static string? Recheck(BotContext context, BotActor bot, GameTables tables, ElementUsePreparation preparation)
    {
        if (bot.View.SessionEpoch != preparation.SessionEpoch)
            return "SESSION_CHANGED";

        if (!context.IsConnected)
            return "DISCONNECTED";

        var session = bot.Session;

        if (session is null)
            return "NO_SESSION";

        if (session.InputMode is not GameInputMode.ProtocolInjection)
            return "INPUT_MODE_NOT_PROTOCOL_INJECTION";

        if (bot.IsMovementTranslationBlocked)
            return "TRANSLATION_LATCH_CLOSED";

        if (bot.IsPaused)
            return "BOT_PAUSED";

        var state = context.State;
        var guard = MovementGuard.Refuse(state, context.IsConnected);

        if (guard is not MovementRefusal.None)
            return MovementReadiness.CodeOf(guard);

        if (state.MapId != preparation.MapId)
            return "MAP_CHANGED";

        if (state.Cell != preparation.Cell)
            return "CELL_CHANGED";

        MapData? map = context.Maps.TryGet(preparation.Target.MapId, out var found) ? found : null;
        var snapshot = new ElementUseSnapshot(
            state,
            map,
            0,
            0,
            context.IsConnected,
            true,
            false,
            false,
            false,
            GameInputMode.ProtocolInjection);
        var resolution = ResolveElement(snapshot, tables, preparation.Target);

        if (resolution.FailureReason is { } reason)
            return reason;

        if (resolution.SkillInstanceId != preparation.SkillInstanceId)
            return "SKILL_INSTANCE_CHANGED";

        if (resolution.ElementCell != preparation.ElementCell)
            return "ELEMENT_CELL_CHANGED";

        return null;
    }

    private static ElementUseResolution ResolveElement(ElementUseSnapshot snapshot, GameTables tables, ElementUseTarget target)
    {
        var resolution = new ElementUseResolution();
        var state = snapshot.State;

        if (snapshot.Map is not { } map || map.Id != target.MapId)
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "MAP_DATA_UNKNOWN");
            return resolution;
        }

        if (!map.TryGetInteractive(target.ElementId, out var interactive))
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "ELEMENT_NOT_ON_MAP");
            return resolution;
        }

        resolution.ElementId = interactive.ElementId;
        resolution.GfxId = interactive.GfxId;
        resolution.ElementCell = interactive.CellId is MapInteractive.NoCell ? null : interactive.CellId;

        if (!state.Elements.TryGetValue(target.ElementId, out var element))
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "ELEMENT_NOT_KNOWN");
            return resolution;
        }

        resolution.InteractiveId = element.InteractiveId;

        if (element.State is 2)
        {
            resolution.Fail(ElementUsePreparationState.Blocked, "ELEMENT_BEING_USED");
            return resolution;
        }

        if (element.State is 1)
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "ELEMENT_DEPLETED");
            return resolution;
        }

        if (!tables.Jobs.TryGetSkill(target.SkillId, out var catalogSkill))
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "SKILL_NOT_IN_CATALOG");
            return resolution;
        }

        resolution.SkillMinLevel = catalogSkill.MinLevel;
        resolution.ExpectedItemId = catalogSkill.GatheredItemId;

        if (!element.TryGetEnabled(target.SkillId, out var skill))
        {
            var disabled = element.DisabledSkills.Any(disabledSkill => disabledSkill.SkillId == target.SkillId);
            resolution.Fail(
                ElementUsePreparationState.Unavailable,
                disabled ? "SKILL_DISABLED" : "SKILL_NOT_OFFERED");
            return resolution;
        }

        resolution.SkillId = skill.SkillId;
        resolution.SkillInstanceId = skill.InstanceId;

        if (catalogSkill.GatheredItemId == Skill.NoItem || !tables.Items.TryGetItem(catalogSkill.GatheredItemId, out _))
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "EXPECTED_ITEM_UNKNOWN");
            return resolution;
        }

        if (interactive.CellId == MapInteractive.NoCell)
        {
            resolution.Fail(ElementUsePreparationState.Unavailable, "ELEMENT_CELL_UNKNOWN");
            return resolution;
        }

        if (element.Cell is { } statedCell && statedCell != interactive.CellId)
        {
            resolution.Fail(ElementUsePreparationState.Blocked, "ELEMENT_CELL_CONFLICT");
            return resolution;
        }

        var usedFrom = ElementCells.UsedFrom(
            map.Cells.Span,
            interactive.CellId,
            map.Cells.Span[target.Cell].Zone);
        resolution.UsesFromCurrentCell = usedFrom.Contains(target.Cell);

        if (!resolution.UsesFromCurrentCell)
            resolution.Fail(ElementUsePreparationState.Blocked, "ELEMENT_OUT_OF_RANGE");

        return resolution;
    }

    private static ElementUsePreparation WithResolution(ElementUsePreparation preparation, ElementUseResolution resolution)
    {
        return preparation with
        {
            ElementId = resolution.ElementId,
            InteractiveId = resolution.InteractiveId,
            ElementCell = resolution.ElementCell,
            GfxId = resolution.GfxId,
            SkillId = resolution.SkillId,
            SkillInstanceId = resolution.SkillInstanceId,
            ExpectedItemId = resolution.ExpectedItemId,
            SkillMinLevel = resolution.SkillMinLevel,
            UsesFromCurrentCell = resolution.UsesFromCurrentCell,
        };
    }

    private static ElementUsePreparation Decided(ElementUsePreparation preparation, ElementUsePreparationState state, string reason)
    {
        return preparation with { State = state, Reason = reason };
    }

    private static SupervisedElementUseResult Result(
        SupervisedElementUseOutcome outcome,
        string reason,
        ElementUsePreparation preparation,
        AttemptState attempt,
        CharacterState? endState,
        string? detail = null)
    {
        var evidence = new SupervisedElementUseEvidence
        {
            Target = preparation.Target,
            SessionEpoch = preparation.SessionEpoch,
            StateRevision = preparation.StateRevision,
            StartMapId = preparation.MapId,
            StartCell = preparation.Cell,
            StartProvenance = preparation.Provenance,
            ElementCell = preparation.ElementCell,
            SkillInstanceId = preparation.SkillInstanceId,
            ExpectedItemId = preparation.ExpectedItemId,
            TranslationBlocked = preparation.TranslationBlocked,
            InputMode = preparation.InputMode,
            RequestSent = attempt.RequestSent,
            RequestMayHaveBeenSent = attempt.RequestMayHaveBeenSent,
            InteractiveUsedObserved = attempt.InteractiveUsedObserved,
            InteractiveUseEndedObserved = attempt.InteractiveUseEndedObserved,
            InventoryBefore = attempt.InventoryBefore,
            InventoryAfter = attempt.InventoryAfter,
            ErrorSkillInstanceId = attempt.ErrorSkillInstanceId,
            EndMapId = endState?.MapId,
            EndCell = endState?.Cell,
            EndPositionTrusted = endState?.IsPositionTrusted == true,
            EndProvenance = endState?.Provenance ?? CellProvenance.Unknown,
            WeightRevisionBefore = attempt.WeightRevisionBefore,
        };

        return new SupervisedElementUseResult(outcome, reason, evidence, detail);
    }

    private static int Carried(CharacterState state, int itemId)
    {
        return state.Inventory.Values
            .Where(stack => stack.ItemId == itemId && !stack.IsWorn)
            .Sum(stack => stack.Quantity);
    }

    private static string Interrupted(Interruption reason)
    {
        return reason switch
        {
            Interruption.Player => "INTERRUPTED_PLAYER",
            Interruption.Disconnected => "INTERRUPTED_DISCONNECTED",
            Interruption.Fight => "INTERRUPTED_FIGHT",
            _ => "INTERRUPTED_HOST_STOPPING",
        };
    }

    private sealed class AttemptState
    {
        public volatile bool SendCompleted;

        public bool RequestSent { get; set; }

        public bool RequestMayHaveBeenSent { get; set; }

        public bool InteractiveUsedObserved { get; set; }

        public bool InteractiveUseEndedObserved { get; set; }

        public int? InventoryBefore { get; set; }

        public int? InventoryAfter { get; set; }

        public long? WeightRevisionBefore { get; set; }

        public int? ErrorSkillInstanceId { get; set; }
    }

    private sealed class BehaviorVerdict
    {
        public SupervisedElementUseOutcome Outcome { get; }

        public string Reason { get; }

        public string? Detail { get; }

        public BehaviorVerdict(SupervisedElementUseOutcome outcome, string reason, string? detail = null)
        {
            Outcome = outcome;
            Reason = reason;
            Detail = detail;
        }
    }

    private sealed class ElementUseResolution
    {
        public ElementUsePreparationState FailureState { get; private set; }

        public string? FailureReason { get; private set; }

        public int? ElementId { get; set; }

        public int? InteractiveId { get; set; }

        public int? ElementCell { get; set; }

        public int? GfxId { get; set; }

        public int? SkillId { get; set; }

        public int? SkillInstanceId { get; set; }

        public int? ExpectedItemId { get; set; }

        public int? SkillMinLevel { get; set; }

        public bool UsesFromCurrentCell { get; set; }

        public void Fail(ElementUsePreparationState state, string reason)
        {
            FailureState = state;
            FailureReason = reason;
        }
    }
}
