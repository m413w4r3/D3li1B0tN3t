using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Input;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Gui.Actions;

/// <summary>Consumes each intention once, emits at most one click, and requires explicit outcome confirmation.</summary>
public sealed class GuiActionExecutor
{
    private readonly object _sync = new();
    private readonly HashSet<Guid> _consumedIntentIds = [];
    private readonly GuiActionGate _gate;
    private readonly GuiOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IGuiOutcomeConfirmation _confirmation;
    private bool _isExecuting;

    /// <summary>Creates the executor with the P2N default confirmation seam, which never confirms an action.</summary>
    public GuiActionExecutor(GuiActionGate gate, GuiOptions options, TimeProvider timeProvider)
        : this(gate, options, timeProvider, new NeverConfirms())
    {
    }

    /// <summary>Creates an executor with a gate, the current P2N policy, and a confirmation seam.</summary>
    public GuiActionExecutor(
        GuiActionGate gate,
        GuiOptions options,
        TimeProvider timeProvider,
        IGuiOutcomeConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(confirmation);

        _gate = gate;
        _options = options;
        _timeProvider = timeProvider;
        _confirmation = confirmation;
    }

    /// <summary>Evaluates and consumes one intention; this method never retries an input operation.</summary>
    public async Task<GuiActionOutcome> ExecuteAsync(
        GuiActionIntent intent,
        GuiWorldSnapshot world,
        GameWindowSnapshot window,
        CapturedFrame frame,
        CalibrationResult calibration,
        TargetRegion target,
        IGuiInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(input);

        if (!TryReserve(intent.IntentId))
            return Blocked(GuiSafetyReason.Busy, false, "An intention is already active or this identifier was consumed.");

        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();

            if (now < intent.CreatedAt || now >= intent.ExpiresAt)
                return Blocked(GuiSafetyReason.IntentExpired, false, "The action intention has expired.");

            if (cancellationToken.IsCancellationRequested)
                return Interrupted(false, "The action was canceled before input.");

            GuiReadiness readiness = _gate.Evaluate(intent, world, window, frame, calibration, target);

            if (!readiness.Allowed)
                return Blocked(readiness.ReasonCode, false, readiness.Detail);

            if (!readiness.IsBoundTo(intent, world, window, calibration, target))
                return Blocked(GuiSafetyReason.StateChanged, false, "The readiness result is not bound to these arguments.");

            if (!input.IsEnabled)
                return Blocked(GuiSafetyReason.Disabled, false, "The configured GUI input is disabled.");

            if (!Enum.IsDefined(_options.Mode))
                return Blocked(GuiSafetyReason.Disabled, false, "The configured GUI mode is not supported.");

            if (input.EmitsRealInput && (_options.Mode == GuiMode.Preview || !_options.AllowRealInput))
                return Blocked(GuiSafetyReason.Disabled, false, "Preview and disabled real-input policy cannot emit a click.");

            if (_timeProvider.GetUtcNow() >= intent.ExpiresAt)
                return Blocked(GuiSafetyReason.IntentExpired, false, "The action intention expired before input.");

            if (cancellationToken.IsCancellationRequested)
                return Interrupted(false, "The action was canceled before input.");

            if (target.ProposedClickPoint is not { } clickPoint || calibration.Profile is not { } profile)
                return Blocked(GuiSafetyReason.TargetUnverified, false, "A verified target point is required.");

            if (!TryGetDesktopPoint(clickPoint, profile.ClientAreaGeometry, out DesktopPixels desktopPoint))
                return Blocked(GuiSafetyReason.TargetUnverified, false, "The verified target point could not be mapped to the desktop.");

            GuiInputResult inputResult;

            try
            {
                inputResult = await input.ClickAsync(desktopPoint, window, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Interrupted(false, "The input operation was canceled.");
            }
            catch (Exception)
            {
                return Failed(false, "The input operation failed.");
            }

            if (inputResult.Status != GuiInputStatus.Sent && inputResult.ClickMayHaveBeenEmitted)
                return Unconfirmed(true, inputResult.Detail ?? "The click command started but its outcome is uncertain.");

            if (inputResult.Status != GuiInputStatus.Sent)
                return ConvertInputFailure(inputResult);

            if (cancellationToken.IsCancellationRequested)
                return Interrupted(true, "The action was canceled after the click.");

            GuiServerOutcome? confirmedOutcome;

            try
            {
                confirmedOutcome = await _confirmation.WaitForConfirmationAsync(intent, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Interrupted(true, "Confirmation wait was canceled after the click.");
            }
            catch (Exception)
            {
                return Unconfirmed(true, "The outcome confirmation failed.");
            }

            if (confirmedOutcome != intent.ExpectedServerOutcome)
                return Unconfirmed(true, "The matching client request and expected server outcome were not confirmed.");

            return new GuiActionOutcome(GuiActionOutcomeStatus.Pass, null, true, null);
        }
        finally
        {
            Release();
        }

        bool TryReserve(Guid intentId)
        {
            lock (_sync)
            {
                if (!_consumedIntentIds.Add(intentId))
                    return false;

                if (_isExecuting)
                    return false;

                _isExecuting = true;
                return true;
            }
        }

        void Release()
        {
            lock (_sync)
            {
                _isExecuting = false;
            }
        }

        static GuiActionOutcome Blocked(GuiSafetyReason reason, bool clickEmitted, string? detail)
        {
            return new GuiActionOutcome(GuiActionOutcomeStatus.Blocked, reason, clickEmitted, detail);
        }

        static GuiActionOutcome Interrupted(bool clickEmitted, string detail)
        {
            return new GuiActionOutcome(GuiActionOutcomeStatus.Interrupted, null, clickEmitted, detail);
        }

        static GuiActionOutcome Failed(bool clickEmitted, string detail)
        {
            return new GuiActionOutcome(GuiActionOutcomeStatus.Failed, null, clickEmitted, detail);
        }

        static GuiActionOutcome Unconfirmed(bool clickEmitted, string detail)
        {
            return new GuiActionOutcome(GuiActionOutcomeStatus.Unconfirmed, null, clickEmitted, detail);
        }

        static GuiActionOutcome ConvertInputFailure(GuiInputResult result)
        {
            return result.Status switch
            {
                GuiInputStatus.Disabled => Blocked(GuiSafetyReason.Disabled, false, result.Detail ?? "The configured GUI input is disabled."),
                GuiInputStatus.WindowNotActive => Blocked(GuiSafetyReason.WindowNotActive, false, result.Detail),
                GuiInputStatus.WindowChanged => Blocked(GuiSafetyReason.WindowChanged, false, result.Detail),
                GuiInputStatus.OutsideWindow => Blocked(GuiSafetyReason.TargetUnverified, false, result.Detail),
                GuiInputStatus.Canceled => Interrupted(false, result.Detail ?? "The input operation was canceled."),
                GuiInputStatus.TimedOut => Failed(false, result.Detail ?? "The input operation timed out."),
                _ => Failed(false, result.Detail ?? "The input operation failed."),
            };
        }
    }

    private static bool TryGetDesktopPoint(WindowLocalPixels point, ClientAreaGeometry geometry, out DesktopPixels desktopPoint)
    {
        long x = (long)geometry.OriginDesktop.X + point.X - geometry.OriginInWindow.X;
        long y = (long)geometry.OriginDesktop.Y + point.Y - geometry.OriginInWindow.Y;

        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
        {
            desktopPoint = default;
            return false;
        }

        desktopPoint = new DesktopPixels((int)x, (int)y);
        return true;
    }
}
