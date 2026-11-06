# P2L GO 1 — supervised live evidence record

*2026-11-06. Manual/supervised passive diagnostic run after the installation recorded in [P2L_REPORT.md](P2L_REPORT.md).*

## Verdict

**GO 1 BLOCKED — RESOLVER FALSE VERIFIED (STRUCTURAL ALIASING)**

Pass 2 produced a complete live observation for client `3.7.13.17`. Resolution produced eight `VERIFIED` entries, but distinct schema tokens share readable targets. This violates the contract's exact unique-shape requirement when applied across the token-to-readable mapping. Do not use these results to promote a mapping or authorize movement.

## Preflight and installed artifacts

- The client was `3.7.13.17` (build date `2026-11-03T14:11:23+00:00`). The checked client version file had SHA-256 `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`; it matched the private Ankama backup byte for byte. The client pack was `28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0`, schema `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`, and installed mapping `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e`.
- The installed apphost manifest was `38f067482f397b900c99ce6bfee4f79c921bb4a9f9149c8a3a1c5f59bd2932d0` (372 files). The diagnostic engine was `d3b-diagnostic` 0.1.0, SHA-256 `2ac365be2ac70cd5a3fe5e338179ab58cf1a83fdffedcf283e522b3d5200836b`.
- The pre-install P2J apphost was backed up in two copies; both had 369 files and manifest `452a64c1503f2c20d2fee72873264218a23bba0ef811ce8ebc7be9f26827a7e9`, were byte-identical, and the rollback copy was verified. The fresh pass-2 client configuration backup was `go1-20261106T145707Z/version.original`; its bytes matched the live version file.
- Preflight found no Dofus, D3B host, or diagnostic process; ports 5555/5556 had no listener and no diagnostic socket. Runtime data and apphost permissions were verified as private. No mapping change was made.

## Run record

| Pass | Result | Evidence |
|---|---|---|
| 1 | **Contaminated; excluded from clean evidence.** The owner moved the character after placement; walking was confirmed. The session was stopped and the client configuration restored. | Report `diagnostic-report-20261106T145625195Z-c508f92a.json`, 67,545 bytes, mode `0600`, SHA-256 `81e68280fc98e9ed96fc0e13e3a5b1cc44aa83769ef3b1760d926805a0d26027`. |
| 2 | **Clean passive observation.** Client was launched manually and placed; there was no walk. The client was closed before the host was stopped; the engine exited and configuration was restored. | Clean report `diagnostic-report-20261106T145842529Z-af40abcc.json`, 74,247 bytes, mode `0600`, SHA-256 `6ec3e668629424e086d0e6f3016054190160a88f3cef5e61cf144bfefc491a3d`. |

The clean report identifies client version `3.7.13.17` and schema SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`. It records complete coverage and state `COMPLETED`, with 124/124 frames enqueued/analyzed, 0 drops, 0 unattributed analyses, 0 truncation, 0 contradictions, and 86 types. Engine identity is `d3b-diagnostic` 0.1.0, SNS commit `c780035c9020058a772ac8a2eef09555acd5a173`, provenance source `live`.

## Resolver result and blocker

The pass-2 resolver ran with the build pins in `src/D3B.Protocol/Pins/3.7.13.17.json`. Its output is `candidates-3.7.13.17-go1-pass2-pins.json`, 3,784,034 bytes, SHA-256 `9ac81efb42af0fd0c41efd556553dbf4c1c3c2a4de873a301fb1d657baf3b94f`. Summary:

| Classification | Count |
|---|---:|
| `VERIFIED` | 8 |
| `CANDIDATE` | 4,987 |
| `CONFLICT` | 149 |
| `UNKNOWN` | 48 |

The eight `VERIFIED` assignments are structurally non-injective:

| Schema token(s) | Shared readable target |
|---|---|
| `hgf`, `imf` | `d3b.game.PlacementCells` |
| `kkn`, `klu`, `kmj` | `d3b.game.FightTurnFinishRequest` |
| `kkt`, `klx` | `d3b.game.FightTurnReadyRequest` |
| `kod` | `d3b.connection.LoginCredentials` |

The root cause is in `ProtocolResolver.cs`: `uniqueShape = exactShape && exactReadables.Count == 1` establishes uniqueness per token, but the resolver does not enforce global token-to-readable injectivity. No test covers duplicate VERIFIED targets. The contract in [P2L_CONTRACT.md](P2L_CONTRACT.md) defines `VERIFIED` as an exact, unique build-schema/readable shape with complete valid observation; the observed assignments fail the required uniqueness check across the mapping.

Secondary storage finding: `diagnose resolve --out` initially created the candidate file with mode `0664`; it was manually corrected to `0600`. The reports directory was private (`0700`). Candidate output should be private by construction.

## Closeout

Post-run checks found the client configuration restored, no Dofus/host/engine process, no listener on ports 5555/5556, and no diagnostic socket. No mapping change or promotion occurred. No movement automation ran. This record labels the evidence as manual/supervised; it is not approval for GO 2 or automatic movement. No push was made.

The three retained JSON files and their hashes, sizes, and contamination status are listed in [p2l-go1/README.md](p2l-go1/README.md). No raw host logs, terminal output, database, capture payload, account/character identifier, ticket, or private message is part of this evidence record.
