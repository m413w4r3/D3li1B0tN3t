# Contributing to D3B

Thank you for wanting to help. Bug reports, measurements on your own fights and harvests, fixes, docs and new features
are all welcome. This guide says what D3B accepts, how the repository is organized, and the conventions the build
checks, so that your pull request goes through without a round of style fixes.

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md). Vulnerabilities go through the
[security policy](SECURITY.md), never a public issue.

## Four rules that are never bent

1. **Nothing from [D2](docs/decisions.md#d2-what-d3b-never-contains).** No evasion of Ankama's detection (simulated
   mistakes, timings shaped to avoid bans, forged hardware identifiers), no account creation in bulk, no subscription or
   payment automation, nothing that disables or bypasses Ankama Shield, no proxy per account to hide multi-accounting.
   A pull request adding one is closed, whatever else it brings.
2. **Nothing extracted from the official client is committed**: no assets, game data, dumps, packs, schemas, per-build
   mappings or `.proto` text written from a schema. Packs and mappings ship as release assets. What is ours is
   committed: the readable `.proto` files in `src/D3B.Protocol` and the pinned pairs in `src/D3B.Protocol/Pins`.
3. **Never attach a capture, a database or a full log.** A capture holds the account's login ticket, and `d3b.db` names
   your accounts and characters. To show what the protocol did, describe it with `scripts/capture.cs`, which prints a
   capture's structure, never its content.
4. **Ideas from other bots, never their code.** Other projects may be read for ideas, and the idea is then rewritten.
   Code under the GPL would change D3B's license, and code without a license cannot be reused at all
   ([D3](docs/decisions.md#d3-the-references-in-vendor-give-ideas-never-code)).

## Before you write code

- **For a fix**, open the pull request directly, or an issue first if the cause is unclear.
- **For a feature or a change of design**, open an issue first, so that we agree on the approach before you spend time
  on it. D3B follows a [roadmap](docs/roadmap.md), and each choice is recorded with its reasons in
  [docs/decisions.md](docs/decisions.md): read the entries about the area you touch, since they often explain why the
  obvious way was not taken.
- **Everything in the repository is in English**: code, comments, docs, commits and pull requests. Issues may be written
  in English or French.

## Set up

You need Windows 10 or 11, Git and the .NET 10 SDK pinned in [`global.json`](global.json). The official client, installed
by Ankama's launcher, is needed to run the bots and the data tools, not to build or test.

```powershell
git clone https://github.com/AerafalDev/D3B.git
cd D3B
dotnet build
dotnet test
```

To run the app from source, the host needs the installed client's pack, schema and mapping: the
[README](README.md#run) says how to fetch them. `dotnet tool restore` installs the repository's local tools,
`dotnet-ef` and `vpk`, pinned in [`.config/dotnet-tools.json`](.config/dotnet-tools.json).

## How the code is organized

[docs/architecture.md](docs/architecture.md) describes every project and module; in short:

- **Every feature is a module** (`IModule.Build(BotBuilder)`) that registers its services, activities, settings and
  pages. A new feature is a new module or grows an existing one; it does not reach into another module's internals.
- **`src/D3B.Engine` is pure**: pathfinding, the rules of a fight, its effects and the AI's search, with no I/O, so that
  they are tested and benchmarked on data alone.
- **`scripts/` holds the tools**, file-based apps that read the official client. They follow their own conventions,
  below, and have no tests.
- **Tests mirror `src/`**: one test project per project with logic, in `tests/`.

## Conventions

The build enforces most of these: warnings are errors, the analyzers run in the build, and CI checks the formatting.

### C#

These apply to `src/`, `tests/`, `benchmarks/` and `scripts/`.

- **One type per file, and one folder per namespace.**
- **Block bodies** for methods, constructors, operators and local functions, never `=>`. Properties, indexers and
  accessors may use `=>`. Enforced by IDE0021 to IDE0024 and IDE0061.
- **Member order**: constants, static fields, fields, static properties, properties and indexers, events, static
  constructor, constructors, static methods, methods, operators, nested types.
- **No primary constructors.**
- **XML documentation** on public types and members of `src/`: the build requires it.
- **Comments say why** a constraint or an invariant exists, never what the code does.
- **Test methods are PascalCase sentences** without underscores, such as `AModuleTypeIsBuiltOnce`. Enforced by CA1707.
  Tests use xUnit v3 on Microsoft.Testing.Platform, and CsCheck where a property holds over many inputs.

### Build and configuration files

`.csproj`, `.props`, `.targets` and `global.json` carry no comments; their rationale goes in the docs. Package versions
live in `Directory.Packages.props` (central package management), never in a project.

### Tools in `scripts/`

- A tool is a file-based app whose entry file starts with `#!/usr/bin/env dotnet`.
- Its code is pulled in with `#:include` from a lowercase folder of its name; what several tools share lives in
  `scripts/unity/`.
- `#:package` names no version, since central package management sets it. Add `#:property PublishAot=false` when a
  package is not AOT-compatible.
- Every script file is listed in `D3B.slnx`, so that it shows in the IDE, and CI builds each tool.

### Files

UTF-8 and LF line endings, as `.editorconfig` and `.gitattributes` set them.

## Checks before a pull request

Run these from the repository's root; CI runs the same on Windows and must be green:

```powershell
dotnet build                          # warnings are errors
dotnet test                           # every test project
dotnet format --verify-no-changes     # the formatting CI checks
dotnet build scripts/<tool>.cs        # each tool you changed
```

- **Changes to `src/` come with their tests.** A bug fix comes with a test that failed before it.
- **Behavior that only shows in the game** cannot be fully tested: say in the pull request how you checked it, on which
  client build, and what you saw. A measurement on your own fights or harvests, through `scripts/fights.cs` or the
  app's journal, is worth more than an argument.
- **Performance** changes come with numbers from the benchmarks:
  `dotnet run -c Release --project benchmarks/D3B.Engine.Benchmarks` or `benchmarks/D3B.Network.Benchmarks`.
- **Docs follow the code.** A change of behavior updates the README or `docs/` where they describe it. A change of
  direction is recorded as a new entry at the end of `docs/decisions.md`: entries are never rewritten, a later one
  supersedes an earlier one. Propose it in your pull request; the maintainer may renumber it.

## Commits and pull requests

- Branch from `main`, one topic per pull request. A large change lands in reviewable steps rather than one diff.
- Commit messages follow the history's style: a lowercase type, then what the commit does, such as
  `fix: keep the bot's path when the server moves the character` or `docs: describe the fight's options`. The types
  used are `feat`, `fix`, `docs`, `ci` and `chore`.
- Fill in the pull request template: what changed, why, and how you checked it.
- The maintainer reviews and merges. Contributions are released under the [MIT license](LICENSE), like the rest of D3B.

## Reporting a bug or asking for a feature

Use the [issue forms](https://github.com/AerafalDev/D3B/issues/new/choose). For a bug, give D3B's version and the
client build, both on the app's settings page, what you did, what you expected and what happened, and the relevant lines
of the bot's journal from the logs page. Leave out anything that names your account if you prefer: it is never needed.
