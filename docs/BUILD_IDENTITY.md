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

The Phase 0 pack is `/tmp/p04b/packs/3.7.12.12/data-8.pack`, SHA-256 `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175`, extraction fingerprint `2d869168d5a44e3828a153e530b06264291883be712d3b2963784b3139c00390`; schema is `/tmp/p011/schema/3.7.12.12/schema-1.binpb`, 170,287 bytes, SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` ([`docs/PHASE0.md:66`](PHASE0.md#L66)). No mapping was produced; mapping SHA and tool version remain **UNKNOWN**. This is an identity snapshot, not live-client acceptance.

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

`inspect summary` reports the client version and Unity version; `od` reads the IL2CPP metadata header's little-endian version field (expected `39`). The read-only mapping diagnostic prints the runtime `readableDigest` and `toolVersion` in its JSON before returning its audit status; with the known mismatch it exits 1 and writes no mapping. The `.proto` tree hash and Git revision identify the readable protocol source revision; the runtime `ProtocolDigest` is a descriptor-level digest, not a raw file hash. Known artifact SHA-256 values above are the expected comparison values.

`PackInfo.Fingerprint` is embedded in the pack. There is no read-only CLI command in this checkout that prints it: `scripts/release.cs` reads it but also writes checksum sidecars. Use the recorded fingerprint above for this Phase 0 pack; do not call the release tool when a read-only audit is required. If a mapping is later produced, record its SHA-256 with `sha256sum <mapping-file>` and its `ToolVersion` from the mapping/release diagnostic.

## P1 and later gates

- **Offline P1 capture diagnostics:** protocol-independent replay/capture work is not blocked by mapping. Record the identity fields above alongside the offline evidence.
- **Supervised live Linux acceptance:** gated on a supervisor recording and reviewing this identity and completing the client configuration/restore safety checks. This manual record is P1's mitigation for the unwired fingerprint helper.
- **P2:** wire client build identity into pack, schema, and mapping artifact identity, with compatible format/version handling.

P1 changes no artifact formats: the scope is offline capture diagnostics and supervised live acceptance, and changing pack/schema/mapping identity formats belongs to P2. P1 must not claim that the Linux schema has a compatible or valid mapping, that the mapping audit passes, or that live protocol translation is accepted.

The mapping diagnostic is limited evidence: the Linux schema loaded and ran through matching and audit, but `d3b.game.AuthenticationRequest` had no counterpart, one audit failure remained, and no mapping was written. The next smallest safe experiment is a read-only trace of `AuthenticationRequest` across the readable descriptor, the build's pins, and the Linux schema to identify whether the cause lies in the readable definition, pins, schema, or matching. Do not change pins or protocol files until that cause is known.
