# P2K — unblock the translation latch without weakening movement safety

*2026-11-04. Branch `p2k-latch-classification`, base `70c9ec3`. No installed/live client, host, relay, Ankama
connection, route, harvest or movement was run; only the test suite's in-process test hosts and fake relays were used.
No GO 2 and no `p2j move`.*

## Verdict

**BLOCKED for a new passive GO 1.** The P2I latch still closes on 64 of the 89 types observed in P2J GO 1, including
the first blocking server type (`.mem`, `AccountRoleStatusEvent`, if the real `.lok` payload matches its two schema fields;
otherwise `.lok` itself) and the client `PingRequest` (`.kne`, 12 frames). The correction is coherent and reproducible for
the 25 types whose full payload graph a reference names field by field, but it is not complete. A new GO 1 would reproduce
a closed latch and cannot be called ready. The precise remaining list is the coverage table in
[Appendix A](#appendix-a--coverage-of-the-89-go-1-types).

## A. What was measured

The diagnosis uses three offline sources:

1. The private P2J GO 1 host log (`~/.local/share/D3B/private-logs/go1-20261104T182159Z/host.log`): type names, directions,
   lengths and counts only. No raw payload, ticket or account identifier was read into this report or committed.
2. The installed `3.7.13.17` schema, dumped to a field inventory: message names, field numbers, field names, protobuf
   types and repetition/optionality.
3. BillBot's `game_mappings_detailed.json` as an external, **different-build** reference. A name is never accepted alone:
   every field of the message must be named, and the build's schema must validate the payload.

The P2I latch is the predicate `!IsRead && !IsUnderstood` in `GameSession.OnFrame`. P2J GO 1 decoded 418 game frames
(70 server + 12 client understood, 163 server read/partial, 173 unread: 136 server + 37 client) across 89 distinct carried
types. All 173 were `UnknownCarriedType`: the envelope parsed, its length was exact, the carried type was absent from the
readable protocol, and the payload was never parsed. There were no malformed envelopes, no envelope length mismatches and
no unreadable readable payloads in that session. The first unread frame was server `.lok` (`TimeEvent`) before placement;
the latch was closed at first placement.

The unread types are not one protocol feature. They include server settings, time, queue and pong, social and contact
lists, inventory and exchange updates, quests, house and haven-bag state, fight-team updates, chat channels, verification,
empty trigger messages and three messages for which no reference name exists.

## B. The `.jpe` / `.kjt` conflict

The conflict is real and cannot be resolved by trusting an external name.

| Build's type | Schema field | Reference name | Readable message |
|---|---|---|---|
| `.jpe` | `fwff`, `int32`, field 1 | BillBot `gamemap.MapInformationRequest`, field `fwff -> map_id` | `d3b.game.MapInformationRequest` has `int64 map_id = 1`; the types do not match, so no pair |
| `.kjt` | `fzkw`, `int64`, field 1 | BillBot `context.ContextReadyRequest`, field `fzkw -> map_id` in **its** build | `d3b.game.MapInformationRequest` is pinned to `.kjt`; the shape matches `{int64 map_id = 1}`, but that does not prove which message it is |

- Our `3.7.13.17` schema has `.jpe` field 1 as `int32` and `.kjt` field 1 as `int64`.
- Our readable `MapInformationRequest.map_id` is `int64`; it is pinned to `.kjt` by shape. The external reference calls `.kjt`
  `ContextReadyRequest`, not `MapInformationRequest`; both can encode `{int64 map_id = 1}`. The pin's **semantic identity is
  therefore not proven** by the reference. This is a pre-existing mapping-provenance risk (P2D), not a P2K change.
- BillBot's `.jpe` is `MapInformationRequest` for **another build**, where the field is an `int32`; BillBot's `.kjt` is a
  different message in that build. The names moved between builds; the obfuscation is build-specific.
- Pairing `.jpe` to our readable `MapInformationRequest` would change a field type and is refused by the contract audit.
  The external reference calls `.jpe` a map request for another build, but that name does not transfer to `3.7.13.17`; its
  single `int32` could carry a map or an unrelated identifier. The conservative block is the only supported choice.
- `.jpe` was sent by the client twice in GO 1. It is treated as position/map-affecting for safety and **stays
  blocking**. `.kjt` is a mapped,
  read type, so it is outside the unknown-type policy; D3B does not send `MapInformationRequest`, and no code path should
  do so before a capture or a response pair proves which message `.kjt` is. No mapping change was made.

## C. Correction

### C1. Structured translation problems

`TranslationProblemKind` now distinguishes `MalformedEnvelope`, `EnvelopeLengthMismatch`, `UnknownCarriedType`,
`UnreadablePayload` and `PartialPayload` from `None`. `DecodedGameFrame.ProblemKind` carries it. This is the vocabulary a
policy may act on; the old free-text `Problem` remains for logs.

### C2. Schema validation of an unknown payload

`ProtocolTranslation` stores every schema message descriptor. When a carried type is absent from the readable protocol,
`SchemaPayload.IsValid` walks the payload with that descriptor:

- every tag must name a field of the message (an unknown field is refused, not discarded);
- the wire type must be the field's expected type;
- a nested message is walked the same way, including map entries;
- a repeated packable scalar declared packed may be packed (length-delimited) or unpacked (scalar wire type); a
  field declared unpacked must use the scalar wire type (a packed encoding is refused, fail-closed);
- the last field must end on the payload's last byte; trailing bytes, truncation and malformed varints are refused.

`DecodedGameFrame.IsSchemaValid` is true for a readable frame whose payload parsed (whole or partial), a frame with no
content, or an unknown type whose payload passes this walk. For a partial readable payload it does not mean the fields the
readable protocol leaves out were validated; the policy's `UnknownCarriedType` gate keeps that case out of the exemption.
Malformed envelopes, length mismatches and unreadable readable payloads are not schema-valid. While adding the nested test,
the dynamic descriptor's `IsPacked` was observed true for a singular message field; the packed branch is therefore guarded
by both `IsRepeated` and a packable scalar type, and repeated messages are tested explicitly. Strings are checked as UTF-8
and group fields are refused, so an invalid string or an unparsed group cannot pass the walk.

### C3. Build-scoped non-position classification

`NonPositionMessages` holds 25 types for exactly `3.7.13.17`. Each entry requires:

- every field in the message's **full payload graph** to have a reference name, recursively through message-typed fields
  (no anonymous field at any depth);
- a family outside the map, gamemap, game action, fight, house, haven bag, role-play challenge, NPC map state, client
  verification, BAK/HAAPI session and account role/security state;
- an independently recorded reference message name in the comment, with the field-by-field mapping.

The policy is applied only when **all** of these hold:

1. `ProblemKind == UnknownCarriedType`;
2. the carried type is in the list **for the mapping's exact client version**;
3. the frame arrives from the side GO 1 observed (a listed client request from the server, or a listed server event from
   the client, is contradictory);
4. the `Any` type URL is exactly the game's `type.ankama.com/` prefix followed by the carried type; and
5. `IsSchemaValid` is true.

Everything else latches: unknown/unlisted types, a listed type with a different client version, a listed type from the
other side or under a foreign type URL, a malformed envelope or length mismatch, an unreadable readable payload, and a
listed type whose payload does not have exactly the build's schema shape. This is fail-closed for 64 types.

A **partial readable payload** is the one deliberate exception: it is `IsRead=true` under the P2I predicate, because D47
lets D3B read its declared fields and relay the frame unchanged. It does not latch, before or after P2K. This is a
pre-existing residual case, not covered by the new unknown-type policy; a mission that wants it fail-closed needs a
separate decision because it would change normal bot reading. The tests keep `APartialButReadableMessageDoesNotLatchTranslationFailure`
as the documented behavior.

`GameSession` logs a listed non-position frame at Information and repeated occurrences at Debug; no payload is logged.

The 25 entries are listed in `src/D3B.Bot/Network/NonPositionMessages.cs` with their field evidence. The 64 remaining
types keep the original latch and are not globally allowed. The detailed field inventory used for the audit is not
committed: it is a per-build mapping-shaped dump (D10/D11); only the pins and this allowlist are source.

### C4. Why the readable protocol was not extended in this lot

The preferred long-term correction remains a readable descriptor and a pin for each identified message. Two constraints
made it the wrong change for this session:

- adding a game message changes `ReadableProtocol.Digest`; `MappingFormatTests` requires the mapping format version to be
  raised and every build's mapping to be regenerated. A `TimeEvent` prototype was written, tested, and reverted for this
  reason. The mapping artifacts are release artifacts (D10/D11), not source files.
- 25 descriptors (with their nested map/entry types) would be a protocol-contract change wider than the movement-safety
  correction, while the remaining 64 unknowns would still block GO 1.

The classification list is the narrow build-scoped correction; a deliberate mapping-format lot is the correct home for
readable descriptors. D307 records this choice.

## D. Tests

New tests:

| Test | What it proves |
|---|---|
| `ProtocolTranslationTests.AnUnknownCarriedTypeIsSchemaValidWhenItsPayloadFitsTheBuildsSchema` | a schema-shaped unknown payload is valid |
| `AnUnknownCarriedTypeHoldingWhatItsSchemaLacksIsNotSchemaValid` | an extra field refuses the exemption |
| `AnUnknownCarriedTypeWhosePayloadDoesNotParseIsNotSchemaValid` | truncation refuses the exemption |
| `AGameFrameWithAForeignTypeUrlIsNotTheGamesType` | a foreign URL, including an extra path segment, is not the game's type |
| `SchemaPayloadTests.AScalarPayloadMustHoldOnlyItsFieldAndEndOnIt` | scalar shape, unknown field, wrong wire type, trailing byte |
| `ANestedPayloadIsWalkedTheSameWay` | nested message recursion |
| `ARepeatedMessageIsNotAPackedScalar` | repeated messages are not mistaken for packed scalars |
| `AMapPayloadIsWalkedThroughItsEntry` | map entries recurse through key/value |
| `APackedPayloadMayBePackedOrUnpacked` | packed and unpacked repeated scalars |
| `AFixedPayloadNeedsItsWholeEightBytes` | fixed64 width and wire type |
| `AStringPayloadMustBeUtf8` | an invalid UTF-8 string is refused |
| `AnEmptyPayloadHoldsNoByte` | empty schema |
| `NonPositionMessagesTests.*` | 25 entries, exact build/side set, excluded types |
| `TheExactBuildAndSideSetIsPinned` | the exact 25 type:side pairs, so a substitution cannot pass |
| `NonPositionTranslationTests.AListedMessageWithAValidSchemaDoesNotCloseMovement` | a listed sync message leaves movement open |
| `AListedClientRequestFromTheClientDoesNotCloseMovement` | a listed client request from the observed side leaves movement open |
| `AListedClientRequestFromTheServerClosesMovement` | a listed type from the other side is contradictory and latches |
| `AListedMessageWithAForeignTypeUrlClosesMovement` | a listed type with a foreign type URL latches |
| `AnUnlistedMessageWithAValidSchemaClosesMovement` | unlisted unknown still latches |
| `AListedMessageHoldingWhatItsSchemaLacksClosesMovement` | extra field latches |
| `AListedMessageWhosePayloadDoesNotParseClosesMovement` | malformed payload latches |
| `AnUnlistedPositionMessageWithAValidSchemaClosesMovement` | `.jpe`-shaped unknown still latches |
| `AnUnlistedFightEventWithAValidSchemaClosesMovement` | `.jxh`-shaped unknown still latches |
| `AListedMessageOfAnotherBuildClosesMovement` | build scoping |
| `AListedMessageDoesNotWithdrawAQueuedMovementSend` | P2I queued-send withdrawal is not triggered by a listed frame |
| `AListedMessageDuringTheInboxBarrierDoesNotCloseMovement` | relay/inbox order |
| `TheServersTimeIsAClassifiedNonPositionMessage` | `.lok` after its identification |

Existing P2I and P2J tests continue to cover malformed envelopes, partial readable messages, unknown envelope fields,
cancellation, observation failure, queued-send withdrawal and one-shot move safety. The full Protocol test project also
runs the mapping contract/format audit (`ContractAuditTests`, `MappingFormatTests`, `PinnedPairsFileTests`); no mapping
artifact was regenerated or changed in P2K.

Results on this branch:

- `dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build`: **1465 passed, 0 failed, 0 skipped**
  (Protocol 102, Bot 608).
- `dotnet build D3B.slnx`: 0 warnings, 0 errors.
- `dotnet format D3B.slnx --verify-no-changes`: clean.

### P2K-2 audit follow-up (2026-11-05)

The historical P2K result above remains 1,465 passed. This follow-up adds three tests (+3), bringing the serialized suite
to **1,468 passed, 0 failed, 0 skipped**; production behavior is unchanged.

- The unreadable unknown-type and listed-message fixtures now use a truncated varint on known field 1. The listed-message
  extra-field case remains separate.
- `SchemaPayloadTests` now verifies that an explicitly unpacked repeated field rejects packed input, truncated and
  11-byte `int64` varints are refused, and a real proto2 group descriptor's StartGroup/EndGroup payload is refused.
- `NonPositionMessagesTests.TheExactBuildAndSideSetIsPinned` asserts the exact client version literal `3.7.13.17`.

The proto2 group descriptor was constructible with Google.Protobuf, so the test exercises the real group-field path.

## E. Independent review

Two independent, read-only reviews were run on the current working tree: a Claude review and a GPT-6 Luna xhigh
audit. Their findings are recorded in section G; no reviewer may widen scope, merge or push.

## F. Remaining risks

- **Coverage**: 64 of 89 types still latch, including 8 whose direct fields are named but whose nested payload graph is
  not (`ifg`, `ipe`, `ira`, `ism`, `kjm`, `kmt`, `kxn`, `lxb`) and `.lwy` whose subarea state is map-adjacent. Assuming the
  real `.lok` payload has exactly its two schema fields, the first blocking server types in GO 1 order are `.mem`, `.meb`,
  `.hmj`, `.kog`, `.jpx`, `.iym`, `.imf`, `.mdp`, `.ica`; if it carries any other field, `.lok` itself remains the first
  blocker. The first blocking client frame is `.kod` (client verification), and `.kne` follows with 12 frames. A new session
  can meet a different order or new types.
- **Evidence quality**: the 25 entries depend on an external reference for another build; the schema field match is the
  proof, but the reference's semantic names are not a measurement by D3B. The table is intentionally narrow and can only
  exempt a type whose full payload graph is named. The 8 nested-unproven types are evidence of how easy that distinction
  is to miss.
- **Partial readable**: a readable payload with fields the readable protocol leaves out is `IsRead=true` and does
  not latch (P2I/D47). P2K preserves that behavior; the new policy only covers `UnknownCarriedType`. A mission that wants
  partial readable frames fail-closed needs a separate decision and would change normal bot reading.
- **No live validation** of the new policy was run. The schema walker is unit-tested and the session policy is tested
  through synthetic descriptors and in-process fake relays; the installed build's schema was not exercised with a real
  listed payload in this mission.
- **Mapping/apphost**: no mapping, apphost or installed artifact was changed or published. The current installed apphost
  predates this branch and does not contain the policy; a deployment would need a new exact-commit apphost.
- **`.jpe`**: the one explicit conflict is unresolved and position-affecting. It is the first type to identify next,
  before any GO 1 rerun can be meaningful.

## G. Review result and final verdict

**Reviews found the unknown-type code path fail-closed, and corrected the report.** Neither review found a false accept
in `SchemaPayload` that would let an unknown position-bearing frame skip the latch; the Luna audit found invalid UTF-8 and
group-content gaps, now closed by the UTF-8 check and group refusal. Both found a reporting error: partial readable
payloads do **not** latch
(the P2I/D47 behavior above); the report now says so and lists it as a residual case. The Claude review found that 8 of the
34 then-exempt entries had unnamed fields at nested depth; those 8 (`ifg`, `ipe`, `ira`, `ism`, `kjm`, `kmt`, `kxn`, `lxb`)
were removed. The Luna review's map-adjacency note on `.lwy` led to removing it too. The final allowlist is 25 types. The
Luna review's other findings were addressed: exact type-URL equality, an exact set/side test, the log wording, UTF-8
string validation and group refusal. **Final mission verdict: BLOCKED** — 64 of 89 types still latch; the allowlist is safe
as far as it goes but insufficient for a new GO 1.

## Appendix A — coverage of the 89 GO 1 types

Status meanings:

- **exempt**: every field in the full payload graph is named by the reference, the family is outside the excluded state
  families, the frame arrived from the side GO 1 observed under the exact game type URL, and the payload validates
  against the build's schema. No latch.
- **blocking (unidentified)**: no reference message at all.
- **blocking (empty/trigger)**: the schema message has no field, but the type can be a trigger or security event; no
  field evidence, so no exemption.
- **blocking (no field evidence)**: a reference message exists but no field is named.
- **blocking (unproven fields)**: some fields are named, some are not.
- **blocking (map/combat/security/state)**: every field is named, but the family is map, gamemap, game action, fight,
  house, haven bag, role-play challenge, NPC map state, client verification, BAK/HAAPI, or account role/security. The
  latch stays closed.
- **blocking (nested fields unnamed)**: every direct field is named, but a nested message in the payload graph has a field
  the reference does not name; the whole graph is the evidence.
- **blocking (map-adjacent state)**: a world subarea/state field is too close to map state to classify as non-position.

`S`/`C` are GO 1 server/client occurrence counts. The field-by-field inventory was audited offline against the installed
schema and the external reference; it is not committed because it is a per-build mapping-shaped dump (D10/D11).

| Type | GO1 S/C | Reference message | Family | Named fields | Status |
|---|---|---|---|---|---|
| `.hgf` | S1 | TitlesAndOrnamentsEvent | tinsel | 2/2 | **exempt** (non-position) |
| `.hgg` | S1 | OrnamentSelectErrorEvent | tinsel | 1/1 | **exempt** (non-position) |
| `.hgh` | S1 | TitleSelectErrorEvent | tinsel | 1/1 | **exempt** (non-position) |
| `.hkv` | C1 | SpouseInformationRequest | social | 0/1 | blocking (no field evidence) |
| `.hla` | S1 | SpouseStatusEvent | social | 1/1 | **exempt** (non-position) |
| `.hlf` | C1 | ChatCommunityChannelSetCommunityRequest | social | 1/1 | **exempt** (non-position) |
| `.hlv` | S1 | — | — | 0/2 | blocking (unidentified) |
| `.hlw` | S1 | AlignmentRankUpdateEvent | social | 2/2 | **exempt** (non-position) |
| `.hmd` | S1 | ServerSettingsEvent | server | 7/7 | **exempt** (non-position) |
| `.hmj` | S1 | — | — | 0/1 | blocking (unidentified) |
| `.hmp` | S4 | RemoveChallengeEvent | roleplay | 1/2 | blocking (unproven fields) |
| `.hnl` | S2 | ShowChallengeEvent | roleplay | 1/1 | blocking (map/combat/security/state) |
| `.hpe` | S1 | — | — | 0/1 | blocking (unidentified) |
| `.hrl` | S1 | ExchangeJobIndexStartedEvent | exchange | 1/2 | blocking (unproven fields) |
| `.ica` | S1 | QuestsEvent | quest | 4/6 | blocking (unproven fields) |
| `.icj` | S7 | QuestStepInformationEvent | quest | 2/3 | blocking (unproven fields) |
| `.ico` | S1 | PrismListEvent | prism | 1/2 | blocking (unproven fields) |
| `.ifg` | S1 | PresetListEvent | preset | 2/2 | blocking (nested fields unnamed) |
| `.ilr` | S1 | NpcsMapQuestStatusUpdateEvent | npc | 1/1 | blocking (map/combat/security/state) |
| `.imf` | S1 | NotificationsEvent | notification | 1/2 | blocking (unproven fields) |
| `.ipe` | S1 | JobCrafterDirectorySettingsEvent | job | 1/1 | blocking (nested fields unnamed) |
| `.iqu` | C1 | ObjectAveragePricesRequest | exchange | 0/1 | blocking (no field evidence) |
| `.ira` | S3 | SetUpdateEvent | inventory | 3/3 | blocking (nested fields unnamed) |
| `.ism` | S2 | ShortcutBarContentEvent | inventory | 2/2 | blocking (nested fields unnamed) |
| `.isr` | S1 | ObjectAveragePricesEvent | exchange | 1/3 | blocking (unproven fields) |
| `.ixd` | S1 | HouseAccountEvent | house | 1/1 | blocking (map/combat/security/state) |
| `.ixe` | S8 | HousePropertiesEvent | house | 3/4 | blocking (unproven fields) |
| `.iye` | S1 | HavenBagPackListEvent | haven | 1/2 | blocking (unproven fields) |
| `.iym` | S2 | HavenBagRoomUpdateEvent | haven | 2/3 | blocking (unproven fields) |
| `.izn` | S1 | HaapiSessionEvent | haapi | 1/2 | blocking (unproven fields) |
| `.jcy` | C1 | ServerMaintenanceInformationRequest | guild | 0/1 | blocking (no field evidence) |
| `.jcz` | S1 | ServerMaintenanceEvent | guild | 1/2 | blocking (unproven fields) |
| `.jet` | C1 | GuildMemberWarnOnConnectionStopRequest | guild | 0/1 | blocking (no field evidence) |
| `.jgm` | C3 | GuildInformationRequest | guild | 1/3 | blocking (unproven fields) |
| `.jpe` | C2 | MapInformationRequest | gamemap | 1/1 | blocking (map/combat/security/state) |
| `.jpp` | S4 | MapFightCountEvent | gamemap | 1/3 | blocking (unproven fields) |
| `.jpx` | S2 | GameActionItemListEvent | game | 0/2 | blocking (no field evidence) |
| `.jxh` | S13 | FightTeamUpdateEvent | fight | 2/2 | blocking (map/combat/security/state) |
| `.kaq` | S1 | ExchangeMoneyMovementLimitEvent | exchange | 1/2 | blocking (unproven fields) |
| `.kbi` | S1 | ExchangeErrorEvent | exchange | 1/1 | **exempt** (non-position) |
| `.kcf` | C1 | JobBookSubscribeRequest | exchange | 1/1 | **exempt** (non-position) |
| `.kes` | S1 | EmotesEvent | emote | 1/1 | **exempt** (non-position) |
| `.kjm` | S3 | ContextRefreshEntityLookEvent | context | 2/2 | blocking (nested fields unnamed) |
| `.kkn` | C1 | ContactWarnOnAchievementCompleteSetRequest | contact | 1/1 | **exempt** (non-position) |
| `.kkt` | C1 | FriendSetStatusShareRequest | contact | 1/2 | blocking (unproven fields) |
| `.klu` | C1 | FriendSetWarnOnLevelGainRequest | contact | 1/1 | **exempt** (non-position) |
| `.klx` | C1 | ContactWarnOnPermanentDeathSetRequest | contact | 1/2 | blocking (unproven fields) |
| `.kmi` | S1 | AcquaintanceListEvent | contact | 1/2 | blocking (unproven fields) |
| `.kmj` | C1 | FriendSetWarnOnConnectionRequest | contact | 1/1 | **exempt** (non-position) |
| `.kmk` | C1 | AcquaintanceListRequest | contact | 0/0 | blocking (empty/trigger) |
| `.kmt` | S1 | FriendListEvent | contact | 1/1 | blocking (nested fields unnamed) |
| `.kmu` | C1 | FriendListRequest | contact | 0/0 | blocking (empty/trigger) |
| `.kna` | S4 | QueueStatusEvent | connection | 2/2 | **exempt** (non-position) |
| `.knb` | S12 | PongEvent | connection | 1/1 | **exempt** (non-position) |
| `.kne` | C12 | PingRequest | connection | 1/2 | blocking (unproven fields) |
| `.kni` | S1 | OptionalFeaturesEvent | connection | 1/1 | **exempt** (non-position) |
| `.knz` | S1 | GuildTokenConfigurationEvent | guild | 1/4 | blocking (unproven fields) |
| `.kod` | C1 | ClientChallengeInitRequest | client | 1/1 | blocking (map/combat/security/state) |
| `.kog` | S1 | ServerVerificationEvent | client | 0/0 | blocking (empty/trigger) |
| `.kpu` | C1 | SubscribeMultipleChannelRequest | chat | 2/2 | **exempt** (non-position) |
| `.kpv` | S1 | ChatChannelsEnabledEvent | chat | 2/2 | **exempt** (non-position) |
| `.kpx` | S13 | ChannelUpdateEvent | chat | 2/3 | blocking (unproven fields) |
| `.krm` | S1 | CharacterExperienceGainEvent | character | 1/1 | **exempt** (non-position) |
| `.krw` | S1 | CharacterCapabilitiesEvent | character | 1/1 | **exempt** (non-position) |
| `.ktc` | S1 | CharacterLoadingCompleteEvent | character | 0/0 | blocking (empty/trigger) |
| `.kxn` | S1 | TierAmountTableEvent | preset | 1/1 | blocking (nested fields unnamed) |
| `.lnt` | C2 | SequenceNumberRequest | basic | 1/1 | **exempt** (non-position) |
| `.lnu` | S2 | SequenceNumberEvent | basic | 0/0 | blocking (empty/trigger) |
| `.loj` | C1 | BasicLatencyStatsRequest | basic | 1/2 | blocking (unproven fields) |
| `.lok` | S3 | TimeEvent | basic | 2/2 | **exempt** (non-position) |
| `.lou` | S1 | BasicLatencyStatsEvent | basic | 0/0 | blocking (empty/trigger) |
| `.lov` | S1 | AlmanachCalendarDateEvent | basic | 1/3 | blocking (unproven fields) |
| `.lpj` | C1 | BakApiTokenRequest | bak | 0/0 | blocking (empty/trigger) |
| `.lpp` | S1 | BakApiKeyEvent | bak | 1/2 | blocking (unproven fields) |
| `.lsh` | S1 | ArenaUpdatePlayerInformationEvent | arena | 2/2 | **exempt** (non-position) |
| `.ltb` | S2 | AreaFightModifierUpdateEvent | area | 1/1 | blocking (map/combat/security/state) |
| `.lto` | S1 | CharacterAppearanceCollectionEvent | cosmetic | 2/3 | blocking (unproven fields) |
| `.luo` | S1 | CosmeticInventoryLoadedEvent | cosmetic | 1/5 | blocking (unproven fields) |
| `.lve` | S1 | OutfitsLoadedEvent | cosmetic | 2/4 | blocking (unproven fields) |
| `.lvw` | S1 | OutfitEntityLookChangedEvent | cosmetic | 2/3 | blocking (unproven fields) |
| `.lwn` | S1 | OutfitAuraChangedEvent | cosmetic | 1/1 | **exempt** (non-position) |
| `.lwx` | C1 | AnomalySubareaInformationRequest | anomaly | 0/0 | blocking (empty/trigger) |
| `.lwy` | S1 | AnomalyStateEvent | anomaly | 3/3 | blocking (map-adjacent state) |
| `.lxb` | S1 | AnomalySubareaInformationEvent | anomaly | 1/1 | blocking (nested fields unnamed) |
| `.lxg` | S1 | AlterationsEvent | alteration | 1/2 | blocking (unproven fields) |
| `.mdp` | S1 | AchievementsEvent | achievement | 1/2 | blocking (unproven fields) |
| `.meb` | S1 | AccountSecurityFlagsEvent | account | 1/2 | blocking (unproven fields) |
| `.mem` | S1 | AccountRoleStatusEvent | account | 3/3 | blocking (map/combat/security/state) |
| `.meq` | S1 | SessionElapsedDurationEvent | account | 1/1 | **exempt** (non-position) |

Summary: 25 exempt (non-position), 8 nested-unproven, 31 partial, 9 exact-but-excluded, 8 empty/trigger,
5 no-field-evidence, 3 unidentified = 89. The 64 non-exempt types keep the latch.

Near-miss field names among the exempt: `kna.position` is the connection queue's position, not the character's map or cell;
`lsh.ban_end_date`/`arena_ranks` are arena metadata. `.lwy`'s `subarea_id` was judged too map-adjacent and was removed.
