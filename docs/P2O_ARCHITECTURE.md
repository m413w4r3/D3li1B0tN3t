# P2O architecture and implementation boundaries

**Status — 2026-11-09, implementation base `3ebf635aecdc9ce632bbc898254a6d391035a255`:** offline integration is
complete and audited for the offline boundary. A supervised live action is **BLOCKED**. This document describes the
checked-in integration; it does not authorize a client launch or input.

## Responsibilities and dependencies

| Component | Responsibility and boundary |
| --- | --- |
| `D3B.Gui` | Owns Linux X11 window observation, `maim` capture, OpenCV calibration and target projection, the GUI action gate, XTest input and the executor contracts. It depends on `D3B.Engine` and `D3B.Data`; it has no dependency on Bot, Host, App, Network, or Protocol. |
| `D3B.Bot` | Owns `CharacterState`, one serialized `BotActor` inbox per character, protocol observations, GUI intent correlation, acknowledgement, revocation, waiters and the caller-invoked `WalkToCell` coordinator. It composes the existing planner with the GUI evidence contracts. |
| `D3B.Network` | Owns the transparent socket relay, frame boundaries, observation hooks and final typed admission for D3B-originated frames. Ordinary client/server traffic continues through the relay. |
| `D3B.Protocol` | Owns readable message definitions and build-specific translation between game frames and readable events or requests. It does not decide whether visual input is authorized. |
| `D3B.Diagnostic` and `rust/d3b-diagnostic` | Own the diagnostic sidecar contract and offline schema/replay analysis. They are diagnostic tools, not a gameplay controller or a second runtime. |
| `D3B.Engine` and `D3B.Data` | `D3B.Engine` supplies pure map/world algorithms, including `CellPathfinder`; `D3B.Data` supplies build data, maps and world data. The GUI coordinator uses these existing sources. |
| Harvesting | `HarvestActivity` and its regular module remain available in the existing bot composition. GUI host capability does not activate an activity, capture loop, or automatic harvesting trigger. |

The dependency direction is `D3B.Gui → D3B.Engine` and `D3B.Data`, then `D3B.Bot → D3B.Core`, `D3B.Gui`,
`D3B.Engine`, `D3B.Data`, `D3B.Network`, `D3B.Protocol`, and `D3B.Diagnostic`. `D3B.Storage` reaches Bot; the App
and Host reach the runtime through Storage. The GUI layer does not own mutable character state or a client connection.

P2O keeps one planner, one relay, and one vision pipeline. It adds no second planner, second relay, Python daemon, or
concurrent vision pipeline. Protocol and visual observations join at the existing BotActor coordinator boundary.

## Data and action flow

1. **Observe:** the official client and server exchange bytes through `D3B.Network`. `GameSession` translates observed
   game frames through `D3B.Protocol`; `Bots` forwards readable observations to the corresponding `BotActor`, which
   updates `CharacterState` and its published GUI world snapshot.
2. **Plan:** a caller supplies one same-map `WalkToCellPlan`, produced with the existing `CellPathfinder` and map data.
   The coordinator checks that its start is the current server-proven cell and that its endpoint matches the selected
   visual cell.
3. **Evaluate:** `GuiWalkToCellCoordinator` takes protocol snapshots around one X11 window observation and capture,
   validates calibration and target evidence, checks the safety gate and translation-latch evidence, and registers one
   expiring intent with the actor.
4. **Authorize the attempt:** the actor acknowledges the same session epoch, state revision, map, window and snapshot.
   The coordinator and actor recheck state and window immediately before the click gate. An actor revocation that wins
   the click lease prevents launch; a click whose launch claim wins is treated as possibly emitted.
5. **Observe and correlate:** a GUI click, when separately authorized and supplied by a caller, asks the official
   client to create its request. `BotActor` correlates that request with its server observations through the existing
   relay and protocol path. The click itself never becomes server truth.
6. **Finish:** only the full movement confirmation contract below yields `PASS`. Refusal, contradiction, interruption,
   expiry, or missing confirmation terminates the one attempt; it does not schedule another click.

Fail-closed boundaries apply before registration, at acknowledgement, at the final click gate, during observation, and
at terminal confirmation. Unknown or incomplete protocol state, a closed translation latch, a changed map/window,
stale calibration, an ambiguous target, an actor revocation, or an unexpected request prevents or terminates the
attempt. The production network admission boundary separately refuses D3B gameplay requests while `GuiClientInput` is
selected.

## Wired and deliberately inactive

**VERIFIED — wired:**

- `GuiClientInput` admission is checked at `GameSession.SendUnguardedAsync`; queued injected frames receive a final
  admission check, claim and write under `InjectedFrames._admissionLock`.
- `Bots.InputMode` is propagated to each new game session. Host startup strictly validates the `Gui` section. The
  production default is `Gui:Enabled=false` and `InputMode=ProtocolInjection`; `GuiClientInput` requires
  `Gui:Enabled=true`.
- `BotActor` has a GUI intent inbox, registration and acknowledgement, one-click tracking, synchronous revocation,
  protocol observation correlation, an outcome waiter, and final consumption. `WalkToCell` is the only GUI action
  accepted by the actor inbox.
- `GuiWalkToCellCoordinator` is an explicit caller API. It is not registered as a host gameplay trigger. The preflight
  manifest tool is present for offline snapshots, verification, comparison, backup and restore.
- `GuiHostCapability` reports `AutomaticActivity`, `SupervisedOneShotAvailable`, and `RealInputAvailable` as false.
  `GuiHostModule` passes the validated admission mode only; it does not register the executor, XTest input, or a capture
  loop.

**OPEN — not wired or activated:** real input through the host, automatic activity, `SupervisedOneShot`, host executor
registration, and a live action. `XdotoolGuiInput` and `GuiActionExecutor` exist as library types; the host does not
register or activate them. No Dofus input was performed for this integration.

## Source anchors

These line references are from the implementation base `3ebf635`:

| Claim | Source |
| --- | --- |
| GUI and Bot dependency direction | `src/D3B.Gui/D3B.Gui.csproj:1-15`; `src/D3B.Bot/D3B.Bot.csproj:7-21`; `src/D3B.Engine/D3B.Engine.csproj:1-12`; `src/D3B.Storage/D3B.Storage.csproj:1-12` |
| X11, capture, calibration, target and input types | `src/D3B.Gui/Windowing/X11WindowLocator.cs:8`; `src/D3B.Gui/Capture/MaimWindowCapture.cs:10`; `src/D3B.Gui/Calibration/CalibrationValidator.cs:8-26`; `src/D3B.Gui/Vision/TerrainCellTargetLocator.cs:15`; `src/D3B.Gui/Input/XdotoolGuiInput.cs:7-23`; `src/D3B.Gui/Actions/GuiActionExecutor.cs:11-14` |
| Host capability and mode validation | `src/D3B.Bot/Hosting/GuiHostCapability.cs:29-44`; `src/D3B.Bot/Hosting/GuiHostStartup.cs:16-38`; `src/D3B.Bot/Hosting/GuiHostModule.cs:6-15`; `src/D3B.Gui/Safety/GuiOptionsValidator.cs:20-28` |
| Session admission and locked final write | `src/D3B.Bot/Network/GameSession.cs:208-235,306-333`; `src/D3B.Network/Relay/InjectedFrames.cs:132-189` |
| Mode propagation and actor observations/revocations | `src/D3B.Bot/Runtime/BotsModule.cs:49-62`; `src/D3B.Bot/Runtime/Bots.cs:150-170,236-270`; `src/D3B.Bot/Runtime/BotActor.cs:875-965,1461-1644` |
| Existing planner and explicit coordinator | `src/D3B.Engine/Maps/CellPathfinder.cs:1-20`; `src/D3B.Bot/Movement/WalkToCellPlanner.cs:1-80`; `src/D3B.Bot/Gui/GuiWalkToCellCoordinator.cs:27-28,51-215,224-356` |
| Harvesting and inactive GUI capability | `src/D3B.Bot/DefaultModules.cs:23-29`; `src/D3B.Bot/Harvesting/HarvestActivity.cs:37`; `src/D3B.Bot/Hosting/GuiHostCapability.cs:20-44` |
| Diagnostic sidecar and Rust entry point | `src/D3B.Diagnostic/Ipc/DiagnosticSidecarFactory.cs:7`; `src/D3B.Diagnostic/Replay/ReplayEngineClient.cs:8`; `rust/d3b-diagnostic/src/main.rs:8` |
| Offline preflight CLI and atomic manifest writes | `scripts/preflight/PreflightCommand.cs:33-93`; `scripts/preflight/PreflightManifestStore.cs:74-117`; `scripts/preflight/PreflightOperations.cs:20-105` |
