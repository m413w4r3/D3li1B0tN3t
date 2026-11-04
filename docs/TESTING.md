# Testing

This page records the Phase 0 checks and the boundary between automated tests and work that needs an installed Dofus client. Test results below are the recorded Phase 0 results; they are not a promise that every machine will produce the same result.

## Environment and reproduction

The repository pins .NET SDK `10.0.403` in `global.json`. On the Phase 0 Linux host, `/usr/bin/dotnet` was `10.0.114` and could not run this repository. The working SDK was `/home/nill/.dotnet/dotnet`.

The Phase 0 sandbox could make caches under `$HOME` read-only. These exports redirect the CLI and package caches to writable temporary storage. This setup recipe is **FUTURE** (the individual environment values were verified; this exact combined setup command was not recorded as run):

```sh
export DOTNET_ROOT=/home/nill/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME=/tmp/d3b-dotnet-home
export NUGET_PACKAGES=/tmp/d3b-nuget-packages
export XDG_DATA_HOME=/tmp/d3b-xdg-data
export XDG_RUNTIME_DIR=/tmp/d3b-xdg-runtime
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$XDG_DATA_HOME" "$XDG_RUNTIME_DIR"
```

The first restore needs NuGet network access. Use the pinned SDK executable if `dotnet` resolves to a different SDK:

```sh
/home/nill/.dotnet/dotnet --version
```

The expected version is `10.0.403`. The repository uses Microsoft.Testing.Platform with xUnit v3; test projects are executable projects, and their common settings are in `tests/Directory.Build.props`.

## Test levels

- **Unit tests** exercise individual types and modules with in-memory data, fakes, and temporary files. They check local behavior and contracts without launching a game or requiring installed client data.
- **Characterization tests** pin important current behavior at boundaries. P0.6 added four tests for relay split writes, upstream refusal, varint wire order, and redirect restoration after a later hosted-service start fails. They protect observed behavior during subsequent changes.
- **Linked-script tests** compile script source into `D3B.Scripts.Tests` to check shared script logic without launching each file-based app. P0.3 added this project; P1.1/P1.3 adds capture health, message extraction, schema/mapping diagnosis, and JSON contract coverage. Script smoke commands separately check that the file-based entry points start and display help.
- **Offline integration tests** combine real repository components using local fixtures or fakes, such as protocol translation, capture replay, pack readers, storage, and host composition. The full automated suite is intended to run without starting the game. These checks prove integration against the supplied fixture/test data, not compatibility with every installed client build.
- **Manual/supervised client checks** use an installed official client and write generated artifacts to `/tmp`. Extraction and schema generation completed in Phase 0. These checks are not CI tests, and CI must never launch a game.

The Avalonia tests use Avalonia Headless. They do not require a display server.

## Automated commands and recorded oracles

Commands in this section were run in Phase 0 unless labeled **FUTURE**. Run the full suite alone: concurrent builds or test processes can exceed the host's 128-instance inotify limit and cause unrelated `HostRunnerTests` failures.

| Check | Command | Recorded result |
| --- | --- | --- |
| Solution build | `dotnet build D3B.slnx` | Passed: 20 projects, 0 warnings, 0 errors. |
| Full test suite (Phase 1 final isolated run) | `/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1` | PASS, 1,231 total, 1,231 passed, 0 failed, 0 skipped, exit 0. Count: 1,202 Phase 0 + 16 capture/replay + 13 message/schema/mapping tests. |
| Formatting | `dotnet format --verify-no-changes` | Passed after the final code changes. |
| Script help smoke | Commands below | All six exited 0 after the final code changes. |

Earlier full-suite runs: base SHA run 1 had 1,187 total / 1,186 passed / 1 failed (Inspector ordering race); run 2 had 1,187 passed. Post-P0.6 had 1,191 total / 1,190 passed / same failure. P0.8 fixed the race with a test-only observer.

These targeted filters are verified. Both P0.8 parent runs passed 5/5 after the deterministic Inspector order fix:

```sh
dotnet test tests/D3B.App.Tests/D3B.App.Tests.csproj --no-build --filter 'FullyQualifiedName~TheSendersAndTheNameSoughtFilterTheFrames'
dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --filter 'FullyQualifiedName~ABotsFramesAreRecordedFromItsConnectionsFirstFrameBothSidesAndD3BsOwn'
```

Other recorded targeted results: after P0.6, `D3B.Network.Tests` was 112/112; after P0.8, the deterministic Inspector order test was 20/20 and its parent test project was 10/10.

File-based script smoke checks (each exited 0):

```sh
dotnet run --artifacts-path /tmp/d3b-p03-artifacts/inspect --file scripts/inspect.cs -- --help
dotnet run --artifacts-path /tmp/d3b-p03-artifacts/extract --file scripts/extract.cs -- --help
dotnet run --artifacts-path /tmp/d3b-p03-artifacts/schema --file scripts/schema.cs -- --help
dotnet run --artifacts-path /tmp/d3b-p03-artifacts/mapping --file scripts/mapping.cs -- --help
dotnet run --artifacts-path /tmp/d3b-p03-artifacts/release --file scripts/release.cs -- --help
dotnet run --artifacts-path /tmp/d3b-p03-artifacts/client --file scripts/client.cs -- --help
```

All six Phase 0 smoke commands exited 0. The parent independently reran `inspect.cs` and `schema.cs` successfully. Keep artifact paths under `/tmp`; do not put generated output in the repository.

## Phase 1 results

The final isolated P1 checks used the pinned SDK. The Phase 1 full-suite count is 1,231: 1,202 Phase 0 tests + 16 capture/replay tests + 13 message/schema/mapping tests.

| Check | Command | Recorded result |
| --- | --- | --- |
| Solution build | `/home/nill/.dotnet/dotnet build D3B.slnx` | PASS, 0 warnings, 0 errors. |
| Full test suite | `/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1` | PASS, 1,231 total, 1,231 passed, 0 failed, 0 skipped, exit 0. |
| Formatting | `/home/nill/.dotnet/dotnet format --verify-no-changes` | PASS, exit 0. |
| Script help smoke | `inspect`, `extract`, `schema`, `mapping`, `release`, and `client` `--help` commands listed above | All six exited 0. |

P1 linked-script coverage in `tests/D3B.Scripts.Tests/Captures/` is 35 tests: 24 capture diagnostics and JSON contract, 9 `ClientInstall`, and 2 `CallRegisters`. `D3B.Network.Tests` has 117 tests, including 5 replay determinism tests (112 prior + 5). The 24 linked capture tests cover message extraction and schema/mapping diagnosis, including valid and invalid mappings.

The capture CLI keeps text output available and adds JSON diagnostics. These command forms were exercised with synthetic fixtures under `/tmp/p1demo/fixtures`; they are not committed:

```sh
/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/valid.d3bcap  # text output, unchanged
/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/valid.d3bcap --json  # exit 0; valid capture report
/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/truncated.d3bcap --json  # exit 0; capture.record_truncated warning
/home/nill/.dotnet/dotnet run --file scripts/capture.cs -- /tmp/p1demo/fixtures/unsupported.d3bcap --json  # exit 1; capture.format_unsupported error
```

Stderr was empty in all three JSON demonstrations. JSON adds message/schema/mapping sections. The invalid-mapping result `mapping.file_invalid`, exit 1, is linked-test/demo evidence only, not live compatibility. These are synthetic offline results, not real-client acceptance.

## Phase 2B restore-safety results

The following counts were recorded on the P2B branch based on integrated master `9f1836d`:

| Check | Recorded result |
| --- | --- |
| Targeted `D3B.Network.Tests` | PASS, 127/127. |
| Full suite | PASS, 1,253/1,253. |
| Build | PASS, 0 warnings, 0 errors. |
| Formatting | PASS, `/home/nill/.dotnet/dotnet format --verify-no-changes --no-restore` exited 0. |
| Fake-install CLI restore guard | A version file without `ConfigUrl` printed `has no ConfigUrl line: repair the client from the launcher...` and exited 2. |

These checks cover offline restore safety. No installed client, host, relay, or sniffer was run. Re-record the build, suite, and format results after the final merge.

## P2D client 3.7.13.17 migration and update guard

- Targeted suites: `D3B.Network.Tests` 133/133, `D3B.Bot.Tests` 439/439, `D3B.Engine.Tests` 257/257.
- Full solution serialized with `dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build`: 1,265/1,265.
- `dotnet build D3B.slnx`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: exit 0.
- New tests:
  - `ClientVersionFileTests`: build parse, missing version, malformed build date.
  - `ClientRedirectTests`: a changed build stops the host and keeps the launcher's new configuration; the same version with another build date stops it; a changed build at start leaves the version file untouched.
  - `PackSourceTests`: a pack of another build with the same version is refused; a pack of another version on this machine is refused.
  - `WorldPathfinderTests.AZaapOnAMapTheGraphDoesNotHoldIsIgnored`.
  - `GameLogTests`: decoded game message fields; unknown/translation warning without the payload.
  - `LoggingTests.TheSupervisedArgumentEnablesTheGameSessionAtDebug`.
- `GameSessionTests.FramesAreLoggedAtTheDebugLevelByWhatTheyAreNeverByWhatTheyHold` now expects the new `GameLog.NotUnderstood` warning; `TestRelay` supplies a fake host lifetime for the new redirect dependency.
- Offline mapping audit: 222 messages, 492 fields, 29 enums, 206 values; 0 failures, 2 notes, 100 partial; no renumbering; `ProtocolTranslation.Create` succeeds.
- Offline runtime check of the installed `3.7.13.17` pack/schema/mapping: `pack_identity_ok=True`, `mapping_identity_ok=True`, `translation_ok=True`, `READY=True`.
- No host, relay to Ankama, or Dofus client was started.

## P2F state, navigation, and observability results

The final integrated P2F verification used the pinned SDK at /home/nill/.dotnet/dotnet, outside the
Codex sandbox:

| Check | Command or filter | Result |
| --- | --- | --- |
| Solution build | /home/nill/.dotnet/dotnet build D3B.slnx | PASS, 0 warnings, 0 errors. |
| Character reliability | FullyQualifiedName~Character | 115 total, 115 passed. |
| World travel | FullyQualifiedName~WorldTravel | 20 total, 20 passed. |
| Position and translation logs | GameSessionTests, GameLogTests, and PositionLoggingTests | 21 total, 21 passed. |
| Integrated D3B.Bot.Tests project | Full project | 466 total, 466 passed. |
| Full solution | /home/nill/.dotnet/dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build | 1,292 total, 1,292 passed, 0 failed, 0 skipped. |
| Formatting | /home/nill/.dotnet/dotnet format --verify-no-changes --no-restore | PASS, exit 0. |

The P2D baseline was 1,265 tests. P2F adds 27: 13 character position reliability tests,
2 simulated navigation tests, and 12 observability tests (8 PositionLoggingTests, 3 GameSessionTests,
and 1 GameLogTests). The observability worktree's earlier 464-case Bot project count preceded the two
navigation tests; the final integrated project count is 466.

The new state cases cover map placement clearing and invalid self placement, delayed movement/cancel/
refusal guards, matching movement starts, confirmation, reconnect state, unknown protobuf fields,
and navigation with no position. The navigation simulations cover a journey reaching the target on
a confirmed cell and a refused walk ending on the server-named cell without a follow-up request.
Observability coverage checks transition logs, deduplicated translation diagnostics, and that private
markers are not logged. These are offline automated results, not installed-client acceptance. P2F
did not launch a game, host, relay, or client.

## Real-client procedures (manual/supervised)

Use an installed Linux client at `/home/nill/.config/Ankama/Dofus-dofus3`. These procedures are not CI commands. They need a NuGet restore and client-file access. Keep build artifacts and results under `/tmp`.

### Extract game data

```sh
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p04b/artifacts --file scripts/extract.cs -- --client /home/nill/.config/Ankama/Dofus-dofus3 --output /tmp/p04b/packs --force
```

This command was run and exited 0 in 61.53 seconds. It extracted 17,722 maps; `data-8.pack` was 26,870,514 bytes with SHA-256 `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175`. Allow at least 5 minutes for this extraction on a similar host; that allowance is an operator recommendation, not a measured timeout. Keep the output under `/tmp` and never commit client data, maps, packs, captures, or generated schemas.

### Generate the protocol schema

```sh
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/p05verify/artifacts --file scripts/schema.cs -- --client /home/nill/.config/Ankama/Dofus-dofus3 --unity-version 6000.3.16f1 --output /tmp/p05verify/schema --proto /tmp/p05verify/proto
```

An earlier Phase 0 schema attempt exited 1 after opening `Unity 6000.3.16f1, IL2CPP metadata version 39`: `The schema could not be read: mew: field 1 is written 0 times.` This is historical evidence; ABI-aware register handling resolved the failure. The final Linux schema generation succeeded: 170,287 B, SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`; connection schema has 37 messages, 9 enums, and 92 fields; game schema has 2,175 messages, 423 enums, and 6,302 fields. Explicit Unity version selection and automatic detection both work. Windows is unverified. The 10-minute allowance is a suggested supervision window, not a recorded timeout.

## P2J supervised one-transition results

The P2J preparation added 32 movement tests and 2 module tests. The focused classes are
`D3B.Bot.Tests.Movement.SupervisedMoveTests` and
`D3B.Bot.Tests.Hosting.SupervisedMoveModuleTests`; they drive the real `TestPlay` bots and
`TestConnection` relay surface and assert the requests actually recorded in `connection.Sent`.

GO 1 was subsequently executed on the real client: observation PASS, movement **BLOCKED** because
the translation latch closed before first placement. The relay sent 0 frames from D3B. The session,
aggregates and proposed TDD correction are recorded in [P2J_GO1_REPORT.md](P2J_GO1_REPORT.md).

| Check | Result |
| --- | --- |
| `/home/nill/.dotnet/dotnet build D3B.slnx` | PASS, 0 warnings, 0 errors |
| `SupervisedMoveTests` | 32/32 |
| `SupervisedMoveModuleTests` | 2/2 |
| Full serialized suite | 1,433/1,433, 0 failed, 0 skipped (baseline 1,398) |
| `dotnet format --verify-no-changes --no-restore` | PASS, exit 0 |
| `git diff --check` | PASS, exit 0 |

The tests cover passive readiness, no connection-triggered action, a queued movement frame withdrawn when the translation latch closes, unknown and held positions, the
translation latch before and after first placement, non-neighbour/door/map-action/haven targets, map-action on the approach path, last-walk-end-only exit,
one-scroll PASS, walk + one map change, unexpected destination, walk refusal, timeout, every
interruption kind, and second requests after success, timeout and a first BLOCKED request. No live
client, host, relay, or movement was run.

## Failure modes and test rules

- **Wrong SDK:** system `dotnet` 10.0.114 failed against the required SDK. Check `dotnet --version` and use `/home/nill/.dotnet/dotnet` for the Phase 0 environment.
- **NuGet unavailable:** a first restore needs network access. An offline restore can fail before any build or test executes; distinguish that from a test failure.
- **inotify exhaustion:** a default parallel `dotnet test` run under a tight inotify budget produced 1,197 passed and 5 environment failures across ClientRedirect tests, `HostRunnerTests`, and `StoragePreparation` (`host limit 128 on inotify instances`). Run the full suite with `--max-parallel-test-modules 1` to avoid this limit; the final Phase 0 isolated run passed all 1,202 tests, and the Phase 1 isolated run passed all 1,231.
- **No display:** Avalonia Headless tests need no display server.
- Do not weaken or delete tests to make a run pass. Fix the cause or report the failure with its exact output.
- Do not add secrets, account data, captures, client files, extracted data, schemas, or packs to git.
- Keep manual/supervised client checks separate from automated CI. CI must not launch the game.
