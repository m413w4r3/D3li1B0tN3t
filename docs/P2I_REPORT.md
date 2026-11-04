# P2I C1 report

*2026-11-04 — branch `p2i-relay-order-fence`, base `2ac0bd049f076a5a91db473e43768e6d6c49868b`.*

Commits: RED `e2a2b29`; GREEN `3f0f23c5cc01ed6db528c76a68eb58c6f9a2f8a1`.

## Change

Each relayed connection now has one observation tracker shared by its client-to-server and server-to-client pumps.
As soon as `ReadAsync` returns a non-empty buffer, the pump assigns a ticket before the client pump waits for
`InjectedFrames._gate`. A wait snapshots the shared ticket watermark and completes after all tickets up to that snapshot
have returned from `InterceptedSide.Read` with the side still watched. The tracker accounts for reads that complete out
of order and is scoped to one connection.

Movement-capable `GameSession.SendAsync` now follows relay observation, the existing bot inbox marker, the session gate,
and transport injection. `BotContext.SendGuardedAsync` follows its display wait, relay observation, bot inbox marker,
caller guard, pending movement marker, and transport injection. A failed bot inbox handler faults movement barriers.
Cancellation affects only the caller's relay wait. Connection closure, host stop, an unframed stream, a failed
interceptor, or a relay pump ending fails the connection tracker.

The inbox `Processing` marker enqueued after the relay snapshot is the bot-side linearization point. Frames admitted
before the relay snapshot are shown before the marker can complete and are applied before the gate. A frame admitted
after the relay snapshot may still be posted before the marker and then be applied by it. A frame admitted after that
marker is concurrent with the send and outside the guarantee.

## RED evidence

Test/API commit `e2a2b29` (`test(network): add failing P2I relay observation fences`) added the regression cases and the
minimum wait API. Before the implementation:

- The focused bot run used
  `/home/nill/.dotnet/dotnet artifacts/bin/D3B.Bot.Tests/debug/D3B.Bot.Tests.dll -class D3B.Bot.Tests.Runtime.P2ISendFenceTests -maxThreads 1 -noColor`.
- `P2ISendFenceTests`: 4 total, 3 failed, 1 passed. `ARelayObservationFailurePreventsMovementInjection` reported
  `Assert.Throws() Failure: No exception was thrown; Expected: typeof(D3B.Network.Relay.RelayInjectionException)`.
  `ASessionWaitsForRelayObservationBeforeItsInboxBarrierAndGate` expected a pending relay-wait task but got a completed
  send task. `AGuardedSendWaitsForRelayObservationBeforeApplyingItsMovementGuard` expected a pending relay-wait task
  but got a completed behavior task. `NonMovementSendsDoNotWaitForRelayObservation` passed.
- The focused network run used
  `/home/nill/.dotnet/dotnet artifacts/bin/D3B.Network.Tests/debug/D3B.Network.Tests.dll -class D3B.Network.Tests.Relay.RelayObservationTests -maxThreads 1 -noColor`.
- `RelayObservationTests`: 2 total, 2 failed. `TheObservationWaitCoversAnAdmittedClientReadUntilItsFrameIsShown` and
  `TheObservationWaitCoversAnAdmittedServerReadUntilItsFrameIsShown` each reported `Assert.False() Failure; Expected:
  False; Actual: True` while `OnFrame` was deliberately blocked.

The loopback relay tests were run against `TestRelay` only; no external server or client was contacted.

## GREEN verification

All builds used the pinned SDK at `/home/nill/.dotnet/dotnet` (10.0.403).

- `P2ISendFenceTests`: 8/8 passed.
- `RelayObservationTrackerTests`: 8/8 passed, covering multiple frames in one read, fragmented frames, out-of-order
  observations, snapshot limits, independent connections, cancellation, closure failure, and unframed bytes.
- `RelayObservationTests`: 5/5 passed, covering both relay directions, caller cancellation, socket closure during
  observation, and interceptor failure.
- `D3B.Bot.Tests`: 539/539 passed; `D3B.Network.Tests`: 146/146 passed.
- Full serial suite: App 142, Bot 539, Core 11, Data 133, Engine 257, Network 146, Protocol 88, Scripts 35, Storage 27
  — **1,378/1,378 passed**, 0 failed, 0 skipped. This is +8 Bot and +13 Network tests from the 1,357 baseline.
- `/home/nill/.dotnet/dotnet build D3B.slnx` passed with 0 warnings and 0 errors.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check` passed.

The Microsoft Testing Platform `dotnet test` frontend could not complete its local named-pipe handshake in this
environment. The compiled test assemblies were therefore run with their xUnit v3 in-process runner, one thread at a
time, in project order. Loopback tests and formatter/build-host communication required local sandbox elevation.

The focused and project test commands were:

```sh
/home/nill/.dotnet/dotnet artifacts/bin/D3B.Bot.Tests/debug/D3B.Bot.Tests.dll -class D3B.Bot.Tests.Runtime.P2ISendFenceTests -maxThreads 1 -noColor
/home/nill/.dotnet/dotnet artifacts/bin/D3B.Network.Tests/debug/D3B.Network.Tests.dll -class D3B.Network.Tests.Relay.RelayObservationTrackerTests -maxThreads 1 -noColor
/home/nill/.dotnet/dotnet artifacts/bin/D3B.Network.Tests/debug/D3B.Network.Tests.dll -class D3B.Network.Tests.Relay.RelayObservationTests -maxThreads 1 -noColor
/home/nill/.dotnet/dotnet artifacts/bin/D3B.Bot.Tests/debug/D3B.Bot.Tests.dll -maxThreads 1 -noColor
/home/nill/.dotnet/dotnet artifacts/bin/D3B.Network.Tests/debug/D3B.Network.Tests.dll -maxThreads 1 -noColor
```

The full serial suite used this loop, with stdout captured under `/tmp`:

```sh
for project in D3B.App.Tests D3B.Bot.Tests D3B.Core.Tests D3B.Data.Tests D3B.Engine.Tests D3B.Network.Tests D3B.Protocol.Tests D3B.Scripts.Tests D3B.Storage.Tests; do p2i_log_path="/tmp/p2i-serial-${project}.log"; /home/nill/.dotnet/dotnet "artifacts/bin/$project/debug/$project.dll" -maxThreads 1 -noColor > "$p2i_log_path" 2>&1; p2i_status=$?; tail -n 5 "$p2i_log_path"; if [ "$p2i_status" -ne 0 ]; then exit "$p2i_status"; fi; done
```

Final checks were
`/home/nill/.dotnet/dotnet build D3B.slnx`,
`/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore`, and `git diff --check`.

## Limits and follow-up

The relay admits data at non-empty `PipeReader.ReadAsync` results. Bytes still in the socket, transport, or unread pipe
are outside the snapshot. A read containing only a frame fragment is observed as a read, but the frame itself is not
shown until a later read completes it; only that completing read is covered if admitted before the snapshot.

An inbox handler failure latches movement barriers closed for the lifetime of that `BotActor`, preserving fail-closed
behavior when a state update may have been lost. No changes were made to movement-refusal correlation, `CharacterEvents`,
`MovementGuard`, or `MapWalk`; the same-map `MapMovementRefusedEvent` correlation is reserved for the next prompt. No live
Dofus client, production host, Ankama relay, activity, or movement was run.

## Section F — Same-map movement refusals (C2)

*2026-11-04 — based on C1 `3d8190621abd8fa140bb9c7644252fe3fb1e22a9`.*

Commits: RED `2c160e8`; GREEN `9553ff8`.

### Finding and policy

`MapMovementRefusedEvent` contains only x/y coordinates (`src/D3B.Protocol/Game/movement.proto:29`). It has no request
id, map id, cell id, or actor id, and unknown fields are discarded by translation (`game.proto:14,42`,
`ProtocolTranslation.cs:84,142`), so a refusal cannot be correlated formally to the movement whose behavior is waiting.
`CharacterEvents` still applies a refusal only when a movement is pending on the current map; it retains a mapped cell for
diagnostics, clears the path and pending marker, and clears `IsWalkHeld`.

The policy treats `CellProvenance.ServerRefused` as diagnostic evidence only. It no longer makes
`CharacterState.IsPositionTrusted` true, and `MovementGuard` reports `MovementRefusal.AmbiguousPosition`. Server placement
and confirmation remain actionable. A fresh `MapInformationEvent` that places the character on the current map restores
trust as `ServerPlaced`; `MapCurrentEvent` clears the position as before. There is no timeout or automatic resync.

### RED evidence

The focused RED command was:

```sh
/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --filter 'FullyQualifiedName~P2HPositionTrustTests|FullyQualifiedName~MovementGuardTests|FullyQualifiedName~P2IRefusalCorrelationTests' --max-parallel-test-modules 1
```

It failed **3/23** tests (20 passed):

- `MovementGuardTests.ARefusedPositionHasAnAmbiguousGuardReason`: expected `AmbiguousPosition`, actual `None`.
- `P2IRefusalCorrelationTests.ALateSameMapRefusalLeavesThePositionAmbiguousUntilFreshPlacement`: `Assert.False()` expected
  `False`, actual `True` after walk A timed out, walk B was emitted, and its waiter received the late refusal.
- `P2HPositionTrustTests.ARefusalNamesTheCellForDiagnosticsButDoesNotRestoreTrust`: `Assert.False()` expected `False`,
  actual `True` for the refusal-named cell.

The characterization had already asserted that walk B ended as `Refused` and retained the server-named cell before it
failed on the old trusted-position result. `FreshMapInformationRestoresTrustAfterARefusal` passed before the fix.

### GREEN verification

- Focused trust/guard/refusal/travel tests: **25/25 passed** with:

  ```sh
  /home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --filter 'FullyQualifiedName~P2HPositionTrustTests|FullyQualifiedName~MovementGuardTests|FullyQualifiedName~P2IRefusalCorrelationTests|FullyQualifiedName~WorldTravelSimulationTests' --max-parallel-test-modules 1
  ```

- `/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --max-parallel-test-modules 1`:
  **542/542 passed**.
- `/home/nill/.dotnet/dotnet test --solution D3B.slnx --no-build --max-parallel-test-modules 1`:
  **1,381/1,381 passed**, 0 failed, 0 skipped; +3 from C1.
- `/home/nill/.dotnet/dotnet build D3B.slnx`: passed, 0 warnings, 0 errors.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check`: passed.

The deterministic `TestPlay` case advances its fake clock until A returns `NoAnswer`, sends B on the same map, then
delivers a refusal while B is pending. It proves B returns `Refused`, the named cell remains for diagnostics but is not
trusted, a third walk returns `Busy` without another request reaching the connection, and fresh map information restores
`ServerPlaced` trust. The updated world-travel test now expects the journey to stop as `Failed` instead of trying to
plan again from an untrusted refused cell.

### Limits and resumption

The server does not distinguish a late refusal for A from a refusal for B or for a client-issued movement. Refusal
coordinates cannot establish which map or request they refer to. A refusal may arrive arbitrarily late, including while a
later same-map request is pending; there is no timeout that can prove it stale and no formal wire-level correlation.
When a refusal leaves `ServerRefused`, automated position-dependent actions stay blocked until a fresh placement on the
current map, or a new session followed by that placement observation. This policy limits further actions after the
refusal; it cannot prevent a still-later refusal from being mistaken for another request. No protocol, framing, relay,
or movement-refusal correlation semantics were added. No live client, production host, Ankama relay, activity, or
movement was run.
