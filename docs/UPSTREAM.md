# Upstream and fork strategy

## Baseline

The original Phase 0 report describes a Linux-native Dofus 3 bot fork starting
from upstream commit `0a995ad8a99f4417c99f2cf5eafe46cf13eb709b` (2026-11-02; merge commit
`Merge pull request #55 from AerafalDev/feat/step-9f4-glyphs`). The repository
is MIT licensed. At that historical baseline, the solution had 9 projects under
`src/` and 8 under `tests/`.

## Remotes

The current checkout has `origin` at
`git@github.com:m413w4r3/D3li1B0tN3t.git`; it has no configured `upstream`
remote. The original Phase 0 report's upstream URL is historical and does not
describe the current remotes. The fetched `origin/master` is one commit ahead of
local `master`, with a README-only change. See [Checkout reality (Phase 1)](#checkout-reality-phase-1).

### Recommended follow-up

Ask the owner to authorize re-adding AerafalDev/D3B as `upstream` for future
upstream merges. It is not configured in this checkout. After authorization and
confirmation of the desired URL, the future command is:

```sh
git remote add upstream https://github.com/AerafalDev/D3B.git
```

## Syncing upstream

For each update, fetch upstream, inspect the incoming commits and diff, then
create a review branch from the current integration branch. The following are
**future workflow commands**; replace `<upstream-branch>` after checking which
branch upstream uses, and replace `<phase>` and `<topic>` with the phase ID and
a short task name:

```sh
git fetch upstream --prune
git log --oneline --decorate HEAD..upstream/<upstream-branch>
git diff --stat HEAD...upstream/<upstream-branch>
git diff HEAD...upstream/<upstream-branch>
git switch -c p0-<phase>-<topic>-upstream-sync
```

Merge when importing a coherent upstream range and preserving its history is
the goal. Cherry-pick when importing a small, independent fix or feature is
safer than taking the full range. Keep Linux-specific behavior localized and
prefer additive modules and narrowly scoped documentation changes to reduce
overlap. Resolve conflicts deliberately in shared project/solution files,
package and build configuration, cross-platform abstractions, tests, and docs;
these are likely to change upstream too. Do not commit generated build output.

Before integrating, rebuild and run the applicable tests. These are **future
verification commands**, not run as part of documenting this baseline:

```sh
dotnet build D3B.slnx
dotnet test D3B.slnx
```

## Branches, notices, and repository hygiene

The branch `p0-1-upstream-docs` and commit
`docs: add fork and upstream strategy (P0.1)` are historical Phase 0 details.
Follow that `p0-<id>-<topic>` pattern for phase work; review and integrate
scoped commits deliberately.

Preserve `LICENSE` and its MIT copyright and permission notice in distributions
and substantial copies. Retain and update `THIRD-PARTY-NOTICES.md` whenever
bundled components, assets, or their licenses/notices change; preserve each
third-party notice and license text as required by its terms.

Never commit credentials, tokens, cookies, private configuration, client
captures, or large extracted game/client assets. Keep generated binaries,
intermediate output, and local environment files out of commits as well.

## Verification checklist

- Confirm the base commit and inspect the complete incoming upstream diff.
- Review conflicts in shared configuration, platform boundaries, tests, and docs.
- Confirm Linux changes remain localized and generated/private files are absent.
- Build and run relevant tests; review license and notice changes.
- Commit only the intended scope and record the imported upstream range.

## Checkout reality (Phase 1)

Verified in the re-imported checkout: local `master` is `38db76b` (`first`),
and the configured `origin` is `m413w4r3/D3li1B0tN3t`. Fetched `origin/master`
is `4406f09`, one commit ahead, changing only `README.md`; local `master` is
zero commits ahead and one behind. The worktree was clean at inspection. No
`upstream` remote is configured. This checkout does not establish push or pull
request history. The Phase 0 snapshot `29b8fb1` and objects `8f62ba4`, `29b8fb1`,
and `0a995ad` are absent here.
