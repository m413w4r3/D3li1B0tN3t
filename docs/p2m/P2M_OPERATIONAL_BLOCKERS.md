# P2M Lot 3 — operational blockers

**Disposition: BLOCKED. All 62 observed types below remain fail-closed for client 3.7.13.17.** The table is a read-only comparison of the complete GO 1 report with `NonPositionMessages`; it does not change or promote the policy.

The observation contains 124 analyzed occurrences across 86 types, with complete coverage and zero dropped frames. All 124 occurrences have status `VALID`; there are no problem codes or type-URL mismatches. Six types carry the aggregate `SHAPE_VARIATION` contradiction code (14 affected occurrences): five blockers (`ira`, `ism`, `jgm`, `jpp`, `kpx`) and one source-accepted type (`kna`). A valid schema shape is not a semantic identity proof.

Family and message names are hypotheses taken from the prior P2K annotation or the named external mapping. `supposed` means a name is suggested but unproven; `unknown` means no message name is available. No entry is `proved`. `high`, `possible`, and `undetermined` describe conservative position-effect potential, not a measured runtime effect.

The first seven priorities follow the requested order. The remaining rows place initialization, security, and connection hypotheses before position-relevant families, then sort by occurrence count descending and token.

| Rank | Token | Direction | Occurrences | Status / problems | Identity | Family hypothesis | Position effect | Evidence needed |
|---:|---|---|---:|---|---|---|---|---|
| 1 | `.mem` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | account / AccountRoleStatusEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 2 | `.kne` | client ×2 | 2 | VALID ×2; problems: none; contradictions: none | supposed | connection / PingRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 3 | `.kpx` | server ×13 | 13 | VALID ×13; problems: none; contradictions: SHAPE_VARIATION ×3 | supposed | chat / ChannelUpdateEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 4 | `.icj` | server ×8 | 8 | VALID ×8; problems: none; contradictions: none | supposed | quest / QuestStepInformationEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 5 | `.jxh` | server ×3 | 3 | VALID ×3; problems: none; contradictions: none | supposed | fight / FightTeamUpdateEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 6 | `.jpe` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | gamemap / MapInformationRequest (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 7 | `.kod` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | client / ClientChallengeInitRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 8 | `.hme` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | server / ServerExperienceModifierEvent (BillBot hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 9 | `.izn` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | haapi / HaapiSessionEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 10 | `.jet` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | guild / GuildMemberWarnOnConnectionStopRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 11 | `.knz` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | guild / GuildTokenConfigurationEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 12 | `.kog` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | client / ServerVerificationEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 13 | `.ktc` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | character / CharacterLoadingCompleteEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 14 | `.lnu` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | basic / SequenceNumberEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 15 | `.lov` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | basic / AlmanachCalendarDateEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 16 | `.lpj` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | bak / BakApiTokenRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 17 | `.lpp` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | bak / BakApiKeyEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 18 | `.meb` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | account / AccountSecurityFlagsEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 19 | `.iym` | server ×2 | 2 | VALID ×2; problems: none; contradictions: none | supposed | haven / HavenBagRoomUpdateEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 20 | `.jpp` | server ×2 | 2 | VALID ×2; problems: none; contradictions: SHAPE_VARIATION ×2 | supposed | gamemap / MapFightCountEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 21 | `.jpx` | server ×2 | 2 | VALID ×2; problems: none; contradictions: none | supposed | game / GameActionItemListEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 22 | `.hmp` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | roleplay / RemoveChallengeEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 23 | `.hnl` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | roleplay / ShowChallengeEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 24 | `.ico` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | prism / PrismListEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 25 | `.ilr` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | npc / NpcsMapQuestStatusUpdateEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 26 | `.ixd` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | house / HouseAccountEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 27 | `.iye` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | haven / HavenBagPackListEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 28 | `.ltb` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | area / AreaFightModifierUpdateEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 29 | `.lwx` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | anomaly / AnomalySubareaInformationRequest (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 30 | `.lxb` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | anomaly / AnomalySubareaInformationEvent (P2K hypothesis) | high | Exact-build identity, recursive field proof, and effect review |
| 31 | `.ibw` | server ×4 | 4 | VALID ×4; problems: none; contradictions: none | unknown | unknown / unidentified (no source hypothesis) | undetermined | Exact-build identification, recursive field proof, and effect review |
| 32 | `.ira` | server ×3 | 3 | VALID ×3; problems: none; contradictions: SHAPE_VARIATION ×2 | supposed | inventory / SetUpdateEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 33 | `.jgm` | client ×3 | 3 | VALID ×3; problems: none; contradictions: SHAPE_VARIATION ×2 | supposed | guild / GuildInformationRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 34 | `.ism` | server ×2 | 2 | VALID ×2; problems: none; contradictions: SHAPE_VARIATION ×2 | supposed | inventory / ShortcutBarContentEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 35 | `.hkv` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | social / SpouseInformationRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 36 | `.hlv` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | unknown | unknown / unidentified (no source hypothesis) | undetermined | Exact-build identification, recursive field proof, and effect review |
| 37 | `.hmj` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | unknown | unknown / unidentified (no source hypothesis) | undetermined | Exact-build identification, recursive field proof, and effect review |
| 38 | `.hpe` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | unknown | unknown / unidentified (no source hypothesis) | undetermined | Exact-build identification, recursive field proof, and effect review |
| 39 | `.hrl` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | exchange / ExchangeJobIndexStartedEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 40 | `.ibs` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | quest / QuestsFollowedEvent (BillBot hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 41 | `.ica` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | quest / QuestsEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 42 | `.ifg` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | preset / PresetListEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 43 | `.imf` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | notification / NotificationsEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 44 | `.ipe` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | job / JobCrafterDirectorySettingsEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 45 | `.iqu` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | exchange / ObjectAveragePricesRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 46 | `.isr` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | exchange / ObjectAveragePricesEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 47 | `.jcy` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | guild / ServerMaintenanceInformationRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 48 | `.jcz` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | guild / ServerMaintenanceEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 49 | `.kaq` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | exchange / ExchangeMoneyMovementLimitEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 50 | `.kkt` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | contact / FriendSetStatusShareRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 51 | `.klx` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | contact / ContactWarnOnPermanentDeathSetRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 52 | `.kmi` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | contact / AcquaintanceListEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 53 | `.kmk` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | contact / AcquaintanceListRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 54 | `.kmt` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | contact / FriendListEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 55 | `.kmu` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | contact / FriendListRequest (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 56 | `.kxn` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | preset / TierAmountTableEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 57 | `.lto` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | cosmetic / CharacterAppearanceCollectionEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 58 | `.luo` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | cosmetic / CosmeticInventoryLoadedEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 59 | `.lve` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | cosmetic / OutfitsLoadedEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 60 | `.lvw` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | cosmetic / OutfitEntityLookChangedEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 61 | `.lxg` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | alteration / AlterationsEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |
| 62 | `.mdp` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none | supposed | achievement / AchievementsEvent (P2K hypothesis) | possible | Exact-build identity, recursive field proof, and effect review |

## Evidence required before reconsidering any blocker

For each token, establish its meaning on this exact build with independent evidence; map every schema field recursively, including nested messages; confirm direction and a complete schema-valid observation; and review whether it can change map, cell, fight, movement, placement, or session state. External names and prior annotations remain hypotheses. No one may clear the latch based on this report.

## Source-accepted observed types

These 24 observed types match an entry and direction in the current source allowlist. This is a report of the existing source policy only; the P2M resolver independently demonstrates zero semantic identities.

| Token | Direction | Occurrences | Status / problems |
|---|---|---:|---|
| `.hgf` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.hgg` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.hgh` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.hla` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.hlf` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.hlw` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.hmd` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kcf` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kes` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kkn` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.klu` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kmj` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kna` | server ×4 | 4 | VALID ×4; problems: none; contradictions: SHAPE_VARIATION ×3 |
| `.knb` | server ×2 | 2 | VALID ×2; problems: none; contradictions: none |
| `.kni` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kpu` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.kpv` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.krm` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.krw` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.lnt` | client ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.lok` | server ×2 | 2 | VALID ×2; problems: none; contradictions: none |
| `.lsh` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.lwn` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |
| `.meq` | server ×1 | 1 | VALID ×1; problems: none; contradictions: none |

## Listed but unobserved

`.kbi` is listed for `server` in the source policy but has 0 observed occurrences. Its `exchange` / `ExchangeErrorEvent` label is a prior hypothesis, not an identity proof.

## Eight former GO 1 `VERIFIED` aliases

These are distinct from the historical P2K nested-field alias hypotheses below. Although previously reported as GO 1 `VERIFIED`, the corrected resolver classifies each token/readable row as `CANDIDATE` with `exactShape=true`, `uniqueShape=false`, `semanticIdentity=null`, and `proof=null`. They remain unproven candidates.

| Token | Readable candidate | Resolver status | Operational status |
|---|---|---|---|
| `hgf` | `PlacementCells` | `CANDIDATE` | Source-accepted by `NonPositionMessages` |
| `imf` | `PlacementCells` | `CANDIDATE` | Blocker |
| `kkn` | `FightTurnFinishRequest` | `CANDIDATE` | Source-accepted by `NonPositionMessages` |
| `klu` | `FightTurnFinishRequest` | `CANDIDATE` | Source-accepted by `NonPositionMessages` |
| `kmj` | `FightTurnFinishRequest` | `CANDIDATE` | Source-accepted by `NonPositionMessages` |
| `kkt` | `FightTurnReadyRequest` | `CANDIDATE` | Blocker |
| `klx` | `FightTurnReadyRequest` | `CANDIDATE` | Blocker |
| `kod` | `LoginCredentials` | `CANDIDATE` | Blocker |

Source acceptance is the existing direction allowlist comparison, not semantic identity proof. The four blockers remain fail-closed.

## Eight historical P2K nested-field alias hypotheses

This is a separate group from the eight former GO 1 `VERIFIED` aliases above. The prior report says these eight direct-field aliases were removed after nested-field review: `ifg`, `ipe`, `ira`, `ism`, `kjm`, `kmt`, `kxn`, and `lxb`. They remain candidates for further evidence only; all are marked `uniqueShape=false` here. **The corrected P2M resolver itself labels seven observed tokens `UNKNOWN` and returns no row for unobserved `.kjm`; it does not label these eight as resolver `CANDIDATE` pairs.** No identity is proved and all observed members remain in the blocker table. This preserves the resolver output rather than changing its classification to match the historical annotation.

See [P2M resolution report](P2M_RESOLUTION_REPORT.md) for resolver method, classification counts, provenance, and full inventory pointer. Exact per-type fields and evidence are in [P2M_BLOCKERS.json](P2M_BLOCKERS.json).
