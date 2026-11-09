# P2O preflight mount boundary fix report

**Base:** `c51a56e7d59c323645e013bce9b3e1d253e29246`. **Branch:** `p2o/preflight-2`.
**Code HEAD:** `c8e7f8b` (this documentation commit follows).

## Summary

The previous A3 **MAJOR** finding is fixed. An external bind mount of a source descendant evaded
`EnsureOutsideTree`/`IsDescendantOrSelf` and the repository-marker walk because walking `..` from the alias followed
the alias mountpoint ancestry instead of reaching the source. The fix indexes directory identities by device and inode
and checks each existing destination ancestor against that index. Code is committed; an independent A3 re-audit of
this commit is pending. This report does not claim READY.

The earlier Phase A diagnosis remains valid: source trees had no mount boundary; the failure was output ancestry
rejecting unrelated mount transitions. Commits `8d9f28a`, `bcbbf00`, and `a0bb4e2` allowed unrelated transitions,
added regression coverage, and added safe boundary diagnostics. See [the diagnostic](P2O_PREFLIGHT_DIAGNOSTIC.md).

## Changes

| Commit | Change |
| --- | --- |
| `1d645cf` | Add a device+inode directory identity index, a single ancestry walker, source and restore containment checks, repository identity protection, and deterministic tests. |
| `c8e7f8b` | Add Linux user+mount namespace integration tests using real bind mounts. |

## Evidence

- **RED on `c51a56e`:** inside `unshare --user --map-root-user --mount`, bind-mounting `source/deep` at
  `/tmp/alias` let `snapshot --root source --output alias/manifest.json` exit 0 with `OK snapshot entries=1`;
  `manifest.json` appeared inside `source/deep`.
- **GREEN after the fix:** source-descendant aliases, aliases with a deep missing output parent, and source-root
  aliases exit 1 with `BLOCKED: destination_aliases_source`; no file or directory is written through the alias.
  A repository-descendant alias returns `BLOCKED: repository_output_forbidden`. Restore destinations under backup
  aliases return `BLOCKED: restore_paths_overlap`, with no destination or `.preflight-stage-*` directory created.
  Normal external snapshot and restore succeed and verify `OK`.
- `D3B.Scripts.Tests`: 92/92, including all 7 real namespace scenarios, 0 skipped.
- Pinned SDK build: 0 warnings and 0 errors. `dotnet format --verify-no-changes` and `git diff --check` exited 0.
- **Full suite:** 2,329 tests, 4 skipped (`D3B.Diagnostic`), 0 failed. It ran serialized in two environments:
  10 non-GUI test assemblies in a privileged Debian 12 container with the pinned SDK/runtime (2,104 tests,
  4 skipped), plus `D3B.Gui.Tests` on the host because its X11 tools are host-only (225/225).
  The standard host `dotnet test --solution` run as uid 1000 first produced 28 failures in Network/Bot/App/Storage
  after exhausting `fs.inotify.max_user_instances=128` (`The configured user limit (128) on the number of inotify
  instances has been reached`). The same built assemblies passed with a fresh UID inotify budget. This is an
  environment limit, not a containment regression.
- **Live source checks:** `verify` of `~/.local/share/D3B/client` (3 entries) and
  `~/.config/Ankama/Dofus-dofus3` (27,826 entries) against the Phase F manifests in `/tmp/p2o-preflight-f.0X4Q1S`
  returned `OK`. The Ankama verify took about 91 seconds and was read-only. A private snapshot created a parent
  directory with mode `0700` and a manifest with mode `0600`.

### Historical Phase E/F record

The earlier Phase E run recorded build success (0 warnings/errors), 2,306 full-suite tests with 2,302 passed,
4 skipped and 0 failed, `D3B.Scripts.Tests` 69/69, format success, and a clean diff check. Phase F used the fixed
tool at `bcbbf00` with private destinations; it did not use Dofus, Zaap, the host, or input and did not modify Ankama
files. It recorded D3B client snapshot/verify `OK` (3 entries), Ankama snapshot/verify `OK` (27,826 entries; 7.3 MB
manifest), a private backup of 27,826 entries and 12 G with internal content verification, and private restore plus
verification `OK`. External verification of that private backup against the source manifest returned the expected
`tree_mismatch` because backup files were mode `0600` while the manifest retained original source modes; this was not
content corruption. The earlier A3 audit rated the descendant bind-alias gap **MAJOR**; a second independent review
rated it **MINOR**, and the more severe A3 classification was retained.

## Limits

### Demonstrated on this host

The bind-alias RED reproduction on the base and GREEN scenarios after the fix ran in real Linux user and mount
namespaces. Normal external snapshot and restore, the live source verifies, and private output modes were also
observed as described above.

### Deterministic synthetic coverage

Synthetic tests cover directory identities, ancestry, mount transitions, containment, and failure behavior without
requiring namespace privileges. They do not by themselves prove the kernel bind-mount behavior exercised by the
namespace scenarios.

### Coverage requiring Linux privileges

The seven integration scenarios use `unshare` and bind mounts. They ran on this host; when Linux user/mount namespaces
or mount capability is unavailable, the tests skip with an explicit reason. Such a skip is not a passing real-mount
result.

### Residual limits

- Repository protection anchors on `Directory.GetCurrentDirectory()` and `AppContext.BaseDirectory`; a detached alias
  of another repository is not identifiable without mount metadata.
- The repository index is a per-process snapshot. Concurrent privileged namespace/mount or repository changes after
  discovery are outside the pre-check. Transient repository changes during index capture fail closed as
  `repository_index_unavailable`.
- A hostile local administrator is out of scope.
- Numeric mount IDs are namespace-local and are not stable identifiers across namespaces.
- The existing `renameat2` check-to-syscall window remains.
- Live action remains separately **BLOCKED** (D340). This report covers observation preflight only.

## Verdict

The previous A3 **REJECT** is addressed in code and evidence. Independent A3 re-audit is pending on this commit.
No READY claim is made, and this is not live action approval or a real-client acceptance result.
