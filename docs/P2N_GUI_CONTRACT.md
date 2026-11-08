# P2N GUI action contract

This document records the guard, one-action, and correlation boundaries added in P2N-E2. The executor is not
registered by a module or host, and this work does not provide a gameplay trigger.

## Readiness and input

`GuiActionGate` is a pure classifier. Its first failing check is returned in this order: enabled configuration, X11
availability, window existence and process identity, active window state, capture result, usable scene, consistency of
the window observations around capture, calibration status and identity, target status and identity, connection, map,
cell, position provenance, movement ambiguity, snapshot epoch and revision, intention expiry, fight state, busy state,
player interruption, and protocol contradiction.

An allowed `GuiReadiness` is bound to the exact intention identifier, session epoch, state revision, window process
identity, map, calibration profile, and target evidence. It is a process-local binding check, not a cryptographic token;
the executor evaluates a new readiness for each submitted intention and accepts no readiness from a caller.

`GuiActionExecutor` consumes every submitted intention, including blocked and failed submissions. It permits one active
intention per executor instance and invokes the input seam at most once. `GuiInputStatus.Sent` records one click; only
the confirmation seam can then return `Pass`, and only for the expected server outcome after checking the compatible
client request. `NeverConfirms` is the default no-confirmation implementation, so a click without matching evidence
ends as `Unconfirmed` and cannot be retried with the same identifier.

The current application mode is `Preview`. The executor rejects real input implementations while mode is `Preview` or
`AllowRealInput` is false. `DisabledGuiInput` is also disabled at the interface boundary. A test fake may model a click
without claiming to emit real desktop input. No host or DI registration connects an executor to a client.

## Correlation model

`D3B.Bot.Gui.GuiIntentCorrelation` is an isolated state model and does not modify or integrate with `BotActor`,
`PlayerActions`, or `GameSession`.

```text
Registered -> Acknowledged -> Clicked -> ClientRequestSeen -> ServerConfirmed -> Consumed
```

Registration records the immutable world snapshot and window before input. Acknowledgement and the pre-click step
recheck session epoch, revision, map, relevant snapshot state, window identity, mapping, focus, activity, and geometry.
The model allows one intention at a time and never reuses an identifier. An expired intention is canceled before a
click; absent confirmation becomes `Unconfirmed` with no restart path.

The neutral client action values map in a future `D3B.Bot` integration to the observed client movement request or
interactive-element use request. The neutral server outcome values map there to movement confirmation or element-use
completion observations. This project does not reference `D3B.Protocol` for those mappings.

An unexpected client request interrupts correlation and sets player priority. A compatible packet does not prove its
source. If simultaneous human input is reported, the model stops as ambiguous; identical synthetic and human clicks
cannot be distinguished cryptographically from packet fields alone. This is a known observability limit, not a claim
that packet matching attributes the action to the bot.

## Verification boundary

The tests use synthetic world snapshots, windows, targets, input seams, and confirmation seams. They do not launch
Dofus, connect to a game server, register the executor with DI, or establish real-client acceptance.
