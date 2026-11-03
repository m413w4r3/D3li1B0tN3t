# P2H: Trust position before automated movement

## Result

**PASS — offline implementation and automated tests. No live client, host, relay, game session, or capture was run.**

The character state now records whether its cell is unknown, client-reported, estimated, server-placed, server-confirmed,
or server-refused. Automated walk, travel, and map-change requests require a connected character on a known map and cell,
not walking or held, out of a fight, and not using an element or in a dialog. A server-derived position is required.

The guard runs against the current state before planning and again after display waits immediately before sending requests.
Refusal returns an existing Busy/Failed outcome and sends no request. A map change clears position provenance; matching
server map information must place the character again before movement can continue.

## Position transitions

- `MapCurrentEvent` clears the cell, path, held-walk flag, and provenance.
- Matching server map information gives a valid self cell `ServerPlaced`; absent or invalid placement clears the cell and
  leaves provenance `Unknown`.
- A self movement announcement records its path without changing the cell or provenance. A server confirmation while
  walking records the path end as `ServerConfirmed`.
- A valid refusal cell is `ServerRefused`; an invalid refusal clears the cell and provenance. A client movement cancel is
  `ClientReported` and cannot authorize automated movement.
- A timed-out walk settles to `Estimated` and held. A late confirmation upgrades that estimated held cell to
  `ServerConfirmed` without inventing a cell. A confirmation without a pending walk otherwise clears only the held flag.
- Entering fight context clears path/held state and provenance while retaining the cell only as history. Returning to
  roleplay does not restore trust. A fresh map event and matching placement are required.
- Disconnect leaves the actor's old snapshot available for inspection. The next attachment starts from the chosen
  character's `Unknown` provenance, and the guard checks the live connection flag separately.

The P2G sequence is covered in order: cancellation at cell 368 becomes `ClientReported` and sends no automated walk;
the manual movement announced from 368 does not advance the cell; server confirmation at cell 434 gives
`ServerConfirmed`; navigation from 434 is then allowed.

## Guard and entry points

`MovementGuard` returns `None` only when the connection, map, cell, action state, and server-derived provenance all pass.
It distinguishes disconnected, unknown map/cell, walking, held walk, fighting, busy, client-reported, estimated, and
unknown-provenance refusals. `CharacterState.IsPositionTrusted` is true only for a known map and cell with server-placed,
server-confirmed, or server-refused provenance. It deliberately does not absorb walking or held state.

`MapWalk.WalkAsync` refuses uncertain, held, or active walks as `Busy`; a public walk no longer attempts recovery.
`WorldTravel` checks every journey iteration before leaving a haven bag, pathfinding, or changing maps. It returns `Busy`
on a first-iteration refusal and `Failed` after previous changes or failures. Map changes guard each walk, transition,
teleport, interactive use, NPC action, and haven-bag request at send time.

## Controlled held-walk release

The sole automatic recovery exception is inside `MapChange`; explicit callers may also request it. The evidence is D85
and D89: D85 measured server-held walks for which map changes, haven-bag exits, and teleports were refused, while a walk
from the server's held cell was followed; D89 measured a step aside releasing a held walk. The held-walk cause was not
identified, so the recovery remains bounded and proven rather than a general retry policy.

Preconditions are a live connection, known map and estimated cell, no active walk, fight, use, or dialog, and
`IsWalkHeld`. The recovery chooses one reachable neighboring cell and sends at most one aside walk. `Arrived` is returned
only if that walk itself arrives and the resulting state has trusted provenance, the same map, a known cell, and neither
walking nor held state. Otherwise it returns `Busy` when proof is missing or preserves the aside walk's exact failure
outcome. It never sends a second movement request from an uncertain position.

`MapChange` requires both `Arrived` and the trusted postcondition before sending any map-change, teleport, door, NPC, or
haven-bag request. Failure logs that no further movement follows and returns `MapChangeOutcome.Busy`. `WorldTravel` and
ordinary `MapWalk` do not invoke this exception.

## Test-first record

Commit 1, `fc84713` (`test(bot): add failing P2H position trust tests`), added the minimal provenance surface and initial
transition/navigation tests without changing transition behavior. The focused test run was:

```sh
/home/nill/.dotnet/dotnet artifacts/bin/D3B.Bot.Tests/debug/D3B.Bot.Tests.dll -class D3B.Bot.Tests.Characters.P2HPositionTrustTests -maxThreads 1 -noColor
```

**RED: 13 total, 11 failed, 2 passed.** The failing tests and assertion messages were:

- `AFightAndRoleplayReturnNeedFreshMapInformationToTrustTheCell`: Expected True, Actual False.
- `AServerPlacementIsTrusted`: Expected ServerPlaced, Actual Unknown.
- `ASettledWalkIsEstimatedHeldAndUntrusted`: Expected Estimated, Actual Unknown.
- `AValidRefusalTrustsTheServerNamedCell`: Expected ServerRefused, Actual Unknown.
- `P2GCancelThenManualConfirmationAllowsNavigationFromTheConfirmedCell`: Expected ClientReported, Actual Unknown.
- `ALateConfirmationOnlyTrustsAnEstimatedHeldCell`: Expected ServerConfirmed, Actual Unknown.
- `AServerConfirmationTrustsTheWalkDestination`: Expected ServerConfirmed, Actual Unknown.
- `AClientCancelIsUntrustedAndAutomatedMovementSendsNothing`: Expected ClientReported, Actual Unknown.
- `AMapChangeClearsProvenanceUntilTheNewMapPlacesTheCharacter`: Expected ServerPlaced, Actual Unknown.
- `AReconnectStartsUnknownAndNavigationSendsNothingUntilPlacement`: Expected True, Actual False.
- `AnAnnouncedMovementDoesNotConfirmItsDestination`: Expected ServerPlaced, Actual Unknown.

Commit 2, `feat(bot): guard automated movement on server-trusted positions`, implements the transitions, send-time guards,
held-walk recovery proof, logs, tests, and this documentation.

## Verification

All checks were offline with SDK 10.0.403. The full solution test run passed **1,321/1,321** (baseline 1,292, delta
+29). The Bot suite passed **495/495**; the focused P2H/movement filter passed **134/134**.

```sh
/home/nill/.dotnet/dotnet restore D3B.slnx --source /tmp/p2h-empty-nuget-source --verbosity minimal -m:1
/home/nill/.dotnet/dotnet build D3B.slnx --no-restore -m:1
/home/nill/.dotnet/dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --filter 'FullyQualifiedName~P2HPositionTrustTests|FullyQualifiedName~MovementGuardTests|FullyQualifiedName~MapWalkTests|FullyQualifiedName~MapChangeTests|FullyQualifiedName~WorldTravelTests|FullyQualifiedName~CharacterEventsTests|FullyQualifiedName~CharacterPositionReliabilityTests|FullyQualifiedName~PositionLoggingTests'
/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore
git diff --check
```

The solution build completed with 0 warnings and 0 errors. Format verification and `git diff --check` exited 0. The
initial sandbox attempt at test-host IPC was denied; the offline checks passed after the workspace approval review
allowed the SDK's local named-pipe communication. No live or visual validation is claimed.

## Residual risks and next step

- No live server movement was exercised. D85/D89 support the bounded recovery, but its behavior on the current client
  build still requires supervised observation.
- `Recovery`, `ElementUse`, `Banking`, and `GuildChest` still ignore the outcome of their explicit
  `ReleaseHeldWalkAsync` call. They are non-movement recovery/action callers and were not changed in P2H.
- Movement events do not carry a map identity. A delayed mapless movement event whose start happens to equal the freshly
  placed cell cannot be distinguished from a current-map event by this state model.
- Movement refusal logging and held-release failure logging are covered offline, not against a live logging session.

The next permissible action is **one supervised automatic move to a single neighboring map**, after the current build is
published and reviewed for that test. It was **not executed** in P2H.

## Audit follow-up

The independent P2H audit found four gaps in the integrated implementation. This follow-up fixes each:

1. **Raw movement sends bypassed the guard.** `BotContext.SendAsync` rejects raw map transitions in every state and raw
   movement unless the current behavior is a fight behavior and the state remains in a fight. It checks before the display
   wait and again just before sending. Production movement and transition sends use `SendGuardedAsync`.
2. **A delayed refusal could be applied to a newly placed map.** `CharacterState.PendingMovementMapId` records the map
   awaiting a movement answer. Client requests and guarded sends set it; map changes, accepted movement, confirmations,
   refusals, cancels, context changes, and walk settlement clear it. A refusal changes position only when the current map,
   current cell, and pending map agree. An ignored refusal leaves position, provenance, and the held flag unchanged.
3. **Element use could transition maps from an untrusted cell.** Generic element use checks `MovementGuard` after held-walk
   release and checks it again through `SendGuardedAsync`, including zero-step uses such as doors. Refusal returns `Busy`
   without sending and cancels both answer expectations.
4. **A stale map-change step could release a held walk first.** Held state now validates the map, cell, pack map, current
   world vertex, step source, and transition range before the recovery release. The state and step are checked again after
   release before planning continues.

The required RED run was **9/9 failed** before behavior changes:

- `ARefusalWithoutAPendingMovementRequestDoesNotChangeThePosition`: expected the placed state to be unchanged; actual
  cell became 311 with `ServerRefused`.
- `AnInvalidRefusalClearsTheCellAndItsTrust`: expected `PendingMovementMapId` null; actual 191104002.
- `AClientMovementRequestMarksTheMapAwaitingItsAnswer`: expected 191104002; actual null.
- `AValidRefusalTrustsTheServerNamedCell`: expected `PendingMovementMapId` null; actual 191104002.
- `ContradictoryEventsAfterAMapChangeCannotRestoreTrustOrACell`: expected newly placed cell 350; actual 311.
- `ABehaviorCannotSendRawMovementFromATrustedPosition` and
  `ABehaviorCannotSendRawMovementFromAClientReportedPosition`: expected `InvalidOperationException`; actual no
  exception, and the request was sent.
- `ACharacterReportedCellCannotUseAnElementWithoutMovementTrust`: expected `Busy`; actual `Refused` after sending.
- `AHeldCharacterWithAStaleStepSourceDoesNotSendAReleaseWalk`: expected one sent request; actual two, including the
  release walk.

GREEN evidence with SDK 10.0.403:

The targeted P2H, position, movement, runtime, and element-use filter passed **176/176**:

```sh
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --filter 'FullyQualifiedName~P2HPositionTrustTests|FullyQualifiedName~MovementGuardTests|FullyQualifiedName~MapWalkTests|FullyQualifiedName~MapChangeTests|FullyQualifiedName~WorldTravelTests|FullyQualifiedName~WorldTravelSimulationTests|FullyQualifiedName~CharacterEventsTests|FullyQualifiedName~CharacterPositionReliabilityTests|FullyQualifiedName~PositionLoggingTests|FullyQualifiedName~ElementUseTests|FullyQualifiedName~BotActorTests' --max-parallel-test-modules 1
```

- `/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --max-parallel-test-modules 1` passed **507/507**, up from 495 (**+12**).
- `/home/nill/.dotnet/dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build` passed **1,333/1,333**, up from 1,321 (**+12**).
- `/home/nill/.dotnet/dotnet build D3B.slnx` passed with 0 warnings and 0 errors.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check` passed.

The remaining protocol assumption is one ordered game connection. A refusal has no map id or request correlation, so a
refusal that crosses a newer request on the same map cannot be distinguished; the pending map only makes cross-map stale
refusals harmless. The prior caveat about a mapless movement announcement matching a newly placed cell also remains.
`Recovery`, `Banking`, and `GuildChest` still ignore explicit held-release outcomes; `ElementUse` now checks movement
trust after release.

No live client or game was run. The next live action still requires a **NEW OWNER GO**, plus publication and review of the
build for that action. The earlier P2H live-action note is not authorization for this follow-up.

## Second audit follow-up: session send gate and NPC continuations

The second independent audit found that the first follow-up still trusted session sends too broadly and could leave a
pending movement marker after a transport failure. This lot closes those paths:

1. **Session send gate.** `GameSession` installs a default gate before any observer callback, so an observer registered
   before `Bots` cannot send movement during session start. `Bots.OnSessionStarted` installs the same default, which blocks
   every movement-capable request until bind. At bind, the session gate becomes `BotActor.CanSendRawRequest`; raw
   `MapMovementRequest` then requires both fight state and a running fight behavior. `NpcDialogReplyRequest` requires a
   connected, trusted, idle dialog state. Raw map changes, teleports, haven-bag requests, interactive uses, and NPC action
   requests remain denied by the session gate.
2. **Guarded movement and NPC policy.** `BotContext.SendAsync` allows raw movement only from a fight behavior while the
   character remains in a fight. It always rejects raw map transitions and `NpcActionRequest`. Banking now sends its NPC
   action through `SendGuardedAsync`; Banking and `MapChange.ThroughNpcAsync` recheck dialog trust before every raw reply,
   cancel their expectations, and return Busy when the state no longer permits it. `SendGuardedAsync` stays internal and
   bypasses the session gate only after each caller supplies its current movement guard; that call-site review is an
   invariant because the method cannot validate a caller's predicate.
3. **Failed-send cleanup.** Once a guarded send posts `PendingMovementMapId`, any transport or cancellation exception
   clears the marker and is rethrown. The false-guard cleanup remains covered.

The test-only commit `80b901c` was RED: `BotActorTests` ran 34 tests, with **9 failed and 25 passed**. The failures showed
raw NPC/map transitions sent from a behavior, roleplay movement allowed during a fight, public observer sends allowed
from roleplay and before bot binding, untrusted dialog replies allowed, and a closed transport leaving the marker at map
`191104002`. The fight behavior positive case passed before the implementation.

The nine failed tests were:

- `AClosedConnectionDuringGuardedSendClearsThePendingMovementMap`: expected a null marker; actual `191104002`.
- `RawMapTransitionRequestsAreProhibitedDuringFight`: expected `InvalidOperationException`; no exception was thrown.
- `AnObserverCannotSendANpcDialogReplyFromAnUntrustedDialogPosition`: expected `InvalidOperationException`; no exception was thrown.
- `RawMapTransitionRequestsAreProhibitedOutsideFight`: expected `InvalidOperationException` for the raw NPC action; no exception was thrown.
- `ARoleplayBehaviorCannotSendRawMovementDuringAFight`: expected `InvalidOperationException`; no exception was thrown.
- `AnObserverCannotSendRawMovementDuringAFightWithoutAFightBehavior`: expected `InvalidOperationException`; no exception was thrown.
- `AnObserverCannotSendRawMovementWhileAFightBehaviorRuns`: expected `InvalidOperationException`; no exception was thrown.
- `AnObserverRegisteredBeforeBotsCannotSendMovementDuringSessionStart`: expected `InvalidOperationException`; no exception was thrown.
- `AnObserverCannotSendRawMovementFromATrustedRoleplayPosition`: expected `InvalidOperationException`; no exception was thrown.

With SDK 10.0.403, the final targeted P2H/movement/runtime/element-use/banking/fight/session filter passed **216/216**.
The final `D3B.Bot.Tests` run reached 519 tests: **508 passed and 11 host tests failed** while starting file watchers,
because the shared environment reached its 128 inotify-instance limit. A preceding full solution run reached 1,344 tests;
1,318 passed and 26 host tests hit the same limit, including a retry with one reported processor. The full solution run
preceded the final active-fight observer regression test. `dotnet build D3B.slnx` passed with 0 warnings and 0 errors.
The final format verification and `git diff --check` passed. No live client, host, relay, game, or capture was run.

The RED command and final verification commands were:

```sh
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --filter 'FullyQualifiedName~D3B.Bot.Tests.Runtime.BotActorTests' --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --filter 'FullyQualifiedName~P2HPositionTrustTests|FullyQualifiedName~MovementGuardTests|FullyQualifiedName~MapWalkTests|FullyQualifiedName~MapChangeTests|FullyQualifiedName~WorldTravelTests|FullyQualifiedName~WorldTravelSimulationTests|FullyQualifiedName~CharacterEventsTests|FullyQualifiedName~CharacterPositionReliabilityTests|FullyQualifiedName~PositionLoggingTests|FullyQualifiedName~ElementUseTests|FullyQualifiedName~BotActorTests|FullyQualifiedName~BankingTests|FullyQualifiedName~FightPlayTests|FullyQualifiedName~GameSessionTests' --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet build D3B.slnx
/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore
git diff --check
```

The full solution test command was run exactly as requested, then retried with one reported processor; both attempts
reported 1,318 passed and 26 failures from the shared inotify-instance limit:

```sh
/home/nill/.dotnet/dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build
DOTNET_PROCESSOR_COUNT=1 /home/nill/.dotnet/dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build
```

The ordered-connection assumption remains: refusals have no request correlation, so a refusal crossing a newer request on
the same map is still indistinguishable. A mapless movement announcement can still match a newly placed cell.
