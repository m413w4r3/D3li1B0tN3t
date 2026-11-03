# Linux runtime artifacts

## Private runtime root

The host reads build-specific data from `/home/nill/.local/share/D3B/data`. The staged build is `3.7.12.12` under `/home/nill/.local/share/D3B/data/3.7.12.12`. Keep the D3B data directories owned by `nill` with mode `0700` and each pack, schema, and mapping file at mode `0600`. These files are derived from the installed client and must not be committed.

| File | Size | SHA-256 |
| --- | ---: | --- |
| `data-8.pack` | 26,870,514 B | `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175` |
| `schema-1.binpb` | 170,287 B | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` |
| `mapping-15.binpb` | 14,687 B | `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` |

The dedicated host apphost is `/home/nill/.local/share/D3B/apphost/D3B.Host`, 78,192 bytes, SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`, x86-64 ELF PIE, mode `0700`, owner `nill`. Its informational version is `0.0.0-alpha.0.10+0b84ba47f60631f6443f5b4301f1c3751f617beb`. The managed payload files are `D3B.Host.dll` (5,632 B, SHA-256 `0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`) and `D3B.Network.dll` (108,544 B, SHA-256 `0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`). The ELF launcher hash is stable across this republish and does not alone identify the managed code; verify the payload hashes and informational version too. No capability is applied. The 443 capability procedure is restricted to this apphost and requires separate owner authorization; do not set it on shared `dotnet` or run the host as root.

## Publish the apphost

The apphost was published from the repository root at integrated P2B master `0b84ba47f60631f6443f5b4301f1c3751f617beb` with:

```sh
/home/nill/.dotnet/dotnet publish src/D3B.Host/D3B.Host.csproj --configuration Release --runtime linux-x64 --self-contained true -p:UseAppHost=true --artifacts-path /tmp/p2b-apphost/build --output /home/nill/.local/share/D3B/apphost -p:RestoreSources=/tmp/p2a-impl/emptyfeed -p:RestoreIgnoreFailedSources=true --verbosity minimal
```

Republish after any code change, then verify the `D3B.Host` launcher and both `D3B.Host.dll` and `D3B.Network.dll` hashes, plus the informational version. If the 443 capability is ever authorized, reapply and verify it after republishing because replacing the apphost can remove it. No capability is applied here.

## Verify staged files

From the repository root, verify the existing private stage before host use:

```sh
DATA_ROOT=/home/nill/.local/share/D3B/data
DATA="$DATA_ROOT/3.7.12.12"
(
  cd "$DATA"
  sha256sum -c - <<'EOF'
fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175  data-8.pack
e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0  schema-1.binpb
c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d  mapping-15.binpb
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

Expected: `78192 700 nill`, x86-64 ELF PIE; launcher SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`; `D3B.Host.dll` SHA-256 `0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`; `D3B.Network.dll` SHA-256 `0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`; informational version `0.0.0-alpha.0.10+0b84ba47f60631f6443f5b4301f1c3751f617beb`; and no capability output. The launcher and both managed payload hashes must match; the launcher hash by itself does not prove the managed code identity.

## Regenerate into scratch

Keep generated output under `/tmp/p2b-docs/`. Start from the repository root with the client closed. Before running the `dotnet run` commands, apply the writable CLI and XDG setup in [Linux acceptance](LINUX_ACCEPTANCE.md#writable-cli-and-xdg-directories). These commands read the installed client and write only to scratch:

```sh
CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
GENERATED=/tmp/p2b-docs/data

/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/inspect --file scripts/inspect.cs -- summary --client "$CLIENT"
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/extract --file scripts/extract.cs -- --client "$CLIENT" --output "$GENERATED" --force
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/schema --file scripts/schema.cs -- --client "$CLIENT" --unity-version 6000.3.16f1 --output "$GENERATED" --proto /tmp/p2b-docs/proto
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/mapping --file scripts/mapping.cs -- --client "$CLIENT" --data "$GENERATED" --pins src/D3B.Protocol/Pins --without-captures --json
```

`inspect summary` confirms the installed client identity. `extract` writes the build pack; `schema` writes the schema and optional readable schema output; `mapping` audits the readable protocol against that generated schema and the build pins. No capture is required or used. Require successful pack/schema generation and a mapping audit with zero failures before staging. The offline pack was read as format 8, client `3.7.12.12`, 17,738 entries, including 56,227 French text entries. The mapping's `ContractAudit` had 0 failures, 2 notes, 100 partial matches, 222 messages, 492 fields, 29 enums and 206 values; `ProtocolTranslation.Create` succeeded. `AuthenticationRequest` → `knc` has `Pin` evidence.

## Stage with verification and atomic promotion

Run as `nill`. For a new client version, stage the three generated files in a temporary directory under the same data root, verify their expected hashes, then rename the directory into place. The final `mv -T` is a same-filesystem atomic rename. This command refuses to replace an existing version directory and removes an incomplete stage on failure. The P2B `3.7.12.12` directory is already staged; use the verification command above for it.

```sh
set -eu
umask 077
VERSION=3.7.12.12
SOURCE="/tmp/p2b-docs/data/$VERSION"
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
fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175  data-8.pack
e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0  schema-1.binpb
c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d  mapping-15.binpb
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
```

The normal-user 5555-only start command and its supervised checks are in [Linux acceptance](LINUX_ACCEPTANCE.md). The 443 route uses default connection ports `[5555,443]` and remains blocked until explicit owner authorization for `cap_net_bind_service` on the dedicated apphost.

## Residual risks

Atomic replacement creates a new file entry, so it does not preserve symlink identity, ACLs, or ownership. A `SIGKILL` or a final temporary-file deletion failure can leave a hidden temporary file. A concurrent launcher or second host can race between reading the version file and replacing it. `Flush(true)` flushes file contents but does not fsync the containing directory, so a power loss can lose the directory update. Windows replacement and ACL behavior remain unverified.

## Mapping tool-version identity

The staged `mapping-15.binpb` hash `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` was generated with tool version `0.0.0-alpha.0.8+0dec0db`. Regenerating the mapping on the integrated checkout with tool version `alpha.0.9+9f1836d` produces SHA-256 `f253285eb61435b5b351a7f8b19c0fb01e4c7312316cab6922f62838de5e8ec6`. Every message, enum, field, and enum-value pair and the audit are identical. Mapping file hashes depend on the mapping tool version; compare pairs and audit results, not only the file hash.

## After a launcher update

Stop the host and close the client before allowing a launcher update. After the update, repeat `inspect summary` and the client identity checks in [Linux acceptance](LINUX_ACCEPTANCE.md). If version, build date, platform, Unity version, metadata version, or any binary hash changes, treat the installation as a new identity: do not reuse the old pack, schema, or mapping. Re-run the scratch extraction, schema generation, and mapping audit above; compare the new output and audit, and stage only after review under its version-specific directory. If the client remains `3.7.12.12` but a binary fingerprint changes, stop for manual review because runtime loading does not enforce that fingerprint. Do not change pins or readable `.proto` files to force an audit through without diagnosing the cause.

## Repository hygiene

Never commit installed client files, credentials, captures, extracted assets/data, generated packs, schemas, mappings, scratch outputs, or generated schema text. Keep generation under `/tmp/p2b-docs/`, keep runtime artifacts under the private local data root, and record only non-secret hashes and audit summaries in documentation.
