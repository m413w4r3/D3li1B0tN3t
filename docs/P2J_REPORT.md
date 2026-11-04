# P2J — one supervised map transition, prepared and observed

**Status: PREPARED; GO 1 executed on 2026-11-04 — observation PASS, movement BLOCKED.** No
movement was executed. The single movement authorization was not used. GO 1 (observation) and GO 2
(one move) remain separate owner gates. The live passive result is recorded in
[P2J_GO1_REPORT.md](P2J_GO1_REPORT.md); this report keeps the preparation evidence.

## A. Git and build identity

| Item | Value |
| --- | --- |
| Repository | `/home/nill/perso/dofus/bot` |
| Base | `9d33c1fae445d665b0719add6d410e931ba4182d` (`master`, verified on `origin/master`) |
| P2J branch | `p2j-supervised-move` (local, not pushed) |
| P2J code commit | `bef5066d0c17cb2d89d9ef0d5fd98185337c0449` |
| P2J informational apphost version | `0.0.0-alpha.0.56+bef5066d0c17cb2d89d9ef0d5fd98185337c0449` |
| Installed client | `3.7.13.17`, build date `2026-11-03T14:11:23+00:00` |
| Client binary identity | `Dofus.x64` `6d7b6348…`, `GameAssembly.so` `f2aa597d…`, `UnityPlayer.so` `b01fc32d…`, `global-metadata.dat` `dea17c3c…`, metadata v39 |
| Staged runtime | `data-8.pack` `28db5da4…`, `schema-1.binpb` `e383c947…`, `mapping-15.binpb` `f42deadf…` |
| P2J launcher | 78,192 B, SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0` |
| P2J `D3B.Host.dll` | 5,632 B, SHA-256 `b4c48761cecec769e314d9197eb5fd99842c72c5cb53f913eefab83c0f0da39e` |
| P2J `D3B.Network.dll` | 118,272 B, SHA-256 `6789048fb7befaa29b00d0b4f4d543d9e0998d884c4513178140e8f4921496b0` |
| P2J `D3B.Bot.dll` | 784,896 B, SHA-256 `3debd3ce1d8522a0363d06ca85d642d43e8c709266216e8e9e4ea71ec48df8fa` |
| Previously installed apphost | P2F `0.0.0-alpha.0.17+e873b71…`; backups `~/.local/share/D3B/private-backups/apphost-p2f-20261104T173953Z` and `apphost-p2f-live-20261104T180525Z` (369 files each, manifests verified) |

The P2I code is present in the state to publish: `master` contains the C5 commits `0190fc2` and
`973ddbd`, the `GameSession` translation latch, `RelayObservationTracker`, the refused/ambiguous
position state, and their tests. The baseline before P2J changes was **1,398/1,398**, matching the
mission's expected count.

The client identity and the staged `3.7.13.17` artifacts were rechecked read-only before any code
change; the three staged files matched their recorded SHA-256 values and private permissions. No
launcher update occurred during this mission. The client and its configuration were not modified,
and Dofus was not started.

## B. P2I translation latch and P2E's 210 unread frames

### The latch condition is exactly P2E's "not read" bucket

`GameSession.OnFrame` latches the session when

```csharp
if (!decoded.IsRead && !decoded.IsUnderstood)
```

(`src/D3B.Bot/Network/GameSession.cs:222`), and logs a frame's state with

```csharp
var state = decoded.IsUnderstood ? "understood" : decoded.IsRead ? "read" : "not read";
```

(`src/D3B.Bot/Network/GameSession.cs:235`). The two conditions are the same. P2E recorded
**210 not read** frames out of 848 (`docs/P2E_REPORT.md:14`, `:32`). Therefore, **all 210 would
have latched the P2I movement authorization had the latch existed in that binary**. This is a
derived fact from code equivalence, not a re-run of P2E.

The latch is sticky for the lifetime of that `GameSession`; a new session starts clear, but the
position-trust requirements of P2H/C4 remain. It is checked before the relay observation wait, after
it, and again immediately before a movement-capable send. A movement frame still queued behind the
relay's injection gate now carries a cancellation token linked to the latch, so it is withdrawn when
the latch closes before the relay claims it for writing. A latch closure concurrent with the
claim/write itself can still race the last frame, as any concurrent cancellation can; the
one-shot bound and the target-placement verification are unaffected.

### Did they occur before the character was placed?

**UNKNOWN from the retained P2E evidence.** P2E kept aggregate counts only; its report explicitly
does not preserve per-frame order or placement timing (`docs/P2E_REPORT.md:32`, `:45-47`). The
aggregate does not break the 210 unread frames down by direction or name them individually; the
latch applies to either direction. With 210 unread frames in a 4 min 27 s session, the risk that at
least one precedes GO 2 cannot be excluded, but the retained evidence cannot quantify or prove it.

A new passive diagnostic answers the question live without sending movement. `BotActor` records
whether the session's latch was already closed when the first trusted placement frame was shown to
it (`src/D3B.Bot/Runtime/BotActor.cs`, `TranslationBlockedAtFirstPlacement`). Readiness reports:

- `true` — the latch was already closed when the first trusted placement was observed (an unread
  frame was seen before or concurrently with that placement; the exact wire order of concurrent
  pump reads is not retained);
- `false` — the first placement was observed while the latch was open; any later closure happened
  after placement;
- `unknown` — no trusted placement has been observed yet.

`p2j readiness` prints this value with `translation_blocked`, without sending a frame. GO 1 must
show `translation_blocked=false` before GO 2 is allowed. If it is `true`, P2J is BLOCKED on the
current mapping and the fail-closed latch must not be weakened to force the attempt.

### Guard refusal versus session translation failure

`MovementReadiness` reports two independent fields:

- `guard` — `MovementGuard`'s position/activity refusal (`CellUnknown`, `WalkHeld`, `Fighting`,
  `AmbiguousPosition`, ...);
- `translation_blocked` — the P2I session latch.

A trusted position with a closed latch has `guard=None` and `translation_blocked=true`; an untrusted
position with an open latch has the opposite. The move's reason code prefers the translation latch,
because no send can be authorized at all. Tests cover both directions
(`SupervisedMoveTests.ATranslationFailureAfterPlacementBlocksTheMove`,
`UnknownPositionBlocksTheMove`).

## C. The trigger and its scope

### Entry point

The only production entry point is a two-command line reader on the host's standard input:

- `p2j readiness` — passive readiness only;
- `p2j readiness <sourceMapId> <targetMapId>` — passive readiness plus plan check, no consumption;
- `p2j move <sourceMapId> <targetMapId>` — the one explicit authorization.

It is registered only by `D3B.Host` (`src/D3B.Host/Program.cs`) when
`--Supervised:Move:Enabled true` is present. `SupervisedMoveModule` is not part of the default
modules, so the desktop app never reads standard input. Disabled by default; no connection event,
observer, timer, scheduler, web server, or remote API starts it. The reader never reacts to a
connection: it changes state only after a line naming both maps.

### Checks before any send

`SupervisedMove.Plan` requires, in order:

1. positive, distinct source and target map identifiers;
2. a connected, attached, non-paused, non-acting session with no haven bag;
3. `MovementGuard.Refuse == None` on a fresh snapshot (server-placed or server-confirmed cell);
4. the session translation latch is open;
5. the current map equals the explicit source;
6. the target is one of the source map's four `MapData` neighbours, has a world-graph vertex, and a
   one-step graph path exists;
7. the step is a plain `WorldTransitionType.Scroll`, not a teleport, NPC way, door, map action or
   scroll with an action, and its requested map equals the target;
8. at least one exit cell is available.

The path search is run with all non-scroll transitions banned, zaaps disabled, and every NPC way
banned. `WorldTravel` is not used, so no multi-map journey can be started.

### One transition, once

Execution calls `MapChange.ChangeMapAsync` exactly once with the prevalidated `WorldStep`. A plain
scroll path sends at most one walk to the edge and one `MapChangeRequest`; it never calls the map
action or door branches, and it never releases a held walk because `MovementGuard` refuses one.

`SupervisedMoveSession` consumes the move authorization with `Interlocked.Exchange` before any
check. A first `move` that is BLOCKED, FAILED or INTERRUPTED still consumes it; every later `move`
returns `ALREADY_CONSUMED` with no request. A new attempt therefore requires restarting the host
under a new explicit owner GO, not a retry in the same process.

### Verdict

- **PASS** only when `MapChangeOutcome.Changed`, the state map equals the target, the cell is known,
  and provenance is `ServerPlaced` or `ServerConfirmed` with `IsPositionTrusted`;
- **FAILED** when a request went but the move ended elsewhere, timed out, was refused, was not
  placed, or ended untrusted;
- **BLOCKED** when no map-change request went (readiness, plan, consumption, busy, invalid plan);
- **INTERRUPTED** on player action, fight, disconnect, host stop, or cancellation.

The result carries source/target, start map/cell/provenance, transition type/index, planned exit
cell, end map/cell/provenance, and end trust. No payload, ticket, account or connection identifier
is printed.

## D. TDD and the one-transition proof

Added 32 focused movement tests, 2 module tests, and 1 P2I queued-send regression test:

- passive readiness and no connection-triggered action;
- empty console input and `readiness` send nothing;
- unknown cell, held/estimated walk, fight, disconnect;
- translation latch before and after first placement;
- non-neighbour target, door target, map-action target, map-action on the approach path, haven
  bag, last-walk-end-only exit;
- one scroll PASS with target placement;
- walk + one map change, exactly two requests at most;
- unexpected destination, walk refusal, timeout;
- second request after success, after timeout, after a first BLOCKED request, and two concurrent
  move requests;
- cancellation, player action, disconnect, fight and host stop during the move;
- a movement frame queued behind the relay gate is withdrawn when the translation latch closes;
- console PASS then `ALREADY_CONSUMED`;
- reader disabled by default and registered only when enabled.

The tests assert the requests actually recorded by `TestConnection.Sent`, not only returned results:
`MapChangeRequest` count is at most one, `MapMovementRequest` count is at most one for the edge
walk, and blocked paths leave the connection with zero D3B sends. No additional simulator was
added: the tests use `TestPlay`, `TestConnection`, and `TestWalks`.

Verification with the pinned SDK:

| Check | Result |
| --- | --- |
| `dotnet build D3B.slnx` | PASS, 0 warnings, 0 errors |
| `SupervisedMoveTests` | 32/32 |
| `SupervisedMoveModuleTests` | 2/2 |
| Full serialized suite | **1,433/1,433**, 0 failed, 0 skipped (baseline 1,398 + 35) |
| `dotnet format --verify-no-changes --no-restore` | PASS, exit 0 |
| `git diff --check` | PASS, exit 0 |

No live client, host, relay, Ankama connection, activity, or movement was involved.

## E. Publication, installation and fingerprints

The apphost was published from a detached worktree at the exact P2J code commit
`bef5066d0c17cb2d89d9ef0d5fd98185337c0449` with the pinned SDK:

```sh
git worktree add --detach /tmp/p2j-apphost/source bef5066d0c17cb2d89d9ef0d5fd98185337c0449
/home/nill/.dotnet/dotnet publish src/D3B.Host/D3B.Host.csproj \
  --configuration Release --runtime linux-x64 --self-contained true \
  -p:UseAppHost=true --artifacts-path /tmp/p2j-apphost/build \
  --output /tmp/p2j-apphost/publish --verbosity minimal
```

The publish is 369 files. Its launcher is a stripped x86-64 ELF PIE, 78,192 B, SHA-256
`de956557…`; `D3B.Host.dll` 5,632 B `b4c48761…`, `D3B.Network.dll` 118,272 B `6789048f…`,
`D3B.Bot.dll` 784,896 B `3debd3ce…`; informational version
`0.0.0-alpha.0.56+bef5066…`.

The P2F apphost was preserved before installation at
`~/.local/share/D3B/private-backups/apphost-p2f-20261104T173953Z`, and the live directory was moved
to `apphost-p2f-live-20261104T180525Z` during installation. Both have 369 files and verified
manifests. The P2J publish was staged under the private root, its manifest compared byte-for-byte
with the publish output, then renamed into `/home/nill/.local/share/D3B/apphost` as one set. The
installed directory is `0700`, the launcher `0700`, the payload `0600`, owner `nill`; the installed
manifest is identical to the publish output; `getcap` reports no capability. No host, relay, client,
or movement was started during publication or installation.

## F. Supervised runbook

The reproducible GO 1 / GO 2 procedure is in [LINUX_ACCEPTANCE](LINUX_ACCEPTANCE.md#p2j--one-supervised-map-transition-prepared-not-executed).
GO 1 is observation-only: it checks identity, placement, trust and the translation latch, and sends
no movement. GO 2 is allowed only after GO 1 passes and the owner gives a separate GO; it triggers
exactly one `p2j move` and stops on any unexpected outcome.

## G. Verdict

**READY FOR GO 1 ONLY.** The code, tests and device are prepared for one supervised transition.
An independent read-only review (GPT-6 Luna xhigh) found no path to a second transition and
confirmed the one-shot bound. It found one MEDIUM inherited queued-send race; the send now carries a
token linked to the translation latch and `InjectedFrames` registers cancellation before releasing
the gate, closing the finding apart from the inherent claim/write instant, which is documented.
GO 2 is conditional on GO 1 showing:

- `connected=true`, `session=true`, `guard=None`, `trusted=true`;
- `translation_blocked=false` at the end of observation;
- `p2j readiness <source> <target>` returning `PLAN_READY` with a plain `Scroll`;
- a visible source/target pair matching the host's map/cell.

If GO 1 reports `translation_blocked=true`, P2J is **BLOCKED** on the current mapping: the 210 P2E
unread frames would have latched the session, and the fail-closed behaviour must not be relaxed.

**GO 1 result (2026-11-04):** the live passive session did report `translation_blocked=true` and
`translation_blocked_at_first_placement=true`; no movement was attempted and the relay sent 0
frames from D3B. The verdict is **BLOCKED for GO 2**; see [P2J_GO1_REPORT](P2J_GO1_REPORT.md) for
the first message type, aggregates, restoration and the proposed TDD correction.

## Limits

- The preparation itself ran no live execution; GO 1 later ran a live **passive** session (see
  [P2J_GO1_REPORT](P2J_GO1_REPORT.md)). No movement and no push occurred.
- The `SupervisedMove` engine and its readiness/plan/result records are internal to `D3B.Bot`; the
  only production entry point is the internal session behind the console. No host or app service
  can bypass the one-shot consumption. No other production caller exists.
- The latch is per game session; a reconnect clears it but still requires fresh server placement.
- The read of `TranslationBlockedAtFirstPlacement` proves only the state at the first trusted
  placement, not the exact frame order inside the relay.
- The target must be one of the four map-data neighbours with a plain scroll; other direct
  transitions are deliberately refused for this first attempt.
