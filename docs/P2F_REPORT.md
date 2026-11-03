# P2F — character position reliability, observability, and simulated travel

**Result: PASS for offline state, navigation, and observability checks.** P2F records the behavior
implemented and verified for client build 3.7.13.17. It includes no live client validation: no game,
host, relay, or client was launched, and no protocol payload, capture, account data, or raw log was
committed.

## A. Character state

CharacterState remains the existing position model. Its nullable MapId and Cell, Path, IsWalkHeld,
and LastWalkEnd do not form a stored provenance enum. The transition logs identify where a position
came from; consumers must not treat every non-null Cell as server-confirmed.

- **MapId — server observed.** MapCurrentEvent supplies the map id and clears the current cell,
  path, and map-scoped state. Map ids are never inferred from movement or a route endpoint.
- **Cell — server observed at placement.** A matching MapInformationEvent places the character only
  from the current character's valid disposition cell.
- **Cell — server confirmed.** MapMovementConfirmResponse confirms the endpoint of a pending
  announced path.
- **Cell — server refused.** A mapped MapMovementRefusedEvent X/Y coordinate names the cell where
  the server left the character. If it cannot be mapped, the cell and path are cleared.
- **Cell — client reported.** A MapMovementCancelRequest names a cell only while a walk is active.
  It is not server confirmation.
- **Cell — estimated.** CharacterEvents.NotConfirmed settles at the last announced path endpoint
  and leaves IsWalkHeld=true; this is estimated while the server may still hold the character.
- **Unknown.** Cell is null after a map change until valid self placement. It is also cleared by
  absent or invalid self placement and an unmappable refusal. An announced path alone never supplies
  the current cell.

MapMovementEvent announces Path and LastWalkEnd, not Cell. A self movement is accepted only when
the character has a known cell and the path starts at that cell. Old-map information is ignored; a
repeated MapCurrentEvent also clears placement. A refusal is applied only when a cell is known, and
a client cancel only during a walk. These guards prevent delayed mapless movement, refusal, or
cancel messages from repopulating an unplaced position. The messages have no map-generation token,
so a stale event that arrives after a new placement and happens to match it cannot be distinguished
by these fields alone.

LastWalkEnd deliberately survives map changes as movement history, based on the measured D85/9g5
behavior. It has no map id and is not the current Cell; MapWalk and MapChange use it when planning
around the previous walk endpoint.

On detach, BotActor clears the session and connected flag but retains the old state snapshot for
inspection. No behavior can run without a session. On the next attach, Bots.Bind creates a fresh
CharacterState.Chosen; a new MapCurrentEvent and matching MapInformationEvent are needed to
establish a current map and cell.

Navigation without both MapId and Cell returns Busy and sends no request. MapChange waits for
MapCurrentEvent and matching map information with a non-null cell before treating a transition as
placed.

Relevant code paths: src/D3B.Bot/Characters/CharacterEvents.cs (MapCurrentEvent,
MapInformationEvent, MapMovementEvent, confirmation, refusal, cancel, and NotConfirmed handlers);
src/D3B.Bot/Characters/CharacterState.cs; src/D3B.Bot/Runtime/BotActor.cs;
src/D3B.Bot/Runtime/Bots.cs; src/D3B.Bot/Movement/WorldTravel.cs;
src/D3B.Bot/Movement/MapChange.cs; src/D3B.Bot/Movement/MapWalk.cs.

## B. Decoding and mapping

The supervised P2E session on 3.7.13.17 recorded 848 server/client game frames: 499 understood,
139 read/partial, and 210 not read. Its separate 349 NotUnderstood warning count overlaps the
partial and unread frame buckets; it is not another frame category. There were no D3B-sent frames.

| P2E message | Count and state | P2F state relevance |
| --- | --- | --- |
| MapCurrentEvent | 4, all understood | Supplies MapId; clears Cell and Path. |
| MapInformationEvent | 4, all read/partial | May supply self placement; P2E aggregate does not show field values. |
| MapMovementEvent | 40, all read/partial | Announces a path; P2F accepts a self path only from a known matching start cell. |
| MapMovementConfirmResponse | 13, all understood | Confirms the endpoint of a pending path. |
| Client MapMovementRequest | 17, all understood | Client request, not a state confirmation. |
| Client MapMovementConfirmRequest | 13, all understood | Client request, not a state confirmation. |

No MapMovementRefusedEvent or MapMovementCancelRequest appeared in P2E. The session therefore
does not demonstrate those P2F state paths or any D3B-initiated movement.

The mapping audit covered 222 messages, 492 readable fields, 29 enums, and 206 enum values:
0 failures, 2 notes, 100 partial messages, and no renumbered fields. There were no unmatched
readable fields. Partial status comes from additional schema fields absent from our readable
descriptors, not from a failed match.

The state-relevant mapping evidence is:

- MapCurrentEvent field 1 (map_id) matched.
- MapInformationEvent fields 2 (stated_elements), 3 (actors), 6 (haven_bag), 7 (interactive_elements),
  10 (map_id), and 12 (sub_area_id) matched. Nested Actor fields 1, 2, and 3 and ActorDisposition
  fields 2 and 3 matched; the event has 10 additional schema fields.
- MapMovementEvent fields 1 (cells) and 4 (actor_id) matched; it has 2 additional schema fields.
- MapMovementRefusedEvent fields 2 (x) and 3 (y) matched; it has 2 additional schema fields.
- MapMovementCancelRequest field 1 (cell_id) matched. MapMovementConfirmResponse has no fields.

GameSession relays original frame bytes unchanged. The shorter readable descriptors do not cause
the live relay to discard extra schema fields, and no protocol correction is indicated. These audit
results establish field-number matches and message-level read states; P2E's aggregate log does not
prove which optional fields occurred in a particular frame, what map or cell the state held, whether
the visible client agreed, or whether a state transition occurred in that run.

Relevant code paths: src/D3B.Protocol/Mapping/ContractAudit.cs,
src/D3B.Protocol/Translation/DecodedGameFrame.cs,
src/D3B.Bot/Network/GameSession.cs, and src/D3B.Bot/Characters/CharacterEvents.cs.

## C. TDD and verification

The state fixes were developed against failing cases first. RED tests exposed stale cells after
invalid placement and unmappable refusal, movement accepted without a current placement or from a
mismatched start, old-map movement restoring a cell, and delayed cancel/refusal repopulating an
unplaced position. GREEN guards clear or reject these cases while preserving valid server refusal,
client stop during a walk, and server confirmation behavior.

P2F added 27 tests to the P2D baseline of 1,265:

- 13 CharacterPositionReliabilityTests for map clearing, placement, stale-event guards, movement
  confirmation, reconnect state, unknown protobuf fields, and navigation without a position.
- 2 WorldTravelSimulationTests for a confirmed multi-map journey and a refused walk.
- 12 observability tests: 8 PositionLoggingTests, 3 GameSessionTests, and 1 GameLogTests.

The integrated D3B.Bot.Tests project is **466/466 passed**. The observability branch's earlier
464-case project count preceded the two navigation tests; it is not the final integrated count.
Final verification with the pinned SDK /home/nill/.dotnet/dotnet, performed outside the Codex
sandbox:

| Check | Result |
| --- | --- |
| dotnet build D3B.slnx | PASS, 0 warnings, 0 errors. |
| Targeted FullyQualifiedName~Character | 115 total, 115 passed. |
| Targeted FullyQualifiedName~WorldTravel | 20 total, 20 passed. |
| Targeted GameSessionTests, GameLogTests, and PositionLoggingTests | 21 total, 21 passed. |
| dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build | 1,292 total, 1,292 passed, 0 failed, 0 skipped. This is 1,265 baseline + 13 state + 2 navigation + 12 observability tests. |
| dotnet format --verify-no-changes --no-restore | PASS, exit 0. |

No live client, host, relay, or game validation is included in these results.

## D. Observability

Position transition messages are emitted at Information when state changes, not for every
unchanged frame. They cover attach with known/unknown position; first, changed, or repeated map;
placement; position becoming unknown; a shown but unconfirmed walk; confirmed or refused walks;
client-reported cancellation; estimated settlement; and detach with the last known position.
Rejected self walks are Information; an ignored cancel is Debug.

Sanitized examples below use placeholders in place of runtime identifiers. The selected lines are
the exact message templates from BotLog:

- Placement: “Bot <character>: placed on map <map> at cell <cell>”
- Confirmed movement: “Bot <character>: walk confirmed on map <map> at cell <cell>”
- Estimated settlement: “Bot <character>: walk settled on map <map> at cell <cell>, estimated while the server may still hold it”
- Unknown position: “Bot <character>: position unknown on map <map> until it is placed”
- Detach: “Bot <character>: game connection <connection> closed; last known map <map> cell <cell>”

Translation diagnostics are deduplicated per GameSession by (side, message, stable category).
The first partial but readable translation is Information; repeats are Debug with a cumulative
count. The first unread/unknown translation is Warning; repeats are Debug with a cumulative
count. A separate key gets a separate first record. Stable categories are “readable payload contains
fields this schema leaves out”, “readable message payload could not be parsed”, “game envelope
length mismatch”, “readable protocol lacks the carried type”, and “malformed game envelope”.
Existing per-frame decode records remain at Debug.

Sanitized examples:

- First partial: “Connection <connection>: the server sent partial readable d3b.game.MapInformationEvent, relayed as it is: readable payload contains fields this schema leaves out” (Information)
- Repeated partial: “Connection <connection>: repeated partial readable translation problem for d3b.game.MapInformationEvent from the server; seen 2 times (readable payload contains fields this schema leaves out)” (Debug)
- First unread: “Connection <connection>: the server sent d3b.game.MapMovementEvent, not understood and relayed as it is: readable protocol lacks the carried type” (Warning)
- Repeated unread: “Connection <connection>: repeated unread translation problem for d3b.game.MapMovementEvent from the server; seen 2 times (readable protocol lacks the carried type)” (Debug)

Position and translation diagnostics contain no payloads, tickets, account identifiers, private
messages, raw protocol bytes, or raw parser problem text. Tests check that a secret marker is not
logged.

Relevant code paths: src/D3B.Bot/Runtime/BotActor.cs, src/D3B.Bot/Runtime/BotLog.cs,
src/D3B.Bot/Network/GameSession.cs, and src/D3B.Bot/Network/GameLog.cs.

## E. Navigation

The smallest public call for one target map is:

    await bot.TravelAsync(targetMapId, cancellationToken);

This BotActor extension enters the serialized behavior, resolves the current map and cell, finds a
world route, and follows its next transition through MapChange. Existing BotContext overloads
support a set of destination maps and an optional stop predicate. With no known map and cell,
navigation returns Busy without sending a request. Map changes require the new server map event
and matching map information with a placed cell.

The existing WorldTravelTests and new WorldTravelSimulationTests pass 20/20. The new
AFirstJourneyFromAPlacedPositionReachesTheTargetOnAConfirmedCell simulation observes an announced
path without advancing Cell, then server confirmation, placements across map changes, and arrival
on the confirmed destination cell. ARefusedWalkLeavesTheJourneyOnTheServerNamedCellAndEndsWithoutAPath
checks that a refusal leaves the server-named cell and sends no follow-up request. The existing
no-position test verifies Busy and no request. No new CLI or production change was needed.

Relevant code paths: src/D3B.Bot/Movement/WorldTravel.cs,
src/D3B.Bot/Movement/WorldPathfinder.cs, and src/D3B.Bot/Movement/MapChange.cs.

**Residual risk — automated movement trust.** WorldTravel gates on MapId, Cell, and IsBusy, but
does not reject IsWalkHeld or a client-reported cancel cell. MapChange awaits
ReleaseHeldWalkAsync but ignores its outcome before continuing. Before allowing automated movement
from a position that is not server-confirmed, the project needs a trusted-position and recovery
policy or an explicit decision accepting this constraint.

P2F recorded two lower risks: a delayed mapless movement event could pass the guards if it arrived
after a new placement and happened to match that cell; and an absent MapCurrentEvent.map_id had no
presence guard and could decode as 0. The mapless movement-announcement risk remains.

P2H's audit follow-up mitigates the stale-refusal case from this warning. MapMovementRefusedEvent
also has no map id, so the character now records the map on which a movement answer is pending and
applies a refusal only when that map still matches the current map. This makes a delayed refusal
after a map change and new placement harmless. The protocol does not correlate refusals with
requests, so one ordered game connection is assumed; a refusal that crosses a newer request on the
same map still cannot be distinguished. The absent MapCurrentEvent.map_id presence warning remains.

P2H's second audit-fix lot adds a `GameSession` send gate before observer callbacks and replaces it when `Bots` binds the
session. The gate blocks movement-capable sends until a bot is attached, permits raw `MapMovementRequest` only while a
fight behavior runs in fight state, and permits raw dialog replies only from a connected, trusted, idle dialog. Raw
`NpcActionRequest` and other map-transition requests are denied; Banking uses a guarded NPC action and both Banking and
`MapChange.ThroughNpcAsync` recheck dialog trust before replies. `SendGuardedAsync` remains internal, bypasses the session
gate after the caller's movement guard, and requires review of every call site. Fight movement permission is scoped to the
active behavior's send call so observers cannot borrow it. The refusal's same-map correlation
ambiguity and the mapless movement-announcement risk remain.

## F. Supervised visual test and reproducible procedure

**Not run during P2F.** The described observation-only visual validation was subsequently executed
and passed in P2G; see [P2G_REPORT.md](P2G_REPORT.md). P2E's GO did not authorize the later session.
The procedure is retained as a reproducible runbook in
[LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md#p2f-visual-validation-procedure). P2G does not authorize
bot movement.

Before the GO is used, close Dofus and confirm no updater, host, or relay is active. Recheck the
installed client build, date, platform, Unity and metadata identity, and binary fingerprint against
[BUILD_IDENTITY.md](BUILD_IDENTITY.md). Verify the staged 3.7.13.17 pack, schema, mapping, pins, and
audit. Confirm the client is not redirected, ports are free, captures are off, Fights:Alone=false,
and no profile, activity, or schedule can act. Make a fresh private backup of the version file.

Build and publish a fresh Linux apphost from the P2F implementation commit
e873b71e09af2beaf54ea60406870df01648a5b9 using the pinned SDK and the procedure in
[RUNTIME_ARTIFACTS.md](RUNTIME_ARTIFACTS.md#publish-the-apphost). Preserve the existing P2D apphost
as a private backup, install the complete P2F publish directory, and verify the launcher and managed
payload identity against the fresh publish output. Record the P2F informational version and hashes
for D3B.Host, D3B.Host.dll, D3B.Network.dll, and D3B.Bot.dll; the old P2D apphost is not a valid
substitute. Keep the apphost without a capability. At the time of the P2F report, its apphost had
not yet been published or installed, so its version and hashes remained for the next GO-gated
preflight to record.

After the GO and all preflight gates pass, start the verified apphost as the normal user with
captures disabled and both position and game-session diagnostics enabled:

    /home/nill/.local/share/D3B/apphost/D3B.Host \
      --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
      --Data:Directory /home/nill/.local/share/D3B/data \
      --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
      --Network:Captures:Enabled false \
      --Fights:Alone false \
      --Network:Address 127.0.13.59 \
      --Network:Ports:0 5555 \
      --Network:Game:Ports:0 5556 \
      --Logging:LogLevel:D3B.Bot.Network.GameSession Debug \
      --Logging:LogLevel:D3B.Bot.Runtime.BotActor Information

The supervisor launches the client and selects the character manually. Compare the visible map and
cell with the host's transition records. A placement line and a confirmed-walk line establish
server-observed/confirmed cells. A shown path is not confirmation. Treat the exact
“walk settled ... estimated while the server may still hold it” message as estimated; wait for a
server confirmation before calling that cell confirmed. A “position unknown ... until it is placed”
line means cell validation is blocked until a valid placement. The detach line records the last
known snapshot and does not prove that it remained current after detachment. Keep detailed logs
private; retain only build identity, result, and non-sensitive aggregate counts.

| Outcome | Criterion |
| --- | --- |
| PASS | The server-observed map and a server-placed or server-confirmed cell match the visible client. |
| BLOCKED | Only an estimated or client-reported cell is available, position remains unknown, decoding fails, the log and client disagree, or any identity/preflight check fails. Stop and preserve only sanitized diagnostics. |

P2F itself made no live or visual validation claim; the later P2G result is recorded in
[P2G_REPORT.md](P2G_REPORT.md).
