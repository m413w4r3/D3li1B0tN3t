# P2K-2 — SniffSniffSquared integration and agentic Dofus 3 protocol resolution

*2026-11-05. Base: P2K preserved at `c73fae2`; plan and contract at `32a4492`; final code integration
`f9e45cecace4c86224827c2313eb403ca5ffb7d3`, merged locally into `master` at `ff46aac` (no push). No Dofus client,
D3B host, relay, Ankama connection, activity, route, harvest or movement was run. Every capture and fixture in this
report is synthetic; no real session data was read, persisted or transmitted.*

## Verdict

**READY for the diagnostic component.** A real Rust engine, derived from SniffSniffSquared, analyzes D3B's already
delimited frames in replay and over a local Unix-socket sidecar; the C# bridge owns the build schema and translation;
a deterministic resolver produces candidates with evidence, contradictions and uncertainty; a promotion gate refuses
anything unproven.

**BLOCKED for a new passive GO 1 and for any automatic movement.** The 64 P2K blocking types are not resolved, no live
observation exists, no mapping was promoted, `.jpe` is a hard conflict, `.kjt` remains ambiguous, and the P2I/P2K
translation latch is unchanged and still closes on unknown types. The protocol must not be declared ready for
automatic movement.

## A. SniffSniffSquared analysis and reused modules

Frozen at `c780035c9020058a772ac8a2eef09555acd5a173`, MIT, copyright (c) 2026 Miouzora.

Reused, adapted with attribution:

| SNS source | Adapted file | What was kept |
|---|---|---|
| `sniffer/src/pb.rs` | `rust/d3b-diagnostic/src/wire/reader.rs` | handwritten bounded protobuf wire reader; no schema needed. |
| `sniffer/src/schema.rs` | `rust/d3b-diagnostic/src/schema/{model,walker}.rs` | JSON-schema walk approach, now with full protobuf kinds, packed rules, depth/size limits and redacted observations. |
| `sniffer/src/dump.rs` | `rust/d3b-diagnostic/src/analysis/mismatch.rs` | "prefer the wire, flag the schema" mismatch stance and stable codes. |

Not reused: `frame.rs`, `framer.rs`, `flow.rs`, `main.rs` (pcap capture and SNS's own envelope), `messages.rs`,
`interpret.rs`, `dispatch.rs` (HDV/brisage/inventory semantics), the Python tools (PostgreSQL-backed), and the
IL2CPP/reference artifacts (no separate provenance). No pcap, PostgreSQL, dashboard, HDV or brisage code enters D3B.
Attribution lives in each adapted file header, `rust/d3b-diagnostic/NOTICE` and `THIRD-PARTY-NOTICES.md` (D310).

## B. Chosen Rust/C# architecture

```text
client -> D3B relay -> Ankama                 (unchanged bytes, unchanged latch)
              |
              +-> GameSession.OnFrame -> DiagnosticObserver (D3B.Bot)
                        | bounded item+byte queue, drop+count, no await
                        v
                  DiagnosticSession (D3B.Diagnostic) -> optional Unix-socket sidecar
                        | versioned JSON IPC, disabled by default
                        v
                  d3b-diagnostic (rust/d3b-diagnostic): schema-vs-wire analysis, redacted
                        v
                  ProtocolResolver (offline, deterministic) -> candidates/evidence
                        v
                  PromotionGate -> owner GO + mapping pipeline (not run here)
```

The schema remains D3B's `FileDescriptorSet` (`.binpb`); the C# exporter projects it to the contract JSON and the
engine never parses `.binpb`. The build's SHA-256 is the fingerprint. Replay is stdin/stdout JSONL; IPC is a private
Unix domain socket with 4-byte big-endian length framing. Diagnostics are one-way: no diagnostic result feeds
`GameSession`, `MovementGuard`, `SendGate`, the latch or a send path. See
[P2K2_ARCHITECTURE.md](P2K2_ARCHITECTURE.md), [P2K2_CONTRACT.md](P2K2_CONTRACT.md),
[P2K2_PROMOTION.md](P2K2_PROMOTION.md), D308-D310.

## C. Replay prototype, functional

- Engine command: `d3b-diagnostic replay` (JSONL in/out), streaming hello/analysis/summary; a terminal `error` may
  follow earlier output; it never waits for stdin EOF after `end`.
- Exported `3.7.13.17` schema: 2,414 messages, 805,622 bytes, deterministic SHA-256
  `95a39e8c4c567dad3f4d3061af80e9b006c0b1da8fbedd21d3ddc7b5a6cab323`; source `.binpb` SHA-256
  `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`.
- Real-engine checks with that schema: `.lok` (INT64, INT32), `.jpe` (INT32), `.kjt` (INT64), `.mem` (BOOL, ENUM,
  ENUM) and `.kog` (empty) all `VALID`; malformed, unknown-field, wrong-wire, truncation, UTF-8, group, packed and
  depth cases tested.
- C# `ReplayEngineProcess` + real engine: test passes; `D3B_DIAGNOSTIC_ENGINE` gates it. The synthetic corpus CLI
  (`scripts/diagnose.cs replay`) is functional; payload bytes stay in memory/FIFO and are never written.
- Contract hardening: tokens/type URLs bounded, index bounded to `long`, final analysis output capped with a small
  `LIMIT_EXCEEDED` fallback, summaries count analysis-level problems only.

## D. Live-simulated IPC prototype, functional

- `d3b-diagnostic serve --socket <path>`: one client, 4-byte length framing, max 1 MiB per JSON message, handshake
  `hello`+`schema`, per-frame `sequence`, `bye`+summary, socket mode 0600 in a 0700 directory, no TCP, no shell, no
  elevation, no packet injection.
- `DiagnosticObserver` only enqueues exact game type URLs whose carried token is an exported build-schema message.
  Payloads over 1 MiB are rejected by length before any copy. Both queues are bounded by item count and bytes
  (`Diagnostic:QueueCapacity` 256; `Diagnostic:QueueByteCapacity` 16 MiB), drops are counted, and a partial run is
  reported as `coverage: partial`.
- Disabled by default: `Diagnostic:Enabled=false` registers no observer, starts no process and creates no socket.
  A missing engine, crash or timeout disables diagnostics for the session; the relay continues untouched.
- Real sidecar end-to-end test: passes with the Rust binary; the IPC smoke (handshake, analysis, summary, clean close,
  socket removed) was reproduced manually. Timeout, crash, oversized/zero-length frame, sequence mismatch and queue
  overflow are covered with a fake socket server; relay isolation is covered by the observer tests.
- Known residual: payloads travel as hex, so the effective frame ceiling is about 0.5 MiB against the 1 MiB IPC
  message cap; such a frame is a failed/partial observation and cannot affect the relay or promotion.

## E. Resolver and evidence format

`ProtocolResolver` compares the declared build shape with readable descriptors and reference corpora, recursively
through nested messages, and emits a deterministic compact report: token, readable candidate, classification
(`VERIFIED`/`CANDIDATE`/`CONFLICT`/`UNKNOWN`), evidence with source/build/verified/provenance, contradictions and
uncertainty. The heuristic score is ranking only. External references from another build never yield `VERIFIED`.

`VERIFIED` requires a unique exact structural match, no contradiction, build-exact provenance, a `VALID` observation
with a valid type URL (or a validated synthetic fixture), and verified `schema`/`readable` evidence. `PromotionGate`
evaluates the report's candidate itself (not a detached caller copy), requires two distinct verified sources including
`schema` or `readable`, the RED/GREEN fixture, a green `ContractAudit`, no renumbered field, no partial observation,
no live session and an explicit owner token. It is pure: it cannot write a mapping or clear a latch. A fixture payload
is synthetic and never serialized into a report.

## F. P2K type results (offline, structural only)

Real inputs: installed `3.7.13.17` schema, committed pins, SNS `Mapping.v2.json`, BillBot
`game_mappings_detailed.json`. No analysis or observation was supplied, so **no token is `VERIFIED`**.

| Set | Tokens | VERIFIED | CANDIDATE | CONFLICT | UNKNOWN |
|---|---:|---:|---:|---:|---:|
| Priority | 17 | 0 | 7 | 1 | 9 |
| Blocking | 64 | 0 | 19 | 1 | 44 |

- **`.jpe` — CONFLICT, resolver-produced**: BillBot names `MapInformationRequest` for its own build; the build schema
  has INT32 at field 1 while readable `MapInformationRequest` has INT64 at field 1, so
  `FIELD_TYPE_MISMATCH` overrides the heuristic score. Ten other INT32-shaped candidates do not establish identity.
- **`.kjt` — CANDIDATE, ambiguous**: seven structural alternatives, including the committed pin
  `MapInformationRequest`; BillBot names `ContextReadyRequest` in its own build with no matching readable descriptor.
  No observation; the pin is not semantic proof.
- No mapping, pin or `NonPositionMessages` entry was changed. The full table, evidence and hashes are in
  [P2K2_TYPES_REPORT.md](P2K2_TYPES_REPORT.md). Deterministic hashes: priority `241e7949…fccff7`, blockers
  `4670d94d…e311f7`.

## G. Tests, limits and risks

Final verification (parent, serialized, with the real Rust binary):

- `dotnet test D3B.slnx --max-parallel-test-modules 1 --no-build`: **1,614 passed, 0 failed, 0 skipped** with
  `D3B_DIAGNOSTIC_ENGINE` set (the two engine tests were skipped without it in the lot branch).
- `cargo test --offline --locked --all-targets`: **56 passed** (15 IPC, 41 replay); `cargo fmt` and clippy clean.
- `dotnet build D3B.slnx`: 0 warnings/errors; `dotnet format --verify-no-changes` clean; `git diff --check` clean.
- CLI smokes: `schema`, `replay` (synthetic corpus), `types` (priority and blockers) deterministic; a numeric
  type-URL suffix is dropped and never printed.
- The host's inotify limit (128 instances) can fail the unrelated `ClientRedirectTests` when many concurrent
  agent/dotnet processes hold watchers; this was observed once under load and the final run was clean. It is an
  environment limit, not a P2K-2 change.

Residual limits and risks:

- No real capture, live session or real-client validation. Replay/IPC tests use synthetic fixtures and an authorized
  local engine; no external service received session data.
- The resolver is structural offline code; real type identification needs observation-backed fixtures and, for
  `VERIFIED`, build-exact evidence. `.jpe` remains unresolved and blocking; `.kjt` remains ambiguous.
- Reference corpora are other builds; they are marked unverified and cannot alone promote a mapping.
- The sidecar is Linux-only; on a platform without Unix domain sockets it is disabled. Diagnostics remain disabled by
  default on every platform.
- The exported schema is derived from the client and is never committed; only the redacted report and the code are.
- The 64 P2K blockers remain; no new exemption was added. P2H/P2I/P2K latch, `NonPositionMessages`, `MovementGuard`,
  `SendGate` and relay paths are unchanged from `c73fae2` (independent diff audit).

## H. Future supervised live observation

The procedure is in [P2K2_LIVE_RUNBOOK.md](P2K2_LIVE_RUNBOOK.md), marked NOT EXECUTED: prepare offline, enable
`Diagnostic:*`, wait for an explicit GO, launch Dofus manually, observe unknown types, feed the local resolver, review
candidates/contradictions, produce tests/changes outside the session, close and restore, then validate mappings before
any movement. No automatic external AI call with unredacted data; no raw payload persistence.

## I. Status

- **Diagnostic component: READY.** Real Rust replay and local IPC sidecar, real schema acceptance, deterministic
  resolver, promotion gate, redaction and failure isolation verified by tests and two independent audits.
- **Protocol ready for automatic movement: BLOCKED.** 64 P2K types remain, `.jpe` is a conflict, `.kjt` is ambiguous,
  no mapping was promoted and no new GO 1 confirmed `translation_blocked=false`. The latch must stay closed.
- **Delivery**: merged locally into `master` at `ff46aac` on the owner's explicit GO (D314); nothing pushed, published
  or deployed, and no live session was run.

## Independent audits

Two read-only Luna xhigh audits ran on `1c6d4c3`: a security/latch audit and a contract/privacy/resolver audit. Both
initially rejected real defects (pre-copy payload size and byte budget; peer-controlled token serialization; summary
problem counting; empty-analysis acceptance; promotion-gate trust; output cap; `typeUrlOk`). The fixes are in
`f307b0a`, `b6ffc6f`, `c2651e4` and the contract/doc commits `fd40b52`/`566ac55`. The security re-audit at `9542125`
returned **APPROVE** with one residual MINOR (hex-encoded IPC frame ceiling). The contract/privacy re-audit at
`f9e45ce` returned **APPROVE**, closing the CLI token-leak BLOCKER and the promotion-gate MAJOR; its only remaining
NIT was a stale replay-fallback comment, corrected in the final docs commit. The audits were source-inspection only;
the parent ran the final suite, cargo tests, format, CLI smokes and verified the unchanged latch diff.
