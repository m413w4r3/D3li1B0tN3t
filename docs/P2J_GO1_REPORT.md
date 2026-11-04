# P2J GO 1 — passive session and translation latch

**Status: GO 1 observation PASS, movement BLOCKED.** A real supervised session was observed on
Dofus Linux `3.7.13.17` with the P2J apphost. The translation latch closed before the first
placement; `p2j readiness` returned `can_move=false`, so no movement was attempted and GO 2 was not
requested. The relay reports **0 frames from D3B**. The session was stopped and the client restored.
This report is sanitized: no payload, ticket, account, character or connection identifier is kept.

## A. Connection and build identity

| Item | Value |
| --- | --- |
| Client | `3.7.13.17`, build `2026-11-03T14:11:23+00:00`, Unity `6000.3.16f1`, IL2CPP metadata v39 |
| Client binaries | `Dofus.x64` `6d7b6348…`, `GameAssembly.so` `f2aa597d…`, `UnityPlayer.so` `b01fc32d…`, metadata `dea17c3c…` |
| Apphost | P2J `0.0.0-alpha.0.56+bef5066…`, 369 files, launcher `de956557…`, no capability |
| Runtime data | pack `28db5da4…`, schema `e383c947…`, mapping `f42deadf…` |
| Modules | 8, including `SupervisedMoveModule` |
| Session duration | `07:33.2400841` |
| Relay traffic | 2,259 bytes from the client, 117,482 from the server, **0 frames from D3B** |

Connection, authentication, character selection and the game session completed. Captures stayed
disabled and `Fights:Alone=false`.

## B. Observed position

- First server placement: map `191105028`, cell `260`, provenance `ServerPlaced`, `trusted=true`.
- The supervisor then moved the character manually to map `191105026`, cell `552`; the last known
  position on connection close was map `191105026`, cell `552`.
- The host emitted no movement request. Visual coherence with the client is the supervisor's
  observation, not inferred from the protocol alone.

## C. Translation latch

Last `p2j readiness`:

```text
connected=true session=true translation_blocked=true
translation_blocked_at_first_placement=true paused=false acting=false haven_bag=false
guard=None map=191105026 cell=552 provenance=ServerPlaced trusted=true
can_move=false reason=TRANSLATION_LATCH_CLOSED
```

The latch is sticky for the session and was already closed when the first trusted placement was
observed. No attempt was made to reset or bypass it.

## D. Messages responsible for the closure

The first unread frame is **server `.lok`**, 36 bytes, before the first placement (frame 114 of the
private log; first placement at frame 666).

- Build schema: field 1 `int64 geei`, field 2 `int32 geej`.
- External BillBot protocol mapping, with identical field names, identifies it as
  `com.ankama.dofus.server.game.protocol.basic.TimeEvent` (`timestamp`, `timezone_offset`). This is
  a non-position time-synchronisation message, seen after `AuthenticationAcceptedEvent` and again
  after `MapCurrentEvent`. The external identification is strong evidence but is not a live
  semantic proof; it must be confirmed by the TDD mapping below.

Session aggregates:

| State | Server | Client |
| --- | ---: | ---: |
| `understood` | 70 | 12 |
| `read` / partial | 163 | 0 |
| `not read` | 136 | 37 |

- **173 unread frames**, **89 distinct types**; 132 unread frames before the first placement.
- Every unread frame is a carried type absent from the readable protocol
  (`readable protocol lacks the carried type`).
- **0 malformed envelopes** and **0 parse failures** were observed.
- Partially readable messages (`MapInformationEvent`, `MapMovementEvent`, `MapActorShownEvent`,
  `CharacterStatsEvent`, `InventoryContentEvent`, …) are `read`; they do not latch even though the
  schema leaves fields out.

Most frequent unread types:

| Side | Type | External name (indicative) | Frames |
| --- | --- | --- | ---: |
| server | `.jxh` | FightTeamUpdateEvent | 13 |
| server | `.kpx` | ChannelUpdateEvent | 13 |
| server | `.knb` | PongEvent | 12 |
| server | `.ixe` | HousePropertiesEvent | 8 |
| server | `.icj` | QuestStepInformationEvent | 7 |
| server | `.hmp` | RemoveChallengeEvent | 4 |
| server | `.jpp` | MapFightCountEvent | 4 |
| server | `.kna` | QueueStatusEvent | 4 |
| server | `.ira` | SetUpdateEvent | 3 |
| server | `.kjm` | ContextRefreshEntityLookEvent | 3 |
| server | `.lok` | TimeEvent | 3 |
| client | `.kne` | PingRequest | 12 |
| client | `.jgm` | GuildInformationRequest | 3 |
| client | `.jpe` | MapInformationRequest (external; **to confirm**, our pin is `.kjt`) | 2 |
| client | `.lnt` | SequenceNumberRequest | 2 |

The other 68 types are singletons, grouped by family:

- **Connection/server/time:** ServerSettingsEvent, OptionalFeaturesEvent, ServerVerificationEvent,
  SequenceNumberEvent, BasicLatencyStatsEvent, ServerMaintenanceEvent, HaapiSessionEvent,
  BakApiKeyEvent, AlmanachCalendarDateEvent.
- **Account/character:** AccountRoleStatusEvent, AccountSecurityFlagsEvent,
  SessionElapsedDurationEvent, CharacterExperienceGainEvent, CharacterCapabilitiesEvent,
  CharacterLoadingCompleteEvent, NotificationsEvent, ClientChallengeInitRequest.
- **Inventory/quests/achievements:** ShortcutBarContentEvent, QuestsEvent, AchievementsEvent,
  PresetListEvent, TierAmountTableEvent, ObjectAveragePricesEvent/Request, JobBookSubscribeRequest,
  JobCrafterDirectorySettingsEvent, ExchangeJobIndexStartedEvent, ExchangeErrorEvent,
  ExchangeMoneyMovementLimitEvent.
- **Social/chat/contacts:** ChatChannelsEnabledEvent, AlignmentRankUpdateEvent, SpouseStatusEvent,
  FriendListEvent, AcquaintanceListEvent, TitlesAndOrnamentsEvent, OrnamentSelectErrorEvent,
  TitleSelectErrorEvent, FriendSet*Request, ContactWarn*Request, Guild*Request,
  ServerMaintenanceInformationRequest.
- **Cosmetics/house/anomaly:** HouseAccountEvent, CharacterAppearanceCollectionEvent,
  CosmeticInventoryLoadedEvent, OutfitsLoadedEvent, OutfitEntityLookChangedEvent,
  OutfitAuraChangedEvent, AnomalyStateEvent, AnomalySubareaInformationEvent and its request.
- **Fight/map context:** ShowChallengeEvent, AreaFightModifierUpdateEvent, GameActionItemListEvent,
  NpcsMapQuestStatusUpdateEvent, MapInformationRequest (client, external).
- **Haven bag/prisms/presets:** HavenBagRoomUpdateEvent, HavenBagPackListEvent, PrismListEvent.
- **Other client requests:** PingRequest, SequenceNumberRequest, GuildInformationRequest,
  BakApiTokenRequest, ObjectAveragePricesRequest, SubscribeMultipleChannelRequest,
  ChatCommunityChannelSetCommunityRequest, SpouseInformationRequest, AcquaintanceListRequest,
  FriendListRequest, JobBookSubscribeRequest, MapInformationRequest.

The build schema contains all observed types; `mapping-15.binpb` has no readable counterpart for
them because the readable protocol lacks those messages. The schema can describe their shape but
not their semantics. The external names are useful evidence but not uniform: for example, `.jpe`
conflicts with this build's pinned `MapInformationRequest` → `.kjt`, so each type must be validated
before use.

### Minimal TDD correction proposed

Do not allow unknown messages globally and do not weaken fail-closed.

1. Add a **build-scoped non-position type classification** consulted by `GameSession` before the
   latch: an explicitly reviewed type does not latch; every unlisted, unknown or build-changed type
   still latches.
2. Red/green tests:
   - `ANonPositionTypeDoesNotLatchMovementTranslation` (start with `TimeEvent`);
   - `AnUnlistedUnknownTypeStillLatchesMovementTranslation`;
   - `TheNonPositionTypesAreScopedToTheClientBuild`;
   - `AFrameOfAKnownPositionChannelStillLatchesIfUnread`.
3. Replace the raw classification with real interpretation, one type at a time: add readable
   `.proto` descriptors and pins for `TimeEvent` (`timestamp`, `timezone_offset`), then
   `ServerSettingsEvent`, `QueueStatusEvent`, `OptionalFeaturesEvent`, etc. A mapped type becomes
   `read` (partial) and no longer latches; its test verifies both the reading and the non-latch.
4. Do not modify the mapping or the latch during a supervised session.

## E. Passive neighbour plan

Not executed. With the latch closed, `p2j readiness <source> <target>` was forbidden by the runbook;
no plan was checked and no authorization was consumed.

## F. Stop and restoration

- Dofus was closed by the supervisor; the game connection closed with last known map `191105026`,
  cell `552`.
- The host was stopped with Ctrl+C; no `D3B.Host` or Dofus process remains.
- The client `version` file SHA-256 is `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`,
  identical to the private session backup
  `~/.local/share/D3B/private-backups/go1-20261104T182159Z/version.original`.
- `ConfigUrl` points to Ankama again, not to D3B; no active residual redirect.
- Ports `5555` and `5556` are free. The private `~/.local/share/D3B/client` cache remains but is no
  longer referenced by the client.
- No push, no hot fix, no automatic movement.

## G. Verdict

**BLOCKED for GO 2.**

The P2I latch closed on the first unread frame `.lok` (TimeEvent, non-position) before the first
placement and stayed closed for the whole session. The position was server-placed and trusted
(`guard=None`), but the device correctly refused movement; the relay confirms 0 D3B frames. A new
mission must implement and test the minimal non-position classification or interpretation, then a
new passive GO 1 must show `translation_blocked=false`; only then can a distinct GO 2 with explicit
source and target be requested.
