# Agent entry points

- Start with `CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/UPSTREAM.md`, `docs/BASELINE.md`, `docs/TESTING.md`,
  `docs/RISKS.md`, and the actual worktree status.
- Repository documentation and code are in English. Follow `CLAUDE.md` for scope, style, and project decisions.

## Linux SDK and checks

- `global.json` pins SDK `10.0.403`, installed at `/home/nill/.dotnet`. Use `/home/nill/.dotnet/dotnet`; system
  `/usr/bin/dotnet` is `10.0.114` and fails the pin. Do not change the pin to make local commands pass.
- Build, run the full suite, and check formatting from the repository root:

  ```sh
  /home/nill/.dotnet/dotnet build
  /home/nill/.dotnet/dotnet test
  /home/nill/.dotnet/dotnet format --verify-no-changes
  ```

- Smoke file-based tools with their help command; use the pinned executable:

  ```sh
  /home/nill/.dotnet/dotnet run --file scripts/inspect.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/capture.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/client.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/extract.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/schema.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/mapping.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/fights.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/release.cs -- --help
  /home/nill/.dotnet/dotnet run --file scripts/embed.cs -- --help
  ```

## Tests, client data, and delivery

- Add automated tests for `src/` behavior. Test names are PascalCase sentences without underscores; keep fixtures
  deterministic. D10 excludes standalone tests for file-based scripts, with the narrow linked-source exception in
  `tests/D3B.Scripts.Tests/` for `scripts/unity/ClientInstall.cs`, `ClientPlatform.cs`, and
  `scripts/schema/CallRegisters.cs` (P0.11 ABI tests).
- If the host inotify limit is tight, run the full suite with `--max-parallel-test-modules 1`; the final Phase 0 run
  passed 1,202/1,202 this way, while the default parallel run hit the environment limit.
- Never commit captures, credentials, secrets, installed client files, extracted assets/data, generated packs, schemas,
  or per-build mappings.
- Stay on the assigned worktree and branch; check `git status` before and after edits and keep each change scoped. Do not
  push, open a pull request, or merge without explicit owner approval. Commit only when the task asks for a commit.
- A real-client test is MANUAL/SUPERVISED. Label it that way and record the client/build and observable result; do not
  present offline replay or a mock as real-client acceptance.
- **Definition of done:** scoped implementation and docs, required `src/` tests, build and format checks green with the
  pinned SDK, relevant script smoke checks complete, no client data or secrets committed, and any real-client result
  clearly marked MANUAL/SUPERVISED.
