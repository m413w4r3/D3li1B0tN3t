# Linux client acceptance runbook

**Status: P2E is COMPLETE/PASS** for connection and relay, authentication, character selection, the
supervised game session, and stop/restore on `3.7.13.17`. Frame decoding was mixed. P2G subsequently
completed and passed visual map/cell validation on Dofus Linux `3.7.13.17`; see
[P2G_REPORT.md](P2G_REPORT.md). The P2F procedure below is retained as a historical, reproducible
runbook. Any further live session requires a new owner GO, including the future one-neighbour
automated move; P2G did not authorize or execute that move. P2J subsequently prepared the single
one-neighbour trigger and its passive readiness checks without executing them; see
[P2J_REPORT.md](P2J_REPORT.md) and the P2J GO 1 / GO 2 procedure below. The 443 route remains
**BLOCKED** until explicit owner authorization for the dedicated apphost capability; no capability
has been applied.

## P2J — one supervised map transition (prepared, not executed)

**Status: GO 1 executed on 2026-11-04 — observation PASS, movement BLOCKED.** P2J added a
disabled-by-default, single-use trigger for one map transition to a direct neighbour, plus a
passive readiness check that distinguishes the P2I translation latch from `MovementGuard`. The
live passive session found the latch closed before first placement; no movement and no push
occurred. See [P2J_GO1_REPORT.md](P2J_GO1_REPORT.md). The P2J apphost identity is recorded in
[P2J_REPORT.md](P2J_REPORT.md).

The P2E aggregate recorded **210 not read** frames out of 848. The P2I latch condition
(`!IsRead && !IsUnderstood`) is exactly the condition that produced that bucket, so all 210 would
have latched movement for the session. The retained P2E evidence does **not** record whether they
came before or after the first placement. GO 1 measures that live and sends nothing; GO 2 is
forbidden until GO 1 shows the latch open.

### The host command

The reader exists only in `D3B.Host` and only when `--Supervised:Move:Enabled true` is present. Keep
the host attached to the terminal: commands are read from its standard input. The desktop app does
not register the reader.

```sh
/home/nill/.local/share/D3B/apphost/D3B.Host \
  --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
  --Data:Directory /home/nill/.local/share/D3B/data \
  --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
  --Network:Captures:Enabled false \
  --Fights:Alone false \
  --Network:Address 127.0.13.59 \
  --Network:Ports:0 5555 \
  --Network:Game:Ports:0 5556 \
  --Supervised:Move:Enabled true \
  --Logging:LogLevel:D3B.Bot.Network.GameSession Debug \
  --Logging:LogLevel:D3B.Bot.Runtime.BotActor Information
```

The host prints one banner line:

```text
P2J supervised move enabled. Commands: p2j readiness [<sourceMapId> <targetMapId>] | p2j move <sourceMapId> <targetMapId>
```

The commands are deliberately small:

| Command | Effect |
| --- | --- |
| `p2j readiness` | Prints the passive session/position readiness. Sends nothing. Does not consume the move. |
| `p2j readiness <source> <target>` | Prints the readiness and the plan for that pair. Sends nothing. Does not consume. |
| `p2j move <source> <target>` | Consumes the one authorization, then attempts exactly one plain scroll to the target. Every later `move` in the same host process is refused. |

### GO 1 — observation (no movement)

Perform every preflight from [RUNTIME_ARTIFACTS](RUNTIME_ARTIFACTS.md) and the P2J identity in
[P2J_REPORT](P2J_REPORT.md). In particular: client closed, no host/relay/updater, client identity
`3.7.13.17` with the recorded hashes, staged pack/schema/mapping verified, private version-file
backup present, ports 5555/5556 free, captures off, `Fights:Alone=false`, no profile/activity/
schedule able to act, P2J apphost installed as one verified set with private permissions and no
capability.

1. Start the host with the command above. Keep its terminal in the foreground.
2. Launch Dofus manually and select the character. Do not start an activity or a profile.
3. Wait for the character to be visible and placed. Then type exactly:

   ```text
   p2j readiness
   ```

4. Compare the printed line with the visible game. A passing line has:

   ```text
   connected=true session=true translation_blocked=false
   translation_blocked_at_first_placement=false|unknown
   paused=false acting=false haven_bag=false guard=None
   map=<visible map> cell=<visible cell> provenance=ServerPlaced
   trusted=true can_move=true reason=READY
   ```

   - `translation_blocked=true` or `reason=TRANSLATION_LATCH_CLOSED` means an unread frame closed
     the session's movement authorization. **GO 1 is BLOCKED.** Stop; do not attempt a move. The
     fail-closed latch must not be disabled or loosened. Preserve only sanitized aggregates and plan
     a mapping improvement in a later mission.
   - `translation_blocked_at_first_placement=true` confirms the latch was already closed when the
     first trusted placement was observed. Same STOP.
   - Any guard other than `None`, any `trusted=false`, or a map/cell disagreement with the visible
     client is a STOP.
5. Optionally check the proposed pair without consuming the move:

   ```text
   p2j readiness <sourceMapId> <targetMapId>
   ```

   A passing plan prints `P2J PLAN ready=true reason=PLAN_READY ... transition=Scroll`. Only a
   direct map-data neighbour with a plain scroll is accepted. `FORBIDDEN_TRANSITION`,
   `TARGET_NOT_NEIGHBOUR`, `TRANSITION_UNAVAILABLE`, or any other reason is a STOP.
6. Confirm that the host has logged no `D3B sent` movement request and that no map change occurred.
   The only expected lines are the banner, readiness/plan lines, and the ordinary relay/state logs.

**GO 1 PASS** requires a trusted server-placed position matching the visible client, an open
translation latch, `guard=None`, and a `PLAN_READY` pair. GO 1 never authorizes a move.

### GO 2 — the single move

Only after GO 1 PASS and a separate owner GO:

1. The supervisor names one source map and one directly adjacent target map, and confirms both on
   the visible client. The pair must be the one checked in GO 1.
2. Type exactly once:

   ```text
   p2j move <sourceMapId> <targetMapId>
   ```

3. Wait for the result line and compare it with the game:

   ```text
   P2J MOVE outcome=PASS reason=PASS ... transition=Scroll
   ... end_map=<target> end_cell=<visible cell>
   end_provenance=ServerPlaced end_trusted=true
   ```

   **PASS** requires the target map, a server-placed or server-confirmed cell, and a trusted
   position. A shown walk or an estimated cell is not confirmation.
4. Any other outcome is a STOP:

   | Outcome | Meaning | Action |
   | --- | --- | --- |
   | `BLOCKED` | No map-change request went | Stop. Do not retry. |
   | `FAILED` | A request went but the target/placement was not confirmed | Stop; the character may have moved. Do not retry. |
   | `INTERRUPTED` | Player, fight, disconnect, host stop or cancellation | Stop. Do not retry. |
   | `ALREADY_CONSUMED` | The one authorization was used | Stop. |

5. Do not type a second `move`, even after a `BLOCKED` or `FAILED` result. The process retains the
   used authorization; a new attempt requires restarting the host and a new owner GO.
6. Stop manually, close the client, stop the host, and verify the Ankama configuration is restored,
   no redirect remains, no listener remains, and the private version-file backup still matches.

### P2J result criteria

| Result | Criterion |
| --- | --- |
| GO 1 PASS | Server-placed map/cell matches the visible client; `guard=None`; `translation_blocked=false`; plan is a plain `Scroll` to a direct neighbour. |
| GO 1 BLOCKED | Latch closed (including first-placement `true`), position untrusted, guard refusal, missing plan, identity or preflight failure. **No GO 2.** |
| GO 2 PASS | Exactly one `p2j move`; `outcome=PASS`; target map and server placement/trust confirmed; no second transition or retry. |
| GO 2 BLOCKED/FAILED/INTERRUPTED | Any other result or mismatch. STOP; preserve sanitized evidence; no improvised recovery. |

## P2F visual validation procedure

**Not run during P2F.** The supervised visual validation was subsequently executed and passed in
P2G; see [P2G_REPORT.md](P2G_REPORT.md). P2E's GO did not authorize that later run. This procedure
is retained for reproducibility. A new owner GO is required before any further client session,
including the future one-neighbour automated move, which P2G did not authorize or execute.

1. **Preflight.** Close Dofus and confirm no updater, host, or relay is active. Recheck the installed
   client version, build date, platform, Unity/metadata identity, and binary fingerprint against
   [BUILD_IDENTITY](BUILD_IDENTITY.md). Verify that the pack, schema, mapping, and pins match that
   build and pass their audit. Make a fresh private byte-for-byte backup of the current version file.
   Confirm the client is not redirected, ports 5555 and 5556 are free, captures are disabled,
   Fights:Alone=false, and no bot profile, activity, or schedule can act. Stop on any identity
   mismatch or unexpected state.

2. **Publish and install the P2F apphost.** Do not reuse the P2D apphost. From a clean source tree at
   the P2F implementation merge commit
   e873b71e09af2beaf54ea60406870df01648a5b9, publish into scratch with the pinned SDK:

       git worktree add --detach /tmp/p2f-apphost/source e873b71e09af2beaf54ea60406870df01648a5b9
       cd /tmp/p2f-apphost/source
       /home/nill/.dotnet/dotnet publish src/D3B.Host/D3B.Host.csproj --configuration Release --runtime linux-x64 --self-contained true -p:UseAppHost=true --artifacts-path /tmp/p2f-apphost/build --output /tmp/p2f-apphost/publish --verbosity minimal

   Preserve the installed P2D apphost in a private backup, then install the complete P2F publish
   directory as one set; do not mix individual files from the two builds. Set the private directory,
   launcher, and payload permissions as in [RUNTIME_ARTIFACTS](RUNTIME_ARTIFACTS.md#publish-the-apphost).
   Before launch, record the P2F informational version and SHA-256 values for the launcher,
   D3B.Host.dll, D3B.Network.dll, and D3B.Bot.dll. Compare the installed files and full manifest with
   the fresh publish output. The P2D identity is not a substitute for this P2F identity. Leave the
   apphost without a capability.

3. **Complete the GO-gated host preflight.** Obtain the owner's new GO before starting the session.
   Use the verified P2F apphost as the normal user. Keep captures off and enable game-session and
   position transition logging:

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

   Start only after the owner GO and every preflight check passes.

4. **Observe manually.** The supervisor launches Dofus and selects the character manually. Compare
   the visible map and cell with the host's transition records. These are the exact sanitized P2F
   message templates to recognize:

   - Placed: “Bot <character>: placed on map <map> at cell <cell>”
   - Walk confirmed: “Bot <character>: walk confirmed on map <map> at cell <cell>”
   - Walk settled, estimated: “Bot <character>: walk settled on map <map> at cell <cell>, estimated while the server may still hold it”
   - Position unknown: “Bot <character>: position unknown on map <map> until it is placed”
   - Detach, last known: “Bot <character>: game connection <connection> closed; last known map <map> cell <cell>”

   A shown path is not confirmation. Treat the estimated-settlement line as estimated and wait for
   server confirmation before calling that cell confirmed. A position-unknown line blocks cell
   validation until placement. The detach line records the last known snapshot; it does not prove
   the position remained current after detachment. Do not enable a bot activity or inject movement.

5. **Stop and restore.** The supervisor closes the client. Stop the host, verify the original
   configuration is restored, and check that no redirect, host listener, or test process remains.
   Keep detailed local logs private; record only build identity, outcomes, and non-sensitive
   aggregate counts.

| Result | PASS/BLOCKED criterion |
| --- | --- |
| Confirmed position | **PASS** when the server-observed map and a server-placed or server-confirmed cell match the visible client. |
| Estimated or client-reported cell only | **BLOCKED for cell validation.** Record its quality accurately; it is not server confirmation. |
| Unknown position, decode error, identity failure, or disagreement | **BLOCKED.** Stop and preserve only sanitized diagnostics. |

## P2D — current build 3.7.13.17

The P2C supervised run on `3.7.12.12` worked at the TCP, connection-protocol and game-connection layers; the launcher then updated the client to `3.7.13.17`. Its evidence is preserved in [P2C_REPORT](P2C_REPORT.md); the P2D details and deployment record are in [P2D_REPORT](P2D_REPORT.md). The values in sections A–E below are the historical P2C `3.7.12.12` run; use this section for the current build.

### Identity

| File | Size | SHA-256 |
| --- | ---: | --- |
| `Dofus.x64` | 4,472 B | `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff` |
| `GameAssembly.so` | 175,835,648 B | `f2aa597d4ca5550d4188d878f6dd577d79fbfc01615bc48f073a1c4d8d9c76a0` |
| `UnityPlayer.so` | 41,372,064 B | `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f` |
| `global-metadata.dat` | 43,581,760 B | `dea17c3c465a1937737e848a290057db9812bc2e7f4f8176d8ae1bf2616cdc9d` |

Version `3.7.13.17`, build date `2026-11-03T14:11:23+00:00`, Linux x86_64, Unity `6000.3.16f1`, IL2CPP metadata `39`. The `Dofus.x64` and `UnityPlayer.so` files are unchanged from `3.7.12.12`; `GameAssembly.so` and `global-metadata.dat` changed. The `version` file SHA-256 at the P2D audit is `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`; re-record it if the launcher rewrites it.

### Runtime artifacts

| File | Size | SHA-256 |
| --- | ---: | --- |
| `data-8.pack` | 26,890,912 B | `28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0` |
| `schema-1.binpb` | 170,287 B | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0` |
| `mapping-15.binpb` | 14,688 B | `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e` |

They are staged at `/home/nill/.local/share/D3B/data/3.7.13.17`, directories `0700`, files `0600`, owner `nill`. `data/3.7.12.12` remains intact and must not be used for `3.7.13.17`. The pack is 17,639 entries, 17,623 maps and 56,317 French texts; the graph has 11,331 vertices and 31,337 transitions; the atlas has 15,470 positions, 569 sub-areas, 70 areas and 63 zaaps. The new zaap 81 is on map 243140612, a real client map with an atlas position and sub-area but no graph vertex; the pathfinder ignores it, and the extraction reports it as a note. The protocol schema is byte-identical to `3.7.12.12`; the mapping pairs are identical. `ContractAudit` has **0 failures, 2 notes and 100 partial messages** across 222 messages, 492 fields, 29 enums and 206 values; no field or value is renumbered; `ProtocolTranslation.Create` succeeds. The pins are `src/D3B.Protocol/Pins/3.7.13.17.json`, derived from the `3.7.12.12` pins only after proving the schema and readable-digest identity.

### Host command and supervised logging

Use the P2D apphost after the gate below. The only change from the P2C settings is the targeted game-session logging:

```sh
/home/nill/.local/share/D3B/apphost/D3B.Host \
  --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
  --Data:Directory /home/nill/.local/share/D3B/data \
  --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
  --Network:Captures:Enabled false \
  --Fights:Alone false \
  --Network:Address 127.0.13.59 \
  --Network:Ports:0 5555 \
  --Network:Game:Ports:0 5556 \
  --Logging:LogLevel:D3B.Bot.Network.GameSession Debug
```

- `D3B.Bot.Network.ConnectionPhaseSession` stays at Information: local connection, message names, direction and length for the connection phase.
- `D3B.Bot.Network.GameSession` at Debug: every decoded game message's name, client/server direction, length and state (`understood`, `read`, `not read`).
- `GameLog.NotUnderstood` at Warning: a game message the readable protocol lacks or whose translation does not hold, with its message name, direction, length and problem. It never logs the payload.
- `D3B.Bot.Runtime.BotActor` at Information: `Attached` (character state created), `ChangingMap`, `ChangedMap` and `Walking` (map/cell updates). These lines contain local connection/character identifiers; keep the log private and report only message names, directions and counts.
- Captures stay disabled. No ticket, payload, account identifier or private message is logged or shared.

Offline validation: `LoggingTests.TheSupervisedArgumentEnablesTheGameSessionAtDebug` and `GameLogTests` pass; the full suite is green.

### Apphost installed

The live apphost `/home/nill/.local/share/D3B/apphost` is the verified P2D payload published from commit `c9c0d5e0660ecbd373380f1f57f2015b6f459125`: launcher 78,192 B `de956557…`, `D3B.Host.dll` 5,632 B `c6589b05…`, `D3B.Network.dll` 112,640 B `279e2cf3…`, `D3B.Bot.dll` 650,240 B `a71bdb88…`, informational version `0.0.0-alpha.0.12+c9c0d5e…`, no capability. The directory is `0700`, the launcher `0700`, the payload `0600`, owner `nill`; a 369-file manifest verifies the set. The P2B publication is backed up at `~/.local/share/D3B/private-backups/apphost-p2b-20261103T162503Z` (`D3B.Host.dll` `0cc2…`, `D3B.Network.dll` `0ce5…`, `D3B.Bot.dll` `65e4…`), with a verified 369-file manifest. No host or client was started during the P2D preparation mission. The subsequent supervised P2E result is recorded in [P2E_REPORT](P2E_REPORT.md).

### Restore rules for 3.7.13.17

The P2E pre-test backup is `~/.local/share/D3B/private-backups/p2e-20261103T162601Z/version.original` (113 B, SHA-256 `796d68f1…`), byte-identical to the current `version`. **Do not apply the P2C `3.7.12.12` backup** (`28f3f4…`) to the updated client. If the launcher rewrites the version file during the session, the P2D host logs `The client's build changed from … to …`, stops instead of relaying a protocol it was not prepared for, and leaves the launcher's new Ankama configuration in place; the normal stop then preserves it. Do not relaunch, and do not reuse the `3.7.13.17` artifacts if the new build's version or build date changes again: repeat the P2D identity and regeneration procedure first.

This runbook is a sequential procedure for one supervised connection-phase observation. Keep the client closed until section C. A human supervisor must remain present at the client and host. Stop at the first unexpected prompt, error, reconnect, or activity. Do not run as root, configure a bot activity, or enable captures.

### Writable CLI and XDG directories

Before running the `dotnet run` commands below, use writable scratch directories for CLI state, runtime files, and NuGet packages. Keep this shell open so the exported variables apply to each command:

```sh
export TMPDIR=/tmp/p2b-docs/tmp
export XDG_DATA_HOME=/tmp/p2b-docs/xdg-data
export XDG_RUNTIME_DIR=/tmp/p2b-docs/xdg-runtime
export DOTNET_CLI_HOME=/tmp/p2b-docs/dotnet-cli
export NUGET_PACKAGES=/tmp/p2b-docs/nuget-packages
mkdir -p "$TMPDIR" "$XDG_DATA_HOME" "$XDG_RUNTIME_DIR" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"
chmod 700 "$XDG_RUNTIME_DIR"
```

## A. Prerequisites

1. **Confirm the separate go and close the client.** Obtain the owner's separate authorization for this supervised attempt. In the Ankama/Zaap UI, confirm that the connection port is **5555**, the default, and leave it unchanged. Close Dofus and confirm no client update is in progress. Do not start an activity or bot profile; keep `Fights:Alone=false`.

2. **Check the exact installed client identity read-only.** Run from the repository root. Keep the `version` output private because it contains the original `ConfigUrl`.

   ```sh
   CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
   VERSION_FILE="$CLIENT/Dofus_Data/StreamingAssets/version"
   cat "$VERSION_FILE"
   /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/inspect --file scripts/inspect.cs -- summary --client "$CLIENT"
   uname -s
   uname -m
   stat -c '%s %n' "$CLIENT/Dofus.x64" "$CLIENT/GameAssembly.so" "$CLIENT/UnityPlayer.so" "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
   sha256sum "$CLIENT/Dofus.x64" "$CLIENT/GameAssembly.so" "$CLIENT/UnityPlayer.so" "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
   od -An -tu4 -j4 -N4 "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
   sha256sum "$VERSION_FILE"
   ```

   Expected identity: version `3.7.12.12`, build date `2026-10-19T14:46:54+00:00`, Linux x64, Unity `6000.3.16f1`, metadata v39. Expected sizes and SHA-256 values:

   | File | Size | SHA-256 |
   | --- | ---: | --- |
   | `Dofus.x64` | 4,472 B | `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff` |
   | `GameAssembly.so` | 175,931,264 B | `0af08ea7dcc7fd15907be5761ff8b0eab62ddb020fc38ad588c06bc7f2e34da8` |
   | `UnityPlayer.so` | 41,372,064 B | `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f` |
   | `global-metadata.dat` | 43,605,684 B | `fc135ad151e13adf297cc07b50c37680de4a367b8b757fdf958b4344c09d6c66` |

   The metadata command must report `39`. Stop if any identity field, size, or hash differs; do not rely on the version string alone.

3. **Verify the staged pack, schema, and mapping.** The installed files are under `/home/nill/.local/share/D3B/data/3.7.12.12`. Verify their hashes and private permissions:

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

   Both directories must be mode `700`, each file mode `600`, owner `nill`. The offline acceptance record is `GamePack`/`PackSource` client `3.7.12.12`, format 8, 17,738 entries and 56,227 `texts/fr` entries. `ProtocolSource.Load` and `ProtocolTranslation.Create` succeeded; `ContractAudit` had 0 failures, 2 notes and 100 partial matches across 222 messages, 492 fields, 29 enums and 206 values. `d3b.game.AuthenticationRequest` maps to `knc` with `Pin` evidence. This is offline evidence only.

   To repeat the mapping audit without touching the staged mapping, copy the pack and schema into scratch and write the regenerated mapping there:

   ```sh
   AUDIT_ROOT=/tmp/p2b-docs/audit-data
   mkdir -p "$AUDIT_ROOT/3.7.12.12"
   install -m 0600 "$DATA/data-8.pack" "$AUDIT_ROOT/3.7.12.12/data-8.pack"
   install -m 0600 "$DATA/schema-1.binpb" "$AUDIT_ROOT/3.7.12.12/schema-1.binpb"
   /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/mapping-audit --file scripts/mapping.cs -- --client-version 3.7.12.12 --data "$AUDIT_ROOT" --pins src/D3B.Protocol/Pins --without-captures --json
   ```

   Require exit 0, zero audit failures, 222 message pairs and 29 enum pairs. A regenerated mapping hash can differ with the tool version; compare pairs and audit counts as described in [runtime artifacts](RUNTIME_ARTIFACTS.md).

4. **Verify the dedicated apphost payload and capability state.** The apphost directory must be owned by `nill`, mode `700`. The launcher must be 78,192 bytes, x86-64 ELF PIE, with SHA-256 `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`. Its informational version must be `0.0.0-alpha.0.10+0b84ba47f60631f6443f5b4301f1c3751f617beb`. `D3B.Host.dll` must be 5,632 bytes with SHA-256 `0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`; `D3B.Network.dll` must be 108,544 bytes with SHA-256 `0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`. Require the launcher and both payload hashes to match; the stable launcher hash alone does not prove the managed code identity. No capability must be present on the launcher.

   ```sh
   APPHOST_DIR=/home/nill/.local/share/D3B/apphost
   APPHOST="$APPHOST_DIR/D3B.Host"
   stat -c '%a %U %n' "$APPHOST_DIR"
   stat -c '%s %a %U %n' "$APPHOST"
   file "$APPHOST"
   sha256sum "$APPHOST" "$APPHOST_DIR/D3B.Host.dll" "$APPHOST_DIR/D3B.Network.dll"
   getcap "$APPHOST"
   ```

   `getcap` must show no capability. Confirm the informational version against the expected value above. If the launcher or either payload identity differs, stop and review it before use.

5. **Record and verify a private, byte-for-byte version-file backup.** Do not copy the original URL into the shared report. The reference version-file SHA-256 at this audit is `28f3f473c932b36bd3a1d9f71cd4dc5327165a72dcf8fed410222dc73fc3d6d7`; re-record it if the current file differs.

   ```sh
   umask 077
   BACKUP_ROOT="$HOME/.local/share/D3B/private-backups"
   BACKUP="$BACKUP_ROOT/p2b-$(date -u +%Y%m%dT%H%M%SZ)"
   mkdir -p "$BACKUP"
   chmod 700 "$BACKUP_ROOT" "$BACKUP"
   cp -- "$VERSION_FILE" "$BACKUP/version.original"
   chmod 600 "$BACKUP/version.original"
   cmp --silent "$VERSION_FILE" "$BACKUP/version.original"
   sha256sum "$VERSION_FILE" "$BACKUP/version.original"
   ```

   `cmp` must succeed and the two hashes must match. Keep the backup private until restoration is verified. The redirect/restore tests use fake temporary installations: `ClientVersionFileTests` and `ClientRedirectTests` in `tests/D3B.Network.Tests/Clients/`. The targeted network suite passed 127/127 on the P2B branch based on integrated master `9f1836d`. The CLI was also checked against a fake version file missing `ConfigUrl`; restore printed `has no ConfigUrl line: repair the client from the launcher...` and exited 2. None of these checks changed or exercised the installed client.

6. **Check the current redirect pointer and ports.** The client must still be closed. Use the configured redirect directory and inspect all TCP listeners before the host starts:

   ```sh
   REDIRECT=/home/nill/.local/share/D3B/client
   /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-check --file scripts/client.cs -- check --client "$CLIENT" --redirect-directory "$REDIRECT"
   ss -ltnp
   ```

   If `check` reports that the client is already redirected to D3B, run `restore` and check again before proceeding. Do not start the host while the client is redirected.

   The client must not be redirected to D3B before this test, and no D3B listener may occupy `127.0.13.59:5555` or `127.0.13.59:5556`. Confirm that 5555 and 5556 are free. Other listeners on 443 must be recorded and checked by address; the existing Tailscale 443 listeners are on `100.119.62.63%tailscale0` and its IPv6 address, not `127.0.13.59`.

7. **Confirm safe host settings.** The first-test settings are `Client:Directory=/home/nill/.config/Ankama/Dofus-dofus3`, `Data:Directory=/home/nill/.local/share/D3B/data`, `Network:Redirect:Directory=/home/nill/.local/share/D3B/client`, `Network:Captures:Enabled=false`, `Fights:Alone=false`, `Network:Address=127.0.13.59`, `Network:Ports:0=5555`, and `Network:Game:Ports:0=5556`. Confirm no bot profile, activity, or schedule is configured to act. Keep captures disabled for the entire test.

## B. Host start

Proceed only after every A gate passes and the separate owner go is recorded. Run the dedicated apphost as the normal user in a terminal that remains visible to the supervisor:

```sh
/home/nill/.local/share/D3B/apphost/D3B.Host \
  --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
  --Data:Directory /home/nill/.local/share/D3B/data \
  --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
  --Network:Captures:Enabled false \
  --Fights:Alone false \
  --Network:Address 127.0.13.59 \
  --Network:Ports:0 5555 \
  --Network:Game:Ports:0 5556
```

Expected informational startup lines, with paths filled by the host, include:

```text
Opened the pack of client 3.7.12.12, format 8: .../data-8.pack
Loaded the mapping of client 3.7.12.12, format 15: 222 messages, 492 fields, 29 enums and 206 values audited, .../mapping-15.binpb
Relaying the connection host on 127.0.13.59:5555
Relaying the game servers on 127.0.13.59:5556
Clients reach D3B: their configuration sends ... to 127.0.13.59, from ...
```

Check the actual listeners in another terminal:

```sh
ss -ltnp
```

Expected D3B listeners are exactly `127.0.13.59:5555` and `127.0.13.59:5556`; there must be no D3B listener on 443. The client configuration still lists `5555,443`, so this expected nonfatal warning may appear:

```text
The client's configuration lists ports 5555, 443, and D3B listens on 5555: clients choosing another go nowhere
```

The Zaap UI confirmation that it will use 5555 is therefore a required gate. Stop if the pack or mapping log is absent, an audit failure occurs, a listener differs, a different warning/error occurs, or the redirect pointer cannot be read. Confirm restore remains possible by checking that the private backup remains readable and by running:

```sh
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-check --file scripts/client.cs -- check --client /home/nill/.config/Ankama/Dofus-dofus3 --redirect-directory /home/nill/.local/share/D3B/client
test -s /home/nill/.local/share/D3B/client/config-url
```

The first command must identify the current redirect state; the second confirms that the recovery sidecar exists without printing its private URL. The pre-redirect version-file backup is the independent recovery copy.

### 443 fallback (not applied)

The fallback keeps default connection ports `[5555,443]` and requires the dedicated apphost to bind 443. It is **BLOCKED until explicit owner authorization for this capability**. No capability is currently applied. Never apply it to shared `dotnet` and never run the host as root. Before any authorized use, inspect `ss -ltnp`; existing Tailscale listeners are on a different interface/address, but verify that `127.0.13.59:443` is free.

If authorized, apply and verify the capability as a separate step:

```sh
sudo setcap 'cap_net_bind_service=+ep' /home/nill/.local/share/D3B/apphost/D3B.Host
getcap /home/nill/.local/share/D3B/apphost/D3B.Host
```

Expected capability output is `/home/nill/.local/share/D3B/apphost/D3B.Host cap_net_bind_service=ep`. Then run this command as `nill`, leaving the apphost's default connection ports `[5555,443]` in effect:

```sh
/home/nill/.local/share/D3B/apphost/D3B.Host \
  --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
  --Data:Directory /home/nill/.local/share/D3B/data \
  --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
  --Network:Captures:Enabled false \
  --Fights:Alone false \
  --Network:Address 127.0.13.59 \
  --Network:Game:Ports:0 5556
```

Verify `getcap` and all listeners with `ss -ltnp`. To roll back the capability, run this separately:

```sh
sudo setcap -r /home/nill/.local/share/D3B/apphost/D3B.Host
getcap /home/nill/.local/share/D3B/apphost/D3B.Host
```

Republishing the apphost can remove the capability; reapply and verify it only under the same authorization. This fallback has not been applied or tested.

## C. Manual game launch

Only after B passes and the supervisor confirms the expected listeners and logs, launch Dofus through the Ankama/Zaap UI. Do not use a shell launch command. Confirm the UI still selects connection port 5555 and has not been changed. Keep `Network:Captures:Enabled=false`, `Fights:Alone=false`, and all bot activities unconfigured.

Observe only whether the client connects through the local relay and how far the connection phase proceeds. Watch the client and host continuously. Do not start combat, movement, harvesting, or any other character action. There must be no reconnect loop and no automatic action. Stop at the first unexpected prompt, error, reconnect, or activity by closing the client and following D. Record the observation as **MANUAL/SUPERVISED**; it is not broader compatibility evidence.

## D. Stop and restore

Close the client through its UI, then stop the host with Ctrl+C. Check the client pointer and restore only if it still names D3B:

```sh
CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
VERSION_FILE="$CLIENT/Dofus_Data/StreamingAssets/version"
REDIRECT=/home/nill/.local/share/D3B/client
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-check --file scripts/client.cs -- check --client "$CLIENT" --redirect-directory "$REDIRECT"
```

If `check` reports the client still redirected to D3B, restore it and check again:

```sh
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-restore --file scripts/client.cs -- restore --client "$CLIENT" --redirect-directory "$REDIRECT"
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-check --file scripts/client.cs -- check --client "$CLIENT" --redirect-directory "$REDIRECT"
```

Verify the restored file hash equals the hash recorded in A. At this audit the expected value is `28f3f473c932b36bd3a1d9f71cd4dc5327165a72dcf8fed410222dc73fc3d6d7`; if A recorded another value, compare to that value instead.

```sh
sha256sum "$VERSION_FILE"
ss -ltnp
```

Verify no D3B listener remains on `127.0.13.59:5555` or `127.0.13.59:5556`. Write a sanitized report with the client identity, supervisor, times, host command/settings, expected and observed logs, listener state, mapping audit counts, pre/post version-file hashes, restore result, and stop reason. Do not include the original `ConfigUrl`, credentials, tickets, captures, or other secrets.

## E. Failure or interruption

If the host or client exits unexpectedly, keep Dofus closed and do not retry. Inspect the current pointer and listeners, then restore only if the client still points to D3B:

```sh
CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
REDIRECT=/home/nill/.local/share/D3B/client
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-check --file scripts/client.cs -- check --client "$CLIENT" --redirect-directory "$REDIRECT"
ss -ltnp
```

If `check` reports the client still redirected to D3B, run restore and verify again:

```sh
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-restore --file scripts/client.cs -- restore --client "$CLIENT" --redirect-directory "$REDIRECT"
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2b-docs/artifacts/client-check --file scripts/client.cs -- check --client "$CLIENT" --redirect-directory "$REDIRECT"
sha256sum "$CLIENT/Dofus_Data/StreamingAssets/version"
```

If `ConfigUrl` is missing, restore reports `has no ConfigUrl line: repair the client from the launcher, which writes the version file anew.` and exits 2. The atomic staged write and same-directory replacement reduce the chance of a truncated version file; they do not replace the private backup. If `ConfigUrl` is missing or damaged, the redirect sidecar is missing/unreadable, or the restored hash does not match the recorded original, stop and use the private byte-for-byte backup or repair the client through the Ankama launcher. Do not guess at file contents, retry reconnections, or change ports/settings at random. Keep the client closed until restoration is proved and report the interruption without secrets.
