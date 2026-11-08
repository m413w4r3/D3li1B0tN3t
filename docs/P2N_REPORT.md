# P2N implementation and evidence report

## Decision

**BLOCKED — final independent audits remain to be run.**

The final P2N tree builds and passes the recorded automated checks, synthetic calibration and Xvfb evidence below.
Section 19 also requires the independent audits to succeed before declaring READY FOR P2N VISUAL GO. Those final audit
results are not available and are not represented as passes. This decision is based on that missing required evidence.

## Verified evidence

The parent verified the final integrated tree at 3c22fe6, including the window-identity correction at d45e657:

| Check | Result |
| --- | --- |
| Solution build with the pinned .NET SDK | Pass; 0 warnings, 0 errors. |
| Full serialized test suite | Pass; 1,943 total, 1,939 passed, 4 skipped because Rust was not configured, 0 failed; exit 0; 56 seconds. |
| D3B.Gui.Tests | Pass; 179/179 after the identity correction. |
| D3B.Bot.Tests | Pass; 648/648 after the identity correction. |
| Formatting | Pass; dotnet format --verify-no-changes exited 0. |
| X11 backend tests | 21 Xvfb tests passed with no skips: 8 windowing, 8 capture, and 5 XTest input tests against fake windows. |
| Preview help smoke | dotnet run --file scripts/gui-preview.cs -- --help exited 0. |

The final action-gate and target-matching identity rule compares XID every time, compares ProcessId and ExecutablePath
only when both sides know the corresponding value, and ignores Title and Status in equality. Title is never identity
evidence. GuiActionGate separately requires Status=Verified on the current window snapshot before returning Ready.

The early independent audit initially returned REJECT with 1 BLOCKER, 4 MAJOR, and 3 MINOR findings. The
corrections were implemented and verified: revalidate focus immediately before click; bound process-pipe drains;
enforce 2-second, 16-MiB, and 3840×2160 limits; enforce the 30-second calibration age; publish pause and translation
latch in the atomic view; prioritize WindowChanged; and clean up owned Xvfb resources. OpenCvSharp notices are added
with this documentation change. The final independent audits still remain to be run.

## Synthetic measurements

All calibration and target fixtures below are labelled SYNTHETIC.

| Fixture | Measurement |
| --- | --- |
| Exact affine fit | Median fit residual 0.000 px; maximum validation residual 0.000 px; 12/12 fit inliers. |
| Two-outlier fit | RANSAC rejected both corrupted fit anchors; 10/12 inliers, or 83.3%. |
| Template target | Best score 1.0000; independent second peak 0.0000; margin 1.0000; visual box {159,141,24,20}; proposed point {171,151}. |
| Offline preview | Report labelled SYNTHETIC. Output directory mode 0700 and files mode 0600. An explicit --out inside the repository was rejected. No preview artifact was written into the repository. |

These measurements show deterministic behavior on the listed synthetic inputs. They do not establish calibration
accuracy on a Dofus Unity screenshot or localization accuracy on a real resource.

## Verified and inferred

| Category | Statement |
| --- | --- |
| VERIFIED | The C# build, formatting, suite counts, GUI and Bot test counts, Xvfb tests, preview smoke, and synthetic metrics listed above. |
| VERIFIED | Preview reads local files; it does not perform live desktop capture or input. The preview output guard rejects repository paths and the writer applies private Linux modes. |
| VERIFIED | Tests exercise XTest against fake Xvfb windows only. |
| INFERRED | The hybrid architecture is the selected target: protocol observations build CharacterState, planning stays in D3B.Bot/D3B.Engine, and a future GUI executor would ask the official client to produce its own gameplay requests. End-to-end wiring is not present. |
| NOT ESTABLISHED | A Dofus capture, any real click, movement or harvest, XTest compatibility with Dofus, universal Unity calibration, or detection of Dofus resources. |

## Corrections, limitations, and boundaries

- No Dofus capture or asset was used. No real click, movement, harvest, game connection, or Ankama connection was
  performed.
- Synthetic calibration is not Unity evidence. A captured image passing pixel checks is not a recognized scene.
- Resource locations have not been validated against relevant Dofus images. Template thresholds remain initial values.
- The P2N host and application do not register a GUI executor, input implementation, capture loop, or gameplay trigger.
- GuiWorldSnapshotAdapter, GuiIntentCorrelation, and GameInputModePolicy are contracts; the correlation and admission
  model are not wired to BotActor's player-priority path or the relay's final network admission point.
- Identical human and synthesized clicks can produce indistinguishable packet fields. Such an ambiguity must stop the
  future one-action flow; packet correlation is not cryptographic attribution.
- X11's focus check and global click retain a race between revalidation and emission. Window-manager decoration
  extents outside the selected XID are not measured separately.
- No network, root, Linux capability, Python, Frida, or Unity injection dependency was added.

## Remaining exit evidence

The final independent security and contract audits are still pending. Their results must be recorded before changing
this verdict to READY FOR P2N VISUAL GO. The P2O plan is prospective and does not authorize or perform a real action.
