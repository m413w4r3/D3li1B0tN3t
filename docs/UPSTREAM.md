# Upstream and fork strategy

## Baseline

This Linux-native Dofus 3 bot fork starts from upstream commit
`0a995ad8a99f4417c99f2cf5eafe46cf13eb709b` (2026-11-02; merge commit
`Merge pull request #55 from AerafalDev/feat/step-9f4-glyphs`). The repository
is MIT licensed. At this baseline, the solution has 9 projects under `src/` and
8 under `tests/`.

## Remotes

The remote named `upstream` points to `https://github.com/AerafalDev/D3B.git`.
There is currently no `origin` or fork remote. After creating a fork, add its
URL using this **future command** (replace the placeholder; do not guess a URL):

```sh
git remote add origin <your-fork-url>
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

This mission uses the isolated branch `p0-1-upstream-docs` and a scoped commit
`docs: add fork and upstream strategy (P0.1)`. Follow that `p0-<id>-<topic>`
pattern for phase work; review and integrate scoped commits deliberately.

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
