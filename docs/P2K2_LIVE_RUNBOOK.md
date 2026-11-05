# P2K-2 future supervised live runbook

**Status: NOT EXECUTED in this mission.** No Dofus client, D3B host, relay, Ankama connection, movement, harvest, or
live observation was run. This document describes a future supervised scenario only.

## Configuration

Diagnostics are disabled unless explicitly enabled. Set these keys in the D3B host configuration for a supervised
run; the listed values are the implemented defaults when a key is omitted:

| Key | Default | Use |
|---|---:|---|
| `Diagnostic:Enabled` | `false` | Enables observer and sidecar registration. |
| `Diagnostic:EnginePath` | `""` (empty) | Path to the `d3b-diagnostic` executable. This is not a usable path: when diagnostics are enabled, set it to the locally built engine or diagnostic setup degrades. |
| `Diagnostic:SocketDirectory` | `Environment.SpecialFolder.LocalApplicationData/D3B/diagnostic` | Directory for the private Unix socket; an explicit value overrides this default. |
| `Diagnostic:QueueCapacity` | `256` | Maximum queued frames; excess observations are dropped and counted. |
| `Diagnostic:IncludeReadable` | `false` | Include already-readable frames; normally only unknown carried types are observed. |

Enabling diagnostics creates a one-way, read-only observation path. It cannot delay, alter, or drop relayed bytes, and
its output does not feed translation, the P2K latch, movement guards, send gates, or any send path. The queue is bounded;
overflow is counted and makes coverage partial. A sidecar failure disables diagnostics for that session and also
marks coverage partial. With diagnostics disabled, D3B starts no sidecar and creates no socket.

Raw payload bytes exist only in memory and in transit to the local engine. Neither engine nor resolver persists them;
reports contain redacted structure such as message tokens, field numbers and wire-shape diagnostics, never payload
bytes, string contents, numeric field values, tickets, or account identifiers. Do not send unredacted session data to
an external AI service. No automatic external AI calls are permitted.

## Supervised procedure

1. **Prepare offline.** Confirm the exact client build, schema fingerprint, engine version, readable protocol, pins,
   and reference provenance. Build the engine locally without network access. Prepare a resolver invocation that reads
   only local schema and reference files and produces a redacted candidate report.
2. **Configure D3B.** Set `Diagnostic:Enabled` to `true`, `Diagnostic:EnginePath` to the reviewed local executable
   (the empty default is invalid for an enabled run; without a path, diagnostic setup degrades),
   and optionally set `Diagnostic:SocketDirectory`, `Diagnostic:QueueCapacity`, and `Diagnostic:IncludeReadable`.
   Keep `Diagnostic:IncludeReadable` false unless the scenario specifically needs understood frames. Verify the
   configuration before starting the host.
3. **Wait for explicit GO.** The supervisor confirms the scope, build, local socket location, and that the engine and
   resolver are local. Do not start Dofus before GO. After GO, the supervisor launches Dofus manually; no automated
   launcher, account action, route, activity, movement, or harvest is part of diagnostics.
4. **Observe unknown types.** Let D3B relay normally. Inspect only redacted diagnostics and queue/coverage status.
   Record unknown carried type tokens and direction. If the sidecar fails or any observations are dropped, label the
   run partial and do not treat its observations as complete evidence.
5. **Resolve locally.** Pass the structured, redacted analysis and exact build schema to the local deterministic
   resolver. Keep payloads out of command arguments, output files, logs, reports, and external services. Preserve
   source/build provenance for each candidate and keep contradictions and ambiguity visible.
6. **Review candidates and contradictions.** A candidate or high score is a hypothesis, never a mapping. In
   particular, retain hard field-shape conflicts and ambiguous matches. Do not edit or promote mappings during the
   live session.
7. **Close and restore.** The supervisor stops the client and host in the normal order, confirms the sidecar has
   exited, and removes any temporary socket/runtime artifacts. Restore the normal disabled diagnostic configuration.
   Retain only approved redacted reports; discard transient raw input and any unapproved session artifacts.
8. **Verify offline before any movement authorization.** Outside the session, prepare a synthetic RED/GREEN fixture
   for each proposed correspondence, run the engine and tests, pass `ContractAudit`, and obtain an independent review.
   Regenerate a build-exact mapping only through the mapping pipeline after all promotion gates and explicit owner GO
   are satisfied. Load it in a new session and validate the mapping and latch state before any movement. A live
   diagnostic result never authorizes movement, and promotion never clears the latch in an existing session.

The discovery path and promotion gates are defined in [P2K2_ARCHITECTURE.md](P2K2_ARCHITECTURE.md),
[P2K2_CONTRACT.md](P2K2_CONTRACT.md), and [P2K2_PROMOTION.md](P2K2_PROMOTION.md). This future runbook does not record
live acceptance: **NOT EXECUTED in this mission**.
