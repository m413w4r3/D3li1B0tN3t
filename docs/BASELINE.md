# Phase 0 Linux baseline (P0.2)

## Scope and environment

This document records the Linux build and test baseline for the D3B fork at base commit `0a995ad8a99f4417c99f2cf5eafe46cf13eb709b`. The repository is MIT licensed. The verified environment was Ubuntu 24.04, `linux-x64`, kernel `6.8.0-147-generic`, .NET runtime `10.0.14`, and SDK `10.0.403` at `/home/nill/.dotnet`. In this repository, `/usr/bin/dotnet` resolves to SDK `10.0.114` and fails SDK selection: `global.json` pins `10.0.403` with `rollForward: latestMinor`, which does not roll down to an older SDK.

Codex sandboxes may block writes to .NET/NuGet caches under `$HOME` or block NuGet access. That is an execution-environment limitation, not a repository defect. The results below were verified by the parent outside that sandbox. If cache writes are blocked, this optional workaround redirects the common caches to `/tmp`:

```sh
export DOTNET_ROOT=/home/nill/.dotnet
export DOTNET_CLI_HOME=/tmp/p02/dotnet-home
export NUGET_PACKAGES=/tmp/p02/nuget
export XDG_DATA_HOME=/tmp/p02/xdg
export XDG_RUNTIME_DIR=/tmp/p02/runtime
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$XDG_DATA_HOME" "$XDG_RUNTIME_DIR"
```

## Verified commands and results

Run from the repository root with `/home/nill/.dotnet/dotnet`:

| Command | Verified result |
|---|---|
| `dotnet build src/D3B.Host/D3B.Host.csproj -v minimal` | PASS; 0 warnings, 0 errors; 24.94 s. |
| `dotnet build D3B.slnx -v minimal` | PASS; 0 warnings, 0 errors; 22.66 s. After P0.3 the solution contains 20 projects. |
| `dotnet test --nologo -v minimal` at base SHA | Run 1: 1,187 total, 1,186 passed, 1 failed, 0 skipped; 25.1 s. |
| `dotnet test --no-build` at base SHA | Run 2: 1,187 passed. |
| `dotnet test tests/D3B.App.Tests/D3B.App.Tests.csproj --no-build` | PASS; App project alone passed. |
| Post-P0.6 `dotnet test --nologo -v minimal` | 1,191 total, 1,190 passed, 1 failed. |
| `/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1` after all Phase 0 code | PASS; 1,202 total, 1,202 passed, 0 failed, 0 skipped; exit 0. Parent verified. |
| `dotnet format --verify-no-changes` | PASS; exit 0. |

The final count is 1,187 base tests + 4 P0.6 characterization tests + 11 linked-script tests (9 from P0.3 and 2 P0.11 ABI tests). A default parallel full run under a tight inotify budget passed 1,197/1,202; its 5 environment failures affected `ClientRedirect`, `HostRunner`, and `StoragePreparation` tests. The host limit is 128 inotify instances. This is an environment limitation; when that limit is tight, use `--max-parallel-test-modules 1`.

## Failure classification and limits

**FACT.** The identified failure is `D3B.App.Tests.ViewModels.InspectorViewModelTests.TheSendersAndTheNameSoughtFilterTheFrames` ([InspectorViewModelTests.cs:131](../tests/D3B.App.Tests/ViewModels/InspectorViewModelTests.cs#L131)). It expected `MapMovementRequest` at index 1 but received `MapMovementConfirmRequest`. `FrameRecorder` order follows observer-callback lock order, while the test assumed causal order. This is a pre-existing upstream test race, not a fork regression. P0.8 made a test-only fix using a test observer and retained the exact-order assertion; the fix agent reported 20/20 targeted passes and the parent 10/10. No production code changed for this fix.

**ENVIRONMENT NOTE.** Running many full suites in parallel can exhaust the user's 128-instance inotify limit and cause unrelated `HostRunnerTests` failures. Run the final full suite alone.

These results establish build and test behavior only. They do not prove a live client or session. Client detection, extraction, and schema evidence belongs in the Phase 0 documentation, not this baseline.
