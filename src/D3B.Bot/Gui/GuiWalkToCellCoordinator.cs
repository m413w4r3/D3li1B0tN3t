using System.Collections.Immutable;
using D3B.Bot.Movement;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Coordinates one snapshot-bound WalkToCell action through capture, intent registration, input, and server proof.</summary>
public sealed class GuiWalkToCellCoordinator
{
    private static readonly ImmutableArray<WindowLocalRect> _noExclusions = ImmutableArray<WindowLocalRect>.Empty;
    private readonly GuiWalkToCellCoordinatorOptions _options;

    /// <summary>Creates a coordinator with short production-safe snapshot, intent, and confirmation bounds.</summary>
    public GuiWalkToCellCoordinator(GuiWalkToCellCoordinatorOptions? options = null)
    {
        _options = options ?? new GuiWalkToCellCoordinatorOptions();
    }

    /// <summary>Executes one supplied same-map plan and never retries its click.</summary>
    public async Task<GuiWalkToCellResult> ExecuteAsync(GuiWalkToCellRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CapturedFrame? frame = null;
        GuiWorldSnapshot? startWorld = null;
        GuiWorldSnapshot? endWorld = null;
        GameWindowSnapshot? startWindow = null;
        GameWindowSnapshot? endWindow = null;
        CalibrationResult? validatedCalibration = null;
        TargetRegion? target = null;
        GuiActionIntent? intent = null;
        MapData? map = null;
        int? targetCell = null;
        long? mapId = null;
        bool clickGateOpened = false;
        GameWindowSnapshot? lastConfirmationWindow = null;

        try
        {
            if (cancellationToken.IsCancellationRequested)
                return Result(GuiActionOutcome.CreateInterrupted(false, "The walk was canceled before its first snapshot."));

            startWorld = request.SnapshotAdapter.Capture(request.Bot);
            mapId = startWorld.MapId;

            GuiActionOutcome? worldBlock = ValidateWorld(startWorld, request, "S1");
            if (worldBlock is not null)
                return Result(worldBlock);

            if (!IsSnapshotFresh(startWorld, request.TimeProvider.GetUtcNow()))
                return Result(GuiActionOutcome.CreateBlocked(GuiSafetyReason.StateChanged, "The S1 protocol snapshot is stale or from the future."));

            if (request.GuiOptions.Backend != GuiBackend.X11)
                return Result(GuiActionOutcome.CreateBlocked(GuiSafetyReason.NotX11, "WalkToCell requires the configured X11 backend."));

            startWindow = await request.WindowLocator.LocateAsync(request.Xid, request.WindowExpectation, cancellationToken)
                .ConfigureAwait(false);
            GuiActionOutcome? windowBlock = ValidateWindow(startWindow, request);
            if (windowBlock is not null)
                return Result(windowBlock);

            frame = await request.WindowCapture.CaptureAsync(request.Xid, request.WindowExpectation, cancellationToken)
                .ConfigureAwait(false);
            if (frame is null)
                return Result(GuiActionOutcome.CreateBlocked(GuiSafetyReason.CaptureFailed, "The selected X11 window returned no capture frame."));

            GuiActionOutcome? frameBlock = ValidateFrame(frame, startWindow, request.GuiOptions);
            if (frameBlock is not null)
                return Result(frameBlock);

            if (!TryValidateCalibration(
                request.Calibration,
                frame,
                startWindow,
                startWorld.MapId!.Value,
                request,
                request.TimeProvider.GetUtcNow(),
                out validatedCalibration,
                out GuiActionOutcome? calibrationBlock))
            {
                return Result(calibrationBlock!);
            }

            if (!TryValidatePlan(startWorld, request, out map, out targetCell, out GuiActionOutcome? planBlock))
                return Result(planBlock!);

            CalibrationProfile profile = validatedCalibration!.Profile!;
            HashSet<int> interactiveCells = GetInteractiveCells(startWorld);
            target = TerrainCellTargetLocator.Locate(
                frame,
                profile,
                mapId!.Value,
                new LogicalCell(targetCell!.Value),
                CreateClientAreaGeometry(startWindow),
                request.SafetyMargin,
                request.UiExclusions.IsDefault ? _noExclusions : request.UiExclusions,
                interactiveCells);

            GuiActionOutcome? targetBlock = ValidateTarget(target);
            if (targetBlock is not null)
                return Result(targetBlock);

            if (!IsSnapshotFresh(startWorld, request.TimeProvider.GetUtcNow()))
                return Result(GuiActionOutcome.CreateBlocked(GuiSafetyReason.StateChanged, "The S1 protocol snapshot expired during capture and projection."));

            endWorld = request.SnapshotAdapter.Capture(request.Bot);
            GuiActionOutcome? endWorldBlock = ValidateWorld(endWorld, request, "S2");
            if (endWorldBlock is not null)
                return Result(endWorldBlock);

            if (!SnapshotsMatch(startWorld, endWorld, out string difference))
            {
                GuiSafetyReason reason = endWorld.IsUserInterrupted
                    ? GuiSafetyReason.UserInterrupted
                    : GuiSafetyReason.StateChanged;
                GuiActionOutcome changed = reason == GuiSafetyReason.UserInterrupted
                    ? GuiActionOutcome.CreateInterrupted(false, $"The S1 and S2 protocol snapshots differ in {difference}.")
                    : GuiActionOutcome.CreateBlocked(reason, $"The S1 and S2 protocol snapshots differ in {difference}.");
                return Result(changed);
            }

            DateTimeOffset evidenceNow = request.TimeProvider.GetUtcNow();
            if (!IsSnapshotFresh(startWorld, evidenceNow) || !IsSnapshotFresh(endWorld, evidenceNow))
                return Result(GuiActionOutcome.CreateBlocked(GuiSafetyReason.StateChanged, "The S1 or S2 protocol snapshot is stale or from the future."));

            endWindow = await request.WindowLocator.LocateAsync(request.Xid, request.WindowExpectation, cancellationToken)
                .ConfigureAwait(false);
            windowBlock = ValidateWindow(endWindow, request);
            if (windowBlock is not null)
                return Result(windowBlock);

            if (!SameWindowObservation(startWindow, endWindow)
                || !FrameMatchesWindow(frame, startWindow)
                || !FrameMatchesWindow(frame, endWindow))
            {
                return Result(GuiActionOutcome.CreateBlocked(
                    GuiSafetyReason.WindowChanged,
                    "The selected window or its frame changed between the initial and final observations."));
            }

            if (!TryValidateCalibration(
                request.Calibration,
                frame,
                endWindow,
                endWorld.MapId!.Value,
                request,
                evidenceNow,
                out CalibrationResult? finalCalibration,
                out calibrationBlock))
            {
                return Result(calibrationBlock!);
            }

            if (!string.Equals(
                validatedCalibration.Profile!.CalibrationId,
                finalCalibration!.Profile!.CalibrationId,
                StringComparison.Ordinal))
            {
                return Result(GuiActionOutcome.CreateBlocked(GuiSafetyReason.CalibrationStale, "The validated calibration identity changed during the evidence window."));
            }

            validatedCalibration = finalCalibration;
            evidenceNow = request.TimeProvider.GetUtcNow();
            DateTimeOffset expiresAt = evidenceNow + _options.IntentLifetime;
            intent = new GuiActionIntent(
                Guid.NewGuid(),
                GuiActionKind.WalkToCell,
                endWorld.SessionEpoch,
                endWorld.StateRevision,
                mapId.Value,
                new LogicalCell(targetCell.Value),
                null,
                endWindow.Identity,
                validatedCalibration.Profile.CalibrationId,
                GuiClientAction.Walk,
                GuiServerOutcome.MovementConfirmed,
                evidenceNow,
                expiresAt);

            GuiProtocolTrustEvidence trustEvidence = new(
                intent,
                startWorld,
                endWorld,
                startWindow,
                endWindow,
                frame,
                validatedCalibration,
                target,
                startWorld.CellId!.Value.Value,
                targetCell.Value,
                true,
                HasUnknownProtocolEvidence(startWorld, endWorld),
                HasClosedTranslationLatch(startWorld, endWorld),
                false,
                HasHumanAmbiguity(startWorld, endWorld, startWindow, endWindow),
                evidenceNow,
                _options.MaxSnapshotAge,
                _options.AllowSyntheticCalibration);
            GuiProtocolTrustDecision trustDecision = new GuiProtocolTrustPolicy(request.Gate, request.TimeProvider).Evaluate(trustEvidence);
            if (!trustDecision.Allowed)
                return Result(GuiActionOutcome.CreateBlocked(trustDecision.Reason, trustDecision.Detail));

            GuiIntentRegistration registration = await request.Bot.RegisterGuiIntentAsync(
                intent,
                endWorld,
                endWindow,
                cancellationToken).ConfigureAwait(false);
            if (registration.Status != GuiIntentRegistrationStatus.Registered)
            {
                await CancelIntentAsync(request, intent.IntentId, false, registration.Detail ?? "The actor refused the GUI intention.", cancellationToken)
                    .ConfigureAwait(false);
                GuiActionOutcome refusal = RegistrationOutcome(registration);
                return Result(refusal);
            }

            bool acknowledged = await request.Bot.AcknowledgeGuiIntentAsync(
                intent.IntentId,
                endWorld,
                endWindow,
                cancellationToken).ConfigureAwait(false);
            if (!acknowledged)
            {
                GuiWorldSnapshot current = request.SnapshotAdapter.Capture(request.Bot);
                bool interrupted = cancellationToken.IsCancellationRequested || current.IsUserInterrupted;
                string detail = interrupted
                    ? "The player or caller interrupted the intention before acknowledgement."
                    : "The actor refused the final intention acknowledgement because its state or window changed.";
                await CancelIntentAsync(request, intent.IntentId, false, detail, cancellationToken).ConfigureAwait(false);
                GuiActionOutcome refusal = interrupted
                    ? GuiActionOutcome.CreateInterrupted(false, detail)
                    : GuiActionOutcome.CreateBlocked(GuiSafetyReason.StateChanged, detail);
                return Result(refusal);
            }

            CancellationToken actorRevocationToken = request.Bot.GetGuiClickCancellationToken(intent.IntentId);

            if (actorRevocationToken.IsCancellationRequested)
            {
                string detail = "The actor revoked the intention after acknowledgement and before input.";
                await CancelIntentAsync(request, intent.IntentId, false, detail, cancellationToken).ConfigureAwait(false);
                return Result(GuiActionOutcome.CreateInterrupted(false, detail));
            }

            GameWindowSnapshot clickWindow = await request.WindowLocator.LocateAsync(
                request.Xid,
                request.WindowExpectation,
                cancellationToken).ConfigureAwait(false);
            windowBlock = ValidateWindow(clickWindow, request);
            if (windowBlock is not null || !SameWindowObservation(endWindow, clickWindow))
            {
                bool interrupted = cancellationToken.IsCancellationRequested
                    || request.Bot.IsPaused
                    || IsWindowUnavailable(clickWindow);
                GuiActionOutcome refusal = interrupted
                    ? GuiActionOutcome.CreateInterrupted(false, "The selected window or player state changed after acknowledgement and before input.")
                    : GuiActionOutcome.CreateBlocked(
                        GuiSafetyReason.WindowChanged,
                        windowBlock?.Detail ?? "The selected window changed after acknowledgement and before input.");
                await CancelIntentAsync(request, intent.IntentId, false, refusal.Detail ?? "The final window guard failed.", cancellationToken)
                    .ConfigureAwait(false);
                return Result(refusal);
            }

            BotGuiOutcomeConfirmation confirmation = new(
                request.Bot.WaitForGuiOutcomeAsync,
                request.TimeProvider,
                _options.ConfirmationTimeout,
                async (currentIntent, token) =>
                {
                    GameWindowSnapshot current = await request.WindowLocator.LocateAsync(
                        request.Xid,
                        request.WindowExpectation,
                        token).ConfigureAwait(false);
                    lastConfirmationWindow = current;
                    return current;
                });

            AcknowledgedGuiInput acknowledgedInput = new(
                intent.IntentId,
                endWorld,
                endWindow,
                async (intentId, world, window, token) =>
                {
                    bool marked = await request.Bot.MarkGuiIntentClickedAsync(intentId, world, window, token)
                        .ConfigureAwait(false);
                    if (marked)
                        clickGateOpened = true;
                    return marked;
                },
                request.Input,
                actorRevocationToken);

            GuiActionOutcome execution = await request.Executor.ExecuteAsync(
                intent,
                endWorld,
                endWindow,
                frame,
                validatedCalibration,
                target,
                acknowledgedInput,
                confirmation,
                cancellationToken).ConfigureAwait(false);

            if (execution.Status == GuiActionOutcomeStatus.Pass)
            {
                bool consumed = await request.Bot.ConsumeGuiIntentAsync(intent.IntentId, cancellationToken).ConfigureAwait(false);
                if (!consumed)
                {
                    await CancelIntentAsync(request, intent.IntentId, true, "Server confirmation arrived but the actor could not consume the intention.", cancellationToken)
                        .ConfigureAwait(false);
                    return Result(GuiActionOutcome.CreateUnconfirmed(true, "Server confirmation arrived but the actor could not consume the intention."));
                }

                return Result(execution);
            }

            if (execution.Status == GuiActionOutcomeStatus.Blocked)
            {
                await CancelIntentAsync(request, intent.IntentId, false, execution.Detail ?? "The executor's final guard blocked input.", cancellationToken)
                    .ConfigureAwait(false);
                return Result(execution);
            }

            if (execution.Status == GuiActionOutcomeStatus.Failed)
            {
                await CancelIntentAsync(request, intent.IntentId, execution.ClickEmitted, execution.Detail ?? "The input backend failed.", cancellationToken)
                    .ConfigureAwait(false);
                return Result(execution);
            }

            if (execution.Status == GuiActionOutcomeStatus.Interrupted)
            {
                bool mayHaveClicked = execution.ClickEmitted;
                await CancelIntentAsync(request, intent.IntentId, mayHaveClicked, execution.Detail ?? "The action was interrupted.", cancellationToken)
                    .ConfigureAwait(false);
                return Result(execution);
            }

            await CancelIntentAsync(request, intent.IntentId, true, execution.Detail ?? "The click outcome could not be confirmed.", cancellationToken)
                .ConfigureAwait(false);

            if (request.Bot.IsPaused
                || lastConfirmationWindow is not null && IsWindowUnavailable(lastConfirmationWindow))
            {
                return Result(GuiActionOutcome.CreateInterrupted(true, "The player took control or the selected window was lost after the click."));
            }

            return Result(GuiActionOutcome.CreateUnconfirmed(true, execution.Detail ?? "The click outcome could not be confirmed."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (intent is not null)
            {
                await CancelIntentAsync(
                    request,
                    intent.IntentId,
                    clickGateOpened,
                    "The caller canceled the intention.",
                    cancellationToken).ConfigureAwait(false);
            }

            return Result(GuiActionOutcome.CreateInterrupted(clickGateOpened, "The caller canceled the intention."));
        }
        catch (Exception)
        {
            if (intent is not null)
            {
                await CancelIntentAsync(
                    request,
                    intent.IntentId,
                    clickGateOpened,
                    "An unexpected coordinator failure ended the intention.",
                    cancellationToken).ConfigureAwait(false);
            }

            return Result(clickGateOpened
                ? GuiActionOutcome.CreateUnconfirmed(true, "An unexpected failure followed the one permitted click attempt.")
                : GuiActionOutcome.CreateFailed(false, "An unexpected coordinator failure occurred before input."));
        }
        finally
        {
            frame?.Dispose();
        }

        GuiWalkToCellResult Result(GuiActionOutcome outcome)
        {
            return new GuiWalkToCellResult(outcome, intent?.IntentId, mapId, targetCell, target);
        }
    }

    private static GuiActionOutcome? ValidateWorld(GuiWorldSnapshot world, GuiWalkToCellRequest request, string name)
    {
        if (!world.IsConnected)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.Disconnected, $"The {name} protocol snapshot is disconnected.");

        if (world.IsUserInterrupted)
            return GuiActionOutcome.CreateInterrupted(false, $"The player paused or took control before the {name} snapshot.");

        if (world.IsFighting)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.InFight, $"A fight is active in the {name} protocol snapshot.");

        if (world.IsWalking || world.IsUsing || world.IsInDialog)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.Busy, $"The character is walking, using an element, or in a dialog in {name}.");

        if (world.HasAmbiguousMovement)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.PositionAmbiguous, $"Movement is ambiguous in the {name} protocol snapshot.");

        if (world.ProtocolObservationStatus is ProtocolObservationStatus.TranslationBlocked or ProtocolObservationStatus.Contradictory)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.ProtocolContradiction, $"Protocol observations are blocked or contradictory in {name}.");

        if (world.ProtocolObservationStatus == ProtocolObservationStatus.Unknown || world.MapId is null or <= 0)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.MapUnknown, $"A trusted map is unavailable in the {name} protocol snapshot.");

        if (world.ProtocolObservationStatus == ProtocolObservationStatus.Partial || world.CellId is null)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.CellUnknown, $"A complete map and cell snapshot is required at {name}.");

        if (world.ProtocolObservationStatus != ProtocolObservationStatus.Complete)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.ProtocolContradiction, $"The {name} protocol snapshot is not complete.");

        if (world.CellProvenance is not (GuiCellProvenance.ServerPlaced or GuiCellProvenance.ServerConfirmed))
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.PositionUntrusted, $"The {name} cell lacks server placement or confirmation provenance.");

        if (world.CellId.Value.Value is < 0 or >= MapData.CellCount)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.CellUnknown, $"The {name} cell is outside the map cell range.");

        return null;
    }

    private static GuiActionOutcome? ValidateWindow(GameWindowSnapshot window, GuiWalkToCellRequest request)
    {
        if (!window.Exists || window.Identity.Status == GameWindowIdentityStatus.NotFound)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowNotFound, "The explicitly selected X11 window was not found.");

        if (window.Identity.Xid != request.Xid)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowChanged, "The locator returned a different X11 window than the caller selected.");

        if (window.Identity.Status != GameWindowIdentityStatus.Verified)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowIdentityUnverified, "The selected window process identity is not verified.");

        if (!string.Equals(Path.GetFileName(window.Identity.ExecutablePath), request.WindowExpectation.ExpectedExecutableName, StringComparison.Ordinal))
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowIdentityUnverified, "The selected window executable does not match the requested process identity.");

        if (!window.IsSessionX11 || !window.HasValidDisplay || !window.IsXTestAvailable)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.NotX11, "A valid X11 display with XTest is required for the selected window.");

        if (!window.IsMapped || !window.IsViewable || window.IsMinimized || !window.IsFocused || !window.IsActive)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowNotActive, "The selected window must remain mapped, viewable, focused, and active.");

        if (!window.GeometryReliable
            || window.WindowWidth <= 0
            || window.WindowHeight <= 0
            || window.ClientWidth <= 0
            || window.ClientHeight <= 0
            || window.BorderWidth < 0)
        {
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowChanged, "The selected window geometry is not reliable.");
        }

        return null;
    }

    private static GuiActionOutcome? ValidateFrame(CapturedFrame frame, GameWindowSnapshot window, GuiOptions options)
    {
        if (frame.Status != CaptureStatus.Captured)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.CaptureFailed, "The selected window capture did not complete successfully.");

        if (!frame.ContentUsable || frame.PixelWidth <= 0 || frame.PixelHeight <= 0
            || frame.PixelWidth > options.MaxWidth || frame.PixelHeight > options.MaxHeight
            || frame.Image.Empty())
        {
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.SceneUnverified, "The capture is empty, unusable, or outside the configured dimensions.");
        }

        if (frame.PixelWidth != frame.Image.Cols
            || frame.PixelHeight != frame.Image.Rows
            || frame.PixelWidth != window.WindowWidth
            || frame.PixelHeight != window.WindowHeight
            || !SameIdentity(frame.WindowIdentity, window.Identity)
            || !FrameMatchesWindow(frame, window))
        {
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.WindowChanged, "The capture dimensions or identity do not match the selected window.");
        }

        return null;
    }

    private bool TryValidateCalibration(
        CalibrationResult prepared,
        CapturedFrame frame,
        GameWindowSnapshot window,
        long mapId,
        GuiWalkToCellRequest request,
        DateTimeOffset now,
        out CalibrationResult? validated,
        out GuiActionOutcome? failure)
    {
        validated = null;
        failure = null;
        CalibrationProfile? profile = prepared.Profile;

        if (prepared.Status == CalibrationStatus.Stale)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.CalibrationStale, "The prepared calibration is marked stale.");
            return false;
        }

        if (!prepared.IsCalibrated || profile is null)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.NotCalibrated, "A valid prepared calibration profile is required.");
            return false;
        }

        if ((profile.IsSynthetic || prepared.IsSynthetic) && !_options.AllowSyntheticCalibration)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.CalibrationStale, "Synthetic calibration is allowed only by the test-only coordinator option.");
            return false;
        }

        if (!TryCreateEnvironment(frame, window, mapId, profile, out CalibrationEnvironment? environment))
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.CalibrationStale, "The current window, capture, or calibration geometry cannot form a valid environment.");
            return false;
        }

        CalibrationResult result = CalibrationValidator.ValidateProfile(
            profile,
            environment!,
            now,
            TimeSpan.FromSeconds(request.GuiOptions.CalibrationMaxAgeSeconds));

        if (!result.IsCalibrated)
        {
            GuiSafetyReason reason = result.Status == CalibrationStatus.Stale || result.Status == CalibrationStatus.Invalidated
                ? GuiSafetyReason.CalibrationStale
                : GuiSafetyReason.NotCalibrated;
            failure = GuiActionOutcome.CreateBlocked(reason, result.Problems.IsDefaultOrEmpty
                ? "The prepared calibration did not pass current environment validation."
                : result.Problems[0].Detail);
            return false;
        }

        if ((profile.IsSynthetic || result.IsSynthetic) && !_options.AllowSyntheticCalibration)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.CalibrationStale, "Synthetic calibration is disabled for production coordinator options.");
            return false;
        }

        validated = result;
        return true;
    }

    private static bool TryCreateEnvironment(
        CapturedFrame frame,
        GameWindowSnapshot window,
        long mapId,
        CalibrationProfile profile,
        out CalibrationEnvironment? environment)
    {
        environment = null;

        if (!TryCreateClientAreaGeometry(window, out ClientAreaGeometry geometry)
            || window.WindowWidth <= 0
            || window.WindowHeight <= 0)
        {
            return false;
        }

        double widthScale = frame.PixelWidth / (double)window.WindowWidth;
        double heightScale = frame.PixelHeight / (double)window.WindowHeight;
        if (!double.IsFinite(widthScale) || !double.IsFinite(heightScale)
            || widthScale <= 0 || heightScale <= 0
            || Math.Abs(widthScale - heightScale) > 1e-9)
        {
            return false;
        }

        environment = new CalibrationEnvironment(
            window.Identity,
            frame.PixelWidth,
            frame.PixelHeight,
            geometry,
            (widthScale + heightScale) / 2,
            mapId,
            CalibrationAnchorDocument.CurrentFormatVersion);
        return profile.CalibrationVersion == CalibrationAnchorDocument.CurrentFormatVersion;
    }

    private static bool TryCreateClientAreaGeometry(GameWindowSnapshot window, out ClientAreaGeometry geometry)
    {
        long desktopX = (long)window.DesktopOrigin.X + window.BorderWidth;
        long desktopY = (long)window.DesktopOrigin.Y + window.BorderWidth;
        if (desktopX is < int.MinValue or > int.MaxValue || desktopY is < int.MinValue or > int.MaxValue)
        {
            geometry = default;
            return false;
        }

        geometry = new ClientAreaGeometry(
            new DesktopPixels((int)desktopX, (int)desktopY),
            new ClientAreaPixels(window.BorderWidth, window.BorderWidth),
            window.ClientWidth,
            window.ClientHeight);
        return true;
    }

    private static ClientAreaGeometry CreateClientAreaGeometry(GameWindowSnapshot window)
    {
        TryCreateClientAreaGeometry(window, out ClientAreaGeometry geometry);
        return geometry;
    }

    private static bool TryValidatePlan(
        GuiWorldSnapshot world,
        GuiWalkToCellRequest request,
        out MapData? map,
        out int? targetCell,
        out GuiActionOutcome? failure)
    {
        map = null;
        targetCell = null;
        failure = null;

        if (request.Plan.Status != WalkToCellPlanStatus.Reachable
            || request.Plan.TargetCell is not { } plannedTarget)
        {
            failure = GuiActionOutcome.CreateBlocked(
                request.Plan.Status == WalkToCellPlanStatus.Unreachable ? GuiSafetyReason.PositionUntrusted : GuiSafetyReason.CellUnknown,
                request.Plan.Detail ?? "A reachable WalkToCell plan is required.");
            return false;
        }

        targetCell = plannedTarget;
        if (!request.Maps.TryGet(world.MapId!.Value, out map) || map is null || map.Id != world.MapId)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.MapUnknown, "The current map is not present in the supplied GameMaps source.");
            return false;
        }

        ReadOnlySpan<int> path = request.Plan.PathCells.Span;
        if (path.Length < 2 || path[0] != world.CellId!.Value.Value || path[^1] != plannedTarget)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.PositionUntrusted, "The supplied plan does not start at S1 and end at its requested target.");
            return false;
        }

        if (plannedTarget == world.CellId.Value.Value)
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.CellUnknown, "The current cell cannot be the target of a GUI walk.");
            return false;
        }

        HashSet<int> interactiveCells = GetInteractiveCells(world);
        if (interactiveCells.Contains(plannedTarget))
        {
            failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.TargetUnverified, "The target cell contains a known interactive element.");
            return false;
        }

        ReadOnlySpan<MapCell> cells = map.Cells.Span;
        for (int index = 0; index < path.Length; index++)
        {
            int cell = path[index];
            if (cell < 0 || cell >= cells.Length || !cells[cell].IsWalkable)
            {
                failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.PositionUntrusted, "The supplied plan contains a cell that is not walkable on the current map.");
                return false;
            }

            if (index > 0 && MapGeometry.Direction(path[index - 1], cell) < 0)
            {
                failure = GuiActionOutcome.CreateBlocked(GuiSafetyReason.PositionUntrusted, "The supplied plan contains a non-adjacent step on the current map.");
                return false;
            }
        }

        return true;
    }

    private static HashSet<int> GetInteractiveCells(GuiWorldSnapshot world)
    {
        HashSet<int> cells = [];
        if (world.KnownElements.IsDefaultOrEmpty)
            return cells;

        foreach (GuiKnownElement element in world.KnownElements)
        {
            if (element.Cell is { } cell)
                cells.Add(cell.Value);
        }

        return cells;
    }

    private static GuiActionOutcome? ValidateTarget(TargetRegion target)
    {
        if (target.Status == TargetStatus.Ambiguous)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.AmbiguousTarget, "The target projection was ambiguous.");

        if (target.Status != TargetStatus.Verified || target.ProposedClickPoint is null)
            return GuiActionOutcome.CreateBlocked(GuiSafetyReason.TargetUnverified, $"The terrain cell projection was not verified ({target.Reason}).");

        return null;
    }

    private bool IsSnapshotFresh(GuiWorldSnapshot world, DateTimeOffset now)
    {
        TimeSpan age = now - world.CapturedAt;
        return age >= TimeSpan.Zero && age <= _options.MaxSnapshotAge;
    }

    private static bool SnapshotsMatch(GuiWorldSnapshot first, GuiWorldSnapshot second, out string difference)
    {
        if (first.SessionEpoch != second.SessionEpoch)
            difference = "session epoch";
        else if (first.StateRevision != second.StateRevision)
            difference = "state revision or translation latch";
        else if (first.MapId != second.MapId)
            difference = "map";
        else if (first.CellId != second.CellId)
            difference = "cell";
        else if (first.CellProvenance != second.CellProvenance)
            difference = "cell provenance";
        else if (first.IsConnected != second.IsConnected)
            difference = "connection state";
        else if (first.IsFighting != second.IsFighting)
            difference = "fight state";
        else if (first.IsWalking != second.IsWalking)
            difference = "walking state";
        else if (first.IsUsing != second.IsUsing)
            difference = "use state";
        else if (first.IsInDialog != second.IsInDialog)
            difference = "dialog state";
        else if (first.HasAmbiguousMovement != second.HasAmbiguousMovement)
            difference = "movement ambiguity";
        else if (first.ProtocolObservationStatus != second.ProtocolObservationStatus)
            difference = "protocol observation status";
        else if (first.IsUserInterrupted != second.IsUserInterrupted)
            difference = "paused state";
        else if (!KnownElementsMatch(first.KnownElements, second.KnownElements))
            difference = "known map elements";
        else
        {
            difference = string.Empty;
            return true;
        }

        return false;
    }

    private static bool KnownElementsMatch(ImmutableArray<GuiKnownElement> first, ImmutableArray<GuiKnownElement> second)
    {
        if (first.IsDefault != second.IsDefault || first.Length != second.Length)
            return false;

        for (int index = 0; index < first.Length; index++)
        {
            GuiKnownElement left = first[index];
            GuiKnownElement right = second[index];
            if (left.ElementId != right.ElementId
                || left.Cell != right.Cell
                || left.InteractiveId != right.InteractiveId
                || left.State != right.State
                || !left.EnabledSkillIds.SequenceEqual(right.EnabledSkillIds))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameWindowObservation(GameWindowSnapshot first, GameWindowSnapshot second)
    {
        return SameIdentity(first.Identity, second.Identity)
            && first.Exists == second.Exists
            && first.IsMapped == second.IsMapped
            && first.IsViewable == second.IsViewable
            && first.IsMinimized == second.IsMinimized
            && first.IsFocused == second.IsFocused
            && first.IsActive == second.IsActive
            && first.IsXTestAvailable == second.IsXTestAvailable
            && first.IsSessionX11 == second.IsSessionX11
            && first.HasValidDisplay == second.HasValidDisplay
            && first.GeometryReliable == second.GeometryReliable
            && first.DesktopOrigin == second.DesktopOrigin
            && first.WindowWidth == second.WindowWidth
            && first.WindowHeight == second.WindowHeight
            && first.BorderWidth == second.BorderWidth
            && first.ClientWidth == second.ClientWidth
            && first.ClientHeight == second.ClientHeight;
    }

    private static bool IsWindowUnavailable(GameWindowSnapshot window)
    {
        return !window.Exists
            || window.Identity.Status == GameWindowIdentityStatus.NotFound
            || !window.IsMapped
            || !window.IsViewable
            || window.IsMinimized
            || !window.IsFocused
            || !window.IsActive;
    }

    private static bool FrameMatchesWindow(CapturedFrame frame, GameWindowSnapshot window)
    {
        return SameIdentity(frame.WindowIdentity, window.Identity)
            && SameWindowObservation(frame.WindowSnapshotBefore, window)
            && SameWindowObservation(frame.WindowSnapshotAfter, window);
    }

    private static bool SameIdentity(GameWindowIdentity first, GameWindowIdentity second)
    {
        return first.Xid == second.Xid
            && first.ProcessId == second.ProcessId
            && string.Equals(first.ExecutablePath, second.ExecutablePath, StringComparison.Ordinal)
            && first.Status == second.Status;
    }

    private static bool HasUnknownProtocolEvidence(GuiWorldSnapshot first, GuiWorldSnapshot second)
    {
        return first.ProtocolObservationStatus != ProtocolObservationStatus.Complete
            || second.ProtocolObservationStatus != ProtocolObservationStatus.Complete;
    }

    private static bool HasClosedTranslationLatch(GuiWorldSnapshot first, GuiWorldSnapshot second)
    {
        return (first.StateRevision & 1L) != 0
            || (second.StateRevision & 1L) != 0
            || first.ProtocolObservationStatus == ProtocolObservationStatus.TranslationBlocked
            || second.ProtocolObservationStatus == ProtocolObservationStatus.TranslationBlocked;
    }

    private static bool HasHumanAmbiguity(
        GuiWorldSnapshot first,
        GuiWorldSnapshot second,
        GameWindowSnapshot startWindow,
        GameWindowSnapshot endWindow)
    {
        return first.HasAmbiguousMovement
            || second.HasAmbiguousMovement
            || first.IsUserInterrupted
            || second.IsUserInterrupted
            || !startWindow.IsFocused
            || !endWindow.IsFocused
            || !startWindow.IsActive
            || !endWindow.IsActive;
    }

    private static GuiActionOutcome RegistrationOutcome(GuiIntentRegistration registration)
    {
        return registration.Status switch
        {
            GuiIntentRegistrationStatus.Busy => GuiActionOutcome.CreateBlocked(GuiSafetyReason.Busy, registration.Detail ?? "Another GUI intention is active."),
            GuiIntentRegistrationStatus.Duplicate => GuiActionOutcome.CreateBlocked(GuiSafetyReason.StateChanged, registration.Detail ?? "This GUI intention identifier was already submitted."),
            GuiIntentRegistrationStatus.Expired => GuiActionOutcome.CreateBlocked(GuiSafetyReason.IntentExpired, registration.Detail ?? "The GUI intention expired before registration."),
            _ => GuiActionOutcome.CreateBlocked(GuiSafetyReason.ProtocolContradiction, registration.Detail ?? "The actor rejected the GUI intention."),
        };
    }

    private static async Task CancelIntentAsync(
        GuiWalkToCellRequest request,
        Guid intentId,
        bool clickMayHaveBeenEmitted,
        string reason,
        CancellationToken cancellationToken)
    {
        CancellationToken cleanupToken = cancellationToken.IsCancellationRequested
            ? CancellationToken.None
            : cancellationToken;

        try
        {
            await request.Bot.CancelGuiIntentAsync(intentId, clickMayHaveBeenEmitted, reason, cleanupToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }
}
