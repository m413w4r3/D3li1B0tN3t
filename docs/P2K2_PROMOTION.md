# P2K-2 mapping promotion — gates and procedure

*2026-11-05. Normative process. Discovery, verification, promotion and deployment are separate steps. No step may be
skipped because a candidate "looks right", and no agent, script or resolver may write a production mapping.*

## 1. Terms

- **Observation**: a frame analysed by the engine. Complete only if no observation was dropped; a partial run is
  labelled as such and cannot support `VERIFIED` alone.
- **Candidate**: a proposed `token -> readable message` mapping with evidence, contradictions and uncertainty.
- **Promotion**: turning a verified candidate into a build-exact mapping artifact and loading it through the existing
  mapping pipeline.
- **Latch**: the per-session P2I/P2K `translation_blocked` state. Promotion never clears it; only a new session with a
  validated mapping starts unlatched.

## 2. Nine steps

| # | Step | Owner | Machine gate |
|---|---|---|---|
| 1 | Candidate discovered | resolver | report written, provenance present |
| 2 | Evidence gathered | resolver | every item has source, build, verified flag |
| 3 | Correspondence verified | resolver | `classification == VERIFIED`, no contradiction |
| 4 | RED/GREEN tests | implementer | failing test first, then green, fixture synthetic or approved |
| 5 | Contract audit | `ContractAudit` | green, no renumbered readable field or enum value |
| 6 | Change review | independent reviewer | read-only audit of diff, tests and evidence |
| 7 | Mapping regenerated | `scripts/mapping.cs` | exact client build and schema SHA-256 |
| 8 | Deployment | owner | explicit owner GO, new exact-commit apphost |
| 9 | New session | owner | new session; latch state re-evaluated normally |

## 3. Hard refusals

`PromotionGate` refuses, with a stable reason code, when:

- `NOT_VERIFIED`: classification is `CANDIDATE`, `CONFLICT` or `UNKNOWN`, or the candidate's own structural flags
  (`ExactShape`, `UniqueShape`, `HasValidObservation`) are not all true, or no evidence item is verified;
- `CONTRADICTION`: a contradiction exists, even if the score is high;
- `BUILD_MISMATCH`: the report's client version or schema SHA-256 differs from the target build;
- `PARTIAL_OBSERVATION`: the only supporting observation run dropped frames or the sidecar failed;
- `NO_FIXTURE`: no reproducible RED/GREEN fixture pair;
- `NO_AUDIT`: `ContractAudit` is missing or failed;
- `RENUMBERED_CONTRACT`: the readable field or enum value numbers would change without a deliberate mapping-format lot;
- `NO_OWNER_APPROVAL`: the explicit approval token is absent;
- `LIVE_SESSION`: promotion is attempted while a live session is identified.

The gate is pure: it returns a decision and reasons. It never edits files, never clears a latch and never authorizes
movement.

## 4. What counts as proof

Accepted:

- the build's own schema descriptor matching every field number, kind, repetition and nested graph;
- a readable descriptor matching the same shape, exactly and unambiguously;
- a reproducible synthetic fixture decoded by the engine with `VALID` status;
- an independent source naming the same message for the same build (our pins, a prior validated observation).

Not accepted as proof:

- a heuristic score, a similarity percentage or a "best match";
- a name from another build's mapping (SNS `Mapping.v2.json`, BillBot `game_mappings_detailed.json`) without a
  build-exact structural comparison;
- a field count alone, a payload length alone, or a plausible chronology alone;
- a resolver report, an agent transcript or a debug log;
- any observation labelled partial.

## 5. `.jpe` / `.kjt` regression case

The pipeline must keep refusing the plausible false match:

- `.jpe` field 1 is `INT32` in `3.7.13.17`; `d3b.game.MapInformationRequest.map_id` is `INT64`: hard contradiction,
  `CONFLICT`.
- `.kjt` field 1 is `INT64` and matches the shape of both `MapInformationRequest` and the reference's
  `ContextReadyRequest`: ambiguous, at most `CANDIDATE`, never `VERIFIED` without build-exact independent evidence.

A test must pin that a high external-reference score cannot upgrade either case.

## 6. Live behaviour

- The sidecar and resolver may run during a live session in read-only mode if diagnostics were explicitly enabled.
- Their output is data for the supervisor; it never feeds `MovementGuard`, `SendGate`, the latch or any send path.
- The latch of a blocked session cannot be cleared by a promotion decision; a new session is required.
- No automatic external AI call with unredacted session data. Reports use the redacted contract JSON only.
