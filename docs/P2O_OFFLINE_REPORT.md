# P2O offline integration and audit report

**Date:** 2026-11-09
**Verdict:** **BLOCKED for a supervised live action.** Offline integration is complete and audited for the offline
boundary. No READY claim is made.

## Repository and evidence state

**VERIFIED:** the implementation tree audited by this report is commit
`3ebf635aecdc9ce632bbc898254a6d391035a255` (`p2o/integration`, merge of L3f `012112e`). The documentation writer
worktree is branch `p2o/l8-docs`, based at that exact commit. `origin/master` is unchanged at
`3bf41da5f941f5ce64e22ecea2111c9dc1afd5d1`. The documentation commit is local and is not pushed. The parent
repository's pre-existing modified gitlink was preserved. This report describes the parent implementation evidence;
the exact documentation commit is returned in the delivery record because it contains this report.

**VERIFIED:** no live Dofus launch, input, relay session, calibration, or movement occurred for the P2O integration.
The test counts below are the exact parent results supplied for the merged implementation, not checks rerun by this
documentation-only change.

## Parent verification evidence

| Check | VERIFIED result |
| --- | --- |
| Build | `dotnet build D3B.slnx`: exit 0, 0 warnings, 0 errors. |
| Full serialized C# suite after L3f | `dotnet test --solution D3B.slnx --max-parallel-test-modules 1 --no-build`: 2,303 total, 2,299 passed, 4 skipped, 0 failed, exit 0. |
| Per-project tests after L3f | App 167/167; Bot 899/899; Core 11/11; Data 133/133; Diagnostic 255 total (251 passed, 4 skipped); Engine 257/257; Gui 225/225; Network 157/157; Protocol 106/106; Scripts 66/66; Storage 27/27. |
| Rust offline suite | 41/41 PASS, exit 0. |
| Format | `dotnet format --verify-no-changes`: exit 0. |
| Whitespace validation | `git diff --check`: exit 0 in the parent integration record. |
| Preflight CLI smoke | `/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- --help`: exit 0. |

The build, test, formatting, Rust and preflight rows are **VERIFIED parent results**. They were not rerun during this
documentation task. No code build is required for the documentation-only delta.

## Flake and contention record

**VERIFIED:** one earlier full-suite run after L3e reported 11 failures, all in `D3B.Gui.Tests` (42 seconds versus
about 14 seconds); the runner exited 2. The immediate full rerun and isolated GUI project run were green, with
`D3B.Gui.Tests` at 225/225. A later second full-suite attempt hung in `D3B.Bot.Tests` while L3f worktree tests ran
concurrently and was stopped; it is neither a pass nor a fail. The final full-suite run after the L3f merge ran alone
and passed at 2,303 total / 2,299 passed / 4 skipped / 0 failed, exit 0. The earlier flake and concurrency hang are
recorded honestly; neither is a reproduced final regression.

## Audit verdicts

| Audit | VERIFIED verdict and findings |
| --- | --- |
| A1 — network admission / non-injection | **APPROVE** on merge `19ca4593ec9b09caf4a70483621d063908d9886c` / L1b commit `338f4fcdd3b316e50b23e7866a8ac9f2f6b3a351`. MINOR: no positive host test proves `Gui:Enabled=true` plus `GuiClientInput` reaches a session; synchronization in the after-final-check race test could be stronger. |
| A2 — protocol trust / confirmation | Initial delta on `50da320369d820eeaa3524a3e9eff6564584a1d0`: **REJECT**, with BLOCKER for stale dialog state leaving the click lease open and MAJOR for identical simultaneous human input not being fail-closed. L3f `012112e3f91a4f2492712f726de101a252f987d3` closed the BLOCKER through synchronous client dialog-leave and server dialog/teleport/exchange/interactive-use revocation, actor defense in depth, and regression tests. Delta on `3ebf635aecdc9ce632bbc898254a6d391035a255` / L3f `012112e3f91a4f2492712f726de101a252f987d3`: **APPROVE — offline boundary only**. The BLOCKER is closed; the inherent attribution MAJOR is accepted as a documented residual. NIT: the combined server-dialog-then-client-leave test does not assert actor state between frames; separate tests cover each path. |
| A3 — preflight / host security | **APPROVE** on merge `166bd26f1545cc2ae1826bc40de3ef49ec357101` / L7e commit `b2d17f3939e000a370ae9191c7b1f2b020b33f5c`. MINOR: `renameat2` retains a check-to-syscall window; fail-closed handling and manual inspection are documented. The real bind-mount probe was denied by the environment; synthetic mount-ID tests cover the ancestry boundary. |

**VERIFIED integration history:** L1b merge `19ca4593ec9b09caf4a70483621d063908d9886c`; L7e merge
`166bd26f1545cc2ae1826bc40de3ef49ec357101`; L3e merge `50da320369d820eeaa3524a3e9eff6564584a1d0`; L3f fix
`012112e3f91a4f2492712f726de101a252f987d3`; L3f merge and audited base
`3ebf635aecdc9ce632bbc898254a6d391035a255`.

**OPEN TDD evidence gap:** RED-phase output for L1b and L3b was not captured in the parent evidence record. The later
green tests do not establish a missing RED phase, so no RED claim is made for those slices.

## Non-injection and correlation evidence

**VERIFIED in the parent record:**

- Production `GuiClientInput` remains opt-in; `GuiOptions.Enabled` defaults to false and `InputMode` defaults to
  `ProtocolInjection`. Host validation requires `Gui:Enabled=true` when `GuiClientInput` is selected.
- The production GUI gameplay allowlist is empty. Raw D3B sends are denied in GUI mode. Session admission is checked in
  `GameSession.SendUnguardedAsync`; the relay's final admission check and frame write share `_admissionLock` with mode
  changes.
- Client/server bytes and order remain on the transparent relay path. GUI mode does not rewrite protocol messages.
  The movement translation latch and P2I policy remain unchanged; P2O does not create a latch bypass.
- Tests do not inject D3B gameplay. Correlation tests cover a complete PASS chain and wrong request, refusal,
  out-of-order evidence, timeout, window change, human interruption, one click and no retry.
- The production GUI host capability keeps real input, automatic activity and `SupervisedOneShot` unavailable.

Relevant source anchors at `3ebf635`: `src/D3B.Gui/Safety/GuiOptions.cs:7-16`;
`src/D3B.Gui/Safety/GuiOptionsValidator.cs:20-28`; `src/D3B.Bot/Hosting/GuiHostCapability.cs:29-44`;
`src/D3B.Bot/Network/GameSession.cs:208-235,306-333`; `src/D3B.Network/Relay/InjectedFrames.cs:132-189`;
`src/D3B.Bot/Gui/GuiIntentCorrelation.cs:147-265,340-409`.

## Residual risks and limits

**OPEN — live evidence absent:**

- No live server map or cell was observed. P2N-V map `120063490` and cell `461` are atlas/fingerprint evidence, not
  live server proof.
- No real calibration was measured at action time; no live open-latch observation window was established.
- Zoom and scene detection remain incomplete when the XID and dimensions stay unchanged.
- The coordinator's `DesktopScale` ratio can false-block. This remains a usability/availability risk, not permission to
  weaken the checks.
- Identical simultaneous human and synthesized clicks cannot be attributed perfectly.
- XTest focus observation and the global click are not atomic; window-manager decorations outside the selected XID are
  not measured separately.
- Host/relay verification is in-process and fixture-based; it is not a live-host or real-client result.
- A1 retains the two MINOR findings in the audit table. A3 retains the `renameat2` check-to-syscall residual above.
  A2 retains the identical-input attribution limit and the stated test-quality NIT.
- The GUI test flake and concurrent L3f-suite hang are recorded above. The final isolated post-L3f suite is green.

**VERIFIED decision:** capacity to overcome the translation latch is **NO**. There is no GUI latch bypass. P2O may
observe an open latch with complete build-scoped evidence; a closed latch, an unknown message, or an unproven live latch
means **BLOCKED**.

## Final verdict

**BLOCKED for a supervised live action.** The missing evidence is: a separate explicit observation GO; live,
server-proven map and cell with accepted provenance; a fresh real calibration bound to the action-time window and
scene; an open latch throughout the evidence window with every undecoded frame accounted for by the exact build,
direction allowlist and schema; independent zoom/scene stability evidence; a verified target and active window; and a
separate explicit one-action GO with an operator present. The current host does not expose a supervised one-shot
executor. Offline integration and audit approval do not satisfy those live gates.
