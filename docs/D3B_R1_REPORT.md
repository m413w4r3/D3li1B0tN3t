# D3B-R1 harvest chain report

**Verdict: `END_TO_END_OFFLINE_PASS`** for the offline chain. Live execution remains **`BLOCKED` under D340**. The
translation latch is still closed by 58 unread carried types, including `mem`; the mission runner has no live wiring,
and the server timing and event dependencies below have not been exercised against a real server.

## Capability status

| Mission component | Status | Evidence |
|---|---|---|
| Weight provenance and inventory-revision freshness | EXISTS | `InventoryRevision`/`WeightInventoryRevision`; stale coverage is `WEIGHT_STALE`; `src/D3B.Bot/Characters/CharacterState.cs`; `CharacterEvents.cs` |
| Job level knownness and required-level check | EXISTS | `src/D3B.Bot/Characters/CharacterJob.cs:14-15`; `HarvestJob.cs:26-86` |
| Resource identity, ambiguity and uniqueness | EXISTS | `src/D3B.Bot/Interactives/ElementCandidates.cs:39,129,345`; `ElementCandidate.cs:8`; `HarvestResourceTarget.cs:31` |
| Approach cell and pack-valid path | EXISTS | `src/D3B.Engine/Maps/ElementCells.cs:21`; `src/D3B.Engine/Maps/CellPathfinder.cs:54`; `HarvestApproachPlanner.cs:23` |
| Single-map walk and confirmation | EXISTS | `src/D3B.Bot/Movement/MapWalk.cs:45,274`; `CharacterEvents.cs:150` |
| Multi-map travel and bounded replanning | EXISTS | `src/D3B.Bot/Movement/WorldTravel.cs:38,129`; `MapChange.cs:240` |
| Supervised one-shot use and evidence | EXISTS | `src/D3B.Bot/Interactives/SupervisedElementUse.cs:162,302`; `SupervisedElementUseSession.cs:67` |
| Trusted verdict position | EXISTS | `SupervisedElementUseEvidence.EndPositionTrusted`; final trust check in `SupervisedElementUse`; `HarvestMissionRunner.VerifyEvidence` |
| Passive harvest plan command | EXISTS | `src/D3B.Bot/Hosting/SupervisedElementUseConsole.cs:18,130,235`; 26/26 console tests |
| Internal mission orchestration | TO INTEGRATE | `src/D3B.Bot/Harvesting/HarvestMissionRunner.cs:34`; offline tests only; no host/live registration |
| Remaining unread protocol types | TO CORRECT | 58 blocking types remain; `mem` is not admitted |
| Live server behavior and timing | UNVERIFIED | No live session or real-server action was performed |

## Protocol evidence

The batch admits exactly four types: `lnu` (server), `ktc` (server), `kmk` (client), and `kmu` (client). Their schema
and reference field maps are empty, so field completeness is vacuous; the observed directions and identities are
proven. The focused non-position tests passed 35/35. The `mem` regression remains: a valid three-field payload closes
movement. The build schema has `ggpy` as a bool, `ggpz` as an enum, and `ggqa` as an enum. Only `ggpz` is semantically
proven; `ggpy` and `ggqa` are reference-labelled unknown, and account role/security is an excluded family. Therefore
`mem` is not admitted and there is no artificial latch opening. No ambiguous type was accepted.

The S2B report counted 86 unread types, 24 allowlisted types present (with `kbi` absent), and 62 blockers before the
batch. Four were corrected; 58 remain blocking, including `mem`. Remaining blockers include insufficiently mapped
fields, ambiguous nested messages, unmapped tokens, and policy-excluded account, map, fight, house, haven-bag,
game-action, challenge, NPC, verification and BAK families. The exact per-token list and field-level evidence are held
in the private D3B-R1 protocol analysis outside Git; this report summarizes it by category. No private payload, capture, account identifier or
per-build mapping is included here.

## Readiness, target identity and approach

Pods are known only from server `InventoryWeightEvent` provenance with positive capacity. `CharacterState` tracks
`InventoryRevision` and the revision covered by the latest weight as `WeightInventoryRevision`; a mismatch means the
weight is stale and readiness stops with `WEIGHT_STALE`. For the post-harvest check, `WeightRevisionBefore` is captured
at the post-write send barrier and `CHECK_PODS_AFTER` requires a later weight revision. Free pods are
`max_weight - weight`, and load is `100 * weight / max_weight`. The default threshold is 90%. Absent weight is
`WEIGHT_NOT_OBSERVED`, not zero load; unknown or zero capacity blocks. Job readiness requires a job
with a known level and the catalog's required level. An unknown job level is not assumed to be level 1. Target
selection requires server element evidence and catalog proof for the wanted item. Ambiguous evidence blocks; multiple
verified candidates are resolved deterministically by nearest confirmed approach then element id and recorded as
`MULTIPLE_TARGETS_RESOLVED_BY_APPROACH`; no random choice is made.

Approach uses a server-known element cell and a cell from `ElementCells`, then validates a pack path with
`CellPathfinder`. It does not assume the element cell is walkable. Travel uses `WorldTravel`, follows known pack
transitions, and records evidence for each confirmed transition. Transition count and duration are bounded by the
mission request. The runner also caps actual journaled transitions at `request.MaxTransitions`; exceeding the cap
stops at `TRAVEL_TO_MAP` with `TRANSITION_LIMIT`. There is no implicit door or zaap recovery. `WorldTravel` replans
around a failed travel transition within bounded failure and change limits (`MostFailures`, `mostChanges`); if the
bounded journey fails, the mission stops Blocked and does not retry the mission.

## Orchestrator and evidence contract

The runner is one-shot and internal/test-only. Its 13 ordered stages are:

`CHECK_SESSION` → `CHECK_PODS` → `CHECK_JOB` → `PLAN_ROUTE` → `TRAVEL_TO_MAP` → `DISCOVER_TARGET` → `PLAN_APPROACH` →
`WALK_TO_USE_CELL` → `REVALIDATE_ALL` → `USE_ELEMENT` → `VERIFY_HARVEST` → `CHECK_PODS_AFTER` → `STOP`.

After travel it rechecks session, translation latch, position, pods and job before discovery; after the approach walk it
performs the full revalidation (session, translation latch, position, pods, job, target identity and action readiness).
The post-harvest weight revision is captured at the post-write send barrier and used by `CHECK_PODS_AFTER`. A
successful harvest requires a start attributed to the expected actor, an ordered end, the expected item, a positive
inventory gain still present at verdict, and consistent session and position evidence. The end position must be trusted
(`EndPositionTrusted`); otherwise the verdict is blocked with `POSITION_NOT_TRUSTED` or
`POSITION_NOT_TRUSTED_AFTER_SEND`. An end event by itself cannot establish success. There is no retry of the harvest
action, no banking, combat or activity start, and the runner adds no send path: it uses the existing supervised one-shot
protocol path. It is not registered in the host.

## Passive plan command

`p2j harvest plan <destinationMapId> <wantedItemId>` reports a plan and sends no gameplay request, moves nothing,
starts no activity and does not consume the one-shot. No `harvest run` or other execution command exists. The ready
console test printed:

```text
P2J HARVEST PLAN version=1 verdict=PLAN_READY reason=PLAN_READY start_map=191104000 destination_map=191104002 transitions=1 transition_types=Scroll candidates=0 target=NOT_VISIBLE job=OK job_level=1 required_level=1 weight=100 max_weight=1000 free_pods=900 load_percent=10 threshold=90 use_cell=unknown path=unknown translation_blocked=false missing=TARGET_NOT_VISIBLE_UNTIL_ARRIVAL
```

The blocked no-weight result was `PLAN_BLOCKED`, with `WEIGHT_NOT_OBSERVED`, `JOB_UNKNOWN`, and
`TARGET_NOT_VISIBLE_UNTIL_ARRIVAL`; its weight was unknown. Both plan cases sent zero requests. Candidate counts
describe selected targets (0 or 1), not every eligible resource; this remains a documented residual.

## Offline demo journal

Captured from the passing offline integration test; reproduced verbatim:

```text
MISSION START destination_map=191104002
CHARACTER weight=100 max_weight=1000 load=10%
JOB level=1 required_level=1 status=OK
ROUTE start_map=191104000 destination_map=191104002 transitions=1
MAP CHANGE 1 confirmed
RESOURCE element=514662 item=303 status=AVAILABLE
APPROACH from_cell=300 use_cell=175 status=CONFIRMED
HARVEST request_sent=true used=true ended=true
INVENTORY item=303 before=0 after=3 gain=3
PODS after=105 threshold=90%
MISSION PASS
```

This journal is offline test evidence, not a real-client acceptance result.

## Refusal matrix

All 31 T1b cases passed. Expected and actual stage are shown as the stage in which the case was observed; sent/use
counts are requests sent / element-use requests.

| Case | Stimulus | Expected / actual stage | Verdict | Reason | Sent / use |
|---|---|---|---|---|---:|
| `ClosedTranslationLatchStopsAtSessionCheck` | Closed latch | CheckSession / CheckSession | Blocked | `TRANSLATION_LATCH_CLOSED` | 0 / 0 |
| `UnknownWeightStopsAtPodsCheck` | No observed weight | CheckPods / CheckPods | Blocked | `WEIGHT_NOT_OBSERVED` | 0 / 0 |
| `ZeroCapacityStopsAtPodsCheck` | Capacity zero | CheckPods / CheckPods | Blocked | `CAPACITY_UNKNOWN_OR_ZERO` | 0 / 0 |
| `ThresholdPodsStopAtPodsCheck` | Threshold reached | CheckPods / CheckPods | Blocked | `PODS_THRESHOLD_REACHED` | 0 / 0 |
| `MissingJobStopsAtJobCheck` | Job absent | CheckJob / CheckJob | Blocked | `JOB_UNKNOWN` | 0 / 0 |
| `UnknownJobLevelStopsAtJobCheck` | Job level unknown | CheckJob / CheckJob | Blocked | `JOB_LEVEL_UNKNOWN` | 0 / 0 |
| `LowJobLevelStopsAtJobCheck` | Level below requirement | CheckJob / CheckJob | Blocked | `LEVEL_TOO_LOW` | 0 / 0 |
| `UnknownWantedItemStopsAtJobCheck` | Wanted item absent from catalog | CheckJob / CheckJob | Blocked | `CATALOG_UNKNOWN` | 0 / 0 |
| `UnreachableDestinationStopsAtRoutePlanning` | No route | PlanRoute / PlanRoute | Blocked | `NO_PATH` | 0 / 0 |
| `TransitionLimitStopsAtRoutePlanning` | EastMapId → MapId → NorthMapId needs two transitions; maxTransitions=1 | PlanRoute / PlanRoute | Blocked | `TRANSITION_LIMIT` | 0 / 0 |
| `MissingResourceStopsAtDiscovery` | No resource after MapId → NorthMapId travel | DiscoverTarget / DiscoverTarget | Blocked | `TARGET_NOT_FOUND` | 2 / 0 |
| `InvalidThresholdStopsBeforeAnyRequest` | Invalid threshold | CheckSession / CheckSession | Blocked | `INVALID_REQUEST` | 0 / 0 |
| `ClosedConnectionStopsAtSessionCheck` | Connection closed | CheckSession / CheckSession | Blocked | `SESSION_CLOSED` | 0 / 0 |
| `ReusingRunnerStopsAsAlreadyRun` | Reuse one-shot runner | CheckSession / CheckSession | Blocked | `ALREADY_RUN` | 0 / 0 |
| `NoUseAnswerStopsAsUnconfirmed` | No use answer | UseElement / UseElement | Blocked | `UNCONFIRMED (NO_ANSWER)` | 2 / 1 |
| `ServerUseRefusalStopsAtUseStage` | Server refuses use | UseElement / UseElement | Refused | `SERVER_REFUSED` | 2 / 1 |
| `ZeroDurationUseStopsAsUnconfirmed` | Use has no duration | UseElement / UseElement | Blocked | `UNCONFIRMED (USE_HAS_NO_DURATION)` | 2 / 1 |
| `MissingUseEndStopsAsUnconfirmed` | No use end | UseElement / UseElement | Blocked | `UNCONFIRMED (NO_END)` | 2 / 1 |
| `UseEndWithoutStartDoesNotConfirmHarvest` | End without start | UseElement / UseElement | Blocked | `UNCONFIRMED (NO_ANSWER)` | 2 / 1 |
| `UseStartedByAnotherActorDoesNotConfirmHarvest` | Start attributed to another actor | UseElement / UseElement | Blocked | `UNCONFIRMED (NO_ANSWER)` | 2 / 1 |
| `MissingItemGainStopsAsUnconfirmed` | No item gain | UseElement / UseElement | Blocked | `UNCONFIRMED (ITEM_NOT_GAINED)` | 2 / 1 |
| `WalkRefusalStopsBeforeUse` | Walk refused | WalkToUseCell / WalkToUseCell | Blocked | `WALK_REFUSED` | 1 / 0 |
| `WalkNotConfirmedStopsBeforeUse` | Walk shown, confirmation missing | WalkToUseCell / WalkToUseCell | Blocked | `WALK_NOT_CONFIRMED` | 1 / 0 |
| `TranslationLatchClosedAfterWalkStopsBeforeUse` | Latch closes after walk | RevalidateAll / RevalidateAll | Blocked | `TRANSLATION_LATCH_CLOSED` | 1 / 0 |
| `MissingWeightRefreshAfterHarvestStopsAtPostCheck` | No post-harvest weight refresh | CheckPodsAfter / CheckPodsAfter | Blocked | `WEIGHT_NOT_REFRESHED` | 2 / 1 |
| `RefreshedPodsAtThresholdStopCleanlyAfterHarvest` | Refreshed pods reach threshold | Stop / Stop | Pass | `PODS_THRESHOLD_REACHED` | 2 / 1 |
| `RemovedExpectedItemGainCannotPassVerification` | Expected gain removed before verdict | UseElement / UseElement | Blocked | `UNCONFIRMED (ITEM_GAIN_NOT_PRESENT_AT_VERDICT)` | 2 / 1 |
| `StaleWeightAfterInventoryChangeStopsAtPodsCheck` | weight reported, then inventory changed | CheckPods / CheckPods | Blocked | `WEIGHT_STALE` | 0 / 0 |
| `UntrustedPositionAfterUseDoesNotPass` | uncorrelated refusal after the use | UseElement / UseElement | Blocked | `UNCONFIRMED (POSITION_NOT_TRUSTED_AFTER_SEND)` | 2 / 1 |
| `PodsThresholdDuringTravelStopsAtArrival` | pods reach threshold during travel | TravelToMap / TravelToMap | Blocked | `PODS_THRESHOLD_REACHED` | 2 / 0 |
| `JobLevelDuringTravelStopsAtArrival` | job level drops during travel | TravelToMap / TravelToMap | Blocked | `LEVEL_TOO_LOW` | 2 / 0 |

The T1a two-map case observed `EastMapId → MapId → NorthMapId`, two confirmed transitions, then stopped at
`DiscoverTarget` / `TARGET_NOT_FOUND`; it sent four requests and no use request.

## Six primary mission metrics

1. Destination is plannable from known pack data.
2. Multi-map route is confirmed in simulation with ordered transition evidence.
3. A valid use cell and pack path are computed for a known target.
4. Job level and pods readiness are checked before action.
5. Harvest proof includes the expected inventory gain and refreshed weight evidence.
6. An invalid or refused condition stops the chain with no further action.

## Verification results

All results below are from the supplied final pre-documentation run with the pinned SDK
`/home/nill/.dotnet/dotnet` 10.0.403:

| Check | Result |
|---|---|
| Focused non-position tests | 35/35 passed |
| Focused HarvestMission tests (O1 + T1a + T1b) | 44/44 passed |
| Harvesting filter | 107/107 passed |
| Console/plan tests | 26/26 passed |
| `D3B.Bot.Tests`, serialized | 1054/1054 passed, three consecutive green runs |
| Full solution, serialized | 2484 total; 2480 passed; 4 skipped (`D3B.Diagnostic`); 0 failed; exit 0, two consecutive green runs |
| Build | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | Exit 0 |
| `git diff --check` | Exit 0 |

## Audit repair

The first independent xhigh audit returned **REJECT** with four blockers: end position could be untrusted; a weight event
from before harvest could be accepted; travel could exceed `MaxTransitions` after replanning; readiness was not
rechecked after travel. All four were fixed and re-tested in `CharacterState.cs`, `CharacterEvents.cs`, `HarvestPods.cs`,
`SupervisedElementUse.cs`, and `HarvestMissionRunner.cs`. The delta audit returned APPROVE with no remaining blocker; the residuals below remain.

Under load, the pre-existing, unrelated
`D3B.Bot.Tests.Fighting.FightActivityTests.WithNothingToEatTheCharacterRegeneratesBackToTheShareAloneBeforeItAttacks`
flaked in two full-solution runs, once at the 2478 tree and once at the 2479 tree; each `TestWalks` failure timed out
waiting for a request (0 requests in 10 s), and each immediate rerun was green. The Bot-only suite was green at
1054/1054 on three runs, and the harvest-chain focused suites were green.

The full-solution serialized result used `dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build`.
Serialization avoids the known environment limit of 128 inotify instances that caused failures in a parallel Bot
run. The four skipped diagnostic tests were pre-existing skips.

## Live blockers and proposed supervised scenario

Live remains blocked by D340. The blocking conditions are:

- 58 unread carried types remain, including `mem`; the latch must not be opened artificially.
- A fresh `InventoryWeightEvent` revision must follow the post-write barrier or the runner stops with
  `WEIGHT_NOT_REFRESHED`/`WEIGHT_STALE`; it does not retry.
- There is no live wiring for `HarvestMissionRunner`; the plan command is passive, and element use remains gated off by
  default.
- Job levels depend on `JobExperiencesEvent`; unknown level blocks and is never treated as level 1.
- Resource identity requires server-stated element evidence and catalog proof; visual identity is unconfirmed.
- Travel requires known pack transitions and zaaps; there is no implicit door/zaap recovery or infinite replanning.
- Weight freshness is provenance/revision based, not a wall-clock TTL. A new session creates a fresh `CharacterState`
  with unknown weight.
- A weight event generated before harvest but processed after the post-write barrier cannot be distinguished without a
  server sequence or timestamp; the inventory marker proves only current-revision coverage.
- There is no deterministic runner-level transition-overrun test: the plan-level bound is tested, and the post-travel
  cap is code-checked.
- The pre-existing, unrelated `FightActivity` timing flake remains possible under full-solution load.
- Live server timing, refusals, fights and interruptions have not been exercised. Their observed results are UNKNOWN.

The following supervised scenario is proposed and **NOT LAUNCHED**; it has no GO:

1. Preconditions: owner's explicit GO, Ankama backup and preflight, D340 unchanged, observation only.
2. Run passive `p2j element candidates` on a known tree map; inspect latch, identity, item and action blockers.
3. Run passive `p2j harvest plan <destinationMapId> <wantedItemId>`; compare route, transitions, candidates and pods
   with the actual map. Send no request.
4. Only after a separate explicit one-action GO, use the existing supervised `p2j element use` path for one already
   identified element. Do not use the mission runner.
5. Observe `InteractiveUsedEvent`, `InteractiveUseEndedEvent`, inventory delta and `InventoryWeightEvent`, then stop.
6. Abort on any mismatch, refusal, missing or out-of-order evidence, changed position/session, stale readiness or
   unexpected action. Retry count is zero.
7. Any future mission-runner wiring requires a new decision and a new GO after offline parity; none is proposed here.

## Residuals and scope

- `candidates` counts selected targets (0/1), not every eligible resource.
- Invalid request and `ALREADY_RUN` are reported at `CheckSession`.
- Weight freshness uses provenance and revision, not a wall-clock TTL.
- Visual resource identity is unconfirmed; live identity proof is UNKNOWN.
- The roadmap remains unchanged because it does not list the new passive plan command or internal mission runner; the
  architecture harvesting section now records their scope.
- Worktree: `/home/nill/.paseo/worktrees/17r3bypd/d3b-r1-harvest-chain`, branch `d3b-r1-harvest-chain`, base
  `5215319a0832eef68aac28ed27bb996aafb9b926`. No commit, push, merge or live action was performed.
