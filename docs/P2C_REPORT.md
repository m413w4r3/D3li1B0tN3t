# P2C — first supervised Linux client test (client 3.7.12.12)

**Status: PARTIAL.** The relay, the connection protocol and the game connection worked on client
`3.7.12.12`. The launcher updated the client to `3.7.13.17` during the session, so the pre-test build
identity was not restored. The redirect was restored to Ankama's configuration. The `3.7.12.12`
mapping is not reused for the new build; see [P2D_REPORT](P2D_REPORT.md).

This report preserves the P2C evidence. It is not a failure of the relay work, and it is not live
acceptance of the new build.

## Configuration

The host was the P2B dedicated apphost: launcher 78,192 B, SHA-256
`de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`; `D3B.Host.dll` SHA-256
`0cc2a33565e8a2539c625eb9cd81b144e360bdc926e939f2f239edb364792f87`; `D3B.Network.dll` SHA-256
`0ce58ee5db62ff3d295ab7e75984a3993c4fc95e8990907a57759c9ba975d432`. It ran as user `nill`
without root or capability.

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

The private byte-for-byte backup was
`~/.local/share/D3B/private-backups/p2c-20261103T151833Z/version.original`, 113 B, SHA-256
`28f3f473c932b36bd3a1d9f71cd4dc5327165a72dcf8fed410222dc73fc3d6d7`. The host log was kept
privately under `/tmp/p2c-supervision/host.log`. Captures were disabled and no bot profile or activity
was started.

## Results by layer

| Layer | Result | Evidence |
| --- | --- | --- |
| TCP and relay | **PASS** | Local connection accepted and relayed to Ankama's connection host; listeners exactly `127.0.13.59:5555` and `127.0.13.59:5556`; no D3B listener on 443 |
| Connection protocol | **PASS** | `request.identification.token` → `response.identification.success`; `request.server_selection` → `response.server_selection.success`; D3B rewrote the answer to the game listener |
| Game server transition | **PASS** | New connection relayed to the game server on the game route |
| Game protocol | **PARTIAL** | Game connection held for 2 min 29 s: 5,732 B from the client and 128,462 B from the server, 0 frames injected by D3B, no observer or translation error. Game messages are logged at `Debug` and were not visible at the information level, so event-by-event identification was not observed |
| Character state | **NOT MEASURED BY THE HOST** | A bot actor was created and the game session stayed open; the visual character state is the supervisor's observation |
| Restoration | **PARTIAL** | Host stopped with `SIGINT`, exit code 0; ports released; `client.cs check` reports Ankama's configuration and no D3B pointer. The launcher changed the build to `3.7.13.17`, so the initial `3.7.12.12` state was not restored |

## Observed sequence (no identifiers or secrets)

1. The client sent `request.identification.token` (126 B).
2. The server answered `response.identification.success` (272 B).
3. The client sent `request.server_selection` (10 B).
4. The server answered `response.server_selection.success` (83 B).
5. D3B rewrote the answer to send the client to `127.0.13.59:5556`.
6. The connection phase closed after 1.74 s (138 B client, 358 B server, 0 injected frames).
7. A game connection was relayed to Ankama's game server.
8. It closed after 2 min 29 s (5,732 B client, 128,462 B server, 0 injected frames).

No `NotUnderstood`, `ConnectFailed`, `NoUpstream`, `ObserverFailed`, `RedirectFailed`, `fail` or
`crit` event was logged. Two `ClientRedirect` warnings were the expected ports warning:
the client's configuration lists `5555,443`, and D3B listens on `5555`.

## Launcher update during the session

| Field | Before (backup) | After the session |
| --- | --- | --- |
| Version | `3.7.12.12` | **`3.7.13.17`** |
| Build date | `2026-10-19T14:46:54Z` | **`2026-11-03T14:11:23Z`** |
| `GameAssembly.so` | 175,931,264 B, `0af08ea7…` | **175,835,648 B, `f2aa597d…`** |
| `global-metadata.dat` | 43,605,684 B, `fc135ad1…` | **43,581,760 B, `dea17c3c…`** |
| `Dofus.x64` | 4,472 B, `6d7b6348…` | unchanged |
| `UnityPlayer.so` | 41,372,064 B, `b01fc32d…` | unchanged |
| Metadata version / Unity | 39 / `6000.3.16f1` | 39 / `6000.3.16f1` |

The host followed the launcher's rewrite while it ran and reapplied the redirect. On stop it restored
the launcher's new Ankama URL, not the old one. The current `version` file hash is
`796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`; it differs from the P2C backup
because the build changed. **Do not apply the P2C `3.7.12.12` backup to the updated client.**

The old runtime artifacts under `data/3.7.12.12` remain intact (pack `fdd6e182…`, schema
`e383c947…`, mapping `c3ec03ce…`). They are not used for `3.7.13.17`.

## Pre-start residual state

A short host/app run at `16:21:45` local time had left `~/.local/share/D3B/d3b.db` and the redirect
files before the supervised run. The database had 0 accounts, 0 characters, 0 runs and 7 default
activity presets; no activity could start. The P2C host reused and overwrote the stale redirect files.
The client was original before the redirect and was restored on stop. If a launch is not the owner's,
stop and identify it before the next run.

## Tests to add

1. A launcher rewrite with a new version/build date during an active redirect stops the host and keeps
   the launcher's new configuration; no old version file is restored. The P2D tests implement this.
2. A pack with the same version but another build date is refused. The P2D tests implement this.
3. A changed build at host start leaves the version file untouched and fails the start. The P2D tests
   implement this.
4. Game-phase message names and translation problems are logged at a usable level, without payloads.
   The P2D logging configuration and tests implement this.
5. A zaap on a map outside the world graph is reported, not treated as an extraction failure; the
   pathfinder ignores it. The P2D extraction and engine tests implement this.

## Next

P2D regenerated and validated the `3.7.13.17` pack, schema and mapping. The protocol schema is
byte-identical to `3.7.12.12`; the mapping pairs are identical. The P2C result remains a successful
transport/connection observation on the older build, with the game-phase observability gap recorded.
