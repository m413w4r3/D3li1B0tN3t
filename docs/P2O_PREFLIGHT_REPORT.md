# P2O preflight mount boundary fix report

**Base:** `d5a3d8a` (`master`). **Branch:** `p2o/preflight-1`. **HEAD:** `a0bb4e2`.

## Summary

Phase A found that the source trees had no mount boundary. The false block came from output parent ancestry checks that rejected unrelated mount transitions on the way to `/`. The implementation has been corrected, the failure has a regression test, and Phase D now provides safe boundary details. The independent A3 audit returned **REJECT** for a bind-mount descendant alias gap, so the mission verdict is **BLOCKED**. Phase F proves normal-path preflight only and does not establish live action safety.

See [the diagnostic](P2O_PREFLIGHT_DIAGNOSTIC.md) for the Phase A measurements and cause analysis.

## Changes and test evidence

| Commit | Change |
| --- | --- |
| `8d9f28a` | Fix ancestry checks so they may cross unrelated mounts. |
| `bcbbf00` | Add regression coverage for ancestry across mounts. |
| `a0bb4e2` | Add safe boundary diagnostics and tests. |

The regression test failed before the fix at `EnsureSameMount` ← `EnsureAncestrySameMount` ← `IsDescendantOrSelf`, then passed after the fix. The final `D3B.Scripts.Tests` result was 69/69.

Phase E checks completed with the pinned SDK:

| Check | Result |
| --- | --- |
| Build | Exit 0; 0 warnings and 0 errors. |
| Full serialized suite | 2,306 total; 2,302 passed, 4 skipped, 0 failed; exit 0. |
| `D3B.Scripts.Tests` | 69/69 passed. |
| Format verification | Exit 0. |
| `git diff --check` | Exit 0. |

## Phase F: private snapshot and restore checks

Phase F used the fixed tool in a temporary worktree at `bcbbf00` and private destinations. It did not use Dofus, Zaap, the host, or input, and it did not modify Ankama files.

- D3B client: snapshot `OK`, 3 entries; manifest mode `0600` inside a directory with mode `0700`; verify `OK`.
- Ankama source: snapshot `OK`, 27,826 entries; the 7.3 MB manifest had mode `0600`; verify against the source `OK`.
- Private backup: `OK`, 27,826 entries and 12 G. Internal content verification checked path, kind, size, hash, and link target.
- An external `verify` of the backup tree against the source manifest returned `tree_mismatch` because the private backup files are intentionally mode `0600`, while the manifest records the source files' original modes. This mode difference is expected and is not content corruption.
- Restore to a private target: `OK`. Verify of the restored target against the source manifest: `OK`.
- Verify of the live Ankama source against the before manifest: `OK`; the live source was untouched.

## Limits and remaining gate

- Numeric mount IDs are namespace-local and must not be compared across namespaces as stable identifiers.
- A real bind-mount integration test was not run. Automated coverage exercises the safe formatting and output-inside-source rejection cases.
- An external verify of the private backup tree reports the expected mode difference described above; its internal content verification passed.
- **A3 independent audit: REJECT.** MAJOR: an external bind mount of a source **descendant** can evade `EnsureOutsideTree`, restore-overlap, and the `WalkAncestors` repository-marker scan because walking `..` from the alias follows the external mountpoint ancestry and never reaches the source root. This requires a pre-existing privileged mount. Paths: `scripts/preflight/PreflightUnix.cs:471,612`; `PreflightOperations.cs:379-385`. MINOR: no real bind-mount test; the bind test uses synthetic statuses and does not exercise the live walk. A second independent review rated the same gap MINOR; the more severe A3 verdict is retained.
- Phase F proves normal-path snapshot/backup/restore on the actual roots; it does not cover the bind-alias gap.
- Live action remains separately blocked. This report makes no claim that a live Dofus action is safe or accepted.

## Verdict

**BLOCKED** for P2O observation preflight. Normal-path preflight evidence is complete, but the A3 REJECT MAJOR bind-mount descendant alias gap must be fixed and independently re-audited before READY. This is not live action approval or a live-client acceptance result.
