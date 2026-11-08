# P2N GUI contracts

This page records the current data contracts and the future supervised action boundary. The P2N application mode is
Preview. The executor, snapshot adapter, correlation model, and input policy are not connected to a host gameplay loop.

## Coordinate spaces

| Type | Meaning | Invariant |
| --- | --- | --- |
| WindowLocalPixels | Pixel coordinates in one CapturedFrame image. | Every target point and rectangle is bound to that frame and its calibration. |
| ClientAreaPixels | Pixel offset from the selected X11 window origin to the measured client area. | Used by ClientAreaGeometry; not interchangeable with desktop coordinates. |
| DesktopPixels | Global coordinates on the X11 desktop. | Required by xdotool input. A local point is translated only through the measured client geometry. |
| LogicalCell | Dofus map cell identifier. | It is not a pixel coordinate; its MapId is carried separately. |
| WindowLocalRect | Rectangle in captured-frame pixels. | It does not name an X11 client rectangle or desktop rectangle. |

These are distinct C# types with no implicit conversions. The measured geometry stores the client-area desktop origin,
its origin within the captured window, and its width and height. A local point must never be passed as a desktop point.
The X11 backend currently measures the selected XID and its X11 border. Decoration extents outside that XID are not
separately measured; see P2N_LINUX_BACKEND.md.

## Window identity

GameWindowIdentity carries Xid, optional ProcessId, optional ExecutablePath, descriptive Title, and Status.

- `TemplateTargetLocator` uses a permissive preview comparison: Xid must match; ProcessId and ExecutablePath are
  compared only when both identities provide the corresponding value. It ignores Title and Status. This rule is for
  preview target matching only.
- Action readiness is strict. Both identities being compared must have Status=Verified, and Xid, ProcessId, and
  ExecutablePath must all match exactly. An identity marked unverified, including one with unavailable process data,
  blocks readiness; an unverified calibration profile also blocks it. Title is never evidence and is not part of the
  comparison.
- An explicit XID with unavailable process identity can still be inspected and captured for Preview; it cannot pass the
  action gate.

The permissive preview matcher rule is not used by `GuiActionGate`. Calibration profile revalidation also requires
verified, exact Xid, ProcessId, and ExecutablePath equality; a missing process value invalidates the profile.

## CapturedFrame and CaptureStatus

CapturedFrame binds a unique CaptureId, WindowIdentity, start and completion timestamps, before and after
GameWindowSnapshot values, decoded width and height, width and height differences from the selected X11 window, an
owned OpenCV Mat, CaptureStatus, a bounded failure description, and ContentUsable.

| CaptureStatus | Meaning |
| --- | --- |
| Captured | PNG decoded, dimensions matched the selected-XID extent, and basic image-content checks passed. It does not mean a game scene was recognized. |
| WindowNotFound | The explicit XID was not found. |
| WindowHidden | The XID is unmapped, unviewable, or minimized. |
| WindowChanged | Identity, geometry, or mapped state changed during capture. |
| TimedOut | The maim process exceeded its configured bound. |
| OutputTooLarge | PNG stdout exceeded the byte limit. |
| BinaryUnavailable | The configured maim executable could not be used. |
| DecodeFailed | The stdout did not decode as PNG. |
| InvalidDimensions | The image was empty, exceeded the configured limits, differed from measured geometry, or had non-positive dimensions. |
| EmptyContent | The decoded image was constant or at least 99.5% near-black. |
| Failed | Another capture or X11 preflight error occurred. |

Capture runs in memory and does not save the source image by default. CaptureStatus.Captured is a transport and pixel
check only; it is not scene, map, UI, or resource recognition.

For capture stability, `MaimWindowCapture` compares XID, ProcessId, ExecutablePath, Status, and window geometry exactly
between the before and after snapshots. A metadata availability or status change during capture therefore produces
WindowChanged. The action gate independently requires verified identities and exact Xid, ProcessId, and
ExecutablePath equality as described above.

## Calibration

CalibrationAnchor associates a LogicalCell and its MapGeometry point with a measured WindowLocalPixels point, a Fit
or Validation role, and ManualAnnotation or SyntheticFixture provenance. Loaded JSON points are manual annotations,
even when the document is marked synthetic. CalibrationAnchorDocument uses format version 1.

CalibrationProfile binds client build, map, window, capture dimensions, measured client geometry, scale, calibration
version, affine mapper, anchors, independent validation residuals, creation time, synthetic label, and a stable
CalibrationId. CalibrationResult carries status, optional profile, metrics, and problems.

The affine model is:

    screenX = a * mapX + b * mapY + tx
    screenY = c * mapX + d * mapY + ty

Fit and invalidation requirements, the seeded OpenCV RANSAC settings, minimum counts, initial pixel thresholds, and
synthetic metrics are recorded in P2N_CALIBRATION.md. The maximum profile age is 30 seconds; map, build, XID, known
process identity, resolution, scale, client geometry, calibration version, or contradictory scene evidence can make
a profile stale or invalid.

The preview JSON report records aggregate errors and inlier ratio plus each fit-anchor residual, each independent
validation error, rejected fit-anchor indexes in the source document, and the affine coefficients `a`, `b`, `tx`, `c`,
`d`, and `ty`.

The gate checks that a profile is calibrated, fresh, bound to the current map/window and capture dimensions, and
consistent with measured client geometry. The standalone `CalibrationValidator.ValidateProfile` compares stored and
current XID, ProcessId, and ExecutablePath exactly; a missing process value on either side therefore invalidates that
profile. The environment supplied to that validator has no current client-build field, although the profile records
the build. Synthetic calibration does not establish a Unity calibration.

## TargetRegion and TargetConfidence

TargetRegion carries MapId, ElementId, Cell, GfxId, optional ExpectedSkillId, projected cell center, optional visual
bounds, optional proposed click point, confidence evidence, status, CaptureId, and CalibrationId. Its coordinates
belong to that exact CapturedFrame and profile.

TemplateTargetLocator uses normalized OpenCV template correlation in a bounded region around the projected cell.
Initial requirements are score at least 0.88, independent-peak margin at least 0.10, search radius 96 pixels, maximum
candidate-center distance 64 pixels from the projected cell, and an 8-pixel frame-edge margin. Configured exclusion
rectangles and known other element cells are checked. Verified proposes the candidate center only after all checks
pass. Ambiguous records competing peaks or known-cell overlap; Unverified records missing or failed evidence. There
is no fallback click at the projected cell center.

These initial thresholds are not universal safety guarantees. No Dofus resource imagery was used to establish them.

## GuiWorldSnapshot

GuiWorldSnapshot is immutable and contains:

- SessionEpoch and StateRevision, the session and state versions used to bind an intention;
- CapturedAt, MapId, CellId, and CellProvenance;
- IsConnected, IsFighting, IsWalking, IsUsing, and IsInDialog;
- HasAmbiguousMovement and the known logical elements with their skills/state;
- ProtocolObservationStatus; and
- IsUserInterrupted, populated from the actor's pause state.

GuiWorldSnapshotAdapter reads the atomically published BotActor view. It includes the movement-translation latch in
the snapshot's protocol status and checks the latch around the view read so a racing latch closure fails closed.
Unknown, partial, contradictory, and translation-blocked observations do not count as positive movement evidence.
StateRevision combines the published view revision with the translation-latch state, so either latch transition changes
the revision and invalidates intentions bound to the previous state. The adapter currently exists as a contract; it is
not called by a gameplay trigger.

## GuiOptions and bounds

| Option | Default | Validation bound or rule |
| --- | --- | --- |
| Gui:Enabled | false | Capture/preview is disabled unless explicitly enabled. |
| Gui:Backend | X11 | X11 is the only defined backend. |
| Gui:Mode | Preview | Preview is the only defined mode. |
| Gui:AllowRealInput | false | true is rejected by GuiOptionsValidator in P2N. |
| Gui:CaptureTimeoutMs | 2000 | 100–2000 ms. |
| Gui:MaxCaptureBytes | 16777216 | 1048576–16777216 bytes. |
| Gui:MaxWidth | 3840 | 1–3840 pixels. |
| Gui:MaxHeight | 2160 | 1–2160 pixels. |
| Gui:CalibrationMaxAgeSeconds | 30 | 1–30 seconds. |

The options validator is a pure validation component. `GuiActionGate` calls `GuiOptionsValidator.ValidateOrThrow` when
it is constructed, so invalid options fail at composition. P2O must also validate the options when the host starts. The
host does not bind or register the GUI feature, so these are contract defaults and limits rather than an active host
configuration.

## GameInputMode and GuiClientInput

GameInputMode.ProtocolInjection preserves the existing protocol-injection policy. GameInputMode.GuiClientInput models
the future rule that client-originated requests may pass while D3B-originated gameplay requests are refused;
D3B-originated non-gameplay requests are allowed only when the classifier marks them as such. Unknown origins fail
closed.

GuiInputModePolicy and IGameInputRequestClassifier implement this as a policy seam. A fake-relay integration test
verifies unchanged client/server forwarding, refusal of D3B gameplay requests, admission of client requests and
D3B non-gameplay requests, and zero XTest input. Production GameSession admission is still not wired to this policy;
that integration remains P2O work. The latch and existing network protections are not changed by P2N. A future real GUI
action must not be enabled until the production admission boundary is implemented and separately audited.

## GuiActionIntent and GuiActionOutcome

GuiActionIntent is one expiring, single-map request containing IntentId, ActionKind, SessionEpoch, StateRevision,
MapId, TargetCellId, optional TargetElementId, WindowIdentity, CalibrationIdentity, ExpectedClientAction,
ExpectedServerOutcome, CreatedAt, and ExpiresAt.

The only action kinds are WalkToCell and UseElement. Their neutral matching categories are Walk or UseElement, and
MovementConfirmed or ElementUseEnded. Banking, combat, zaaps, route planning across maps, and automatic harvesting
are outside this contract.

| GuiActionOutcomeStatus | Meaning |
| --- | --- |
| Pass | One click was reported and the compatible client request plus expected server outcome were confirmed. |
| Blocked | A safety gate or disabled-input rule rejected the intent before a click. |
| Failed | Input failed before a click command was launched. |
| Interrupted | Cancellation or interruption occurred. The outcome records whether a click may have been emitted. |
| Unconfirmed | A click may have happened, but matching confirmation was absent, late, or contradictory, or the launched click command failed uncertainly. |

A click by itself is never Pass. The P2N default confirmation implementation, NeverConfirms, does not confirm anything.
`GuiInputResult.ClickMayHaveBeenEmitted` records whether the click command was launched. A failure, cancellation, or
timeout after launch is converted to Unconfirmed with this flag true; the executor never retries it. Refusals and
failures before launch report no possible click. The executor consumes each submitted intent identifier and allows one
active intent per executor. If cancellation arrives after the input backend has reported Sent, while the executor is
waiting for confirmation, the outcome is Interrupted with the click recorded as emitted; this path is also terminal
and is not retried.

## GuiReadiness and GuiSafetyReason

GuiActionGate returns the first failing reason in a deterministic order. Ready means the supplied snapshot passed the
classifier's checks; it does not override Preview mode or authorize a click by itself.

| Reason | Condition |
| --- | --- |
| Disabled | Gui:Enabled is false. |
| NotX11 | Backend is not X11, XDG_SESSION_TYPE is not x11, DISPLAY is invalid, or XTEST is unavailable. |
| WindowNotFound | The selected window does not exist, has NotFound status, or has a non-positive XID. |
| WindowIdentityUnverified | The current window or calibration profile identity is not Verified. |
| WindowNotActive | The window is not mapped/viewable, is minimized, or is not both focused and active. |
| CaptureFailed | CaptureStatus is not Captured. |
| SceneUnverified | Image content is unusable, empty, dimensionally invalid, or exceeds configured dimensions. |
| WindowChanged | Geometry is unreliable; intent/frame identity differs; or before/after observations do not match the current window. |
| CalibrationStale | Calibration is marked stale, outside its allowed age, bound to a different window/map/profile/size, or disagrees with measured geometry. |
| NotCalibrated | No calibrated profile is available. |
| TargetUnverified | Target status or identity is unusable, it belongs to another frame/profile/map/action, has no proposed point, or the point is outside the safe client area. |
| AmbiguousTarget | The target has competing plausible candidates. |
| Disconnected | The protocol snapshot reports a disconnected game session. |
| MapUnknown | MapId is missing/different, or protocol observation status is Unknown. |
| CellUnknown | CellId is missing, target cell differs from the intent, or observation status is Partial. |
| PositionUntrusted | Cell provenance is not ServerPlaced or ServerConfirmed. |
| PositionAmbiguous | Movement evidence is ambiguous. |
| StateChanged | SessionEpoch or StateRevision differs from the intention. |
| IntentExpired | Current time is before CreatedAt or at/after ExpiresAt. |
| InFight | The character is in a fight. |
| Busy | The character is walking, using an element, or in a dialog. |
| UserInterrupted | The player has paused the bot or taken control. |
| ProtocolContradiction | Protocol observation is TranslationBlocked or Contradictory. |
| Ready | None of the preceding checks failed. |

The reason order follows GuiActionGate: configuration and X11; window identity and activity; capture and scene;
window consistency; calibration; target; connection, map, cell and position; state revision and intent expiry; fight,
busy and user interruption; protocol contradiction. A stale CalibrationStatus is reported before NotCalibrated.

## Non-transferable authorization binding

GuiReadiness binds an allowed result to the intent id, SessionEpoch, StateRevision, window identity, calibration id,
target identity, frame, map, and action target. Its AuthorizationId is process-local and is not a cryptographic token.
The executor computes readiness itself for each submission and accepts no caller-supplied readiness. IsBoundTo checks
that the readiness still refers to those exact arguments; it is not a substitute for a fresh gate evaluation.

The executor also refuses implementations that emit real input while Mode is Preview or AllowRealInput is false.
Preview is the sole defined P2N mode and GuiOptionsValidator rejects AllowRealInput=true. The host does not register
the executor or XdotoolGuiInput.

## Correlation and confirmation

The future one-intent correlation sequence is:

Registered → Acknowledged → Clicked → ClientRequestSeen → ServerConfirmed → Consumed

Registration happens before input. The acknowledgement and pre-click checks compare the session snapshot and window;
at most one click is recorded. A compatible client request is matched to action, map, cell, or element fields when
those values are observable. Only then can the expected server outcome complete the intent. Unexpected client
requests interrupt correlation and restore player priority. Expired or canceled intents are terminal.

Packet fields cannot identify whether an identical request came from a human click or a synthesized click made at the
same time. PotentiallyConcurrentHumanInput causes an interruption; the correlation contract does not claim
cryptographic attribution. Missing or conflicting confirmation yields Unconfirmed and no automatic retry. Visual
animation alone is not a server confirmation.

For WalkToCell, BotActor now feeds GuiIntentCorrelation from its existing decoded-frame inbox. It matches the initial
client MapMovementRequest by map and endpoint, requires the self MapMovementEvent and a matching
MapMovementConfirmResponse with server-confirmed position provenance, and rejects refusal or incomplete evidence. The
actor exposes a one-outcome waiter and one-time consumption; terminal uncertainty and expiry cannot produce success.
MapMovementConfirmRequest is the client's arrival acknowledgement, not the initial request used for correlation.

This bounded observer does not emit input, register a capture loop or gameplay trigger, or connect GuiActionExecutor to
BotActor. UseElement remains a contract-only correlation action. Packet correlation cannot identify whether an
indistinguishable request came from a human or a synthesized click.

## Security boundaries

- Preview reads local files and writes an annotated PNG and JSON report under a private directory. It does not capture
  a live window or send input.
- Both the preview output guard and writer walk upward from the destination and reject any path under a directory marked
  by `D3B.slnx`, regardless of the current working directory. CLI input errors expose only the file name and a stable
  diagnostic code, not the private path.
- The executor is not registered by the host or application, and no gameplay trigger or periodic capture exists.
- Xdotool input is tested only against fake Xvfb windows. No real Dofus click, movement, or harvest is claimed.
- There is no /dev/uinput access, root requirement, Linux capability, Frida use, Python inference runtime, or Unity
  process injection in this P2N implementation.
