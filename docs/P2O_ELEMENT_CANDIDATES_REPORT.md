# D3B-S2A report: passive element-candidate discovery

**Date:** 2026-11-10
**Verdict: D3B-S2A PASS — OFFLINE ONLY.** The supervised module can passively describe the harvest candidates of the
character's current map without knowing their identifiers in advance. No live observation or action was performed.
S2B is prepared but not executed. Live action remains **BLOCKED** under D340; this report grants no GO.

## 1. HEAD and environment

- Repository: `/home/nill/perso/dofus/D3B_linux`, inner Git worktree.
- Worktree: `/home/nill/.paseo/worktrees/17r3bypd/d3b-s2a-element-candidates`.
- Branch: `d3b-s2a-element-candidates`, created from base
  `34b3b0538bdef8b6b7a669d68bb703f248bf917d` (`D3B-S1.1: harden element-use confirmation and observation gates`).
- SDK: `/home/nill/.dotnet/dotnet`, `10.0.403`, as pinned by `global.json`.
- Platform: Linux, `net10.0`, x64.
- Working tree: uncommitted on the branch; no commit, push, merge or pull request was performed.
- No Dofus client, relay, host session, X11 click, movement, harvest or live `p2j element use` was launched.

The mission documents read before implementation: D340, D344, D345 and D346; `docs/P2O_PASSIVE_OBSERVATION_REPORT.md`;
`docs/P2O_ELEMENT_USE_RUNBOOK.md`; `SupervisedElementUse` and its preparation records; `CharacterState.Elements`,
`MapData`, `MapInteractive`, `JobCatalog`, `ItemCatalog`, `HarvestSightings`, `GraphicSkills` and
`GuiWorldSnapshotAdapter`; and the existing supervised element-use tests.

## 2. Reused mechanism and passive addition

Existing interfaces were reviewed before adding anything:

- `GuiWorldSnapshotAdapter`/`GuiWorldSnapshot` already capture a coherent server view, but they expose only enabled
  skill ids and no skill instances, disabled skills, pack cells/graphics or catalog item deductions. They are not
  sufficient for the required candidate record.
- `HarvestSightings` is populated by `HarvestActivity`, which this mission forbids. It cannot be the discovery source.
- `GraphicSkills` is populated passively by `GraphicLearner` and is reused only as a labelled hint for pack-only
  elements; it is never treated as visual proof.
- `CharacterState.Elements` remains the server-observed element source, `MapData.Interactives` the pack source,
  `JobCatalog`/`ItemCatalog` the catalog source, and `ElementCells` the current-cell range source.

The addition is a passive core, not a second inventory and not a second harvest engine:

- `ElementUseSnapshot` captures one `BotActor.View`, one session read and one map read.
- `ElementCandidates.Build` is a pure function over that snapshot, the game tables, the optional `GraphicSkills` hint
  and an optional item-name callback. It performs no I/O on the relay, session or character.
- `SupervisedElementUse` was refactored to share the same snapshot, `SessionBlocker` and `Prepare` path while keeping
  its S1.1 behavior; the existing tests stay green.
- Only the existing `p2j` console is extended. No new relay, harvest engine, pathfinder, OCR, X11 pipeline, capture
  service, external dependency or automatic activity was added.

## 3. Candidate list format

`p2j element candidates` takes no argument and prints one header followed by zero or more candidate lines, in
ascending `ElementId` order.

Header:

```text
P2J ELEMENT CANDIDATES version=1 reason=<code> session_epoch=<n|unknown> state_revision=<n|unknown> connected=<true|false> session=<true|false> translation_blocked=<true|false> paused=<true|false> acting=<true|false> input_mode=<name|unknown> action_enabled=<true|false> consumed=<true|false> map=<id|unknown> cell=<n|unknown> provenance=<name|unknown> trusted=<true|false> candidates=<n> target=<TARGET_IDENTIFIED|TARGET_AMBIGUOUS|TARGET_UNKNOWN> action=<ACTION_READY|ACTION_BLOCKED> action_reason=<code>
```

Candidate:

```text
P2J ELEMENT CANDIDATE version=1 element=<id> map=<id|unknown> server_known=<true|false> pack_known=<true|false> interactive=<n|unknown> server_cell=<n|unknown> pack_cell=<n|unknown> gfx=<n|unknown> state=<n|unknown> skill=<n|unknown> instance=<n|unknown> skills=<csv|unknown> skill_enabled=<true|false|unknown> catalog=<Matched|Partial|Unmatched|Unknown> item=<n|unknown> item_name=<"name"|unknown> possible_items=<csv|unknown> min_level=<n|unknown> uses_from_current_cell=<true|false|unknown> identity=<Verified|Inferred|Ambiguous|Unknown> identity_source=<server+catalog|pack+graphics+catalog|server|pack+graphics|pack|unknown> cell_conflict=<true|false> visual_identity=unconfirmed prep=<Ready|Blocked|Unavailable> prep_reason=<code>
```

Provenance is explicit:

- server-observed: `server_known`, `server_cell`, `interactive`, `state`, `skill`, `instance`, `skills`,
  `skill_enabled`;
- pack: `pack_known`, `pack_cell`, `gfx`;
- catalog: `catalog`, `item`, `item_name`, `possible_items`, `min_level`;
- visual: always `visual_identity=unconfirmed`; a `GfxId` or `GraphicSkills` hint never establishes the visible
  resource.

Verdicts are independent:

- `TARGET_IDENTIFIED` requires exactly one candidate with verified server+catalog identity, one gathered item and no
  cell conflict. It is a structural identity verdict: it does not include position trust or visual identity. An
  untrusted position can still be `TARGET_IDENTIFIED` while the action is blocked by `GUARD_*`. Several candidates,
  inferred hints, unknown catalog skills or several possible items yield `TARGET_AMBIGUOUS`; no candidate yields
  `TARGET_UNKNOWN`.
- `ACTION_READY` requires that identified target to prepare `Ready` while `ActionEnabled=true`. A closed translation
  latch, an untrusted position, an ambiguous target, a disabled action gate or a consumed attempt yields
  `ACTION_BLOCKED` with a stable reason. During observation-only, the expected verdict is `ACTION_BLOCKED`.

## 4. Tests: red, corrections and green

Initial implementation produced compile-time red: the first parent build failed with nine compiler errors in
`ElementCandidates.cs` (name collision, `short`/`int` conditional typing, nullable `MapElement` dereferences and a
nullable `GraphicSkills` result). The errors were corrected; an unnecessary `using` was also removed.

A controlled red check then proved the tests detect a broken candidate filter:

- Mutation: temporarily inverted `if (!include) continue;` in `ElementCandidates.Build`.
- Build: succeeded with 0 errors.
- Focused run: 65 total, 14 failed, 51 passed, exit 2. The 14 failures were the 14 discovery tests at that point.
- Restore: the exact byte-identical pre-audit-fix source was restored (SHA-256
  `85679b00d18c3d573450368fb39e5affd6a5be2777f656f8693d3773761e226c`; the later audit fix changed the identity
  condition, so the final source is `98e0cb0f12d902351bc2aab9205634726e5fac0bed44a6a65ec3e2c332815227`).
- Rerun: 65/65 passed, exit 0.

The independent audit then found one additional fail-closed defect: a server element with one cataloged harvest skill and one
skill outside the job catalog could remain `Verified`/`Identified`. The identity rule now treats mixed known/unknown
evidence as `Ambiguous`; `AnElementWithAnUnknownSkillAlongsideAKnownHarvestIsAmbiguous` covers it. A targeted red run
without the new identity condition failed 1/1; restoring the exact source passed 1/1. The final discovery tests are 15.
The independent delta audit then returned **APPROVE**: the fixed condition and the new test cover the defect, the
only-unknown-skill case is unchanged, and the audit found no other semantic change.

Final results with the pinned SDK:

| Check | Result |
| --- | --- |
| `dotnet build --no-restore` (solution, includes `D3B.Host`) | 0 warnings, 0 errors |
| Focused `ElementCandidates`/`SupervisedElementUse` | 68/68 passed |
| `D3B.Bot.Tests` | 967/967 passed |
| Full solution, `--max-parallel-test-modules 1` | 2,397 total, 2,393 passed, 4 skipped (`D3B.Diagnostic`), 0 failed, exit 0 |
| `dotnet format --verify-no-changes --no-restore` | exit 0 |
| `git diff --check` | exit 0 |

The four skips require `D3B_DIAGNOSTIC_ENGINE` or `D3B_P2L_DEMO_DIR`; they are the existing supervised-process skips,
not this mission's tests. The Paseo implementer sandboxes could not run the pinned-SDK build or test runner because
their sandbox denied named-pipe binding (`SocketException: Permission denied`); the parent ran the build, focused tests,
full suite, format and diff checks with the pinned SDK. No parallel wrapper was used and the serialized full solution
run completed normally.

## 5. Complete synthetic example

Values below come from the synthetic `TestPlay` fixture (map `191104002`, character cell `190`, tree element
`514662`, visual identity deliberately unconfirmed). `session_epoch` and `state_revision` are runtime values.

```text
P2J ELEMENT CANDIDATES version=1 reason=READY session_epoch=7 state_revision=42 connected=true session=true translation_blocked=false paused=false acting=false input_mode=ProtocolInjection action_enabled=false consumed=false map=191104002 cell=190 provenance=ServerPlaced trusted=true candidates=1 target=TARGET_IDENTIFIED action=ACTION_BLOCKED action_reason=ACTION_DISABLED
P2J ELEMENT CANDIDATE version=1 element=514662 map=191104002 server_known=true pack_known=true interactive=34 server_cell=176 pack_cell=176 gfx=650 state=0 skill=40 instance=19146 skills=40 skill_enabled=true catalog=Matched item=303 item_name="Ash" possible_items=303 min_level=1 uses_from_current_cell=true identity=Verified identity_source=server+catalog cell_conflict=false visual_identity=unconfirmed prep=Ready prep_reason=READY
```

The same candidate with the translation latch closed keeps every diagnostic field but prints
`prep=Blocked prep_reason=TRANSLATION_LATCH_CLOSED`, and the header prints
`action=ACTION_BLOCKED action_reason=TRANSLATION_LATCH_CLOSED`. A pack-only element prints `server_known=false`,
`pack_known=true`, `skill=unknown` (or an inferred skill when `GraphicSkills` knows the graphic) and
`prep_reason=ELEMENT_NOT_KNOWN`. A server-only element prints `pack_known=false`, `gfx=unknown` and
`prep_reason=ELEMENT_NOT_ON_MAP`. A catalog mismatch prints `catalog=Unmatched`, `item=unknown`,
`identity=Unknown` and `prep_reason=SKILL_NOT_IN_CATALOG`.

## 6. Zero gameplay request

The candidate path is passive in code and in tests:

- `ElementCandidates` and its records contain no `RunAsync`, `SendGuardedAsync`, `SendAsync`, `ElementUse.Request`,
  `StartActivity`, `HarvestActivity`, `MapMovementRequest`, `MapChangeRequest` or `InteractiveUseRequest`.
- `SupervisedElementUseSession.Candidates` only calls `ElementCandidates.Capture`; it does not touch `_consumed`,
  `UseAsync` or `Prepare`.
- `SupervisedElementUseConsole` handles `p2j element candidates` before the action gate and only prints/logs the
  report.
- Tests assert no new gameplay request: an empty `connection.Sent` where the fixture sent nothing, and no additional
  request where the fixture already sent a movement request. They also assert no movement/map-change/use request,
  `bot.Activity is null`, and `session.Prepare(...).AttemptConsumed == false` after a candidates command. A second
  test feeds a `p2j element use` command with the action gate closed and asserts it is refused with no send.
- No test or validation step launched Dofus, a relay or a live host. `TestPlay` uses fake game connections only.

The command remains available with `Supervised:ElementUse:ActionEnabled=false`, which is the observation-only
configuration. Both settings are still false by default.

## 7. Conditions for identifying a real Walnut

A real Walnut can be identified only when all of the following hold:

1. The server-observed element has a harvest skill that `JobCatalog` maps to a `GatheredItemId` present in
   `ItemCatalog`.
2. The candidate has no server/pack cell conflict. Its position provenance is recorded separately: a trusted server
   position is required for the operator's identification record and for any action, but it is not part of the
   structural `TARGET_IDENTIFIED` token. An untrusted position yields `TARGET_IDENTIFIED` plus `ACTION_BLOCKED` with a
   `GUARD_*` reason.
3. The command's structural verdict is `TARGET_IDENTIFIED` only when exactly one verified server+catalog candidate
   remains. If several remain, the command stays `TARGET_AMBIGUOUS`; the S2B operator may use a manual screenshot to
   correlate the visible tree with one candidate, but must record the command's ambiguous verdict and the manual
   correlation separately.
4. The readable item name, when available, comes from the pack's texts; an absent name stays `unknown`.
5. A `GfxId` or `GraphicSkills`-only match is never written as `Walnut confirmed`.

If several candidates remain, the report must stay `TARGET_AMBIGUOUS`; no candidate is chosen arbitrarily in the
command output.

## 8. Remaining blockers before an action

- **Identity:** protocol identity is passive and offline; visual identity of the Walnut is not yet observed in S2A.
- **Scope:** no exact session/build/map/cell/element/skill-instance tuple has been admitted for an action.
- **Latch:** P2O closed the movement translation latch on 58 unread carried types; S2A does not decode them or widen
  `NonPositionMessages`.
- **Admission:** D340 remains BLOCKED. Even a `TARGET_IDENTIFIED` + `ACTION_READY` display is not an authorization;
  `p2j element use` still requires the action gate and a separate explicit one-action GO with an operator present.

## 9. Prepared S2B procedure (not executed)

`docs/P2O_ELEMENT_CANDIDATES_RUNBOOK.md` is **PREPARED / NOT EXECUTED / OWNER GO REQUIRED**. It covers:

- a new private apphost published from the S2A source, never the old `apphost-p2o-b82780a` or installed P2L build,
  with source/diff identity, informational version, full manifest, file count/size, launcher and payload hashes;
- re-verification of the client version/build, client tree, pack, schema, mapping, diagnostic engine and new apphost
  banner;
- fresh Ankama and D3B-client manifests, a verified private backup, read-only preflight checks, and exact
  compare/restore/verify commands;
- observation-only configuration with `Supervised:ElementUse:Enabled=true` and
  `Supervised:ElementUse:ActionEnabled=false`;
- the only allowed passive commands, `p2j element candidates` then `p2j element readiness ...` for an exact candidate;
- the private relevé format, the historical `120063492` / cell `103` / Walnut / Lumberjack 40 scenario as a reference
  only, and the rule that a graphic hint is not visual proof;
- hard stop conditions, the first latch-closing message record (timestamp, direction, type/token, decode state,
  carrier, client version/build, evidence hash), and the prohibition on extrapolating or disabling protections;
- shutdown, comparison, restoration, private evidence retention and the separation of resource identification from
  action availability.

No setting was changed and no S2B observation was run.

## 10. Git state and no real action

- Branch `d3b-s2a-element-candidates` from base `34b3b0538bdef8b6b7a669d68bb703f248bf917d`.
- Working tree contains the uncommitted implementation, tests and docs; no commit, push, merge or pull request.
- No Dofus launch, relay, live host, X11 click, movement, harvest, `p2j element use`, action-gate opening, mapping
  change, resolver promotion or Ankama modification.
- The D340 live-action block is unchanged.

**Offline verdict:** D3B-S2A passes for passive discovery only. The next permissible step is a separately authorized
S2B passive observation prepared by the runbook; no S2B execution, no live action and no GO are implied.
