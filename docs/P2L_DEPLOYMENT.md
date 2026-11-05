# P2L deployment preparation

**Status: checklist only.** This mission did not publish or install an apphost, install the Rust engine, modify Ankama configuration, or touch installed files. The live installation remains owner-gated.

## Safe offline staging now

Stage only from the exact reviewed integrated commit, in a scratch directory. Record `git rev-parse HEAD` before building and use the pinned `/home/nill/.dotnet/dotnet` SDK (`global.json` pins `10.0.403`). Keep all output under `/tmp/p2l-stage/` and do not publish into `/home/nill/.local/share/D3B/apphost`.

```sh
STAGE=/tmp/p2l-stage
APPHOST="$STAGE/apphost"
mkdir -p "$APPHOST"
/home/nill/.dotnet/dotnet publish src/D3B.Host/D3B.Host.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:UseAppHost=true \
  --artifacts-path "$STAGE/build" \
  --output "$APPHOST" \
  -p:RestoreSources=/tmp/p2a-impl/emptyfeed \
  -p:RestoreIgnoreFailedSources=true \
  --verbosity minimal

cd rust
cargo build --release --offline --locked
cd ..
install -d -m 0700 "$STAGE/engine"
install -m 0700 rust/target/release/d3b-diagnostic "$STAGE/engine/d3b-diagnostic"
```

R3 verified that `/tmp/p2a-impl/emptyfeed` exists. Rust is built from the audited `rust/` workspace using toolchain `1.83.0`, crate version `0.1.0`; the expected release binary path is `rust/target/release/d3b-diagnostic`.

Before review, set the scratch apphost directory to `0700`, its `D3B.Host` launcher to `0700`, and managed/runtime payload files to `0600`. Keep the Rust engine in a private `0700` directory with executable mode `0700`. Verify there is no file capability on the launcher or engine (`getcap` returns no capability). Record the following in a private staging manifest:

- source commit SHA and the informational version embedded by the publish;
- launcher, `D3B.Host.dll`, `D3B.Network.dll`, `D3B.Bot.dll`, `D3B.Diagnostic.dll`, and Rust engine SHA-256 values and file sizes;
- full sorted relative-path manifest and file count for the apphost directory;
- Unix owner/modes and `getcap` output;
- evidence that the published payload contains the P2L report code (`DiagnosticReportAggregator`, `AtomicDiagnosticReportWriter`, and report configuration in the published assemblies).

For example, from the staged apphost directory, record `find . -type f | LC_ALL=C sort` and a SHA-256 manifest over every file; also record `file "$APPHOST/D3B.Host"`, `sha256sum` of the named payloads and engine, and `getcap "$APPHOST/D3B.Host" "$STAGE/engine/d3b-diagnostic"`. Compare all values after copying to a private staging location. Do not commit generated manifests or binaries.

The current live P2J apphost is a pre-P2K set of 369 files with no `D3B.Diagnostic.dll`; it must be republished before P2L use. R3 found no named backup of that current P2J apphost under `private-backups`. The matching GO 1 `version.original` client backup exists at `go1-20261104T182159Z/version.original`; verify that exact backup before relying on it and make a fresh session backup before any client configuration change. The measured client, data, and P2J values are recorded in [RUNTIME_ARTIFACTS.md](RUNTIME_ARTIFACTS.md) and [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md); re-record them at preflight instead of treating an old hash as current.

## Owner-gated live steps

These steps require a separate explicit owner GO before touching the installed apphost or Ankama configuration.

1. Confirm no D3B host, Dofus client, or diagnostic engine is running. Record the current installed apphost manifest, launcher/payload hashes, modes, owner, and capability state.
2. Before installation, back up the **entire** current apphost directory under `~/.local/share/D3B/private-backups/apphost-p2l-<UTC>`. Use mode `0700` for backup directories and `0600` for payload files, retain the launcher as `0700`, and verify the backup manifest matches the live set. A current P2J backup was not found by R3, so create this one before replacement.
3. Install the complete reviewed staged apphost as one directory set. Never combine files from P2J and P2L. Verify the installed full manifest, file count, launcher and managed payload hashes, informational version, owner, `0700` directory/launcher and `0600` payload modes.
4. Install the reviewed Rust engine at `/home/nill/.local/share/D3B/diagnostic/d3b-diagnostic` in a private directory, record and verify its SHA-256 and executable mode, then check that `getcap` remains empty. Do not add a capability or privilege.
5. If a client redirect/configuration change is part of the approved session, verify a private byte-for-byte Ankama version-file backup first. Follow [P2L_LIVE_RUNBOOK.md](P2L_LIVE_RUNBOOK.md) and [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md); retain the backup until restoration is proved.

No network access, privileged launcher, root host, or capability is part of staging. Owner GO is required before any installed apphost or Ankama configuration change.

## Rollback

Stop the host and client, restore the complete backed-up apphost directory as one set, then verify its full manifest, file count, hashes, owner, modes, and empty capability state. Do not mix files during restore. Restore the client version file only from its verified private byte-for-byte backup if the redirect check proves the client still points to D3B. Do not restore an old client build after the Ankama launcher has updated the client; stop and re-stage for the current build instead.

Keep current measured values in [RUNTIME_ARTIFACTS.md](RUNTIME_ARTIFACTS.md), client identity and supervised procedure in [BUILD_IDENTITY.md](BUILD_IDENTITY.md) and [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md). This preparation does not grant GO 1, GO 2, mapping promotion, movement, harvest, or push approval.
