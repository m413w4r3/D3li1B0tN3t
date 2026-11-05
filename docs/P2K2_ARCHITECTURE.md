# P2K-2 — diagnostic integration architecture

*2026-11-05. Specification for the P2K-2 mission. No live client, relay, Ankama connection, harvest or movement is in
scope. Build-identity: Dofus Linux `3.7.13.17`, base commit `c73fae2` (P2K preserved).*

## 1. Goal

Turn the manual protocol-mapping loop into a passive, testable diagnostic engine:

1. D3B already delimits every game frame and decodes its envelope (`GameSession.OnFrame`).
2. A Rust sidecar, derived from SniffSniffSquared (SNS), analyses the carried payload against the exact build schema.
3. A C# resolver turns analyses plus reference material into ranked candidates with evidence, contradictions and
   uncertainty.
4. A separate promotion gate refuses anything not proven; the P2I/P2K translation latch stays closed until a promoted,
   build-exact mapping is loaded.

The discovery path never authorizes gameplay. A hypothesis is not a mapping, and a candidate is not a movement
authorization.

## 2. Non-goals

- No PostgreSQL, dashboard, HDV, brisage or web component from SNS.
- No pcap dependency: D3B already owns the frames.
- No packet injection by Rust and no second relay.
- No automatic external AI call with session data.
- No raw payload persistence by the engine or the resolver.
- No production mapping written during a live session.

## 3. Safety invariants

These invariants are testable and must not regress:

- **One-way diagnostic flow.** The observer enqueues a copy; it never waits for the sidecar. A full queue drops and
  counts the observation. A dead, slow or wrong sidecar cannot delay, alter or drop a relayed byte.
- **No write path from diagnostic to translation.** The engine and resolver return data; only a promoted mapping
  changes translation, through the existing mapping pipeline.
- **Latch unchanged.** `GameSession` keeps `!IsRead && !IsUnderstood` and the 25-type P2K policy exactly as committed.
  Diagnostic output is not an input to that predicate.
- **Disabled by default.** No process is started and no socket exists unless diagnostics are explicitly enabled.
- **Bounded everything.** Frame size, schema size, nesting depth, client queue, IPC message, and response size are
  bounded; overflow is reported, never silently treated as complete evidence.
- **Redaction.** Reports contain field numbers, declared/observed wire types, lengths, counts and stable problem
  codes. They never contain payload bytes, string content, numeric values, tickets or account identifiers. Payload
  hex exists only in transit between D3B and the engine.
- **No new privilege.** A Unix domain socket in a user runtime directory, mode 0600; no TCP listener; no root.

## 4. Components

```text
Dofus client ──▶ D3B relay ──▶ Ankama server
                    │
                    │  GameSession.OnFrame (unchanged latch)
                    ▼
             DiagnosticObserver (D3B.Bot)
                    │ bounded queue, drop + count, no await
                    ▼
             DiagnosticSidecarClient (D3B.Diagnostic)
                    │ versioned Unix-socket IPC, disabled by default
                    ▼
             d3b-diagnostic (Rust, rust/d3b-diagnostic)
                    │ schema-vs-wire analysis, redacted
                    ▼
             ProtocolResolver (D3B.Diagnostic)
                    │ evidence, contradictions, classification
                    ▼
             CandidateReport (JSON) + PromotionGate
                    │ explicit owner GO + contract audit
                    ▼
             mapping pipeline (scripts/mapping.cs, ContractAudit)
```

Replay mode replaces the observer and sidecar with a one-shot stdin/stdout process; it uses the same in-band schema
and frame messages, so tests exercise the same contract.

### 4.1 Rust crate — `rust/d3b-diagnostic`

Single crate in a workspace, `edition = "2021"` (the pinned local toolchain is Rust 1.83; SNS itself is edition 2024 and
is adapted, not depended on). The only runtime dependency is `serde_json` (already in the local cargo cache); no
`pcap`, `pnet`, `postgres`, `dotenvy` or `prost`.

Modules:

| Module | Origin | Purpose |
|---|---|---|
| `src/wire/reader.rs` | adapted SNS `sniffer/src/pb.rs` (MIT) | bounded protobuf wire reader. |
| `src/schema/model.rs`, `src/schema/walker.rs` | adapted SNS `sniffer/src/schema.rs` (MIT) | D3B schema JSON, full protobuf kinds, packed rules, depth/size limits, redacted observations. |
| `src/analysis/mismatch.rs` | adapted SNS `sniffer/src/dump.rs` mismatch stance (MIT) | prefer the wire, flag the disagreement with stable codes. |
| `src/replay.rs` | new | JSONL replay loop. |
| `src/ipc.rs` | new | length-prefixed JSON over a Unix socket, handshake, bounded frames. |
| `src/main.rs` | new | `replay` and `serve` subcommands. |

SNS's `frame.rs`, `framer.rs`, `flow.rs`, `messages.rs`, `interpret.rs`, `dispatch.rs`, `main.rs` and the Python tools are
**not** reused: they implement pcap capture, SNS's own envelope, HDV/brisage semantics and PostgreSQL persistence. The
reuse is limited to the generic wire reader, the schema-walk approach and the mismatch stance, with attribution. The
mission's "reuse the real Rust modules" is satisfied by adapting those files rather than porting them to C#.

Licensing: SNS is MIT, copyright (c) 2026 Miouzora, commit `c780035c9020058a772ac8a2eef09555acd5a173`. Each adapted file
carries a header naming the source file, commit and licence. `rust/d3b-diagnostic/NOTICE` holds the MIT text and the
repository's `THIRD-PARTY-NOTICES.md` gains the same entry.

### 4.2 C# project — `src/D3B.Diagnostic`

References `D3B.Protocol` and `D3B.Network`; `D3B.Bot` references it. No new NuGet package.

| Namespace | Purpose |
|---|---|
| `D3B.Diagnostic.Schema` | `DiagnosticSchemaExporter`: `ProtocolSchema` (`FileDescriptorSet`) → contract-v1 schema JSON; SHA-256 stays the build fingerprint. |
| `D3B.Diagnostic.Replay` | `ReplayCorpus`: reads a `.d3bcap` or a contract JSONL file into frames; `ReplayEngineClient`: one-shot stdin/stdout process. |
| `D3B.Diagnostic.Ipc` | `SidecarProcess`, `DiagnosticSidecarClient`, length-prefixed JSON framing, handshake, timeouts, sequence gaps, drop accounting. |
| `D3B.Diagnostic.Resolution` | `ProtocolResolver`, evidence/contradiction model, classification rules, `PromotionGate`, fixture proposal. |
| `D3B.Diagnostic.Observation` | `IDiagnosticSink`, bounded queue and drop counters; no dependency on `D3B.Bot`. |

`D3B.Bot/Network/DiagnosticObserver.cs` implements `IGameObserver` and forwards only frames the engine can use:
unknown carried types (the P2K blockers) by default, with an option to include understood ones for replay tests. It
never inspects payload values.

The observer and sidecar are registered by a new `DiagnosticModule` only when `Diagnostic:Enabled` is true. Default
configuration registers nothing and starts no process.

### 4.3 Resolver

The resolver is deterministic offline code, not an LLM call. Its "agentic" cycle is explicit:

**Observe** (engine analysis + schema + observation metadata) → **propose** (structural matches against readable
protocol, prior pins, external references) → **verify** (field-by-field and nested comparison, direction, build) →
**contradict** (hard mismatches, ambiguity, build rotation) → **classify** (`VERIFIED`, `CANDIDATE`, `CONFLICT`,
`UNKNOWN`) → **test** (synthetic fixture proposal and engine re-run).

Provenance is mandatory on every evidence item. A heuristic score may order candidates but is never proof and never
upgrades a classification.

Reference sources are read-only inputs with explicit provenance:

- D3B readable `.proto` descriptors and pins (build-exact, our contract);
- the build's `schema-1.binpb` (build-exact, derived from the client, never committed);
- SNS `reference/Mapping.v2.json` at commit `c780035` (another build: low evidence, rotation warning);
- BillBot `game_mappings_detailed.json` (another build: low evidence, explicit build mismatch);
- prior observations / P2K report evidence (our own, with recorded limits).

A reference from another build can support a `CANDIDATE`; it can never produce `VERIFIED` alone.

### 4.4 Promotion

`PromotionGate` enforces the machine-checkable part of the nine-step process in
[P2K2_PROMOTION.md](P2K2_PROMOTION.md): `VERIFIED`, exact build, no contradiction, engine validation on a reproducible
fixture, contract audit green, RED/GREEN tests present, and an explicit owner approval token. It never edits a mapping
and never clears a latch. Mapping regeneration stays in `scripts/mapping.cs`; deployment remains an owner action.

## 5. Test strategy

- **Rust**: wire reader (truncation, overflow, groups, depth), schema walker (all kinds, packed/unpacked, maps, nested,
  unknown fields, limits), replay (known/unknown/wrong build/wrong direction), IPC (handshake, framing, timeout,
  oversized message, clean close). Tests use synthetic bytes and fixtures only.
- **C#**: schema export (deterministic JSON, map entries, oneofs, packed), replay corpus and process (EOF, exit codes,
  malformed output), IPC client (queue overflow drop accounting, crash, timeout, no relay blocking), resolver
  (`VERIFIED`/`CANDIDATE`/`CONFLICT`/`UNKNOWN` rules, `.jpe`/`.kjt` refusal, build rotation, ambiguity), promotion gate
  (refuses unproven, no latch clearing), privacy (no payload in any report/log).
- **Integration**: an in-process fake engine and a real `d3b-diagnostic` process fed a synthetic replay, including sidecar
  crash and queue overflow, asserting the relay path is unaffected.
- **Baseline**: the serialized full C# suite starts at 1,465 passed; every lot adds tests and keeps the suite green. Rust
  tests run with `cargo test --offline --locked` locally and `cargo test --locked` in CI.

## 6. Delivery sequence

1. **L0b** — P2K audit follow-up: strengthen the tests the independent audit found weak; documentation wording; no
   behaviour change.
2. **L1** — Rust engine: adapted modules, schema walker, replay, tests.
3. **L2** — C# schema export + replay bridge + process client, tests.
4. **L3** — IPC sidecar + bounded observer + module wiring, tests.
5. **L4** — resolver + evidence format + promotion gate, tests.
6. **L5** — offline P2K type analysis report (no promotion), priority `.lok`, `.jpe`/`.kjt`, then the listed families.
7. **L6** — full verification, privacy audit, independent latch/security audit, final report.

Lots with overlapping files are serialized; L1 and L2 may run in parallel because they touch disjoint trees
(`rust/` vs `src/D3B.Diagnostic` + tests) once this contract is committed.
