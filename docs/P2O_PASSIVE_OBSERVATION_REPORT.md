# P2O passive observation report

**Date:** 2026-11-09
**Verdict: P2O PASS — PASSIVE OBSERVATION CONFIRMED**, scoped to the owner-confirmed map, position, and harvestable tree; complete diagnostic report coverage; zero D3B automation game commands; and verified restoration and P2O cleanup. This is not a live-action pass; live action remains **BLOCKED** under D340.

## Status and stop

The session ran under an explicit owner GO. It stopped at the first inconsistency when unread, non-allowlisted carried message types closed the movement translation latch. This met the runbook hard stop. There was no retry in the same session.

The passive-observation PASS is limited to the observations and checks listed in this report. It does not claim a latch-open result or a fully decoded session. The 58 unread frames, 24 recognized non-position frames, and 11 partial-readable event types remain explicit limits.

## Session identity

- P2O host: private apphost `$OBS/apphost-p2o-b82780a`, informational version `0.0.0-alpha.0.144+b82780a1f2cdf9dd6e1de2e2491e5042e8189f0f`. The installed P2L apphost was neither used nor replaced.
- Client: `3.7.13.17`, build `2026-11-03T14:11:23+00:00`; schema SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`.
- Diagnostic engine: `d3b-diagnostic 0.1.0`, source `live`, SNS `c780035c9020058a772ac8a2eef09555acd5a173`.
- Connection phase was relayed to Ankama. The client sent `request.identification.token` and `request.server_selection`; D3B rewrote the server selection to its game listener at `127.0.13.59:5556`. The game connection identifier is retained in private evidence and omitted from this report.
- Diagnostic interval: `2026-11-09T17:03:58.1753453Z` through `2026-11-09T17:05:10.5517711Z`. Session duration: `00:01:12.5631650` (about 1 minute 12.56 seconds).
- Host log totals before stop: 1,416 bytes from the client, 99,893 bytes from the server, and 0 frames from D3B.
- Private evidence was retained outside Git under `$OBS/`: `logs/host.out`; `ui/ui-20261109T170404Z.png`; `ui/ui-stop-20261109T170501Z.png`; and `diagnostic-reports/diagnostic-report-20261109T170510636Z-dd1ac001.json`. `$OBS` is defined in [the passive observation runbook](P2O_PASSIVE_OBSERVATION_RUNBOOK.md). The diagnostic report is 66,719 bytes, SHA-256 `299b29f4c02a8cdadf19816488ff4fad2b58d80577bdd1035ebab4204ec6f08e`. These files and raw connection identifiers are not included in Git.

## Server and relay observations

The relay observed the connection phase and the client requests listed above. The server event stream included character list and selection, character stats, inventory, job, spells, map information, map movement, interactive-element update, map actor shown, and text information events.

The host reported `BotActor` placed on map `120063492` at cell `103`. The following 11 event types were explicitly partial-readable; their presence is evidence, but their messages were not completely decoded:

- `CharacterListEvent`
- `CharacterSelectionEvent`
- `CharacterStatsEvent`
- `InventoryContentEvent`
- `JobDescriptionEvent`
- `SpellsEvent`
- `MapInformationEvent`
- `MapMovementEvent`
- `InteractiveElementUpdatedEvent`
- `MapActorShownEvent`
- `TextInformationEvent`

The map and position scope below was subsequently confirmed by the owner from the private visual evidence. The partial-readable protocol status remains a limitation on protocol-to-UI and field-level correlation.

## Owner-confirmed visual observations

On 2026-11-09, after the live session, the owner confirmed the map, position, and harvestable tree shown in the private screenshots:

- UI map label: **Astrub (Tainela)**; corresponding observed server map id: `120063492`.
- Position: cell `103`.
- A harvestable Walnut tree was visible, with tooltip **Walnut / +0% XP / Lumberjack Lvl.40**.

This confirmation supports only those passive observations. No movement or harvest action was attempted. The private screenshots remain outside Git.

## Harvest elements

The owner confirmed the visible Walnut tree as harvestable. No harvest action occurred. The harvestable element's `ElementId`, `GfxId`, target `CellId`, and screen-position correlation were not established; this does not change the owner-confirmed current position at cell `103`.

## Diagnostic coverage

The private report records diagnostic session status `COMPLETED`: 122 enqueued and 122 analyzed; 0 dropped; 0 unattributed; `includeReadable=false`; coverage complete; 82 types; 122 occurrences marked `VALID`; type URL checks 122 OK and 0 mismatches; 0 problems; and no truncation.

It records 12 `SHAPE_VARIATION` contradictions across five types: `ira` (2), `ism` (2), `jgm` (2), `kna` (3), and `kpx` (3). These remain for offline review. Complete diagnostic coverage means all 122 enqueued observations were analyzed; it does not mean every carried protocol type was readable or that the movement latch stayed open.

Private report: `diagnostic-reports/diagnostic-report-20261109T170510636Z-dd1ac001.json`, SHA-256 `299b29f4c02a8cdadf19816488ff4fad2b58d80577bdd1035ebab4204ec6f08e`.

## Unread types and unresolved correlations

Before the stop, the host logged 58 frames with `readable protocol lacks the carried type`, covering 58 unique obfuscated tokens:

- Client direction (14): `.hkv`, `.iqu`, `.jcy`, `.jet`, `.jgm`, `.jpe`, `.kkt`, `.klx`, `.kmk`, `.kmu`, `.kne`, `.kod`, `.lpj`, `.lwx`.
- Server direction (44): `.hlv`, `.hmj`, `.hpe`, `.hrl`, `.ibs`, `.ibw`, `.ica`, `.icj`, `.ico`, `.ifg`, `.ilr`, `.imf`, `.ipe`, `.ira`, `.ism`, `.isr`, `.ixd`, `.iye`, `.iym`, `.izn`, `.jcz`, `.joo`, `.jpx`, `.kaq`, `.kmi`, `.kmt`, `.knz`, `.kog`, `.kpx`, `.ktc`, `.kxn`, `.lnu`, `.lov`, `.lpp`, `.ltb`, `.lto`, `.luo`, `.lve`, `.lvw`, `.lxb`, `.lxg`, `.mdp`, `.meb`, `.mem`.

The host also logged 24 proven non-position messages of this build that did not close the movement latch. Those frames do not negate the separate stop caused by the unread carried types. The 58 unread tokens are diagnostic candidates only: none was promoted, and none changed a mapping or resolver.

Owner confirmation establishes the map label, map, current position, and visible tree at the stated scope. It does not establish the harvestable element's `ElementId`/`CellId`/`GfxId`/screen-position correlation. The map label-to-id correspondence is visually confirmed for this observation, but no map transitions were observed. The 11 partial-readable events and 12 `SHAPE_VARIATION` contradictions remain limitations for offline diagnostic review.

## Automation game commands

The expected zero was satisfied: 0 frames from D3B. No movement, harvest, click, injected frame, mapping change, resolver promotion, or automatic profile was issued.

## Restoration and cleanup

The P2O observation stack is stopped: the host was stopped with SIGINT, `d3b-diagnostic` stopped, and no P2O listener remains. The observed session ended. ClientRedirect restored the Ankama version file; its SHA-256 returned to `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`. `scripts/client.cs check` exited 0 and reported that clients connect straight to Ankama. Full preflight verification of the live client against the private before manifest returned `OK`.

The Dofus.x64 instance used for the observation was closed. A later Dofus.x64 instance was reported running after that closure; it is outside the P2O session and owner GO, has direct Ankama configuration and no D3B listener, and is not P2O evidence. It remains a separate client-process cleanup item. The private logs, screenshots, diagnostic report, manifests, and session data remain outside Git. No push was made.

## Follow-up and limits

- Review the unread carried types and `SHAPE_VARIATION` contradictions offline.
- Make no mapping change or resolver promotion from this observation.
- Any future live observation requires a new explicit owner GO; do not retry this session.
- The result is a passive-observation PASS only. Live action remains **BLOCKED** under D340.
