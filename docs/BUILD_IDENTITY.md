# Linux client build identity

## Current state and evidence

`ClientInstall.ComputeFingerprint()` hashes the version, platform, and present `GameAssembly`, `UnityPlayer`, and IL2CPP metadata files ([`scripts/unity/ClientInstall.cs:103-117`](../scripts/unity/ClientInstall.cs#L103)). Synthetic tests cover determinism and a changed binary ([`tests/D3B.Scripts.Tests/Unity/ClientInstallTests.cs:128-168`](../tests/D3B.Scripts.Tests/Unity/ClientInstallTests.cs#L128)); repository search found no production caller. Pack identity currently records the extraction input fingerprint in `PackInfo.Fingerprint` ([`src/D3B.Data/Packs/PackInfo.cs:5-20`](../src/D3B.Data/Packs/PackInfo.cs#L5)); schema identity is format/version plus file content, with no client build identity ([`src/D3B.Protocol/Mapping/SchemaFile.cs`](../src/D3B.Protocol/Mapping/SchemaFile.cs)); mapping records client version, schema SHA-256, readable protocol digest, and tool version ([`src/D3B.Protocol/Mapping/MappingFile.cs`](../src/D3B.Protocol/Mapping/MappingFile.cs)).

## Measured client identity snapshot (2026-11-02)

Read-only measurements were taken from `/home/nill/.config/Ankama/Dofus-dofus3`. The version file reports version `3.7.12.12`, build date `2026-10-19T14:46:54+00:00`, and `ConfigUrl=https://dofus2.cdn.ankama.com/config/dofus3.json`. Platform is Linux; Unity is `6000.3.16f1`; IL2CPP metadata version is `39`.

| File | Size | SHA-256 |
|---|---:|---|
| `Dofus.x64` | 4,472 B | `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff` |
| `GameAssembly.so` | 175,931,264 B | `0af08ea7dcc7fd15907be5761ff8b0eab62ddb020fc38ad588c06bc7f2e34da8` |
| `UnityPlayer.so` | 41,372,064 B | `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f` |
| `Dofus_Data/il2cpp_data/Metadata/global-metadata.dat` | 43,605,684 B | `fc135ad151e13adf297cc07b50c37680de4a367b8b757fdf958b4344c09d6c66` |

The Phase 0 pack is `/tmp/p04b/packs/3.7.12.12/data-8.pack`, SHA-256 `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175`, extraction fingerprint `2d869168d5a44e3828a153e530b06264291883be712d3b2963784b3139c00390`; schema is `/tmp/p011/schema/3.7.12.12/schema-1.binpb`, 170,287 bytes, SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` ([`docs/PHASE0.md:66`](PHASE0.md#L66)). At the Phase 1 baseline, no mapping had been produced. P2A later generated a temporary mapping after resolving the ambiguity with an evidenced pin; its identity is recorded below and in [P2_AUTH_MAPPING](P2_AUTH_MAPPING.md). This remains offline evidence, not live-client acceptance.

## Read-only identity record commands

Run from the repository root on the Linux client machine. The commands do not change client or identity artifact inputs; `dotnet` may use its standard build cache, and the mapping command directs that cache to `/tmp`:

```sh
CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
CLIENT_EXE="$CLIENT/Dofus.x64"
cat "$CLIENT/Dofus_Data/StreamingAssets/version"
/home/nill/.dotnet/dotnet run --file scripts/inspect.cs -- summary --client "$CLIENT"
uname -s
uname -m
sha256sum "$CLIENT_EXE" "$CLIENT/GameAssembly.so" "$CLIENT/UnityPlayer.so" "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
od -An -tu4 -j4 -N4 "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
sha256sum /tmp/p04b/packs/3.7.12.12/data-8.pack /tmp/p011/schema/3.7.12.12/schema-1.binpb
sha256sum src/D3B.Protocol/Pins/3.7.12.12.json
find src/D3B.Protocol -type f -name '*.proto' -print0 | LC_ALL=C sort -z | xargs -0 sha256sum | sha256sum
git rev-parse HEAD
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p1e-artifacts/mapping --file scripts/mapping.cs -- --client-version 3.7.12.12 --data /tmp/p1e/data --pins src/D3B.Protocol/Pins --without-captures --json
```

`inspect summary` reports the client version and Unity version; `od` reads the IL2CPP metadata header's little-endian version field (expected `39`). The Phase 1 mapping diagnostic reported the unmatched `AuthenticationRequest`; P2A's offline run now exits 0 and writes only under the temporary data root. Its mapping identity and audit are recorded in [P2_AUTH_MAPPING](P2_AUTH_MAPPING.md). The `.proto` tree hash and Git revision identify the readable protocol source revision; the runtime `ProtocolDigest` is a descriptor-level digest, not a raw file hash. Known artifact SHA-256 values above are the expected comparison values.

`PackInfo.Fingerprint` is embedded in the pack. There is no read-only CLI command in this checkout that prints it: `scripts/release.cs` reads it but also writes checksum sidecars. Use the recorded fingerprint above for this Phase 0 pack; do not call the release tool when a read-only audit is required. If a mapping is later produced, record its SHA-256 with `sha256sum <mapping-file>` and its `ToolVersion` from the mapping/release diagnostic.

**P2D supersedes the runtime limitation below:** `PackSource` now compares the installed client's build date with the pack, `ClientRedirect` stops the host on a version/build-date change while it runs, and the full binary fingerprint remains a manual preflight gate. See the P2D identity section at the end of this document.

## P1 and later gates

- **Offline P1 capture diagnostics:** protocol-independent replay/capture work is not blocked by mapping. Record the identity fields above alongside the offline evidence.
- **Supervised live Linux acceptance:** gated on a supervisor recording and reviewing this identity and completing the client configuration/restore safety checks. This manual record is P1's mitigation for the unwired fingerprint helper.
- **P2:** wire client build identity into pack, schema, and mapping artifact identity, with compatible format/version handling.

P1 changed no artifact formats. At the P1 baseline, the Linux schema did not yet have a passing mapping audit. P2A resolves that offline for build `3.7.12.12`; it does not claim live protocol translation acceptance.

## P2A mapping identity

| Artifact | Identity |
| --- | --- |
| Linux schema | `/tmp/p011/schema/3.7.12.12/schema-1.binpb`; 170,287 bytes; SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` |
| Public release schema | `/tmp/p2a-evidence/release/schema-1.binpb`; 170,287 bytes; same SHA-256 and byte-identical to the Linux schema |
| Build pins | `src/D3B.Protocol/Pins/3.7.12.12.json`; 99 message pins, 0 enum pins; SHA-256 `53a51f30cdb54d0122ad5add9ad47cfff1d31e90b386159283402ee42ceec018` |
| Readable protocol digest | `4e36426ac4d31cd517b8cb57d4b66d184f2ec9eaa85ed4ffc1448fb2d7c4cda9` |
| Public release mapping | `/tmp/p2a-evidence/release/mapping-15.binpb`; format 15, client `3.7.12.12`; 14,687 bytes; SHA-256 `6ce5a2dc148db21ef91f3e1c5cfcf8eda99ecbe0e4e9e7c54cdfa9333af6a7f0`; `AuthenticationRequest` → `knc`, evidence `Capture`, fields 1→1 and 2→2 |
| P2A offline mapping | `/tmp/p2a-impl/data/3.7.12.12/mapping-15.binpb`; format 15; 14,687 bytes; SHA-256 `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d`; tool `0.0.0-alpha.0.8+0dec0dbe9d65dca9037ae9f216b5f180881be937` |
| P2A audit | 222 messages, 492 fields, 29 enums, 206 values; 0 failures, 2 notes, 100 partial; translation creation succeeds |

The produced offline mapping has the same 222 message pairs, 29 enum pairs, message field pairs, and enum value pairs as the public release mapping. The release mapping records capture evidence, but it does not record the capture platform. Windows schema generation for this version remains unverified; no direct Linux live capture exists. The `3.7.12.12` version string and byte-identical schema do not establish that the historical release capture was made on Linux. See [P2_AUTH_MAPPING](P2_AUTH_MAPPING.md) for the candidate analysis, tests, and supervised observation limits.

## P2B staged runtime identity

The offline runtime artifacts are staged privately under `/home/nill/.local/share/D3B/data/3.7.12.12`. The directories are owned by `nill` with mode `0700`; the files are owned by `nill` with mode `0600`.

| Artifact | Size | SHA-256 | Runtime path |
| --- | ---: | --- | --- |
| Pack `data-8.pack` | 26,870,514 B | `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175` | `/home/nill/.local/share/D3B/data/3.7.12.12/data-8.pack` |
| Schema `schema-1.binpb` | 170,287 B | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` | `/home/nill/.local/share/D3B/data/3.7.12.12/schema-1.binpb` |
| Mapping `mapping-15.binpb` | 14,687 B | `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` | `/home/nill/.local/share/D3B/data/3.7.12.12/mapping-15.binpb` |

Offline validation opened the pack as client `3.7.12.12`, format 8, with 17,738 entries and 56,227 French text entries. `ProtocolSource.Load` and `ProtocolTranslation.Create` succeeded. `ContractAudit` reported 0 failures, 2 notes and 100 partial matches across 222 messages, 492 fields, 29 enums and 206 values. `d3b.game.AuthenticationRequest` maps to `knc` with `Pin` evidence. This does not establish live Linux compatibility.

The staged mapping was produced with tool version `0.0.0-alpha.0.8+0dec0db`. Regenerating it from the integrated checkout with `alpha.0.9+9f1836d` produces SHA-256 `f253285eb61435b5b351a7f8b19c0fb01e4c7312316cab6922f62838de5e8ec6`; all pairs, values and audit results are identical. Mapping file hashes depend on the mapping tool version. When comparing regenerated mappings, compare message/enum/field/value pairs and the audit, not only the file hash. See [runtime artifact staging and regeneration](RUNTIME_ARTIFACTS.md).

The dedicated host apphost was republished from integrated P2B master `0b84ba47f60631f6443f5b4301f1c3751f617beb`. `/home/nill/.local/share/D3B/apphost/D3B.Host` is 78,192 bytes, SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`, x86-64 ELF PIE, owner `nill`, mode `0700`. Its informational version is `0.0.0-alpha.0.10+0b84ba47f60631f6443f5b4301f1c3751f617beb`. The managed payload is `D3B.Host.dll`, 5,632 bytes, SHA-256 `0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`, and `D3B.Network.dll`, 108,544 bytes, SHA-256 `0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`. The launcher hash is stable across this republish and does not identify the managed code by itself; the payload hashes and informational version establish that identity. No file capability is applied. The 443 fallback capability is documented but has not been applied. See [runtime artifact publishing and verification](RUNTIME_ARTIFACTS.md#publish-the-apphost).

**Runtime identity limitation (R02):** `ClientInstall.ComputeFingerprint()` is not enforced by runtime artifact loading. `PackSource` checks the client and pack version; `ProtocolSource` loads and audits the versioned schema and mapping, but the host does not compare the installed executable, `GameAssembly.so`, `UnityPlayer.so`, or metadata hashes with a recorded fingerprint. The exact binary identity therefore remains a manual preflight gate. Follow [Linux acceptance](LINUX_ACCEPTANCE.md) before any supervised client attempt.

## P2D client 3.7.13.17 identity (2026-11-03)

The launcher updated the client from `3.7.12.12` to `3.7.13.17` during the P2C supervised session. The measured identity is:

| File | Size | SHA-256 |
| --- | ---: | --- |
| `Dofus.x64` | 4,472 B | `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff` (unchanged) |
| `GameAssembly.so` | 175,835,648 B | `f2aa597d4ca5550d4188d878f6dd577d79fbfc01615bc48f073a1c4d8d9c76a0` |
| `UnityPlayer.so` | 41,372,064 B | `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f` (unchanged) |
| `Dofus_Data/il2cpp_data/Metadata/global-metadata.dat` | 43,581,760 B | `dea17c3c465a1937737e848a290057db9812bc2e7f4f8176d8ae1bf2616cdc9d` |

Version `3.7.13.17`, build date `2026-11-03T14:11:23+00:00`, Linux x86_64, Unity `6000.3.16f1`, metadata `39`. The `version` file SHA-256 is `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`. The `ClientInstall.ComputeFingerprint()` value is `6accb105954a5eeacb15bd9737e607705102fe5bbc919a8d59ffa1dd6e0329b3`.

The P2D staged runtime is:

| Artifact | Size | SHA-256 |
| --- | ---: | --- |
| Pack `data-8.pack` | 26,890,912 B | `28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0` |
| Schema `schema-1.binpb` | 170,287 B | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` |
| Mapping `mapping-15.binpb` | 14,688 B | `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e` |
| Pins `3.7.13.17.json` | same pairs as `3.7.12.12.json` | only `clientVersion` differs |

The protocol schema is byte-identical to `3.7.12.12` (`e383c947…`), and the readable digest is unchanged (`4e36426…`). The mapping pairs are identical: 222 messages, 492 fields, 29 enums and 206 values; 0 failures, 2 notes and 100 partial messages; no renumbering. The new pins were accepted only after this identity proof, not copied blindly.

The P2D apphost was published from commit `c9c0d5e0660ecbd373380f1f57f2015b6f459125` and installed at `/home/nill/.local/share/D3B/apphost`. Its launcher is 78,192 B `de956557…`; `D3B.Host.dll` 5,632 B `c6589b05…`, `D3B.Network.dll` 112,640 B `279e2cf3…`, `D3B.Bot.dll` 650,240 B `a71bdb88…`; informational version `0.0.0-alpha.0.12+c9c0d5e…`; directory `0700`, launcher `0700`, payload `0600`; no capability. The P2B publication is backed up at `~/.local/share/D3B/private-backups/apphost-p2b-20261103T162503Z` with its verified 369-file manifest. Every `D3B.*.dll` in the live set differs from the P2B backup; no old P2B managed DLL is mixed in.

**P2D update to R02:** `PackSource` now refuses a pack whose `ClientBuildDate` differs from the installed client's build date, even when the version string matches. `ClientRedirect` now compares the version file's version and build date with the installed build and calls `IHostApplicationLifetime.StopApplication()` on a change, so the host stops instead of relaying a protocol it was not prepared for; the normal stop preserves the launcher's new Ankama configuration. The full binary SHA-256 fingerprint is still not enforced at runtime, and `PackInfo.Fingerprint` is the extractor input fingerprint, not the binary identity. A same-version, same-build-date, different-binary build remains a manual preflight gate.

## P2J apphost identity (2026-11-04)

The P2J preparation published a fresh Linux apphost from the exact code commit
`bef5066d0c17cb2d89d9ef0d5fd98185337c0449` on local branch `p2j-supervised-move` (not pushed) and
installed it at `/home/nill/.local/share/D3B/apphost` as one verified set. The installed client
remains `3.7.13.17` with the identity above; no client file or launcher was touched.

| File | Size | SHA-256 |
| --- | ---: | --- |
| `D3B.Host` (launcher) | 78,192 B | `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0` |
| `D3B.Host.dll` | 5,632 B | `b4c48761cecec769e314d9197eb5fd99842c72c5cb53f913eefab83c0f0da39e` |
| `D3B.Network.dll` | 118,272 B | `6789048fb7befaa29b00d0b4f4d543d9e0998d884c4513178140e8f4921496b0` |
| `D3B.Bot.dll` | 784,896 B | `3debd3ce1d8522a0363d06ca85d642d43e8c709266216e8e9e4ea71ec48df8fa` |

The informational version is `0.0.0-alpha.0.56+bef5066d0c17cb2d89d9ef0d5fd98185337c0449`. The
launcher is a stripped x86-64 ELF PIE; directory `0700`, launcher `0700`, payload `0600`, owner
`nill`; no capability. The complete 369-file manifest matches the publish output. The P2F apphost is
preserved at `~/.local/share/D3B/private-backups/apphost-p2f-20261104T173953Z` and
`apphost-p2f-live-20261104T180525Z`. No host, relay, client, or movement was started during the
publication. See [P2J_REPORT](P2J_REPORT.md).
