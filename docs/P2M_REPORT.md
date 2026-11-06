# P2M — Final mission record

**P2M COMPLETE — EVIDENCE QUALITY FIXED; MOVEMENT NOT READY (62 TYPES STILL BLOCKED)**

**Base master:** `d1270e9380df4ab40a0bdb6ae0c078744b5043ff`

**Final integration:** `d10c1926baf052486d0a04ff32a8aa8cf7aa0944` (`p2m-integration`)

P2M corrected the resolver's semantic evidence rules, made resolver CLI output private and atomic, and reconciled the retained GO 1 observation offline. The result demonstrates zero message identities. The translation latch remains closed for all 62 observed blocker types. This record is documentation for work already completed at the final integration SHA.

**Boundary:** local work only. No push, live client/host/relay, new capture, latch change, mapping promotion, or movement occurred.

## Verdict

**EVIDENCE QUALITY FIXED; MOVEMENT NOT READY.** The corrected resolver reports 0 `VERIFIED` rows. Structural similarity and reference names do not establish semantic identity. The GO 1 observation remains useful for offline review, but no identity was demonstrated and no movement or mapping promotion is authorized by this result.

## Delivered

| Lot | Result |
|---|---|
| L1 — resolver semantics | `VERIFIED` requires exact shape, global uniqueness in both directions, exact build, valid complete observation, no blocking issue, fixture, and valid matching semantic proof. Candidate JSON protocol v2 exposes the independent evidence facts. `PromotionGate` fails closed with `NO_SEMANTIC_PROOF`. |
| L1b — proof deduplication | Fixed a same-source valid-plus-invalid proof deduplication path that could otherwise fail open. Mixed proof sets remain invalid in either order. |
| L2 — private CLI output | `diagnose --out` uses private atomic output: file mode `0600` at creation, private parent `0700`, safe under umask `000`, atomic replacement, and refusal of unsafe targets. |
| L2b — post-move failure | Removed the potentially failing chmod after replacement, so a reported write failure cannot occur after the destination has already been replaced. |
| L3 — offline reports | Re-resolved the retained complete GO 1 report and produced the human-readable resolution and operational blocker inventories. No capture or live session was created. |

### Resolver evidence rules

The resolver keeps three separate facts: exact structural shape, global structural uniqueness, and demonstrated semantic identity. Global uniqueness checks token-to-readable and readable-to-token uniqueness across all non-map schema messages and readable descriptors, including unobserved tokens. Ambiguous competitors produce `AMBIGUOUS_SCHEMA_TOKEN`.

`SemanticProof` binds token, readable identity, build, schema SHA, proof kind, source, and artifact SHA. Allowed kinds are `IL2CPP`, `BEHAVIORAL`, `AUDITED_PIN`, and `AUDITED_REFERENCE`. Optional proof fields on pins and normalized corpus entries can carry semantic proof. Historical shape-only pins, BillBot/SNS references, and pins without proof never establish identity. Missing or invalid proof yields `SEMANTIC_PROOF_MISSING` or `SEMANTIC_PROOF_INVALID`; a mixed valid/invalid set fails closed regardless of input order. A candidate is `VERIFIED` only when exact shape, global uniqueness, exact build, complete valid observation, no blocking issue, fixture, and matching valid proof all hold.

Candidate JSON protocol v2 reports `exactShape`, `uniqueShape`, `hasValidObservation`, `semanticIdentity`, and proof separately. Detached proofs are ignored. The promotion gate now requires a matching proof and returns `NO_SEMANTIC_PROOF` for structural-only evidence; structural source counts do not substitute for proof.

## Offline resolution and operational disposition

The final resolver inventory contains **0 `VERIFIED`, 4,995 `CANDIDATE`, 241 `CONFLICT`, and 48 `UNKNOWN`** pair rows, **5,284 total**. It demonstrates **0 identities**. Among candidates, 4,965 have `uniqueShape=false`; 30 have `uniqueShape=true` but no proof. All rows have null semantic identity and proof. All eight former GO 1 aliases now resolve as `CANDIDATE` with `uniqueShape=false`.

The operational comparison covers **86 observed types**, of which **24 are accepted** by the existing `NonPositionMessages` policy with matching direction and **62 remain blockers**. `kbi` is listed but unobserved. The first review priorities are `mem`, `kne`, `kpx`, `icj`, `jxh`, `jpe`, and `kod`, followed by initialization/security/connection families, position-relevant families, then frequency and token order. Every proposed identity remains `supposed` or `unknown`; none is proved. The latch remains closed for all 62 blockers.

The full inventory SHA-256 is `a487bac837a2175d51226fdf5ecb832a033d916b5efa9e39fc89f1dd5cf5a2cd`. Supporting records: [resolution summary](p2m/P2M_RESOLUTION_REPORT.md), [operational blockers](p2m/P2M_OPERATIONAL_BLOCKERS.md), [full resolver inventory](p2m/P2M_RESOLUTION_FULL.json), [blocker data](p2m/P2M_BLOCKERS.json), and [inventory README](p2m/README.md).

## Audit trail

The independent audits were read-only reviews. Initial rejections and their corrections are retained here.

1. **A1 initial — REJECT:** same-source valid and invalid proofs could be deduplicated in a fail-open way. L1b changed deduplication to preserve conflicting records and fail closed in both input orders. **A1 delta — APPROVE:** both orders classify as `CANDIDATE` with `SEMANTIC_PROOF_INVALID`; identical proofs deduplicate by record equality; uniqueness, gate behavior, and replay were revalidated. The replay is byte-identical to the full inventory hash above.
2. **A2 initial — REJECT:** chmod after the atomic move could fail after replacing the destination. L2b removed throwing work after the move and retained private replacement and refusal behavior. **A2 delta — APPROVE:** atomic/private writer behavior was verified and no latch or mapping changes were found.

Residual audit findings: bytes behind `artifactSha256` are not authenticated by the proof model; proof source strings are trusted; reflection-based test accessors weaken compile-time coverage; and Windows behavior is untested. The identical-proof dedup case was code-verified rather than covered by a dedicated test.

## Verification evidence

All final integration evidence is for `d10c1926baf052486d0a04ff32a8aa8cf7aa0944`.

| Check | Result |
|---|---|
| Build | Pinned .NET SDK build: 0 warnings / 0 errors. |
| Targeted tests | 137/137 passed after final integration. Diagnostic group: 253/253 before L1b; 246/246 after L1b. |
| Full serialized suite | 1,741 total; 1,741 passed, 0 failed, 0 skipped; exit 0. Run alone twice with the engine and demo. |
| Format | `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore`: exit 0. |
| Corrected replay | 0 `VERIFIED`, 4,995 `CANDIDATE`, 241 `CONFLICT`, 48 `UNKNOWN`; byte-identical to `P2M_RESOLUTION_FULL.json`. All eight former aliases are `CANDIDATE` / `uniqueShape=false`. |
| Isolation and scope | No live process/listener was created; no mapping changed. No capture, latch change, or push. |

RED/GREEN evidence: L1 RED `c429ca9`, GREEN `41db9e5`; L1b RED `ebf5c84`, GREEN `bccad19`; L2 RED `28b3b68`, GREEN `7935c94`; L2b fix `576e339`. Lot merges: L1 `76fa972`, L1b `fa69670`, L2 `43a521e`, L2b `6dacf7f`; L3 `c9c5b2e` plus clarification `75d8923`; final integration `d10c192`.

## Residual risks

- SHA-256 records identify artifact bytes but do not authenticate the bytes behind a semantic proof's `artifactSha256`; proof source strings are trusted.
- Reflection-based test accessors reduce compile-time coverage, and Windows atomic replacement behavior is untested.
- All 62 observed blocker types remain unresolved; no live protocol acceptance or movement readiness is claimed.
- The semantic proof model does not itself certify its source. Independent review and the existing promotion gates remain necessary.

## Next mission proposal

Run a targeted semantic-resolution mission using the installed client's IL2CPP/code and the existing GO 1 observations. Do not create a new capture or infrastructure. Scope work first to the priority tokens (`mem`, `kne`, `kpx`, `icj`, `jxh`, `jpe`, `kod`) and the initialization families. Produce audited `SemanticProof` entries bound to the exact build, schema, and artifact digest; have them reviewed independently; then re-resolve the retained evidence offline. Any mapping candidate must still pass the existing P2K2 promotion gates and receive owner approval before promotion.

Explicit non-goals for that lot: no live host or client, no movement, and no mapping promotion within the lot.

## Final status

**P2M COMPLETE — EVIDENCE QUALITY FIXED; MOVEMENT NOT READY (62 TYPES STILL BLOCKED)**
