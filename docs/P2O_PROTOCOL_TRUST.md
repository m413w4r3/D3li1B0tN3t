# P2O protocol and GUI trust policy

## Two separate authorization risks

P2O has two different ways an action could be unsafe, and they require separate decisions:

- **Network injection:** D3B could send a gameplay request from incomplete, stale, or contradictory protocol state. The
  `GameSession.SendUnguardedAsync` and `InjectedFrames.Drain` checks enforce the `GameInputMode` admission boundary.
  `GuiClientInput` refuses D3B-originated gameplay; the production gameplay allowlist is empty. Client traffic continues
  through the relay unchanged.
- **GUI input:** a synthesized click could hit the wrong target or use a scene or protocol snapshot that has gone
  stale. `GuiProtocolTrustPolicy` evaluates this separate evidence. A network-injection authorization does not authorize
  a click, and a GUI decision does not authorize a D3B-originated request.

The GUI policy is a pure decision component. It calls `GuiActionGate` itself and requires `Ready`; it does not accept a
caller-supplied readiness result. It accepts only a first `WalkToCell` intent whose expected client action is `Walk` and
whose expected server outcome is `MovementConfirmed`. A blocked decision reports one first failing `GuiSafetyReason`.

## WalkToCell proof required

The policy allows a click only when all of these proofs hold in its fixed evaluation order:

1. The action is `WalkToCell`, with the `Walk` and `MovementConfirmed` contract, and the existing GUI gate returns
   `Ready` for the same intent, end snapshot, window, captured frame, calibration, and target.
2. Start and end protocol snapshots agree on session epoch, state revision, map, cell, provenance, connection, fight,
   walking, using, dialog, movement ambiguity, protocol status, player interruption, and known elements. Snapshot
   timestamps may advance, but an intervening state change may not.
3. Both snapshots are complete. The map and cell are known, cell provenance is `ServerPlaced` or `ServerConfirmed`,
   the session is connected, and the character is not paused, fighting, walking, using an element, in a dialog, or in
   ambiguous movement.
4. The movement translation latch stayed open and no unread message outside the proven safe classification was
   observed. Any closed latch or unknown message blocks the action.
5. No client gameplay or detected human-input ambiguity occurred during the evidence window. This interrupts the
   action rather than trying to infer who caused the input.
6. The end snapshot is not from the future and its age is within `MaxSnapshotAge`; the start snapshot is no newer than
   the end snapshot. The intent must also remain valid under `GuiActionGate`'s clock.
7. The start and end window observations have verified process identity and remain mapped, viewable, focused, active,
   and geometrically identical. The capture's frame-bound observations must also pass the existing gate.
8. Calibration is current, bound to the intent's map and exact calibration identity, verified window, and frame size.
   Synthetic calibration is rejected unless `AllowSyntheticCalibration` is explicitly enabled by a test.
9. The target is verified and unambiguous, and is bound to the exact frame capture, calibration, map, and target cell.
   Its proposed click point must lie inside the calibrated safe client area. There is no fallback point.
10. Plan start and target cells are required and must match the server-proven current cell and intent cell. If the
    evidence includes a reachability result, it must say reachable.

`GuiWorldSnapshot.IsUserInterrupted` carries the actor's pause/player-took-control state into this decision. A change in
that flag between the snapshots is itself a state change.

## The translation latch remains authoritative

The only safe undecoded-message case is a frame that the existing `NonPositionMessages` allowlist proves non-position
for the exact client build and direction, with its payload fully validated by that build's schema. Those messages may
leave the translation latch open. The allowlist is build scoped and direction scoped. A different build, side, type, or
payload shape does not qualify. Any other unread frame closes the latch and blocks movement and GUI trust.

The GUI policy requires the latch to have remained open. The shortcut `if (GuiClientInput) ignoreTranslationLatch` is
forbidden: `GuiClientInput` only controls which D3B gameplay requests may be admitted, and it does not translate an
unknown frame or repair incomplete world state. P2O cannot overcome the latch; it can only observe an open latch and
complete evidence. A closed latch is `ProtocolContradiction` and blocks the click.

## Human input attribution

Detected concurrent human input or an unexpected client gameplay request interrupts the intent. Identical simultaneous
human and synthesized clicks may be indistinguishable to the observer. The policy therefore makes no perfect attribution
claim; if available evidence indicates ambiguity, it fails closed.

## Offline evidence and remaining trust gap

Synthetic tests prove the policy's deterministic decisions for supplied evidence. They do not prove that a live server
provided the map and cell, that calibration describes the actual client at action time, that a latch stayed open in a
live session, or that the localized visual target is the intended game cell.

In particular, the current window binding does not detect every zoom or scene change when the XID and dimensions remain
unchanged. A stale calibration or a visually plausible target after such a change remains a trust gap. The policy cannot
claim the scene is stable from window identity and dimensions alone.

A future **MANUAL/SUPERVISED GO** must independently establish all of the following at the action time:

- The live server's map and cell are complete and have accepted server provenance.
- Calibration is real, fresh, and measured against the current client scene.
- The translation latch stays open through the observation window, with any undecoded frame accounted for by the exact
  build and direction allowlist and schema validation.
- Zoom and scene stability are checked independently, including changes that keep the same window identity and
  dimensions.
- The window, target, character readiness, and absence of unexpected client gameplay are observed independently of the
  policy's own inputs.
- An independent audit confirms the recorded evidence and result. Any ambiguity or unaccounted state change means
  **BLOCKED**, not GO.

No live client action or supervised acceptance is claimed by this document or by the offline policy tests.
