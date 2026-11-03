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
