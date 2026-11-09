# P2O first supervised live action runbook

**Status: BLOCKED. No GO is issued by this runbook.** A future action requires separate explicit authorization for
observation and, after review, a separate explicit GO for exactly one action. Keep an operator present throughout. No
automatic loop, route, retry, harvesting, or second action is in scope.

The current host reports `SupervisedOneShotAvailable=false` and has no registered GUI executor or real-input service.
This remains true for the GUI `WalkToCell` path. Separately, the protocol `UseElement` path is implemented offline and
opt-in through `SupervisedElementUseConsole`; it is not a GUI one-shot, is disabled by default, and has no live GO. Its
observations and its separate one-action authorization are governed by
[P2O_ELEMENT_USE_RUNBOOK.md](P2O_ELEMENT_USE_RUNBOOK.md). An owner GO alone cannot activate the missing GUI capabilities.

## 1. Observation-only authorization and preflight

Obtain an explicit GO for an observation-only phase. It authorizes no input. Keep the client closed while preparing the
private output directory and checking the exact Ankama configuration root selected by the operator. Keep all manifests,
backups, captures, templates and reports outside the repository in private storage. The preflight tool creates or
requires a private `0700` output directory and writes manifest files with mode `0600`; use a private parent directory.

Set these variables in the same shell, using the exact operator-confirmed configuration root and a private output
location outside that tree and the repository:

```sh
ANKAMA_CONFIG_ROOT="/absolute/path/to/operator-confirmed/Ankama/config"
PRIVATE_DIR="/absolute/path/to/private/p2o-run"

/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- snapshot --root "$ANKAMA_CONFIG_ROOT" --output "$PRIVATE_DIR/ankama-before.json"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$PRIVATE_DIR/ankama-before.json"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- backup --root "$ANKAMA_CONFIG_ROOT" --output "$PRIVATE_DIR/ankama-backup"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$PRIVATE_DIR/ankama-before.json"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$PRIVATE_DIR/ankama-backup/manifest.json"
```

Require successful snapshot, verify and backup results. Inspect the manifest comparison and verify the private backup
before launch. A missing, invalid, unreadable or mismatching result means **BLOCKED**. The manifest verifies entry
paths, regular-file bytes, entry kinds, modes and symbolic-link targets; it intentionally omits directory entries,
empty directories and directory modes. If the required restoration boundary includes those directory properties,
record their separate verification or remain **BLOCKED**.

Before launch, confirm the exact client build, selected X11 session and the operator's authority to observe that
client. After launch in the authorized observation-only phase, select one numeric XID explicitly and verify its
expected executable identity. Do not change Ankama files to make preflight pass. If launch or a subsequent process
changes configuration, compare after the client and host are closed and restore from the verified backup.

## 2. Observation-only live evidence

With no real input available, an operator must independently observe and record:

- the live server map and cell, with fresh `ServerPlaced` or `ServerConfirmed` provenance;
- a complete protocol snapshot and an open movement translation latch through the evidence window; every unread frame
  must match the exact client build and direction in `NonPositionMessages` and pass that build's schema validation;
- the selected window's verified process identity, XID, mapped/viewable/active/focused state, and stable geometry;
- a real calibration profile measured for this client view and current scene, no older than 30 seconds at action time,
  with at least eight distributed fit anchors and three independent validation anchors; synthetic fixtures do not
  qualify;
- zoom and scene stability, including changes that preserve XID and dimensions;
- the target cell, its hitbox, UI exclusions, and the absence of player interruption, dialogs, fight, walking, element
  use, or unexpected client gameplay.

Record provenance, timestamps, build identity, window identity and calibration identity in the private report. Stop
with **BLOCKED** if any item is stale, unknown, partial, contradictory or unverified. An atlas or fingerprint cell is
not a live server observation. A closed latch is an unconditional stop; GUI mode cannot override it.

If a separate protocol `UseElement` observation phase is authorized, its console may be enabled with
`Supervised:ElementUse:Enabled=true` and `Supervised:ElementUse:ActionEnabled=false`; only `p2j element readiness` may be
issued. `p2j element use` is refused by the action gate and must never be called during observation-only.

Review the observation report before considering an action. This phase does not authorize a click.

## 3. Separate GO for exactly one `WalkToCell`

After observation review, obtain a second explicit GO naming the exact client/build, server-proven map, current cell,
destination cell and the single `WalkToCell` action. The authorization permits at most one click attempt. Require an
operator able to stop the process and a reviewed caller that can register and acknowledge one intent. The current host
cannot supply this supervised one-shot, so this section remains unavailable until that implementation boundary is
closed and independently reviewed.

This GUI one-shot is distinct from the protocol `UseElement` path. The protocol path is not a substitute for this GUI
click contract and is governed by [P2O_ELEMENT_USE_RUNBOOK.md](P2O_ELEMENT_USE_RUNBOOK.md).

For the single authorized attempt:

1. Recheck map, cell, session epoch, state revision, translation latch, window, calibration, target and exclusions.
   Require one reachable same-map `CellPathfinder` plan from the current server-proven cell to the exact authorized
   target cell.
2. Create an expiring intent bound to that state, window and calibration. Register it with `BotActor`; wait for its
   acknowledgement. Abort on refusal, expiry or any changed observation.
3. Recheck the actor snapshot and window at the final `MarkClicked` gate. Permit at most one click.
4. Observe the compatible client `MapMovementRequest`, the server's self `MapMovementEvent` for the same path and
   endpoint, and the final `MapMovementConfirmResponse` that establishes the target cell with
   `ServerConfirmed` provenance.
5. Report `PASS` only after the complete ordered evidence chain. A click, animation, request, partial response or
   movement event alone is never success.

Apply the outcome rules in [P2O_ACTION_CONTRACT.md](P2O_ACTION_CONTRACT.md). Any server refusal, out-of-order or
contradictory event, human-input ambiguity, timeout, state/window change or operator stop is terminal. After a click,
report `UNCONFIRMED` when evidence is missing and `INTERRUPTED` when player control, cancellation or window loss ends
the attempt. `BLOCKED` applies before input; `FAILED` applies only when failure is known to precede a possible click.
Never retry and never issue a second click under the same GO.

## 4. Stop, reconcile and restore

On any terminal result, stop input and all further action. Reconcile position from authoritative protocol observations
under operator supervision. Do not claim the action was reversible or that the original game state was restored; a
click may already have caused a server action even if confirmation is missing. Close the client and host before
configuration comparison or restoration.

```sh
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- compare --root "$ANKAMA_CONFIG_ROOT" --before "$PRIVATE_DIR/ankama-before.json" --after-output "$PRIVATE_DIR/ankama-after.json"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- restore --backup "$PRIVATE_DIR/ankama-backup" --manifest "$PRIVATE_DIR/ankama-backup/manifest.json" --root "$ANKAMA_CONFIG_ROOT"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$PRIVATE_DIR/ankama-before.json"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$PRIVATE_DIR/ankama-backup/manifest.json"
```

Compare after close, then restore from the verified backup and verify the restored tree, even when the comparison found
a change. A mismatch keeps the action **BLOCKED**. Missing or invalid comparison, restore or verification evidence is
also **BLOCKED** and requires manual inspection; do not claim successful restoration. Keep evidence private with
restrictive permissions. Never commit captures, templates, secrets, client data or private reports to Git.

## Evidence labels

- **VERIFIED:** offline implementation, audit and parent test results recorded in
  [P2O_OFFLINE_REPORT.md](P2O_OFFLINE_REPORT.md).
- **OPEN:** every live observation listed in section 2 and the operator's separate one-action authorization.
- **BLOCKED:** the current readiness result. No live GO or real-client action is recorded.
- The protocol `UseElement` path is separately offline-validated and opt-in; its live action remains BLOCKED and is
  governed by [P2O_ELEMENT_USE_RUNBOOK.md](P2O_ELEMENT_USE_RUNBOOK.md). The GUI `WalkToCell` labels above are unchanged.
