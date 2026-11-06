# P2M Lot 3 — offline resolution report

**Result: zero demonstrated identities. The movement latch remains closed for every type not accepted by the existing exact-build policy.** This lot analyzed the completed, sanitized GO 1 report offline. It did not start a client, open a live session, or create a capture.

## Method and protocol

The corrected resolver inventory uses `protocolVersion: 2`, client build `3.7.13.17`, and schema SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`. Its provenance binds it to the complete P2L diagnostic report with SHA-256 `6ec3e668629424e086d0e6f3016054190160a88f3cef5e61cf144bfefc491a3d`. That report records 124 analyzed occurrences, 0 dropped frames, complete coverage, and 86 distinct observed tokens.

The resolver compares build schema and readable protocol shapes, observation aggregates, reference evidence, contradictions, and proof metadata. The counts below are **token/readable pair rows**, not counts of distinct messages. An exact or even unique structural shape is not semantic identity proof. BillBot and SNS names, prior P2K annotations, heuristic scores, and `VALID` schema observations remain hypotheses or structural evidence; they do not promote a pair.

The operational split was derived by comparing the 86 observed tokens and their recorded directions with the read-only `NonPositionMessages` list for exactly `3.7.13.17`. A token is counted as source-accepted only when the listed direction matches every observed direction. There are no opposite-side matches. That comparison yields 24 source-accepted observed types, 62 observed blocker types, and one listed but unobserved type (`kbi`). The 24 entries describe the current source policy; this P2M resolution demonstrates no new identities for them.

## Resolver counts

| Classification | Pair rows |
|---|---:|
| `VERIFIED` | 0 |
| `CANDIDATE` | 4,995 |
| `CONFLICT` | 241 |
| `UNKNOWN` | 48 |
| **Total** | **5,284** |

Of the 4,995 candidate rows, 4,965 have `uniqueShape=false` and 30 have `uniqueShape=true`. All 5,284 pair rows have `semanticIdentity: null` and `proof: null`. The 241 conflicts remain conflicts even where other readable alternatives are candidates. `UNKNOWN` means the resolver returned no named readable candidate for that row; it does not mean the schema token was absent from the observation.

The eight former P2K nested-field aliases are `ifg`, `ipe`, `ira`, `ism`, `kjm`, `kmt`, `kxn`, and `lxb`. They are retained here only as **candidate family hypotheses** with `uniqueShape=false`, never as identities. The corrected resolver’s current token rows classify seven observed members as `UNKNOWN`; `.kjm` is unobserved in this GO 1 and has no resolver row. The current resolver does **not** classify these eight as `CANDIDATE` pairs. This distinction is preserved in [P2M_BLOCKERS.json](P2M_BLOCKERS.json) rather than changing the input classifications.

## Observation quality and noise reduction

The completed report covers all 124 occurrences across 86 token aggregates. Every occurrence is `VALID`; there are no problem codes and no type-URL mismatches. Six types have a `SHAPE_VARIATION` contradiction aggregate, affecting 14 occurrences: blocker tokens `ira`, `ism`, `jgm`, `jpp`, and `kpx`, plus source-accepted token `kna`. Shape variation is a reason for review; it is not a semantic identity proof.

The summary reduces the 124 frame observations to the 86 distinct types that need policy accounting, then separates 24 types accepted by the current source/direction rule from 62 fail-closed blockers. The full 5,627,768-byte resolver inventory is retained unchanged for detailed review; the summary does not discard or overwrite its candidate and conflict records.

## Blocker disposition

All 62 observed types not accepted by `NonPositionMessages` for their observed direction remain blocked. Every unproven type keeps the latch closed. The first review order is `.mem`, `.kne`, `.kpx`, `.icj`, `.jxh`, `.jpe`, `.kod`, followed by initialization/security/connection hypotheses, position-relevant families, then remaining types by occurrence count descending and token. See [P2M_OPERATIONAL_BLOCKERS.md](P2M_OPERATIONAL_BLOCKERS.md) for all 62 types and the separate sections for the 24 source-accepted observations and unobserved `kbi`.

Each family/message label in the blocker inventory is explicitly a hypothesis from the prior P2K annotation or an external BillBot/SNS mapping. The exact-build identity status is `supposed` when a name is hypothesized and `unknown` when no name is available; none is `proved`. Position-effect potential is conservatively recorded as `high`, `possible`, or `undetermined`, never as absent. Per type, the JSON preserves directions, occurrence counts, statuses, problem and contradiction codes, resolver row classifications, hypotheses, priority, potential effects, and evidence still needed.

## Full inventory

The exact corrected resolver output is copied to [P2M_RESOLUTION_FULL.json](P2M_RESOLUTION_FULL.json), 5,627,768 bytes, SHA-256 `a487bac837a2175d51226fdf5ecb832a033d916b5efa9e39fc89f1dd5cf5a2cd`. Its hash matches the supplied input. It remains available on demand; the human-readable report is not a replacement for it.

Regeneration inputs, full paths and hashes, commands, and validation checks are recorded in [README.md](README.md).
