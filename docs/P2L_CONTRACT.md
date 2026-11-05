# P2L diagnostic report contract — version 1

**Status: implemented for offline review; no live report was produced in this mission.** This contract describes the optional redacted report written by the diagnostic session and the deterministic resolver input. It supplements the Rust/C# engine protocol in [P2K2_CONTRACT.md](P2K2_CONTRACT.md); it does not change that protocol or the P2I/P2K latch.

## Purpose and opt-in settings

The report retains bounded structural aggregates across a diagnostic session so an operator can review repeated observations, coverage, and contradictions after shutdown. The resolver produces a separate candidate JSON file with classification, evidence, contradictions, uncertainty, and provenance. These files form a redacted evidence dossier for a later human or agentic investigation.

Report output is opt-in. `Diagnostic:Enabled=true` enables the observer; `Diagnostic:ReportEnabled=true` additionally enables report writing. Both default to `false`. The report writer is created only when diagnostics and report export are enabled and its setup succeeds.

| Setting | Default | Bound and behavior |
| --- | --- | --- |
| `Diagnostic:ReportDirectory` | `Environment.SpecialFolder.LocalApplicationData/D3B/diagnostic/reports` (`~/.local/share/D3B/diagnostic/reports` on the current Linux host) | Explicit path overrides the default. The writer creates or sets the directory to mode `0700` on Unix. |
| `Diagnostic:ReportMaxBytes` | `4194304` (4 MiB) | Accepted range is 65,536 bytes through 67,108,864 bytes. It bounds generated report JSON; the loader uses its configured `MaxBytes` bound (4 MiB by default), while the resolver CLI allows up to 64 MiB. |

The in-memory defaults are 2,048 message types, 256 fields per type, 4,096 field nodes per type, nesting depth 8, 16 variants per type, 32 retained shape values per list, and 64 problem entries per type. Their validation ceilings are 20,000 types, 4,096 fields, 65,536 field nodes, depth 32, 1,024 variants, 4,096 shape values, and 4,096 problems. The runtime configuration exposes the byte limit; the other aggregation limits use their defaults.

## JSON v1 shape

The report is compact UTF-8 JSON with `kind="diagnostic-report"`, `reportVersion=1`, and the current diagnostic `protocolVersion`. Properties are camel-cased, enums are stable strings, unknown properties and duplicate properties are rejected, and nullable properties may be omitted.

| Property | Meaning |
| --- | --- |
| `clientVersion`, `schemaSha256` | Dofus build version and lowercase SHA-256 of the exact source schema bytes. The resolver requires both to match its supplied build schema. `clientVersion` is dotted numeric, for example `3.7.13.17`, and comes from the Dofus build, not the engine. |
| `engine` | Engine protocol version, engine name/version, and SNS source commit returned by the local engine handshake. |
| `provenance` | `source` (`live` or `synthetic`), producer identity/version, and UTC start/completion times. Synthetic tests and demos label this explicitly; they are not live observations. |
| `session` | `state` (`COMPLETED`, `FAILED`, or `CANCELLED`), optional stable `errorCode`, accepted/analyzed/dropped frame totals, unattributed analysis count, `includeReadable`, and queue item/byte capacities. |
| `coverage` | `complete` only when the session completed without drops, unattributed or missing analyses, aggregation/output truncation, or other incomplete accounting; otherwise `partial`. A complete report has no session error, drops, truncation counts/flags/reasons, or incomplete frame accounting. |
| `types` | One sorted entry per safely attributed schema token, described below. |
| `truncation` | Whether JSON output was reduced to fit, the omitted aggregate counts, and stable reasons. |

Each `types[]` entry contains:

- `token` and optional schema-approved `message` name, plus the aggregate `occurrences` count;
- `directions[]` and `statuses[]` counts; statuses are `VALID`, `SCHEMA_MISMATCH`, `MALFORMED`, `TRUNCATED`, `TRAILING_BYTES`, `UNKNOWN_TYPE`, or `LIMIT_EXCEEDED`;
- `typeUrlOkOccurrences` and `typeUrlMismatchOccurrences`;
- `fields[]`, each with field number, optional safe schema name and declared kind, repeated/packed flags, occurrence count, observed wire types, safe observed shape, and optional nested fields and field-problem counts;
- `problems[]`, aggregated by stable problem code and optional numeric field path; codes include `UNKNOWN_TYPE`, `UNKNOWN_FIELD_NUMBER`, `WIRE_TYPE_MISMATCH`, `PACKED_NOT_ALLOWED`, `INVALID_UTF8`, `GROUP_UNSUPPORTED`, `DEPTH_EXCEEDED`, `LENGTH_OVERFLOW`, `VARINT_OVERFLOW`, `TRUNCATED`, `MALFORMED`, `LIMIT_EXCEEDED`, `SCHEMA_MESSAGE_MISSING`, and `SCHEMA_INVALID`;
- `variants[]`, containing a safe structural signature, occurrence count, and status counts;
- `contradictions[]`, containing a stable code, a producer-generated safe summary, and an occurrence count. Codes are `TYPE_URL_MISMATCH`, `MESSAGE_MISMATCH`, `MIXED_STATUS`, `SHAPE_VARIATION`, `FIELD_DECLARATION_MISMATCH`, and `DETAIL_TRUNCATED`;
- `detailTruncated`, which records loss of nested engine detail.

The safe shape object contains only optional `varintBytes`, `fixedLength`, `length`, `lengthCapped`, `utf8`, `fieldCount`, `zeroLength`, and `truncated` properties. Numeric values here are structural sizes or counts, never protobuf field values.

The top-level `truncation` object contains `outputTruncated`, `typesOmitted`, `fieldsOmitted`, `nestedFieldsOmitted`, `variantsOmitted`, `problemsOmitted`, `shapeValuesOmitted`, and `reasons`. Stable reasons include `type-limit`, `field-limit`, `nested-limit`, `variant-limit`, `problem-limit`, `shape-limit`, `unattributed-analysis`, `detail-truncated`, `dropped-frame`, `session-incomplete`, `summary-mismatch`, `aggregation-failed`, and `output-size`. Any omission, incomplete accounting, or output-size reduction forces `coverage="partial"`; the loader does not upgrade partial input.

The aggregator first enforces per-session caps. If serialized JSON still exceeds `ReportMaxBytes`, the serializer deterministically removes variants, nested fields, shape values, fields, and finally types until it fits. It marks the result `outputTruncated=true`, adds the applicable reason and `output-size`, and sets partial coverage. If even the required envelope cannot fit, writing fails and the session records the report-write failure in its local logs.

`DiagnosticReportJson.LoadFile` checks the byte limit before and while reading, rejects duplicate or unknown JSON properties and invalid enums, validates the v1 envelope and complete-coverage accounting, hashes the exact source bytes, and checks aggregate counts with overflow rejection, names, shapes, nesting, and configured bounds. Identity formats are field-specific: `clientVersion` is a dotted numeric Dofus build version matching `^[0-9]+(\.[0-9]+)+$`; it is bound to the supplied build schema and is the acknowledged build identity exception, not an engine-supplied identity. `engine` is empty or exactly `d3b-diagnostic`; `engineVersion` is empty or matches `^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?$`; `producerVersion` is empty or semver matching `^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$`. Neither version may contain an IPv4-like substring (four one-to-three digit numeric components separated by periods). `snsCommit` is empty or 7–64 lowercase hexadecimal characters; `producer` is empty or exactly `D3B.Diagnostic`. The aggregator replaces invalid identity values with empty strings; the loader and resolver reject values outside these formats. The resolver CLI accepts reports up to 64 MiB; runtime-generated reports default to 4 MiB.

## Privacy and storage

Reports never contain payload bytes, tickets, account or character identifiers, protobuf numeric field values, string contents, private messages, absolute paths, stack traces, or free-form engine detail. The aggregator accepts only schema tokens and safe schema field names, aggregate counters, observed wire/shape metadata, stable problem codes, and fixed producer-generated contradiction summaries. Client version, engine metadata, and producer identities use the field-specific formats above. Invalid engine or provenance values are sanitized to empty strings, and the loader and resolver apply the same checks. It discards `DiagnosticProblem.Detail`; arbitrary engine text is not copied to the report. The resolver output is also redacted and does not contain raw frame indices.

On Unix, the writer uses a private `0700` directory and `0600` report file, writes a same-directory temporary file, flushes it, then atomically renames it to a report filename. A failed write cleans up its temporary/final file best-effort. No report is written when the setting is off or writer setup fails.

The relay-facing `GameSession.OnFrame` path only selects eligible frames and enqueues bounded work. It performs no report aggregation or disk I/O. The observer pump and diagnostic session pump do analysis and aggregation asynchronously; only the session pump calls the asynchronous report writer after normal completion or shutdown. The observer supports synchronous and asynchronous disposal so the host's shutdown path can flush the bounded queue. Shutdown and report writes have time bounds; a timeout, sidecar failure, or dropped frame must remain visible as failed/partial coverage and must not be described as complete evidence.

## Resolver semantics and evidence dossier

`diagnose resolve --report ...` verifies the requested client version against the report, exports the supplied schema, and requires exact client-version and schema-SHA identity before resolution. The resolver uses the build schema and readable descriptors for structural comparisons, plus aggregate report facts: occurrence counts, directions, statuses, type-URL matches, safe problem counts, hard report contradictions, coverage, drops, and truncation. `TYPE_URL_MISMATCH`, `MESSAGE_MISMATCH`, and `FIELD_DECLARATION_MISMATCH` remain hard contradictions even when aggregate status and shape otherwise look valid; `DETAIL_TRUNCATED` makes coverage partial. It does not invent per-frame evidence from aggregates or treat report field-shape similarity by itself as proof.

The classifications are:

| Classification | Meaning |
| --- | --- |
| `VERIFIED` | Exact, unique build-schema/readable shape with valid complete aggregate observation and no disqualifying ambiguity or cross-build evidence. |
| `CANDIDATE` | Plausible pairing still missing exact, unique, complete proof, or affected by a soft observation condition. |
| `CONFLICT` | A hard shape or observation contradiction exists. |
| `UNKNOWN` | No readable message can be named. |

Incomplete or truncated coverage, dropped frames, mixed/invalid statuses, schema problems, ambiguous directions, partial readable descriptors, and references from other builds cannot strengthen a candidate into `VERIFIED`. A build-exact pin that names a different readable full name leaves an alternative exact-shape candidate unverified with `CONFLICTING_PRIOR_MAPPING`; matching pins remain corroborating evidence. The `.jpe` regression remains `CONFLICT` for its int32/int64 shape contradiction. `.kjt` remains `CANDIDATE` when multiple readable messages share the same shape, including when a pin names one of them.

The resolver writes candidate JSON only. `PromotionGate` refuses every `CANDIDATE`; no candidate is automatically promoted to a production mapping. A later agentic follow-up can use the redacted report and candidate JSON as a structured dossier: the report hash and observation provenance identify the input, while candidate evidence, contradictions, and uncertainty keep the reasoning auditable. This workflow uses local files and the deterministic resolver; it adds no LLM call or network service.

See [P2L_LIVE_RUNBOOK.md](P2L_LIVE_RUNBOOK.md) for the future passive GO 1 procedure and [P2L_DEPLOYMENT.md](P2L_DEPLOYMENT.md) for deployment gates.
