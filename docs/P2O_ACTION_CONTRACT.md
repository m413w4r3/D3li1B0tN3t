# P2O WalkToCell action contract

**Status — offline contract only.** `WalkToCell` is the only action eligible for this contract. The actor inbox rejects
other GUI intent kinds. `UseElement` remains deferred; its current intent shape is not a live authorization.

**VERIFIED:** the contracts and deterministic offline checks are present at implementation base `3ebf635`. **OPEN:**
no real-client input or action-time evidence establishes that this offline contract has passed in a live session.

## Preconditions and intent binding

One caller supplies a reachable `WalkToCellPlan` on the current map. Its start cell must equal the complete,
server-proven current cell; every path step must belong to that map; the planned endpoint must equal the verified
visual target cell. The planner is the existing `CellPathfinder` path through `WalkToCellPlanner`. The coordinator
does not choose another map, infer a route, or continue into a second action.

One immutable intent binds:

- a non-empty unique `IntentId`;
- `ActionKind=WalkToCell`, `ExpectedClientAction=Walk`, and
  `ExpectedServerOutcome=MovementConfirmed`;
- `SessionEpoch`, `StateRevision`, exact `MapId`, and destination `TargetCellId`;
- the verified X11 window identity and exact calibration identity;
- creation time and short expiry. Production coordinator defaults are a 2-second intent lifetime, 2-second maximum
  protocol snapshot age, and 5-second confirmation timeout. The configured intent lifetime may not exceed 5 seconds.

Only one intent may be active for the actor, and each session epoch permits at most one GUI click attempt. Registration
and actor acknowledgement must both succeed before input. The final `MarkClicked` recheck compares the current actor
snapshot and window with the registered intent and enforces expiry. It opens the one-click gate once. The executor
consumes the identifier once and never retries. A launch that may have emitted a click remains terminal even if the
input command later fails or times out.

## Required proof for `PASS`

The actor must observe this ordered chain, all bound to the same session epoch, map and intended endpoint:

1. One compatible client-originated `MapMovementRequest` whose map and decoded endpoint match the intent.
2. The server's self `MapMovementEvent`, after that request, with a valid path beginning at the pending start cell and
   ending at the intended target cell on the intended map.
3. The final `MapMovementConfirmResponse`, after the matching request and self movement event, leaving
   `CharacterState` at the intended cell with `CellProvenance.ServerConfirmed`.

The actor treats each step as separate evidence. A click, a running animation, a client request, a partial path, a
movement event without the final response, or the final response without the preceding matching evidence never yields
`PASS`. A server refusal, wrong map, wrong endpoint, out-of-order event, contradictory or partial state, unknown
translation, or timeout ends without success.

## Terminal outcomes

| Outcome | Contract meaning |
| --- | --- |
| `PASS` | Exactly one click was emitted and the complete client-request, self-movement and server-confirmation chain established the target cell with `ServerConfirmed` provenance. |
| `BLOCKED` | A pre-click safety condition, stale/changed snapshot, invalid plan, disabled input policy, registration refusal, or final window/state check refused the attempt. No click is authorized. |
| `UNCONFIRMED` | A click may have been emitted, but the complete confirmation chain did not arrive, timed out, was refused, or contradicted the intent. |
| `INTERRUPTED` | Player control, caller cancellation, actor revocation, session change, or window loss interrupted the attempt. After the click gate, treat the click as possibly emitted and do not retry. |
| `FAILED` | An input or coordinator failure occurred before the click could have been emitted. |

Before the click gate, expiry or a failed safety check blocks the attempt; caller cancellation interrupts it. After the click
gate, missing confirmation is `UNCONFIRMED`, while player interruption, cancellation, or window loss is
`INTERRUPTED`. A server refusal is never `PASS`. There is no retry, second click, or automatic fallback after any
terminal status. Stop and reconcile from authoritative observations.

## Attribution limit

The protocol fields cannot distinguish an identical human click that occurs simultaneously with the synthesized click.
Detected human-input ambiguity interrupts the attempt. When the two inputs are observationally identical, no perfect
attribution claim is possible; this is an accepted documented residual, not proof that the action was caused by the
automation.

## Source anchors

Verified implementation at `3ebf635`: `src/D3B.Gui/Actions/GuiActionIntent.cs:6-107`;
`src/D3B.Bot/Gui/GuiWalkToCellCoordinatorOptions.cs:1-50`; `src/D3B.Bot/Gui/GuiWalkToCellCoordinator.cs:27-28,92-105,172-215,224-356`;
`src/D3B.Bot/Runtime/BotActor.cs:875-965,1503-1644`;
`src/D3B.Bot/Gui/GuiIntentCorrelation.cs:147-265,340-409`;
`src/D3B.Gui/Actions/GuiActionExecutor.cs:11-14,70-153`;
`src/D3B.Gui/Actions/GuiActionOutcomeStatus.cs:1-18`.
