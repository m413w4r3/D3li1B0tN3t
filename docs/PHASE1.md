# Phase 1 report

P1 adds offline capture health diagnostics, message extraction, schema/mapping diagnosis, and deterministic capture replay checks. The offline exit criteria pass. Linux live-client acceptance remains **BLOCKED** by the failed mapping audit, host startup prerequisite, and unresolved client-facing port plan. No live client result is claimed.

## Checkout and scope

This repository is an imported checkout: local `master` is `38db76b` (import) followed by P1 commits. `origin` is `m413w4r3/D3li1B0tN3t`; `origin/master` at `4406f09` was README-only and ahead, and was merged into `master` as merge commit `3302402`. The README conflict was resolved in favor of the remote cleanup (`README.md` is now `doto`). Local `master` is ahead of `origin/master` by the P1 commits and zero behind. There is no upstream remote. The Phase 0 snapshot `8f62ba4` is absent. Phase 0's report also records its original snapshot separately; these checkout facts are documented in [UPSTREAM](UPSTREAM.md#checkout-reality-phase-1).

P1 stayed within the existing script, network, and protocol architecture. It added no second CLI/framework or protobuf decoder, and used no Rust, Frida, or PostgreSQL. No real captures or credentials were committed.

## Exit criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Capture inspection reports file, framing, message, schema, and mapping diagnostics offline | PASS | `capture.cs --json`, DTO contract, and linked-source tests. |
| Message extraction and schema/mapping diagnosis use existing repository types | PASS | Refactored `GameMessages`; diagnosis uses existing `SchemaMatch` and `MappingFile`; linked-source tests. |
| Replay is deterministic under its stated ordering contract | PASS | Five replay determinism tests in `D3B.Network.Tests`. |
| Existing capture formats remain supported | PASS | Format 1 reader test `ACaptureOfFormat1StillReads`; full suite green. |
| No sensitive capture or credential committed | PASS | Synthetic fixtures only under `/tmp`; no real captures or credentials committed. |
| No Rust, Frida, or PostgreSQL added to runtime | PASS | Runtime dependencies remain within the existing stack. |
| Linux acceptance runbook is ready | PASS | `docs/LINUX_ACCEPTANCE.md` is ready; execution remains blocked. |
| No unverified live compatibility claimed | PASS | Live status is BLOCKED / UNMANIFESTED; no live test was performed. |
| Build, full suite, formatting, and six requested script help smokes | PASS | Final isolated checks recorded below. |
| Supervised Linux native-client acceptance | **BLOCKED** | Mapping audit fails; host cannot start without a valid mapping; port plan is unresolved. |

## Implemented capabilities

- Existing `scripts/capture.cs` now accepts `--json`; capture diagnostics and DTOs live in `scripts/capture/CaptureDiagnostics.cs` and companion DTO files.
- Message extraction was refactored from `GameMessages` and can identify unknown message types.
- Schema and mapping diagnosis reuses `SchemaMatch` and `MappingFile`.
- Linked-source tests under `tests/D3B.Scripts.Tests/Captures/` cover diagnostics, message extraction, schema/mapping states, and JSON shape.
- Replay checks pin the byte and frame behavior for ordered records without introducing a network connection.

## JSON and diagnostics contract

The JSON top level has `schemaVersion`, `captures`, `status`, and `exitCode`. Each capture has `capture`, `build`, `summary`, `diagnostics`, and `status`. Each diagnostic has `code`, `severity`, `stage`, `direction`, `recordIndex`, `frameIndex`, `byteOffset`, `message`, and `details`. Severity is `info`, `warning`, or `error`; stage is `capture`, `framing`, `message`, `schema`, or `mapping`. Reports contain no payload values or bytes, absolute paths, or stack traces.

Diagnostic code families:

- `capture.*`: `file_unreadable`, `format_unsupported`, `header_invalid`, `record_invalid`, `record_truncated`, `empty`.
- `framing.*`: `tls`, `frame_incomplete`, `length_invalid`.
- `message.unknown_type`.
- `schema.*`: `not_provided`, `message_unknown`, `payload_mismatch`, `file_invalid`.
- `mapping.*`: `not_provided`, `message_unknown`, `file_invalid`, `format_unsupported`, `build_mismatch`, `schema_mismatch`.

Capture, framing, schema, and mapping errors produce exit 1. Warnings and informational diagnostics produce exit 0. No captures produces exit 2.

## Replay contract

The same ordered records produce the same per-direction bytes and parsed frame sequence. Replay does not guarantee timing or cross-pipe interleaving, and it opens no network connection. Injected records are optional and separate.

## CLI demonstrations

The following command forms were run against synthetic fixtures in `/tmp/p1demo/fixtures`; these fixtures are not committed:

```sh
/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/valid.d3bcap
# Excerpt:
# valid.d3bcap: client 3.7.12.12, listener port 443, server port 5556, 5 records over 00:00:00.001
#   order: client 3, server 3, D3B 3, client closed, server closed
#   from the client: 1 reads, 3 bytes, not TLS; 1 varint frames of 2 to 2 bytes, 0 bytes left over, 1 parse as protobuf, 1 of 1 reads end on a frame
#   from the server: 1 reads, 3 bytes, not TLS; 1 varint frames of 2 to 2 bytes, 0 bytes left over, 1 parse as protobuf, 1 of 1 reads end on a frame

/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/valid.d3bcap --json
# exit 0; valid capture JSON report

/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/truncated.d3bcap --json
# exit 0; capture.record_truncated warning

/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/unsupported.d3bcap --json
# exit 1; capture.format_unsupported error
```

Stderr was empty in all three JSON demonstrations. JSON adds message/schema/mapping sections. The linked tests cover valid and invalid schema/mapping cases; an invalid mapping test/demo produced `mapping.file_invalid`, exit 1. This is offline synthetic evidence, not live compatibility evidence.

## Verification and test counts

Final isolated checks, using the pinned SDK:

| Check | Result |
| --- | --- |
| `/home/nill/.dotnet/dotnet build D3B.slnx` | PASS, 0 warnings, 0 errors. |
| `/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1` | PASS, 1,231 total, 1,231 passed, 0 failed, 0 skipped, exit 0. |
| `/home/nill/.dotnet/dotnet format --verify-no-changes` | PASS, exit 0. |
| Help smoke: `inspect`, `extract`, `schema`, `mapping`, `release`, `client` | All six exited 0. |

The total is 1,202 Phase 0 tests + 16 capture/replay tests + 13 message/schema/mapping tests = 1,231. `D3B.Scripts.Tests` has 35 tests: 24 capture diagnostics/contract tests, 9 `ClientInstall`, and 2 `CallRegisters`. `D3B.Network.Tests` has 117 tests: 112 prior + 5 replay determinism tests.

## Linux gate and maintenance boundaries

The Linux redirect mechanism is ready and verified offline; simulated relay is PASS. The mapping audit is FAIL: `d3b.game.AuthenticationRequest has no counterpart`; 221 messages and 29 enums paired, with 1 failure, 2 notes, and 100 partial. No mapping was written. `D3B.Host` composes `DefaultModules` → `ProtocolModule` → `NetworkModule`; `BotHost` stops before hosted services without a valid mapping. Host start and live client are therefore BLOCKED. See [Linux acceptance](LINUX_ACCEPTANCE.md) and [build identity](BUILD_IDENTITY.md); there is no live acceptance claim.

Default port 443 is privileged. High ports require a matching `connectionHosts` rewrite, which is not available. The alternative is narrowly scoped `cap_net_bind_service` on a dedicated apphost; never set it on shared `dotnet` or run as root. Current identity, audit, and port blockers prevent a live attempt.

| Work type | Boundary |
| --- | --- |
| Automated | Deterministic fixtures, linked-source diagnostics tests, network replay tests, build/format checks, and CLI help/demo checks. These establish offline behavior only. |
| Observation | Installed-client identity, redirect/restore, port/listener observations, and any real-client session are MANUAL/SUPERVISED and must follow `LINUX_ACCEPTANCE.md`. No such live session is claimed here. |
| P2 | Diagnose the unmatched `AuthenticationRequest` read-only against its readable descriptor, build pins, and Linux schema. Do not change protocol definitions or pins before the cause is known. |

The next smallest TDD slice is a read-only P2 mapping diagnosis for `AuthenticationRequest`: first add a deterministic test that exposes the observed mismatch and its source, then implement the diagnosis without changing protocol definitions or pins. The investigation should distinguish descriptor, pin, schema, and matcher causes before any mapping or protocol change.
