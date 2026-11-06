# P2L — Final mission record

**Status: INSTALLED; GO 1 EXECUTED — BLOCKED BY RESOLVER FALSE VERIFIED.**
**Date:** 2026-11-06  
**Base code SHA:** `4854297d6f60cd99dbfc0d1cb8785ea80acb8b0a`  
**Final code SHA:** `8cfa4f03d0a988caef4d35fc8476c7e9cc651cf5`  
**Integrated code branch:** `p2l`

The apphost and engine were installed on 2026-11-06 after the owner gave an installation-only GO. A later, separately authorized supervised GO 1 ran on 2026-11-06. Pass 1 was contaminated by manual character movement; pass 2 was clean and produced a complete live report, but resolution returned structurally aliased `VERIFIED` targets. The final result is blocked. No movement automation, mapping change, or promotion occurred; nothing was pushed. The GO1 evidence record is [P2L_GO1_REPORT.md](P2L_GO1_REPORT.md).

## Verdict

**GO 1 BLOCKED — RESOLVER FALSE VERIFIED (STRUCTURAL ALIASING)**

The supervised GO 1 run produced a complete live observation, but the resolver's eight `VERIFIED` results contain duplicate readable targets and therefore fail the contract's exact unique-shape requirement at the global token-to-readable level. This is not mapping promotion, GO 2, or movement authorization. The translation latch remains unchanged.

## Delivered

| Layer | Delivered |
|---|---|
| L1 | Versioned report contract, bounded aggregator, strict loader, and serialization validation. |
| L2 | Session aggregation and engine identity; opt-in configuration; private atomic report writer; host-stop flush. |
| L3 | Report-backed resolver evidence and `diagnose resolve --report` CLI. |
| L4 | Synthetic frame-to-report-to-resolver chain, integration fixtures, documentation, GO 1 runbook, and deployment checklist. |
| L5 | Fixed initial report-accounting and resolver contradictions: loader accepts stable partial reasons; complete accounting is enforced; hard report contradictions and unattributed/dropped observations remain partial; conflicting build-exact pins do not verify another message; engine and provenance identities are restricted. |
| L5b | Replaced saturating sums with overflow-checked accounting and revalidated identities in the public resolver path using field-specific formats. |
| L5c | Restricted producer and producer-version values and made direct resolver input with incomplete frame accounting partial. |
| L5d | Rejected embedded IPv4-like version substrings and validated direct-input enum/string fields before projection. |
| L5e | Added direct-input serialized-size bounds, a 128-character engine-version bound, boundary-free IPv4-like rejection, and deterministic test startup synchronization. |
| L5f | Made public report serializers reject invalid identities with a generic error before emitting data. |
| L5g | Made the report JSON context and report members internal, closing the metadata bypass; added a regression check for exported report contexts. The separate contract protocol context remains unchanged. |

## Audit trail

The audits were read-only source and behavior reviews. Initial REJECTs and each follow-up finding are retained here.

1. **L4, `d4c30a2` — A2 REJECT.** A `MESSAGE_MISMATCH` report contradiction was dropped by the resolver projection and could still reach `VERIFIED`; complete coverage also accepted `UnattributedAnalyses > 0` without resolver inspection.
2. **L4, `d4c30a2` — A1 REJECT.** The loader rejected `summary-mismatch` and `aggregation-failed`, preventing valid partial reports from loading. Engine/provenance identity fields used a broad character filter that could admit a path or identifier. Privacy checks otherwise confirmed that payloads, tickets, account data, values, strings, and free-form engine details were excluded; storage was private, opt-in, and had no LLM, network service, or capture writer.
3. **L5, `ab54b56` — fixes.** Added the loader reasons, complete-coverage consistency across state, drops, unattributed analyses, truncation and frame accounting; hard contradiction handling; unattributed/dropped-to-partial behavior; conflicting-pin handling; and strict identity filtering. **A2 delta APPROVE** confirmed both original findings closed. **A1 delta REJECT** then found (a) saturating sums could accept `long.MaxValue` accounting, (b) direct `LoadedDiagnosticReport` input could bypass resolver identity validation, and (c) broad identity filtering admitted IPv4-like values. L5b added checked sums and direct-resolver identity validation with field-specific formats. A2's follow-up on the original findings was APPROVE.
4. **L5c, `58e1561` — follow-up fixes.** Producer is restricted to `D3B.Diagnostic` or empty, producer version to the allowed version format or empty, and Dofus `clientVersion` retains its dotted build format. Direct complete reports whose enqueued and analyzed counts differ are forced partial. Reporting 25/25, Resolution 63/63, Observation 30/30, Diagnostic 209/209; full suite 1694/1694. The CLI demo produced 0 verified, 14 candidate, 17 conflict.
5. **L5d, `cc7918a` — A1 and A2 REJECT.** A1 found a word-boundary regex missed `1.2.3-a192.0.2.1b`, and an order-dependent Observation test (111/112). A2 found direct input had no serialized-size check and `engineVersion` was unbounded. L5d had already added direct enum/string validation (undefined problem code 999 fails closed), but these delta findings remained open.
6. **L5e, `ca27f7f` — fixes and re-check.** Added a direct-input serialized-size bound, 128-character engine-version bound, boundary-free IPv4-like rejection, and test-side startup synchronization. Targeted 115/115, Diagnostic 214/214, format exit 0. A1 then REJECTED because public `SerializeToUtf8Bytes`/`Serialize` could emit caller-constructed reports without identity validation; A2 APPROVED the size, length, undefined-code, accounting, pin, `.jpe`, and `.kjt` behavior.
7. **L5f, `0be6019` — fix and serializer delta.** Public serializers now validate client, engine, producer, and SNS identities before emitting, return only `report is invalid`, and do not sanitize caller input. Targeted 120/120, Diagnostic 219/219, build 0 warnings/0 errors, format exit 0. The serializer delta found that public `DiagnosticReportJsonContext`/`Protocol` still allowed metadata bypass: A1 REJECT; A2 APPROVE (writer guard precedes file creation; pipeline and classifications intact; no relay, latch, or mapping change).
8. **L5g, `8cfa4f0` — final context delta.** The report JSON context and its members are internal; a regression test asserts there is no exported reporting `JsonSerializerContext`, while the contract protocol context remains intentionally untouched. A1 privacy/redaction APPROVE on `8cfa4f0`; A2 network/latch/isolation APPROVE on `0be6019` for L5f. L5g was accessibility-only, covered by the A1 delta APPROVE and V1. A1 context delta APPROVE: no public report-metadata bypass, forged identities rejected, pipeline intact; targeted plus integration evidence 124 total / 2 skipped.

The fail-closed rules are: hard report contradictions remain `CONFLICT`; complete-report accounting must agree across all counters and state; unattributed or dropped observations force partial coverage; a conflicting build-exact pin cannot verify an alternative; identities are strict and revalidated at serialization and direct resolver entry points; all counter sums are overflow checked; and direct resolver input is validated and size bounded before projection.

## V1 verification evidence

All final V1 evidence is for code SHA `8cfa4f0`.

| Check | Command | Result |
|---|---|---|
| Rust tests | `cargo test --offline --locked --all-targets` | 56/56 (15 IPC + 41 replay). |
| Rust release build | `cargo build --offline --locked --release` | Exit 0. V1 release SHA-256 `1a3325b253036e32515b61547b993fa20ed8ba785f56a25e2e18462b4b3532e1`. |
| Rust format | `cargo fmt --check` | Exit 0. |
| C# build | `/home/nill/.dotnet/dotnet build` | 0 warnings / 0 errors. |
| Reporting + Observation + Resolution targeted tests | Targeted project checks | 121/121. |
| Bot diagnostic tests | Diagnostic test group | 16/16. |
| IPC tests | IPC test group | 18/18. |
| Integration chain using Rust engine | Integration test group | 3/3. |
| Full serialized test suite | `/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1` | 1705 total; 1705 passed, 0 failed, 0 skipped; exit 0. |
| .NET format verification | `/home/nill/.dotnet/dotnet format --verify-no-changes` | Exit 0. |
| CLI help | `/home/nill/.dotnet/dotnet run --file scripts/diagnose.cs -- --help` | Exit 0. |
| Demo resolver | Synthetic report resolution | 14 `CANDIDATE`, 17 `CONFLICT`, 0 `VERIFIED`; no canaries. |
| Demo report | Synthetic report artifact | 1,422 bytes; file mode `0600` in a directory mode `0700`; no canaries. |

The demo also showed no false `VERIFIED` for near-shape matches, partial reports, mixed or hard problems, `.jpe` (`CONFLICT`), `.kjt` (ambiguous `CANDIDATE`), conflicting-pin (`CANDIDATE`), and `MESSAGE_MISMATCH` (`CONFLICT`). The report coverage goes partial on truncation. The resolver does not promote candidates.

Demo SHA-256 values: report `d19c9e7a…`, schema `d892f5f1…`, pins `aa72efcf…`, candidates `dc6e026b…`. The Rust release artifact staged for deployment has a different hash from the V1 build because the absolute build path differs; both were built from audited source SHA `8cfa4f0`.

## Privacy and storage

Reports contain no payload bytes, login tickets, account or character identifiers, protobuf numeric field values, string contents, private messages, absolute paths, stack traces, or arbitrary engine detail. They retain only bounded aggregate counts, schema-approved names/tokens, safe wire and shape metadata, stable problem codes, fixed contradiction summaries, coverage, and validated build/engine/session provenance. Client build identity is dotted numeric and is validated against the exact supplied schema; other producer, engine, version, and SNS identities use strict field-specific formats. Public serialization and loading fail closed on invalid identities.

Report writing is opt-in and disabled by default. On Unix, the report directory is `0700`, report file `0600`, and a same-directory temporary file is flushed and atomically renamed. Aggregation and disk I/O do not run synchronously on `GameSession.OnFrame`; bounded asynchronous pumps own analysis and shutdown flush. A drop, truncation, sidecar failure, timeout, or write failure must remain visible as partial/failed evidence. Privacy canary tests passed. No LLM call, external network service, or raw capture writer was added.

## Deployment record

The apphost was published from the exact integrated code SHA `8cfa4f03d0a988caef4d35fc8476c7e9cc651cf5` using a clean detached source at `/tmp/p2l-apphost/source`.

| Artifact | SHA-256 / identity |
|---|---|
| Full apphost manifest | 372 files; `38f067482f397b900c99ce6bfee4f79c921bb4a9f9149c8a3a1c5f59bd2932d0`. |
| `D3B.Host` launcher | `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`. |
| `D3B.Host.dll` | `87b3e5c1deda691de242fc4460c45588baeb7c362f9f39418dca7bb1236bbcc0`. |
| `D3B.Network.dll` | `749a3ea8e541e6d5bfc344451653f4f58d5687a3bda0f230965d60b1217c222b`. |
| `D3B.Bot.dll` | `76ff95f6ea82e5e339aef97e49cf16552d8204f95883d5d9eee8665288d1bc23`. |
| `D3B.Diagnostic.dll` | `3fd6b226611a20906fc07b89d85bde094481e9e878ebd0ca1010482fd28344c2`. |
| Informational version | `0.0.0-alpha.0.102+8cfa4f03d0a988caef4d35fc8476c7e9cc651cf5`. |
| Rust engine | `2ac365be2ac70cd5a3fe5e338179ab58cf1a83fdffedcf283e522b3d5200836b`; installed at `/home/nill/.local/share/D3B/diagnostic/d3b-diagnostic`. |

The apphost is installed at `/home/nill/.local/share/D3B/apphost`; directory mode `0700`, payload files mode `0600`, launcher mode `0700`. The installed manifest matches the publish manifest. No capability is set. The Rust engine is installed at `/home/nill/.local/share/D3B/diagnostic/d3b-diagnostic`, SHA-256 `2ac365be2ac70cd5a3fe5e338179ab58cf1a83fdffedcf283e522b3d5200836b`, mode `0700`, with no capability.

The pre-install P2J apphost is preserved at `/home/nill/.local/share/D3B/.apphost-before-p2l-20261106T083635Z` and `/home/nill/.local/share/D3B/private-backups/apphost-before-p2l-20261106T004111Z`. Both copies have 369 files and manifest `452a64c1503f2c20d2fee72873264218a23bba0ef811ce8ebc7be9f26827a7e9`; they are byte-identical to each other and to the pre-install apphost. The rollback copy was verified. The report directory was not created. No Ankama configuration was changed.

## GO 1 preflight — 2026-11-06

Read-only preflight was rechecked before staging. Client version `3.7.13.17`, build date `2026-11-03T14:11:23+00:00`; the private version-file path is `/home/nill/.config/Ankama/Dofus-dofus3/Dofus_Data/StreamingAssets/version`, SHA-256 `796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae`.

| Preflight item | Recorded identity |
|---|---|
| `Dofus.x64` | `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff`. |
| `GameAssembly.so` | `f2aa597d4ca5550d4188d878f6dd577d79fbfc01615bc48f073a1c4d8d9c76a0`. |
| `UnityPlayer.so` | `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f`. |
| Metadata | SHA-256 `dea17c3c465a1937737e848a290057db9812bc2e7f4f8176d8ae1bf2616cdc9d`; metadata version 39. |
| Pack | `28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0`. |
| Schema | `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`. |
| Mapping | `f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e`. |

Data directories were `0700`, files `0600`, owner `nill`; no mapping change was made. Exact-name process check found no Dofus, D3B host, or diagnostic process; ports `5555`/`5556` had no listener and no diagnostic socket existed. The Ankama version backup at `/home/nill/.local/share/D3B/private-backups/go1-20261104T182159Z/version.original` matched the current version file byte-for-byte (`cmp`), with the same SHA-256 `796d68f1…`.

The installed P2L apphost has 372 files and manifest `38f067482f397b900c99ce6bfee4f79c921bb4a9f9149c8a3a1c5f59bd2932d0`; its launcher SHA-256 is `de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0`, `D3B.Diagnostic.dll` SHA-256 is `3fd6b226611a20906fc07b89d85bde094481e9e878ebd0ca1010482fd28344c2`, and informational version is `0.0.0-alpha.0.102+8cfa4f03d0a988caef4d35fc8476c7e9cc651cf5`. Directory, payload, and launcher modes are `0700`, `0600`, and `0700`, respectively; no capability is set. The pre-install P2J copies and rollback verification are recorded above.

At the pre-run check, client `3.7.13.17` and its data hashes remained unchanged from the preflight identities above. The Ankama version backup matched the live version file. Before either pass, no Dofus, host, relay, or diagnostic process was running; ports `5555` and `5556` had no listener, and no diagnostic socket existed. No redirect or configuration change had occurred.

## GO 1 procedure

The supervised run followed the passive configuration in [P2L_LIVE_RUNBOOK.md](P2L_LIVE_RUNBOOK.md): diagnostics and report export enabled, readable inclusion and classic captures disabled, `Fights:Alone=false`, and no activity/profile/schedule or movement automation. Pass 2 was placed manually and had no walk; the client was closed before the host was stopped. The Ankama configuration was restored and post-run checks found no process or listener. The two pass reports and pass-2 resolver output are inventoried in [p2l-go1/README.md](p2l-go1/README.md); interpretation and verdict are in [P2L_GO1_REPORT.md](P2L_GO1_REPORT.md).

The resolver command form is:

```sh
/home/nill/.dotnet/dotnet run --file scripts/diagnose.cs -- resolve \
  --report "$REPORT" \
  --schema "$SCHEMA" \
  --client-version 3.7.13.17 \
  --out "$OUT"
```

Every lost observation, dropped frame, timeout, sidecar/engine error, report-write failure, or partial coverage must appear in the sanitized bilan. Stop on a changed client/build identity, unexpected process/listener/configuration, missing verified backup, automatic activity, unexpected client behavior, or any diagnostic failure; close the client and stop the host without retrying in-session. Keep report and candidate files private. This mission did not execute that procedure.

## Residual risks

- Reports are produced by a local audited producer. SHA-256 provenance identifies artifacts but is not a cryptographic signature or proof against a compromised local producer.
- A process crash or stuck sidecar can prevent the final report from being written; post-crash/stuck-sidecar report loss remains possible.
- Windows atomic replacement behavior is untested.
- Dofus `clientVersion` intentionally uses the dotted numeric build format as an identity exception.
- 64 blocking protocol types remain unresolved; `.jpe` remains `CONFLICT`, and `.kjt` remains an ambiguous `CANDIDATE`.
- Full client binary fingerprinting remains a manual preflight.
- GO 1 produced a live complete observation, but the resolver's duplicate `VERIFIED` targets block the result. No mapping was promoted, and live protocol acceptance is not claimed.

## Next session

Fix resolver-wide token-to-readable injectivity and add regression coverage for duplicate `VERIFIED` targets before relying on this result. Then re-resolve the retained clean evidence, review the remaining candidates and conflicts, and add deterministic fixtures for any proposed protocol changes. Mapping promotion still requires all contract gates and explicit owner approval. Candidates alone never authorize promotion.

## Final verdict

**GO 1 BLOCKED — RESOLVER FALSE VERIFIED (STRUCTURAL ALIASING)**
