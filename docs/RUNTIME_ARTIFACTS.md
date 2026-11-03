# Linux runtime artifacts

## Private runtime root

The host reads build-specific data from `/home/nill/.local/share/D3B/data`. The current staged build is `3.7.13.17` under `/home/nill/.local/share/D3B/data/3.7.13.17`. Keep the D3B data directories owned by `nill` with mode `0700` and each pack, schema, and mapping file at mode `0600`. These files are derived from the installed client and must not be committed.

| File | Size | SHA-256 |
| --- | ---: | --- |
| `data-8.pack` | 26,890,912 B | `28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0` |
| `schema-1.binpb` | 170,287 B | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` |
| `mapping-15.binpb` | 14,688 B | `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e` |

The protocol schema is byte-identical to `3.7.12.12`; the mapping pairs are identical. The pack is client `3.7.13.17`, build date `2026-11-03T14:11:23Z`, extraction fingerprint `f936e23d81b6ed18732d0245ca87e10003365ec0694d360ae1a02451cfd2a93b`. The mapping audit has 0 failures, 2 notes and 100 partial messages, with no renumbering. The `3.7.13.17` pins are `src/D3B.Protocol/Pins/3.7.13.17.json`, derived from the `3.7.12.12` pins after proving the schema and readable digest are identical. The pack has one zaap (81) on map 243140612 outside the world graph; the extractor records it as a note and the pathfinder ignores it.

The previous P2C build remains under `/home/nill/.local/share/D3B/data/3.7.12.12` for its evidence and must not be used for `3.7.13.17`:

| File | Size | SHA-256 |
| --- | ---: | --- |
| `data-8.pack` | 26,870,514 B | `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175` |
| `schema-1.binpb` | 170,287 B | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` |
| `mapping-15.binpb` | 14,687 B | `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` |

The live dedicated host apphost is `/home/nill/.local/share/D3B/apphost/D3B.Host`, 78,192 bytes, SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`, x86-64 ELF PIE, mode `0700`, owner `nill`. Its informational version is `0.0.0-alpha.0.10+0b84ba47f60631f6443f5b4301f1c3751f617beb`. The managed payload files are `D3B.Host.dll` (5,632 B, SHA-256 `0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`) and `D3B.Network.dll` (108,544 B, SHA-256 `0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`); `D3B.Bot.dll` is `65e446ed…`. The ELF launcher hash is stable across republishes and does not alone identify the managed code; verify the payload hashes and informational version too. No capability is applied. The 443 capability procedure is restricted to this apphost and requires separate owner authorization; do not set it on shared `dotnet` or run the host as root.

The P2D code build is staged at `/tmp/p2d-3.7.13.17/apphost`; it is **not** installed over the live P2B apphost. Its launcher is the same `de956557…` ELF, its `D3B.Host.dll` is 5,632 B `f3e1155d…`, `D3B.Network.dll` 112,640 B `fb341cd4…`, `D3B.Bot.dll` 650,240 B `238d5025…`, and its informational version is `0.0.0-alpha.0.11+55b0df7…`. It has no capability. A new real session is blocked until this payload is installed as a set, with the directory `0700` and launcher `0700`, and every hash re-verified.

## Publish the apphost

The apphost was published from the repository root at integrated P2B master `0b84ba47f60631f6443f5b4301f1c3751f617beb` with:

```sh
/home/nill/.dotnet/dotnet publish src/D3B.Host/D3B.Host.csproj --configuration Release --runtime linux-x64 --self-contained true -p:UseAppHost=true --artifacts-path /tmp/p2b-apphost/build --output /home/nill/.local/share/D3B/apphost -p:RestoreSources=/tmp/p2a-impl/emptyfeed -p:RestoreIgnoreFailedSources=true --verbosity minimal
```

Republish after any code change, then verify the `D3B.Host` launcher and both `D3B.Host.dll` and `D3B.Network.dll` hashes, plus the informational version. If the 443 capability is ever authorized, reapply and verify it after republishing because replacing the apphost can remove it. No capability is applied here.

The P2D build was published offline from the P2D working tree with the same command, but to `--output /tmp/p2d-3.7.13.17/apphost`; the live apphost was left at P2B. The staged identity is the one above (`D3B.Host.dll` `f3e1155d…`, `D3B.Network.dll` `fb341cd4…`, `D3B.Bot.dll` `238d5025…`, informational version `0.0.0-alpha.0.11+55b0df7…`). Do not copy a mixed set: install the whole staged directory and re-verify every hash.

## Verify staged files

From the repository root, verify the existing private stage before host use:

```sh
DATA_ROOT=/home/nill/.local/share/D3B/data
DATA="$DATA_ROOT/3.7.13.17"
(
  cd "$DATA"
  sha256sum -c - <<'EOF'
28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0  data-8.pack
e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0  schema-1.binpb
f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e  mapping-15.binpb
EOF
)
stat -c '%a %U %n' "$DATA_ROOT" "$DATA" "$DATA/data-8.pack" "$DATA/schema-1.binpb" "$DATA/mapping-15.binpb"
```

All checksums must pass, the two directories must report mode `700` and owner `nill`, and the files must report mode `600` and owner `nill`. The apphost check is:

```sh
APPHOST_DIR=/home/nill/.local/share/D3B/apphost
APPHOST="$APPHOST_DIR/D3B.Host"
stat -c '%s %a %U %n' "$APPHOST"
file "$APPHOST"
sha256sum "$APPHOST" "$APPHOST_DIR/D3B.Host.dll" "$APPHOST_DIR/D3B.Network.dll"
getcap "$APPHOST"
```

Expected: `78192 700 nill`, x86-64 ELF PIE; launcher SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`; `D3B.Host.dll` SHA-256 `0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`; `D3B.Network.dll` SHA-256 `0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`; informational version `0.0.0-alpha.0.10+0b84ba47f60631f6443f5b4301f1c3751f617beb`; and no capability output. The launcher and both managed payload hashes must match; the launcher hash by itself does not prove the managed code identity. This block checks the **live P2B apphost**; the staged P2D payload has different managed hashes, listed at the top of this document.

## Regenerate into scratch

Keep generated output under `/tmp/p2d-docs/`. Start from the repository root with the client closed. Before running the `dotnet run` commands, apply the writable CLI and XDG setup in [Linux acceptance](LINUX_ACCEPTANCE.md#writable-cli-and-xdg-directories). These commands read the installed client and write only to scratch:

```sh
CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
GENERATED=/tmp/p2d-docs/data

/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2d-docs/artifacts/inspect --file scripts/inspect.cs -- summary --client "$CLIENT"
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2d-docs/artifacts/extract --file scripts/extract.cs -- --client "$CLIENT" --output "$GENERATED" --force
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2d-docs/artifacts/schema --file scripts/schema.cs -- --client "$CLIENT" --unity-version 6000.3.16f1 --output "$GENERATED" --proto /tmp/p2d-docs/proto
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2d-docs/artifacts/mapping --file scripts/mapping.cs -- --client "$CLIENT" --data "$GENERATED" --pins src/D3B.Protocol/Pins --without-captures --json
```

`inspect summary` confirms the installed client identity. `extract` writes the build pack; `schema` writes the schema and optional readable schema output; `mapping` audits the readable protocol against that generated schema and the build pins. No capture is required or used. Require successful pack/schema generation and a mapping audit with zero failures before staging. The offline pack was read as format 8, client `3.7.13.17`, 17,639 entries, including 56,317 French text entries. The mapping's `ContractAudit` had 0 failures, 2 notes, 100 partial matches, 222 messages, 492 fields, 29 enums and 206 values; `ProtocolTranslation.Create` succeeded. `AuthenticationRequest` → `knc` has `Pin` evidence. The schema is byte-identical to `3.7.12.12`, and the mapping pairs are identical.

## Stage with verification and atomic promotion

Run as `nill`. For a new client version, stage the three generated files in a temporary directory under the same data root, verify their expected hashes, then rename the directory into place. The final `mv -T` is a same-filesystem atomic rename. This command refuses to replace an existing version directory and removes an incomplete stage on failure. The P2D `3.7.13.17` directory is already staged; use the verification command above for it. The `3.7.12.12` directory remains for the P2C evidence and is not used for the new build.

```sh
set -eu
umask 077
VERSION=3.7.13.17
SOURCE="/tmp/p2d-docs/data/$VERSION"
DATA_ROOT=/home/nill/.local/share/D3B/data
TARGET="$DATA_ROOT/$VERSION"
STAGE="$DATA_ROOT/.${VERSION}.stage.$$"

if [ -e "$TARGET" ]; then
  printf 'Refusing to replace existing artifact directory: %s\n' "$TARGET" >&2
  exit 1
fi
trap 'rm -rf -- "$STAGE"' EXIT
install -d -m 0700 "$DATA_ROOT" "$STAGE"
install -m 0600 "$SOURCE/data-8.pack" "$STAGE/data-8.pack"
install -m 0600 "$SOURCE/schema-1.binpb" "$STAGE/schema-1.binpb"
install -m 0600 "$SOURCE/mapping-15.binpb" "$STAGE/mapping-15.binpb"
(
  cd "$STAGE"
  sha256sum -c - <<'EOF'
28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0  data-8.pack
e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0  schema-1.binpb
f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e  mapping-15.binpb
EOF
)
mv -T "$STAGE" "$TARGET"
trap - EXIT
```

For another client build, replace `VERSION` and the expected artifact hashes with the reviewed values produced by regeneration. If the target already exists, stop and compare its identity; do not overwrite a staged build in place. Host settings for the first supervised test are:

```text
Client:Directory=/home/nill/.config/Ankama/Dofus-dofus3
Data:Directory=/home/nill/.local/share/D3B/data
Network:Redirect:Directory=/home/nill/.local/share/D3B/client
Network:Captures:Enabled=false
Fights:Alone=false
Network:Address=127.0.13.59
Network:Ports:0=5555
Network:Game:Ports:0=5556
Logging:LogLevel:D3B.Bot.Network.GameSession=Debug
```

The normal-user 5555-only start command and its supervised checks are in [Linux acceptance](LINUX_ACCEPTANCE.md). The 443 route uses default connection ports `[5555,443]` and remains blocked until explicit owner authorization for `cap_net_bind_service` on the dedicated apphost.

## Residual risks

Atomic replacement creates a new file entry, so it does not preserve symlink identity, ACLs, or ownership. A `SIGKILL` or a final temporary-file deletion failure can leave a hidden temporary file. A concurrent launcher or second host can race between reading the version file and replacing it. `Flush(true)` flushes file contents but does not fsync the containing directory, so a power loss can lose the directory update. Windows replacement and ACL behavior remain unverified. Runtime loading now compares the installed client's version and build date with the pack, and the redirect stops on a version/build-date change, but the full binary SHA-256 fingerprint is still a manual preflight gate (R02).

## Mapping tool-version identity

The `3.7.12.12` `mapping-15.binpb` hash `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` was generated with tool version `0.0.0-alpha.0.8+0dec0db`. Regenerating that mapping on the integrated checkout with tool version `alpha.0.9+9f1836d` produced SHA-256 `f253285eb61435b5b351a7f8b19c0fb01e4c7312316cab6922f62838de5e8ec6`; every pair and the audit were identical.

The current `3.7.13.17` mapping is `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e`, generated with tool version `0.0.0-alpha.0.11+55b0df7b08a0b01a7fc85748f32d8236a3b1e96c`. The schema is byte-identical to `3.7.12.12` and every message, enum, field, enum-value pair and audit result is identical. Mapping file hashes depend on the client version and the mapping tool version; compare pairs and audit results, not only the file hash.

## After a launcher update

Stop the host and close the client before allowing a launcher update. After the update, repeat `inspect summary` and the client identity checks in [Linux acceptance](LINUX_ACCEPTANCE.md). If version, build date, platform, Unity version, metadata version, or any binary hash changes, treat the installation as a new identity: do not reuse the old pack, schema, or mapping. Re-run the scratch extraction, schema generation, and mapping audit above; compare the new output and audit, and stage only after review under its version-specific directory. If the version stays the same but a binary fingerprint or build date changes, stop for manual review. Runtime loading now refuses a pack whose build date differs from the installed client, and the redirect stops when the version or build date changes during a run; the full binary fingerprint is still a manual preflight gate (R02). P2C observed such an update during a session (`3.7.12.12` → `3.7.13.17`): do not restore the old backup, and repeat the identity, extraction, schema, mapping and audit before any new host start. Do not change pins or readable `.proto` files to force an audit through without diagnosing the cause.

## Repository hygiene

Never commit installed client files, credentials, captures, extracted assets/data, generated packs, schemas, mappings, scratch outputs, or generated schema text. Keep generation under `/tmp/p2d-docs/`, keep runtime artifacts under the private local data root, and record only non-secret hashes and audit summaries in documentation.
