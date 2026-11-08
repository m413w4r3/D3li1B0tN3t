# P2O supervised one-action plan

**Status: future plan only. No real capture, calibration, or action was performed under this document.**

The first later GO may authorize real-client capture and calibration only. A separate GO is required before the first
GUI action. The current P2N result is BLOCKED pending final independent audits.

## Preconditions

1. Complete the final independent P2N audits and record their outcomes. Resolve any finding that affects identity,
   input gating, snapshot consistency, or confirmation before requesting a real-client GO.
2. Obtain an explicit GO for one real-client capture and calibration. That GO does not authorize input.
3. Use an Xorg/X11 session with valid DISPLAY and XTEST. Select one numeric XID explicitly. Confirm that the current
   process identity is Verified and that the expected executable matches the approved Dofus client.
4. Confirm the exact client build and map from authorized observations. Keep captures, templates, annotations, and
   reports in a private user directory with restrictive permissions; do not place them in the repository.
5. Use at least eight distributed fit anchors and three independent validation anchors. Keep the JSON, capture,
   dimensions, measured client geometry, and profile identity bound to that one client view. Record the measured
   residuals and thresholds. The profile must be no older than 30 seconds at action time.
6. Review and accept the real capture and calibration result before requesting any action GO. A synthetic result
   cannot substitute for this step.

## Separate GO for one action

After the capture/calibration-only phase has ended, request a distinct GO naming exactly one action: WalkToCell or
UseElement. Keep an operator present and able to stop the process throughout.

For that one intent:

1. Build the immutable protocol snapshot and bind its SessionEpoch, StateRevision, map, target, window identity, and
   calibration identity into the expiring intent.
2. Register the intent with the BotActor correlation inbox and wait for acknowledgement before input.
3. Re-read the snapshot and window, validate the gate, calibration, target, focus, active state, and geometry. Abort if
   any required value changed or is unknown.
4. Emit at most one XTest click. Revalidate the expected focused window immediately before the global click.
5. Match one compatible client-originated request by action and observable map/cell/element fields.
6. Wait for the matching server outcome. Only the specified confirmation can produce PASS and consume the intent.
   Missing or contradictory evidence ends UNCONFIRMED.

The correlation contract cannot prove whether an identical human click and synthesized click occurred simultaneously.
Any observation of concurrent input is an interruption, restores player priority, and stops this one-action plan.

## Abort conditions

Abort before input if the session is not X11, DISPLAY or XTEST is unavailable, the XID or verified executable changes,
the window is not active/focused, geometry or capture dimensions change, the calibration is invalid or older than
30 seconds, the target is unverified/ambiguous, the map/state revision/epoch changes, protocol status is unknown,
partial, contradictory, or translation-blocked, position is not server-placed/server-confirmed, the player pauses,
or the character is fighting, walking, using an element, or in a dialog.

Abort after input on an unexpected client request, simultaneous human input, map/session change, contradiction,
missing confirmation, timeout, or operator stop. Do not reissue the intent or click.

## Stop and rollback

Before a click, cancel the intent and release the GUI resources. After a click, mark the outcome UNCONFIRMED unless the
expected client and server evidence both arrive. Stop all further GUI input and do not retry. Reconcile the character
state from authoritative observations under the operator's supervision.

A GUI action may not be reversible: a click can already have caused a server action even when its acknowledgement is
missing. Rollback means stopping and reconciling; it does not mean clicking again or claiming the game state was
restored.

## Prohibited during this plan

- More than one action, any route, loop, retry, automatic harvest, combat, bank, or map transition sequence.
- Protocol gameplay injection, changes to a mapping, client redirection, an unattended host, or an automatic trigger.
- Treating a title, animation, preview, synthetic fixture, or matching client packet alone as action confirmation.
- Saving real client captures, assets, credentials, or private reports in the repository.
