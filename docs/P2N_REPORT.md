# P2N implementation and evidence report

## Decision

**READY FOR P2N VISUAL GO**

The section 19 criteria are satisfied by the recorded implementation evidence and independent audit outcomes below.
This GO authorizes only future supervised real-client capture and visual calibration. It does not authorize movement,
harvesting, or any other real GUI action; that requires a separate GO.

### Section 19 exit criteria

- **GUI project compiles:** verified by the pinned-SDK solution build with 0 warnings and 0 errors.
- **OpenCV works with the selected runtime:** verified through the X11 capture tests and the synthetic OpenCV
  calibration and template-matching measurements below.
- **X11 capture works on a fake window:** verified by the Xvfb capture tests; all 21 X11 tests passed on fake windows.
- **Calibration, tests, and metrics pass:** the full GUI suite passed 190/190, and the synthetic calibration metrics
  below were produced.
- **Preview cannot emit a Dofus click:** Preview reads local files and does not capture a desktop or invoke input;
  real input is rejected in Preview, and the host and app do not register the executor or a gameplay trigger.
- **Safety guards and integration boundaries remain fail-closed:** verified by the final security/boundary audit;
  action readiness requires verified exact window identity and a valid current profile. Existing relay admission
  protections remain unchanged, and production GUI admission remains outside P2N.
- **BotActor contracts are defined:** the snapshot adapter and intent-correlation contracts are documented in
  [P2N_GUI_CONTRACT.md](P2N_GUI_CONTRACT.md) and [P2N_ARCHITECTURE.md](P2N_ARCHITECTURE.md).
- **Full suite and independent audits pass:** the full suite exited 0 and the final code delta and
  security/boundary/compliance audits were approved after their recorded findings were corrected.
- **No network, root, or Python dependency was added:** verified by the final security/boundary/compliance audit and
  the documented dependency boundary.

## Verified evidence

The parent verified the final integrated tree at `531c337`:

| Check | Result |
| --- | --- |
| Solution build with the pinned .NET SDK | Pass; 0 warnings, 0 errors. |
| Full serialized test suite | Pass; 1,955 total, 1,951 passed, 4 skipped because Rust was not configured, 0 failed; exit 0. |
| D3B.Gui.Tests | Pass; 190/190. |
| D3B.Bot.Tests | Pass; 649/649. |
| X11 backend tests | 21 Xvfb tests passed with no skips: 8 windowing, 8 capture, and 5 XTest input tests against fake windows. |
| Formatting | Pass; `dotnet format --verify-no-changes` exited 0. |
| Preview help smoke | Pass; `dotnet run --file scripts/gui-preview.cs -- --help` exited 0. |

The preview target matcher compares Xid and compares ProcessId and ExecutablePath only when both values are known; it
ignores Title and Status. This permissive rule is limited to target matching. Action readiness requires both compared
identities to be Verified and requires exact Xid, ProcessId, and ExecutablePath equality. An unverified window or
calibration profile blocks the action gate.

The report serializes per-anchor fit residuals, individual validation errors, rejected anchor indexes, and affine
coefficients `a`, `b`, `tx`, `c`, `d`, and `ty`, in addition to aggregate fit and validation metrics.

## Audit corrections in the merged implementation

- **Preview output blocker:** the output guard and writer both reject destinations beneath any ancestor containing the
  `D3B.slnx` repository marker. Detection starts from the destination and does not depend on the current working
  directory. CLI input errors expose a file name and stable diagnostic code without the full path.
- **Action identity:** the gate uses exact, verified Xid/PID/executable identity and rejects unverified profiles. The
  permissive missing-value comparison remains confined to preview target matching.
- **Click uncertainty:** `GuiInputResult.ClickMayHaveBeenEmitted` marks a launched click command. A later failure,
  cancellation, or timeout reported by the input backend becomes `UNCONFIRMED`; the executor does not retry.
  Pre-launch refusals report no click. Cancellation during confirmation after input returned `Sent` is `INTERRUPTED`
  with the click marked emitted, also without retry.
- **Relay contract:** a fake-relay integration test verifies unchanged client/server traffic, refusal of D3B gameplay
  requests, admission of client requests and D3B non-gameplay requests, and zero XTest input. Production admission
  wiring remains P2O work.
- **Latch revision:** `StateRevision` combines the published view revision with translation-latch state, so a latch
  transition changes the revision and invalidates an intention bound to the prior state.
- **Options:** the gate constructor calls `GuiOptionsValidator.ValidateOrThrow`. P2O must also validate options when the
  host starts.
- **Preview evidence:** per-anchor residuals, per-validation errors, rejected-anchor indexes, and all six affine
  coefficients are serialized.
- **Path handling:** output protection is based on the repository marker rather than the working directory, and CLI
  errors redact directory paths.
- **Cleanup:** owned Xvfb test resources are cleaned up on completion or failure.

These corrections are present in the merged implementation.

## Independent audit outcomes

- **Early audit:** initial `REJECT` with 1 BLOCKER, 4 MAJOR, and 3 MINOR findings. The findings were corrected in code
  and verified.
- **Final code audit:** audit of `3c22fe6` returned `REJECT` with 1 BLOCKER, 3 MAJOR, and 5 MINOR/NIT findings. All
  findings were corrected. The subsequent delta audit on `de6745b` returned `APPROVE`, with one MINOR finding about
  post-launch timeout coverage; commit `87f87ef` corrected it.
- **Final security, boundary, and compliance audit:** audit of `531c337` returned `APPROVE`, with no BLOCKER or MAJOR
  findings. It identified one documentation MINOR in this report, corrected by this update.

## Synthetic measurements

All calibration and target fixtures below are labelled SYNTHETIC.

| Fixture | Measurement |
| --- | --- |
| Exact affine fit | Median fit residual 0.000 px; maximum validation residual 0.000 px; 12/12 fit inliers. |
| Two-outlier fit | RANSAC rejected both corrupted fit anchors; 10/12 inliers, or 83.3%. |
| Template target | Best score 1.0000; independent second peak 0.0000; margin 1.0000; visual box {159,141,24,20}; proposed point {171,151}. |
| Offline preview | Report labelled SYNTHETIC. Output directory mode 0700 and files mode 0600. A destination under a D3B repository marker was rejected, with no artifact written there. |

These measurements establish behavior on the listed synthetic inputs. They do not establish calibration accuracy on a
Dofus screenshot or localization accuracy on a real resource.

## Verified and inferred

| Category | Statement |
| --- | --- |
| VERIFIED | The build, formatting, suite counts, GUI and Bot test counts, Xvfb tests, preview smoke, and synthetic metrics listed above. |
| VERIFIED | Preview reads local files; it does not perform live desktop capture or input. The output guard and writer reject paths under a D3B repository marker and apply private Linux modes. |
| VERIFIED | XTest tests use fake Xvfb windows only. The fake-relay test does not start XTest input. |
| INFERRED | The hybrid architecture is the selected target: protocol observations build CharacterState, planning stays in D3B.Bot/D3B.Engine, and a future GUI executor would ask the official client to produce its own gameplay requests. End-to-end wiring is not present. |
| NOT ESTABLISHED | A Dofus capture, real click, movement or harvest, XTest compatibility with Dofus, Unity calibration accuracy, or detection of Dofus resources. |

## Limits and remaining exit evidence

- No Dofus capture or asset was used. No real click, movement, harvest, game connection, or Ankama connection was
  performed.
- A captured image passing pixel checks is not a recognized scene. Resource locations have not been validated against
  relevant Dofus images; template thresholds remain initial values.
- The P2N host and application do not register a GUI executor, input implementation, capture loop, or gameplay trigger.
- `GuiWorldSnapshotAdapter`, `GuiIntentCorrelation`, and `GameInputModePolicy` remain isolated contracts. Production
  relay admission wiring and host-start options validation remain P2O work.
- Identical human and synthesized clicks can produce indistinguishable packet fields. Such ambiguity must stop a future
  one-action flow; packet correlation is not cryptographic attribution.
- X11 focus and global click are not atomic. Window-manager decoration extents outside the selected XID are not
  measured separately.
- No network, root, Linux capability, Python, Frida, or Unity process-injection dependency was added.
- The P2O plan is prospective and does not authorize or perform a real action. READY FOR P2N VISUAL GO authorizes only
  supervised real-client capture and visual calibration; movement, harvesting, and other real actions require a
  separate GO.
