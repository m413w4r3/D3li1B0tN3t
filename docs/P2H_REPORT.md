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
`191104002`. The fight behavior positive case passed before the implementation. The parent reproduced this RED run
independently from a detached worktree: 34 total, 9 failed, 25 passed.

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
The agent's own full-suite attempts hit the shared environment's 128 inotify-instance limit (519 Bot tests: 508 passed,
11 host tests failed; a preceding solution attempt: 1,318 passed, 26 host tests failed). The parent then ran every test
project serially on the integrated tree and all passed: `D3B.Bot.Tests` **519/519**, `D3B.App.Tests` **142/142**,
`D3B.Engine.Tests` **257/257**, `D3B.Data.Tests` **133/133**, `D3B.Network.Tests` **133/133**, `D3B.Protocol.Tests`
**88/88**, `D3B.Scripts.Tests` **35/35**, `D3B.Storage.Tests` **27/27**, and `D3B.Core.Tests` **11/11** — **1,345/1,345
passed, 0 failed, 0 skipped** (1,292 baseline, +53). `dotnet build D3B.slnx` passed with 0 warnings and 0 errors. The
final format verification and `git diff --check` passed. No live client, host, relay, game, or capture was run.

The RED command and final verification commands were:

```sh
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --filter 'FullyQualifiedName~D3B.Bot.Tests.Runtime.BotActorTests' --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --filter 'FullyQualifiedName~P2HPositionTrustTests|FullyQualifiedName~MovementGuardTests|FullyQualifiedName~MapWalkTests|FullyQualifiedName~MapChangeTests|FullyQualifiedName~WorldTravelTests|FullyQualifiedName~WorldTravelSimulationTests|FullyQualifiedName~CharacterEventsTests|FullyQualifiedName~CharacterPositionReliabilityTests|FullyQualifiedName~PositionLoggingTests|FullyQualifiedName~ElementUseTests|FullyQualifiedName~BotActorTests|FullyQualifiedName~BankingTests|FullyQualifiedName~FightPlayTests|FullyQualifiedName~GameSessionTests' --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet build D3B.slnx
/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore
git diff --check
```

The agent's solution-level attempts were limited by the shared inotify-instance environment, not by a test failure. The
parent verified the integrated tree by running every test project serially instead:

```sh
for p in tests/D3B.*.Tests; do /home/nill/.dotnet/dotnet test "$p" --no-build --max-parallel-test-modules 1; done
```

The serial per-project run passed **1,345/1,345**, 0 failed, 0 skipped, as listed above. The solution-level
`dotnet test --solution` command stalled in environment-level orchestration while 185 inotify instances were open for a
maximum of 128; it was stopped, and the equivalent serial run passed.

The ordered-connection assumption remains: refusals have no request correlation, so a refusal crossing a newer request on
the same map is still indistinguishable. A mapless movement announcement can still match a newly placed cell.

## Third audit follow-up: session identity and inbox barrier

The third independent audit found two remaining authorization gaps in the second follow-up. Its session gate read
bot-wide state without checking which session invoked it, and actor state could lag behind frames already posted to the
bot's inbox. This follow-up closes both paths. D295 remains the historical record of the second lot; the session gate
description there did not yet guarantee session identity or inbox ordering.

1. **Session identity.** `BotActor._session` is volatile because sender threads read it. The send authorization receives
   the invoking `GameSession`. A non-current session may send non-movement requests, but every movement-capable request
   from it is denied. `Bots.Bind` captures that exact session in its gate, so an older open session cannot borrow a newer
   session's trusted dialog or fight state.
2. **Inbox barrier.** `Bots.Bind` gives the session `BotActor.ProcessedAsync` as its barrier. A movement-capable
   `GameSession.SendAsync` awaits it before checking the session gate; non-movement requests skip it. Guarded sends await
   the same actor processing point after the display wait and before the movement guard. The order remains barrier,
   authorization, pending movement marker post, then transport send. `BotContext.SendAsync` relies on the session barrier
   and does not add a second one.
3. **Deterministic regression coverage.** Two open sessions prove the older dialog reply is rejected on the older
   connection. A blocking expectation holds the actor while a map change is posted, then starts a guarded send; it waits
   for processing and refuses without sending. Separate tests assert the bound barrier, barrier-before-gate ordering,
   and no barrier for non-movement sends.

The test-only commit `c37bd2b` compiled and ran RED: 5 tests, 4 failed and 1 passed. The failures were the stale-session
reply, queued map change, missing binding barrier, and missing barrier-before-gate ordering. The test-only first commit
added only the internal barrier property needed to compile those tests; it did not perform synchronization.

GREEN evidence with SDK 10.0.403:

- The focused P2H/movement/runtime/element-use/banking/fight/session set passed **221/221**, including the five new tests.
- `D3B.Bot.Tests` passed **524/524** (519 previous + 5).
- The nine projects passed serially: Bot 524, App 142, Engine 257, Data 133, Network 133, Protocol 88, Scripts 35,
  Storage 27, and Core 11 — **1,350/1,350**, 0 failed, 0 skipped (1,345 previous + 5).
- `/home/nill/.dotnet/dotnet build D3B.slnx --no-restore -m:1` passed with 0 warnings and 0 errors after restoring
  only from the local NuGet cache with package auditing disabled.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check` passed.

The environment denied the Microsoft Testing Platform's named-pipe handshake during the agent's run, so its focused and
full project runs used the local xUnit v3 in-process runner directly, sequentially, instead of `dotnet test`. The parent
reproduced the integrated result with `dotnet test` on the merged tree: the focused set passed 221/221,
`D3B.Bot.Tests` passed 524/524, and the nine projects run serially passed 1,350/1,350, 0 failed, 0 skipped. The parent
also reproduced the RED commit `c37bd2b` at 5 total, 4 failed, 1 passed; the build passed with 0 warnings and 0 errors;
formatting and `git diff --check` passed. No test data, captures, client, host, relay, or game were used.

The barrier covers every frame already posted to the bot inbox. A frame still held by the relay's read loop has not yet
reached the inbox and can arrive after the barrier; the gate therefore cannot guarantee synchronization with an
in-flight relay frame. The older same-map refusal-correlation and mapless movement-announcement risks remain. The next
live action still requires a **NEW OWNER GO** and publication and review of the build for that action.

## Fourth audit follow-up: object use, self-walk requests, and stop lifecycle

The fourth independent audit found three remaining first-party gaps. This bounded lot closes each:

1. **Generic object use.** `ObjectUseRequest` and `ObjectUseMultipleRequest` are movement-capable. `UseObjectsAsync`
   checks `MovementGuard` after inventory and busy checks, and the bound session gate permits these requests only for a
   connected, trusted, idle roleplay position. The local check logs refusal and returns `Busy`; the session gate remains
   the send-time backstop. This covers map-transition items such as teleportation potions.
2. **Self movement announcement.** `CharacterEvents` accepts a self `MapMovementEvent` only when the current map is
   known, the marker from a client `MapMovementRequest` or guarded send names that current map, and the path starts at the
   current cell. Acceptance clears the marker. A delayed mapless announcement after map change and new placement cannot
   create a path or be promoted by a following confirmation. Group and other-actor paths are unchanged.
3. **Stop lifecycle.** `DisposeAsync` clears `_session` and `_connected` before it posts `Stopping` and completes the
   inbox. `OnStopping` remains idempotent. A deterministic regression holds the actor loop while disposal closes the
   inbox, then verifies a concurrent movement-capable session send is rejected before the stop message is processed.

The test-only commit `6e9fc8d` compiled and ran RED: **3/3 failed**. The behavior-level object use returned `Used` instead
of `Busy`; direct session `SendAsync` did not reject object use from a client-reported cell; and a delayed mapless walk
created a path. The test-only commit changed no production behavior.

GREEN evidence with SDK 10.0.403:

- The focused P2H/movement/runtime/element-use/banking/fight/session/inventory filter passed **228/228**.
- `D3B.Bot.Tests` passed **528/528**, up four tests from the 524 baseline.
- The nine test projects passed serially: App 142, Bot 528, Core 11, Data 133, Engine 257, Network 133, Protocol 88,
  Scripts 35, and Storage 27 — **1,354/1,354**, 0 failed, 0 skipped.
- `/home/nill/.dotnet/dotnet restore D3B.slnx --source /tmp/p2h-empty-nuget-source --verbosity minimal -m:1 -p:NuGetAudit=false`
  restored from the local package cache, and `/home/nill/.dotnet/dotnet build D3B.slnx --no-restore -m:1` passed with 0
  warnings and 0 errors. Restoring builds attempted NuGet vulnerability metadata and failed offline with NU1900; the
  literal `/home/nill/.dotnet/dotnet build D3B.slnx` also exited without project output or reported errors here.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check` passed.

The bot boundary is closed for the tested paths. The strict end-to-end current-position criterion still needs a
relay-level inbound ordering barrier: a frame still in the relay read loop may be posted after `ProcessedAsync` returns.
The protocol also gives movement refusals neither a map id nor request correlation, so a same-map refusal crossing a
newer request remains ambiguous. These are the explicit remaining follow-ups; this lot does not attempt either. No live
client, host, relay, game, or capture was run. The next live action still requires a **NEW OWNER GO** and publication and
review of the build for that action.
