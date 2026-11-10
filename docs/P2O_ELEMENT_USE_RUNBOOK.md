# P2O supervised UseElement runbook (protocol path)

**Status: BLOCKED. No GO is issued by this runbook.** This runbook covers the protocol `UseElement` path implemented
by `SupervisedElementUseSession` and `SupervisedElementUseConsole` (D345, D346). It is not the GUI `WalkToCell` path of
[P2O_FIRST_LIVE_ACTION_RUNBOOK.md](P2O_FIRST_LIVE_ACTION_RUNBOOK.md) and does not change the GUI action contract in
[P2O_ACTION_CONTRACT.md](P2O_ACTION_CONTRACT.md). Live action remains **BLOCKED** under D340. Two separate
authorizations are required: passive observation only, then an explicit one-action GO after review.

## 1. Two paths and two authorizations

| Path | Trigger | Status |
| --- | --- | --- |
| GUI `WalkToCell` | X11 click through the GUI coordinator | offline contract; host GUI one-shot unavailable; live BLOCKED |
| Protocol `UseElement` | `p2j element use` through `GameSession` | offline-validated; opt-in; live BLOCKED |

The protocol path sends at most one `InteractiveUseRequest` through the existing relay, never walks, never changes map
and never retries. It is not a GUI click and does not provide the GUI calibration, window or human-input evidence.

Two independent settings gate it, both false by default:

- `Supervised:ElementUse:Enabled` registers the console and both passive commands: `p2j element candidates` and
  `p2j element readiness`.
- `Supervised:ElementUse:ActionEnabled` permits the `p2j element use` action. Both passive commands remain
  available when this gate is closed.

The two authorizations are:

1. **Observation-only.** `Supervised:ElementUse:Enabled=true`, `Supervised:ElementUse:ActionEnabled=false`. Only
   `p2j element candidates` and `p2j element readiness ...` may be issued. `p2j element use ...` is refused by the
   action gate and must never be called. No gameplay command, no movement, no click, no activity.
2. **One-action GO.** After reviewing the observation evidence, a separate explicit owner GO names the exact session,
   build, map, cell, element, skill instance and expected item. Only then may `Supervised:ElementUse:ActionEnabled=true`
   be set for that host session; the host is restarted, exactly one `p2j element use ...` is sent, and no retry follows.

## 2. Observation-only phase

Preconditions: exact client build, private Ankama snapshot/verify/backup, relay and host started with the two settings
above, no other D3B command and no activity. Keep the operator present.

Allowed passive commands:

~~~text
p2j element candidates
~~~

It takes no ElementId and lists candidates on the current map with their server, pack, catalog, and labelled graphics
provenance. It never sends, moves, starts an activity, or consumes the one-shot. Its target identity verdict and action
availability verdict are independent; ACTION_BLOCKED is expected while the action gate remains closed. See
[P2O_ELEMENT_CANDIDATES_RUNBOOK.md](P2O_ELEMENT_CANDIDATES_RUNBOOK.md) for the full S2B protocol and private relevé
format.

Readiness is also passive:

```text
p2j element readiness <mapId> <cell> <elementId> <skillId>
```

It is passive: it reads the current `CharacterState`, map data, job/item tables and session latch and prints the
preparation. It sends nothing and does not consume the one attempt.

Record privately: map and cell with `ServerPlaced`/`ServerConfirmed` provenance; the element's `ElementId`, server cell
and `GfxId`; the enabled skill id and server instance; the expected item from `Skill.GatheredItemId`; the translation
latch state; the input mode. A closed latch, an unknown or ambiguous element, a missing skill instance or an unproven
item is a **BLOCKED** observation. Do not guess an `ElementId` from a name, a screenshot or a historical session.

Forbidden during observation-only: `p2j element use`, `Supervised:ElementUse:ActionEnabled=true`, any movement, any
click, any activity, any `HarvestActivity`, any second relay or planner. If the Walnut's identity cannot be established
passively, stop **BLOCKED** and propose a separate UI/protocol correlation experiment under its own GO.

## 3. One-action GO

After review, the GO names the exact map, cell, element, skill id, skill instance and expected item, the client/build
and the host session. Set `Supervised:ElementUse:ActionEnabled=true` only for that session (keep
`Supervised:ElementUse:Enabled=true`), restart the host, and issue exactly one:

```text
p2j element use <mapId> <cell> <elementId> <skillId>
```

The execution rechecks the session epoch, connection, `ProtocolInjection` admission, open translation latch, movement
guard, map/cell, element state, enabled skill and instance, the current cell's used-from range and the unconsumed
authorization inside the serialized behavior and again immediately before the single write. The attempt frontier opens
after the guarded write completes and is serialized with the bot inbox, after the observation barrier and final rechecks;
start and refusal events observed during the observation barrier or the write are ignored; the end must follow this
attempt's start; a refusal must name the exact skill instance. A genuine server response cannot precede the write.

Outcomes:

- `PASS_HARVEST` only with the matching self `InteractiveUsedEvent` after the frontier, the matching
  `InteractiveUseEndedEvent` after that start (when a duration is announced), and a final expected-item increase that
  still holds after the bounded settlement in the same session, map and cell.
- `REFUSED` only for an `InteractiveUseErrorEvent` naming the exact skill instance.
- `UNCONFIRMED` for missing, stale, ambiguous or uncertain evidence, including `ITEM_GAIN_NOT_PRESENT_AT_VERDICT`.
- `BLOCKED` for a local precondition, a closed action gate, a closed latch or a refused admission.
- `INTERRUPTED` for player, disconnect, fight, host stop or cancellation.

There is no retry, no second request, no movement and no automatic fallback. The first terminal verdict ends the GO.

## 4. Stop, reconcile and restore

Stop after the first terminal verdict. Reconcile position from authoritative protocol observations under operator
supervision. Close the client and host, compare and restore the Ankama configuration with the preflight scripts, verify
the restoration, set `Supervised:ElementUse:ActionEnabled=false`, and keep all evidence private. A failed or missing
comparison, restore or verification keeps the action **BLOCKED**.

## 5. Attribution limits

`InteractiveUseEndedEvent` carries no actor or attempt id. A matching end after this attempt's start could theoretically
belong to another use; the code requires the start-after-frontier ordering and an inventory increase, and any ambiguity
must remain `UNCONFIRMED`, never `PASS_HARVEST`.

An inventory increase does not prove its source. The live validation requires a quiet session: no other interaction,
trade, bank, drop, quest reward or object source during the proof window. Any concurrent source or unexplained change
makes the outcome `UNCONFIRMED`.

The server's skill-id field may be incomplete. A too-far refusal whose first error names the skill id instead of the
instance is ignored; the second error naming the instance can still prove `REFUSED`. Human input cannot be
distinguished from identical server events; keep an operator present.

## 6. Evidence labels

- **VERIFIED:** offline code, tests, build and format recorded in D346; the independent audit's initial REJECT and final
  delta **APPROVE** are recorded there.
- **OPEN:** the live observation and the separate one-action GO.
- **BLOCKED:** live action under D340. No GO is issued by this runbook.
