# P2D — controlled migration to client 3.7.13.17

**Status: the artifact/data layer is READY, and the P2D apphost is installed and verified. A new
real supervised session is READY for a separately authorized P2E GO.** No host, relay to Ankama, or
Dofus client was started in this mission. The P2C `3.7.12.12` report is preserved in
[P2C_REPORT](P2C_REPORT.md).

The source commit is `c9c0d5e0660ecbd373380f1f57f2015b6f459125` on the local branch
`p2d-linux-3.7.13.17`; the apphost was published from that clean tree. The P2B apphost was backed up
to `~/.local/share/D3B/private-backups/apphost-p2b-20261103T162503Z` before replacement.

## A. Build identity

| Field | Value |
| --- | --- |
| Version | `3.7.13.17` |
| Build date | `2026-11-03T14:11:23+00:00` |
| Platform | Linux x86_64 |
| Unity | `6000.3.16f1` |
| IL2CPP metadata | 39 |
| `Dofus.x64` | 4,472 B, SHA-256 `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff` (unchanged) |
| `GameAssembly.so` | 175,835,648 B, SHA-256 `f2aa597d4ca5550d4188d878f6dd577d79fbfc01615bc48f073a1c4d8d9c76a0` |
| `UnityPlayer.so` | 41,372,064 B, SHA-256 `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f` (unchanged) |
| `global-metadata.dat` | 43,581,760 B, SHA-256 `dea17c3c465a1937737e848a290057db9812bc2e7f4f8176d8ae1bf2616cdc9d` |
| `version` file | SHA-256 `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae` |
| `ClientInstall.ComputeFingerprint()` | `6accb105954a5eeacb15bd9737e607705102fe5bbc919a8d59ffa1dd6e0329b3` |

The launcher had finished the update: no Dofus, host or updater process remained; the client identity
was re-checked after the extraction and after the schema generation and did not change. The identity
was recorded before each step and compared to the initial manifest.

## B. Protocol diff `3.7.12.12` → `3.7.13.17`

The protocol schema is **byte-identical** to `3.7.12.12`:

| Artifact | `3.7.12.12` | `3.7.13.17` |
| --- | --- | --- |
| Schema | 170,287 B, `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` | 170,287 B, same SHA-256 |
| Readable protocol digest | `4e36426ac4d31cd517b8cb57d4b66d184f2ec9eaa85ed4ffc1448fb2d7c4cda9` | same |
| Mapping messages | 222 | 222 |
| Mapping fields | 492 | 492 |
| Mapping enums | 29 | 29 |
| Mapping enum values | 206 | 206 |
| Message/field/enum/value pairs | — | **no difference** (0 missing, 0 added, 0 changed) |
| Renumberings | 0 | **0** |
| Audit | 0 failures, 2 notes, 100 partial | **0 failures, 2 notes, 100 partial** |

The game data changed, as the pack comparison shows:

| Count | `3.7.12.12` | `3.7.13.17` | Difference |
| --- | ---: | ---: | --- |
| Pack entries | 17,738 | 17,639 | -99 |
| Maps | 17,722 | 17,623 | -99 (129 old-only, 30 new-only, 17,593 common) |
| French texts | 56,227 | 56,317 | +90 |
| Graph vertices | 11,327 | 11,331 | +4 |
| Graph edges | 30,909 | 30,917 | +8 |
| Graph transitions | 31,329 | 31,337 | +8 |
| Graph criteria | 619 | 619 | 0 |
| Atlas positions | 15,409 | 15,470 | +61 |
| Atlas sub-areas | 567 | 569 | +2 |
| Atlas areas | 69 | 70 | +1 |
| Atlas zaaps | 62 | 63 | +1 |
| Harvestables | 1,318 | 1,318 | 0 |
| Monsters | 3,039 | 3,039 | 0 |

The map difference is a real content change, not a partial read: the old-only IDs are the contiguous
`241,96x,xxx` range and the new-only IDs are in `241,97x,xxx` and `243,2xx,xxx`+; the extraction read
17,623 maps in both layouts (`15,884 ClientMapData`, `1,739 MapMetadata`), every atlas position has a
map, every graph vertex has a map, and every zaap was checked.

The new build adds zaap 81 on map `243140612`. That map is a real client map (560 cells, 330
walkable, one zone), has an atlas position (`x=20`, `y=67`, sub-area `1137`, world map `1`) and the
sub-area exists. It is **not** a vertex of the world graph. The pathfinder ignores a zaap whose map is
not in the graph (`WorldPathfinder.ZaapsOf` requires a graph vertex and an atlas position), so the
extraction reports it as a note and keeps the stronger checks: the map must be in the client, and the
sub-area must be in the atlas.

## C. Mapping

- The `3.7.12.12` mapping has schema SHA-256 `e383c947…` and readable digest `4e36426…`. The new
  schema and the current readable protocol have the same identities.
- The `3.7.13.17` pins are therefore the `3.7.12.12` pins with only `clientVersion` changed. This was
  not a blind copy: the schema is byte-identical, the readable digest is identical, and the mapping
  audit passes with the new data. The pins file is committed as `src/D3B.Protocol/Pins/3.7.13.17.json`.
- Matching without pins from structure alone fails on many fight messages, as expected; the pins
  resolve them. The verified run pairs 222 messages (99 by pin, 92 by field, 31 by score) and 29 enums
  (25 by field, 4 by score).
- `ContractAudit`: 222 messages, 492 fields, 29 enums and 206 values; **0 failures**, 2 notes, 100
  partial messages. `ProtocolTranslation.Create` succeeds; no readable field or enum value is
  renumbered.
- The installed mapping is 14,688 B, SHA-256
  `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e`. Its file hash differs
  because the client version and tool version strings differ; every message, field, enum and value
  pair is identical.
- The runtime stage is `/home/nill/.local/share/D3B/data/3.7.13.17`: pack 26,890,912 B
  `28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0`, schema 170,287 B
  `e383c947…`, mapping 14,688 B `f42deadf…`. Directories are `0700`, files `0600`, owner `nill`.
  `data/3.7.12.12` is untouched.

## D. TDD

New deterministic tests:

- `ClientVersionFileTests`: build parse, missing version, malformed build date.
- `ClientRedirectTests`:
  - `AChangedBuildStopsTheHostAndKeepsTheLaunchersNewConfiguration`
  - `ASameVersionWithAnotherBuildDateStopsTheHost`
  - `AChangedBuildAtStartLeavesTheVersionFileUntouched`
- `PackSourceTests`:
  - `APackOfAnotherBuildWithTheSameVersionIsRefused`
  - `APackOfAnotherVersionOnThisMachineIsRefused`
- `WorldPathfinderTests.AZaapOnAMapTheGraphDoesNotHoldIsIgnored`
- `GameLogTests`: decoded game message fields; unknown-game-message warning without the payload.
- `LoggingTests.TheSupervisedArgumentEnablesTheGameSessionAtDebug`
- Existing test updated: `GameSessionTests.FramesAreLoggedAtTheDebugLevelByWhatTheyAreNeverByWhatTheyHold`
  now expects the new warning; `TestRelay` supplies a fake host lifetime for the new redirect dependency.

Commands actually run:

| Command | Result |
| --- | --- |
| `dotnet build D3B.slnx` | **0 warnings, 0 errors** |
| `dotnet test --project tests/D3B.Network.Tests/...` | **133/133 passed** |
| `dotnet test --project tests/D3B.Bot.Tests/...` | **439/439 passed** |
| `dotnet test --project tests/D3B.Engine.Tests/...` | **257/257 passed** |
| `dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build` | **1265/1265 passed** |
| `dotnet format --verify-no-changes` | **exit 0** |
| `scripts/mapping.cs` with `src/D3B.Protocol/Pins` | exit 0, 0 failures, 2 notes, 100 partial, no renumbering |
| offline runtime check of pack/schema/mapping | `pack_identity_ok=True`, `mapping_identity_ok=True`, `translation_ok=True`, `READY=True` |

## E. Protection against updates

- A pack whose `ClientBuildDate` differs from the installed client's `BuildDate` is refused, even when
  the version string matches (`PackSource`).
- When the launcher rewrites the version file with another version or build date while the host runs,
  `ClientRedirect` logs the old and new identities and calls `IHostApplicationLifetime.StopApplication()`.
  It does not reapply the redirect, leaves the launcher's new Ankama URL in place, and the normal stop
  restores the client. There is no hot reload of the mapping and no continued automated action.
- At start, a version/build mismatch fails the redirect and leaves the version file untouched.
- `GameLog.NotUnderstood` is a `Warning` for a game frame whose message is unknown or whose translation
  does not hold. It names the message, side, length and problem; it never logs the payload.
- The supervised logging configuration is
  `--Logging:LogLevel:D3B.Bot.Network.GameSession Debug`. It exposes decoded game message names,
  direction, length and state. Character attachment and map/cell updates are logged at information by
  the existing `BotLog` (`Attached`, `ChangingMap`, `ChangedMap`, `Walking`). Raw connection/character
  identifiers stay in the local log and must not be shared.

**Residual risk (R02):** the full binary fingerprint is still a manual preflight gate. `PackInfo.Fingerprint`
is the extractor input fingerprint; the runtime checks the pack version and build date, not the
SHA-256 of every client binary. A same-version, same-build-date, different-binary build is not refused
at runtime. The P2C launcher update is covered by the build-date change; a binary-only rebuild with the
same identity is not.

## F. Linux validation summary

| Component | Status |
| --- | --- |
| Client identity `3.7.13.17` | **READY** — read-only identity and fingerprints verified |
| Pack / schema / mapping | **READY** — offline load, audit and translation pass |
| Runtime data stage | **READY** — `data/3.7.13.17` installed, `data/3.7.12.12` intact |
| Mapping pins | **READY** — `3.7.13.17.json`, pairs identical to `3.7.12.12` |
| P2D apphost payload | **INSTALLED** — `/home/nill/.local/share/D3B/apphost`: launcher 78,192 B `de9565…`, `D3B.Host.dll` 5,632 B `c6589b05…`, `D3B.Network.dll` 112,640 B `279e2cf3…`, `D3B.Bot.dll` 650,240 B `a71bdb88…`, informational version `0.0.0-alpha.0.12+c9c0d5e…`, directory `0700`, launcher `0700`, payload `0600`, no capability |
| Live apphost | **P2D installed** — the P2B publication is backed up at `~/.local/share/D3B/private-backups/apphost-p2b-20261103T162503Z` (`D3B.Host.dll` `0cc2…`, `D3B.Network.dll` `0ce5…`, `D3B.Bot.dll` `65e4…`), 369 files and a verified manifest |
| New real supervised session | **READY** — subject to a separate owner GO; use [LINUX_ACCEPTANCE](LINUX_ACCEPTANCE.md); no host or Dofus has been started |
| No host/relay/Dofus launched in P2D/P2E | confirmed |

The P2D apphost is installed; the next gate is the owner's separate GO for the P2E live session. No
host or client is started by this report. The P2C evidence is preserved; the `3.7.12.12` mapping is
not reused.
