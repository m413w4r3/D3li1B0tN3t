# P2O preflight mount boundary diagnostic

**Status:** Phase A diagnosis retained; P2O-PREFLIGHT-2 alias diagnosis fixed in code, with independent A3 re-audit pending. **Original base:** `d5a3d8a` (`master`). **Original branch:** `p2o/preflight-1`. **Original HEAD:** `a0bb4e2`. **Current base:** `c51a56e7d59c323645e013bce9b3e1d253e29246`. **Current branch:** `p2o/preflight-2`. **Code HEAD:** `c8e7f8b`.

## Symptom

`preflight snapshot --root ~/.config/Ankama/Dofus-dofus3` and the same command for `~/.local/share/D3B/client` exited 1 with `BLOCKED: mount_boundary_unsupported`. An earlier attribution to ZFS or `statx` was not supported by the measurements.

## Method

A temporary C probe in a private `/tmp` directory called `statx` with `AT_SYMLINK_NOFOLLOW` and `STATX_BASIC_STATS | STATX_MNT_ID`. It compared path results with `AT_EMPTY_PATH` results for open file descriptors, read `/proc/self/fdinfo/<fd>` mount IDs, and correlated the results with `/proc/self/mountinfo` and `findmnt --target`. It did not read file payloads.

## Evidence

- `~/.config/Ankama/Dofus-dofus3` contained 27,858 entries: 27,826 files and 32 directories. Every entry had device `0:38` and mount ID `9691`; path and file descriptor `statx` results agreed.
- `~/.local/share/D3B/client` contained 3 entries. Every entry had the same device and mount ID as its source root; path and file descriptor `statx` results agreed.
- Neither source tree contained a mount boundary.
- In the diagnostic process, `/home/nill` was on ZFS mount `9691` (`0:38`), `/` was mount `9016` (`0:27`), and `/tmp` was a separate mount view `13831` (`0:27`). Mount IDs are namespace-local: the parent namespace measured `/home/nill` as `60` and `/` and `/tmp` as `31`.
- `struct statx` was 256 bytes. The measured offsets were mask 0, mode 28, inode 32, size 40, ctime 96, mtime 112, device major/minor 136/140, and mount ID 144. `STATX_BASIC_STATS=0x7ff`, `STATX_MNT_ID=0x1000`, `AT_EMPTY_PATH=0x1000`, and `AT_SYMLINK_NOFOLLOW=0x100`. These match `ReadStatus`, its P/Invoke signature, and `PreflightFileStatus`.

## Cause and classification

The failure was in output parent ancestry, not in either source tree or the `statx` layout. After capture, `PreflightManifestStore.WriteAtomically` validates the manifest parent. `EnsureOutsideTree` calls `IsDescendantOrSelf`, which had required every ancestor to remain on one mount. `EnsureNoRepositoryMarker` calls `WalkAncestors`, which imposed the same requirement. Walking from an output directory under `$HOME` or `/tmp` to `/` therefore crossed an unrelated mount and threw `mount_boundary_unsupported` before the containment check could finish.

This was an **implementation defect in output ancestry containment**. The source trees had no mount boundary, and the `statx` layout, offsets, constants, P/Invoke, and parsing were correct. ZFS alone did not cause the failure.

## Fix history and RED evidence

The Phase A fix sequence was:

- `8d9f28a` — allow ancestry checks to cross unrelated mounts.
- `bcbbf00` — add the regression test.
- `a0bb4e2` — add boundary diagnostics and tests.

The regression `OutputContainmentAndRepositoryWalkAllowUnrelatedMountTransitions` failed before the fix at `EnsureSameMount` ← `EnsureAncestrySameMount` ← `IsDescendantOrSelf`. It passed after the ancestry fix. The tree-entry checks remain responsible for rejecting mount boundaries within a snapshot, backup, verify, or restore tree.

## Phase D diagnostic behavior

On a blocked mount boundary, a safe `DETAIL:` line reports the category, escaped relative path, expected and observed mount IDs, and expected and observed device major/minor values. It does not include file contents. The machine-readable `BLOCKED:` line remains present. Tests cover safe detail formatting and rejection when output is inside the source tree.

## P2O-PREFLIGHT-2 alias diagnosis

The A3 **MAJOR** on base `c51a56e7d59c323645e013bce9b3e1d253e29246` was a separate containment defect. Walking `..`
from an external bind alias follows the alias mountpoint ancestry, so the old root-identity walk and marker scan never
reach the source descendant. An output could therefore be written into the source through its alias.

Commit `1d645cf` indexes every source and repository directory using device+inode identity (mount ID is excluded),
checks every existing destination ancestor, allows unrelated outer mount transitions, and applies the same identity
mechanism to restore overlap and repository protection. Commit `c8e7f8b` adds real Linux user+mount namespace tests.

The RED reproduction on the base bind-mounted `source/deep` at `/tmp/alias`; snapshotting to `alias/manifest.json`
exited 0 with `OK snapshot entries=1` and created the manifest inside `source/deep`. After the fix, descendant,
deep-missing-parent, and source-root aliases return `BLOCKED: destination_aliases_source` without writing through the
alias; a repository descendant returns `BLOCKED: repository_output_forbidden`; restore beneath backup aliases
returns `BLOCKED: restore_paths_overlap` without a destination or `.preflight-stage-*`. Normal external snapshot
and restore still succeed and verify `OK`. The 7 namespace scenarios ran with 0 skips on this host; they skip with an
explicit reason when `unshare` or mount capability is unavailable. The implementation and residual limits are recorded
in [the report](P2O_PREFLIGHT_REPORT.md).
