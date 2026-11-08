# P2N-V — first real capture and Unity calibration

## Decision

**READY FOR P2O OFFLINE INTEGRATION**

The real client capture, the verified X11 window identity, and the real Unity calibration are demonstrated and
reproducible: a second capture of the same scene produced identical calibration metrics. The resource localization
also passed for a bounded template, with the limitations recorded below.

This verdict authorizes only offline P2O integration. It does not authorize movement, harvesting, a click, protobuf
injection, a relay start, or any other live action. No relay was started or reconfigured during this session.

## Scope and safeguards

- The agent performed only read-only X11 inspection (`xwininfo`, `xprop`, `xdotool getwindowgeometry`,
  `xdotool getwindowfocus`) and `maim` capture of the selected window.
- No focus click, no XTest input, no keyboard or mouse simulation, no protobuf injection, no game action.
- The D3B relay was not started and its configuration was not modified.
- The client and its files were not modified by the agent.
- The offline preview performed no capture, input, click, or network request.

## Preflight

| Item | Result |
| --- | --- |
| Base | `D3B_linux` branch `master`, HEAD `ceacbed9d01fb8afc7e4da59143a94f8678739b6`; tree identical to `f87b1af` (`a94c5f74…`) |
| Worktree | Clean (0 tracked changes in `D3B_linux`) |
| Client | Dofus `3.7.13.17`, BuildDate `2026-11-03T14:11:23+00:00` |
| Client binary identity | `Dofus.x64 6d7b6348…`, `GameAssembly.so f2aa597d…`, `UnityPlayer.so b01fc32d…`, `global-metadata.dat dea17c3c…`; all four prefixes match the audited P2J GO1 identity |
| Runtime data | Pack `3.7.13.17` present; map `120063490` present |
| X11 | Native Xorg/X11 `:0`, `XDG_SESSION_TYPE=x11`, 1920×1200 root, XTEST opcode 132 |
| Capture tools | `maim`, `xdotool`, `xdpyinfo`, `xwininfo`, `xprop` |
| Vision runtime | `OpenCvSharp5 5.0.0.20261018` and the Linux-x64 slim runtime present; preview loaded and ran |
| .NET SDK | Pinned `10.0.403` used explicitly from `~/.dotnet` (not on `PATH`) |
| P2N backend | `scripts/gui-preview.cs --help` passed; no capture or input in the preview |
| D3B before test | No D3B app process, no D3B listener or socket; apphost deployed but not running |
| Host manifest | Deployed apphost manifest recorded (371 files), unchanged after the session |

## 1. Capture — PASS

### Window identity: VERIFIED

| Field | Value |
| --- | --- |
| XID | `0x7a00008` (decimal `127926280`) |
| PID | `755778` |
| Executable | `~/.config/Ankama/Dofus-dofus3/Dofus.x64` |
| Title | `Dofus` (generic; not identity evidence) |
| Class | `Dofus.x64` |
| Geometry | 1920×1175 at desktop `(0,25)`; X11 border width 0 |
| State | `WM_STATE Normal`; `_NET_WM_STATE_MAXIMIZED_HORZ`, `_NET_WM_STATE_MAXIMIZED_VERT`, `_NET_WM_STATE_FOCUSED`; `Map State: IsViewable` |
| Focus / active | Focused XID `127926280`; `_NET_ACTIVE_WINDOW` = `0x7a00008` |

The window was maximized under a 25-pixel desktop top panel. No window-manager decoration outside the selected XID was
measured; the selected XID has border width 0. The identity was verified without a focus click or any other input.

### Capture geometry and content

| Item | Value |
| --- | --- |
| Client area | 1920×1175 at desktop `(0,25)` |
| PNG dimensions | 1920×1175 (all retained captures) |
| PNG origin | Window-local `(0,0)`; desktop offset `(0,25)` |
| Borders | X11 border 0; no decoration inside the selected XID |
| Content | Unity scene visible and usable; not black, not masked by another window, no private chat, ticket, or private panel visible |
| Privacy | Top-left nameplate redacted in every retained capture; map-area pixels identical before/after redaction (difference 0 in the map area; only 22,321 pixels changed, the redaction rectangle) |

Captures retained privately (redacted, mode 0600; directory mode 0700):

| Evidence | SHA-256 |
| --- | --- |
| `capture-01-redacted.png` (no in-game grid) | `8e8d39ea72d81a3fb4714d45a97b8c225a1ba3e2376dc884db561c1443b7732b` |
| `capture-02-grid-redacted.png` | `c4a20f388fd0783034c0c351947867f6fc301a2a81c2d01d858aeee9d5722083` |
| `capture-03-grid-redacted.png` | `52f932d709403ab2b1410a62f713144bfbbd8fcf4c65796ad923ef7b45c59719` |

Raw, unredacted captures were deleted after the redacted copies and regenerated evidence were verified. No screenshot
was written to Git.

## 2. Live map identity and provenance

The live map was identified offline from the client data pack and the visible scene:

- The client HUD location text was redacted; the atlas match uses the sub-area **Tainéla** and coordinates **(2,−33)**.
- The client's `world/atlas` (`MapsInformationDataRoot`) contains exactly one map at `(2,−33)`:
  **map `120063490`**, sub-area `30` (Tainéla), area `18` (Astrub), world map `1`.
- Map `120063490` has 506 walkable and 54 non-walkable cells. Its 54 non-walkable clusters align with the four main
  houses and the visible obstacles; the fifth, partly visible house lies outside the cell grid (background decoration).
- The map has five interactive elements:

| ElementId | CellId | GfxId |
| ---: | ---: | ---: |
| 483684 | 168 | 654 |
| 483686 | 535 | 653 |
| 483703 | 125 | 677 |
| 483705 | 293 | 677 |
| 483706 | 461 | 651 |

The P2J GO1 map `191105026` is a different map: the atlas places it at `(5,−18)` in Cité d'Astrub, with four
interactives. It is not the live map. The live character was on Tainéla, not on the P2J GO1 map.

**Provenance limitation.** The live MapId `120063490` is established by the client atlas (unique coordinate/sub-area
match) and corroborated by the 54-cell non-walkable fingerprint. It is **not** a live server observation, because the
D3B relay was intentionally not started in this GO. P2O must confirm the live MapId with a server observation before
binding any action.

## 3. Unity calibration — PASS

### Anchor document

The document is a `CalibrationAnchorDocument` format version 1 with `clientBuild = 3.7.13.17`, `mapId = 120063490`,
image `1920×1175`, `synthetic = false`, 8 fit anchors and 3 independent validation anchors. It is retained privately as
`anchors-120063490.json` (SHA-256 `e9386a70c17c543a28d305767e523c0b1a7ba9743fb8e5c2cc772ccbba58e8ed`).

Annotation method: the supervisor enabled the in-game cell grid; the grid line families were measured at slopes
**±0.50** with an intercept pitch of **50.50 px** (line-fit RMS 0.20–0.25 px), giving cells of approximately
**101×50.5 px**. The anchors were placed at the visually verified centers of the in-game cell diamonds. No anchor was
adjusted after seeing a residual or a score.

| Cell | Role | Pixel (x, y) | Residual / error (px) |
| ---: | --- | ---: | ---: |
| 56 | fit | 278, 117 | 0.055 |
| 66 | fit | 1288, 117 | 0.236 |
| 113 | fit | 379, 218 | 0.066 |
| 340 | fit | 682, 622 | 0.287 |
| 348 | fit | 1490, 622 | 0.119 |
| 402 | fit | 1288, 723 | 0.230 |
| 510 | fit | 884, 926 | 0.580 |
| 555 | fit | 1237, 1001 | 0.356 |
| 280 | validation | 277, 521 | 0.997 |
| 457 | validation | 1187, 824 | 0.328 |
| 550 | validation | 732, 1001 | 0.429 |

### Metrics and thresholds

| Metric | Measured | Threshold | Result |
| --- | ---: | ---: | --- |
| Median fit residual | **0.233 px** | ≤ 4 px | PASS |
| Maximum fit residual | 0.580 px | — | — |
| Maximum validation error | **0.997 px** | ≤ 8 px | PASS |
| Largest individual validation error | 0.997 px | ≤ 12 px | PASS |
| RANSAC inlier ratio (fit anchors) | **100.0 %** | ≥ 80 % | PASS |
| Rejected fit anchors | none | — | PASS |

The six affine coefficients are:

| Coefficient | Value |
| --- | ---: |
| `a` | 50.49297041072401 |
| `b` | 50.505947988687595 |
| `tx` | 278.0800514738653 |
| `c` | 25.255843330816408 |
| `d` | −25.28025997507299 |
| `ty` | 15.939655972695407 |

These coefficients match the isometric lattice (`a ≈ b ≈ 50.5`, `c ≈ −d ≈ 25.27`). The annotated preview showed the D3B
projected grid coinciding with the in-game grid; no systematic offset was visible.

### Reproducibility

A second capture of the same scene (`capture-03-grid-redacted.png`) was calibrated with exactly the same 11 anchors:

| Run | Status | Median fit | Max validation | Inliers | CalibrationId |
| --- | --- | ---: | ---: | ---: | --- |
| Calibration on capture 02 | calibrated | 0.233 px | 0.997 px | 100 % | `69afa88720120b7897763996c373c3117b0e4b8a7131ec5d92dc070d892274bd` |
| Calibration on capture 03 | calibrated | 0.233 px | 0.997 px | 100 % | `9f3d7919823bbb96a950761715cbda6be12edcad10a92d8dd46f5f68d2f3025f` |

The metrics, residuals, rejected-anchor set, and all six affine coefficients are identical. The `CalibrationId`
differs because it includes the capture identity; the geometric profile is the same. This demonstrates
reproducibility of the real calibration.

The preview document reports `isSynthetic: false` and `label: null`. The preview tool's generic limitation string
("A synthetic calibration is not validation on Unity") is boilerplate text, not a synthetic label on this document.

## 4. Resource localization — PASS (bounded template)

The target was the visible plant at **ElementId `483706`, CellId `461`, GfxId `651`**. Its `ElementId`, `CellId`, and
`GfxId` are established by the client map data. Its exact interactive kind, skill, and name are **not** established:
the client map data does not name the kind, and no server interaction was observed.

Template tests were performed offline with `TemplateTargetLocator`. The successful template was a **50×60 px** crop of
the lower foliage (SHA-256 `7b24d6625c1f02bf02e0c51a4b7123fe5d24ef7823255805a00846866f07194b`). The bounded search
radius is the locator default of ±96 px around the projected cell center; the maximum allowed center distance is the
default 64 px.

| Test | Image | Template | Score | Runner-up | Margin | Status | Proposed point |
| --- | --- | --- | ---: | ---: | ---: | --- | --- |
| Same frame | capture 03 (grid) | 50×60 grid | 0.999993 | 0.263805 | 0.736188 | Verified | (1591, 810) |
| Cross-frame | capture 02 (grid) | 50×60 grid | 0.985159 | 0.263813 | 0.721346 | Verified | (1591, 810) |
| No-grid self | capture 01 (no grid) | 50×60 no-grid | 1.000000 | 0.293947 | 0.706053 | Verified | (1591, 810) |
| No-grid template on grid image | capture 03 (grid) | 50×60 no-grid | 0.914562 | 0.228387 | 0.686176 | Verified | (1591, 810) |

Measurements:

- Projected cell center: **(1591, 824)**. Proposed point: **(1591, 810)**. Offset: **14 px upward** from the cell
  center.
- The visual envelope of the whole plant is approximately **75×130 px** (source box `x=1560…1635`, `y=705…835`), with
  an estimated visual center near `(1597.5, 770)`, approximately **54 px above** the cell center.
- The whole-plant 75×130 template **failed** with the default bounded search: score `0.187945`, runner-up `0.110475`,
  margin `0.077470`, status `Unverified` (`ScoreTooLow`); the best bounding box was at the search-region boundary
  (`x=1611, y=730`). Cause: the template top lies about 119 px above the cell center, outside the ±96 px search region
  centered on the cell. The locator's default assumptions fit objects whose visual box is centered near the cell; tall
  resources need a smaller anchor template or a larger/configurable search radius.
- Ambiguity is measured only inside the bounded search region: runner-up scores were 0.228–0.294, margins 0.686–0.736.
  This is not a whole-image ambiguity measurement.
- UI occlusion: the target box `x=1566…1616, y=780…840` does not intersect the right-side UI bar (starting near
  `x=1880`), the top-left nameplate (`x<220`), or the top-right icons (`y<60`). The in-game grid overlay crosses the
  scene and reduced the score in the cross-condition test; it is a display overlay, not a UI mask.
- No click was performed. The preview proposes a point only in its offline report; it does not perform input.

## 5. Projection deviations and error causes

- The median fit residual is **0.233 px** and the maximum independent validation error is **0.997 px**, both far
  below the P2N thresholds. There were no RANSAC rejects; the inlier ratio is 100 %.
- The dominant residual sources are the manual grid measurement (line-fit RMS 0.20–0.25 px), the one-pixel grid line
  rendering, and rounding the measured centers to integer pixels. No evidence of perspective or non-affine distortion
  appeared at this scale: the six-parameter affine reproduces the isometric lattice with coefficients consistent with
  the measured 101×50.5 px cells.
- The largest fit residual (0.580 px, cell 510) and the largest validation error (0.997 px, cell 280) show no common
  direction or systematic bias.
- The live MapId is atlas+fingerprint verified, not live server-observed; this is a provenance limitation, not a
  geometric error.

## 6. Client build compatibility

- Installed client: **3.7.13.17**, BuildDate `2026-11-03T14:11:23+00:00`.
- The four binary prefixes still match the audited P2J GO1 identity:
  `Dofus.x64 6d7b6348…`, `GameAssembly.so f2aa597d…`, `UnityPlayer.so b01fc32d…`,
  `global-metadata.dat dea17c3c…`.
- The `3.7.13.17` data pack is present; map `120063490` and the five interactive elements above were read from it.
- No client-install file under `~/.config/Ankama/Dofus-dofus3` was modified during the session (no install file has an
  mtime after the session start), and the version file still reports `3.7.13.17`.
- The P2J GO1 map `191105026` remains valid in this build; the live map difference is a position/session difference,
  not a build incompatibility.

## 7. End-of-session checks

| Check | Result |
| --- | --- |
| Dofus/Zaap/Ankama processes after close | None |
| D3B app processes | None (`D3B.Host`, `D3B.App`, `D3B.Bot`, `gui-preview`, `d3b-diagnostic`) |
| D3B listeners / sockets | None; the Dofus/Zaap listeners on ports 26116/26117 are gone |
| Selected XID | Gone (`Bad Drawable`; no longer in `_NET_CLIENT_LIST`) |
| Deployed D3B apphost | Unchanged (371-file manifest identical before/after) |
| Client install | No mtime change after session start; binaries and version unchanged |
| Ankama/Zaap launcher state | Zaap wrote its own repository/index/settings/log files at launch and close (normal launcher operation). No file was written in the grid-toggle window (22:02–22:15 local), so the in-game grid was a runtime overlay; no grid-like key exists in the game `settings.json`. The pre-session launcher hash manifest was malformed by a shell-quoting error, so a byte-for-byte launcher comparison is not available; this is a reporting limitation, not an agent write. |
| Synthetic input | None. Only read-only X11 queries and `maim` were used; no `xdotool` move/click/key/type command was issued. |
| Git | `D3B_linux` has no other tracked changes; this report is the only new file. The supervisor authorized commit and merge on `master`; the commit/merge is local and no push is performed. Parent repository retains its pre-existing modified `D3B_linux` gitlink. No image is tracked or staged. |
| Capture privacy | Only redacted captures are retained; files 0600, directories 0700; raw captures deleted; no screenshots in Git. |

## 8. Limits and required work before P2O

1. **MapId provenance.** `120063490` is atlas+fingerprint verified, not live server-observed. Confirm it with a relay
   observation before binding any action or resource to this profile.
2. **Tall resource templates.** The locator now accepts a per-target visual-anchor offset and a configurable search
   radius. Synthetic coverage confirms the default still rejects the 75×130 template while an explicit −14 px anchor
   and 128 px radius can find it. The verified 50×60 lower-foliage measurement remains a 14 px upward offset from
   the projected cell center; callers must pass that measurement for the target they are locating.
3. **Display overlays.** The in-game grid changes image pixels and lowers template scores. Templates must be captured
   under the same display settings or made invariant to overlays. The small template still passed cross-condition at
   `0.915`, but the full template failed.
4. **Interactive kind and skill.** The map data gives `ElementId`, `CellId`, and `GfxId`, but not the interactive kind
   or the harvest skill. A supervised server observation is required before any harvest or click.
5. **Offline preview identity.** The preview uses a placeholder `NotFound` window identity; its calibration profile is
   not action-ready. A future action requires the verified live identity, a fresh capture within the 30-second
   freshness bound, and the action gate's exact identity checks.
6. **Preflight state capture.** Fix the shell quoting in the Ankama launcher hash manifest; the current session can
   prove the Dofus client install unchanged by mtime and binary identity, but not every launcher-managed file
   byte-for-byte.
7. **UI exclusions.** The locator accepts validated, caller-supplied rectangles and rejects a candidate when its box or
   proposed point overlaps one. Callers still need to measure the nameplate, right-side bar, and top icons for each
   relevant frame layout; this report does not establish universal Dofus UI coordinates.
8. **Window-manager decorations.** The selected XID had border width 0 and no decoration was measured outside it. Other
   window-manager configurations must be measured separately.
9. **Scene and zoom changes.** Capture, calibration, dimensions, map, and target identity are bound to each result, and
   the action gate invalidates a result for another capture or frame size. Matching XID and dimensions do not reveal a
   Unity zoom or scene change by themselves; callers must revalidate calibration and target measurements.

## 9. Verdict

**READY FOR P2O OFFLINE INTEGRATION**

The three required gates are met and reproducible:

- **CAPTURE: PASS** — real window content captured twice, 1920×1175, visible and non-black, with private data
  redacted.
- **WINDOW IDENTITY: VERIFIED** — exact XID, PID, executable, geometry, visibility, focus, and active window.
- **UNITY CALIBRATION: PASS** — median fit error 0.233 px, maximum independent validation error 0.997 px, 100 %
  RANSAC inliers, with identical metrics on a second capture.

Resource localization is **PASS** for a bounded 50×60 template (0.985 cross-frame score, 0.721 margin, 14-px visual
offset), with the tall-template and overlay limitations above. The MapId provenance and action-readiness items are
P2O work; no live action is authorized by this report.

The P2O offline locator implementation keeps zero offsets, a 96 px search radius, and a 64 px candidate-center limit
as defaults. It does not hard-code the measured 14 px target offset or any UI exclusion. Its synthetic tests exercise
large-radius tall-template matching, caller-provided exclusions, no-proposal failure paths, and result binding. No
private capture or template was added to the repository.

### P2O targeting verification

| Check | Result |
| --- | --- |
| Pinned-SDK solution build | Pass; 0 warnings, 0 errors. |
| `D3B.Gui.Tests` | Pass; 202/202 (190 before this lot, +10 locator cases, +2 action-gate cases). |
| `D3B.Bot.Tests` | Pass; 649/649, unchanged. |
| `dotnet format --verify-no-changes --no-restore` | Pass. |
| `git diff --check` | Pass. |

RED/GREEN evidence is synthetic: the new anchor-distance case failed against the prior locator with
`TooFarFromExpectedCell` (90 px from the cell despite being 50 px from the expected visual center), then passed with
the configured anchor-relative check. The tall-template test keeps the default 96 px search unverified with no
proposed point and verifies the same synthetic 75×130 image with a −14 px anchor and 128 px radius. NuGet audit was
disabled only for the local commands because `api.nuget.org` was unreachable; repository configuration was not
changed. Test and formatting tools used local-socket permission because their runners require IPC sockets.

One repeated GUI run had an Xvfb fake-window startup failure; its next full run passed 202/202. One repeated Bot run
had an existing fight request-order assertion fail; that case passed alone and the next full run passed 649/649.

## Private evidence

The redacted captures, anchor document, templates, and preview reports are outside Git under
`~/.local/share/D3B/p2n-visual/` (directory 0700, files 0600). The retained evidence hashes are:

| Evidence | SHA-256 |
| --- | --- |
| `capture-01-redacted.png` | `8e8d39ea72d81a3fb4714d45a97b8c225a1ba3e2376dc884db561c1443b7732b` |
| `capture-02-grid-redacted.png` | `c4a20f388fd0783034c0c351947867f6fc301a2a81c2d01d858aeee9d5722083` |
| `capture-03-grid-redacted.png` | `52f932d709403ab2b1410a62f713144bfbbd8fcf4c65796ad923ef7b45c59719` |
| `anchors-120063490.json` | `e9386a70c17c543a28d305767e523c0b1a7ba9743fb8e5c2cc772ccbba58e8ed` |
| `template-plant-small-element483706.png` | `7b24d6625c1f02bf02e0c51a4b7123fe5d24ef7823255805a00846866f07194b` |
| `out-redacted-cal-02/report.json` | `fb1177dd769570cbe14bf9510c1b12325ae49f8c82d3ff2e9a9639e6ce3d153e` |
| `out-redacted-cal-03/report.json` | `ce89b13c70f0fd5a7e7ab38b4d4e709e7984fb328b2baf01cc620d0efc100a2a` |
| `out-redacted-target-03/report.json` | `e54594e7cc7f625a99fdc24de697f57b8a9918c8d963042ba6f9a88387643e49` |
| `out-redacted-target-02/report.json` | `8548b4646ac658078d9ae1c2334aa1684bf61a8d2087a83bd2fe8a1b735b161c` |
| `out-redacted-target-nogridself/report.json` | `709e1bcd26f934522de61fcaf09260ed80fbd29e4d0112abebf768977ec5fc04` |
| `out-redacted-target-nogridcross/report.json` | `298f9de475fed079af3f4b9ded44398a2d29919e08b30e01ad1a09ae6d5d4d54` |

No screenshot is present in Git. The report is committed on `master` by a local merge; no push is performed.
