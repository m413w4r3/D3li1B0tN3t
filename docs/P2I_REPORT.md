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

### D2 finding and C4 late-refusal latch

The independent D2 review found a later gap: walk A can return `NoAnswer`; walk B can then be sent and confirmed, clearing
`PendingMovementMapId` and setting `ServerConfirmed`; a delayed refusal for A then matches no pending marker and used to be
ignored. The state stayed trusted and `MovementGuard` allowed another automated action. The wire still cannot identify
whether that refusal belongs to A, B, or client movement.

`CharacterState.MovementAnswerAmbiguous` now latches every server `MapMovementRefusedEvent`. When the current map has a
matching pending marker, C2 behavior remains: a mappable server-named cell is retained with `ServerRefused`, and path,
held-walk state, and pending marker are cleared. Without a matching pending marker, the current cell and provenance stay
untouched for diagnosis. Either way `IsPositionTrusted` becomes false and `MovementGuard` returns
`AmbiguousPosition`, including for `ServerPlaced` and `ServerConfirmed` provenance. Movement confirmations and other
events do not clear the latch. A current-map `MapInformationEvent` that contains a valid self cell restores
`ServerPlaced` and clears it; `MapCurrentEvent` clears both the position and the latch. No timeout or automatic
resynchronization was added.

The controlled `TestPlay` case advances A to `NoAnswer`, sends B, applies B's movement and confirmation, then injects the
late refusal. It asserts that B was trusted before the refusal, the confirmed cell and provenance remain diagnostic
after it, `WalkAsync` returns `Busy` without a third request, and a fresh self placement restores trust.

#### C4 RED evidence

RED used the in-process xUnit runner with class filters for `P2IRefusalCorrelationTests`, `P2HPositionTrustTests`,
`MovementGuardTests`, `CharacterPositionReliabilityTests`, and `CharacterEventsTests`. It ran **72 tests, 10 failed**.
The new end-to-end case failed at `Assert.True()` (`Expected: True; Actual: False`) for the absent ambiguity latch. The
no-pending case failed because `Assert.NotSame()` found the original state instance. The guard case expected
`AmbiguousPosition` but got `None`. The other failures showed that map-current reset, matching and invalid refusals,
confirmations, mapless refusals, and post-map-change refusals did not yet implement the latch.

#### C4 GREEN evidence

- Focused character, guard, correlation, and snapshot classes: **72/72 passed**.
- Full `D3B.Bot.Tests` module, xUnit parallelism disabled: **551/551 passed**.
- `/home/nill/.dotnet/dotnet test --solution D3B.slnx --no-build --max-parallel-test-modules 1`:
  **1,394/1,394 passed**, 0 failed, 0 skipped: App 142, Bot 551, Core 11, Data 133, Engine 257, Network 150,
  Protocol 88, Scripts 35, Storage 27 (+4 Bot tests from C3).
- `/home/nill/.dotnet/dotnet build D3B.slnx`: passed with 0 warnings and 0 errors.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check`: passed.

The default parallel xUnit run hung in the existing `ActivityRunTests.TheNextStepWaitsTheTimeBetweenTwoActions` case.
The full solution command passed after placing a temporary `xunit.runner.json` only in the ignored Bot test output folder
to disable collection parallelism; that file was removed immediately after the run. No tracked test or runner settings
were changed. No live client, production host, Ankama relay, activity, or movement was run.

## Section G — Close the observation failure window and unread translation (C3)

*2026-11-04 — based on C2 `8dd735ffd7b0bc3ba84e5d4083139044559de396`.*

Commits: RED `40fb7f0`; GREEN `d0d15ec`.

### D1 finding and final injection checkpoint

The relay watermark wait alone was not a durable authorization: observation could fail after the wait returned while a
movement sender was still at the bot inbox barrier or waiting for the client's injection gate. `RelayObservationTracker`
now exposes a sticky `IsFailed` state and a `Failed` token. Failure state is set under the tracker lock; token cancellation
runs after that lock is released so cancellation callbacks cannot re-enter the tracker while its gate is held.

`RelayConnection.SendToServerAsync` rejects an already failed tracker and links the caller token with the tracker's failure
token. It gives `InjectedFrames` a fast synchronous validator. `Drain()` checks that validator under the injection gate,
immediately before claiming and writing a queued frame. A failure after the observation wait but before that checkpoint
fails the frame without writing it. A failure after the frame has been claimed is concurrent with an injection that has
already linearized at the checkpoint. Caller cancellation remains an `OperationCanceledException`; tracker failure maps
to `RelayInjectionException`.

### Translation latch

`GameSession` now latches movement authorization closed for its lifetime when a decoded frame has no `GameMessage`
envelope, or carries a type that is not readable (`CarriedType != null && !IsRead`). The latch is checked before and after
the relay observation wait, and again immediately before encoding a movement-capable send. That last check covers a
translation failure discovered during the inbox barrier after the relay wait returned. A fresh `GameSession` starts with a
clear latch; it does not bypass the existing position-trust requirements.

An understood envelope with no carried content does not latch failure. A partial message with readable content
(`IsRead == true`) also does not latch failure. An unknown carried type or bytes that do not parse as `GameMessage` do
latch. This deliberately blocks movement for the rest of the session when the active mapping cannot show the bot all
content the server sent.

### RED evidence

The RED commit `40fb7f0` added the network and bot regressions with only the minimal compile-time surface. The focused
network run had **17 tests, 3 failed, 14 passed**:

- `InjectedFramesTests.AQueuedFrameIsRejectedWhenItsConnectionIsNoLongerObservable` failed with `Assert.Throws()
  Failure: No exception was thrown; Expected: typeof(D3B.Network.Relay.RelayInjectionException)`.
- `RelayObservationTrackerTests.FailingTheTrackerExposesStickyFailureAndCancelsItsToken` failed with `Assert.True()
  Failure; Expected: True; Actual: False` for the failure token.
- `RelayObservationTests.AServerObservationFailureAfterTheFencePreventsLaterInjection` failed with the same
  `Assert.Throws()` / `No exception was thrown` result after the server pump logged an unframed read.

The final 13-case `P2ISendFenceTests` run against the C2 `GameSession` and fake relay had **3 failed, 10 passed**.
`ARelayObservationFailureDuringTheInboxBarrierPreventsMovementInjection`,
`AnUnknownCarriedTypeDuringTheInboxBarrierLatchesFailureForMovement`, and
`BytesWithoutAGameMessageLatchTranslationFailureForMovement` each reported `Assert.Throws() Failure: No exception was
thrown; Expected: typeof(D3B.Network.Relay.RelayInjectionException)`. The understood empty envelope and partial-but-read
cases passed, confirming they must remain allowed.

### GREEN verification

- Focused relay observation, tracker, and injection tests: **17/17 passed**.
- `P2ISendFenceTests`: **13/13 passed**.
- `D3B.Network.Tests`: **150/150 passed**; `D3B.Bot.Tests`: **547/547 passed** in the required full solution run.
- `/home/nill/.dotnet/dotnet build D3B.slnx`: passed with 0 warnings and 0 errors.
- `/home/nill/.dotnet/dotnet test --solution D3B.slnx --no-build --max-parallel-test-modules 1`:
  **1,390/1,390 passed**, 0 failed, 0 skipped. Counts were App 142, Bot 547, Core 11, Data 133, Engine 257,
  Network 150, Protocol 88, Scripts 35, Storage 27. This is +5 Bot and +4 Network tests from C2.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check` passed.

The standalone Bot `dotnet test` invocation did not consistently return a summary in this environment; a detailed standalone
run completed 547/547 once, while later standalone attempts were stopped after no summary appeared. The required full
solution command completed and reported the Bot module at 547/547. No tests were changed to work around this runner
behavior.

### Limits and unpinned admission check

The real `TestRelay` regression proves that the fence can return, a later server-side unframed read can fail observation,
and a subsequent send then fails without reaching the upstream server. The `InjectedFrames` unit test proves a queued
frame whose validator has turned false leaves the writer empty. Bot tests prove the same failure while the inbox barrier is
held, and prove the translation latch for unknown types, malformed game bytes, empty understood envelopes, and partial
readable messages. Several waiters on one tracker watermark complete together.

No separate test pins that ticket admission precedes waiting for `InjectedFrames._gate`. The gated `OnFrame` integration
tests stop after the client pump has acquired that gate; they cannot distinguish whether admission happened just before
or just after it. Exposing a test hook at the private gate boundary would add a production seam solely for this assertion.
The implementation order remains directly visible in `RelayConnectionHandler.PumpAsync`: it calls `Admit()` immediately
after the non-empty `ReadAsync` result and before `await injected.EnterAsync()`.

The watermark still excludes bytes in a socket, transport, or unread pipe. A partial frame is covered only by the read
that completes it. Reads admitted after the watermark are concurrent with movement and outside that cutoff. An unreadable
translation latches only its current `GameSession`; reconnecting clears that latch, while position-dependent actions still
require fresh trusted state under C2. No automatic resynchronization, wire correlation, protobuf, framing, or activity
behavior was added. No live Dofus client, production host, Ankama relay, activity, or movement was run.

## Section H — Close final-review C5 findings

*2026-11-04 — based on C4 `c12903fbe896ac67faa2ee36df143ce4604114f0`.*

Commits: RED `4785466`; GREEN `973ddbd`.

### Held-walk recovery respects refusal ambiguity

A late refusal without a matching pending movement preserves the estimated cell, provenance, and `IsWalkHeld` state for
diagnosis while setting `MovementAnswerAmbiguous`. That state used to pass the held-walk release exception before
`MovementGuard`, allowing the recovery step to escape the position latch. `ReleaseHeldWalkAsync` now returns `Busy`
before finding a path when the latch is set, and `CanSendWalk` independently rechecks the latch at the send guard.
`MapChange`, `Recovery`, `ElementUse`, `Banking`, and `GuildChest` all use this common release path, so none can send a
held-walk step through it after ambiguity. The latch remains until a fresh self placement under C4.

### Unread envelopes latch the session

`GameSession` now latches a frame when `!decoded.IsRead && !decoded.IsUnderstood`. This includes an otherwise valid
envelope with an unknown envelope field that protobuf discards, unknown carried types, and bytes that are not a
`GameMessage`. Understood empty envelopes remain allowed (`IsUnderstood == true`), as do partial messages whose carried
content was read (`IsRead == true`). The latch belongs to one session and blocks its movement-capable sends until that
session ends; reconnecting starts a new latch but still requires trusted position under C4.

### Inbox barrier cancellation

`GameSession.SendAsync` checks cancellation before invoking its movement inbox barrier and waits on that barrier with
`WaitAsync(cancellationToken)`. `BotContext.SendGuardedAsync` likewise checks cancellation and waits on the actor's
`ProcessedAsync` marker with the caller token. Cancellation stops the sender even when the actor remains blocked; the
already queued marker may be processed later. Neither canceled path reaches the connection's send method.

### RED evidence

The RED commit `4785466` added four cases and tightened the ambiguity-reason assertion. The focused command
`/home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --max-parallel-test-modules 1 --filter
'FullyQualifiedName~ARefusalWithoutPendingMovementBlocksHeldWalkRecovery|FullyQualifiedName~AnUnknownEnvelopeFieldLatchesTranslationFailureForMovement|FullyQualifiedName~CancellingWhileTheSessionInboxBarrierIsBlockedStopsPromptly|FullyQualifiedName~CancellingWhileAGuardedSendWaitsForTheInboxStopsPromptly|FullyQualifiedName~AnAmbiguousMovementAnswerOverridesTrustedCellProvenance'`
ran five tests, all failing for the intended behaviors:

- `MovementGuardTests.AnAmbiguousMovementAnswerOverridesTrustedCellProvenance` expected `AmbiguousPosition`, got
  `MapUnknown` when both map and cell were absent.
- `MapChangeTests.ARefusalWithoutPendingMovementBlocksHeldWalkRecovery` expected `Busy`, got `Refused` after a second
  movement request was emitted for held-walk recovery.
- `P2ISendFenceTests.AnUnknownEnvelopeFieldLatchesTranslationFailureForMovement` reported `Assert.Throws() Failure:
  No exception was thrown; Expected: typeof(D3B.Network.Relay.RelayInjectionException)`.
- `P2ISendFenceTests.CancellingWhileTheSessionInboxBarrierIsBlockedStopsPromptly` and
  `P2ISendFenceTests.CancellingWhileAGuardedSendWaitsForTheInboxStopsPromptly` both expected
  `OperationCanceledException` but received `TimeoutException` from the one-second promptness bound.

### GREEN verification

- The same focused command passed **5/5**.
- `/home/nill/.dotnet/dotnet artifacts/bin/D3B.Bot.Tests/debug/D3B.Bot.Tests.dll -parallelMode none -reporter default`
  passed **555/555** in the direct in-process xUnit runner.
- `/home/nill/.dotnet/dotnet test tests/D3B.Network.Tests/D3B.Network.Tests.csproj --no-build --max-parallel-test-modules 1`:
  **150/150 passed**.
- `/home/nill/.dotnet/dotnet test --solution D3B.slnx --no-build --max-parallel-test-modules 1`:
  **1,398/1,398 passed**, 0 failed, 0 skipped: App 142, Bot 555, Core 11, Data 133, Engine 257, Network 150,
  Protocol 88, Scripts 35, Storage 27 (+4 Bot tests from C4).
- `/home/nill/.dotnet/dotnet build D3B.slnx`: passed with 0 warnings and 0 errors.
- `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` and `git diff --check`: passed.

The standalone Bot Microsoft.Testing.Platform command entered its module but did not return a summary; it was stopped.
The in-process xUnit runner with parallel mode disabled completed all 555 Bot tests. The required full-solution serial
Microsoft.Testing.Platform command also completed and reported Bot 555/555. The focused tests and the full solution were
offline; no production host, real client, Ankama relay, activity, or movement was run.

### Limits and status

The admission-before-`InjectedFrames._gate` minor remains documented in Section G: the current `OnFrame` integration
test does not pin which side of that private gate admission happens on, and no production test hook was added. The relay
watermark still excludes bytes in sockets, transports, and unread pipes; fragmented frames enter the guarantee only
when their completing read is admitted. Wire-level refusal correlation remains impossible, so an observed refusal can
still arrive arbitrarily late and leaves position-dependent movement blocked until a fresh placement.

Status: **READY for one supervised automatic move to a neighboring map, subject to a fresh independent review and a NEW
OWNER GO; not executed in P2I.**
