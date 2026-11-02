# Decisions

Product and architecture decisions, oldest first. Each entry states what was decided, why, and what was rejected. An
entry is never rewritten: a later entry supersedes it, and the old one is marked as superseded. Questions still open
are listed at the end, with the [roadmap](roadmap.md) step that settles them.

Decisions D1 to D18 were taken on 2026-10-27, one by one, after studying the references in `vendor/` and the bots
sold for Dofus 3. From D19 on, each entry names the roadmap step that took it; an entry the owner chose says so, the
others are local choices of the lead session.

## Brief

Set by the project owner, not up for decision:

- An open-source bot for Dofus 3 (Unity), in .NET 10.
- Better than the existing bots, open source or sold: inspired by them, never copied.
- Every feature is a module, assembled as in Bevy, in the repository (D116, the owner's change: the brief's external
  .NET plugins, then the Lua scripts of D102 that replaced them, go).
- Mods are made in blueprint style, graphs of nodes edited in the app, after the fights of step 9 (D117, D169, the
  owner's changes).
- The desktop UI is Avalonia with ShadUI.
- The network goes through Kestrel's socket layer.
- Game data is extracted from the official client by file-based apps in `scripts/`, as in Dofus Remastered.
- Fights take their ideas from Bubble emu and BubbleBot, and do everything better.
- Every new large task starts in a new session, closed with a prompt for the next one.

## D1: .NET 10 on Windows

*2026-10-27*

D3B is written in C# on .NET 10 and runs on Windows only.

- **Why.** One runtime for the bot, its tools and its UI. The Unity readers of Dofus Remastered (MIT, same owner) and
  the Cpp2IL and AssetsTools.NET libraries run in process, where BillBot shells out to C# tools from Python. Source
  generators cover the protocol and data code. Async I/O carries many accounts in one process. The official client and
  its launcher are Windows programs.
- **Rejected.**
  - Python, continuing BillBot: its core depends on PyQt6 (11 files of `src/core`), it runs one thread per timer, and
    it has no license.
  - A Linux headless host: no need yet. The host stays free of Windows UI code, so it remains possible.

## D2: What D3B never contains

*2026-10-27*

D3B never contains, in its code, its default modules or its documentation:

- evasion of Ankama's detection: simulated mistakes, timings shaped to avoid bans, forged hardware identifiers;
- mass account creation;
- subscription or payment automation;
- disabling or bypassing Ankama Shield;
- one proxy per account to hide multi-accounting.

- **Why.** These parts exist to defeat Ankama's protections or to run farms at scale, not to play. The references have
  them (BillBot's `fake_bad_*` behaviors and `human_timings`, BubbleBot's `HardwareService`, `AccountCreation*`,
  `Subscribe*`, `ShieldDisable`, `ProxyAssociate`): they are not studied and not reproduced.
- Using any bot on the official servers breaches Ankama's terms of service and can get the account banned. D3B says
  so plainly and does not hide it.

## D3: The references in `vendor/` give ideas, never code

*2026-10-27*

`vendor/` is ignored by Git. What each reference is for:

| Reference | License | Use |
| --- | --- | --- |
| `BillBot-master` (Python) | None | Ideas: clean protobuf classes behind a translation layer, the mapping toolkit, the version gate, immutable state snapshots, the debug recorder. Code the owner wrote may be ported. |
| `Bubble.D3.Bot-main` (.NET 9) | GPL-3.0 | Ideas: datacenter reading from the installed client, generated readers, handlers by domain, fight AI by simulation, treasure hunt clue solving. |
| `bubble-emu-v2-develop` (.NET 8) | GPL-3.0 | Ideas for fights. It is a Dofus 2 emulator: its formulas are checked against Dofus 3 before use. |
| `JondoEmu-main` (.NET 10) | None | Ideas: protocol rebuilt from the Cpp2IL dump, anchors named from real captures, structural matching across builds. |
| `dofus-unity-proto-main` | None | Reference for message shapes. |
| `Dofus` (Dofus Remastered) | MIT, same owner | Unity readers copied into `scripts/` (D10), conventions, the Bevy-like plugin model. |
| `metiers` | The owner's | Lua scripts the owner wrote for Snowbot, levelling each profession from 1 to 200. They are the acceptance test of the Lua API (D12), measured in D104. |
| `moonbot` | None | The documentation of MoonBot's Lua API, of Snowbot's family: ideas for the scripts' API, added in D104. |

- **Why.** Copying GPL code would make D3B GPL (D4). Code with no license cannot be reused at all. BubbleBot's damage
  calculator is a port of Ankama's own client code. Reading for ideas and rewriting keeps D3B clean and lets it fix
  their faults: BubbleBot's shared mutable pathfinder and data races, BillBot's silently dropped fields.

## D4: MIT license, private repository until the first milestone

*2026-10-27*

D3B is published under the MIT license, as Dofus Remastered and Palforge. The repository stays private until the first
milestone (D16).

- **Rejected.**
  - GPL-3.0: it would allow BubbleBot's code, which D3 already rules out.

## D5: Modules assembled as in Bevy, for composition only

*2026-10-27*

A bot application is assembled from modules. A module implements `Build(BotBuilder)` and registers what it brings:
services, message handlers, activities, fight strategies, Lua API namespaces, settings and UI pages. A module may
require other modules; adding a module type twice builds it once. A default group gathers the standard modules
(network, protocol, data, world, movement, harvesting, bank). A plugin is an assembly that contains modules (D6).

The model applies to composition only. At run time each bot is an async actor (D9), not a world of systems run in
phases. `BotBuilder` sits on the .NET Generic Host: the host is a long-running service whose lifetime, configuration,
logging and dependency injection the Generic Host already covers.

- **Rejected.**
  - A Bevy-like run time, with resources, events and systems per bot: a bot reacts to messages and timers, not to
    frames, and async workflows say that more directly.
  - Reusing the core of `Dofus.Framework`: it would first have to be split from SDL in Dofus Remastered, and the two
    projects would be tied together.

## D6: External plugins load once, never unload

*2026-10-27. Superseded by D102: Lua scripts replace the external plugins.*

An external plugin is a folder under `plugins/`. Each loads into its own `AssemblyLoadContext`, which shares the host's
assemblies, as Palforge's `PluginLoadContext`, but is not collectible. A plugin declares its identity with an
attribute. A `dotnet new` template creates one.

- **Why.** Unloading constrains every type a plugin touches and is rarely needed: updating a plugin restarts the host.
- **Rejected.**
  - Collectible contexts, as in Palforge.
  - Modules compiled into the application only: nobody could add one from outside.
  - NuGet packages or a catalogue in the UI: more work than a folder needs at this stage.

## D7: A headless host runs the bots, the desktop app is its client

*2026-10-27. Superseded by D91: the bots run in the app's process, and `D3B.Host` runs them without the app.*

The bots run in a headless host. The Avalonia app connects to it over gRPC on a named pipe, served by Kestrel.

- **Why.** A crash or a restart of the UI does not stop the bots. A remote front stays possible later. Kestrel is
  already in the stack (D8).
- The transport, gRPC on a named pipe, is a local and reversible choice.
- **Rejected.**
  - Everything in one process: simpler at first, but the UI's faults would take the bots down.

## D8: The network goes through Kestrel's socket layer, socket mode first

*2026-10-27. Superseded in part by D23: there is no socket mode, only MITM.*

Outgoing connections use `SocketConnectionContextFactory.Create(Socket)`, Kestrel's public API since .NET 7, on a
socket connected by the bot. The MITM mode listens with Kestrel and a connection handler. Both give a
`ConnectionContext`, and the codec only sees its `IDuplexPipe`. Socket mode, with no game client, comes first. The MITM
mode comes later, mainly to capture sessions and debug.

- **Why.** Kestrel's pipes, memory pool and scheduling, with no copy of its internals. One abstraction for both modes.
  An in-memory `Pipe` replaces the network in tests and replays.
- The network project references the `Microsoft.AspNetCore.App` framework. No other project needs it.
- **Rejected.**
  - A copy of Kestrel's internal socket classes, as in BubbleBot (`Bubble.Core/Network/Transport/Sockets`).
  - `NetworkStream` with pipes on top: workable, but no better than what Kestrel already exposes.

## D9: Each bot is an async actor

*2026-10-27*

Each bot owns one inbox (`Channel<T>`) and processes it on one logical thread, so its state needs no locks. Every
delay goes through `TimeProvider`, so tests control time. Behaviors are async methods taking a `CancellationToken`:
an interruption, such as a fight starting, cancels the current behavior, and a recovery step runs before it resumes.
Logic code reads immutable snapshots of the state, an idea from BillBot.

- **Rejected.**
  - A thread per timer with callbacks, as in BillBot.
  - Mutable flags and an if-chain, as in BubbleBot (about 70 flags in `GameRuntimeState`).
  - Singletons shared between bots: BubbleBot's world pathfinder keeps the search in fields with no lock.

## D10: Game data comes from file-based apps, and ships as packs

*2026-10-27*

Data is extracted from the official client by file-based apps in `scripts/`, with Dofus Remastered's conventions:
they have no tests, and shared code is pulled in through `#:include`. The Unity readers of Dofus Remastered (on
AssetsTools.NET) are copied there, then evolve on their own. The output is packs: MemoryPack serialization,
compressed with zstd. Packs are published as release artifacts, never committed. The scripts stay usable by anyone who
prefers to extract from their own client.

- **Why.** One extraction per client build, by the maintainer, instead of one per user.
- **Risk, accepted by the owner.** Distributing data derived from the client is a legal risk.
- **Rejected.**
  - Each user extracting from their own client only: safer, but every user needs the tools and runs them at every
    update.
  - A library shared with Dofus Remastered: it would tie the release cycles of both projects.
  - JSON in Git LFS (BillBot) or a committed `maps.json` (BubbleBot).

## D11: Bot code sees clean protobuf classes, translated per build by our own pipeline

*2026-10-27*

Messages are C# classes generated from readable `.proto` files, with Google.Protobuf and Grpc.Tools. A mapping per
client build translates the obfuscated wire into these classes and back. A field that is not mapped fails loudly. The
host refuses to start when its mapping does not match the installed client's build.

The mapping comes from our own pipeline: a Cpp2IL dump of the client, the IDA Pro tracer (the owner has IDA), scored
matching, pinned pairs that are versioned, and a contract audit that fails the build. The readable `.proto` files and
the pinned pairs are committed. The mapping of each build ships with that build's data pack (D10). The pipeline takes
its ideas from BillBot (scored matching, pins, audit, version gate, update watcher) and from JondoEmu (anchors from
real captures, structural matching across builds).

- **Rejected.**
  - protobuf-net classes with obfuscated field names in the bot code, as in BubbleBot.
  - Starting from `dofus-unity-proto`: no license.
  - BillBot's translator, which drops unmapped fields silently, and its pins, which each update wipes.

## D12: Lua for routes and routines, .NET plugins for features

*2026-10-27. Superseded by D90: routes and routines are .NET plugins too, and there is no Lua.*

Users write routes and routines in Lua. The API is in the family of the Dofus Retro bots and Snowbot: namespaces
called with `:` (`global`, `map`, `inventory`, `npc`, `job`, `exchange`, `character`), and a script defines `move()`
and `bank()`, which the bot calls. Every commercial Dofus 3 bot studied offers this. The owner's profession scripts
(`vendor/metiers`) must run once adapted to it: that is the API's acceptance test, at the end of the roadmap.
Features are .NET plugins (D6). The Lua engine is LuaCSharp: pure C#, with async calls, so `move()` awaits without
blocking. A prototype confirms it first (Q1).

- **Rejected.**
  - Plugins only: users would have to code and compile.
  - Profiles in the UI only: too limited.
  - MoonSharp, no longer maintained, and NLua, native and without async calls.

## D13: Fights run on a simulator of our own

*2026-10-27*

Fights are decided by simulating candidate actions and scoring them. The idea comes from BubbleBot, which scores cells
per spell. The simulator is written from the spell effects in the game data, not ported from Ankama's code or from GPL
code. Bubble emu informs the mechanics: its formulas are Dofus 2, so each is checked against Dofus 3 (Q5). Fight
strategies, per class or per build, are modules.

- **Rejected.**
  - A greedy choice of the best-weighted action, as in BillBot, which detects life steal through the French word
    "vol" in the effect text.

## D14: Avalonia and ShadUI, in French and English

*2026-10-27*

The desktop app uses Avalonia 12, ShadUI and CommunityToolkit.Mvvm. Views stay thin, so ShadUI, still at 0.x, can
change without rewriting them. Translation is built in from the start, in French and English. The pages of the first
version: accounts and profiles, the live map with the bot's state, and logs with a protocol inspector.

- **Rejected.**
  - A web dashboard: it would allow control from a phone, but the owner chose a desktop app. The host (D7) keeps a web
    front possible.

## D15: The rest of the stack

*2026-10-27. Superseded in part by D23: D3B does not call Haapi, so the "HTTP (Haapi authentication)" row no longer
applies. The "Secrets" row waits for a secret to keep (D100). The "Scripts" row holds since D103. The "Distribution" row
is built by D171.*

Versions are the latest stable on NuGet on 2026-10-27, unless the line says otherwise.

| Area | Choice | Why |
| --- | --- | --- |
| Host | Microsoft.Extensions.Hosting | Lifetime, configuration, logging and dependency injection for a long-running service. |
| HTTP (Haapi authentication) | `IHttpClientFactory`, Microsoft.Extensions.Http.Resilience 10.11.0 | Standard retries and timeouts. |
| Protobuf | Google.Protobuf 3.36.2, Grpc.Tools 2.85.0 | Generated at build from `.proto` files; the descriptors give the per-build translation its tables. |
| IL2CPP dump | Samboy063.Cpp2IL.Core 2022.1.0-pre-release.21 | In process. The prerelease JondoEmu uses: the last stable release dates from 2022. |
| Unity data | AssetsTools.NET 3.0.5 | The version Dofus Remastered uses. |
| Data packs | MemoryPack 1.21.4, ZstdSharp.Port 0.8.8 | Generated binary serialization; ZstdSharp is already in Dofus Remastered. |
| Pathfinding | `PriorityQueue`, our own code | No library needed. |
| Scripts | LuaCSharp 0.5.8 | D12. |
| Storage | Microsoft.EntityFrameworkCore.Sqlite 10.0.14 | Accounts, profiles and statistics, with migrations. |
| Secrets | ASP.NET Core Data Protection | Already in the framework the network references; DPAPI on Windows. |
| Logs | Serilog.Extensions.Hosting 10.0.0, Serilog.Sinks.File 7.0.0 | Structured, with the bot's id as a property; an in-memory sink feeds the UI. |
| UI | Avalonia 12.1.4, ShadUI 0.2.4, CommunityToolkit.Mvvm 8.4.2 | D14. |
| Map view | An Avalonia control drawing with `DrawingContext` | Enough for cells, entities and paths. |
| Notifications | Discord webhooks through `HttpClient` | One POST, no library. |
| Tests | xunit.v3 4.0.2, CsCheck 5.0.0, Verify.XunitV3 33.3.4, Microsoft.Extensions.TimeProvider.Testing 10.11.0 | As in Dofus Remastered, with property tests for pathfinding and line of sight, and snapshots of replayed sessions. |
| Benchmarks | BenchmarkDotNet 0.15.8 | Codec and pathfinding. |
| Build | Central package management, `Directory.Build.props`, warnings as errors, MinVer 8.0.0, GitHub Actions on Windows | Dofus Remastered's conventions. |
| Distribution | Velopack 1.2.161 | Installer and updates, later. |

No Native AOT: EF Core and the Lua engine do not suit it, and a bot does not need it.

## D16: The first milestone is harvesting and banking

*2026-10-27. Superseded in part by D23: the account plays through its official client and D3B's MITM, not in socket
mode.*

The first milestone: one account, in socket mode, harvests and banks its resources, controlled from the desktop app's
first pages (D14). The repository may go public once it is reached (D4).

- **Rejected.**
  - Fights first: they need the simulator (D13), a larger step.
  - Treasure hunts first: they need clue data and more of the world.

## D17: One large task per session, documents in Markdown

*2026-10-27*

The working agreement of Dofus Remastered applies, as written in `CLAUDE.md`: one roadmap step per session, one branch
and one pull request per session, a task is done with its tests, and each session closes with a report and a prompt
for the next one. Documents are Markdown files in `docs/`.

- **Rejected.**
  - A documentation site, as Palforge's: premature before there is anything to document for users.

## D18: The project is named D3B

*2026-10-27*

The project, its solution and its namespaces are named D3B (`D3B.*`).

## D19: The solution's layout and build

*2026-10-27, step 1. Superseded in part by D109: `samples/` holds example scripts, and no plugin.*

The projects of the [architecture](architecture.md#projects) are confirmed, in `D3B.slnx`. They target `net10.0`, not
`net10.0-windows`: nothing needs a Windows API yet, and D1 keeps a Linux host possible. The build follows Dofus
Remastered: SDK pinned in `global.json`, central package management with transitive pinning, MinVer on every project,
output under `artifacts/`, warnings as errors, `AnalysisMode` Recommended, code style enforced in the build, XML
documentation on public APIs.

- Projects with no code yet are empty class libraries. `D3B.App` stays one until step 5 makes it the Avalonia app.
  Project references are added by the step that needs them; for now only `D3B.Bot` and `D3B.Host` reference
  `D3B.Core`.
- `DefaultModules` lives in `D3B.Bot`, which will reference the projects holding the standard modules. It is empty
  until those modules exist.
- `samples/` holds example plugins, built with the solution and loaded by the tests.
- `.editorconfig` makes the C# style of `CLAUDE.md` fail the build: block bodies (IDE0021 to IDE0024, IDE0061) and
  test names without underscores (CA1707). The member order and the ban on primary constructors have no analyzer and
  stay a review rule.
- Added to D15: Microsoft.Extensions.Hosting 10.0.14 for `D3B.Core`, and Microsoft.Extensions.Diagnostics.Testing
  10.11.0, whose fake logger lets a test see what a plugin logs.
- **Rejected.**
  - `net10.0-windows` everywhere, as Palforge: it would tie the host to Windows for nothing.
  - An empty Avalonia app now: its packages and entry point belong to step 5.

## D20: How modules compose

*2026-10-27, step 1*

`BotBuilder` wraps the Generic Host's `HostApplicationBuilder` and is itself an `IHostApplicationBuilder`, so modules
register into the host's services, configuration and logging, and the host's extension methods apply. A module is
built as soon as it is added, as in Bevy and Dofus Remastered's `AppBuilder`:

- A module requires another by calling `bot.AddModule<T>()` at the start of its `Build`. The required module is then
  built first. `BotBuilder.Modules` lists modules in the order they finished building, so each comes after the modules
  it requires.
- Adding a module type twice builds it once. A module counts as added before its `Build` runs, so a requirement that
  leads back to a module being built does nothing, instead of failing: a module may add companions that require it.
- A group (`IModuleGroup`) lists modules in order. Whoever adds it may disable some of them first; disabling a module
  the group does not hold fails. A disabled module is still built when another module requires it.
- Nothing is registered after `Build`.
- **Rejected.**
  - Declared requirements (`[Requires(typeof(T))]`) with a topological sort before building: order independence and a
    readable graph, but more code and a departure from D5's model, for no need yet.
  - Failing on a requirement cycle: it would reject a module adding companions that require it.
  - Replacing a default module by another type: no plugin needs it yet. When one does, the replacement should claim
    the replaced module's type, so that requirements on it are satisfied.

## D21: The plugin contract

*2026-10-27, step 1. Superseded by D102 for external plugins.*

`bot.AddPlugins(directory)` loads the plugins under a directory. The host passes `plugins/` next to its executable,
or the `Plugins:Directory` setting, resolved from the executable's directory.

- A plugin is a folder holding an assembly named after the folder, with its dependencies and `.deps.json`. Folders
  load in the order of their names.
- The assembly holds exactly one public, concrete module class with a public parameterless constructor, marked
  `[Plugin("id")]`, with an optional name and author. That root module is added to the builder and adds the rest of
  the plugin. The plugin's version is its assembly's informational version.
- Each plugin loads once into its own `AssemblyLoadContext`, not collectible, which asks the host's context first and
  the plugin's `.deps.json` second: a plugin and the host share one D3B.Core and one framework.
- A `PluginInfo` per plugin is registered in the services, for the host's log and later the app.
- Any invalid folder, or two plugins with the same id, throws `PluginLoadException` and the host does not start. A bot
  missing a feature it was given could misbehave; a clear error at start is cheaper.
- **Rejected.**
  - An assembly-level identity attribute, or adding every module of the assembly: the root module says what the plugin
    brings, in code.
  - Skipping an invalid plugin with a warning, as Palforge does.

## D22: Plugins compile against the D3B.Core package

*2026-10-27, step 1, chosen by the owner. Superseded in part by D95: `D3B.Bot` and the libraries it names are packed
beside `D3B.Core`, and a plugin references `D3B.Bot`.*

`D3B.Core` is the only packable project, versioned by MinVer. A plugin references the `D3B.Core` package with
`ExcludeAssets="runtime"`: it compiles against it, and the host provides it at run time, so a plugin's folder holds
only its own files. The `dotnet new d3b-plugin` template in `templates/d3b-plugin` does this, takes the version to
compile against as a required parameter, and can deploy every build to a host's plugins folder.

- Until the repository goes public (D4), the package is packed into a local folder. Publishing it, and the template as
  a package, comes with the first milestone.
- CI packs D3B.Core, installs the template, creates a plugin from it and builds it against the packed package.
- **Rejected.**
  - Referencing `D3B.Core.dll` from an installed host: no feed to run, but compiling a plugin, in CI too, would need
    an installed host, and nothing would state which version a plugin targets.

## D23: MITM only, no socket mode

*2026-10-27, during step 1, chosen by the owner*

D3B never connects to Ankama's servers on its own. Every bot plays through an official client: D3B sits between the
client and the game server, with a Kestrel listener and a connection handler on the client's side, its own
connection to the server on the other, and the same codec over `IDuplexPipe` on both. This supersedes D8's socket mode
and the "in socket mode" of D16. The rest of D8 stands: Kestrel's transport, `ConnectionContext`, and an in-memory
`Pipe` in tests and replays. Roadmap step 8, the MITM mode, merges into step 3.

- **Consequences.**
  - The official client logs in, through Haapi and the login handshake. D3B handles neither, which settles Q3, and
    the Haapi row of D15 no longer applies.
  - D3B never imitates the client, so nothing it does has to get past the client's integrity checks (D2).
  - Every account needs its own running client. The memory and CPU of one client per account bound how many accounts
    a machine runs (step 10).
  - The bot acts through the client's connection, and the client must stay consistent with what the bot does: Q9.
  - How each client's connection reaches D3B is Q8.
- **Rejected.**
  - Socket mode first, MITM later (D8): a bot without a client is lighter, but it must reimplement the login, which
    may touch the client's integrity checks (Q3).

## D24: Packs ship as releases of the D3B repository, one release per client build

*2026-10-27, step 2, chosen by the owner. Settles Q4. Superseded in part by D172: the host no longer downloads; the
releases are where the app's workflow takes a build's files to ship them.*

Data packs, and from step 3 the protocol mappings (D11), are assets of the D3B repository's GitHub releases.

- **Identity.** A client build is named by the `Version` line of `Dofus_Data/StreamingAssets/version`, such as
  `3.7.12.12`: every build measured so far has its own. `ClientBuild`, in `D3B.Data`, reads it and accepts only numbers
  separated by dots, since the version ends up in paths and URLs.
- **Releases.** One release per client build, tagged `client-<version>`. Its assets: the pack `data-<format>.pack`, the
  protocol mapping from step 3, and their SHA-256. The pack format is in the file name, so that a D3B reading a newer
  format adds its pack to the same release while older hosts keep theirs. MinVer only reads tags starting with `v`, so
  these tags leave D3B's versions alone.
- **Fingerprint.** A pack records the SHA-256 of the client files it was read from, through their SHA1 in the
  launcher's `manifest.json`. A published pack is never replaced by one with another fingerprint under the same
  version: should Ankama change content without changing the version, the difference shows, and the identity then
  grows to include the fingerprint.
- **Fetching**, built in session 2d. The host reads the installed client's version and looks for the pack under
  `%LOCALAPPDATA%\D3B\data\<version>\`, where the extraction tool writes by default, so a pack extracted locally is
  used first and works offline. Otherwise it downloads the release asset, checks its SHA-256, then moves it into place.
  With no pack for the installed build, the host does not start, and says which build is missing and how to extract
  it. While the repository is private (D4), a download needs a GitHub token; only the owner runs D3B until then.
- **Publishing.** The maintainer extracts with `scripts/extract.cs` and creates the release. CI cannot: it has no
  client.
- **Risk, accepted by the owner.** Ankama has filed DMCA notices on GitHub before (2018, against forks of TouchEmu). A
  notice against the packs would target this repository, its code included. The scripts keep working to extract
  locally, whatever happens to the releases.
- **Rejected.**
  - A separate repository for the packs, which the lead session recommended: a notice would reach only the data. The
    owner prefers one repository.
  - Object storage (S3, R2): an account, keys and costs to manage, for the same risk elsewhere.
  - Falling back to the pack of another build: the mapping must match its build exactly (D11), and data that differs
    could make a bot misbehave.

## D25: The pack format

*2026-10-27, step 2. Superseded in part by D28: entries are compressed several at once, still deterministically, and
there is no zstd dictionary.*

A pack is one file, written by `PackWriter` and read by `GamePack`, both in `D3B.Data`: the extraction tool references
`D3B.Data` through `#:project`, so the format has one definition.

- **Layout.** A header (the magic `D3B-PACK` and the format version), the entries, an index, then a trailer (the
  index's offset and lengths, and the magic again). The index comes last, so the writer streams entries; a pack whose
  writing was cut short has no trailer and is rejected. The format version comes first, so a pack of another format is
  reported as such rather than as damaged.
- **Entries.** Each has a name, such as `texts/fr`, and is one MemoryPack payload compressed as its own zstd frame with
  zstd's checksum: a damaged entry fails when it is read, alone. The index records the type each entry was written as,
  and a read must ask for that type. MemoryPack's default layout is not version tolerant: `GamePack.FormatVersion`
  changes whenever an entry type does, and a host reads only its own format.
- **Lazy reading.** Opening a pack reads its index only: about 60 ms for the 11 MB pack of session 2a. `GamePack.Read`
  reads one entry with positional reads, safe from any number of threads, and keeps nothing. `PackCache` keeps the
  entries last read within a budget in bytes, weighed by their decompressed length, and drops the least recently used:
  maps go through it from session 2b. A whole table, such as a language's texts, is read once and kept by its store.
- **Determinism.** zstd on one thread at level 19, and MemoryPack in declaration order: one client and one extractor
  give the same bytes, checked on client 3.7.12.12. A pack can be compared with another by its hash.
- **Texts.** One `TextTable` per language, French and English: identifiers sorted in one array and found by binary
  search, texts in another. For now every numbered text, 339,912 per language and 11.4 MB for both; session 2c keeps
  only those the extracted tables name. The keyed texts, the client's interface, are left out.
- **Rejected.**
  - A directory of files per pack: more downloads and hashes, where one file is one release asset and one SHA-256.
  - MemoryPack's version-tolerant mode: packs are per build and per format, so tolerance would only slow reading.
  - SQLite: queries D3B does not need, for a format that is read whole or by key.
  - A zstd dictionary trained on the entries: it may pay for many small maps; session 2b measures it.

## D26: The data tools

*2026-10-27, step 2*

- `scripts/unity/` holds what several tools share, copied from Dofus Remastered and now evolving on their own:
  `ClientInstall`, `UnityBundle`, `I18nFile` and `LauncherManifest`, which now hashes the files its manifest lacks,
  since a fingerprint must follow content. Dofus Remastered's typed readers (`UnityReader`, `UnityTypeTree`,
  `UnityTypeNode`, `UnityFields`) come with the map reader in session 2b, their first user, so that no copied file sits
  unbuilt.
- `scripts/inspect.cs` is Dofus Remastered's inspection tool without its texture and shader commands, which a bot does
  not need: `summary`, `bundles`, `types`, `objects`, `dump`, `layout` and `texts`.
- `scripts/extract.cs` writes the pack of the installed client's build to `<output>/<version>/data-<format>.pack`, by
  default where the host looks first (D24). It does nothing when the pack there has the same fingerprint and the same
  extractor version, and writes beside the pack then moves it, so a pack is never half written under its name.
- CI builds both tools, as Dofus Remastered's does, so that they keep compiling.
- Added to D15: System.CommandLine 2.0.14, the latest stable, for the tools' command lines.
- `docs/client.md` records what the client holds that D3B uses: layouts and counts, measured, never content.

## D27: Maps and the world in the pack, format 2

*2026-10-27, step 2, session 2b*

A pack now holds, besides the texts, every map, the client's world graph, and an atlas of map positions, sub-areas,
areas and zaaps, measured in [client.md](client.md). `GamePack.FormatVersion` is 2.

- **Maps.** One entry per map, `maps/<id>`: a `MapData` with its four neighbours, its 560 cells and its interactive
  elements, each an identifier and a cell. Both of the client's layouts are read, `ClientMapData` and `MapMetadata`,
  17,722 maps; graphics, materials, effects and sounds are left out. `GameMaps` reads a map when first asked for, through
  a `PackCache` of 16 MB by default.
- **Cells** are a packed unmanaged struct of 9 bytes, `MapCell`: the client's booleans as `MapCellProperties`, and its
  numbers sized after the ranges measured. MemoryPack copies an array of them as raw bytes, so a map is about 5 KB and
  reads in microseconds. The constructor rejects a value that does not fit, so a client widening a field fails the
  extraction instead of being misread. Fields whose meaning is not measured, such as `moveZone` and `arrow`, are kept
  as they are.
- **Zones.** A cell's zone is the high four bits of its `linkedZone` (`MapCell.Zone`), the hypothesis of BillBot and
  BubbleBot, confirmed: every vertex's zone is that of walkable cells of its map, and on 10,795 of the 10,981 maps the
  two sets of zones are equal; on the others, cells hold zones with no way out, which the graph leaves out.
- **Interactive elements** are keyed by map and identifier: an identifier is unique within a map only. An element's
  cell is its own, or that of the middleground group listing it; the few drawn without a cell keep
  `MapInteractive.NoCell`.
- **World graph.** One entry, `world/graph`, read once by `GameWorld`: vertices sorted by map and zone, edges by source
  and destination, transitions contiguous per edge, criteria in one table, all packed unmanaged structs except the
  criteria. Transition types are the four measured, `Scroll`, `ScrollAction`, `MapAction` and `Interactive`; the
  extractor fails on another type, or on a skill or element where one was never measured, rather than guess. Criteria
  stay text until step 4 parses them.
- **Atlas.** One entry, `world/atlas`, read once: positions, sub-areas (what travel needs: area, level, associated zaap,
  access without a subscription), areas and zaaps, each table sorted by identifier. `m_flags` is kept raw: its bits are
  not measured.
- **Checks.** The extractor reads and checks everything before it writes: every vertex's zone on a walkable cell of
  its map, every interactive transition's element on its map, every position's map held, every zaap on the graph.
- **Format test.** `PackFormatTests` hashes what defines the format: the header and trailer, and, for every entry type,
  its name and the bytes MemoryPack writes for a sample whose every field has its own value. It fails when that hash is
  not the one recorded for `GamePack.FormatVersion`; a format's line is never edited once a pack of it is published.
- `scripts/unity/` gains Dofus Remastered's typed readers, as D26 planned, with what maps and tables need: 8, 16 and
  64-bit integers, integer arrays, counts checked against the bytes left, fields named by their property when the client
  serializes a backing field (`<interactiveId>k__BackingField`), a shared reader of managed reference registries, and
  `UnityDataTable` for `DataRoot` tables.
- **Rejected.**
  - One entry for all maps: a host would read 90 MB to use a few maps.
  - Cells as a struct of arrays: 1.4% smaller compressed, for a clumsier API.
  - Transitions as classes holding their criterion: 31,329 objects to build, where a struct array is copied.
  - Leaving out the `MapMetadata` maps, on no map of the graph: they cost 1,739 small entries, and a bot may still
    stand on one.
  - Keeping `MapScrollActionsDataRoot` and the arrow cell lists: the graph's scroll actions already lead where the
    server sends, and nothing reads the lists.

## D28: Packs compressed at zstd 19, several entries at once, without a dictionary

*2026-10-27, step 2, session 2b; the dictionary chosen by the owner. Supersedes in part D25.*

Measured on the pack of 3.7.12.12 (client.md):

| zstd level | Entries compressed | One thread | 16 threads |
| ---: | ---: | ---: | ---: |
| 15 | 17.7 MB | 23.8 s | 6.2 s |
| 19 | 15.5 MB | 45.4 s | 10.3 s |
| 22 | 15.2 MB | 56.4 s | 14.5 s |

- **Level.** 19 stays the default: 22 saves 2% for a quarter more time, 15 costs 14%.
- **Parallel compression.** `PackWriter` serializes each entry as it is added, compresses several at once on the thread
  pool, each as its own frame on one thread, and writes them in the order they were added. The bytes do not change: the
  parallel pack equals the sequential one but for its format number, and two extractions give the same SHA-256.
  Extraction takes about 25 s instead of 68 s.
- **No dictionary.** A 64 KiB zstd dictionary trained on the maps shrinks them from 3.72 MB to 3.29 MB with the
  dictionary, 2.8% of the pack, and decompression of every map from 62 to 34 ms. Not worth a dictionary entry, a field
  per entry and a training kept deterministic. The texts are three quarters of the pack; once session 2c keeps only the
  texts tables name, the maps weigh more, and the question can be measured again.
- **Rejected.**
  - zstd's own multithreading inside a frame: the gain is on the two text entries only, and its output would need its
    own proof of determinism.

## D29: Game tables in the pack, format 3

*2026-10-27, step 2, session 2c; what the pack keeps chosen by the owner*

A pack now holds, besides maps, the world graph and the atlas, what the first milestone needs of the client's tables,
measured in [client.md](client.md), and only the texts they name. `GamePack.FormatVersion` is 3.

- **What it keeps.** Only what a harvesting and banking bot uses:
  - `items/catalog` (`ItemCatalog`): every item's id, name, type, level and weight in pods, and every type's name,
    `superTypeId` and `categoryId`, kept raw since the client names neither.
  - `jobs/catalog` (`JobCatalog`): the jobs, and every skill's name, job, gathered item, minimum level, `range` and
    `useRangeInClient`.
  - `interactives/catalog` (`InteractiveCatalog`): every interactive's name.
  - `banks/catalog` (`BankCatalog`): the 10 banks, as the map in front of each, and the 8 bankers with the replies that
    open the bank.
  - `world/atlas`: each sub-area gains its resources, the items professions gather that its `harvestables` lists.
  - `texts/<language>`: 26,411 texts per language instead of 339,912; the pack shrinks from 15.6 MB to 4.7 MB.
- **What links them.** The client's data says neither which interactive offers which skill nor which interactive an
  element of a map is: the server tells, element by element. It links a profession's skill to its resource, and the
  resource to the sub-areas listing it. A bot looks for a resource by item, through these links, then uses the elements
  the server says offer the skill.
- **Banks.** The client marks neither banks nor bankers. The extractor takes a bank for a hint drawn with graphic 401,
  and a banker for an NPC with a reply reading `Consulter son coffre personnel` in French; it keeps the replies, so a
  bot needs no text at run time, in any language. Talking is NPC action 3, `Banker.TalkAction`.
- **Layout.** Tables are packed unmanaged structs sorted by id, and a row's list, such as a sub-area's resources or a
  banker's replies, is a slice of one array of the catalog, as transitions are of the graph's; `Tables` holds what the
  catalogs and the atlas share. `Item` keeps the client's widths, 13 bytes for 21,554 items; the small tables keep
  `int`. `GameTables` reads each catalog once, when first asked for.
- **Checks.** The extractor fails when a table contradicts what D3B relies on: a record keyed under another id, a skill
  of a job the client lacks, a profession's skill gathering an item the client lacks or an item of no known type, a
  harvestable that is no item, a reply that is not a pair, no bank, a bank off the graph, no banker, a banker who
  cannot be talked to, a named text missing in a language. What a bot does not rely on is counted and kept: one test
  item of a type the client lacks, skills of `Base` naming item 0 or a missing item, kept as gathering nothing.
- **Rejected.**
  - Every NPC's name and actions, 6,472, for the live map of step 5: the first milestone needs only the bankers. A step
    that needs NPCs adds them.
  - Every hint, 834, workshops and marketplaces among them: steps 11 and 12 add what they need.
  - Items' `m_flags`, kept raw as maps' are: no bit is measured, and nothing would read it.
  - Every harvestable: the loot boxes a sub-area lists are no resource to look for.
  - The bank's inside, found at extraction: from 5 of the 10 banks a door leads to 2 or 3 maps at the bank's
    coordinates, and which holds the banker only the server says.
  - Bankers and replies written in the extractor by id: a client adding a banker would go unnoticed; the text finds
    him, and the extraction fails if it finds none.
  - Choosing the chest reply by its text at run time, as BillBot does: a bot would depend on the client's language.
  - One entry for every table: a catalog per domain keeps each type small, at no cost, since all are read once.

## D30: Still no zstd dictionary

*2026-10-27, step 2, session 2c, chosen by the owner*

D28 left the dictionary to be measured again once the texts shrank. On the pack of format 3, 4.67 MB with maps 80% of
it, a 16 KiB dictionary trained on the maps, deterministic, saves 0.45 MB, 9.6% of the pack, and makes every map
decompress in 2.3 µs instead of 3.5; the other entries gain nothing (client.md). Packs stay without one.

- **Why.** 0.45 MB a download does not pay for an entry every map depends on, so that a damaged dictionary loses them
  all, a field per entry in the index, and a training whose output must stay deterministic across zstd's versions. A map
  is read once, so faster decompression shows nowhere.
- **Rejected.**
  - A 16 KiB dictionary trained on the maps, which the lead session measured and did not recommend.

## D31: The host gets the pack of the installed build before it starts

*2026-10-27, step 2, session 2d. Builds D24's fetching. Superseded in part by D172: the host finds the pack on this
machine, else among the app's own, and downloads nothing.*

- **Flow.** `BotHost.RunAsync` builds the host, then, before starting it, calls `PackSource.EnsureAsync`: it reads the
  installed client's build (`ClientBuild`, from `Client:Directory`, by default where the launcher installs it), looks
  for its pack under `Data:Directory` (by default `%LOCALAPPDATA%\D3B\data`, D24), and otherwise downloads it from the
  release `client-<version>` of `Releases:Repository` (by default `AerafalDev/D3B`). The host then opens the pack, so that
  an unreadable one stops it before any service reads it. Without a pack for the installed build, or with the pack of
  another build under its name, the host logs a critical message that names the build and the command extracting its
  pack, does not start, and exits with code 1.
- **Download.** `ReleaseDownloader`, in `D3B.Data.Releases`, goes through GitHub's REST API, which serves private
  repositories: the release by its tag, then each asset by its identifier with `Accept: application/octet-stream`. The
  asset's URL is built from its identifier, never taken from the answer, and the token is set on each request to the
  API; `HttpClient` clears it when it follows GitHub's redirect to the bytes. The SHA-256 asset is read first. The pack
  is streamed into `<pack>.download` and hashed as it comes. It must match the SHA-256, then open as a pack of this
  format extracted from this build, before it is moved into place; a file that appeared there meanwhile, such as one
  the extraction tool wrote, is kept. Only a whole, checked pack is ever under the name the host looks for (D24).
- **Failures.** Every one ends as the host's message and exit code 1, never as a crash. A read waiting 30 s for its bytes
  fails the download, since `HttpClient`'s own timeout ends with the headers. A lock beside the pack, `<pack>.lock`,
  deleted when closed, keeps two hosts from downloading it at once: the second stops and says so. Ctrl+C during the
  download ends the process before the host has started: only `<pack>.download` is left, and the next start downloads
  again.
- **The data module.** `DataModule`, first of `DefaultModules`, registers singletons that every bot shares:
  `ReleaseDownloader`, `PackSource`, `ClientBuild`, `GamePack`, `GameTexts`, `GameMaps`, `GameWorld` and `GameTables`.
  The pack is opened once, by a factory, so that the container owns it and closes it with the host; the stores are safe
  for any number of threads. Nothing is read or downloaded while the module builds: composition stays synchronous and
  offline (D20), and the host downloads asynchronously, with its logs, before it starts.
- **Where it lives.** The module is in `D3B.Bot`, with the default modules (architecture), so that `D3B.Data`, which the
  tools reference, stays free of the Generic Host. `ClientRelease`, in `D3B.Data`, names a build's release, its assets
  and their checksum for the host and for the release tool (D33): `<asset>.sha256` holds one line as `sha256sum` writes
  it, and `sha256sum -c` checks it.
- **Rejected.**
  - Downloading while the module builds: network access in composition, synchronous, before the host has a logger.
  - Downloading in a hosted service: the Generic Host creates every hosted service before starting any, so a service
    taking a store in its constructor would open the pack before it is downloaded.
  - `IHttpClientFactory`, with Microsoft.Extensions.Http: a package for one download per client update.
  - Checking GitHub's own digest of the asset instead of the `.sha256` asset: it proves the download matches the upload,
    not that the upload matches what the maintainer extracted. The release tool compares it (D33).

## D32: The download's token comes from the configuration, else from the GitHub CLI

*2026-10-27, step 2, session 2d, chosen by the owner. Superseded by D172: nothing downloads, so no token is read.*

While the repository is private (D4), a download needs a GitHub token. `ReleaseToken` takes `Releases:Token` when the
host's configuration sets it, such as the user environment variable `Releases__Token`, and otherwise asks the GitHub CLI
for its logged-in account's token, `gh auth token --hostname github.com`: the owner is already logged in, so there is
nothing to configure.

- The token is asked for only when a pack must be downloaded, is never logged, and goes to GitHub's API only (D31).
  Without either source, the download goes without one: a public repository accepts it, and for a private one the host's
  message says where a token comes from.
- **Risk.** The CLI's token has the `repo` scope, wider than reading releases; it leaves the machine for GitHub's API
  only. The host starts `gh` as Windows finds a program by its name: beside the host, in the current directory, then on
  `PATH`, a program of the user's own environment. It gets 10 s, and none of `GH_TOKEN` and `GITHUB_TOKEN`, which it
  would print instead of its account's token.
- Once the repository is public, no token is needed; one still raises GitHub's rate limit.
- **Rejected.**
  - The configuration alone, which the lead session recommended: one place and no process started, but a token to set by
    hand.
  - Windows' credential manager: native code for Windows only, while D15 plans Data Protection for secrets at step 5.
  - `GH_TOKEN` and `GITHUB_TOKEN`: shared with other tools, and possibly holding another context's token.

## D33: Publishing a client build's release

*2026-10-27, step 2, session 2d*

`scripts/release.cs` publishes the pack of a build, the installed client's by default or `--client-version`'s, from
where the extraction tool writes it (`--packs`), to the release `client-<version>` (D24).

- **Checks.** It opens the pack, which checks its format and that it is whole, checks it was extracted from that build,
  and writes its SHA-256 beside it, in `data-<format>.pack.sha256`. Then, by the release's state:
  - no release: `gh release create` with both assets, titled `Client <version>`, with notes naming the build, the
    format, the fingerprint and the extractor, and `--latest=false`, so that D3B's own releases stay the latest;
  - a release without this pack, such as one made for an older format (D24): `gh release upload` of both assets;
  - a release holding this pack, by GitHub's digest of the asset: nothing to do;
  - a release holding another pack under that name: refused.
- **Never replaced.** A host keeps the pack it has and never downloads it again, so a published pack is fixed for its
  build and format: replacing it would leave hosts with two packs under one name. An extraction fix that changes the
  content of a published pack ships under a new format. The extractor's version is in the pack (D26), so another build of
  the extractor gives other bytes, which the tool refuses for a published pack.
- **Dry run by default.** Nothing changes on GitHub without `--publish`, which then compares GitHub's digest of the
  uploaded pack with its own SHA-256.
- **Through the GitHub CLI.** The maintainer's credentials stay in `gh`; the tool handles no token. CI builds the tool and
  cannot run it: it has no client (D24).
- **Immutable releases stay off** for the repository: a release gains assets after it is published, a newer format's
  pack, then step 3's mapping. The tool enforces what immutability would, by never replacing an asset.
- **Rejected.**
  - Steps in the docs only: the checksum must be in the format the host reads, which `ClientRelease` defines once for
    both.
  - The REST API from the tool: more code, and a second place holding a credential.
  - A GitHub Actions workflow: CI has no client.

## D34: A hosts line brings each client to D3B, which rewrites the game server's address

*2026-10-27, step 3, session 3a, chosen by the owner. Settles Q8. Superseded in part by D35: no `hosts` line, the
client's own configuration brings it to D3B; the game server's address is still rewritten.*

The player starts each account's client from the official Ankama launcher, as without D3B. One line of the Windows
`hosts` file sends the connection host, which the client's configuration names, to a loopback address of D3B's. D3B
listens there, resolves the real host without the `hosts` file, and connects to it. The game server's address reaches
the client in a message of that first connection: D3B rewrites it to an address of its own, and connects to the real
game server when the client arrives. Nothing in the client, its files or its process changes.

- **What the client shows**, measured in [client.md](client.md#connection). The launcher starts `Dofus.exe` with its
  own local port, an instance id, a hash and the connection port, 5555 or 443. The client reads the connection host
  from the JSON its `ConfigUrl` names, connects to it, a load balancer, then to a game server whose address the first
  connection sends. In game, a client keeps one game connection, on port 5555, besides the chat, the launcher and the
  CDN.
- **Who starts the client.** The player, from the launcher: the client asks the launcher for its login token, so
  starting it otherwise means emulating the launcher and handling Haapi, which D23 leaves to the official client.
- **The line** names the connection host and an address of `127.0.0.0/8` that D3B's configuration sets, not
  `127.0.0.1`, so that D3B takes no port another program uses there. D3B listens on that address on the configuration's
  ports, 5555 and 443, so the launcher's port setting does not matter. Writing the line needs elevation, once: a tool of
  session 3b adds, checks and removes it, through the UAC prompt. The host never runs elevated and never edits the file.
- **Resolving.** D3B resolves the connection host without the `hosts` file, which would send it back to itself; how is
  session 3b's choice. The tool reads the host's name from the configuration the client reads, the public JSON of its
  `ConfigUrl`; that is no game connection (D23).
- **Every client on the machine goes through D3B** while the line is there. A client without a bot is relayed
  unchanged. Without D3B running, no client of the machine connects: removing the line restores direct play.
- **The game server.** D3B decodes the connection server's message holding the game server's address and ports, which
  needs the protocol mapping, and replaces the address with its own and every port with its game listener's. It keeps
  the real address for that client and matches the client's next connection to it, by what that connection sends first;
  how exactly is session 3e's. Until then the game connection goes straight to the server, and D3B sees the connection
  phase only.
- **Reading.** The references read port 5555 in the clear, framed by a varint length (D3); the client's TLS transport,
  Spin, serves its chat, which D3B leaves alone with Haapi and the launcher. Session 3b measures the real bytes before
  anything relies on them.
- **Within D2.** D3B hides nothing: connections leave the same machine, from the same address, one per client, as
  without it. Nothing is injected into the client, patched or emulated.
- **Risks.**
  - Should the configuration name another connection host, clients would bypass D3B: the tool compares the line with
    the configuration each time it runs.
  - An antivirus may warn about a change to `hosts`.
  - Whether the client accepts a loopback address for its connection host is measured in session 3b.
- **Rejected.**
  - Editing `ConfigUrl` in the client's `version` file and serving the configuration from D3B: no elevation, but the
    client's installation changes, the launcher restores the file at each update, and whether the client accepts a
    local `http://` address is not measured.
  - WinDivert: no rewriting of the game server's address, but a third-party kernel driver loaded with elevation at every
    start, and address translation on packets.
  - Hooking the client's `connect`, as BillBot does through Frida, or patching its code, as JondoEmu does through
    MelonLoader and Harmony: they change the client's process or files (D2, D23).
  - Emulating the launcher to start the client, as BillBot and JondoEmu do: D3B would get the login token through Haapi
    (D23).
  - Playing without a client, as BubbleBot does: ruled out by D23.

## D35: The client's configuration brings it to D3B, rewritten by the host while it runs

*2026-10-27, step 3, session 3b, chosen by the owner. Supersedes D34's `hosts` line.*

The client reads its connection host from the JSON its version file's `ConfigUrl` names (client.md). While D3B's host
runs, that URL names a copy D3B writes, Ankama's configuration with every connection host replaced by D3B's address:
the client connects to D3B, and D3B knows the real host from Ankama's configuration. No `hosts` line, no elevation.

- **Measured**, on client 3.7.12.12 started from the launcher ([client.md](client.md#configuration)). The client loads
  the URL with `UnityWebRequest` at start; it refuses `http:`, even on loopback ("Insecure connection not allowed"), and
  reads a `file:` URL. The launcher started it with the changed file, without repairing it. The client then connected
  through D3B to the server choice and into the game.
- **Flow.** `ClientRedirect`, a hosted service of the network module, starts after the relay listens:
  1. It reads `ConfigUrl` from `Dofus_Data/StreamingAssets/version`. When the URL is already D3B's, as after a crash,
     Ankama's is the one kept from before.
  2. It downloads Ankama's configuration, or uses the last one read when it cannot, and fails the host's start when it
     has neither.
  3. It writes, in `Network:Redirect:Directory` (`%LOCALAPPDATA%\D3B\client` by default), `config.json` (the copy),
     `config-url` (Ankama's URL) and `ankama-config.json` (Ankama's configuration).
  4. It points `ConfigUrl` at `config.json` as a `file:` URL, changing that line and no other byte of the file.

  While the host runs, it watches the file: the launcher rewrites it when it updates the client, and the client is
  pointed at D3B again, a check failing on a file still being written trying again a few times. At stop, Ankama's URL
  goes back, so a client started without D3B plays directly; so it does when the host is disposed without being
  stopped, as when a later service fails to start. After a crash, the next start, or
  `dotnet run --file scripts/client.cs -- restore`, puts things right.
- **One connection host.** A connection carries no name the relay could tell hosts apart by: a configuration listing
  several fails the start. Each would need an address of its own. A connection host that is a D3B address, as another
  D3B's copy would name, fails the start too: the relay would connect to itself.
- **Resolving.** With no `hosts` line, the relay resolves the real host with .NET's own DNS, at each connection: it is a
  load balancer. For D34's line the owner had chosen `DnsQuery_W` with `DNS_QUERY_NO_HOSTS_FILE` and
  `DNS_QUERY_BYPASS_CACHE`, over the DnsClient package and a DNS client of our own; with no line to get around, it was
  removed.
- **Within D2.** One line of a configuration file of the client's installation changes while D3B runs, and the launcher
  rewrites it at each update. The client's code and process stay untouched, and nothing is hidden: every client of the
  machine started while D3B runs goes through it, from the same machine, as D34 said.
- **Risks.**
  - Ankama could make the client check its version file, or refuse `file:` URLs. Each client update measures it again:
    a client that does not reach D3B shows it at once.
  - A path holding spaces or letters outside ASCII is percent-encoded in the URL; whether Unity reads it back is not
    measured.
  - After a crash, clients cannot connect until the host starts again or the tool restores the URL; the tool's `check`
    says so.
  - Two hosts on one machine would share the file, and the first to stop would give clients Ankama's configuration
    back; nothing prevents it yet.
- **Rejected.**
  - D34's `hosts` line: elevation, and a change to the whole machine, which the owner refused once it was built. Its
    tool, writing the line through the UAC prompt, was removed before it was committed.
  - Serving the configuration from D3B over `http:`: the client refuses it, measured.
  - Over `https:` with a certificate of our own: a root certificate in the user's store, worse than a `hosts` line.
  - BillBot's way: it starts `Dofus.exe` under Frida, hooks `ws2_32!connect` to send port 5555 to itself, stands in for
    the launcher's local service and gets the login token from Haapi (D2, D23).
  - The URL rewritten by a tool run by hand, which the lead session laid out as the other option: the configuration goes
    stale, and clients cannot connect while D3B is stopped.

## D36: D3B.Network relays the connection phase

*2026-10-27, step 3, session 3b*

- **Kestrel without a web host.** `BotBuilder` wraps the Generic Host's builder (D20), which has no web host, so
  `RelayServer`, a hosted service, runs Kestrel's own `KestrelServer` on its socket transport. Each endpoint ends in a
  connection handler and serves no HTTP; `KestrelServer.StartAsync` still takes an HTTP application, which nothing
  calls. Kestrel brings what the relay needs: connection ids in the logs, limits, and a graceful stop.
- **Where.** `Network:Address`, 127.0.13.59 by default, whose last two bytes read `0x0D3B`: in 127.0.0.0/8 and not
  127.0.0.1, so that D3B takes no port another program holds there (D34). `Network:Ports`, 5555 and 443, the
  configuration's ports. A port D3B cannot listen on, held by another program or reserved by Windows, stops the host,
  with a message naming the address and the setting to change, and exit code 1.
- **Each connection.** `RelayConnectionHandler` finds the real server through `ClientRedirect` (D35), connects to its
  addresses in turn, 10 s each, on the port the client chose, through `SocketConnectionContextFactory` (D8), and copies
  each direction as it reads: bytes as they are, flushed at once, whatever the framing. It holds no state between
  connections, so any number of clients are relayed at once; a test relays eight.
- **Closing.** A side closing completes the other's output, which Kestrel's socket transport sends, then shuts down both
  ways: the relay keeps no half-open connection. A side still open 5 s later is cut. When the host stops, every
  connection is cut, and one still looking for its server stops looking.
- **The network module**, in `D3B.Bot` after the data module, registers the relay and the redirect; captures record the
  installed build from `ClientBuild` when the data module is there.
- **Rejected.**
  - An accept loop of our own over `IConnectionListenerFactory`: Kestrel's server already manages connections and their
    stop.
  - `WebApplication`'s builder: `BotBuilder` and plugins build on the Generic Host's (D20).
  - Half-closed connections: Kestrel's socket transport shuts down both ways once an output completes.

## D37: Captures record raw reads, in a format of our own

*2026-10-27, step 3, session 3b, chosen by the owner. Settles the format of Q7; replays with injection are session 3f's.*

- **What.** The bytes each side sent, read by read, both ways, and each side closing, timed in microseconds from the
  start. Never decoded messages: they follow from the raw bytes and the build's mapping, so an old capture decodes again
  with a newer one.
- **Format.** One file per connection, `.d3bcap`. A header: the magic `D3B-CAPT`, the format version, the start, the
  installed client's version, the listener, the server's host and address. Then records, appended as they come: a kind,
  the offset as a varint, and for bytes their length as a varint and the bytes. Each record is flushed: a capture whose
  host was killed reads up to its last whole record, and a record cut short is reported as such. A write that fails ends
  the capture, never the relay.
- **Where.** Off by default; `Network:Captures:Enabled` records every connection in `Network:Captures:Directory`,
  `%LOCALAPPDATA%\D3B\captures` by default. A capture holds the account's login ticket: it stays on the machine, is never
  committed, never shared.
- **Replay.** `CaptureReplay` writes each record into a `Pipe` as its own write and flush, so a reader gets the bytes cut
  where the network cut them (D8). Tests build their own captures with `CaptureWriter`.
- `scripts/capture.cs` describes captures by their structure: reads, frames, sizes, TLS or not, and with `--frames` the
  protobuf shape of each frame, field numbers, wire types and lengths, never a value.
- **Rejected.**
  - pcapng: IP and TCP headers to make up, for Wireshark, which does not decode Dofus.
  - JSON lines with Base64: 1.4 times larger and slower, readable for protobuf no one reads by eye.
  - MemoryPack and zstd, as packs: a whole object written at once, where a capture grows as the connection goes, and
    would be lost in a crash.
  - Frames instead of reads: it would depend on the framing being measured, and lose the cuts a reader must handle.

## D38: The connection is varint-framed protobuf, in the clear

*2026-10-27, step 3, session 3b*

Measured on the connection phase of two clients through the relay ([client.md](client.md#the-connection-phase-on-the-wire)):
no TLS, every byte in varint-length frames, every frame a protobuf message.

- `FrameReader.TryReadFrame` reads a frame from a `SequenceReader<byte>`: a length of at most five bytes, 32 bits, then
  the frame, sliced from the sequence without a copy. It consumes nothing until a frame is whole, so the caller reads
  more and tries again, and a length that cannot be one, or above 16 MiB by default, throws: no telling where the next
  frame starts. `FrameWriter` writes them.
- **Benchmark.** 10,000 frames of the measured sizes and of 4,000 bytes: about 16 ns a frame on one segment, 23 ns on
  segments of 4 KiB and 36 ns on segments of 512 bytes, with no allocation (BenchmarkDotNet, short run,
  `benchmarks/D3B.Network.Benchmarks`).
- **Rejected.**
  - Reading a length byte by byte with no bound, as some references do: a misread length would make a reader wait for,
    and buffer, gigabytes.

## D39: The obfuscated schema is a protobuf FileDescriptorSet

*2026-10-27, step 3, session 3c, chosen by the owner*

A client build's obfuscated schema, every message and enum of its protocol under their obfuscated names, is a
`FileDescriptorSet`, protobuf's own description of `.proto` files, serialized: `schema-<format>.binpb`, format 1.

- **Where.** Beside the build's pack, in `%LOCALAPPDATA%\D3B\data\<version>\`, written by `scripts/schema.cs` (D40).
  Derived from the client, it is never committed (D11): it ships as an asset of the release `client-<version>`, with its
  SHA-256 beside it (D24). It is published with the build's mapping in session 3e, since nothing downloads it before;
  until then the maintainer keeps the schemas of the builds measured.
- **Why.** Google.Protobuf builds descriptors from it and links every type name to its type, so a schema that does not
  hold together is never written. Session 3d compares it with the descriptors of the readable `.proto` files, which
  Grpc.Tools compiles: one model on both sides, the one D15 planned for the translation's tables. protoc reads it too.
- **Files.** One per protocol assembly, `connection.proto` and `game.proto`, in proto3 and without a package, since the
  client's types have no namespace; `google/protobuf/any.proto`, which `game.proto` imports, is in the set. A map is
  the entry message protobuf describes it with, a proto3 `optional` field has the synthetic oneof protoc gives it.
- **Provenance.** A descriptor set has no place for it: the directory and the release name the build. The schema is
  deterministic, two runs on one client giving the same bytes, so writing it again checks it. A change in what the tool
  writes for a client whose schema is published bumps the format, as for packs (D33).
- **To read it**, `--proto <dir>` also writes it as `.proto` text, which protoc compiles back to a descriptor equal to
  the schema, field by field (measured); it stays on the machine, never committed.
- **Rejected.**
  - A JSON document of our own: readable and diffable, with its provenance inside, but a copy of `descriptor.proto`, and
    session 3d would convert one side to compare it with the other.
  - The `.proto` text as the artifact: the most readable, but .NET has no `.proto` parser, so every reader would run
    protoc.
  - An entry of a D3B pack, its provenance in `PackInfo`: a model of our own again, and the pack's format is the data's.

## D40: The schema is read from the client's metadata, and its encodings from the machine code

*2026-10-27, step 3, session 3c*

`scripts/schema.cs` opens the client's `GameAssembly.dll` and `global-metadata.dat` with Cpp2IL.Core
2022.1.0-pre-release.21, in process (D15), and reads `Ankama.Dofus.Protocol.Connection` and
`Ankama.Dofus.Protocol.Game`, measured in [client.md](client.md#protocol-code).

- **Metadata version 39**, the session's first risk: Cpp2IL reads it, the client opening in about 5 s. Pre-release.21,
  of 2026-02-22, is still Cpp2IL's latest. JondoEmu's notes record that Il2CppDumper refuses version 39; not measured
  here.
- **The structure, from the metadata.** Protobuf's C# generator gives every message the same members, whose shape
  the obfuscation keeps:
  - a message is a type implementing `IBufferMessage`;
  - its `int` constants are its field numbers, declared in field order: the n-th is the number of the n-th field
    property, a property with a setter, or a `RepeatedField` or `MapField`, whose type gives the field's C# type;
  - a getter-only `bool` property right after a field's says whether the field is set: a proto3 `optional` field, or a
    scalar member of a oneof;
  - a oneof is a getter-only property typed by an enum nested in the message, whose values but zero are its fields'
    numbers;
  - the message's own messages and enums are in a static class nested in it, protobuf's `Types`.

  Any other member fails the reading: a member the generator does not write means a message this reading would
  misread.
- **The encodings, from the machine code.** A C# `int` may be an `int32`, a `sint32` or a `sfixed32`, which differ on
  the wire. Cpp2IL lifts each message's `InternalWriteTo` and static constructor to its instruction-set-independent
  language, and the tool follows the constants loaded into the argument registers: a field of one value is written as
  `WriteRawTag(tag)` then `Write<Type>`, and a list of scalars through a codec, `FieldCodec.For<Type>(tag)`. The tag
  holds the field's number and wire type, so an encoding goes to its field by number, never by order. Every field of
  one value must be written once, by a method fitting its C# type, and every codec must belong to a field, which also
  checks how numbers pair with properties.
  - A map's key and value codecs have the same tags in every map, so one is matched by its C# type, and one with an
    encoding other than that type's default fails the reading: which map is its could not be told.
  - Codecs of messages and enums are generic, and Cpp2IL leaves their calls unresolved: the metadata types those lists,
    and a list of enums is taken as packed, proto3's default; a reader accepts either.
  - Registers are followed only while their value holds on every path: a call forgets those it may change, and an
    instruction a jump lands on forgets them all.
- **Measured** on 3.7.12.12: every field read agrees with its code; the protocol uses only `int32`, `int64`, `uint32`,
  `bool`, `string`, `float`, enums and messages, and packs every list of scalars. The schema, 170,287 bytes, is written
  in about 6 s, the same bytes on two runs.
- **Checked against the captures.** `scripts/capture.cs --schema <file>` finds, among the schema's messages, the one
  typing the most fields of every frame of the captures, both sides, and with `--frames` names every field. On session
  3b's three captures, one message types all 356 fields of the 8 frames, the next at most 8: the connection's root,
  whose fields 1 and 2 are the client's and the server's messages, and theirs 3 and 4 the two exchanges
  ([client.md](client.md#the-connection-phase-read-by-the-schema)).
- **Rejected.**
  - Writing the assemblies to disk and reading them with `MetadataLoadContext`, as JondoEmu does: 69 MB written per
    build, for the metadata Cpp2IL's contexts give in process.
  - The types of the C# properties alone, as JondoEmu's `.proto` writer reads them: an `int32` and a `sint32` look the
    same there.
  - The client's 102 original files: a message's static `Descriptor` getter names its file's class and its index, but
    enums have no such getter, and 69 top-level enums could not be placed without guessing. One file per assembly.
  - The client's own serialized descriptors, which would give its schema whole: they are not in clear in the metadata,
    and finding where they are, perhaps encrypted with the obfuscator's strings, is a study of its own. Not done.

## D41: Step 3 after session 3c: the connection's mapping first, the game's after its captures

*2026-10-27, step 3, session 3d, chosen by the owner*

The roadmap's session 3d held the readable `.proto` files of the connection phase and of the first milestone's
messages, the generated classes, the scored matching, the pinned pairs, the contract audit, the IDA tracer and the
anchors: too much for one session. It is split, and the sessions after it renumbered:

- **3d**: the connection phase's mapping, end to end: its readable `.proto` files and classes, the matching, the pins,
  the audit, the anchors from session 3b's captures, the mapping's format, and the tool writing a build's mapping
  (D42 to D45).
- **3e**: Q2 and the version gate for the connection phase, the mapping and the schema published in the build's release,
  the game server's address rewritten, so that D3B holds both connections and captures the game connection.
- **3f**: mapping, part 3: the first milestone's game messages, with anchors from 3e's captures of the game connection.
- **3g**: Q9 and replays with injection, the former 3f.
- **Why this order.** The game messages' anchors need captures of the game connection, which goes straight to the game
  server until 3e rewrites its address; and 3e needs the connection phase's mapping to decode the message holding it.
- **The IDA tracer** waits for a measured need, in 3f at the earliest. It traces which fields the client's code reads,
  which BillBot uses to tell fields of one shape apart; D40 already reads every field's encoding from the machine code,
  and on the connection phase the structure alone pairs every type (D43).
- **Rejected.**
  - The roadmap's 3d as written: the game messages would have no anchor, their connection not going through D3B yet.
  - The tracer now: the connection phase leaves nothing for it to settle.

## D42: The readable protocol of the connection phase

*2026-10-27, step 3, session 3d*

Bot code reads the connection phase through classes Grpc.Tools 2.85.0 generates at build, with Google.Protobuf (D11,
D15), from five `.proto` files of our own in `src/D3B.Protocol/Connection/`: `connection`, `identification`, `servers`,
`accounts` and `friends`, package `d3b.connection`, C# namespace `D3B.Protocol.Connection`, no gRPC service. They hold
37 messages, 9 enums and 92 fields, one for each of the build's connection file (D39).

- **Numbers are the wire's.** The obfuscation renames messages, fields, enums and values and keeps numbers: 3.7.12.12's
  connection file has the messages and field numbers of `dofus-unity-proto`, an older build's, but where this build
  changed or added a field ([client.md](client.md#the-connection-phase-mapped)). So a readable class reads the
  connection's frames as they are; whether the translation then decodes once or twice stays Q2's, in 3e.
- **Names** come from the references where this build's shape is theirs, same number and type and, for an enum, as many
  values, written in our own words (D3). Elsewhere they are neutral, and the comment says what is not measured: the
  four rights flags, where the references have five or six; the values of the identification's failure reason, 15
  where they have 14, of the server selection's error, 5 where they have 4, and of the server type, new, 7. A field or
  value is renamed once its meaning is measured: their names are in neither the wire nor a mapping's contract (D45).
- **What is not on the wire is ours.** Files, nesting and declaration order are chosen to read well; Ankama's classes
  come from one file. A message field carries no `optional`: the generated C# gives such a field no `Has` property, so
  the schema cannot show its presence (D40).
- `ReadableProtocol` lists the files, the connection's anchor plan (D43), the schema file each package is matched in,
  and the digest of the protocol's contract (D45).
- **Rejected.**
  - The references' files (D3; `dofus-unity-proto` has no license), or Ankama's single file.
  - Numbers of our own: every field would need translating, for nothing.
  - Names guessed from the references where the build changed: a wrong name misleads more than a neutral one.

## D43: Matching: pins, anchors from captures, fields, then scores

*2026-10-27, step 3, session 3d*

`ProtocolMatcher`, in `D3B.Protocol.Mapping`, pairs every readable message and enum with its counterpart in a build's
schema; `scripts/mapping.cs` runs it on the installed build. Ideas from BillBot (scores, pins) and JondoEmu (anchors
from real captures), the code our own (D3).

- **Evidence, strongest first.** The build's pins; the anchors its captures give; the fields of every pair, a field
  being its counterpart's field of the same number when it has its shape (label, map or not, kind of type), which pairs
  their types; then structural scores for what is left, followed through fields again. Pairs are one to one both ways,
  and the first pair a type gets stays: later evidence that disagrees is a conflict, which fails the mapping. Nothing is
  settled by a guess.
- **Scores**, from 0 to 1: the share of fields, on both sides, with the same number and shape, a field of a paired
  type counting only if the types are each other's. A pair is accepted at 0.8 or more, the best of its row and of its
  column by 0.1 or more. Two empty messages score 1, so only where they are used tells them apart.
- **Scope.** A readable package is matched within one file of the schema, `d3b.connection` within `connection.proto`:
  the client keeps the connection phase and the game in two assemblies (D40). Packages matched in one file compete for
  its types together.
- **Anchors**, measured in the build's captures by the structure of their frames only (D37, D38): the root is the
  message of the connection file that reads every frame and types the most of their fields, alone; each frame holds one
  of its fields, the client's `request` and the server's `response`, the field a side's first frame holds, other
  fields, such as the server's events, being passed over; in a side's first frame holding it, the member of a oneof
  the frame holds is `identification`, in its second `server_selection`. A frame contradicting another leaves the
  anchors it touches out, and the tool fails until the plan or the readable protocol is fixed.
- **Pins** are committed, one file per build, `src/D3B.Protocol/Pins/<client version>.json`, protobuf's JSON form of
  `PinnedPairs` (`pins.proto`): a readable message or enum and its obfuscated name, and the fields whose numbers differ.
  A build's pins stay when the next build comes, so that its mapping can be made again. 3.7.12.12 needs none: its file
  is empty. A pin of a field the readable message lacks is a conflict, the tool fails without the directory of pins,
  and a test checks that every pin names its build, a readable type and its fields.
- **Measured on 3.7.12.12**: with the anchors, 37 messages paired, 7 by the captures and 30 by fields, and 9 enums by
  fields; from pins and structure alone, the same pairs, 29 messages and 4 enums by scores
  ([client.md](client.md#the-connection-phase-mapped)).
- **Risks.**
  - Fields pair by number, which the obfuscation keeps. Should a build shuffle the numbers of fields of one type, the
    structure could not tell them apart: values in captures, the IDA tracer or pins would.
  - The tool reads every capture of the build. Once 3e captures the game connection, no message of the connection file
    reads its frames: 3e must keep the connection phase's captures apart.
- **Rejected.**
  - BillBot's signals and weights, runtime confidence, IDA accesses and capture order among them: built for 2,000
    messages, and the connection phase needs none.
  - JondoEmu's matching from one build's dump to the next: structure settles each build on its own, so far.
  - Matching every readable type against the whole schema: 2,213 candidates where the right file has 37.

## D44: The contract audit

*2026-10-27, step 3, session 3d*

`ContractAudit` checks a mapping against the readable protocol and the build's schema. It fails when a readable
message or enum has no counterpart, or one shared with another; when a readable field has none, or one that does not
fit: another number than the mapping's, another type (through the mapping for messages and enums), label or presence,
or another oneof, the members of a readable oneof having to be those of one oneof of the counterpart, and of no other
readable oneof; when a readable enum value has none; when the mapping pairs a field or a value twice, or one the
readable type lacks; and when the mapping names another format, schema or readable protocol. What the build has beyond the readable protocol is noted, not failed: a reader meets it as unknown fields,
which fail loudly when they carry a value (D11).

- **Where it fails the build.** The mapping tool runs it and, on any failure or conflict (D43), exits with code 1 and
  writes nothing. In 3e, the host runs it on the mapping it loads and does not start on a failure, D11's version gate.
  CI has no client and no schema: it runs the audit's tests, on schemas made by renaming the readable protocol and on a
  fixture of our own, checks every pin, and `MappingFormatTests` fails when the readable protocol's contract changes
  without a new mapping format (D45).
- **Measured on 3.7.12.12**: 37 messages, 92 fields, 9 enums and 60 values checked, no failure and no note: the readable
  protocol reads every field and value of the connection phase.
- **Rejected.**
  - An audit in `dotnet build` or `dotnet test` against the installed client: CI has none, and a test reading the
    machine's client is not reproducible.
  - BillBot's contract, a list of required messages with score thresholds: the readable protocol is the contract,
    whole.

## D45: The mapping is a protobuf message of our own

*2026-10-27, step 3, session 3d, chosen by the owner*

A build's mapping is `mapping-<format>.binpb`, a serialized `ProtocolMapping`, which `mapping.proto` defines in
`src/D3B.Protocol/Mapping/`: its format, the client version, the schema's SHA-256 (D39), the readable protocol's digest
and the tool's version; then every readable message by full name with its obfuscated full name, the evidence that paired them
(D43) and its fields as pairs of numbers; and every readable enum with its values likewise.

- **Where.** Beside the build's pack and schema, in `%LOCALAPPDATA%\D3B\data\<version>\`, written by
  `scripts/mapping.cs` aside then moved. Derived from the client, it is never committed (D11): it ships as an asset of
  the release `client-<version>` with its SHA-256 (D24), published with the schema in 3e.
- **Format.** `MappingFile.FormatVersion`, 1, changes whenever `mapping.proto` or the readable protocol's contract does,
  so that each version of D3B reads its own mappings, side by side, and a published one is never replaced (D33). The
  contract is `ProtocolDigest`: every message and enum by full name, every field's number, type, label, presence and
  oneof, every value's number; names of fields, values and oneofs, and comments, are not part of it.
  `MappingFormatTests` records it per format, as `PackFormatTests` does for packs.
- **Measured.** The mapping of 3.7.12.12 is 3,305 bytes, the same bytes on two runs of one build of the tool, whose
  version it records, as a pack records its extractor's (D33); the tool takes 0.2 s once built.
  `--json` prints it to be read, never to be committed.
- **Why.** One model with the schema (D39), with Google.Protobuf already there; generated code reads and writes it,
  deterministically; it pairs numbers both ways, so it serves either answer to Q2.
- **Rejected.**
  - JSON of our own, which the lead session laid out: readable as it is, but a second model beside the descriptors.
  - The schema annotated with custom options naming the readable counterparts: one artifact, but the schema's format
    and the mapping's tied together.

## D46: Session 3e split: the translation and the gate, then the game connection

*2026-10-28, step 3, session 3e, chosen by the owner*

D41's session 3e held Q2, the version gate, the schema and mapping published, the game server's address rewritten, the
game connection relayed and captured, the connection phase decoded and recorded, and measurements on running clients:
two decisions, and work both with and without clients. It is split, and the sessions after it renumbered:

- **3e**: Q2 decided and built for the connection phase, the version gate, the schema and mapping published by the
  release tool, and the relay decoding the connection phase and logging it (D47 to D50). Everything is checked without a
  client, so in CI.
- **3f**: the game connection: the server selection's answer rewritten to a game listener of D3B, the client's next
  connection matched with the real game server, relayed and captured apart from the connection phase, which
  `scripts/mapping.cs` reads (D43), and what client.md leaves open measured on running clients: port 443, whether the
  client takes an address for the game server's host, how it chooses among the ports, and which side speaks first on
  the game connection. That last measurement decides how a connection is matched: by the ticket it presents, or by a
  listening endpoint of its own per server selection. It is decided once measured, not before.
- **3g**: mapping, part 3, the first milestone's game messages, D41's 3f.
- **3h**: Q9 and replays with injection, D41's 3g.
- **Why.** 3f decodes the server selection's answer, which needs the mapping the gate loads and a relay that reads
  frames: both are 3e's. 3e ends reviewable without a client; 3f needs running clients and a decision of its own.
- **Rejected.**
  - Everything in one session, as D41 planned.
  - The gate and publication alone in 3e, the translation with the game connection in 3f: the translation is what the
    gate checks a mapping for.

## D47: The translation decodes once, through the readable classes

*2026-10-28, step 3, session 3e, chosen by the owner. Settles Q2.*

The readable classes read a build's wire as it is: the obfuscation keeps every field's and value's number (D42), and
the connection phase carries no type name. `ProtocolTranslation`, in `D3B.Protocol.Translation`, exists only for a build
whose mapping holds, and decodes through it.

- **At the gate.** `ProtocolTranslation.Create` audits the mapping (D44), then requires every readable field and enum
  value to have its own number on the wire. A build renumbering one is refused, each such field named: its mapping holds
  both numbers (D45), so rewriting them can be added for the messages that change, once one does and is measured.
- **At run time.** A frame is parsed once, its unknown fields dropped, and is understood only when its message encodes
  back to the frame's length: one that does not holds a field or a value the readable protocol lacks, or was encoded
  otherwise. D3B passes it on as it is, and never rewrites it, so that 3f rewrites only frames understood whole, and
  loses nothing doing so. An enum value the readable enum lacks keeps its number and encodes back as it came, so it goes
  undetected and nothing of it is lost; the audit notes the values a build adds (D44), none in 3.7.12.12.
- **The game**, from 3g: the `Any` envelope's type URL names the obfuscated message, which the mapping turns into the
  readable one, then parsed the same way.
- **Measured.** The 8 frames of session 3b's captures decode through the readable classes, hold no unknown field, and
  encode back to the same bytes; so do the 4 frames of a client logged in through the host in this session (client.md).
- **Rejected.**
  - Rewriting field numbers on the wire, both ways: an encoder of our own and a second pass and copy of every frame, a
    changed tag size changing every enclosing length, for renumbering no build has shown.
  - Decoding twice, through the obfuscated descriptors then into the readable classes: Google.Protobuf's C# has no
    dynamic message, so a decoder and a copier of our own, and two trees per message.

## D48: The version gate

*2026-10-28, step 3, session 3e. Builds D11's gate. Superseded in part by D172: the schema and mapping are found
beside the pack on this machine, else among the app's own, never downloaded.*

- **The protocol module.** `ProtocolModule`, in `D3B.Bot.Protocol`, requires the data module and registers
  `ProtocolSource` and the build's `ProtocolTranslation`, a singleton every bot shares. `DefaultModules` holds the data,
  protocol and network modules, in that order, and the network module requires the protocol module: a relay never runs
  without the gate, and disabling the protocol module has no effect while the network module is there (D20).
- **Flow.** After the pack (D31) and before the host starts, `BotHost` calls `ProtocolSource.EnsureAsync`, then asks for
  the translation. The schema and the mapping are looked for beside the build's pack, `schema-1.binpb` and
  `mapping-1.binpb`; one missing is downloaded from the release `client-<version>`, checked against its SHA-256 and that
  it is a schema, or a mapping naming this build, before it is moved into place. The translation then loads both, checks
  that the mapping names the installed build, audits it and checks its numbers (D47). Any failure logs a critical message
  naming what is missing or what fails first, and the commands that map the build, and the host exits with code 1.
- **Mapping a new build.** The gate never needs a running host: a new build's connection phase is mapped from its
  structure alone (D43), so `scripts/schema.cs`, then `scripts/mapping.cs`, both without a capture, map it, and the host
  starts. The mapping tool refuses what the host would, renumbered fields included, so that it never writes a mapping
  the host does not start on. Captures of the game connection, from 3f, need the connection phase's mapping only, which this gives.
- **Shared names.** `SchemaFile` moves from `scripts/unity/` to `D3B.Protocol.Mapping`, beside `MappingFile`, so that the
  schema tool, the mapping tool, the release tool and the host name the schema one way. The data module registers its
  `ReleaseToken` for both downloads.
- **Rejected.**
  - The relay running without the protocol module, raw, as in 3b: it would bypass D11's gate, and 3f cannot rewrite the
    game server's address without decoding.
  - Failing on the build's version alone, without the audit: a mapping from another version of D3B's readable protocol,
    or matched with another schema, would pass.

## D49: The relay reads frames, and logs the connection phase by what it is

*2026-10-28, step 3, session 3e. Superseded in part by D51: the observer becomes an interceptor, which may have a frame
held.*

- **The seam.** `IRelayObserver`, in `D3B.Network.Relay`, sees every whole frame of every relayed connection, with the
  connection's id and the side that sent it. `D3B.Network` stays free of the protocol; the network module registers the
  observer.
- **Forwarding** is D36's: bytes as they are read, at once. With an observer, each side's bytes are also read with
  `FrameReader` (D38): each whole frame is shown, the start of one not yet whole kept until the rest comes. A side whose
  bytes are no varint frames is observed no more, with a warning; so is a side once the observer has thrown, which is
  logged as an error and never reaches the connection.
- **The connection phase.** `ConnectionPhaseDecoder`, in `D3B.Bot.Network`, decodes each frame through the translation
  (D47) and logs it by the readable fields its oneofs hold and its length, such as `request.identification.token, 126
  bytes`, at the information level; a frame not understood is a warning naming why. Never a value: the phase carries the
  account's login token and ticket. That supersedes the architecture's "logged with the raw bytes": the bytes are in the
  captures, when they are on, which stay on the machine (D37).
- **Measured** on a client logged in through the host: the 4 frames of its connection phase decoded and understood
  (client.md).
- **Rejected.**
  - Forwarding frame by frame, each frame held until whole, which the lead session built first: bytes that are no frames
    but read as a plausible length, such as a TLS stream on port 443, which is not measured, would be held until that
    many came, and the connection would hang. 3f holds the one frame it rewrites, the server selection's answer, with a
    way out of its own.
  - Snapshots of decoded messages with Verify, which the architecture planned from 3e: the tests compare what is logged
    with what they built, and snapshots wait for bot states, at step 4.

## D50: The release tool publishes the schema and the mapping

*2026-10-28, step 3, session 3e. Extends D33; publishes what D39 and D45 planned for this session.*

`scripts/release.cs` publishes a build's pack, schema and mapping, `data-3.pack`, `schema-1.binpb` and
`mapping-1.binpb`, each with its SHA-256 beside it, to the release `client-<version>`.

- **All three or nothing.** A host needs all three (D31, D48): a missing one stops the tool with the command that writes
  it, exit code 2.
- **Checks** before anything is published: the pack as D33 checks it; the schema loads; the mapping names the build and
  holds, as `ProtocolTranslation` requires (D47).
- **By the release's state**, each asset on its own: absent, it is uploaded; present with the same GitHub digest,
  nothing to do; present otherwise, or with its checksum missing, or a checksum without its asset, the tool refuses,
  before uploading any. A new release is created with all six files, its notes naming each asset's SHA-256 and where it
  comes from.
- **Never replaced** (D33): the mapping records its tool's version (D45), so a mapping made again by another build of
  the tool has other bytes, and the published one is kept. A change in what a tool writes ships under a new format.
- **Dry run by default**, as D33: nothing changes on GitHub without `--publish`. Measured on 3.7.12.12: the release does
  not exist yet, and would be created with the three files, which hold.
- **Rejected.**
  - Publishing whichever files are there: a release holding a pack without its mapping, which no host can start on.

## D51: The relay holds the server selection's answer alone, and rewrites it once understood whole

*2026-10-28, step 3, session 3f. Supersedes in part D49: the observer becomes an interceptor, which may have a frame
held.*

- **The seam.** `IRelayInterceptor`, in `D3B.Network.Relay`, replaces D49's `IRelayObserver`. The relay asks it for an
  `IConnectionInterceptor` per connection, with the route of the listener that took it (`RelayRoute`: the connection
  phase or the game); none leaves the connection unwatched. The connection's interceptor sees every whole frame, both
  sides, and says whether a side's next frame is held. `D3B.Network` stays free of the protocol.
- **Holding.** A frame is held only when the interceptor asks for it before any of its bytes has gone, and its length is
  within `RelaySettings.MaxHeldLength`, 64 KiB: its bytes wait until it is whole, then what the interceptor returns goes
  in its place, its new length before it. Every other byte goes as it is read, as in D49.
- **The way out.** A frame held goes on as it came, and its side holds nothing more, when it is not whole after
  `RelaySettings.HoldTimeout`, 5 s, or when its side closes; bytes that are no frames, or an interceptor that throws,
  end the watching of the side, as in D49. A stream that is no frames but reads as a plausible length costs that delay
  once: the hang D49 feared is bounded.
- **What is held.** `ConnectionPhaseSession`, each connection phase's interceptor, has the server's frames held from the
  client's `request.server_selection` until the server's `response.server_selection`. The relay shows a frame to the
  interceptor before flushing it to the other side, so the hold is asked for before the server can answer. Events in
  between are held too, each going on once whole.
- **The rewrite.** Only an answer understood whole (D47), and a success: its host becomes D3B's address and its ports
  the game listener's (D53), the real host, ports and ticket going to `GameServerSelections` (D52); the message is
  encoded again through the readable classes, the rest as it was. Any other answer, one not understood, or one the relay
  could not hold whole goes on as it came, with a warning: the client then goes straight to the game server, as before
  this session. Captures keep what each side sent, the answer as the server sent it.
- **Measured** on clients of 3.7.12.12 started from the launcher: the answer, 83 bytes, came in one read, was rewritten,
  and the client went to D3B's address (client.md).
- **Rejected.**
  - Telling the answer by the first bytes of a frame not yet whole: a reader of partial protobuf, where the client's
    request already says which answer comes next.
  - Holding every frame of the connection phase: D49's risk, for nothing.

## D52: The game connection is matched by the ticket it presents

*2026-10-28, step 3, session 3f, chosen by the owner. Settles what D34 and D46 left to this session.*

- **Measured** (client.md): on the game connection the client speaks first, as soon as it is connected and unprompted,
  and the server one round trip later. That first frame, 76 bytes, holds the ticket the selection's answer gave, 32
  characters, as a string four fields down: so did every game connection measured, four.
- **How.** The game listener reads the client's first frame before connecting to any server, leaving it in the pipe,
  within the hold's bounds (D51): whole within 5 s, 64 KiB at most. `GameServerSelections` looks for the ticket of a
  selection waiting among the frame's length-delimited values, which `ProtobufValues` finds by the wire format alone, so
  no mapping of the game is needed. The selection found is taken, and the relay connects to its host, resolved then, on
  the real port behind the game port the client chose; the frame is then relayed and recorded first.
- **Failing closed.** A first frame presenting no ticket waiting, none whole in time, or bytes that are no frame: the
  connection is closed with a warning, and nothing reaches a server. A selection waits 60 s
  (`RelaySettings.SelectionLifetime`), is taken once, and its ticket is kept in memory only, never logged.
- **Several clients at once.** Two clients of two accounts, started together, went into the game through D3B, on two
  game servers, each matched by its ticket.
- **Risk.** A build whose server spoke first would have every game connection closed after 5 s: it would show at once,
  and the measurement is made again at each client update. A listening endpoint per selection would then be the way.
- **Rejected.**
  - A listening endpoint per selection, an ephemeral port listed alone, which the lead session laid out: no reading, and
    whoever speaks first, but a listener per selection outside Kestrel's fixed endpoints, and a port any local program
    could connect to first.
  - The oldest selection waiting, which the measurements used: two clients selecting at once could be crossed, which
    step 10 cannot have.
  - The ticket's bytes searched anywhere in the frame: the wire says where each string starts and ends, a search does
    not.

## D53: The game connection is relayed on a port of its own, unread, and captured apart

*2026-10-28, step 3, session 3f*

- **The listener.** `RelayServer` also listens on `Network:Game:Ports`, on D3B's address, each endpoint handing its
  connections to the relay with its route. One port by default, 5556: the client connects to the first port it is sent,
  whatever its connection port setting (client.md). The answer lists a game port for each real port, as far as the game
  listener has them, each standing for the real port at its position: one. A port the relay cannot take stops the host,
  as in D36.
- **Unread.** The connection phase's decoder gives no interceptor for the game connection, whose protocol is the game's,
  read from session 3g: its bytes go as they are, both ways.
- **Captures apart.** The game connection's captures go to `game`, in the captures' directory
  (`RelaySettings.GameCapturesDirectory`). The mapping tool and `scripts/capture.cs` read the files of the directory
  they are given, not of its subdirectories, so the connection phase's anchors never meet the game's frames (D43);
  `scripts/capture.cs <captures>\game` describes the game's.
- **Measured**: four game connections of 23 to 32 s, every byte in varint frames of protobuf in the clear, both sides.
  The mapping tool, run again with seven captures of the connection phase beside four of the game in `game`, read the
  seven and paired the same types.
- **Rejected.**
  - One listener for both connections, told apart by their first frame: the connection phase's server is reached
    before its client speaks.
  - A field of the capture's header naming the route: a new capture format, where a directory leaves the tools as they
    are.
  - A second address for the game listener: the ports already tell the routes apart.

## D54: The game is paired by pins where its structure is silent, then from build to build

*2026-10-28, step 3, session 3g, chosen by the owner. Supersedes in part D43: at the game's scale, one build's structure
no longer settles it alone.*

Measured on 3.7.12.12's schema and session 3f's four captures of the game connection
([client.md](client.md#the-game-read-by-the-schema)):

- The game file holds 2,175 messages, 1,372 of which no field is typed by: the messages frames carry. 758 of these have
  a shape no other has; 117 are empty, 73 hold one `int32`, 48 one `bool`. Of the 63 messages the first milestone needs,
  as the references name them, 2 have a shape the build holds once, 24 one it no longer holds, and 37 one shared with 2
  to 175 of its messages.
- Neither the client's code nor its metadata names them: the classes handling the game's messages are obfuscated too,
  and the 510 original names the metadata holds are bare strings, tied to no type.

So:

- **Pins** pair what the structure and the captures leave open, committed per build (D43): for 3.7.12.12, the 43
  messages the frames carry that the readable protocol reads, but the ticket's request, which the captures anchor.
  Every message they hold is then paired by its fields.
- **Proposed by the tool.** `scripts/mapping.cs` lists, for each readable message left unpaired, the build's messages it
  fits, those the captures carry first, with the side carrying them. The maintainer pins from what the player did; the
  tool pins nothing.
- **Scores** still run, within the game's file, accepted only clear of every rival (D43): without pins they paired 9 of
  3.7.12.12's messages, each as pinned.
- **From build to build**, later: a build's schema and mapping pair the next build's, by the order and shape of their
  messages, once a second build is installed to measure it on (Q10). Until then, a client update stops the host until
  the game is pinned again (D48).
- **Rejected.**
  - Anchors from scripted captures, the order of the player's actions: every update replays them, and the client sends
    many other requests between.
  - A study of the client's code first: the classes using the messages are obfuscated, measured.
  - Build to build now: no second build to measure it on.

## D55: The game's events read only what bot code uses; its requests are whole

*2026-10-28, step 3, session 3g, chosen by the owner. Amends D47 for the game.*

- **Measured.** The map's information holds, through its fields, 142 messages, 36 enums and 679 fields; the ten largest
  events on entering the game, 171 messages and 779 fields.
- **Partial.** A readable message marked `option (d3b.readable.partial) = true` (`src/D3B.Protocol/Readable/options.proto`)
  declares only the fields bot code reads, their meaning measured, and leaves its counterpart's others out on purpose. A
  message the client sends is never partial, so that the bot builds it whole: the mapping tool fails when the captures
  show the client sending one.
- **The audit** fails as before on what a readable message declares (D44). It counts what a partial message leaves out
  apart from its notes (`AuditReport.Unread`), so that a note stays what a build added.
- **The translation.** A game frame is read when its envelope is understood whole and its message's declared fields
  read; understood whole when its message also encodes back to its length (D47). Bot code may use a frame read; D3B
  rewrites only a frame understood whole, and passes the others on as they came.
- **The gate** fails only when what bot code reads no longer fits: a field a build adds or changes elsewhere is counted.
- **Rejected.**
  - Every message whole, through all it holds: hundreds of messages to name for the milestone, and every change Ankama
    makes to a field the bot never reads would stop the host.

## D56: The readable protocol of the game

*2026-10-28, step 3, session 3g*

Files of our own in `src/D3B.Protocol/Game/`, package `d3b.game`, C# namespace `D3B.Protocol.Game`, matched within the
schema's `game.proto` (D43): `game`, the envelope, then one file per domain of the first milestone, `authentication`,
`characters`, `context`, `maps`, `movement`, `interactives`, `inventory`, `jobs`, `npcs` and `exchanges`. With the
connection phase's, the readable protocol holds 106 messages, 217 fields, 17 enums and 128 values; the game's carried
messages are 44, 33 messages of the game are partial (D55).

- **The envelope**, measured: every frame is a `GameMessage`, whose oneof holds a `GameResponse` at 1, a `GameEvent` at 3
  and a `GameRequest` at 4; its field 2, of the event's type and outside the oneof, is never set. A request and a
  response carry an `int32` and a `google.protobuf.Any`, an event an `Any` and a list of `int32` never set. The client
  sends requests only; the server events, and one response per connection.
- **The Any.** Its type URL is `type.ankama.com/` and the build's name of the message. The obfuscation leaves protobuf's
  own types as they are (D39), so `google.protobuf.Any` is its own counterpart, by its full name, for the matcher, the
  scores and the audit, and no mapping pairs it.
- **Numbers are 3.7.12.12's.** In the game file, 980 of the 982 messages of three fields or more number them 1 to n.
  Against the references' older build, 15 of the 18 messages that pair alone by the kinds of their fields number them
  otherwise, where the connection's keep theirs (D42). 110 of the game's messages hold two fields of one message type,
  of which the captures set one, and the metadata names types `decoy`. So the game's fields are likely shuffled and
  renumbered, with decoys, at each build. The readable numbers stay this build's: a mapping holds both numbers (D45), and
  the translation will rewrite them once a build renumbers, as D47 provides, measured on that build (Q10).
- **Names** as D42: from the references where the meaning is measured, in our own words, neutral elsewhere. A meaning is
  measured in captures, by structure and timing, and by values compared on the machine with the build's pack, such as an
  element's id with its map's elements; no value is written down.
- **Rejected.**
  - Numbers of our own and a rewriter now: fields could no longer be paired by number, which is the next build's work,
    with no second build to measure it on.

## D57: The game's anchors, and the messages its frames carry

*2026-10-28, step 3, session 3g. Extends D43.*

- **The plan.** `ReadableProtocol.GameAnchors`: every frame is the root, the client's frames are its requests, the
  server's its events, and a connection's first request carries the ticket's `AuthenticationRequest`, as every game
  connection did (D52). Nothing else is anchored: what follows depends on the player. An `AnchorPayload` names the
  message a side's n-th frame carries in an `Any`.
- **Type URLs.** The anchors read them, since they name the schema's types, and no other value. Every `Any` of every frame
  must name a message of the schema's file that reads its value whole, or the measurement reports it and the tool fails.
- **What the frames carry** is counted by side (`AnchorMeasurement.Payloads`), for the tool's proposals (D54).
- **Where.** The tool reads a captures directory's own files as the connection phase's, and its `game` directory as the
  game's (D53).
- **Measured** on 3f's four captures and one of this session's: 1,069 frames, whose root types 4,280 fields, the next
  1,912; anchored, the envelope, the request, the event and the ticket's request; 150 messages carried, 37 by the client
  and 113 by the server, each read whole as its type URL names it.

## D58: The translation reads a game frame through its type URL

*2026-10-28, step 3, session 3g. Builds what D47 planned for the game.*

- `ProtocolTranslation.DecodeGame` parses the envelope once, then the message its `Any` carries: the type URL's name is,
  through the mapping, a readable message, whose parser reads the value. `DecodedGameFrame` gives the envelope, the
  build's name, the readable type, the message, and whether the frame is understood whole or read (D55).
- A frame carrying a message the readable protocol lacks is named and not read; bytes no envelope reads have none.
- The relay does not read the game connection yet: bot state needs it at step 4, and nothing rewrites a game frame
  before Q9.
- **Measured** by the mapping tool, which decodes the game's captures with the mapping it just wrote: of 1,069 frames,
  178 understood whole, 69 more read, 822 carrying one of 106 messages the readable protocol lacks, none misread
  ([client.md](client.md#the-game-mapped)).

## D59: Mapping format 2

*2026-10-28, step 3, session 3g*

The readable protocol's contract changed with the game: `MappingFile.FormatVersion` is 2, written to `mapping-2.binpb`
(D45); `mapping.proto` is unchanged. A host of this version looks for `mapping-2.binpb` beside the pack, else in the
build's release (D48), which the release tool publishes (D50).

## D60: The bot sends the requests that start an action, and the client does what follows

*2026-10-28, step 3, session 3h, chosen by the owner. Settles Q9.*

Measured on the six captures of the game connection, then on a client of 3.7.12.12 in game through the host, D3B
sending 18 requests of the first milestone's eight kinds itself ([client.md](client.md#the-game-acted-on)):

- **Request ids.** Every request of the first milestone, the client's and D3B's, carries the id -1; so does every
  response. The client counts other requests, which the readable protocol lacks: in a session of 26 minutes, nine of
  four kinds, numbered 2 to 10 in turn: one sent once after the character list, one about every four minutes after two
  events of the server, and two together after 24 minutes. D3B's requests left that count as it was.
- **What the client does.** The server answered each request D3B sent as it answers the client's own, and the client
  showed it: the character walked, changed maps and harvested; the banker's dialog opened, then the bank, the stack and
  the kama moved into it, and the window closed. The client also sent what follows an action of its own: after each of
  five walks, the confirmation once arrived, 0.46 to 1.77 s later, which the server answered; after each of two map
  changes, the request for what is on the map, with its twin, 0.72 to 0.86 s later; once the server closed the bank D3B
  left, two requests to leave, within 40 ms, which the server did not answer. The player kept playing between D3B's
  requests.
- **What the server refuses.** A path that does not start where the character stands: one event, and no walk. A use it
  refuses: two events. A use of an element that the map's data places elsewhere than the server's state does: nothing.
  The readable protocol lacks these events.

So:

- **What D3B sends**: the requests that start an action, `MapMovementRequest`, `MapChangeRequest`,
  `InteractiveUseRequest`, `NpcActionRequest`, `NpcDialogReplyRequest`, `ExchangeTransferFromInventoryRequest`,
  `ExchangeKamasRequest` and `DialogLeaveRequest`, each whole (D55) and with the id -1, through the game session (D62).
  Never what the client sends after an action, the confirmation of a walk or the request for a map's information, and
  never a counted request.
- **Staying consistent.** The server's answers reach the client as they came, and the client follows them as after its
  own actions: no frame of the game is held, dropped or rewritten, and nothing is sent to the client. The bot learns
  that an action is over from what the server answers, such as the response to the client's confirmation, the map's
  information, or the end of a use; a refusal, or nothing, ends it after a delay. Bot code reads them at step 4, which
  names the refusals it needs in the readable protocol (D55).
- **The player** keeps the client: its own requests go on as before. How the bot and the player share the character is
  step 4's.
- **Within D2.** D3B sends the client's own requests, built whole, through the client's own connection; it hides
  nothing and imitates no timing.
- **Risk.** A client update could stop the client from following an action it did not ask for: the bot would then wait
  for a confirmation that never comes. The measurement is made again at each update, with the session of Q10.
- **Rejected.**
  - The bot driving every step, as BillBot does: its own confirmation after a walk's duration it computes, the client's
    then dropped by the relay. A duration formula, frames dropped, and every request the client sends after an action
    to know and to imitate, where the client sends them itself.
  - Counting D3B's requests: no request of the first milestone is counted.

## D61: D3B's frames go to the server between the client's, and captures record them

*2026-10-28, step 3, session 3h. Extends D37, D49 and D51.*

- **The seam.** `IRelayInterceptor.Intercept` takes the connection, `IRelayConnection`: its id, its route, a token
  cancelled once it closes, and `SendToServerAsync`, which sends the server a frame of D3B's own. Only towards the
  server: nothing D3B does sends the client a frame.
- **Where a frame goes.** The client's read loop and a sender take a gate in turn, the loop from a read's bytes to their
  flush. A frame goes at once when what went to the server ends on one of the client's frames, else as soon as the
  client's frame in progress has gone whole, so that the server reads every frame whole. Frames sent one after another
  go in that order; a sender that stops waiting withdraws a frame not gone. A connection closed, unwatched, or whose
  client's bytes are no frames, fails the frame with `RelayInjectionException`: where a frame could go is not known.
- **Captures, format 2.** A record of a new kind, `Injected`, holds each frame D3B sent, with its length, as it went:
  the server got the client's records and these, in their order. A format 1 capture reads as before.
  `CaptureReplay` gives them to a pipe of their own when asked, and leaves them out otherwise, as a replay through the
  relay, which sends its own, needs. `scripts/capture.cs` describes them apart; the mapping tool reads the client's own
  frames only, so its anchors are the client's (D57).
- **Measured.** The 18 frames D3B sent went whole between the client's, each a record of its own, and the server read
  every one: it answered all but the use it ignored (D60).
- **Rejected.**
  - Waking the client's read loop to write a frame, as the hold's timer does (D51): a cancelled read meaning one more
    thing, where a gate needs none.
  - Recording D3B's frames as the client's: a capture would say the client sent what it did not, and a replay through
    the relay would send them twice.

## D62: The game connection is read through the translation, for the modules that observe it

*2026-10-28, step 3, session 3h. Builds what D58 left to Q9.*

- **The relay's interceptor**, `RelayDecoder`, gives each connection phase its `ConnectionPhaseSession` (D49, D51) and
  each game connection a `GameSession`. It replaces `ConnectionPhaseDecoder`.
- **A game session** decodes every frame once through `DecodeGame` (D58), shows it to every `IGameObserver` in the
  services, and logs it at the debug level by the readable message it carries, or the build's name, its length and
  whether it was understood or read, never what it holds. It holds and rewrites nothing. An observer sees the session
  start, then each frame of each side, from the relay's read loops, so it must be quick and safe across threads, as
  posting to a bot's inbox is (D9); what it throws is logged and goes no further.
- **Sending.** `GameSession.SendAsync` encodes a readable request with `ProtocolTranslation.EncodeGameRequest`, then
  sends it through the relay (D61) and logs what it sent. The translation encodes only a message of the game that the
  frames carry, paired by the build's mapping and not partial: the envelope with the id -1, and an `Any` whose type URL
  names the build's message.
- **Measured.** The plugin that measured D60, never committed, was such an observer: it sent its requests through the
  sessions it was shown.
- **Rejected.**
  - A registry of sessions: an observer is shown each one as it starts.
  - A queue of frames per session: frames would pile up where no bot reads them.

## D63: Replays with injection

*2026-10-28, step 3, session 3h. Settles Q7, with D37.*

Tests build a game session as a capture, read by read, some frames cut across reads, and replay it through the relay,
read by a game session whose observer sends a request once the server said what is on the map. Each side's record goes
once the other side has all that was sent before it, D3B's request included, so every run is the same. They check that
the server gets the client's frames with D3B's request whole between them, that the client gets the server's bytes as
they came, that the observers see every frame both sides sent and none of D3B's, and that the relay's capture of the
replay gives each side what it got. The relay's own tests cover where a frame goes: between frames, after one in
progress, in order, withdrawn, refused.

- **Rejected.**
  - Replaying the captures of a real account: they hold its ticket and are never committed (D37).
  - Snapshots of decoded messages: what they would hold, the tests compare with what they built (D49); bot states are
    compared at step 4.

## D64: Step 4 in six sessions

*2026-10-28, step 4, session 4a, chosen by the owner*

The roadmap's step 4 holds the actor, the state, behaviors, interruption, recovery and reconnection, the sharing of the
character with the player, the server's answers and refusals, cell and world pathfinding, moving between maps, using
interactives and the bank: too much for one session. It is split:

- **4a**: the actor and walking on a map: one inbox per bot fed by an `IGameObserver`, a game connection tied to its
  account and its character, the character's state and its snapshots, how the bot and the player share the character,
  cell paths in `D3B.Engine` measured on the captures, the walk with the server's answer to the client's confirmation,
  and its refusal named in the readable protocol (D65 to D70).
- **4b**: between maps: world pathfinding over the graph's vertices by zone, scrolls, scrolls with an action and map
  actions, with the criteria the state answers; changing maps, what the bot waits for, and its refusals named.
- **4c**: interactives: using an element from the nearest cell the character reaches, harvesting, the use's refusals
  named, doors as transitions of the world path; line of sight here only if measuring shows using depends on it, else
  at step 9.
- **4d**: the bank: the banker's dialog, the exchange with the storage, stacks and kamas, leaving, the way to the
  nearest bank.
- **4e**: zaaps and havens: their messages, which the readable protocol lacks, from captures of the player using them
  and pins, then in the world path.
- **4f**: activities, interruption, recovery and reconnection: behaviors chained, a fight starting, a disconnection,
  full pods and the player as interruptions, the recovery step, the client's new connection taking the bot back.
- Until step 5's app, no command comes from outside: tests drive the bots, and the measurements on the host go through a
  measuring plugin never committed, as in session 3h.
- **Rejected.**
  - Interruption, recovery and reconnection right after 4b: they need the behaviors of 4c and 4d to chain.
  - The bank with zaaps and havens in one session: two sets of messages to measure and map.

## D65: One bot per character, its account known by the ticket

*2026-10-28, step 4, session 4a, chosen by the owner*

A bot plays one character of one account (`CharacterKey`: the account's id and the character's). It is made when a game
connection's server confirms the character the client chose, and outlives its connections: when the client connects
again and chooses the character, the new connection takes the old one's place, and the state starts over from what the
server sends.

- **The account, by the ticket.** The connection phase's identification names the account (`account_id`, its nickname and
  tag), then the server selection gives the client a ticket, which the game connection's first request presents (D52).
  The connection phase's session remembers the account by that ticket in `GameAccounts`, and the game session takes it
  from the ticket of its `AuthenticationRequest` (`GameSession.Account`). An account waits as its selection does, 60 s,
  and is taken once. The ticket stays in memory, never logged.
- **A connection whose account D3B does not know**, such as one whose identification was not understood, gets no bot:
  it is only watched, with a warning.
- **Why.** The state is a character's; step 5's profiles depend on the character's professions and level; and step 4f's
  reconnection resumes where a character's state allows, which a bot per connection could not.
- **Rejected.**
  - One bot per account: its state would start over at each change of character.
  - One bot per game connection: no continuity after a reconnection, which 4f would have to rebuild under another key.
  - The account carried by the relay, as a tag of `GameServerSelections`: `D3B.Network` stays free of the protocol, and
    the game session reads the ticket anyway.

## D66: The player wins, and the bot pauses until it is resumed

*2026-10-28, step 4, session 4a, chosen by the owner. Superseded in part by D81: leaving a dialog is the player's while
the character is in one.*

D3B neither holds nor drops a frame of the client (D60), so the player can always act. A request the player sends that
acts on the character interrupts the bot's behavior (D9), and the bot pauses: it starts no behavior until it is resumed,
by step 5's app, by tests until then.

- **What is the player's** (`PlayerActions`): an observer never sees D3B's requests (D62), so every request it sees is the
  client's. The requests that start an action are the player's: the eight kinds D3B sends (D60), and the stop of a walk,
  which the client sends when the player gives a new path during one, measured 24 times, each followed at once by the new
  path (client.md).
- **What is not**: what the client sends by itself after an action, its own or D3B's: the confirmation of a walk, the
  request for a map's information, the two requests to leave once the server closed an exchange (D60). Leaving a dialog
  is not counted yet: telling the player's from the client's needs the dialog's state, which session 4d adds.
- **Only while the bot acts.** The player acting while no behavior runs pauses nothing: the next behavior is an explicit
  command anyway.
- **Busy.** A behavior starts nothing while the character walks: the walk answers `Busy` (D69).
- **Measured** on a client in game through the host: the player gave a new path 300 ms into a walk D3B sent, the stop
  and the new path came, and the bot was interrupted and paused.
- **Rejected.**
  - Resuming by itself after some time without the player's requests: the bot would start again while the player reads,
    thinks, or is in a menu, which sends nothing.
  - The bot ignoring the player and planning again from the new state: the two would fight over the character.

## D67: The bot's run time

*2026-10-28, step 4, session 4a. Builds D9.*

- **The actor**, `BotActor` in `D3B.Bot.Runtime`: one unbounded `Channel<T>` read by one loop, so its state needs no lock.
  Its inbox gets the messages of its character's game connection, both sides, posted by `Bots` from the relay's read
  loops, and its loop turns each into the next state (`CharacterEvents`), then tries it on the behaviors' expectations.
  The class is not named `Bot`: inside every `D3B.*` namespace the name would be the `D3B.Bot` namespace.
- **The state**, `CharacterState` in `D3B.Bot.Characters`: an immutable record, replaced whole by the loop and read by
  anyone, holding what the server said: the character's name and level, its map, sub-area and cell, the path it walks,
  its inventory by stack, kamas, weight and most weight, and its professions with their levels and skills.
  `CharacterEvents.Apply` is a pure function from a state and a message to the next state: the state handlers, tested on
  their own.
- **Behaviors** are async methods given a `BotContext`: the state's snapshot, the pack's maps, the clock, the log,
  `Expect` and `SendAsync`. `BotActor.RunAsync` runs one at a time, only while a game connection plays the character and
  the player has not taken it over. An interruption cancels it, its token cancelled off the loop so that no behavior code
  runs on it, and it ends with `BotInterruptedException` and its reason: the player, the connection closing, the host
  stopping. The caller cancelling its own token is no interruption.
- **Expectations.** A behavior expects an answer before it sends the request: both go through the inbox in order, so no
  answer can come before the loop looks for it. The loop tries each message on the expectations once the state took it
  in, and an expectation ends with its behavior. A wait's delay goes through the bot's `TimeProvider`.
- **The registry**, `Bots`, an `IGameObserver`: it ties a game connection to its character's bot once the server confirms
  the choice (D65), raises `Added` for a character's first bot, and stops every bot with the host. The bots module
  (`BotsModule`) registers it, after the data and network modules, which it requires; `DefaultModules` holds it.
- **Tests** drive game connections as the relay shows them, on a pack of one map, with `FakeTimeProvider`, and wait for
  the loop through an internal `ProcessedAsync`.
- **Rejected.**
  - Behaviors run on the bot's loop, through a synchronization context of its own: a behavior awaiting on the loop it
    needs to go on would hang it, and D9 asks only that the state have one writer.
  - A bounded inbox: the relay's read loops must never wait for a bot.

## D68: Cells and paths in D3B.Engine

*2026-10-28, step 4, session 4a. Superseded in part by D86: on a map with stairs, a step changes floor by 10 at most but
along a stair.*

Measured on the eight captures of the game connection, 797 cells of the character's paths, and checked against the pack
(client.md):

- **Geometry** (`MapGeometry`, in `D3B.Engine.Maps`): a cell is its row times 14 plus its column, odd rows lie half a cell
  east of even ones, and directions run from 0, east, clockwise to 7. Every cell of the character's paths is walkable and
  the neighbour of the one before it, and the 310 turning points of the requests leave in the direction this gives.
- **Coordinates** (`MapPoint`): the server names a cell by two coordinates, x the column plus half the row rounded up, y
  the column less half the row rounded down, measured on its three refusals of a path (D69).
- **Paths** (`CellPathfinder`): A* over the eight neighbours a character may walk on, the destination walkable and the
  start anywhere. A step across, east, south, west or north, passes between two cells, one of which at least must be
  walkable: the client's paths never pass between two that are not, though the server walked one of D3B's that did.
  Steps are weighed by the time measured on the client's paths, running, 170 ms east or west, 110 north or south and 113
  along a diagonal: 3, 2 and 2. The estimate is the cost of the quickest path with nothing in the way. On the 67 paths the
  client asked for, these paths are as quick by these weights on 61 and quicker on 6, never slower, and the same cells on
  20. Nothing is kept between calls (D9's rejection of a shared pathfinder).
- **Turning points** (`CellPath.TurningPoints`): the first cell and each where the direction changes, with the direction
  leaving it, then the last with the direction arriving on it, as the client's requests name them.
- `D3B.Engine` references `D3B.Data` for the cells; its tests check the geometry, and with CsCheck (D15), on 100 random
  maps, that every path found is valid and as quick as an exhaustive search finds.
- **Rejected.**
  - Steps weighed alike: a path of fewer steps across the map is slower when its steps go east or west.
  - Passing between two cells that cannot be walked on, which the server allowed once: the client never does.
  - A benchmark of cell paths: 560 cells take microseconds; world pathfinding, at 4b, is where one pays.

## D69: The first behavior, walking on a map, and its refusal named

*2026-10-28, step 4, session 4a. Supersedes in part D60: the server's refusal of a path is an event of 40 bytes naming
the character's cell, not of 25.*

- **The walk** (`MapWalk`, in `D3B.Bot.Movement`): from the cell the state gives, a path in the pack's cells (D68), sent
  as the client sends its own, each turning point the direction times 4096 plus the cell (D60); then the server's answer,
  the client's confirmation once arrived, and the server's response to it, after which the character stands on the
  path's last cell. D3B never confirms: the client does (D60).
- **Outcomes** (`WalkOutcome`): arrived, on the response to the confirmation or already there; busy, while the character
  walks or its cell is not known, nothing sent; no path in the pack, nothing sent; refused; no answer from the server
  within 3 s; not confirmed within 1 s and 500 ms a step. Measured: the server answers within 150 ms, the client confirms
  0.38 to 2.9 s after the request, a step taking at most 186 ms running and 491 ms walking, and the server answers the
  confirmation within 100 ms.
- **The refusal**, named `MapMovementRefusedEvent`: a path that does not start where the character stands. One event,
  carrying the character's cell as coordinates (D68), 29 ms after the request, nothing else: measured three times, each
  naming the cell the character stood on. During a walk, the server places the character on the walk's first cell; a
  refusal then stops the client's walk there. The state takes that cell.
- **The stop**, named `MapMovementCancelRequest`: the client names the cell where the character stopped, then sends the
  player's new path from it, in the same read; the server does not answer. The state takes that cell (D66).
- **Pins and mapping.** The structure cannot tell either from the build's messages of its shape, 83 and 84 of them: the
  mapping tool proposed each among the 6 and 9 of these the captures carry, and they are pinned for 3.7.12.12 (D54). The readable protocol
  holds 108 messages and 220 fields: `MappingFile.FormatVersion` is 3, `mapping-3.binpb`, audited without failure.
- **Measured** on a client of 3.7.12.12 in game through the host, the player's hands off: the bot walked eight times
  through the walk behavior, 1 to 16 steps, each arrived in 0.41 to 1.84 s, the server walking every path exactly as
  sent; the path D3B sent from another cell was refused, naming the character's cell.
- **Rejected.**
  - A duration computed per step to end the walk, as BillBot does: the client's confirmation and the server's response say
    when it is over (D60); the delays only bound a wait that went wrong.
  - Waiting for the confirmation the client sends: the server's response to it is what makes the walk over for the
    server.

## D70: Bot states compared with snapshots written in the tests, not Verify

*2026-10-28, step 4, session 4a. Supersedes in part D15: the "Verify.XunitV3" part of the tests row.*

D15 chose Verify for snapshots, and the architecture planned to compare bot states with snapshots from step 4. Verify
33.3.4, the version D15 named, now fails the build without a SponsorCheck license property: a maintenance fee, or an
exemption its publisher defines and the consumer claims. Choosing one is the owner's call, not a test's.

- **So** a test serializes the state with `System.Text.Json`, indented, and compares it with the snapshot it holds as a
  raw string: the expected state reads in the test, and a change shows as a diff of text. Tests of the other handlers
  compare the fields they change.
- CsCheck 5.0.0, D15's property tests, and Microsoft.Extensions.TimeProvider.Testing join the tests' packages.
- Should the owner claim an exemption or sponsor, Verify can come back: the snapshots are text already.
- **Rejected.**
  - Claiming Verify's open-source exemption on the owner's behalf: a license decision.
  - `Verify_SponsorshipLicenseIgnored`: the build would knowingly breach the package's license.

## D71: Paths through the world in D3B.Engine, their criteria answered strictly

*2026-10-28, step 4, session 4b; the criteria rule chosen by the owner*

Measured on the world graph of 3.7.12.12 ([client.md](client.md#world-graph)):

- **The search** (`WorldPathfinder`, in `D3B.Engine.World`): Dijkstra's over the graph's vertices, each map change
  costing the same, to the nearest of the maps sought (`WorldPath`, its `WorldStep`s naming each transition by index).
  No estimate: map actions lead anywhere, so no distance between maps bounds what is left. Built once per pack, which
  reads the 619 criteria once and finds every vertex's sub-area and area; each search keeps its state in its own arrays
  (D9). The bots share one, built when one first travels.
- **What it follows**: scrolls, scrolls with an action and map actions. Doors are session 4c's, zaaps 4e's (D64). From
  the bank of Astrub this reaches 4,021 of the 11,327 vertices; with doors whose criteria are met it would be 5,551, and
  9,024 with every transition.
- **Criteria** (`Criterion`, `CriterionFacts`, in `D3B.Engine.Criteria`): each read once into a tree and answered on
  three values (`CriterionAnswer`): a group of `&` fails as soon as one condition fails, a group of `|` is met as soon as
  one is, and what is left is unknown. The syntax is measured: two-letter codes, an operator and a value, at times a
  context in brackets or a `#` before a group, and no group mixing `&` and `|`. The bot answers the codes its state holds
  (`CriterionCodes`): the level, the objects held, the professions, the subscription (D72), the map, sub-area and area,
  and the cell. Their meaning is the references' and the client's class names as they read them, not measured on the
  server; every other code, 40 of the 47 the graph names, is unknown.
- **Strict.** A transition is followed only when its criterion is met: unknown closes it, as false does. From Astrub it
  costs 67 vertices against ignoring criteria, 4,021 against 4,088.
- **The place.** A criterion is answered on the map its transition leaves. A condition on the cell counts as met while the
  path is planned, then names the cells the map is left from (D73).
- **Benchmark** (`benchmarks/D3B.Engine.Benchmarks`, on the installed build's pack, or `D3B_PACK`): a path 3 changes
  long in 2.2 µs, the farthest map in 154 µs, a map the search cannot reach, which explores everything reachable, in
  176 µs; about 100 KB a search. A journey can find its path again after every map change (D74).
- **Rejected.**
  - Following a transition whose criterion is unknown, then leaving it out once it fails: the bot would try passages a
    quest closes. The owner chose strict.
  - Ignoring criteria.
  - A* with a distance between the maps' coordinates: map actions jump across the world, so it would overestimate.
  - Weights by transition type or by the walk across each map: nothing measured says one change costs more than another.
  - A table of every pair of vertices: memory for nothing, at under a millisecond a search.

## D72: The account's subscription, read from its identification, prudently

*2026-10-28, step 4, session 4b, chosen by the owner*

- **Measured.** The connection phase's identification already holds `subscription_end` (D42): on the 11 captured, of
  two accounts, an ISO 8601 date with its offset, both accounts subscribed. No account without a subscription was measured.
- **Kept.** `GameAccount.SubscriptionEnd` holds the date, or nothing when the text is no date, and the state takes it
  (`CharacterState.SubscriptionEnd`). `IsSubscribedAt` answers by the bot's clock: subscribed while the end is ahead. An end
  not known counts as not subscribed.
- **Where it counts.** A path enters a sub-area the client closes to accounts without a subscription (`basicAccountAllowed`
  false: 533 of 567 sub-areas, 10,675 of 11,327 vertices) only when the account is subscribed, and `PZ` answers it (D71).
  What else an account without a subscription may not do is for later steps (roadmap).
- **Rejected.**
  - An end not known counting as subscribed: the bot would aim at zones the server closes to it.
  - Leaving the subscription for later: a path would cross subscribers' zones for every account.

## D73: Changing maps

*2026-10-28, step 4, session 4b*

Measured on the eight captures of session 4a and three of a client of 3.7.12.12 in game through the host, the player's
hands off, the bot asking for 30 map changes and travelling ([client.md](client.md#the-game-between-maps)):

- **Where a map is left from** (`WorldPathfinder.ExitCells`): a map action from its cell; a scroll from the walkable cells
  of its zone on the edge in its direction that the client marks as leaving it straight, or along a diagonal next to it
  but on a corner, and from the transition's own cell, which lies on that edge. Scrolls leaving one zone in one direction,
  to several zones or maps, share the edge by the nearest of their cells; a condition on the cell names a scroll with an
  action's own cells. The client left from such cells, the transition's own once in 12; the server took an edge cell
  marked nowhere, and ignored a request from a corner marked for the other edge and from a cell off the edge. Every
  scroll keeps at least its own cell, 18.4 on average.
- **The behavior** (`MapChange`, in `D3B.Bot.Movement`): the walk of D69 to the nearest of those cells, then, for a
  scroll, `MapChangeRequest` naming the transition's map, the neighbour even where the server sends the character
  elsewhere; for a map action, the walk onto its cell, stepping off first when the character stands on it. The bot then
  waits for the new map, 3 s, and for what is on it, 10 s, which the client asks for itself (D60). The new map is
  expected before the walk: the server's map actions cover cells next to the graph's, 424 for 409 and 154 for 140, so a
  walk to an edge may change maps by itself, and a request then goes unanswered.
- **No refusal to name.** The server answers nothing to a map change it refuses: 6 of the 30, from a cell off the edge,
  for the neighbour in another direction, for a map that is no neighbour, once the character was already elsewhere, and
  from a corner. Once, after a walk the client never confirmed, it answered with the refusal of D69, naming the walk's
  last cell. The readable protocol gains no message, its mapping stays format 3, and the change ends after its delay
  (`NoAnswer`).
- **The client draws the map first.** A walk the server showed 1 or 50 ms after a new map's information was never
  confirmed, though the server walked it: the server then has the character at its end, while the bot's state keeps it
  walking. Every walk sent 100 ms (twice), 200, 500 or 1,000 ms after was confirmed, and the player's own came 207 ms
  after at the soonest. The change is over 500 ms after the character is placed. This is no timing shaped to look human
  (D2): the client must have drawn the map to follow what the server shows it.
- **Outcomes** (`MapChangeOutcome`): changed, or elsewhere, once placed; busy and no path, nothing sent; not walked;
  refused, kept for a refusal a later measure finds; no answer; not placed.
- **Rejected.**
  - Every walkable cell of the edge, which the server took once: the client's marks are where its own player leaves, and
    a corner showed the server reads them there.
  - Waiting for a sign from the client that it drew the map: it sends none, measured.
  - A pause before every walk: only a new map needs one.

## D74: Travelling from map to map

*2026-10-28, step 4, session 4b*

- **The behavior** (`WorldTravel`, in `D3B.Bot.Movement`): the path from the vertex the character stands in to the maps
  sought, its first step (D73), then the path again from where the character stands, until it is on one of them. A scroll
  with an action may send it elsewhere, two zones may share an edge, and a search costs under a millisecond (D71).
- **Failures.** A change that fails leaves its transition out of the rest of the journey: its criterion may not be met
  for a reason the bot cannot know, or the server may refuse it silently (D73). The journey fails after 3 failed changes,
  after twice the first path's changes and 10 more, or when where the character stands is no longer known.
- **What the criteria are answered on** (`WorldTravel.FactsOf`): the state's level, objects by item, professions'
  levels and subscription at the bot's clock (D72).
- **Measured.** Through the host, with the player's hands off: journeys of one change; one of 6 changes in 27 s, one
  of them refused from a corner, left out, and the path found again through another map; one of 7 in 21.7 s, none
  failing. The first journeys, before D73's two fixes, failed on a request left unanswered after a walk to the edge had
  set off a map action, and on a walk the client did not confirm 1 ms after a map's information.
- **Rejected.**
  - Following the first path to its end: where a change leads is the server's to say.
  - Ending the journey at the first failure.

## D75: Using an element of the map

*2026-10-28, step 4, session 4c*

Measured on the captures of sessions 3g and 3h, then on a client of 3.7.12.12 in game through the host, the player's
hands off, a measuring plugin, never committed, sending uses from chosen cells ([client.md](client.md#the-game-used)):

- **Where the client stands.** On a walkable neighbour of the element's cell, one of the eight, the first its walk
  reaches, never on the element's cell even when it can be walked on; where no neighbour can be walked on, as by water,
  on one of the walkable cells nearest the element. It asks 1 to 5 ms after the server answered its confirmation.
- **What the server takes.** A use from no further than the skill's `range` (D29), counting the sum of the coordinates'
  differences (`MapGeometry.Distance`), or from a neighbour of the element's cell or the cell itself: fishing, range 10,
  was taken at 10 cells and refused at 11, three times, one of them only 6 cells off along either coordinate; the door,
  range 1, was taken from its own cell and from its west neighbour, 2 away, and refused from 2 and 3 away off its
  neighbours.
- **Line of sight does not count**: a fishing use 4 cells away, through a cell that lets no sight through on the
  straight line between them, was taken. It stays at step 9.
- **The map's elements are the map's own.** The map's information also lists the neighbouring maps' elements, marked by
  a field the references name `on_current_map`: in the maps' information of the 11 captures before this session, 104 of
  343 interactive elements and 92 of 232 stated ones, each of the 92 on another cell than the pack's, every other on the
  pack's. The server answered nothing to a use of one, three times, from next to either cell; nor to one of an element
  the map lacks. This was D60's unexplained silence.
- **Refusals**, named `InteractiveUseErrorEvent` and pinned (D54): 28 ms after the request, nothing else; two for a use
  from too far, the first naming the skill's id where the second names the instance's, five times; one for a skill the
  element does not offer now, as a resource harvested, or an instance it does not know.
- **A harvest** ends its duration later, in tenths of a second, to the millisecond, with the objects, the weight, the
  profession's experience and the element's new state within 2 ms, in no set order; the client sends nothing, and walked
  on 5 to 6 ms after. A walk sent during a harvest is refused, naming the character's cell, and the harvest goes on. A
  door's use has no duration: the new map comes in the same millisecond (D76).

So:

- **The state** keeps the map's own elements (`CharacterState.Elements`, `MapElement`): which interactive each is, its
  skills offered now and not, its cell and state, from the map's information and each update of an element it holds;
  and the use the character is in (`Use`), from the server's start of a use that lasts to its end. Walking, changing
  maps and using answer `Busy` during one.
- **Where to stand** (`ElementCells`, in `D3B.Engine.Maps`): the client's cells, the walkable neighbours of the
  element's cell in the character's zone, else the zone's walkable cells nearest it; the quickest of them is walked to.
- **The behavior** (`ElementUse`, in `D3B.Bot.Interactives`): from the state, the element's skill and the id the server
  gave it there, nothing sent when the map does not offer it; the walk (D69); the skill checked again, since another
  actor may have started harvesting; then `InteractiveUseRequest`, the start or the refusal within 3 s, and the end
  within the duration and 2 s. Outcomes (`UseOutcome`): used, busy, unavailable, no path, not walked, refused, no
  answer, not ended. The objects come as the state takes them in, which may be just after the end.
- **Rejected.**
  - Standing as far as the skill's range allows: fishing from 10 cells is no more use than the client's cell, and it
    would need the game's tables in the bots for the range, for nothing measured.
  - Keeping the neighbouring maps' elements in the state, marked: nothing can be done with them from here, and their
    stated cells are another map's.
  - Waiting for the element's new state to end a harvest, so that the objects are in: the end, the objects and the state
    come in no set order.
  - Checking the line of sight before a use: measured not to count.

## D76: Doors in the path through the world, their criteria answered strictly

*2026-10-28, step 4, session 4c; the criteria chosen by the owner. Extends D71 and D73.*

Measured on the world graph of 3.7.12.12 and the host's session of D75:

- **Doors.** 5,237 of the graph's 5,506 interactive transitions use skill 184, "Utiliser"; the 269 others use 49 skills,
  teleporters and the like, and open 42 more vertices. Every door's element stands on a cell of its map in the pack,
  with 1 to 8 walkable neighbours in the transition's zone; 50 transitions name another cell than the element's, mostly
  one that cannot be walked on. The server starts a door's use with no duration and names the new map in the same
  millisecond; the client asks what is on it 545 to 627 ms later.
- **Reached.** From the bank of Astrub, a subscribed character of level 200 holding nothing reaches 4,021 vertices
  without doors and 5,607 with them, strictly; 533 without a subscription. 7 of the 10 banks are reached, each with a
  door into it whose criterion the bot answers; quests close the doors of three other buildings at the same coordinates.
- **What the unknown codes hold back**, each family counted as met: achievements, `OA`, 1,266 more vertices; `Sc` and
  `SC`, whose meaning is not known, 550; quests, `Qa`, `Qf` and `Qo`, 248; others 41 or fewer; kamas, `PK`, and the
  breed, `PG`, none. Every unknown code met: 8,283.

So:

- **The path** follows doors, the interactive transitions of skill 184 (`WorldPathfinder.DoorSkillId`), criteria
  answered strictly as before (D71); the other interactive transitions stay out.
- **Leaving by a door** (`WorldPathfinder.ExitCells`, `MapChange`): from the cells the element is used from (D75),
  around its cell in the pack, a criterion on the cell answered for each; the walk, then the use of the skill the server
  offers on the element, then the new map as after a map action (D73). Nothing is sent when the server offers no
  door's skill on it; the refusal is `Refused`, which leaves the transition out of the journey (D74).
- **No new fact in 4c.** The bot answers the codes it answered (D71). Q12 keeps the measurements, for the large effort
  on criteria the owner noted.
- **Measured through the host**: a journey of four changes into the bank of Astrub, its door among them, in 11 s, and
  out by its map action.
- **Benchmark** (D71's): with doors, a path 3 changes long in 2.9 µs, the farthest map in 370 µs, a map the search
  cannot reach in 369 µs, 100 to 135 KB a search: twice D71's for the farthest, as the search explores more. A journey
  still finds its path again after every change (D74).
- **Rejected.**
  - Quests or achievements read in 4c: new messages to measure and pin, and the meaning of each code, for vertices the
    first milestone does not need. The owner chose to keep the doors strict.
  - Every interactive transition: teleporters and the like, not measured, for 42 vertices.

## D77: The bot sends nothing until the client drew the map

*2026-10-28, step 4, session 4c. Supersedes in part D73: a map change is over once the character is placed.*

- **Measured.** A door used as soon as the server named the new map, before the client asked what is on it: the server
  took it, and the client asked for both maps' information in turn, the server answering the second only. The bot's next
  walk, 55 s later, was never confirmed, though the server walked it; the player's next path was refused, naming that
  walk's end, and the client walked again from there. With D73's walks shown 1 and 50 ms after a map's
  information, never confirmed, a request the client must follow goes only once it has drawn the map.
- **So** `BotContext.SendAsync` holds any request until half a second after the server last placed the character
  (`BotActor.DrawDelay`), and `MapChange` no longer waits itself: the next request does. Behaviors send nothing before
  the character is placed: where it stands is not known until then.
- **Measured through the host**: the journey's walks went 533 to 549 ms after each new map's information, every one
  confirmed.
- **Rejected.**
  - The delay in each behavior after a map change, as D73 had it: a behavior started after the player's own map change
    would not wait.

## D78: Mapping format 4

*2026-10-28, step 4, session 4c*

The readable protocol gains `InteractiveUseErrorEvent`, pinned for 3.7.12.12 (D54), and `on_current_map` on the map's
elements (D75): 109 messages and 224 fields. `MappingFile.FormatVersion` is 4, `mapping-4.binpb`, 7,704 bytes, audited
without failure; the 13 game connections captured decode through it, 6,483 frames, none misread.

## D79: A bank's inside is found in play, and remembered by the host

*2026-10-28, step 4, session 4d, chosen by the owner*

Measured on the world graph of 3.7.12.12 ([client.md](client.md#banks)): from the map in front of each of the 10 banks,
doors lead to 1 to 5 maps; 16 lie at the bank's coordinates on no world map, its insides. 7 banks have one a door
without criterion leads into, 3 have two, of which only the server says which holds the banker, by the NPCs it places
there (D29). At Astrub, one door element leads to the bank and to another inside a quest closes.

- **The insides** (`WorldInsides`, in `D3B.Engine.World`): the maps a map's doors, skill 184, lead into at its
  coordinates on no world map, whatever their criteria, which the path answers strictly (D76).
- **The banks** (`BankDirectory`, in `D3B.Bot.Banks`): one per host, which every bot shares, built when a bot first
  banks from the pack's banks and bankers (D29) and the world. A bot that enters an inside tells whether a banker stands
  there; one where none stands is sought by no bot of the host, until one is seen there. Nothing is kept from one run of
  the host to the next.
- **The journey** (`Banking.GoToBankerAsync`): done where a banker stands; else to the nearest inside still sought
  (D74), then again from there, at most four journeys.
- **Rejected.**
  - Each build's insides pinned in a committed file, measured by visiting them: 10 banks to visit at every update, 3 the
    bot does not reach yet.
  - What the bot learns kept on disk: storage comes at step 5, and one more visit per ambiguous bank and per run costs a
    door and a map action.
  - The insides in the pack: the server still says which holds the banker, and the graph gives them at run time.

## D80: Banking

*2026-10-28, step 4, session 4d*

Measured on the captures of sessions 3g and 3h, then on a client of 3.7.12.12 in game through the host, the player's
hands off, a measuring plugin, never committed, sending the requests one by one, then the final behavior
([client.md](client.md#the-game-banked)):

- **Talking.** NPC action 3 on the banker, naming the map and his actor id, from 10 cells away: the dialog and its
  question in the same millisecond, 28 ms later, with two replies, one of which the pack lists among his. The bank of
  Astrub holds two bankers, NPCs 520 and 522.
- **Opening.** That reply ends the dialog and opens the exchange with the storage, its content and the character's kamas
  in the same millisecond, the opening paid with as many kamas as the storage holds stacks.
- **Putting in.** One request lists the stacks: the server takes those it may, answering with the inventory's stacks
  deleted, the storage's and the weight, and leaves the others, such as a quest's tickets, answering nothing when it
  takes none. Kamas by another request: the storage's, then the character's.
- **Refused**: an action on an NPC the map lacks, or during a dialog or an exchange, with an empty event, named
  `NpcActionErrorEvent` and pinned (D54); a reply not offered, with the question again and a text; nothing at all for a
  request naming another map, a reply, a stack or kamas outside the exchange, no kamas, more than the side holds, or a
  stack the inventory lacks. An action the banker does not offer opens an exchange that fails, with an event the bot
  never meets, not named.
- **Busy.** The server refuses a walk during the dialog and during the exchange, naming the character's cell, and the
  dialog or exchange goes on.

So:

- **The state** keeps the map's NPCs (`CharacterState.Npcs`), the dialog with one (`Dialog`, its question and replies)
  and the exchange with the storage (`Storage`, its kamas), from the server's events. The storage's objects are not
  kept: what the server sends as stacks are taken out is not measured. Walking, using, changing maps and travelling
  answer `Busy` during a dialog or an exchange.
- **The behavior** (`Banking`, in `D3B.Bot.Banks`): to a banker (D79); the action, then the question, 3 s; the banker's
  reply the question offers, then the storage's content, 3 s, a dialog it could not take to the bank left; the stacks
  asked for that the character carries and does not wear in one request, then the kamas, at most those carried, each 3
  s; then leaving, 3 s, the bank left whatever the deposit gave. Every request waits until the client drew the map
  (D77). Outcomes (`BankOutcome`): done, busy, no bank, not travelled, refused, no answer.
- **Measured through the host**: from inside the bank, two stacks and one kama put in and the bank left in 161 ms; from
  two maps away, the journey through the door, the bank opened 529 ms after the inside's information, a kama put in and
  the bank left, in 3.9 s.
- **Rejected.**
  - One request per stack, as the player drags them: the server takes a list, measured.
  - Choosing what to put in, such as the kamas kept for the next opening: the caller's, as step 6's profiles will be.
  - Walking next to the banker first: the server took the action from 10 cells away.

## D81: The player's leaving is told from the client's by the dialog

*2026-10-28, step 4, session 4d. Supersedes in part D66: leaving a dialog is now the player's while the character is in
one.*

- **Measured.** The player closing the banker's dialog or the bank: the client's request to leave, while the dialog or
  the exchange is open, then the server's end 28 to 60 ms later. D3B leaving the bank: the server's end, then the
  client's two requests by itself, 1 to 178 ms later, six times; none once, after a bank D3B opened and left in 161 ms.
  The relay shows each frame of the server to the observers before the client gets it, so a bot's loop sees the end
  before the client's requests.
- **So** `PlayerActions.Starts` takes the state: a request to leave is the player's while the character is in a dialog
  or an exchange, and the client's own once the server ended it. Measured through the host: the player closing the bank
  the bot had opened interrupted the bot, which paused.
- **Rejected.**
  - A delay after the server's end within which a request to leave is the client's: the state already says it.

## D82: Mapping format 5

*2026-10-28, step 4, session 4d*

The readable protocol gains `NpcActionErrorEvent`, pinned for 3.7.12.12 (D54), and names the dialog and exchange values
measured on the bank: 110 messages and 224 fields. `MappingFile.FormatVersion` is 5, `mapping-5.binpb`, 7,743 bytes, the
same bytes on two runs, audited without failure; the 15 game connections captured decode through it, 8,851 frames, none
misread. The release of 3.7.12.12 needs it, which the release tool publishes (D50).

## D83: The path through the world teleports from where the character stands

*2026-10-28, step 4, session 4e, chosen by the owner. Extends D71, D73 and D74. Amended by D167: a teleport may count as
more map changes, and never ties a walk.*

Measured on a game connection of the player using zaaps, the haven bag and the map, then on a client of 3.7.12.12 in
game through the host, the player's hands off, a measuring plugin, never committed, sending the requests one by one
([client.md](client.md#the-game-teleported)):

- **Three ways to one request.** Using a zaap, skill 114, lists the zaaps the character knows with their costs; using
  the haven bag's zaap lists them too; and the client, when the player chooses a zaap on its map, teleports with no list
  open, the teleporter's type 5. Each teleport is one request, answered 28 to 33 ms later, once 96, with the new map,
  the kamas paid and the dialog over; the client asks what is on the map 0.66 to 1.36 s later, after D3B's teleports as
  after the player's.
- **From where.** Teleporting from the map was taken from 6 maps whose flags carry bit 3, a bank's inside and maps
  without a zaap among them, and refused from one without it and from the haven bag; 10,587 of the graph's 11,327
  vertices carry it, every zaap's map among them. The haven bag opens where bit 2 is set, 3 maps measured, refused on 2
  without it; 136 vertices carry bit 2 and not bit 3.
- **What it costs.** Ten kamas a whole coordinate of the straight line between the map the character stands on and the
  zaap's, and a thousand more towards Incarnam's world map: the 120 costs of four lists and the 13 teleports paid. The
  haven bag's list costs from the map the bag was entered from, so it saves nothing. Bubble's formula, ten kamas a tenth
  of a coordinate, gives costs that are not tens, and the server's are.
- **Known.** The server lists the zaaps the character knows on entering the game, and again, whole, when it enters the
  map of one it did not know; a teleport to any other map is refused.

So:

- **The path** (`WorldPathfinder`, owner's choice): from every vertex whose map carries bit 3
  (`MapPosition.AllowsTeleport`), a teleport to the zaap of every map the character knows one on
  (`WorldPathRules.KnownZaaps`) whose cost it carries (`WorldPathfinder.TeleportCost`, `WorldPathRules.Kamas`), one map
  change like any other (D71); a step that teleports has no transition (`WorldStep.IsTeleport`). It lands on the zaap's
  map's only vertex: the one zaap's map with two zones, the Craqueleurs mountain's, is left out, where the character
  lands there not measured. Only a subscribed account teleports, prudently: no account without a subscription was
  measured (D72). The kamas towards other world maps than Incarnam's are not measured, and counted as Incarnam's.
- **The behavior** (`MapChange`): `TeleportRequest` from the map, then the new map and what is on it, as after a scroll,
  every request waiting for the client to draw the map (D73, D77); an error text refuses it (D84), and a refused or
  unanswered teleport leaves every teleport out of the rest of the journey (`WorldTravel`, D74).
- **The state** keeps the zaaps the character knows (`CharacterState.KnownZaaps`), the zaap's dialog with its costs
  (`ZaapDialog`), which leaving ends and during which walking, using, changing maps and travelling answer `Busy`, and
  the haven bag the character is in, by its owner (`HavenBagOwnerId`). A teleport or the haven bag's request the player
  sends is the player's (D66).
- **The haven bag**: a journey from the character's own leaves it first, back to where it was entered, free
  (`MapChange.LeaveHavenBagAsync`); from another's, which is not measured, it finds no path. The path does not travel
  through it.
- **Measured through the host**, the player's hands off: from an inside of Amakna's castle, which no one teleports from,
  to Astrub, out by its door, a teleport to Astrub's zaap and a scroll, 3 changes in 6.1 s; from the haven bag to
  Sufokia's zaap, the bag left and a teleport, in 2.9 s; from there to the nearest banker, a teleport to the Cœur
  immaculé's zaap, a scroll and a door, in 6.6 s, every walk confirmed and every cost the one the rule gives.
- **Benchmark** (D71's, `benchmarks/D3B.Engine.Benchmarks`): every zaap known and the kamas carried, the farthest map in
  591 µs and a map the search cannot reach in 638 µs, about 190 KB a search, against 266 and 281 µs on foot, as
  teleports open more of the graph. A zaap the search reached once leaves its list: every teleport costing the same, no
  later vertex brings it nearer; looking at every zaap from every vertex took 1.45 ms. A journey still finds its path
  again after every change (D74).
- **Rejected.**
  - Walking to a zaap and using it: two to three steps more for the same destinations and costs.
  - The haven bag and its zaap: two steps a teleport, for 136 vertices more.
  - All three: much more code and tests, for 1.2% of the graph.
  - Weighing kamas against map changes: nothing measured sets the rate; step 6's profiles may.
  - Asking the server what a teleport costs first, as the client does before the player's: a request that starts no
    action (D60), whose answer the measured rule gives.

## D84: The teleports' messages named, their refusals texts: mapping format 6

*2026-10-28, step 4, session 4e*

- **Named and pinned** for 3.7.12.12 (D54), each among the messages its side carried once the player teleported:
  `ZaapKnownListEvent`, `TeleportDestinationsEvent`, partial, `TeleportRequest`, `HavenBagEnterRequest` and
  `TextInformationEvent`, partial; with them, the teleporter's type (`TeleporterType`: a map's zaap 0, the haven bag's
  3, from the map 5), the haven bag in the map's information (`HavenBagInformation`, a oneof's member the other maps
  leave unset), and the dialog's type 10, a zaap's (`DIALOG_TYPE_TELEPORT`).
- **Refusals.** The server refuses a teleport with a text of the error kind, 28 ms after the request, and nothing else:
  474 during a dialog or a walk the client did not confirm, 1759 to a map the character knows no zaap on, 416 from a map
  no one teleports from or from the haven bag; 471 the haven bag where it does not open. A teleport of type 0 with no
  list open gets no answer. The bot reads the kind, not the text.
- **Not named**: the client's question of what a teleport costs, before the player's from the map, and its answer: the
  bot does not ask (D83).
- **Mapping.** The readable protocol holds 117 messages, 241 fields, 19 enums and 147 values.
  `MappingFile.FormatVersion` is 6, `mapping-6.binpb`, 8,304 bytes, the same bytes on two runs, audited without failure;
  the 18 game connections captured decode through it, 11,504 frames, none misread. The release of 3.7.12.12 needs it,
  which the release tool publishes (D50).

## D85: A walk the client does not follow leaves the character where the server has it

*2026-10-28, step 4, session 4e, chosen by the owner. Supersedes in part D69: a walk not confirmed now ends with the
character on its last cell. Extended by D89: the server may still hold that walk, which a step aside lets go.*

- **Measured** ([client.md](client.md#the-game-teleported)): of the 104 walks D3B sent in the 18 captures, the client
  confirmed 97, the player stopped one, and the client never confirmed 6, which the server walked: two shown 30 and 90
  ms after a map's information (D73), one after a door used too soon (D77), two in the bank of Astrub 81 and 301 s after
  its information (4d), and one in 4e, 0.53 s after the information of a map the client had never drawn, along 14 plain
  cells. The server then answered a teleport and the haven bag as busy, 9 and 11 s later, and in 4d refused the player's
  path, naming the walk's last cell. A walk sent from that cell 5 minutes later was walked, and the client first walked
  its character there from where it had kept it, then confirmed. The cause is not found: of the walks sent 0.5 to 0.6 s
  after a map's information, the client confirmed 36 of 37.
- **So** `MapWalk`, once the client did not confirm in time, has the bot's loop take the walk's last cell
  (`BotContext.SettleWalkAsync`, `CharacterEvents.NotConfirmed`); the outcome stays `NotConfirmed`, and a change or a
  journey goes on from there, leaving out what failed (D74), instead of answering `Busy` until the server's next
  refusal.
- **Risk.** The server may still hold the character walking, and refuse the next request once.
- **Rejected.**
  - A longer delay before the first request on a map: nothing measured says it prevents this, and two came minutes
    after.
  - Leaving it to session 4f: a journey stopped at each one, the owner's choice.

## D86: Floors count on a map with stairs

*2026-10-28, step 4, session 4f. Supersedes in part D68: a step's floors may forbid it.*

Measured on the pack of 3.7.12.12, the 18 game connections captured before this session, then on a client of
3.7.12.12 in game through the host, the player's hands off, a measuring plugin, never committed, sending chosen paths
([client.md](client.md#the-game-interrupted)):

- **The pack.** A cell's `moveZone` is 0 or 1, 1 on 9,800 cells; 633 maps hold both, 469 of them in the world graph, and
  their cells of moveZone 1 are stairs. Every walkable cell's floor is a multiple of 10.
- **The captures.** On maps of one moveZone, the player walked steps of 10 to 60. On maps of both, the player's client
  and others' walked steps of 30 along a stair, and of 10 between a stair and a cell of moveZone 0. The path refused in
  session 4e stepped from 70 on a stair to 0 off it.
- **The server**, 14 paths each ending on the step tried, every refused one walked again without that step: on a map of
  both moveZones, along a stair, 130 both ways walked; between moveZones 0 and 1, 10 walked, 20, 30, 40 both ways, 70
  and 130 refused; between two cells of moveZone 0, 10 walked, 20, 40 and 130 refused. On a map of one moveZone, 170
  walked. The client confirmed every path the server walked.

So:

- **The rule** (`CellPathfinder`): on a map whose cells hold both moveZones, a step between two cells that are not both
  of a stair, moveZone 1, changes floor by 10 at most; elsewhere floors do not count. The difference is the floors'
  own: Dofus 2's, of their absolute values, gives the same on every pair of the pack. It leaves out 5,655 pairs of
  walkable neighbours, on those 633 maps.
- **Tests**: the steps of each kind, and the property tests on random maps of floors 0 to 30, half with stairs, against
  an exhaustive search applying the rule.
- **Measured through the host**: the journey off the upper level of Sufokia's zaap's map, which the old paths failed
  after three refusals, in 5.3 s, no refusal; then 85 journeys of an activity between that map and the one below its
  stairs, every one arrived.
- **Rejected.**
  - BillBot's rule, Dofus 2's: no change of floor at all between moveZones. The client and the server take 10.
  - Weighing a change of floor in a step's cost: nothing measured says it takes longer.

## D87: Activities, steps chosen from the state

*2026-10-28, step 4, session 4f, chosen by the owner, who asked for a model both permissive and practical, that holds
for a master and its followers. Builds D9.*

The owner's profession scripts in `vendor/metiers` ask the bot for the same: their `move()` is called on every map and
chooses there, harvesting, changing maps, or `bank()` once `inventory:podsP()` passes their `PODS_THRESHOLD`; they
know mules. BillBot's behaviors, read for ideas (D3), resume by deciding again from the game's state with steps that
can run twice, check their stop conditions at safe points, and pass a gate before acting; they also give up a wait and
carry on as if it had worked, treat a reconnection as the fix for any desync, and share global state between bots, which
D3B does not take. BillBot has no master and followers.

- **An activity** (`BotActivity`, in `D3B.Bot.Activities`), written by a module or a plugin (D90): `StepAsync`, a
  behavior's worth of work chosen from the state when the step starts, saying whether the activity goes on. Every other
  hook has a default, so that an activity overrides what it needs: `IsFull`, never, and `UnloadAsync`, unable to;
  `OnInterrupted`, resume; `RecoverAsync`, the default recovery (D89).
- **Its run** (`ActivityRun`, `BotActor.StartActivity`), one per bot: each step, and each unloading and recovery, a
  behavior the bot starts once it can act, a game connection playing the character, placed on a map and out of a fight,
  the player not having taken it over (`RunWhenReadyAsync`). Until then it waits, with no delay. Before each step, a
  full character is unloaded instead, and a character still full once unloaded fails the activity. An interruption
  cancels the step (D9); the activity resumes, or stops, as `OnInterrupted` says; the host stopping ends it, as
  `Stop` does. It says what it does (`ActivityStatus`: waiting, stepping, unloading, recovering, over), its steps, its
  last interruption, and how it ended (`ActivityOutcome`: done, failed, stopped).
- **No position kept.** The next step starts from the state, so the activity resumes where the state allows: after a
  pause, a new connection or a fight, as after a journey that went elsewhere.
- **For groups**, at step 10: anyone may wait for what a bot's state becomes (`BotActor.WaitUntilAsync`), tried by its
  loop now and after everything it handles, as a follower's step will wait for its master's map; a behavior waits on
  its own state with a delay (`BotContext.WaitUntilAsync`).
- **The name**: `BotActivity`, not `Activity`, which `System.Diagnostics` holds, as D67 named the actor.
- **Measured through the host**: a patrol between two maps, 85 journeys; the player's walk interrupted it and paused the
  bot, resumed 22 s later it recovered and went on; full, it went to the nearest bank, put a kama in and left, in
  6.6 s, then went on; the client closed during a journey, the new connection 20 s later took it back, and it went on
  2.2 s after the server named the map, with no command.
- **Rejected.**
  - An activity as one long behavior, started again after each interruption: the run time would see nothing between
    steps, full pods left to every activity, and no step for step 5's app to show.
  - A state machine or a tree of behaviors, with flags: what D9 left out.
  - A position the activity saves to resume from: the server's state is what holds.
  - A delay after which a waiting activity gives up: the client or the player may come back much later.

## D88: A fight is an interruption, until step 9 fights it

*2026-10-28, step 4, session 4f*

Measured on a fight in a capture of session 4d and one through the host, the player attacking monsters
([client.md](client.md#the-game-interrupted)):

- **The start.** The client walks to the group, then sends its confirmation and a request the readable protocol lacks,
  the attack, in one read. The server answers the confirmation, replaces the roleplay's context with the context of
  value 1, and names the fight's own map, a map of the pack off the world graph, at 0,0. It sends no map information:
  the client's request for one gets the fight's.
- **During it**, the client moves the character with `MapMovementRequest`, on the fight's map.
- **The end**: the loot, the context of value 0 again, the map the fight began on, then its information, placing the
  character.

So:

- The value is named `GAME_CONTEXT_FIGHT`. Value names stay out of the readable protocol's digest (D45): mapping 6's
  pairs are the same, only the tool's version differing on a new run, so the format stays 6 and the release keeps its
  mapping.
- **The state** says the character fights (`CharacterState.IsFighting`), and `IsBusy` gathers what makes every behavior
  answer `Busy`: a walk, a use, a dialog or exchange, a fight.
- **The bot** interrupts its behavior once the server creates a fight's context (`Interruption.Fight`), and pauses
  nothing; an activity waits for the fight's end and its map's information, then recovers and goes on.
- The player attacking a group walks first, which pauses the bot (D66) before the fight.
- **Not measured**: a fight lost, and a fight beginning during a step, as an aggression would: the tests cover the
  second. Step 9 fights; its strategies will be what an activity runs on this interruption.
- **Rejected.**
  - Pausing the bot on a fight, as on the player: the owner's model resumes once the fight is over.

## D89: Recovery, the client's new connection, and a walk the server holds

*2026-10-28, step 4, session 4f. Extends D85.*

Measured through the host ([client.md](client.md#the-game-interrupted)):

- **A disconnection.** The client closed 1.7 s into a walk it had not confirmed: on its new connection, 22 s later, the
  server placed the character on the walk's first cell, and walked the next path at once. During an activity's journey,
  the new connection came 20 s later, and the activity went on as above (D87).
- **A walk the server cuts.** On one map, a scroll's walk to the edge passes a map action's cell, where the server stops
  it: of 43 such walks, the client followed 24 at once, confirming them 0.43 to 0.44 s later, and 18 only once D3B had
  sent another walk, the server then refusing that one by the first cell of the cut walk, not its last, and the
  client's confirmation setting off the map action. Both kinds were sent 0.50 to 0.51 s after the map's information.
  The journey arrived every time, each such walk costing about 3.5 s. This is likely D85's walk the client does not
  follow; its cause is not found.
- **A request naming no cell**, measured once: refused, then the server closed the game connection, 83 ms after.

So:

- **The recovery** (`Recovery.SettleAsync`, every activity's default): a use goes on to its end, 30 s at most; a walk
  to its confirmation, else it settles where the server has the character (D85); a dialog or an exchange still open is
  left; a walk the server holds is let go of. Each wait is bounded, and the next step finds the state as it is.
- **A walk the server holds** (`CharacterState.IsWalkHeld`): set once a walk the client did not confirm settled (D85),
  cleared by the next walk, refusal or map. The server refuses what is not a walk from that cell, measured in 4b, 4d and
  4e, and walks one from there. So `MapWalk.ReleaseHeldWalkAsync` steps to a neighbour first, before a map change, a
  teleport, leaving the haven bag, using an element or talking to the banker, and a walk to the cell the character
  stands on steps aside and back.
- **No walk of no step**: `MapWalk.Request` refuses a path that does not move, which the server answers by closing the
  connection.
- **Risk.** A walk the client does not follow is still not understood: the state may take the wrong cell until the next
  refusal, as on the map action's cell, and a journey loses seconds. The step aside rests on one measured fact, a walk
  from the held walk's cell walked; no held walk was provoked to test it.
- **Rejected.**
  - D3B reconnecting the client: the client logs in by itself (D23).
  - A longer delay before the first walk on a map: the walks followed and those not were sent within the same 10 ms.

## D90: Plugins, not Lua

*2026-10-28, step 4, session 4f, chosen by the owner. Supersedes D12; drops Q1 and step 7. Superseded by D102: Lua
scripts replace the plugins.*

The owner does not want Lua scripts: routes and routines are written as .NET plugins, like every feature (D5, D6, D21),
their activities `BotActivity` (D87).

- **So** there is no Lua host and no script API: `src/D3B.Scripting`, an empty project, is removed. Q1, whether
  LuaCSharp holds up, has no object. Step 7 is dropped and keeps its number, as step 8 did. Step 13 turns the owner's
  profession scripts in `vendor/metiers` into plugins.
- **What a plugin sees of the bots** becomes what writing an activity needs: Q11, still settled before step 6
  publishes the package.
- **Rejected.**
  - Lua beside plugins, as D12 had it: a second API to keep, which the owner does not want.

## D91: The bots run in the app's process

*2026-10-29, step 5, session 5a, chosen by the owner. Supersedes D7.*

The owner asked why the app should not run the bots itself. The lead recommended keeping D7: with D3B in the middle of
every connection (D23), the process that holds the relay holds every client's game connection, so a fault of the app,
or closing its window, would disconnect them all, and the sessions writing the app restart it often. The owner chose
the app.

- **So** the Avalonia app runs the bots' host in its own process (`HostRunner`, in `D3B.App.Hosting`): it starts it
  with the app, off the UI's thread, shows where it stands and why it did not start, and stops it once the window is
  gone, the clients getting their configuration back (D35). There is no gRPC API: `D3B.Contracts`, an empty project, is
  removed, as `D3B.Scripting` was (D90).
- **The host's composition is shared** (`BotHost`, moved from `D3B.Host` to `D3B.Bot.Hosting`): the default modules then
  the plugins, the pack and the mapping got ready, the relay started, or why not (`BotHostStart`). `D3B.Host` keeps its
  entry point alone, the bots without the app, for measurements; its tests move to `D3B.Bot.Tests`, and
  `D3B.Host.Tests` goes.
- **Risk, accepted by the owner.** Closing the app, or the app failing, disconnects every client; after a crash,
  `scripts/client.cs restore` gives the client its configuration back. A remote front, which D14 kept possible, would
  need an API again.
- **Measured**: closing the app gave the client its configuration back and closed its game connection, as stopping
  `D3B.Host` does (D94).
- **Rejected.**
  - Keeping D7, the lead's recommendation: its cost was an API, about one session, then a few calls per page.
  - The app starting `D3B.Host` as a process of its own: the same separation, which the owner did not want.

## D92: Step 5 in five sessions

*2026-10-29, step 5, session 5a, chosen by the owner. Replaces the six sessions the owner chose earlier in the same
session, which began with the host's gRPC API, before D91.*

- **5a**: the app with the bots in it: Avalonia, ShadUI and CommunityToolkit.Mvvm, the host in the app's process (D91),
  French and English from the start, the bots page, and the catalogue it starts activities from (D93, D94).
- **5b**: what a plugin sees and declares: Q11, and Q6, which D91 makes simple: a plugin's page loads in the app's
  process, registered by its module as D5 planned. The sample plugin writes an activity and a page.
- **5c**: accounts and profiles in SQLite, EF Core and its migrations, and their pages. D3B sees no password since D23:
  whether Data Protection has a secret to protect is decided there.
- **5d**: the live map, read from the pack by the app.
- **5e**: logs and the protocol inspector: the logs kept in memory, and a bot's game frames decoded, live with the
  ticket hidden (D65), or replayed from a capture (D63).
- **Rejected.**
  - Q11 at the start of step 6: 5c's profiles depend on how a plugin's activity declares its settings.

## D93: Activities the app starts, from a catalogue the modules fill

*2026-10-29, step 5, session 5a. Builds D87. Its point on plugins is superseded by D95: they reach `D3B.Bot`, and
register activities.*

- A module registers an activity with `services.AddActivity<T>(id, name)` (`ActivityServiceCollectionExtensions`, in
  `D3B.Bot.Activities`): an `ActivityDefinition`, its id unique in the host, its name as the app shows it, and how one
  is made: a new one for each start, its constructor given the host's services, since an activity runs once.
- The bots module registers the `ActivityCatalog` of them, in the order they were registered. An id registered twice
  fails while the modules build, so a plugin reusing one stops the host, as an invalid plugin does (D21).
- The app starts the activity chosen on a bot (`BotActor.StartActivity`), stops it (`ActivityRun.Stop`), and resumes
  the bot the player took over (`BotActor.Resume`, D66). An activity started by other code shows by its own name.
- Plugins cannot reach `D3B.Bot` yet (D22): until Q11, in session 5b, D3B's modules and a measuring plugin compiled
  against the projects register activities. D3B writes its own from step 6.
- **Rejected.**
  - Activities found by reflection over the loaded assemblies: a module says what it brings, in code (D21).
  - An activity's settings now: they come with what a plugin declares (5b) and with profiles (5c).

## D94: The app's first version

*2026-10-29, step 5, session 5a; dark by default, chosen by the owner, who asked for a real effort on the interface,
then for a page more practical and pleasant than three panes, its design left to the lead. Builds D14. The theme and the
language are remembered since D101. Superseded in part by D161 and D162: Lucide's icons, the sidebar, and the
language and the theme on the settings page alone.*

- **The stack** (D15): Avalonia 12.1.4, ShadUI 0.2.4, CommunityToolkit.Mvvm 8.4.2, and Avalonia.Fonts.Inter 12.1.4,
  Inter being the typeface ShadUI's design comes from, the same on every machine. Avalonia's packages ShadUI brings are
  pinned at 12.1.4, ShadUI asking for 12.1.0. `D3B.App` targets `net10.0` (D19).
- **The window**: dark, light on a click, which nothing remembers before 5c's storage. No sidebar while there is one
  page: where the host stands is in the title bar, beside the language and the theme. While the host gets ready, a
  spinner; when it did not start, why, in a block the owner can copy, since it often names a command to run; then the
  bots page.
- **The bots page**: a card per bot, in the order they came, in as many equal columns as the window's width holds
  (`CardGrid`), so that every bot shows whole, its actions on it, nothing to select. A card says who: initials with a
  dot of the bot's status, name, level, connection; where, the coordinates in large figures, as players give a place,
  the sub-area named in the app's language from the pack's texts and the cell, the map's id in a tooltip; what it
  carries, pods with a bar that turns to the warning tone past 90 %, a hint only, each activity deciding when it
  unloads, and kamas; what it is in the middle of. At its foot, the activity: the player's pause with the button that
  resumes it, what the activity does or how it ended, its steps and last interruption, the button that stops it, or the
  list to choose one from and the button that starts it. Figures are tabular. Every status has a tone (`Tone`):
  success, warning, info, danger or neutral. The icons are the app's own line drawings (`Icon`).
- **Reading the bots**: the window reads every bot's snapshot each 250 ms, on the UI's thread (D9). The bots' loops
  never wait for the app, and raise nothing to it.
- **Translation**: resources in English (`Strings.resx`) and French (`Strings.fr.resx`), the same keys, which a test
  checks. The language starts as the system's and changes live: views bind through `{l:Tr Key}`, which follows it
  because `Localizer` raises the indexer's own name, `Item`, the only one Avalonia 12.1.4's bindings to an indexer
  listen for, not WPF's `Item[]`: the test drawing the window found it. A text that counts takes the singular or the
  plural as its language has it, French's 0 singular. The host's own messages, such as why it did not start, stay in
  English.
- **Tests**: the view models against bots played as `D3B.Bot.Tests` plays them (D67), the runner against a test
  install, and the window drawn by Avalonia.Headless with Skia, its texts read in both languages.
- **Measured** on a client of 3.7.12.12 through the app, with a measuring plugin, never committed, whose patrol goes
  between the character's map and a neighbour ([client.md](client.md#the-game-from-the-app)): the bot showed once the
  character was chosen; started from the app, 12 journeys arrived; the player's walk paused it twice, resumed from the
  app it went on; stopped from the app; closed, the app gave the client its configuration back.
- **Rejected.**
  - Three panes, a sidebar of pages, the list of bots and the one chosen in full, the first layout shown: the owner
    found it neither practical nor pleasant.
  - A table, a row per bot: denser, but a row cannot hold the pause and the activity's controls without crowding.
  - A tab per bot, as the bots sold do: one bot at a time, when the owner runs several.
  - The window told of each change of a bot: a handler on the bots' loops and a dispatch per message, for a page read
    four times a second.
  - Changing the language through a restart: the bots live in the app now (D91), and a restart disconnects them.
  - An icon font, as ShadUI's demo uses Lucide's: a binary asset and its notice, for a dozen icons.

## D95: Plugins compile against the D3B.Bot package

*2026-10-29, step 5, session 5b, chosen by the owner. Settles Q11. Supersedes D22 in part: D3B.Core is no longer the
only package, nor what a plugin references. Superseded by D102: there are no plugins to compile.*

What a plugin needs to write an activity is the bots' run time itself: `BotContext` names the pack's maps
(`D3B.Data`), the paths through the world (`D3B.Engine`), the side a message came from (`D3B.Network`) and the readable
requests (`D3B.Protocol`); the state is built from the protocol's messages; and the behaviors an activity is made of,
walking, travelling, using an element, banking, live beside the actor.

- **So** `D3B.Bot` and the four libraries its API names are packable beside `D3B.Core`, at the same MinVer version:
  `dotnet pack -o <feed>` at the repository's root packs the six, and nothing else. A plugin references `D3B.Bot` with
  `ExcludeAssets="runtime"`, which brings the five others and their packages, runtime excluded too. Measured: a plugin
  built from the template holds only its own files and its French satellite assembly; `D3B.Network`'s framework
  reference to ASP.NET Core reaches its `runtimeconfig.json`, harmless, the host running it.
- **What a plugin sees** is the public API of those six projects: the state (`CharacterState`), activities
  (`BotActivity`) and their context, the behaviors (`WalkAsync`, `TravelAsync`, `UseAsync`, `BankAsync`, ...), the
  catalogue of D93, the bots (`Bots`, `BotActor`) and the game's observers, the pack's data, and the pages of D96. A
  plugin's activity gets the host's services through its constructor (D93).
- **In the repository**, the sample references the project `D3B.Bot` the same way, with every project reference
  `Private` false, so that the projects `D3B.Bot` references do not land in its folder either.
- **The template** references `D3B.Bot`; CI packs the six, then builds a plugin from it with a page, whose folder must
  hold its own files alone, and one without.
- **Risk.** Everything public in the six projects is what a plugin sees, about 170 types written by hand and the
  protocol's generated classes, and it changes every session. Before the first milestone publishes the packages (D4,
  D22), what serves only the host is to be made internal, and the API kept stable from then on.
- **Rejected.**
  - The bot's contracts moved into `D3B.Core`: they cannot be told apart from the run time; `D3B.Core` would take most
    of `D3B.Bot` and its four libraries, or hide them behind interfaces, and stop being the module model (D22).
  - A facade of interfaces, `D3B.Sdk`, implemented by `D3B.Bot`: a stable contract, but a second API kept in step with
    the first, every behavior written twice, while the API still changes every session. It stays possible once the API
    settles.

## D96: A plugin's page, in the app's rail

*2026-10-29, step 5, session 5b; the rail chosen by the owner among three mockups. Settles Q6. Builds D5, D91, D93 and
D94. The rail shows always since D101, the app having pages of its own; pages come from D3B's own modules alone since
D102.*

- **Registered by its module**, as D5 planned: `services.AddPage(id, title, icon, create)`
  (`PageServiceCollectionExtensions`, in `D3B.Bot.Pages`), a `PageDefinition`, its id unique in the host, which a
  second registration refuses while the modules build, as for activities (D93). The bots module lists them in the
  `PageCatalog`, in the order they were registered. `create` returns the page, an Avalonia control, as an `object`: the
  contract names no type of the UI's framework, so that a host without the app loads a plugin with a page (D97).
- **What a page receives**, `PageContext`: the host's services, the bots, the catalogues, the pack's data and the
  plugin's own services; the app's language, French or English, and its change; the language of the pack's texts that
  goes with it; and the window's refresh, every 250 ms while the page shows, on the UI's thread, as the app's own pages
  read the bots' snapshots (D9, D94). `Texts(resources)` gives the plugin's own texts in the app's language, which
  follow it as it changes, for the page to bind to, `{Binding Texts[Key]}` (`PageTexts`), with `Format` and `Plural`.
- **Translated names.** A text the app shows, a page's title or an activity's name, is a `LocalizedText`
  (`D3B.Bot.Localization`): the same in every language, or one of a module's resources in the language asked for. A
  plugin's resources are its own `.resx`, in English and French; the French build into a satellite assembly in the
  plugin's folder, which its load context finds through its `.deps.json` (D21), measured by the app's tests.
  `AddActivity` takes a `LocalizedText` too, and still a plain name, the same in every language.
- **In the app**: a rail on the left, 60 px wide, the bots page first, then a line, then the modules' pages in the
  catalogue's order, each an icon with its title as a tooltip, the page shown in the rest of the window. It shows once a
  module registered a page: while there is one page, there is no rail (D94). An icon is a path in the syntax of SVG on
  a grid of 24 units, drawn as the app's own icons, round lines in the colour of the text; one the app cannot read, for
  which Avalonia throws `InvalidDataException`, or none, gets the app's own.
- **A page fits the design** by what it inherits: the theme, dark or light, Inter and ShadUI's styles; and by the app's
  classes it may use, `PageHeader` and `PageTitle`, which the bots page uses too, `Plate` and `PlateFooter`, its cards',
  and the typography's.
- **A page is made the first time it shows**, with its context, and kept. One that throws as it is made, as it
  refreshes or as the language changes, or that is no control, is replaced by what it threw, each exception's type and
  message with the inner ones, in a danger block, and logged; its context raises nothing more, and the first fault stays
  shown. So is a page whose own code throws on the UI's thread what nothing caught, in a click or a layout: the app tells
  it by the page's factory's assembly in the exception's stack (`PageDefinition.Owner`), and what no page's code threw is
  still the app's fault, which ends it. The bots and the other pages go on: the app holds every client's connection
  (D91), and its fall would disconnect them all.
- **Nothing a module registered throws in the app.** A text whose resources cannot be found, such as a `.resx` moved to
  a folder, which `GetString` throws for, shows its key between brackets, as a missing one does. The catalogues are read
  before the host starts, so that two activities or two pages of one id, however registered, stop the host, which says
  so, rather than the app later. A page asking for its texts on every refresh gets the same ones each time.
- **The sample** (`samples/D3B.Samples.Hello`) writes an activity and a page: the stroll walks the character ten times
  to a cell within 4 of where it stands, of its zone, away from the cells of the map actions the world graph names,
  chosen from the state at each step (D87), and waits while the character is too busy for a walk, which would come back
  at once; a journal the activity writes and the page reads; and its page, a card per bot, whether it strolls, how many
  walks arrived, the last one, and the stroll's progress. Its texts are in English and French. Drawn without a screen,
  in both languages and both themes, and at 760 px wide.
- **The template** writes an activity and a page; `--page false` writes the activity alone, with no reference to
  Avalonia. It compiles pages against the Avalonia and CommunityToolkit.Mvvm the app runs, which CI checks.
- **Not measured**: the stroll on a client through the app; the tests play it through the sample loaded as a plugin.
- **Rejected.**
  - The pages as tabs in the title bar, the lead's recommendation: the full width left to the cards, but the owner
    chose the rail.
  - A section a plugin adds to each bot's card, alone or with the pages: the cards grow with every plugin, for no need
    a plugin has yet.
  - A contract typed with Avalonia, in a package of its own: `D3B.Host` would need Avalonia to build a plugin's modules.
  - The app's `{l:Tr Key}` for plugins' views: it reads the app's resources, inside `D3B.App`, which no plugin
    references.

## D97: The loader reads a plugin's root module from its metadata

*2026-10-29, step 5, session 5b. Builds D21. Superseded by D102: the loader goes.*

- `PluginLoader` finds the public class marked `[Plugin]` by reading the assembly's metadata
  (`System.Reflection.Metadata`), then loads that type alone. Looking for the attribute by reflection over every public
  type, as it did, loads every assembly their declarations name: a public page, deriving from Avalonia's `UserControl`,
  would stop a host without the app, `D3B.Host` or a test's, from loading the plugin. Measured by `D3B.Core`'s tests
  and `D3B.Host`'s composition in `D3B.Bot`'s, whose hosts hold no Avalonia: the sample, whose page is public, loads,
  its modules build, and its activity and page reach the catalogues, Avalonia never loaded.
- What the rest of a plugin needs then loads as its modules build: an assembly neither the host nor the plugin holds,
  told by the display name the runtime gives it rather than a file's path, is a `PluginLoadException` naming the plugin,
  and the host does not start (D21). So is a type of the plugin's own that bears the attribute's name.
- The root module is a top-level public class: a nested one, which reflection over public types found too, is not.
- **So** a plugin names its page's types in the page's factory alone, never in its module's own code, which a host
  without the app runs.
- `D3B.Core`'s tests reference `D3B.Bot`, which the sample compiles against, as a host provides it.
- **Rejected.**
  - A plugin's page types required to be internal: what a template's author writes by default breaks the host.
  - `System.Reflection.MetadataLoadContext`: a package, for a search the metadata reader in the framework does.
  - `D3B.Host` shipping Avalonia: a UI framework in the host without the app, for types it never makes.

## D98: An activity's settings, a class its module describes in code

*2026-10-29, step 5, session 5c; the description in code, and the scope with the game's values and every mechanism,
chosen by the owner, who noted that the auto modes to come, BillBot's for one, focus a profession, maps, resources of the
profession's level, a monster, an area or a sub-area. Builds D93, D96. Maps on the live map superseded by D123: they
wait for the activity that needs them. Extended by D127: sub-areas suggested by resources, percentages, and a preview.*

D93 left an activity's settings to 5b and 5c, and 5b did not write them.

- **What a module declares**: a class of the settings, with a constructor of no parameter, whose initial values are the
  defaults, and each of its properties it makes a setting, in code, as it registers the activity:
  `services.AddActivity<TActivity, TSettings>(id, name, settings => ...)` (`ActivitySettingsBuilder<TSettings>`, in
  `D3B.Bot.Settings`). A setting is named by an expression, `s => s.Walks`, whose type says what it holds; its name and
  help are `LocalizedText`s (D96).
- **The kinds** (`SettingKind`), D3B's, each with its editor in the app: `Flag`, a `bool`; `Number`, an `int` between
  bounds; `Text`, a `string` of bounded length; `Choice`, an enumeration's value, or a list of them (`Choices`), each
  named by the module; and the game's values by their ids, `Job`, `Area`, `SubArea` and `Resource`, an `int?` for one
  and a list of `int`s for several (`Jobs`, `Areas`, `SubAreas`, `Resources`). A list holds each value once; `Ordered`
  says its order matters, such as a priority. `Required` asks for a value. A sub-area is sought `Within` an area, a
  resource within a profession: the app offers those within the parent's value once it has one. `VisibleWhen` shows a
  setting when a condition of the settings holds, hidden it is not checked. `Section` groups the next settings under a
  title. `Rule` declares what the settings hold together, such as a minimum below a maximum, and what to say when they do
  not.
- **The game's values** (`GameReferences`, registered by the bots module): professions but the base, the atlas's areas
  and sub-areas, each sub-area's level, and the items each profession's skills gather, at the lowest level a skill
  gathers each. Read from the installed build's pack the first time something asks: a host that starts nothing never
  reads them. The app names them from the pack's texts in its language, and searches them whatever the case and accents,
  "frene" finding Frêne.
- **Checked in `D3B.Bot`** (`ActivitySettings.Check`), so a host without the app checks too: a problem per setting at
  most, of a kind the app says in its own language (`SettingProblemKind`: required, out of range, too long, unknown,
  repeated, not within), then each rule that does not hold, with the module's text. Without the pack, the game's ids are
  not checked. A condition or rule of a plugin's that throws shows the setting, or does not hold: the app runs it on its
  UI's thread, in the process that holds every client's connection (D91).
- **Registration checks the defaults** for what the module wrote wrong, a number out of its bounds, a text too long, a
  value repeated: the host does not start (D21). A value the user must give, such as a profession, has no default, and a
  rule may wait for it.
- **Stored as JSON**, a value per setting's key, its property's name; a choice by its value's name, so that an
  enumeration may be reordered. Reading starts from the defaults and takes each value that still reads: a key no longer
  declared is dropped, a value that no longer reads keeps its default and is reported (`SettingsReading.Reset`); of a
  list, what still reads is kept.
- **The activity receives them** through its constructor, an instance of the class, beside the services it asks for
  (`ActivityDefinition.Create(services, settings)`); started without a profile, with the defaults. `AddActivity<T>`
  without settings stays as it was (`ActivitySettings.None`).
- **The sample**: the stroll takes its number of walks and its reach as settings. **The template**: its activity takes a
  setting.
- **Later**: maps, picked on the live map (5d); monsters, once step 9 puts them in the pack. A plugin's own editor, for a
  setting no kind holds such as a path drawn on the map, stays possible beside the kinds.
- **Rejected.**
  - Attributes on the class, as DataAnnotations: least code, but an attribute holds no `LocalizedText`, the texts become
    keys resolved apart, and a wrong kind shows only to reflection.
  - Typed keys and a bag of values: no reflection, but the activity reads `settings.Get(Walks)` rather than an object.
  - An editor a plugin writes, with JSON of its own: every plugin writes its forms again, nothing is checked without the
    app, and the design splits.
  - The kinds alone, the game's values left to step 6: the mechanisms, lists, order, dependencies, conditions, are what
    is dear to add once activities rely on the settings.

## D99: Accounts seen, reusable profiles, a character's own

*2026-10-29, step 5, session 5c; the profile chosen by the owner. Builds D23, D65, D87.*

- **An account is never added by hand**: D3B sees no password (D23). It comes the first time its client lists its
  characters through D3B: the identification gives its id, nickname, tag and subscription (D72), the server's list each
  character's id, name and level (`KnownAccount`, `KnownCharacter`, in `D3B.Storage.Accounts`). The character the client
  chooses is dated as played. It may be forgotten, its characters with it, until its client lists them again.
- **A profile** (`Profile`, in `D3B.Storage.Profiles`) is a name, unique whatever its case, an activity by its id, and
  its settings as JSON (D98), shared by characters: changing it once changes it for all.
- **A character plays a profile, or none**, preselected on its bot's card; and may ask for it to start on connection,
  off by default: once its client first connects while the host runs, when its bot is made, the profile starts
  (`StartOnConnection`). Once per run: a new connection of the client carries the activity on (D89), and an activity
  the owner stopped stays stopped. A character with no profile never starts.
- **Recorded as the relay sees it** (`AccountRecorder`, an observer of the game connections, D62): dated as seen, queued
  off the relay's read loop, written one at a time, a write that fails logged and the relay going on.
- **A profile starts** (`ProfileStarter`) once its activity is registered, every stored value reads, and the settings
  hold in the pack; else it says why, and the bot is left alone: a setting reset is looked at in the app before the bot
  runs on defaults it was not given.
- **Groups**, a master and its followers, come at step 10, over this model.
- **Not measured**: recording and starting on connection on a client through the app; the tests play them through game
  connections built as the relay shows them.
- **Rejected.**
  - A profile chosen on the card at each start, kept by no character: nothing remembered, nothing starts by itself.
  - A profile of a character's own: the same settings copied for every character that does the same.
  - A profile per account: a bot is a character (D65), and an account's characters rarely do the same.

## D100: D3B's database: SQLite through EF Core, in a project of its own, no secret

*2026-10-29, step 5, session 5c. Builds D15, D91, D95.*

- **`D3B.Storage`**, not packed: EF Core 10.0.14 on SQLite, its context (`StorageContext`), its migrations, and its
  module (`StorageModule`). Not in `D3B.Bot`: the package plugins compile against would bring EF Core, and SQLite's
  native libraries into every plugin's folder, which D95 checks. Plugins do not see it: profiles are D3B's, and only
  name the activities plugins register.
- **Shared by the app and `D3B.Host`**: both add the module to the host's composition (`BotHost`), so both read and write
  one file, `%LOCALAPPDATA%\D3B\d3b.db`, beside the packs, captures and the client's configuration, or `Storage:Path`.
  Connections are not pooled, so the file is not held while D3B is idle, and foreign keys are on: forgetting an account
  forgets its characters, deleting a profile leaves its characters without one.
- **The migrations apply before the host starts**, through a step any module may add (`IHostPreparation`, in
  `D3B.Bot.Hosting`), run once the game data and the protocol are ready: a database that does not open stops the host,
  which says why and how to name another file, as for a missing pack (D31). `dotnet tool run dotnet-ef migrations add
  <Name> --project src/D3B.Storage --startup-project src/D3B.Storage --output-dir Migrations` writes the next, through a
  design-time context that needs no host; `dotnet-ef` 10.0.14 is a local tool (`dotnet-tools.json`). Migrations are
  generated code, which the analyzers and the formatter leave alone.
- **In memory too**: the accounts and the profiles are read once the database opens, then again after each write, a few
  rows; the app reads them each refresh without the database, and a version number tells it they changed
  (`AccountBook`, `ProfileBook`). Writes go one at a time per book.
- **No Data Protection**: nothing kept is a secret. Ids, nicknames, character names and settings are personal data, not
  secrets; the ticket is never kept (D65); the file lives in the user's own profile. D15's "Secrets" row waits for the
  first secret D3B keeps, such as a Discord webhook at its step.
- **Rejected.**
  - The storage in `D3B.Bot`: see above.
  - Migrations applied by a hosted service as the host starts: a failure would escape the host's start rather than say
    why it did not start.
  - Data Protection now: it would protect nothing.

## D101: The app's pages: a rail always, accounts by account, profiles in a column

*2026-10-29, step 5, session 5c; both layouts chosen by the owner among mockups. Supersedes D96 in part: the rail
shows always. Builds D94, D96, D99. Its editor grown by D127: tiles, a zone's mini-map, figures, bars and segments. Superseded in part by D159
and D162: the editor on a page of its own, deleting asked in a dialog.*

- **The rail** shows always: the bots, the accounts, the profiles, a line once a module registered a page, then the
  modules' pages (D96). Its icons are the app's own line drawings.
- **The accounts page**: a card per account, the one seen last first: nickname and tag, its subscription's end or that it
  has none, when D3B last saw it or that a bot of its plays now, and a button that forgets it, which asks first. Its
  characters are rows: initials with a dot of their bot's connection, name, level and when they last played, the profile
  they play, chosen from a list, and the switch that starts it on connection, enabled once they have one. An account
  explains where it comes from: its client going through D3B, which never sees its password.
- **The profiles page**: a column of profiles, by name, a new one first. Folded, a profile says its name, its activity,
  three of its settings at a glance, and the characters that play it; to look at again when its activity is gone or a
  stored value no longer reads. "Edit" unfolds it in place, one at a time: its name, its activity, then each section of
  its settings in two columns as the width holds, each setting by its kind, its bounds, help and problem, the rules that
  do not hold below, and save, enabled once all hold, cancel, and delete, which asks first. Changing the activity starts
  its settings again from their defaults. While a profile is edited, what the relay records does not disturb its editor.
- **A bot's card** offers the profiles first, by name, with their activity beside, then the activities with their
  defaults; the profile its character plays is chosen. An activity whose settings need a value asks for a profile; a
  profile that does not start says why.
- **The language and the theme are remembered** in a file of the app's own, `%LOCALAPPDATA%\D3B\app.json`
  (`AppPreferences`), read before the window shows, while the host and its database still get ready: the window opens as
  it was left. A file that does not read leaves the defaults. This is not the database: the app reads it before the host
  runs.
- **Drawn without a screen** in both languages and both themes, and looked at; the window's tests read both pages' texts in
  French and English.
- **Rejected.**
  - For profiles, cards with an editing panel on the right: the form, which the game's values will lengthen, squeezed into
    a narrow panel. A list beside a permanent editor: the three panes the owner turned down in 5a.
  - For accounts, a table of characters grouped by account.
  - The rail shown only beside a module's page: the app has three pages of its own.

## D102: Lua scripts replace the external .NET plugins

*2026-10-29, step 5, session 5c, chosen by the owner. Supersedes D90, D95, D97, and D6, D21, D22 and D96 for external
plugins; brings Q1 and step 7 back. Changes the brief. Superseded by D116: Lua scripts go too.*

The owner chose to drop the external .NET plugins for a Lua scripting system, once 5c showed what remained before the
plugins' packages could be published: trimming the six packages' public API, about 180 types without the protocol's
generated classes, to what a plugin needs, which the app, `D3B.Host`, the storage and the file-based tools all made
hard. The lead recommended finishing 5c, then deciding after a prototype of Lua, which Q1 never got; the owner decided
now.

- **What a user extends the bot with** is Lua scripts: routes and routines, as the bots sold and the owner's profession
  scripts in `vendor/metiers` are written. The brief's "External .NET plugins add modules" becomes: users extend the bot
  with Lua scripts. D3B's own features stay modules (D5, D20), in the repository, which may disable and replace them.
- **Why**: a small API made for scripts, in place of every public type of six packages kept stable; the language the
  ecosystem uses, so that the owner's scripts are adapted rather than written again in C#; no compiling, no packages,
  a file reloaded.
- **What stays**: modules, activities (D87), whose script will be one kind of `BotActivity`; settings (D98), whose kinds,
  checks, editor and profiles serve scripts too, a script declaring its settings in a table rather than a class;
  profiles (D99), naming a script as they name an activity; the pages of D3B's own modules (D96), registered as now.
- **What goes**, in a session of its own: the plugin loader and its attribute, the `plugins` folder, packing `D3B.Bot`
  and the five it names, CI's template check, the `dotnet new d3b-plugin` template, and the sample plugin, its stroll
  written again as a script. Q11's question, what a plugin sees, has no object.
- **Step 7, Lua scripting, comes back**, its number kept. Its first task settles Q1: LuaCSharp behind a `BotActivity`, a
  step calling the bot's behaviors and awaiting them, cancelled by an interruption and resumed, a script's settings,
  errors told in the app, no access to files or processes; a piece of one of the owner's scripts ported. The API's
  namespaces start from D12's (`global`, `map`, `inventory`, `npc`, `job`, `exchange`, `character`) and the owner's
  scripts, a script's `move()` and `bank()` as their steps (D87). LuaCSharp's version is the latest stable then (D15).
- **Risks.** Q1 is unproven: LuaCSharp may not hold up, and the decision would be looked at again. A script brings no
  page of its own.
- **Rejected.**
  - Deciding after a prototype, the lead's recommendation.
  - Lua beside the plugins, as D12 had it: two APIs to keep.
  - Keeping the plugins (D90), their API trimmed and frozen before the first milestone.

## D103: LuaCSharp holds, Q1 settled

*2026-10-29, step 7. Settles Q1. Confirms D15's "Scripts" row: LuaCSharp 0.5.8, of 2026-10-25, is still the latest on
NuGet, every version so far a 0.x.*

A prototype ran Lua scripts behind a `BotActivity` (`ScriptActivity`, in `src/D3B.Scripting`, which D90 had removed
empty), on the bots the tests play (D67), then the API of D105 over it. Measured:

- **A call that waits for the bot.** A C# function of the API is a `LuaFunction` that awaits a behavior, such as
  `bot:walk`, with the step's token; the script resumes once it is over. A Lua function kept in a table is called from
  C# after an `await`.
- **A step cancelled, then resumed.** An interruption cancels the call the script awaits: it comes out as
  `LuaCanceledException`, an `OperationCanceledException`, which the run turns into the interruption (D87). `pcall` does
  not catch it, nor any `OperationCanceledException` a C# function throws, so a script cannot swallow an interruption; it
  does catch every other exception, as a Lua error. The script's globals and the locals its functions hold stay as they
  were: the next step starts from the state with them.
- **A script that never waits.** LuaCSharp checks the token as the script loops, about every 100 ms measured, so a
  `while true do end` stops with its activity; an endless recursion is a stack overflow, a Lua error.
- **No files, no processes** (`ScriptSandbox`): the base library without `dofile`, `loadfile`, `load`, which also loads
  bytecode, and `collectgarbage`, which drives the process's collector; `string` without `dump`; `table`, `math`,
  `bit32`; of `os` its clock and dates alone. No `io`, `package`, `require`, `debug` or `coroutine`. The platform under it
  refuses files, the environment and an exit, should a library come to ask. `print` writes to the script's console.
- **Errors told in the app.** A script's error fails the activity with a `ScriptException` reading as Lua's,
  `mineur.lua:3: attempt to index a nil value (local 'mine')`; the run keeps it (`ActivityRun.Failure`), the bot's card
  shows it in the danger tone, and the script's console (`ScriptConsole`, its last 200 lines) ends with it. Drawn without
  a screen.
- **The owner's scripts**: their 58 files and 17 settings files compile as they are.
- **Limits of LuaCSharp 0.5.8**: writing the stack of a cancellation inside a function throws, which `ScriptException`
  works around; "attempt to call a nil value" does not name the field, where C Lua does; an error raised by a function
  called in a tail call has no line, as in C Lua; strings are UTF-16, `#"é"` being 1; nothing bounds a script's memory,
  `string.rep("x", 1e8)` allocating what it asks.
- **Rejected.**
  - A hook counting instructions to stop a loop: LuaCSharp's own check of the token does it.

## D104: MoonBot among the references, and what the owner's scripts call

*2026-10-29, step 7, MoonBot added by the owner. Adds to D3.*

`vendor/moonbot/lua-api.md`, the documentation of MoonBot's Lua API, a bot sold for Dofus 3, joins D3's references, for
ideas only, as Snowbot's family: D3B copies none of its text and none of what D2 excludes, which it documents too
(reconnections and their staggering, subscription purchases, raw packets).

Measured on the latest version of each of the owner's five profession scripts (`vendor/metiers`), what they ask of a bot:

- **37 functions** of 9 namespaces, `global:printMessage` 113 times, then `global:delay`, `global:printError`,
  `developer:createMessage` and `sendMessage` 35 times each, `map:currentMapId`, `job:level`; 30 of the 37 exist under the
  same name in MoonBot's, so Snowbot's and MoonBot's names are one family. MoonBot lacks `printColor`, `getTeamMembersIds`,
  `Get/AddInGlobalMemory`, `getLastChallengeProposalMessage` and `character:id`.
- **Routes**: `move()` and `bank()` return a table of steps by map, run on the map the character stands on: 2,461 `path`,
  2,091 `gather`, 70 `door`, 43 `custom`. Paths are sides (2,224), `havenbag` (108), `zaap(id)` (80), cells, sides to
  choose from at random (`top|right`) and sides from a cell (`left(392)`). Globals the bot reads: `GATHER`,
  `PODS_THRESHOLD`, `OPEN_BAGS`, `MIN_MONSTERS`, `MAX_MONSTERS`.
- **Settings**: a `settingsX.lua` beside the script, loaded with `pcall(dofile, ...)`.
- **The protocol**: the fight's challenges, by name (`ChallengeSelectionRequest`), and one request by an obfuscated name.
- A dump of the readable names of the game's messages and fields that a third party published, given by the owner, fits
  3.7.12.12: 1,344 of its 1,345 obfuscated types are in the build's schema (D39), and where D3B's mapping pairs a message
  the dump names the same type, under the same name 43 times and another 16, where D3B chose its own words (D42). It is
  data derived from the client, of no license: never committed, read at most for ideas when the readable protocol grows
  (D3, D11).

## D105: The Lua API is D3B's own

*2026-10-29, step 7, chosen by the owner, against the lead's recommendation of the family's names. Builds D87, D102. Superseded by D116.*

The lead recommended the names of Snowbot's and MoonBot's family (D104), so that the owner's scripts run nearly as they
are; the owner chose an API of D3B's own. The form is the lead's, local and reversible:

- **Hooks**: a script defines the hooks of `BotActivity` (D87), each given the `bot`: `step(bot)`, required, a step chosen
  from the state, `false` ending the activity; and optionally `isFull(bot)`, which reads and never acts, `unload(bot)`,
  `onInterrupted(reason)`, answering `"stop"` to end the activity, and `recover(bot, reason)`, run after the default
  recovery (D89). Reasons are `player`, `disconnected`, `fight` and `stopped`.
- **The text runs first**, once per start, to define the hooks and declare the settings (D107). It cannot act: the `bot`
  is no global. Once it ran, its global `settings` holds the values. A first step cut short before it ran whole runs it
  again in a new state; a character full once it ran is unloaded before the first step.
- **The bot** (`BotObject`): fields read the state live, `bot.character`, `bot.map` (id, coordinates, world map,
  sub-area), `bot.cell`, `bot.inventory` (weight, load, kamas, items), `bot.jobs`, `bot.isBusy` and the like; methods act
  through the hook's behavior and wait: `walk`, `changeMap` (a side or a map action's cell), `travel`, `use`, `bank`,
  `leaveHavenBag`, `wait`, `waitIdle`, and `count`, `jobLevel`, which read. An action answers as Lua's own functions,
  `true`, or `false` and why, the behavior's outcome in lower camel case: `local ok, why = bot:walk(301)`.
- **Globals**: `log.info`, `log.success`, `log.error` and `print`, to the script's console and the bot's log; `game`,
  the pack's data: `mapsAt`, `position`, `distance`, `cell`, `mapActions`.
- **Names** in lower camel case; methods with `:`, functions of a namespace with `.`; a `:` call to a namespace's function
  reads as a `.` one.
- **Not yet**: harvesting, fights and the rest of the milestones, which the API gains as D3B's behaviors do; routes as the
  owner's tables, which step 13 may bring as a helper of D3B's own.
- **Rejected.**
  - The family's names, `global:`, `map:`, `move()` returning a route by map, the lead's recommendation: the owner's
    scripts adapted rather than written again, but a dialect of others' with its quirks, `podsP` and two calling forms.
  - Both, D3B's API and the family's over it in Lua: two surfaces to document, test and keep in step.

## D106: Scripts reach the protocol through D3B's readable requests

*2026-10-29, step 7, the lead's recommendation, which the owner left to the lead. Frames the family's
`developer:createMessage` and `sendMessage`. Superseded by D116.*

- `bot:send(name, fields)` sends the game server a request of D3B's readable protocol, named as its `.proto` files name
  it, whole (D55), its fields by their JSON names, in lower camel case as the rest of the API; every field is checked
  against the message's descriptor, an unknown field or a value that does not fit failing the call (`ProtocolTables`).
  It goes through the hook's behavior, between the client's frames (D61), and the client is not told.
- `bot:waitFor(name, ms)` waits for the next event or response of that name, from now on, and gives the fields the
  readable protocol reads, an enumeration's value by name; `nil, "timeout"` when it does not come. `bot:request(name,
  fields, answer, ms)` expects the answer before it sends, as every behavior does (D67).
- Never raw bytes, never a message to the client, no obfuscated name.
- **Risk.** A request the client does not follow may leave it out of step with the server (D60): the script's author
  answers for it, and the definitions say so.
- **Rejected.**
  - No protocol at all: the client always consistent, but what has no behavior yet, such as the owner's challenges, waits.
  - Ankama's original names beside D3B's, from a table per build shipped as the mappings are: closer to the family, but a
    table per build to make, of names D3B does not choose.

## D107: A script's settings, a table onto D98's kinds

*2026-10-29, step 7, the form chosen by the owner. Builds D98, D99. Superseded by D116.*

- **Declared as data**, an entry per setting in the order the app shows them, `{ key, kind, name, help, default, min,
  max, maxLength, options, required, ordered, within }`, or `{ section = ... }`; kinds `flag`, `number`, `text`,
  `choice` and `choices` among the entry's options, and the game's `job`, `area`, `subArea`, `resource`, with an `s` for
  several. A text is a string, or a table by language, `{ fr = "...", en = "..." }` (`LocalizedText.ByLanguage`). Read
  without the bot (`ScriptSettings`), a declaration that does not read says what and where.
- **Into D98's settings**, without a class: `SettingValues`, a value per key, and `SettingValuesBuilder`, whose settings
  are `SettingDefinition`s like a class's, a choice's values being the declaration's own strings rather than an
  enumeration's. The app edits them, a profile stores them as JSON by key, checks and resets apply as for a module's; a
  profile names a script as it names an activity (D99).
- **Not yet**: conditions and rules, which are code in D98 and would run a script's function on the app's thread.
- **Rejected.**
  - Builder functions, `setting.number(...)`: the same data, with functions to document beside it.

## D108: Scripts live in a watched folder, with the API's definitions

*2026-10-29, step 7, the folder chosen by the owner; the definitions asked for by the owner. Superseded by D116.*

- **Where**: `%LOCALAPPDATA%\D3B\scripts`, or `Scripting:Directory`, beside the database: every `.lua` file of it and of
  its folders is a script, but the definitions below. The scripting module (`ScriptingModule`), which the app and
  `D3B.Host` add as they add the storage's, registers the `ScriptLibrary`.
- **Listed as activities**: `script:` and the file's path, such as `script:metiers/paysan.lua`, shown as its path without
  the extension. The library is a source of the `ActivityCatalog` (`IActivitySource`), which lists the modules'
  activities then the sources', and changes `Version` with them; the bots' cards and the profiles page list them again
  when it changes.
- **Read without the bot** (`ScriptReader`): the text runs in its sandbox, 2 s at most, its hooks found and its
  settings read. A script that does not read is listed still, as an activity whose start fails with its error, which the
  card shows; the log says why.
- **Reloaded**: the folder is watched, and once quiet for 300 ms every script is read again, the list replaced whole. A
  start takes the text as last saved; an activity running keeps the text it started with.
- **Documentation and completion**: D3B writes into the folder `d3b.lua`, the API as LuaCATS annotations, and
  `.luarc.json`, Lua 5.2 and the libraries a script lacks, as the host starts, when they differ: opened in VS Code with
  its Lua extension, the folder has the API's documentation and completion. A test checks that `d3b.lua` documents every
  hook, field, method and function of the API, and nothing else.
- **The sample**: the former sample plugin's stroll, `samples/scripts/promenade.lua`, played by the tests.
- **Rejected.**
  - Scripts in the database, edited in the app: renaming safe, but an editor to write, and VS Code lost.
  - A running script reloaded when its file changes: its state lost mid-activity, and a file half saved breaking a bot
    that runs.

## D109: The external plugins removed

*2026-10-29, step 7. Carries out D102. Supersedes in part D19, whose `samples/` now holds example scripts; `samples/`
goes with them, by D119.*

- **Gone**: the plugin loader and its attribute, its load context and `PluginInfo` (`D3B.Core.Plugins`), the `plugins`
  folder and `Plugins:Directory`, packing `D3B.Core`, `D3B.Bot` and the four libraries it names, CI's check of the
  template, the `dotnet new d3b-plugin` template, and the sample plugin, its stroll written again as a script (D108).
- **Stays**: modules (D5, D20), D3B's own, in the repository; activities (D87) and their settings (D98); the pages of
  D3B's modules (D96), in the app's rail, a page's view model now `ModulePageViewModel`; a page that throws still shows
  its error, tested with a page of the tests' own.
- **Why** (D102): no package's public API to keep stable; users extend the bot with scripts, which bring no page.

## D110: Lua scripts, measured through the app

*2026-10-29, step 5, session 5e. Completes D103 and D108, which only the tests had measured.*

Measured on a client of 3.7.12.12 through the app, in two runs, the scripts' folder holding the sample stroll,
`promenade.lua`, and a script failing on purpose at its second step, never committed
([client.md](client.md#the-game-from-the-app)):

- **Listed**: the host read both, neither failing, and the bot's card offered them among its activities.
- **Started from the card**, with its defaults, 10 walks: stopped from the card during its 9th walk, it ended at once,
  `Stopped` after 8 steps; run whole, its 10 walks arrived and it ended `Done`.
- **Interrupted by the player, then resumed**: the player's click during its second walk paused the bot, the activity
  answering that it resumes; resumed from the card 8 s later, it went on from the cell the player had led the character
  to, the script's counter where it was, and ended `Done`.
- **A failing script** failed at its second step, `Failed` after 1, its error with its line, the exception whole in the
  log.
- **Closing the app** gave the client its configuration back and closed the game connection.
- **Found**: the error names the script without its extension, `erreur:10:`, where D103 has `mineur.lua:3:`: LuaCSharp
  is given the name the app shows as the chunk's. Not fixed, Lua scripts going (D116).
- **Not measured on the client**: a script started from a profile, none being saved; what the card showed of the failure,
  which the owner did not report, drawn without a screen in D103.

## D111: The logs and the protocol inspector, two pages of the rail

*2026-10-29, step 5, session 5e; the layout chosen by the owner among three mockups, the filters as the lead proposed
them, chosen by the owner. Builds D92, D101. Superseded in part by D162: a selector in place of the chips, one toolbar.*

- **Two pages** in the rail, after the profiles: the logs, and the protocol inspector. They look at one bot
  (`BotChoice`): chosen on either, it is the other's. A bot's card has a journal button, beside its connection, which
  opens the logs page on its bot.
- **The logs page**: its sources as chips, every source by time, the host, then each bot with a dot of its status; the
  lines' tone as a segment, every line, warnings and errors, or errors; a search through what the lines say, whatever its
  case; "Follow", which scrolls to each new line; "Copy", the lines shown as text. A line says its time, a mark of what it
  is in its tone, its source when every source shows, what happened, then its outcome and duration (D112); a problem of
  the host has its whole message and exception as a tooltip (D114).
- **The protocol inspector**: a bot's frames live, chosen among chips, or a capture of the game connection, chosen in
  "Replay a capture…", the last written first, its folder read again as the list opens. Filters: the client's, the
  server's and D3B's frames, a toggle each; a search through the messages' names, readable or the build's; "Hide unread",
  off at first; "Follow"; "Freeze", live only, the frames waiting until it thaws; and clearing the list. A row says its
  time, its sender, its message, its size, and how far the readable protocol read it: understood whole, read, or not
  read. A frame unfolds in place, which stops following, so that it stays in view (D115).
- **Drawn without a screen** in both languages and both themes, and looked at; the window's test reads both pages in
  French and English and opens a bot's journal from its card.
- **Used on a client** of 3.7.12.12 through the app by the owner: the journal opened from the card, a stroll paused by
  the player and resumed, the filters, the inspector live and a capture replayed, no problem reported
  ([client.md](client.md#the-game-from-the-app)).
- **Rejected.**
  - One page of tabs, journal, script console and frames, the bot chosen at its top: one tab at a time, when the journal
    and the frames are read together.
  - A drawer on the right of the bots page, opened from a card: lines and frames squeezed into a panel, the cards beside
    it.
  - Unread frames hidden at first: three frames of four (D58), but what is hidden is what the one mapping a build looks
    for.
  - Richer filters, by category of the host's log and by time.

## D112: A bot's journal says its actions, not the protocol

*2026-10-29, step 5, session 5e, asked for by the owner: each action, using an element, changing maps, never the
protocol's messages.*

- **Written by the behaviors** (`BotActor.Journal`, entries in `D3B.Bot.Journal`): an action once over, with its outcome
  and how long it took by the bot's clock: a walk to a cell (`WalkEntry`), a map change, by a side, a map action, a door,
  a zaap or leaving the haven bag (`MapChangeEntry`), a use of an element with a skill (`UseEntry`), a travel and its
  number of changes (`TravelEntry`), banking and the stacks and kamas it put in (`BankEntry`). Then what becomes of the
  bot, connected, disconnected, paused by the player, resumed, a fight beginning (`BotEventEntry`), and of its activity,
  started, interrupted and what it answered, unloading, over and how, its error included (`Activity*Entry`).
- **Actions within actions** (`BotContext.BeginAction`): an action is written once over, as deep as the actions it was
  begun within. A travel's map changes come before it, one deeper, and the app sets them in; a walk made for another
  action, to a map's exit or an element, has no entry. An action an interruption cuts has none either: the interruption's
  entry says it.
- **Ids, never words**: an entry names maps, cells, elements and skills by the game's ids; the app names them from the
  pack in its language (`JournalLines`), a map by its coordinates, the sub-area beside them where the place is the point,
  such as a travel's end, an element by what it is and the skill used, and by its id what the pack does not name.
- **Rejected.**
  - The host's log, filtered by bot: the protocol's messages and the host's English, the lines the owner turned down.
  - Every walk, those made for another action included, set in under it: twice the lines for what the action says.
  - No walk at all: an activity's own walks would go unseen.

## D113: The logs and the frames kept in memory, always

*2026-10-29, step 5, session 5e, chosen by the owner.*

- **Kept in the host**, in memory, from its start, the app open on any page or none: each bot's last 2,000 journal
  entries (`BotActor.JournalCapacity`), the host's last 2,000 problems (`HostProblems.Capacity`), and each bot's last
  3,000 game frames, both sides and D3B's, across its connections (`FrameRecorder.Capacity`). Older ones are dropped
  (`RecentItems<T>`), and the pages let go of what the host no longer keeps.
- **Read as they come**: a page reads only what came since its last refresh, every 250 ms, and only while it shows.
- **A frame costs a reference**: the game session decodes it once (D62), and the recorder keeps it.
- **Nothing written to disk**: captures stay what records a connection (D37), and a host that stops forgets the rest.
- **Rejected.**
  - The inspector recording only while a button is on: what came before it was opened, such as the ticket's request and
    the character's entering, is what one looks for.
  - 10,000 lines and frames per bot: about 15 MB per bot, for what a capture keeps better.

## D114: The host's problems on the logs page

*2026-10-29, step 5, session 5e, chosen by the owner.*

- **A source of its own**, "Host": what the host's log writes at the warning level or above, unless it names a bot by
  its character, whose journal says it already (D112). `HostProblems`, an `ILoggerProvider` the bots module registers,
  keeps them as the host writes them, in English, with the exception if any: a script that does not read, a connection
  whose account is unknown, a relay's failure.
- **Rejected.**
  - No such source, the host's problems left to `D3B.Host`'s console, which the app has none of.
  - The host's whole log, information included: the relay's every connection and the database's commands, in English.

## D115: The protocol inspector

*2026-10-29, step 5, session 5e. Carries out D92's 5e, with D63's replays.*

- **Recorded by the host** (`FrameRecorder`, in `D3B.Bot.Inspection`), an observer of the game (D62) the bots module
  registers: a game connection's frames are its character's once the server confirms the character the client chose, as
  bots are tied to theirs (D65), those that came before joining them then; a connection whose account D3B does not know
  is never a bot's, and its frames go with it.
- **D3B's own requests** reach observers apart (`IGameObserver.OnSent`, which does nothing by default), decoded as the
  server reads them, once sent: `OnFrame` still never shows them (D63), the bots learning what they did from the server.
- **A frame unfolded**: a readable message's fields as protobuf's JSON names them, those it holds, at every depth, an
  enumeration's value by name, bytes by their length, a list of scalars past 32 values counted (`FrameFields`). A message
  the readable protocol lacks, or that does not read, shows its shape alone, its fields' numbers, wire types and lengths,
  as `scripts/capture.cs --frames` does (D37); `WireReading` and `WireField` are public for it.
- **The ticket is never shown**: the game connection's first request, `AuthenticationRequest`, shows it is there,
  hidden, live as in a replay, as no log names it (D65).
- **Replays** (`CaptureFrames`): a capture of the game connection is read whole, each side's reads framed again as the
  relay would, D3B's frames being records of their own (D61), through the installed build's translation. A capture of
  another build is refused, saying which, since its frames name messages only its own mapping reads; a capture cut short
  says so.
- **A frame's length**: `DecodedGameFrame.Length`, without its varint, which a row shows.
- **Measured** on a client of 3.7.12.12 through the app ([client.md](client.md#the-game-from-the-app)): its capture of
  298 frames replayed whole, the client's, the server's and D3B's 21; each frame not read showed its shape; the
  `AuthenticationRequest` came first, its ticket hidden; the character chosen, which ties the frames to their bot.

## D116: Lua scripts go, in a session of their own

*2026-10-29, step 5, session 5e, chosen by the owner, against the lead's recommendation. Supersedes D102 and D105 to
D108; D103 stays as what was measured. Changes the brief. Carried out by D119; nothing outside the repository superseded
by D117, mods coming as blueprints.*

Once the scripts were measured through the app (D110), the owner chose to do without them. The lead recommended
freezing them as they are, no more work on their API, and deciding again after step 6.

- **What a user extends the bot with**: nothing outside the repository. Every feature is a module of D3B's own (D5,
  D20); activities (D87), their settings (D98), profiles (D99) and the modules' pages (D96) stay.
- **What goes**, in a session of its own: `D3B.Scripting` and its tests, the scripting module, the scripts' folder and
  `Scripting:Directory`, `d3b.lua` and `.luarc.json`, `samples/scripts/` and its stroll, and LuaCSharp. Profiles naming
  a script then name an activity that no longer exists, which the profiles page shows (D101).
- **In 5e**: no script console tab on the logs page; the chunk's name D110 found is not fixed.
- **The order**: the removal next, then 5d; a client update before a session still gets Q10's first (D54).
- **Rejected.**
  - Freezing them, the lead's recommendation: an API and its definitions kept, unused.
  - Removing them within 5e: one large task per session (D17).

## D117: Mods in blueprint style, after step 6

*2026-10-29, the session removing the Lua scripts, chosen by the owner, the step's place as the lead recommended it.
Supersedes D116's "nothing outside the repository" for mods. Changes the brief. Opens Q13. Its place amended by D169:
after the fights of step 9.*

Asked which activity D3B keeps without Lua (D118), the owner answered that mods will be made in blueprint style: graphs
of nodes, edited on a canvas in the app. The references the owner gave, read for their look and ergonomics only (D3),
all React where the app is Avalonia (D14):

- shadcn.io's Canvas, a node editor for agents' workflows: nodes as cards with a header, content and footer and their
  input and output handles, curved edges, animated or dashed, panning, zooming, fitting, a toolbar on the node chosen,
  keyboard control; written without a flow library.
- React Flow's Workflow Editor template: nodes dragged from a sidebar, laid out automatically (ELKjs), and a runner that
  executes the nodes one after another and shows where it is.
- ReUI's flow blocks, on React Flow: nodes as cards with their status, duration and owner, labelled edges such as yes
  and no, an inspector beside the canvas that reads and edits the node chosen, snapping to a grid, a context menu.

- **When**: after step 6, as step 7, whose number Lua held. A node wraps a behavior of D3B's, and harvesting's, which
  step 6 writes, do not exist yet; step 6's harvesting is a module's activity (D87), the first milestone.
- **What is open** (Q13): who writes mods, the owner or users too; where they live; what a node is over D3B's behaviors
  and state; how a graph runs as an activity, interrupted and resumed from the state (D87); its settings (D98); and which
  canvas in Avalonia, a library or the app's own.
- **Risk**, the lead's: a graph that runs is a language to keep, what Lua cost (D116); its contract is to stay small.
- **Rejected.**
  - The blueprints right after 5d, before harvesting: a runner and nodes over behaviors not written yet.
  - The blueprints before 5d.

## D118: No activity of D3B's own until step 6

*2026-10-29, the session removing the Lua scripts, chosen by the owner, against the lead's recommendation. Builds D93.
Ended by D127: the harvesting module registers D3B's first activity.*

Without Lua, no module in `src/` registers an activity: the scripts were the only ones, the tests' own aside.

- **The catalogue is empty** until step 6 writes harvesting (D93): a bot's card says there is no activity to start yet,
  and that D3B's modules will add them; the profiles page has no activity to give a new profile.
- **What waits**: starting, stopping, pausing and resuming an activity from the app on a client, and profiles, their
  settings and starting on connection, which D99 and D110 did not measure on a client, until step 6. 5d measures the
  live map with what the player does.
- **Rejected.**
  - The stroll written again in C#, a module's activity with its two settings, the lead's recommendation: 5d would see
    the bot walk on its own, and profiles would be measured on a client, for about a hundred lines to keep or drop at
    step 6.

## D119: The Lua scripts removed

*2026-10-29, the session removing the Lua scripts. Carries out D116. Supersedes D107's settings without a class and
D108's source of activities, the owner's choice against the lead's recommendation, and D109's `samples/`.*

- **Gone**: `D3B.Scripting` and its tests, the scripting module the app and `D3B.Host` added, the scripts' folder and
  `Scripting:Directory`, `d3b.lua` and `.luarc.json`, `samples/scripts/` with its stroll, and `samples/` with it, empty,
  and LuaCSharp from the central packages. CI named none of them.
- **D107's pieces go too**, the owner's choice: the lead recommended keeping them for the blueprints (D117), whose
  mods' settings are data and whose activities come and go. Gone: `IActivitySource`, the catalogue's `Version` and the
  app's lists read again when it changed; `SettingValuesBuilder`, `SettingValueBuilder` and `SettingValues`; choices of
  strings; `LocalizedText.ByLanguage`. `SettingDefinition` is a property of the settings' class again, as before step 7;
  the catalogue holds the modules' activities alone, fixed once the host is built (D93).
- **Kept**, serving any activity: `ActivityRun.Failure` and the error on the bot's card (D103); an activity that is
  `IDisposable` disposed once over, its run being the only one to hold it (D93); `WorldPathfinder.Atlas`, which the card
  places the character with.
- **A profile naming a script** names an activity gone, and nothing maps it to another: the profiles page says
  "Activity gone" with its id, `script:promenade.lua`, and edits it only once given another activity; deleting it works.
  A bot's card offers it, the gone activity beside it, and starting it says the activity is gone.
- **On connection**, a profile that does not start says why in the bot's journal (`ProfileNotStartedEntry`, in
  `D3B.Storage.Profiles`), its activity gone, its settings to review or not holding: the host's log names the bot, which
  the host's problems leave to the journal (D114), and the journal said nothing of it, the bot doing nothing in the app
  with no reason shown.
- **The user's folder** `%LOCALAPPDATA%\D3B\scripts` stays as it is: D3B neither reads nor writes it any more, and the
  owner deletes it.
- **Measured** on a client of 3.7.12.12 through the app, on a copy of the database holding a profile that names
  `script:promenade.lua`, its character's own, started on connection ([client.md](client.md#the-game-from-the-app)): the
  host built its five modules, no scripting among them; the profile did not start on connection, which the bot's journal
  said; the card and the profiles page showed its activity gone; the inspector live and a capture replayed; closing the
  app gave the client its configuration back. Not measured on the client: deleting the profile, which the tests do; the
  player's pause, which needs a behavior running, and so an activity (D118).

## D120: The live map, a page of the rail

*2026-10-29, step 5, session 5d, chosen by the owner among three mockups, as the lead recommended it. Carries out D92's
5d. Superseded in part by D162: a selector in place of the chips.*

- **A page** in the rail, after the profiles, before the logs: "Map". It looks at one bot, chosen among chips as on the
  logs page and the inspector, and shares their choice (`BotChoice`, D111): a bot chosen on one is the others'. A bot's
  card has a map button beside its journal button, which opens the page on its bot.
- **Its header**: the map's coordinates large, then where the character is, the sub-area and the cell, in a fight, in a
  haven bag or off the atlas, then what it is in the middle of, as its card says it (D94), and the map's id; the layers'
  toggles beside it, the legend under the map.
- **Read every 250 ms** while it shows, as the other pages (D94): a new scene only when the bot's snapshot, the other
  bots on its map, its connection, its pause or the app's language changed.
- **Rejected.**
  - The map on the bot's card alone: each card some 200 px taller, against D94's whole cards, its cells some 10 px high,
    too small for the elements and to point at.
  - A thumbnail on the card that opens the page: it repeats what the card says large, drawn again for every bot. Still
    possible later, the control being the page's.

## D121: The live map shows what the pack and the state hold, in layers

*2026-10-29, step 5, session 5d, chosen by the owner, as the lead recommended it. Superseded in part by D162: the
layers in a menu, with their marks as the legend.*

- **Seven layers**, each toggled: the cells, walkable, obstacles that block the sight and voids that let it through, and
  the grid; the character, its path as the server showed it and its end; the interactive elements, placed from the pack
  and marked by what the server says of them, a resource to harvest, harvested, another element, a door, or not
  described, grey; the NPCs, a banker named from the pack, any other by its id; the exits, each side's cells with the
  neighbour's coordinates, the map actions and the doors with the maps they lead to, from the world graph, criteria not
  answered; the other bots of D3B on the map; and the zones and stairs, hidden at first, tinted where a map has more than
  one zone, or both move zones (D86).
- **A cell under the pointer** is described in a card beside it, in the app's language: its number and coordinates,
  walkable with its floor and zone, a stair, an obstacle or a void, then what stands on it: the character, the path's
  start or end, another bot, the element with the skills it offers or that it is harvested, the NPC, and where an exit
  leads.
- **The exits** (`MapExits`): a scroll goes with every walkable cell of its edge that leaves the map that way
  (`MapGeometry.LeavesTowards`, moved from `WorldPathfinder` for it), each cell with the nearest scroll of its zone and
  direction (D71); a map action by its cell; a door by its element, on the element's own cell.
- **In a haven bag** the bag's map is drawn as any other, "Haven bag" in place of the place. **In a fight** the fight's
  own map is drawn, its coordinates left out (D88); the character shows once the server shows it moving, its placement
  not being in the readable protocol (D125).
- **Out of 5d**: other players and monsters, which the readable protocol does not read among the map's actors (D55),
  steps 9 and 10; walking to a cell clicked, a command of its own; the world map.
- **Rejected.**
  - The cells, the character and its path alone: the rest is in the state and the pack already, and costs nearly as
    much later.
  - Other players and monsters too: the protocol, the state and the mapping to extend and measure, steps 9 and 10.

## D122: The live map, drawn flat from the pack

*2026-10-29, step 5, session 5d, chosen by the owner, as the lead recommended it.*

- **A control of the app's** (`MapCanvas`, in `D3B.App.Controls`) draws the cells read from the pack, as diamonds of 14
  columns by 40 staggered rows, fitted to its room with margins for the exits' labels (`MapLayout`). What a map holds,
  its walkable cells, obstacles, stairs, zones and exits, is built into geometries once per map; what moves is drawn
  again each refresh.
- **Flat**: floors drawn in height would overlap cells on a map with stairs; the stairs are tinted in the zones' layer,
  and a cell's floor is given under the pointer.
- **The pointer** finds its cell from the cells' geometry (`MapLayout.CellAt`), never by testing the shapes drawn.
- **Colours** from the theme: a palette per theme (`MapPalette`, in `App.axaml`'s theme dictionaries), the dark's and
  the light's; the legend draws each mark with the map's own drawing (`MapMarkIcon`).
- **Nothing to extract** or publish: the pack of 3.7.12.12 holds it all.
- **Rejected.**
  - The client's textures: 63,454 graphics in three scales, the smallest 1.3 GiB on disk and 6.3 GiB decompressed,
    assembled per map in three layers with effects and shaders; an extractor, a pack of textures per build released
    (D24) and a renderer, more than the rest of step 5, for a map prettier and harder to read.

## D123: Maps for settings wait for the activity that needs them

*2026-10-29, step 5, session 5d, chosen by the owner, as the lead recommended it. Supersedes D98's "maps, picked on the
live map (5d)".*

- **Not in 5d**: a setting of maps, its kind `Map` or `Maps`, comes with the first activity that needs it, at step 6 or
  later. What it holds, one map or several, ordered as a route or a set, within a sub-area, is that activity's to say.
- **The control is reusable**: `MapCanvas` draws any scene, a map and what stands on it, and says the cell under the
  pointer, so that a setting's editor may show a map.
- **Rejected.**
  - The kinds `Map` and `Maps` in 5d, a field of the profiles page and "Add to a profile…" on the map page: a third of a
    session measured only by the tests, no activity existing before step 6 (D118), its shape guessed.
  - The same with a world map to pick from: a page of its own.

## D124: The live map, measured through the app

*2026-10-29, step 5, session 5d.*

Measured on a client of 3.7.12.12 through the app, a game connection of 2 min 49 s, the player moving the character by
hand and no activity started (D118), captures on ([client.md](client.md#the-game-from-the-app)):

- **Followed**, the owner reported, in every case: walks on the map of a zaap, the haven bag entered and left there, a
  scroll to a neighbour, the haven bag again from it, then a fight, and the map after it.
- **The maps**, five, all in the pack: two of the world graph, the haven bag's, off the graph at 0,0 on no world map, the
  fight's own, off the graph at 0,0, and a zaap's, where the fight left the character, neither the map it began on nor a
  neighbour of it. The page drew each.
- **The player's walks**, 13, each confirmed; the server showed 13 more of other actors.
- **No frame from D3B**, nothing in the host's log above information.
- **Found**: neither the fight's map nor the map it began on holds a cell marked `red` or `blue` in the pack (D125).

## D125: A fight's placement cells, from the server at step 9

*2026-10-29, step 5, session 5d, asked for by the owner, chosen as the lead recommended it.*

The owner asked for the map to mark the red and blue cells in a fight, where each team is placed.

- **At step 9**: the cells the server gives when a fight begins, its message mapped and measured, then marked on the
  map. In 5d, nothing.
- **Why not the pack's**: the cells marked `red` and `blue`, whose meaning is Dofus 2's, not measured (client.md), are on
  neither the fight's map measured nor the map it began on; of the 2,877 maps at 0,0, off the world graph and on a world
  map, as the fight's is, 1,158 hold some. In the same millisecond as the fight's context, the server sent 16 frames the readable
  protocol lacks, the placement likely among them.
- **Rejected.**
  - The pack's flags tinted on a fight's map from 5d: little code, but nothing for the fight measured, and a meaning not
    measured.
  - The server's message mapped in 5d, from this session's capture, and a fight measured again: protocol work the size
    of a session, a mapping format 7, outside step 5.

## D126: Step 6 in three sessions, the icons in their own

*2026-10-29, step 6, session 6a; both chosen by the owner, as the lead recommended them. Superseded in part by D136: the
icons are stored as raw pixels, not PNG.*

- **6a**: the first milestone itself: the harvesting module and its activity, its settings and their editor, the next
  resource and the next map, banking full and the way back, the activity resumed after a fight, a new connection or the
  player's pause, and what the bot's card and the map page show of it; measured on a client through the app.
- **6b**: icons, in the pack, the owner asking for them beside resources and items: the 254 items the sub-areas list as
  harvestable and the 23 professions, decoded from the client's BC7 textures and stored as PNG in a pack of format 4, about
  1 to 2 MB, released as every pack is (D10, D24); the elements' graphics with them, which would let a tour know a zone
  before it goes there (D128). Measured: `Picto/Items` holds 12,279 item icons, 64 px at 1x and 128 at 2x, one sprite
  each named by the item's `iconId`, in BC7; the pack keeps no icon and no `iconId`.
- **6c**: what 6a's measures leave: fights while harvesting, the resources' growing back, harvest statistics, the
  guild's chest the owner noted, and options the owner listed for later, such as opening resource bags and healing
  under a share of life, each a request and an event to map; then the owner's call on making the repository public (D4).
- 6a's views keep the icons' place, a neutral square, which 6b fills.
- **Rejected.**
  - 6a cut into the activity, then the app: the owner wanted to verify in the app from 6a on.
  - Every icon, 12,279, in a pack of their own: 50 to 60 MB to download, for an app showing fewer than 300.
  - The icons read from the installed client as they show: nothing distributed, but the Unity readers and a BC7 decoder
    in `src/`, with their tests, reading bundles in the process that holds every connection (D91).
  - The icons in 6a: an extractor, a decoder and a release more in a session the milestone filled.

## D127: The harvesting module, its settings, and the profile editor

*2026-10-29, step 6, session 6a; the module and settings as the lead recommended them, the owner asking for more: resources
of any profession, sub-areas suggested by them, the zone's figures, the levels missing, and an editor more beautiful,
chosen among two mockups. Builds D87, D93, D98, D101. Maps as settings still wait (D123).*

- **The module** (`HarvestingModule`, in `D3B.Bot.Harvesting`), in `DefaultModules` after the bots module, registers the
  activity `harvest` (`HarvestActivity`), the host's sightings (D128) and the previews below; its texts are a `.resx` of
  its own in English and French (D96), `D3B.Host` running it without the app. `Register` adds it to a host that builds its
  services apart, as a test's.
- **The settings** (`HarvestSettings`), in three sections: what it harvests, resources of any profession in their order,
  the first harvested first, or everything each character can harvest at its level; where, an area and sub-areas, one or
  the other at least, an area alone standing for its sub-areas that list one of the resources, sub-areas chosen of any
  area; when it banks, pods full at a share from 50 to 100 %, 90 by default, and what goes to the bank, what harvesting
  brings or everything but what is worn, no kamas, which pay the zaaps and the bank's opening (D80).
- **D98 gains three mechanisms**, for any activity: a sub-area setting suggested by a resource setting (`SuggestedBy`), the
  sub-areas where they grow offered first, the most of them first, then the lowest (`GameReferences.SubAreasHolding`); a
  number that is a percentage (`Percent`), shown as a bar; and a preview the activity reckons from its settings and the
  pack (`Preview`, `SettingsPreview`), figures and the maps of its zone. `GameReferences` takes the world graph, so that a
  sub-area counts the maps a character may travel to, and says who gathers a resource (`TryGetSource`).
- **The harvest's preview** (`HarvestPreviews`): the maps of the zone, the resources asked for its sub-areas list, and the
  changes from the nearest bank's front to the zone, a door more, for a character of whom nothing is known. Measured on the
  pack of 3.7.12.12: 34 sub-areas hold ash or chestnut, suggested in 14 ms; the preview of a zone of 128 maps, its path to
  the bank included, in 50 ms.
- **The editor**, the generic one of D101 grown: resources as tiles in their order, each with its profession and level, the
  icon's place, and a warning when a character playing the profile lacks the level, as its bot last saw it; the zone's
  sub-areas beside its mini-map, each map a square at its coordinates on the world map from the atlas, the zone's parts
  tinted, the maps around dim and the nearest bank marked (`ZoneMapCanvas`), the sub-areas suggested as rows a click chooses,
  and the preview's figures; the share of pods as a bar, and a choice of three values at most as segments. A field may take
  a whole row of its section (`CardGrid.IsWide`). Drawn without a screen in both languages and both themes, and looked at.
- **Rejected.**
  - A project `D3B.Harvesting`: a project more for a module, `DefaultModules` living in `D3B.Bot` with the behaviors it
    calls.
  - The area required: sub-areas of two areas could not mix.
  - An editor of the harvest's own: the mechanisms serve every activity, the blueprints of step 7 among them.
  - Maps as settings in 6a, a route or maps to avoid (D123): the sub-areas carry the milestone.

## D128: The next resource and the next map: tours through the zone, the nearest first

*2026-10-29, step 6, session 6a, chosen by the owner, as the lead recommended it; the insides last after a measure.*

The references, read for ideas (D3): BillBot alone chooses by itself, the nearest resource on a map, and between maps a
search scoring each by the time since its visit, capped at an hour, times its worth and a random factor, the maps never
seen first; it learns, while playing, which graphic is which resource, and keeps it on disk. The owner's profession scripts
follow fixed routes by level. None measures how long a resource takes to grow back.

- **On a map**: of the elements the server offers a wanted resource's skill on, at the character's level, a profession
  not described yet counting as the first, the resource asked for first, then the nearest by its walk; one taken by
  another, refused or out of reach is left for the visit.
- **A tour** (`HarvestTour`): a map with nothing left is gone through, and the step travels to the nearest map of the
  zone still to go through, stopping on the first of the way; once every map is, the next tour begins. A tour that
  harvested nothing waits up to a minute on its map for a resource to grow back before the next sweeps the zone again.
- **What the host's bots saw** (`HarvestSightings`): each map's resources, harvested ones included, as the server
  described them; a tour leaves out the maps seen holding none of its own. In memory, shared by the host's bots, nothing on
  disk, as the banks' insides (D79).
- **The insides last.** Measured on the client: a first tour went through a forest's houses for minutes, a journey of 12
  changes through a zaap among them, before its first harvest. Measured on the pack: 114 of the 186 sub-areas listing
  resources hold insides too, 1,493 maps against 6,569, and the mines are insides of such sub-areas, 36 of them with a
  miner's resource. So a tour goes through the maps of the world map first, then its insides (`HarvestZone.Insides`).
- **Failures**: a map the way to failed leaves the tour; three failed journeys in a row end the activity, which says why
  (D131); a zone with no map, or none holding a resource, ends it too.
- **Rejected.**
  - BillBot's scoring: chance and weights nothing measured sets, for what the tours give, the maps gone through longest
    ago first.
  - A route the user draws: maps as settings, later (D123).
  - The nearest alone on a map: the owner asked for a priority.
  - The insides left out: the mines are insides.

## D129: Banking full, and the way back

*2026-10-29, step 6, session 6a, chosen by the owner, as the lead recommended it; the reason said after a measure.*

- **Full** at the settings' share of pods, before each step (D87). **Unloaded** at the nearest bank (D79, D80): what the
  settings say goes, in one request, no kamas; two more tries after a failure that may pass, no answer or a journey
  missed. **The way back**: the next step starts from the state, the character off the zone, and travels to the nearest
  map of the tour, zaaps included for a subscribed account (D83); the map it left stays in the tour while it held
  resources.
- **Still full once banked.** Measured on the client: the bank took the harvest, 3 stacks, and the 4,958 pods left of
  7,590 were still above the share, the character carrying other things; the activity failed with nothing said on the
  card. It now ends there with its reason in the app's language, the weight left and what to change, to bank everything
  but what is worn or raise the share; any activity left full says so (`ActivityRun.IsStillFull`).
- **Risk**: the bank's opening costs a kama per stack the storage holds (D80); a character without them is not measured.
- **Rejected.**
  - A bank chosen in the profile: the nearest carries the milestone.
  - The guild's chest: 6c, its messages to map.
  - Back to the exact map: no position kept (D87); the tour brings it back when it is worth it.
  - Going back to the bank at every step while still full: the reason said instead.

## D130: A fight, a new connection, the player's pause: the activity resumes from the state

*2026-10-29, step 6, session 6a, chosen by the owner, as the lead recommended it. Builds D87 to D89.*

- A fight, a protector's or an aggression or the player's own, interrupts the step; the activity waits for its end,
  the bot not paused (D88), recovers, and the next step finds where the character is: in the zone it goes on, elsewhere it
  travels back, full it banks first. Until step 9 the player fights.
- A new connection takes the bot back (D65, D89); the tour and the sightings stay, the bot outliving its connections.
- The player's pause stays D66's, resumed from the card.
- The activity says what it waits for (D131).
- **Rejected.**
  - Stopping after a fight: the character left where it fought. An option for it may come in 6c, with the fights while
    harvesting.

## D131: An activity says what it does: its report, on the bot's card and the map page

*2026-10-29, step 6, session 6a, chosen by the owner among mockups, as the lead recommended it.*

- **The report** (`BotActivity.Report`, `ActivityReport`, in `D3B.Bot.Activities`), any activity's, replaced whole by the
  activity and read by the app every 250 ms (D94): what it does, how far it is (`ActivityProgress`), what it counts
  (`ActivityCount`), its warnings, and what it looks at on the map (`ActivityFocus`): the element it goes for, those it
  wants, the next map, and the maps of its zone with where it stands in each (`ZoneMap`). An activity that cannot go on
  says why (`ActivityException`).
- **Its texts name the game's values by their ids** (`GameText`, `GameTextValue`, in `D3B.Bot.Localization`), as the
  journal does (D112), the app naming items, professions, sub-areas and maps in its language (`GameTextFormatter`); a text
  that counts gives its plural, which the app chooses as its language has it.
- **The bot's card**: what the activity does, its tour as a bar with its map and the time it ran, what it harvested of
  each resource with the icon's place, the banks, and the levels missing; how it ended, its reason included. A run
  remembers the name it was started under (`ActivityRun.Title`): a profile started on connection shows by its name.
- **The map page**: a line of what the activity does and how far; a layer, Activity, ringing the elements it wants, the
  one it goes for brighter, and the exit it heads to; and beside the map, the tour's mini-map, the maps gone through, to go
  through and seen empty, with the character.
- Drawn without a screen in both languages and both themes, and looked at.
- **Rejected.**
  - A harvest's own display: the report serves every activity, the blueprints' among them.

## D132: Banking lets the client show its windows

*2026-10-29, step 6, session 6a. Builds D77, D80.*

- **Measured** on a capture through the app: D3B replied 1 ms after the banker's question and left the bank 39 ms after it
  opened, the whole bank in 130 ms; the server closed the dialog and the bank, and the client asked to leave each, but both
  windows stayed on its screen, the owner reported. The player, in the same capture, replied 771 ms after the question.
- **So** banking waits half a second, D77's, after the question, after the bank opens, and before leaving a dialog it could
  not take to the bank (`Banking.ShowDelay`). Measured again: the reply 547 ms after the question, the deposit 1,085 ms after
  the action, the bank left at 1,118 ms, the client's two requests to leave at 1,152 and 1,179 ms; the windows closed.
- This is no timing shaped to look human (D2): the client must have shown a window to close it, as it must have drawn a
  map to follow a walk.

## D133: Harvesting, measured through the app

*2026-10-29, step 6, session 6a.*

Measured on a client of 3.7.12.12 through the app, captures on, four runs of the app, the owner's character harvesting
nettles, its profile "Ortie" ([client.md](client.md#the-game-harvested)):

- **From map to map**: harvests on one map after another, the tour going on to the nearest map still to go through.
- **A fight** interrupted the activity, which went on once it was over, with no command.
- **The player's pause**: the player's click during a journey paused the bot; resumed from the card, it went on.
- **A new connection**: the client closed during the activity; the new connection took the bot back, and it went on.
- **Stopped** from the card, it ended at once.
- **A profile started on connection**, never measured before (D99, D118): three times, the profile started once the
  client chose the character.
- **Full**: to the bank of Bonta through a zaap and its door, the banker 6394, the stacks put in, the bank left; still full
  once, the reason said (D129); the windows left open, then closed (D132); then back to the zone through a zaap.
- **Found**: the houses of the zone first (D128); a journey of 12 changes to maps of the zone reachable only far round; walks
  the client did not confirm (D85) on a map of the zone, the journey going another way.

## D134: No human timings

*2026-10-29, step 6, session 6a. The owner asked for human timings, as BillBot's; they stay out (D2).*

- D2 keeps out of D3B every timing shaped to avoid Ankama's detection, BillBot's `human_timings` named among them, and
  `CLAUDE.md` says it again. D3B's delays are those the client needs to follow what the server shows it, each measured:
  D77's for a map, D132's for a window.
- Should the owner want to change D2, it is a change of the brief, decided apart; this session does not add them.

## D135: The icons of 6b: the items a harvest names, and the professions

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it. Builds D126.*

- **Measured** on client 3.7.12.12 (client.md): the 254 items the sub-areas list as harvestable, and the 33 a skill of
  `Base` gathers, such as drinking water from a well, potatoes or shells, which no sub-area lists: 287 items, drawn by
  only 121 icons, loot boxes sharing theirs. 22 of the 23 professions have an icon, 21 in all, two sharing one; `Base` has
  none. `Picto/Items` holds 12,279 icons.
- **In the pack**: those 121 icons and the professions' 21, by their identifier; items and jobs keep theirs (D139). An
  item without an icon in the pack keeps the neutral square.
- The owner noted, choosing, that a bot harvests more than professions' resources, wells among them: their icons are in,
  and harvesting them waits for 6c (D140).
- **Rejected.**
  - The 254 items alone: the wells and the rest of `Base`'s would keep the neutral square.
  - Every icon, D126's: 50 to 60 MB.
  - Characters' classes, which the bot does not know yet, and monsters', at step 9.

## D136: Icons 64 pixels square, in raw RGBA

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it. Supersedes in part D126: no PNG.*

- **Size**: items' icons at 1x, 64 pixels, professions' at 2x, 64 pixels too. The app draws them from 16 to 34 pixels; the
  owner's main screen is 2560x1440 at 100 %, and 64 pixels stay sharp up to 188 %.
- **Format**: 8 bits a channel, red, green, blue, alpha, the alpha straight, rows from the top, one array per kind of icon
  (`IconTable`), which the pack's zstd 19 compresses. Measured: items' icons at 1x take 474 KB so, 500 to 620 KB as PNG.
- **Why not PNG**: larger, an encoder to write, and bytes depending on the zlib of .NET's runtime, where the pack must
  stay deterministic (D25); the app makes a bitmap from the pixels as they are.
- **Rejected.**
  - Every icon at 2x: 1.4 MB, the same at 100 %.
  - PNG, D126's.
  - The client's BC7 blocks as they are, decoded by the app: a decoder in `src/`, which D126 rejected.

## D137: BC7 decoded by AssetRipper.TextureDecoder, in the extractor alone

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it.*

- `scripts/extract.cs` references AssetRipper.TextureDecoder 2.6.3: MIT, managed, for .NET 10, without dependency, the
  decoder AssetsTools.NET.Texture uses. Nothing in `src/` decodes: the pack holds pixels (D136). Added to D15.
- `IconsExtractor` reads each icon's texture and sprite by the typed readers, and fails on a texture that is not BC7,
  64 pixels square, one level, its texels in the bundle's `.resS`, or whose sprite's rectangle does not cover it whole; the
  rows are turned top first, Unity storing the bottom first.
- **Measured**: the 142 icons decoded right, looked at; the bundle of 12,279 item textures read in 1.4 s.
- **Rejected.**
  - A decoder of our own: about 400 lines of BC7's partition tables, without tests in `scripts/` (D10).
  - BCnEncoder.Net: an encoder first, with two dependencies.
  - Kyaru.Texture2DDecoder, AssetStudio's, which Dofus Remastered uses: native binaries per system.

## D138: The maps' graphics, learned by the host, foretell a tour

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it; what is learned changed after a measure.
Builds D128.*

- **Measured** (client.md): the client's data never says which interactive an element is, its dictionary of element
  definitions included; the server says it of each element of the map a character enters. On 31 captures of 6a, the
  elements one graphic draws are mostly one resource: nettle's elements share one graphic, on 775 maps of the world. Fishing
  spots share one between seven fish. Learned on all captures but one, the graphics foretold the last right 102 times of
  103. The 53 sub-areas listing nettle hold 3,133 maps, 750 of them drawing its graphic.
- **In the pack**: each element's graphic, `MapInteractive.GfxId` (D139).
- **What the host learns** (`GraphicSkills`, `GraphicLearner`): for each graphic, every skill the server says its elements
  offer, enabled or not, none for an element that offers none; from every map's information any bot receives, whatever
  its activity. Only the elements the server lists teach: measured, it leaves out a third of the pack's elements of the
  maps it describes, some drawn with a resource's graphic elsewhere.
- **Where it is kept**: D3B's database, a table `Graphics`, its skills as JSON (`GraphicRecorder`), read before the host
  starts, written as the bots learn; from one run to the next, and from one client build to the next, a graphic being an
  asset of the client; a contradiction only adds a skill. Whether a client update renumbers graphics is Q14's.
- **What a tour does with it** (`HarvestZone.Foresee`, `HarvestTour`): a map never seen is probable when one of its
  graphics offers a skill the zone uses, without a resource when every one is known and none does, a map without elements
  included, and unknown otherwise; what a bot saw of a map wins. A tour goes first to the probable maps, the nearest
  first, then to the others of the world map, then to the insides, and leaves out those without a resource.
- **In the app**: the profile's preview counts the probable maps and marks them on the zone's mini-map, the maps left out
  dim; the tour's mini-map shows them apart, with a legend (D141).
- **Rejected.**
  - Kept in memory, as the maps seen (D128): every run of the app would learn again, and the preview could not foretell.
  - Every element of a described map teaching, the unlisted ones as offering nothing: some draw a resource's graphic.
  - A table of graphics learned from the maintainer's captures, in the pack: the server's word in a pack of the client's,
    and only the maps the maintainer went through.
  - Nothing in 6b, the tours staying 6a's.

## D139: Pack format 4, and its release

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it. Builds D24, D25, D33.*

- `GamePack.FormatVersion` is 4: `Item` and `Job` gain their `IconId`, `MapInteractive` its `GfxId`, and one entry,
  `icons/catalog` (`IconCatalog`), holds the items' and the professions' icons apart, the two numbering theirs apart; read
  once by `GameTables`. The fingerprint covers the icons' two bundles. `PackFormatTests` records format 4's layout.
- **Measured** on 3.7.12.12: 5,382,920 bytes against 4,670,325, extracted in 18.4 s, two extractions giving the same
  SHA-256; the icons 0.58 MB compressed, 2.33 MB read in 15 ms; the graphics 0.10 MB more on the maps, the icons' ids
  0.03 MB on the items.
- **Release**: `data-4.pack` and its SHA-256 go to the release `client-3.7.12.12` beside `data-3.pack`, which older hosts
  keep, through `scripts/release.cs --publish`, on the owner's go.
- **Rejected.**
  - A pack of icons of its own: a second source and a second download for 0.6 MB.
  - An entry per icon: 142 entries for a catalog read whole in milliseconds.

## D140: Base's resources harvested in 6c

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it.*

- The harvesting activity of 6a harvests professions' resources only. Wells, potatoes and the rest that skills of `Base`
  gather wait for 6c: no sub-area lists them, so only the graphics learned (D138) say where they are, and their settings
  have no profession's level. Their icons are in the pack already (D135).
- **Rejected.**
  - In 6b, whose icons, extractor, release and graphics filled the session.

## D141: The icons in the app

*2026-10-29, step 6, session 6b; the lists, the profession's icon and the map asked for by the owner while verifying.*

- **Where**: the resource tiles, their icon on its plate, their profession's beside the level; the searches adding a
  resource, the resource's icon and the profession's beside its detail; the bot's card, beside what it counts; the live
  map, a resource drawn by its icon over its cell, faded once harvested. The pack's icons become bitmaps once per pack
  (`GameIcons`); a pack without icons, or an item without one, keeps the neutral square.
- **The owner's asks**: a search's detail under its name, so that its list keeps its width as it scrolls, in the three
  searches of the profile editor; the profession's icon at 20 pixels, at 14 too small to read; the resources' icons on the
  map.
- Drawn without a screen in both languages and both themes with icons of the tests' own, discs of a colour, and looked at.

## D142: 6b measured through the app

*2026-10-29, step 6, session 6b.*

Measured on a client of 3.7.12.12 through the app, captures on, the owner's profile "Ortie", nettle and sage in Amakna
([client.md](client.md#the-game-foretold)):

- **The icons**: the owner saw them on the resource tiles and in the search adding one.
- **Learning**: a first run, nothing learned before, entered 43 maps in 3 minutes and learned 35 graphics, which the database
  kept for the next run.
- **Foretelling**, from the knowledge each map was entered with: 18 maps foretold probable, 13 of which held nettle or sage,
  11 harvested, the 5 others drawing a resource's graphic only on elements the server did not list; 5 foretold without a
  resource, none holding one; 20 unknown, 1 holding one.

## D143: A pass on the app's UI, UX and options, in a session of its own

*2026-10-29, step 6, session 6b, chosen by the owner.*

- Verifying 6b, the owner noted many small things to improve in the app's interface, its ease of use and its options:
  margins, paddings and sizes inconsistent across the app, and many of ShadUI's components left unused. A pass over every
  page, in a session of its own after 6c: 6d in the roadmap. 6b fixed only what it drew (D141).
- **Rejected.**
  - The pass in 6b: every page of the app, past what this session drew.

## D144: A fight while harvesting: resumed, paused or stopped, as the profile says

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D88, D130.*

- **Measured** on the 34 captures before 6c (client.md): 4 fights. 3 the player's, the client sending its walk's
  confirmation and a request of 49 bytes the readable protocol lacks, the attack, in one read; 1 a resource's protector
  during 6a's activity, beginning 2 ms after the end of the character's own harvest, with no request from the client or
  D3B, over 64.5 s later. No aggression captured; how a fight ended is not decoded, and no message read gives a fight's
  kind: an event of 38 bytes, one `int64`, comes before each, but often elsewhere too.
- **The kind, told by its timing**: a protector's when the fight begins right after the end of the character's own
  harvest; an aggression's when the bot was acting and nobody attacked; the player's when the bot was paused, the player's
  walk having paused it (D66). The journal says it, and the bot's card counts fights.
- **A setting**, "After a fight", in three segments: resume, the default and D130's; pause, the bot paused as by the
  player's hand, resumed from its card, its tour and counts kept; stop. Until step 9 the player fights.
- **To measure**: an aggression, and a fight lost, where the character comes back and with how much life.
- **Rejected.**
  - Stopping on every fight: a protector would cost the tour.
  - Avoiding protectors: they come at random.
  - Giving the fight up: a fight's action, step 9's.

## D145: The resources' growing back, learned by the host

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D128, D138.*

- **Measured** on the captures before 6c: no growing back seen whole, the character never staying on a map long enough.
  Bounds alone: nettle seen grown back at most 445 s after its harvest; trout not yet 297 s after, grown back at most
  564 s after. A harvest lasts 3 s, drinking water from a well 1.5 s. The server never says when an element will grow
  back: it shows it once it has, and on entering a map. A bound seen on a later visit holds only from above, another
  player having maybe harvested the element meanwhile.
- **What the host learns**, for each resource: how long it takes, exactly when a bot stayed on the map until its element
  grew back, else the smallest upper bound a later visit gave; kept in D3B's database, as the graphics are (D138).
- **What a tour does with it**: a map whose wanted elements all still grow back waits until they should have; once every
  map of the tour does, the bot waits on its map until the first should have, rather than D128's minute. A resource
  whose growing back is not known yet keeps D128's behavior.
- **In the app**: the tour's mini-map shows the maps growing back; the bot's card says when the next one should.
- **To measure** through the app: exact times, and whether one resource always takes the same; if not, the longest seen.
- **Rejected.**
  - A table of times written by hand: 85 resources to measure, and the next build may change them.
  - Measured alone, in client.md: a small zone would still be gone through for nothing.
  - Learned in memory alone: learned again at every run of the app.

## D146: Harvest statistics, in D3B's database and a page of their own

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it.*

- **Each run kept**: the character, the profile, when it started and ended and how, the items it brought by item, each
  profession's experience gained, the banks and the fights by kind; written at each banking and at its end, so that a
  crash loses little. Structured, not the harvest's own: any activity may give its figures.
- **A page, Statistics**, in the rail: by character and period (today, 7 days, everything), four figures (resources,
  time, rate an hour, levels gained), each resource with its icon and rate, and the runs.
- **On the bot's card**, live: each resource's rate an hour, and the experience gained.
- **Rejected.**
  - Live alone, nothing kept: what the owner asked to see over time would go with the app.
  - In the profile editor: figures belong to characters as much as to profiles, and the editor is long already.
  - Their worth in kamas: the marketplace's prices, step 11's.

## D147: The guild's chest, measured first

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D80, D129.*

- **Known**: no capture opens it. JondoEmu, read for ideas (D3) and never tested against Ankama's server, has it an
  element in banks, graphic 70671, with tabs whose rights go by rank and a new tab at guild levels 3, 7 and 11, opened as
  a storage exchange like the bank's. The pack of 3.7.12.12 draws that graphic in 21 insides, all in the world graph.
- **Two settings** in the banking section: where it deposits, its own chest in the bank or the guild's, and the tab.
  The guild's chest of the nearest bank holding one takes what the bank would, no kamas; refused, for want of a guild,
  rights or room, the character deposits in its own chest and the card says so.
- **Measured first**, in a capture the owner makes by hand: opening the guild's chest, changing tabs, putting a resource
  in and taking it back, and a deposit refused if one can be: the requests and events to map.
- **Rejected.**
  - The guild's chest chosen whenever the guild has one: the owner's harvest in the guild's pool without asking.
  - Stopping on a refusal: the deposit has a place to go.

## D148: Upkeep: resource bags opened, the character healed under a share of its life

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it.*

- **Known**: item type 100, "Sac de ressources", holds 73 items of the pack, such as "Sac de blé"; the pack keeps each
  item's type. Nothing needed is mapped: using an object, which both opening a bag and eating are, the character's life
  and its most, and sitting.
- **A section, Upkeep**, in the profile: a flag opening each resource bag as soon as a harvest gives one, between steps, so
  that what it holds is banked with the rest; a share of life, off by default, under which the bot heals before its next
  step, eating the items the profile lists in their order, else sitting until its life is whole. The card shows the life
  above the pods.
- **Measured** in the owner's capture of D147: opening a bag by hand, sitting, eating, the life written down before and
  after, compared on the machine and never written in the repository.
- **Rejected.**
  - Healing by waiting alone: slow without food.
  - Healing at step 9, with fights: the protectors raise it now (D144).

## D149: A zone's isolated maps, left out unless the profile takes them

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D128, D133.*

- **Measured** on the pack of 3.7.12.12: 67 of the 186 sub-areas listing resources fall apart through their own maps'
  transitions, and 588 of their 8,062 maps lie off their sub-area's main part, the part holding the most maps. From it, 54
  are 2 to 4 changes away, 128 are 5 to 35, as 6a's journey of 12, and 416 are reached only by a teleport or a door's
  criterion. The worst, sub-area 601: 222 maps, 141 in its main part, in 25 parts.
- **Isolated**: a map of a sub-area off its main part, more than 4 changes from it through the world graph, teleports
  aside (`HarvestZone.Isolated`, `MostDetour`), reckoned with the zone.
- **Left out by default**; a flag, "Go through the isolated maps too", takes them, last in a tour, a part at a time. The
  profile editor's mini-map shows them, dashed, and its preview counts them.
- **Rejected.**
  - Kept always, last: the long journey would come back each tour.
  - Left out always: some isolated parts may be rich.

## D150: Base's resources harvested as the others

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Settles D140.*

- **Known**: 34 skills of `Base` gather 33 items, drinking water from a well, potatoes, shells and events' items among
  them, all at level 1; no sub-area lists them; their icons are in the pack (D135). The owner's database knew 46 graphics,
  none offering a skill of `Base`. An index of every map's graphics, 10,981 maps and 3,345 graphics, takes 564 ms.
- **Offered** among the resources, without a profession or level (`GameReferences.IsBase`); a tile and a search say on how
  many maps the learned graphics place one, or that it was never seen and will be learned on the way
  (`GameReferences.PlacesOf`, `GraphicPlaces`).
- **Placed** by the learned graphics: the sub-areas suggested for them, and those an area alone stands for, are those
  holding a map that draws a graphic known to offer their skill.
- **Harvested** as the others: the probable maps first, the unknown learned on the way, no level asked; "what harvesting
  brings" banks them. "Everything each character knows" stays the professions': `Base`'s skills gather events' items too.
- **Rejected.**
  - The wells only.
  - A table of wells from the maintainer's captures, in the pack: D138 rejected it.
  - Learning at once from the owner's earlier captures: a tool more for what the first visit teaches.

## D151: The client's display time, and every delay of D3B in a settings page

*2026-10-29, step 6, session 6c; the owner asked for longer delays, the bots doing "anything" on small machines, then for
a page holding every delay. Builds D77, D132. Superseded in part by D162: the language and the theme on the settings
page alone.*

- **Why**: D3B waits half a second after the server placed the character on a map before acting (D77), and half a second
  for a window to show before closing it (D132), both measured on the owner's machine. On a slower one the client has not
  drawn yet, and what D77 and D132 measured comes back: walks the client does not follow (D85), windows left open.
- **One setting for the machine**, not a profile's: the client's display time, 0.5 s by default, up to 3 s, wherever D3B
  waits for a map or a window. The other delays, how long D3B waits for the server's answers, a walk's arrival, a use's
  end, a recovery, and the relay's, also become settings, each with its measured value by default and a button to restore
  them.
- **A page, Settings**, at the foot of the rail: the client, the delays, the captures (from the next start), the language
  and theme (kept in the title bar too), and where the database and the pack are. Kept in `app.json`; `D3B.Host` reads the
  same values from its configuration. 6d and 6e add their own.
- **Rejected.**
  - A switch for slow machines, doubling the delays: no value to choose.
  - Measured by D3B from the client's pace: longer to make and to check.
  - Random delays between a least and a most: see D152.

## D152: The owner's harvesting settings in 6e; no human or random timings

*2026-10-29, step 6, session 6c. Its settings made by D165 to D168.*

- **6e, chosen by the owner**: settings of harvesting and journeys, each submitted with mockups: no zaap when the
  destination is too near; an automatic mode choosing the zone by the character's level; the highest-level resources
  first, without leaving the others; lists of resources to take in or leave out; profiles given by default, such as the
  automatic harvest; and what the owner adds.
- **Asked and refused**: delays longer and "more human", then drawn at random between a least and a most. A random delay
  helps the client in nothing, which needs only enough time to draw; it serves only to look human, to escape Ankama's
  detection, which D2, D134 and `CLAUDE.md` keep out. The owner asked to remove the line from `CLAUDE.md`; the lead did
  not, and will not write such timings whatever the file says. D151's delays are the client's own, measured, raised by
  hand where a machine needs it.

## D153: Upkeep and the guild's chest, measured: mapping format 7

*2026-10-29, step 6, session 6c, in a session of its own. Amends D147 and D148 with what the owner's capture measured
([client.md](client.md#the-game-kept-up)).*

- **Named and pinned** for 3.7.12.12 (D54): `ObjectUseRequest`, one request to eat and to open a bag;
  `ObjectDeletedEvent`; the characteristics, partial (`CharacterStatsEvent`), holding the life; `LifeRegenEndEvent` and
  `LifeRegenStartEvent`, partial; `ExchangeStartedWithTabbedStorageEvent`, partial, of the exchange type 21, now
  `EXCHANGE_TYPE_GUILD_CHEST`; `ExchangeObjectMoveRequest`; `StorageObjectUpdateEvent`; `StorageObjectRemovedEvent`, partial.
- **D148, the life.** The most life is characteristic 0 plus 11, the life that plus 97, the life lacking; out of fights it
  regenerates a point every half-second, the interval the server gives, from the characteristics' life up to the most. The
  card's life and the share under which the bot heals read it so.
- **D148, healing.** Sitting changes nothing, the owner saw: the bot eats the profile's items one at a time, each answered
  in 28 ms with the life healed, and with none left waits for the regeneration, sitting never. A use at full life is
  refused with a text, nothing used. Using several at once is measured, the server using only what the life lacks, but not
  named: its request holds a map, which the tests' obfuscation does not make.
- **D148, bags.** A bag opens with the same request, its content joining the inventory, its stack lessened or deleted. A
  barrel of type 184, "Conteneur", opens the same way; D148 names type 100 only, and the lead decides whether to open other
  containers.
- **D147, the chest.** It is an element of the bank's inside, used with skill 184, with no NPC's dialog. Stacks go one at a
  time, whole or in part, by `ExchangeObjectMoveRequest`; the bank's list was not tried on it. An object bound to the
  account is refused with text 356 and stays in the inventory, to go to the character's own chest as D147 says; the
  inventory's objects carry effects 981 or 983 then, which the readable protocol does not read. It holds no kamas.
- **D147, the tab.** Not measured: the owner's guild has one tab. Until a guild with several is captured, the bot deposits
  in the tab the chest opens on, and the tab's setting waits.
- **Mapping.** The readable protocol holds 129 messages, 263 fields, 19 enums and 147 values. `MappingFile.FormatVersion` is
  7, `mapping-7.binpb`, 8,926 bytes, the same bytes on two runs, audited without failure; the 36 game connections captured
  decode through it, 25,258 frames, none misread. The release of 3.7.12.12 needs it, which the release tool publishes
  (D50) once the owner says so.

## D154: What the first check through the app found, and the time between two actions

*2026-10-29, step 6, session 6c, the owner verifying on the client; the time between two actions chosen by the owner, as the
lead recommended it. Builds D151, amends D147 and D148.*

- **Too fast**: the bot ate 19 breads 30 ms apart, from 766 to 1,521 life, and put 5 stacks in the guild's chest 28 ms
  apart; the owner saw the client unable to follow. So the client's display time passes between two objects used and two
  stacks moved, and after each step of an activity, each an action such as a harvest, a journey or a deposit: a delay of
  its own, `BotDelays.BetweenSteps`, the display time's half second at least, raised in the settings page. Fixed, never
  drawn at random: the owner asked again for a least and a most drawn at random, as BillBot's, which D152 keeps out.
- **The multiple use**, which D153 measured and left unnamed: the owner asked for it, food then eaten in one request, the
  server using what the life lacks. Named in the protocol by the session that mapped D153 (D156).
- **The guild's chest** took 4 stacks whole and part of a fifth, 9 of 52 left in the inventory; then the character, carrying
  other things, was still above its share of 50 % at 77 %, and the activity ended still full. So a part taken counts as
  refused, and what the guild's chest leaves goes to the character's own chest before the activity says it is still full.
- **What goes to the bank** stays the setting's: "what harvesting brings" leaves what the owner took from the bank.

## D155: 6d, a redesign of the app's interface

*2026-10-29, step 6, session 6c, chosen by the owner. Amends D143.*

- Verifying 6c, the owner found the app a catch-all: too much text, too many explanations, views cut, not pleasant. 6d is
  no longer a pass over spacing: a redesign, mockups first, page by page, the profile editor first, help texts short or
  gone, one scale of spacing and sizes, ShadUI's components where they fit.
- **Rejected.**
  - The redesign in 6c: 6c ends with its check on the client and the call on the repository (D4).

## D156: Using several objects at once named: mapping format 8

*2026-10-29, step 6, session 6c, in a session of its own, as the lead asked: healing ate 19 breads one by one on the
client, and the owner asked for one request. Supersedes D153's "not named" ([client.md](client.md#the-game-kept-up)).*

- **Named and pinned** for 3.7.12.12 (D54): `ObjectUseMultipleRequest`, the stack's uid, the quantity to use, and a message
  no capture sets, holding a `map<bool, string>` and a list of `int64`, named neutrally (D42). Asked for the whole stack
  below full life, the server used only as many as the life lacked, the last in part, in one answer; at full life, a text
  alone, and nothing is used. The bot heals in one request.
- **A map in the readable protocol**, the first. Its entry is a message protobuf makes, which the matcher pairs through the
  map's field and the audit checks as any message, its key and value by number and type. The translation gives it no
  parser: it has no class, and no frame carries it alone. The tests' obfuscation renames it as any nested message and keeps
  its option, so that the audit's tests cover a map read as a list, or of another value's type.
- **Mapping.** The readable protocol holds 132 messages, 270 fields, 19 enums and 147 values. `MappingFile.FormatVersion` is
  8, `mapping-8.binpb`, 9,150 bytes, the same bytes on two runs, audited without failure; the 37 game connections captured
  decode through it, 26,090 frames, none misread. `mapping-7.binpb`, published, is left as it is (D50). The release of
  3.7.12.12 needs format 8, which the release tool publishes once the owner says so.

## D157: 6c checked through the app

*2026-10-29, step 6, session 6c.*

Checked on a client of 3.7.12.12 through the app, captures on, the owner's profile "Ortie" harvesting in Amakna, the
guild's chest chosen, bags opened, healing under 80 % with Carasau:

- **First run**: the bot ate 19 Carasau one by one, 30 ms apart, from 766 to 1,521 life; put 5 stacks in the guild's chest
  28 ms apart, the chest taking part of the fifth; then ended still full, other things weighing 77 % of its pods over a
  share of 50 %. The owner found it all too fast. D154 followed: the time between two actions, the guild's chest's
  leftovers to the character's own, and the multiple use (D156).
- **Second run**, 11 min 37 s: two deposits in the guild's chest, 7 and 10 stacks, each a display time apart, the second
  left as it should, then the harvest went on; five runs stopped by the owner; no error in the host's log. The owner found
  it good.
- **Not measured**: an aggression, a fight lost, a growing back watched whole, healing in one request on the client, and
  Base's resources found by the graphics; the tests cover them.

## D158: The repository public, once the app ships with its data and updates itself

*2026-10-29, step 6, session 6c, chosen by the owner. Settles D4's milestone: the first milestone is reached (D133).*

- **The owner's call**: the repository goes public; the data pack, schema and mapping ship with the app, which updates
  itself by fetching the repository's releases.
- **Said before choosing**: a public repository makes its releases public, so the data extracted from Ankama's client, the
  maps, texts and icons, become downloadable by anyone. The owner chose so.
- **Not now**: the app has no build published and no update yet. A session of its own, 6f, publishes the app with its data,
  makes it update from the releases, and turns the repository public on the owner's go; nothing is public before.
- **Rejected.**
  - Public now, the releases as they are: the app would still need the GitHub CLI's token, and no build to download.
  - Each user extracting their own pack: the owner wants the data delivered with the app.

## D159: The profile editor, a page of its own, its preview beside; one scale for the app

*2026-10-29, step 6, session 6d; the editor chosen by the owner among three mockups, as the lead recommended it, the
owner asking for toasts, dialogs and the resources' modes (D160); the scale the lead's. Supersedes D101's editor unfolded
in place. Builds D127, D155.*

- **What the survey found**, every page drawn without a screen in both languages and themes and the app run on the owner's
  data: no scale, 20 spacings, some 30 paddings, 13 radii, icons in 7 sizes and 10 font sizes; the same classes redefined
  by each view with other values; pages 864, 964 or the whole width wide; 22 texts over 80 characters and 39 helps, details
  or empty states; the editor some 2,000 pixels long, its preview lost among the sub-areas; icon buttons without a name
  for a screen reader; ShadUI's cards, badges, field labels and hints, clearable boxes, toasts and dialogs unused.
- **The editor** opens on the page, the list gone, behind a way back: the profile's name, its activity, cancel and save in
  its header, delete and duplicate in a menu. Its settings go in a column, a card per section, each setting a row, its name
  left and its control right, a list or a zone the card's width; the activity's preview beside, the zone's mini-map, the
  figures and the characters playing the profile, in view as the settings change.
- **Less text**: an activity's help shows in a tooltip, on a mark beside the setting's name; a number's bounds are the bar's
  own; what is wrong stays in view, under its setting or as a badge.
- **Dialogs and toasts**, ShadUI's: a new profile, leaving the editor with changes, deleting a profile and forgetting an
  account, changing the activity, and choosing resources several at once, searched and filtered by profession and level;
  toasts for what was saved, deleted, copied or restored, and for what a bot met, seen from any page, with a way to it.
- **One scale**, held on every page through the app's resources: spacings 4, 8, 12, 16, 24 and 32; fonts 12, 14, 16, 22
  and 28; controls 32 and 36 high; radii 6, 8, 12 and round; icons 14, 16 and 20; pages the window's width up to 1,200,
  32 from its edges.
- **Rejected.**
  - Tabs, a section at a time: shorter, but four sections of five hidden.
  - The editor unfolded in place, compacted: still some 1,300 pixels, its map small.

## D160: Resources by profession or by list, in 6e

*2026-10-29, step 6, session 6d, chosen by the owner among mockups, as the lead recommended it: the model now, made in 6e
with D152's settings. Amends D127's settings and D152. Made by D165.*

- **Two modes.** By profession: the professions chosen, all of them or none of a profession for Base's, each character's
  own level deciding; resources left out; and an order, the nearest first or the highest level first without leaving the
  others. A list: the resources chosen, in their order, as now.
- **Profiles kept**: "everything each character can harvest" reads as every profession but Base's, as the harvest does
  now; a list stays a list.
- **The dialog choosing resources** (D159) serves the list and those left out.
- **6d** draws the harvest's card with it and makes the dialog for the list; 6e makes the modes, the activity's choice of
  its targets, the preview and the stored profiles' reading.
- **Rejected.**
  - The modes in 6d: the harvest's settings, its activity and the stored profiles, D152's work, in a session that redraws
    nine pages.

## D161: Lucide's icons, as the app's geometries

*2026-10-29, step 6, session 6d, asked by the owner. Supersedes D94's icons of the app's own drawing, and keeps its
rejection of an icon font.*

- **What**: the 74 icons the app draws are Lucide's 1.64.0, read from its SVG files and written as path geometries in
  `Themes/Icons.axaml`, each element a path: a circle, a rectangle or a line becomes the arcs and lines that draw it. The
  `Icon` control draws them as Lucide does, lines of 2 on a grid of 24, round caps and joins, in the text's colour.
- **The license**: ISC, some icons Feather's under MIT, copied whole into `THIRD-PARTY-NOTICES.md`.
- **Checked**: the build does not read a geometry's text; a test reads every icon, each within its grid, since a path
  Avalonia cannot parse fails only once a view shows it, as one did while converting.
- **An icon answers the pointer** on its whole square, not on its lines alone, so that one carrying a tooltip, a
  setting's help, shows it.
- **Rejected.**
  - Lucide's font: a binary asset, and the icons drawn as text, D94's reason.
  - Lucide's Avalonia package: a dependency for 74 geometries.

## D162: What the redesign settled while making the pages

*2026-10-29, step 6, session 6d, the lead's choices, within D159's mockups the owner chose. Supersedes D94's language and
theme in the title bar, D101's unfolded editor, D111's and D120's chips choosing a bot, D121's layers as toggles beside
the map and its legend under it, and D151's language and theme kept in the title bar too.*

- **A page of the app** is an `AppPage`: its title, or a header of its own, its actions on the right, its content, with the
  same margins and the window's width up to 1,200 pixels. A page whose lists keep their own scroll, the map, the logs and
  the inspector, does not scroll as a whole. No page has a subtitle: what one said moved into a tooltip, a badge or the
  toolbar.
- **The sidebar** is ShadUI's, its pages named, folded to their icons on a click, which `app.json` remembers. The bots page
  says how many bots there are beside its name. The title bar says where the host stands, nothing else: the language and
  the theme are the settings page's alone.
- **One selector** chooses the bot of the map, the logs and the inspector, top right of each, still shared (`BotChoice`);
  the inspector's captures in a second beside it. The map's layers are a menu, each with the marks it draws, which is the
  legend.
- **Dialogs and toasts** are the view models' (`Dialogs`, `Toasts`), shown by the window through ShadUI's hosts, so that the
  view models are tested without a window. A dialog closes by its buttons and by Escape, never by a click beside it:
  ShadUI closes one so without telling its callbacks.
- **The profile editor**: the activity is chosen with the name, in the dialog making the profile, or copying one; the
  editor chooses it again only when the profile's is gone, the confirmation D159 foresaw needless. Closing it with changes
  asks to save, discard or stay; Ctrl+S saves, Escape closes. Deleting a profile, forgetting an account ask in a dialog.
- **Toasts**: a profile saved or deleted, a copy, the delays restored; and what a bot met under the app's eyes, its activity
  done, failed or still full, the player taking it over, a disconnection while it ran, a fight, with a way to its journal
  or its map. A bot met already paused or done says nothing.
- **Texts**: every page's and the harvest's rewritten short, in both languages; a setting's help, a delay's default and
  what a delay is for are tooltips.
- **Accessibility**: every button drawn as an icon has a name a screen reader says.
- **Drawn without a screen** in both languages and themes, every page and the editor's three dialogs, and looked at.
- **Rejected.**
  - Our own dialog overlay: ShadUI's host animates and dims, and its manager opens a view by its context's type.
  - No toast when the player takes a bot over: several bots run while one is played, the others' cards out of view.

## D163: The redesign made consistent, where the old and the new still mixed

*2026-10-29, step 6, session 6d; the owner found the app a mix of the old design and the new, the lead found why.
Amends D159's scale: one height of controls.*

- **Text**: a text with no class was Avalonia's 12, beside labels of 14; the window's text is 14, a caption 12 by its class.
- **One height**: every control of a toolbar or a row is 36, ShadUI's fields', segments included; the small buttons of 32
  went. Only the icon buttons inside a list's items are 32, in their row of 40.
- **One empty state** on every page (`EmptyState`): an icon, a few words, a line saying what to do, in the same place; a
  list's count hides while it is empty.
- **Rows**: the editor's settings are separated as the settings page's, a line between two, the card's title above.
- **Radii**: ShadUI's 10 and 14 follow the scale, 8 and 12, so that its controls and the app's have the same corners.
- **Bars**: a meter is the text's colour on the ghost's, a tour's the info tone, a life's the danger tone.
- **Rejected.**
  - Each page fixed alone: the causes were shared, the base size, the heights and the empty states.

## D164: 6d checked through the app

*2026-10-29, step 6, session 6d.*

- **Looked at by the owner** in the app run on the owner's data, the profile "Ortie" in Amakna and its three characters,
  after D163: found good. The first look found the old design and the new mixed, which D163 settled.
- **Looked at by the lead** in the app on the same data, captured page by page: the editor's zone of 85 maps, its figures
  and characters, the accounts, the statistics of 8 runs; and every page drawn without a screen in both languages and
  themes, the editor's dialogs included.
- **Found while checking**: choosing a page by a key or a screen reader checked it in the sidebar without showing it; the
  page of the item checked now shows (D162), tested.
- **Not measured on the client**: the toasts of what a bot meets, and the dialog choosing resources with the pack's
  resources of every profession; the tests cover them.

## D165: Resources by profession or by list, made

*2026-10-29, step 6, session 6e; chosen by the owner among dark mockups, as the lead recommended it. Makes D160 and
D152's highest level first and lists of resources. Builds D98, D127, D150, D159.*

- **The harvest's card** begins with `Mode`, two segments: by profession, the default of the presets (D168), or a list, a
  new profile's default, as before.
- **By profession**: the professions as chips on one row, each with its icon, "All" first, which ticks the five
  professions that gather or unticks them, and "Without a profession" last, `Base`'s, ticked apart. Each character's own
  levels decide what it harvests. The resources left out, chosen in 6d's dialog, which then says "Exclude resources" and
  warns of no level lacking. The order: the nearest first, or the highest level first, the map's other resources after,
  none left; on each map, as the priorities of D128 are.
- **A list** stays as it was: the resources in their order, the first first.
- **The stored profiles read without anything redone**: a list stays a list; "everything each character can harvest",
  which `HarvestSettings` no longer holds, reads as by profession with the five professions, `Base`'s not among them, as
  it harvested (D150). Checked on the owner's profile "Ortie", stored by 6d.
- **D98 gains**, for any activity: an activity reading what an earlier version stored, once the settings declared now are
  read (`ReadsEarlier`); a profession setting offering those that gather alone, `Base` among them, which the app shows as
  chips (`Gathering`, `GameReferences.GatheringJobs`); a list of resources to leave out (`Excluding`); a sub-area setting
  suggested by resources the activity reckons from its settings, here the professions' but those left out
  (`SuggestedBy` with a function); and a number saying what it counts (`Unit`).
- **Rejected.**
  - A switch "all the professions" and a list beside: two rows for one choice, the mockup 1B.
  - "All" holding `Base`'s: its skills gather events' items too (D150).
  - A key renamed by the settings framework: the harvest's earlier flag maps onto two settings, which only the module
    knows.

## D166: The place chosen by the activity, the sub-area richest in what the character can harvest

*2026-10-29, step 6, session 6e; chosen by the owner among mockups, as the lead recommended it. Makes D152's automatic
mode. Builds D127, D138, D144, D149.*

- **Measured** on the pack of 3.7.12.12: the sub-areas list a profession's resources widely, 28 the ash, 35 the elm, and
  the test sub-area "Test conventionné" lists every wood; the owner's 109 learned graphics place, for one, the ash on 454
  maps and the chestnut on 458. A ranking from them takes about 10 ms once the graphics' index is built (564 ms, D150).
- **The place**, a segment of the place's card: automatic, or chosen, the area and sub-areas as before, the default of a
  new profile and of the profiles stored. Automatic, for each character, at the start and at each new tour, so that a
  level gained moves it: the sub-area where the learned graphics place the most of what it can harvest at its
  professions' levels, each map counting a resource's worth, half as much every 20 levels below its profession's; while
  no graphic places anything it can harvest, the resources each sub-area lists. The first of the best ten it can travel to.
- **Not above its level**, on by default: a sub-area of a level above the character's is left out, its monsters
  attacking (D144), D3B not fighting before step 9.
- **What the place gives**: a lumberjack of 20 of a character of 60 goes to the Forêt d'Astrub, 69 maps; one of 80 of a
  character of 200 to the Bois de Litneg; an alchemist of 80 to the Dédale du Dark Vlad; a farmer of 40 to the Champs de
  Cania; a miner of 1 to the Souterrains.
- **Said**: the bot's card names the sub-area before its tour, "Forêt d'Astrub, tour 2, map 5 of 69"; the editor's preview
  tells each character playing the profile its sub-area by its levels, as its bot last saw them, and draws the first's,
  of the sub-areas reached from a zaap or a bank, its zaaps and criteria not known there.
- **Rejected.**
  - The sub-areas listing its highest resource, the nearest first: no learned graphics needed, but a listing says nothing
    of how many grow there, Village d'Amakna listing the elm.
  - Tables of zones by level, as the owner's profession scripts hold: data written by hand for each profession, which
    D3B's learned graphics give for every one.
  - Several sub-areas at once: a tour through the world for a zone the regrowth's wait already serves (D145).

## D167: A zaap only when it saves enough maps

*2026-10-29, step 6, session 6e; chosen by the owner among mockups, as the lead recommended it. Makes D152's "no zaap
when the destination is too near". Amends D83's "every map change costs the same, a teleport too".*

- **A setting of the profile**, in the place's card: "Zaap if it saves at least 5 maps", from 1 to 30. Every journey of the
  activity follows it, banking's included: the activity says it (`BotActivity.TeleportChanges`), the journey's rules carry
  it (`WorldPathRules.TeleportChanges`).
- **The path**: a teleport counts as that many map changes, so that a zaap is taken only when it saves as many, and paid
  only then. The costs are doubled and a teleport's is one more, odd, so that it never ties a walk: one that saves fewer
  changes than it counts as, or none, is walked. With 1, a zaap is taken as soon as it saves a change, as before, but no
  longer on a tie.
- **The stored profiles** take 5, the new behaviour the owner asked for.
- **Rejected.**
  - No zaap when the destination is at most so many maps away on foot, the mockup 2B: further, a zaap saving one map would
    still be taken and paid.
  - A setting of the machine, on the settings page: the owner asked for it in the profile, and a character short of kamas
    may want another.

## D168: Profiles given by default, stored once

*2026-10-29, step 6, session 6e; chosen by the owner among mockups, as the lead recommended it. Makes D152's profiles
given by default.*

- **An activity gives presets** (`ActivitySettingsBuilder.Preset`), checked whole when it registers, rules included. The
  harvest gives six, each by profession, the highest level first, the place automatic: "Automatic harvest", the five
  professions, and "Auto lumberjack", "Auto miner", "Auto alchemist", "Auto farmer" and "Auto fisherman".
- **Stored once** as ordinary profiles when the database opens, before the host starts, named in the system's language:
  edited, copied and deleted as any other; one deleted never comes back, the database keeping which were stored
  (`Presets`, a table of migration `Presets`). A name another profile has takes a number after it.
- **Not measured**: fishing, never harvested on the client.
- **Rejected.**
  - Models offered in the dialog making a profile, nothing stored: one step more to play one, the mockup 3B.
  - Read-only profiles of D3B, copied to edit: a second kind of profile, for little.

## D169: After 6e, 6f, then fights and their AI, then blueprints

*2026-10-29, step 6, session 6e, chosen by the owner, as the lead recommended it. Amends D117: blueprints after step 9.*

- **Asked**: fights and an AI before blueprints, an AI that is clever and complete, handles every case, thinks again as
  the fight changes; Bubble's emulator holds much of it.
- **The order**: 6f, which publishes the app with its data and turns the repository public (D158), then step 9, fights,
  in several sessions, then step 7's blueprints. A client update before still gets Q10's session first.
- **What step 9 will likely hold**, settled at its start with measures: the fight's protocol measured on the client,
  placement (D125), turns, actions, end, and the fight's state; the spells, monsters and effects in the pack, and an engine
  that reckons an action's outcome, Bubble's formulas measured one by one (Q5); an AI searching, each turn, the best
  sequences of moves and spells and thinking again after each answer of the server; the hard cases, summons, glyphs and
  traps, states, invisibility, pushes, line of sight, tackle; and the harvest fighting instead of pausing (D144). Every
  spell through its effects, not per class.
- **Said**: Bubble's emulator is Dofus 2's and read for ideas, never copied (D3); "every case" is reached through effects
  as data, measured case by case, not at once.
- **Rejected.**
  - Fights straight after 6e, 6f after: the owner chose 6f first.

## D170: 6e checked through the app

*2026-10-29, step 6, session 6e.*

- **Looked at by the owner** on a client of 3.7.12.12 through the app, run on the owner's data: found good. The database
  migrated, the six presets stored, named in French, beside "Ortie", which read as before, a list in Amakna.
- **Played**, one character, a connection of 2 min 42 s: "Auto lumberjack", its lumberjack at 79, harvested 15 walnut wood
  in 21 s, then stopped by the owner; "Auto alchemist", its alchemist at 104, harvested in 70 s edelweiss, the highest of
  its level, then Freyesque orchids, wild mint, five-leaf clovers and sage, 86 resources and 268 experience, until the
  owner closed the app. The host's log held no warning or error; the graphics learned went from 109 to 111.
- **Not measured on the client**: a level gained moving the place at the next tour, a zaap left for a walk, the dialog
  excluding resources with the pack's, and fishing; the tests cover the first three.

## D171: The app published by CI with Velopack, on a tag

*2026-10-29, step 6, session 6f, chosen by the owner, as the lead recommended it. Builds D15's "Distribution" row.*

- **How.** Pushing a tag `v<version>` runs `.github/workflows/release.yml` on Windows: the build and the tests, `dotnet
  publish` of `src/D3B.App`, self-contained for win-x64, the client build's files beside it (D172), then Velopack's `vpk
  pack` and `vpk upload github` into the release `v<version>`: `D3B.App-win-Setup.exe`, a portable zip, the full package,
  the delta from the release before, and `releases.win.json`. MinVer reads the version from the same tag. CI can do it,
  unlike the packs (D24): building the app needs no client. Only that job may write to the repository, and only a
  collaborator pushes a tag; its actions are pinned by their SHA, as in CI. A version with a suffix, such as `0.2.0-rc.1`,
  is published as a prerelease, which installed apps do not take.
- **Installed** per user, without elevation, into `%LOCALAPPDATA%\D3B.App`: Velopack's package id is `D3B.App`, never `D3B`,
  since uninstalling deletes the whole folder of the id, and `%LOCALAPPDATA%\D3B` holds the database, the app's settings,
  the packs extracted and the captures, which uninstalling keeps. Shortcuts on the desktop and in the Start menu, named D3B.
- **Self-contained**: a user installs neither .NET 10 nor ASP.NET Core, whose installers ask for elevation. No trimming, no
  ReadyToRun and no single file: EF Core, protobuf's reflection and MemoryPack suit them badly, as D15 said of AOT.
- **Unsigned**: SmartScreen warns of a downloaded Setup.exe, until signing is worth a certificate or Azure's signing service;
  the README says so.
- **vpk** is a local tool at 1.2.161, the library's version, so that CI and the maintainer run the same. `LICENSE` and
  `THIRD-PARTY-NOTICES.md` ship beside the app, the notices naming every component it redistributes and its license.
- **Measured** on this machine: 147 MB published in 398 files, the `.pdb` files left out as vpk does; the full package
  74.6 MB, Setup.exe 82 MB; the delta from 0.1.0 to 0.1.1, the code alone changed, 139 KB.
- **Risk, accepted.** Velopack 1.2.161 can leave an install without its `current` folder when a file of the update is
  locked, by an antivirus for instance (velopack#1086); the fix is in 1.3's prereleases. Setup.exe run again repairs it.
- **Rejected.**
  - The same package built and uploaded from the maintainer's machine: CI would have no write access, but each release
    would depend on that machine's state.
  - A zip of `dotnet publish` and an updater of our own: replacing a running executable, restarting and deltas are what
    Velopack does.
  - MSIX, winget or the Store: MSIX needs a certificate, and the stores' policies leave out a bot.
  - Framework-dependent: smaller, but two runtimes to install, with elevation.

## D172: The app ships its client build's files

*2026-10-29, step 6, session 6f, chosen by the owner; the lead recommended downloading them. Supersedes D32, and D24's,
D31's and D48's downloads.*

- **Each version of the app ships the pack, schema and mapping of one client build**, the `ClientVersion` property of
  `src/D3B.App`, which the app also reads from its metadata: in `data/<version>/` beside its executable. The release
  workflow takes them from the release `client-<version>` with `scripts/embed.cs`, checked against their SHA-256, then as
  the host checks them: the pack opens as the build's, and the mapping holds against the schema (D48).
- **The host looks on this machine first** (`Data:Directory`, where the tools write, D24), so that the maintainer's
  extraction or mapping of a build is used, **then among the app's own** (`DataSettings.Locate`), each file on its own.
  Nothing is downloaded any more: `ReleaseDownloader`, `ReleaseToken`, `Releases:Repository` and `Releases:Token` go, and
  with them the GitHub CLI the host started for its token (D32).
- **A client the app does not ship**, after an update of Ankama's: the host does not start, and says which file of which
  build it lacks (`BotHostStart.MissingBuild`). The installed app says that this version of D3B ships another build, and
  that the version for this one comes out after Ankama's update and installs itself (D173); run from its code, it shows
  the host's message, which says how to extract and map the build.
- **A client update** is then a client release, through Q10's session and `scripts/release.cs`, then an app release whose
  `ClientVersion` names the new build: the releases `client-<version>` stay, as the workflow's source.
- **Said before choosing**: an app tied to one build, a release of the app at every update of Ankama's, and the data in
  every installer; for it, an app working offline from its first start.
- **Rejected.**
  - Downloading the build's files at the first start, without a token, which the lead recommended: a client update would
    not stop the app until its own update, and the data would never be in the installer.
  - Both, shipped and downloaded: two paths to keep.

## D173: The app updates itself from the repository's releases, when its user chooses

*2026-10-29, step 6, session 6f, chosen by the owner, as the lead recommended it.*

- **Checked as the app starts**, beside its host getting ready, and downloaded at once (`AppUpdates`, in
  `D3B.App.Updates`, over Velopack's `UpdateManager`). Installed when the user restarts the app from the window's banner,
  asked first while bots play since a restart disconnects the clients (D91), or once the app closes, after its host has
  stopped and given the clients their configuration back. The app never restarts by itself.
- **When the client is a build the app does not ship** (D172), the window says so, with where the update stands and a
  button installing it at once: no bot runs.
- **The settings page** says the app's version, the client build it ships, where the update stands, and checks again on a
  click. A check that fails is said, never thrown: the app runs on.
- **Source**: the repository's stable releases through Velopack's GitHub source, which skips the releases without the
  app's packages, the clients' among them, and needs no token once the repository is public (D174). It reads the 10 newest
  releases only: since every client release is followed by the app's (D172), the app's newest is always among them.
  `Updates:Source`, on the command line or as `Updates__Source`, names a folder of releases instead, to try an update made
  on this machine.
- **Run from its code**, the app does not check: Velopack updates only an installed app.
- **Rejected.**
  - Blocking at start, checked, downloaded and installed before the host: always the latest, but a slower start, no
    choice of when, and a wait offline.
  - Manual only: a client update would leave the app stopped until its user thinks of it.

## D174: The repository public at 6f's end, each step on the owner's go

*2026-10-29, step 6, session 6f, chosen by the owner, as the lead recommended it. Settles D158's "not now".*

- **Checked before**: the history holds no token, no data extracted from the client, no capture and no binary; its only
  e-mail address is GitHub's noreply one; `vendor/` was never committed; `main` is the only branch on GitHub.
- **What it changes**: no token anywhere (D172); the releases, the clients' files and the app's installers that hold them,
  are downloadable by anyone, as D158 accepted; D24's risk stands, a DMCA notice reaching the whole repository, the scripts
  extracting locally whatever happens. CI keeps running pull requests from forks with a read-only token and no secret;
  only the release workflow writes, on a tag.
- **In order, each on the owner's go**: mapping format 8 uploaded to the release `client-3.7.12.12` (D156); this session's
  pull request merged; the repository made public; the tag `v0.1.0` pushed, which publishes the app (D171); an install
  from that release, checked on the owner's machine.
- **Rejected.**
  - The app published while the repository stays private: neither the installer nor the updates are readable without a
    token, which the app would have to carry.
  - Everything put off: D158 wants the app shipped with its data before the fights.

## D175: 6f checked on the owner's machine, before anything is published

*2026-10-29, step 6, session 6f.*

- **Installed** from a Setup.exe of 0.1.0 packed on the owner's machine as the workflow packs it, the data the tools wrote,
  the database and the app's settings set aside: installed into `%LOCALAPPDATA%\D3B.App`, its build's three files in
  `current\data\3.7.12.12`, a new database made; the host ran for 3.7.12.12 on the files the app ships, and a profile given
  played with the owner's client: found good. The check for an update failed, as it must while the repository is private.
- **Updated** to 0.1.1, packed beside it with its 139 KB delta, the app started with `Updates__Source` naming that folder:
  the banner said the version ready, the restart installed it, and the settings page then said 0.1.1, up to date. Velopack's
  updater could not wait for the app's process, access denied, and went on; it installed whole, the process having ended.
- **Uninstalled** from Windows' settings: `%LOCALAPPDATA%\D3B.App`, the shortcuts and the uninstall entry gone,
  `%LOCALAPPDATA%\D3B` kept. The owner's files were put back.
- **Not measured yet**: an install from the release on GitHub, which waits for the steps of D174, and a client the app does
  not ship, which the tests cover.

## D176: The release v0.1.0 checked, installed from GitHub

*2026-10-29, step 9, session 9a, the owner verifying on the owner's machine. Settles D175's install from the release.*

- **Published** on 2026-10-29 by the release workflow on the tag `v0.1.0` (D171): the run green, the files of 3.7.12.12
  embedded and audited (D172), Setup.exe 82 MB, the release marked latest.
- **Installed** from that release on GitHub on the owner's machine, the data the tools wrote, the database and the app's
  settings set aside: the host ran for 3.7.12.12 on the files the app ships.
- **Updates** (D173): the settings page said up to date through the GitHub source, which skips the release
  `client-3.7.12.12`, without the app's packages.
- **Played**: a profile, with the owner's client: found good.

## D177: Step 9 in seven sessions, from the protocol up

*2026-10-29, step 9, session 9a, chosen by the owner, as the lead recommended it. Builds D169. Superseded in part by
D230 (9f's mechanisms after the fights' profiles).*

- **Asked** by the owner: a fight AI complete and clever, handling every case and every criterion of a spell; Bubble's
  emulator and BillBot read closely; the client's data used, its monsters, spells and characteristics; the breeds' own
  mechanisms taken in, such as the Zobal's masks, the Steamer's turrets, the Rogue's bombs, the Xelor's telefrags, the
  Huppermage's runes, the Feca's glyphs and summons.
- **Read** in `vendor/` (D3), for ideas: Bubble's emulator holds a damage calculation ported from Dofus 2's client, with
  every check before a cast, target masks, zones, triggers, formulas, line of sight, tackle, portals, bombs, telefrags and
  carrying; BubbleBot reuses it under a thin fight layer, choosing one action at a time, its state never updated from the
  actions the server announces, its AI racing the network's thread; BillBot chooses greedily, finds life steal by a French
  word, and never sees invulnerability. None searches sequences of actions or thinks again after the server's answers.
- **Measured** in the client (client.md): 17,006 spells and 34,320 spell levels, their effects carrying masks, triggers and
  zones; 884 effects, 6,377 states, 5,129 monsters with their grades, 19 breeds, 123 characteristics, the Rogue's bombs and
  walls, spells' variants and pairs, and 38 formulas in Lua, among them the monsters' characteristics and experience.
- **The sessions**, each layer measured before the next:
  - **9a**, the fight's protocol measured on captures made with the owner: the start, placement and its cells (D125),
    fighters, turns, sequences and what the client sends by itself, moves, casts, the actions the server announces, the
    end and its gains; named and pinned, mapping format 9; the fight's state in the bot; the placement cells and the
    fighters on the app's map. The player still fights.
  - **9b**, the fight's data in the pack, format 5: spells and levels, their flags read from the client's code, effects,
    states, monsters and grades with their formulas, breeds, characteristics, bombs and walls, variants.
  - **9c**, the rules of a cast in `D3B.Engine`: line of sight, ranges, zones, target masks, every condition of a spell,
    moving in a fight with tackle and dodge; checked on captures, D3B accepting and refusing the casts the server did.
  - **9d**, the effects' engine: each effect as data, Bubble's formulas measured one by one (Q5), a bench comparing what
    D3B foresees with what the server answered, action by action.
  - **9e**, the AI, playable: placement, then each turn a search for the best sequences of moves and casts, thought again
    after each answer of the server; the bot fights alone, and harvesting fights instead of pausing (D144).
  - **9f**, the hard cases, one mechanism at a time, each measured: summons, the Steamer's turrets among them, the Rogue's
    bombs, the Feca's glyphs, the Huppermage's runes, the Xelor's telefrags, the Zobal's masks, traps and invisibility,
    portals, carrying, sacrifice, shared damage. Among them, as the owner said, which summon the bot plays and which plays
    alone: a spell and its variant decide it, such as "Maîtrise des invocations", which gives the summons' control, and
    "Liberté des invocations", which lets them play alone, and a Steamer's turret plays alone until its third evolution.
  - **9g**, strategies: the profile's settings, a role, prudence, spells preferred or left out, and the AI tried on many
    fights, per breed through the effects.
- **Every spell through its effects**, never per class (D169); a session may split in two as it grows, as 6c did.
- **Rejected.**
  - A vertical slice, a bot fighting from 9b with direct damage alone: seen sooner, but its AI built on a partial simulator
    and remade.
  - The hard cases before the AI: a complete simulator first, but no fight of the bot's before the step's end.

## D178: The fight's protocol, measured: mapping format 9

*2026-10-30, step 9, session 9a, on captures the owner made ([client.md](client.md#the-game-fought)).*

- **Measured** on the 40 game connections of 3.7.12.12 captured, 11 fights among them, 4 the owner's with a Steamer for this
  session and 7 earlier, a Xelor's among them: 9 the player's attacks and 2 resources' protectors', 8 won and 3 lost. By structure, timing, and values
  compared on the machine, with a measuring tool never committed; nothing written down but counts.
- **Named and pinned** for 3.7.12.12 (D54), in `actors.proto`, `fights.proto` and `fight_actions.proto`: the attack's request;
  the fight's start and join; the placement's cells, request, dispositions and readiness; the fight's start; its fighters,
  shown and synchronized, each a character with its breed and level or a monster with its id, grade and level, its team,
  life, AP and MP; the turns' order, rounds, each turn's start, the client's being let to play, the server asking whether
  the client is ready, the client's answer, the turn's end and the request ending it; the characteristics refreshed; the
  sequences and the client's acknowledgement; the cast's request; the action announced, as a cast, AP or MP spent, life lost
  or healed, a teleport, a swap, a summon or a death; and the end, with each fighter's outcome, experience and loot. The
  map's actors moved to `actors.proto`, which the fighters extend; a characteristic reads its usable and whole forms too.
- **Names**: the references' where measured (D56). BillBot's mapping names this build's obfuscated messages and fields, its
  very names: a hint, each pairing checked on the captures. Its nested fields often mislead here: the placement's lists
  are the other way round, a loss's life lies where it names a permanent loss, its heal is what it calls a detection, its
  teleport what it calls a dodge, and the experience's floors are shuffled. The names here are what the captures measured;
  what no capture set is neutral or left out.
- **A fight lost**, D144's measure: 3 of the player's attacks. The server says a text, 34, and names another map than the
  fight began on, the same twice, from which the harvest travelled back to its zone (D130). A protector's fight names the
  character its attacker, as an attack does: the kind stays told by its timing (D144).
- **What the client does by itself**, measured: it acknowledges the character's sequences that nest in none, 120 of 124,
  and says it is ready for each turn, 49 times, 3 ms to 3.5 s after the server asked; in a fight it never confirms a walk.
  D3B sends none of these: as for walks (D60), the client follows the server.
- **`scripts/capture.cs --messages`** lists the messages a game connection's frames carry by their type URLs, in their order
  and at their time, with `--mapping` the readable names, with `--frames --schema` their fields: the tool to measure fights
  again, never a value.
- **Mapping.** The readable protocol holds 192 messages, 405 fields, 25 enums and 183 values. `MappingFile.FormatVersion` is
  9, `mapping-9.binpb`, 12,783 bytes, the same bytes on two runs, audited without failure; the 40 game connections, 29,428
  frames, decode through it, 8,134 understood whole and 5,219 more read, none misread. The release of 3.7.12.12 needs it,
  which the release tool publishes once the owner says so (D50); then a release of the app, whose files come from it (D172).
- **Rejected.**
  - BillBot's mapping taken as it stands: wrong on the fields measured.
  - Every action of the fight's event named now: the captures set 12 of its 36; the others wait for the sessions measuring
    them, 9d and 9f.

## D179: The fight's state, in the character's

*2026-10-30, step 9, session 9a. Builds D88.*

- **`FightState`**, in `D3B.Bot.Fights`, is part of the character's snapshot (`CharacterState.Fight`), from the fight's
  context to the roleplay's: the phase, placement, fighting or ended; the character's team and both teams' placement cells;
  whether the character said it is ready; the fighters, each with its team, cell, life and most life, AP and MP, level,
  breed or monster and grade, and summoner; the turns' order, the round, whose turn it is and whether the character may act;
  the end, its outcome, experience, kamas and objects. `IsFighting` is now whether there is one.
- **`FightEvents`**, pure functions as the character's (D9), take the fight's messages; `CharacterEvents` hands them each
  fight message first, so that the character's walk in a fight moves its fighter and leaves no path to confirm.
- **AP and MP** come from the characteristics alone, which the server gives after each cast and step and as each turn
  begins: the spending it also announces comes before or after them, and would be counted twice. **Life** comes from the
  losses and heals announced, a permanent loss lowering the most; the characteristics carry it only as a fighter is shown.
- **The journal** writes the fight's end once, its outcome and gains (`FightEndEntry`), which the logs page says.
- **Not taken yet**: the actions no capture set (pushes, marks, states, invisibility, carrying), the spells' cooldowns, and
  the summons the player controls, each for the session measuring it; the bot sends nothing in a fight until 9e. Measured
  once, for 9f: as each fight starts the server gives the client the character's spells as the context it plays in; it says
  a turret just summoned is not controlled, and once the turret evolved, it gave the client the turret's context before its
  turn.
- **Rejected.**
  - The fight kept apart from the character's state, in the actor: two snapshots to keep in step, where one is replaced
    whole.

## D180: The placement cells and the fighters on the map

*2026-10-30, step 9, session 9a, as D125 planned it. Settles D125.*

- **The cells are the server's**: of the 11 fights' maps, all in the pack, none holds a cell flagged red or blue, and the 8
  or 12 cells each team got were all walkable. The pack's flags stay Dofus 2's.
- **The map page** draws a layer of its own, "Combat": during the placement the challengers' cells in red and the
  defenders' in blue, as the client shows them, over the ground; then the character on its fighter's cell, its allies and
  enemies as squares of their colours, the dead left out. A cell under the pointer names the fighter on it, its side, what
  it is, its life, AP and MP, and whose placement cell it is. Monsters are named by their ids until 9b puts them in the pack.
- **Rejected.**
  - The placement cells kept after the placement: the fight's start makes them meaningless.

## D181: What the references give step 9, read again for 9b

*2026-10-30, step 9, session 9b, as the owner asked. Builds D3, D13, D177.*

Read for ideas, never copied (D3): every formula and id of Dofus 2 is measured on Dofus 3 before use (Q5).

- **Bubble's emulator** (`bubble-emu-v2-develop`) runs its fights on `Bubble.DamageCalculation`, a port of the Dofus 2
  client's damage preview, fed by an adapter from its tables. What it gives:
  - *9c*: the order of a cast's checks (turn, cell, AP, free, taken or seen cell, trap, portal, states and their criterion,
    summons left, cooldowns, casts per turn and target, range and line of sight); ranges as a diamond, a cross or
    diagonals; line of sight traced cell to cell and cached; target masks as an inclusion pass, uppercase enemies and
    lowercase allies, then conditions that must all hold, those on the caster marked `*`, breeds, monsters and sidekicks
    OR-ed by kind; tackle as a product over the tacklers of `(evade + 2) / (2 (block + 2))`, AP and MP lost at each cell
    left.
  - *9d*: effects dispatched on their id; a cast's non-random effects, then one random group drawn by weight; damage as
    the roll times `(100 + power + characteristic) / 100` plus fixed bonuses, then the zone's decrease, the target's fixed
    then percent resistances, reflects, shared damage, the dealt and received multipliers, shields, steal and sacrifice;
    heals and pushes' damage likewise; triggers by codes (`TB`, `TE`, `D`, `DA`, `EON`) making an effect a buff; states
    read by the numbers of their effects (invulnerable, incurable, pacifist, no push).
  - *9e*: its monsters' AI, which tries every reachable cell and target cell, simulates each cast and scores it; greedy,
    one action at a time.
  - *9f*: summons from an effect's dice, a monster and its grade, at the summoner's level with its bonus; bombs chaining,
    and walls between bombs of a kind; portals projecting a cast; telefrags as a swap; glyphs, traps and runes as marks of
    a zone and a spell; carrying as two states.
  - *Its faults*, to avoid: chance drawn inside the engine, contexts copied shallowly and buffs mutated, constants of
    Dofus 2 (bombs, turrets, walls, portals, resistance caps, erosion), and bugs: the AP and MP dodge reading the
    attacker's, the received distance multiplier reading a dealt one, a spell's damage bonus counted twice.
- **BubbleBot** (`Bubble.D3.Bot-main`) reads the datacenter by hand, its flags those of an older client, which the
  client's code confirms (D183); its AI casts in a fixed order through the same port, never updates its fighters from the
  server's actions, refuses every spell with a criterion on states, and leaves bombs, walls and summons out.
- **BillBot** found its spell levels' flags by statistics against Dofus 2's tables, chooses greedily by damage over AP,
  and finds life steal and invulnerability by French texts and fixed state ids.
- **What 9b took**: the tables to keep, the fields that matter, and the questions to measure: which level a cast names,
  how a monster's spells are graded, what a grade's values are, what variants and pairs are. The answers are D182 to D184.
- **Rejected.**
  - Porting the damage preview: GPL, Dofus 2's, and built for one preview, not for a search over many states (D13).

## D182: The fight's data in the pack, format 5

*2026-10-30, step 9, session 9b, as D177 planned it. Builds D25, D29, D139.*

`GamePack.FormatVersion` is 5. Six entries, each a catalog read once by `GameTables`, measured in
[client.md](client.md#fights-tables):

- `spells/catalog` (`SpellCatalog`): the spells and their levels, every field of a level's, its flags with the client's
  bits (D183); each level's effects then its critical effects, each with its effect, order, target mask, duration, random
  weight and group, dispel, delay, triggers, element, dice and value, flags and zone; the zones' shape, sizes, decrease,
  stop, direction, carried and line of sight, and the cells some list; the levels' preview zones; spells' variants; the
  Rogue's bombs and walls. Masks, triggers and criteria are text as the client writes them, held once each: 9c and 9d
  parse them.
- `spells/states` (`SpellStateCatalog`): the states, their booleans as one set of flags, their effects' numbers.
- `effects/catalog` (`EffectCatalog`): the effects, by the id spells name and the server announces, with their
  characteristic and operator, category, element and flags.
- `monsters/catalog` (`MonsterCatalog`): the monsters, their flags with the client's bits, grades with every
  characteristic and the bonus a summon takes, spells with their grade at each of the monster's grades, the
  characteristic ratios, races and super-races.
- `breeds/catalog` (`BreedCatalog`): the breeds, their spells, roles and characteristics' costs.
- `characteristics/catalog` (`CharacteristicCatalog`): the characteristics with their keywords, and the four Lua formulas
  that scale a monster's to a level, as the client writes them.
- **Texts**: the names of spells, states, monsters, races, breeds, roles and characteristics, and the effects'
  descriptions, in French and English: 56,227 texts per language instead of 26,411.
- **The extraction fails** when what a fight relies on breaks: a spell's levels not its own by grade from 1, an effect
  the client lacks, a monster's grades not its own in order, a spell without a grade for each, a variant not of two
  spells, a breed's spell in no variant of its own, a flag's bit or a zone's shape the client's code does not name, a field
  left out for being constant that is set. What a fight does not rely on yet is counted: the 69 levels of no spell, left
  out; spells, starting spells, states and monsters named and missing; bombs naming no monster.
- **Left out**: spells' descriptions, animation scripts and types; monsters' drops, looks, animations and aggression
  criteria; effects' icons and tooltips; the client's `SpellPairsDataRoot`, whose 644 pairs belong to the custom mode,
  named by its breeds' spells and by no spell. The spells the owner calls a pair, such as *Maîtrise des Invocations* and
  *Liberté des Invocations*, are a variant, 481, of the spells every character has, breed 19, which the breeds lack.
- **The formulas are kept, not run.** The server gave each of the 54 monster fighters captured its grade's level, life,
  AP and MP, so a fight needs no formula; at each grade's level they give the grade's life on 78% of grades and its other
  characteristics on 22% at most: the grades are values of their own, and the formulas scale a monster to a level its
  grades lack. Running them waits for the session that needs it (Q15).
- **Measured** on 3.7.12.12: 7,432,757 bytes against 5,382,920, 17,737 entries, extracted in 22 s, two extractions giving
  the same SHA-256; the spells 1.29 MB compressed, 10.24 MB read in 30 ms; the monsters 0.33 MB, read in 12 ms; the texts
  0.4 MB more. `PackFormatTests` records format 5's layout.
- **Release**: `data-5.pack` goes to the release `client-3.7.12.12` beside the others, extracted again from this
  session's merged commit, through `scripts/release.cs --publish`, then a release of the app embedding it (D172), each on
  the owner's go.
- **Rejected.**
  - Each effect as a class holding its texts: 126,306 objects to build, where packed structs are copied, and masks
    repeated thousands of times.
  - The formulas written in C# now: the client's, rewritten, would drift from the client's at an update, unseen.
  - Every table in one entry, or one entry per spell: a catalog per domain, read whole in milliseconds (D29).

## D183: The flags and names read from the client's code

*2026-10-30, step 9, session 9b, as the owner asked, with the reverse-engineering skill.*

- **How**: the client's IL2CPP code, opened with Cpp2IL as `scripts/schema.cs` does (D40), in a measuring tool never
  committed. Each record's `m_flags` is typed by an enum of the client's, `SpellFlags`, `SpellLevelFlags`,
  `EffectInstanceFlags` and `MonsterFlags`, whose values name its bits; the getters of the records' booleans read the same
  bits, checked on the spell levels' 14, from `castInLine`, bit 0, to `proportionalDiagonal`, bit 13. The references named
  the same bits but the newest: `proportionalDiagonal`, `canMove`, `canBeControlledByFightOwner` and the ground marks'.
- **What D3B keeps**: its own enums of the bits, named as the client names them, as the readable protocol is D3B's own
  (D42): `SpellProperties`, `SpellLevelProperties`, `SpellEffectProperties`, `MonsterProperties`, and
  `SpellStateProperties`, `EffectProperties` for booleans the client stores apart. The extraction fails on a bit they
  lack, so that a client adding one is read again in its code.
- **Also named there**, measured and recorded in client.md: the zones' shapes, each a character, and what the zones'
  parameters are; the 1,029 actions an effect's id names, 408 of them a spell's effect; the items' and the maps' flags.
- **Rejected.**
  - The bits found by statistics, as BillBot did: a bit set on few levels, such as `hidden`, once, cannot be told apart.
  - The client's enum of actions copied into D3B: what 9d handles, it names in its own words, measured.

## D184: The cast's level and the fighters' and loot's monsters, checked; monsters named on the map

*2026-10-30, step 9, session 9b. Settles D178's level of a cast and D180's monsters named by their ids.*

- **Checked** on the 40 game connections captured, decoded through mapping 9 and read against the pack of format 5, with
  a measuring tool never committed: the 344 casts name a level of their spell, which the spell's grade finds again, 52
  spells at grades 1 to 6; the 54 monster fighters, of 17 monsters, name a grade of their monster at its level, with its
  life, AP and MP, and a name in both languages; the 6 summons a grade of theirs; of the 16 groups of loot, 14 name a
  monster of the pack, and 2 monster 0, one object each that no monster dropped.
- `fight_actions.proto` says the cast's `level_id` is its level, no longer likely.
- **The map page** names a monster fighter by its monster's name in the app's language, its id when the pack lacks it.

## D185: 9c's rules in `D3B.Engine`, pure, measured on every cast and walk captured

*2026-10-30, step 9, session 9c, as D177 planned it. Builds D13, D68, D181.*

- **Asked** by the owner: the rules of a cast in `D3B.Engine`, from the pack of format 5, each measured on Dofus 3 (Q5),
  what cannot be measured said; then D3B accepting the 344 casts the server made. The release of `data-5.pack` (D182)
  was put to the owner first, who said not yet.
- **Pure.** The rules read a fight as `Combatant`s, the engine's view of a fighter (its team, cell, AP, MP, range, lock,
  dodge, life, summons, breed or monster and its properties, summoner, states), a `FightMap` built once per map, and a
  fighter's `CastHistory`; they change nothing, keep nothing, and are built once per pack (`CastRules`, `FightMoves`).
  The bot's fight state (D179) does not feed them yet: 9e's AI does, once 9d gives it the states and cooldowns.
- **Measured** on the 11 fights of the 40 game connections, replayed through the bot's `FightEvents` and the pack, with a
  measuring tool never committed, printing counts only ([client.md](client.md#the-game-cast)). The 344 casts are 79 the
  fighters chose, each the first cast of a sequence of a spell of their own, and 265 that effects cast, at a fight's or
  a turn's start or within another cast, which no rule of a cast checks; one weapon's blow is not a spell's. The 79 pass
  every rule; so do the 32 walks.
- **States and cooldowns** come from two events the readable protocol lacks, `FightEffectsEvent` and
  `RefreshCooldownEvent` as BillBot's mapping names them: the first gives a fighter a state as action 950, which a
  turret's criterion `HS=135` needed. The measuring tool read them raw; 9d names them, with the buffs they carry, mapping
  format 10, and the fight's state keeps them.
- **Rejected.**
  - Reading the client's code for the rules: it is obfuscated, its zones' classes named but their methods not, and no
    decompiler of native code is installed on this machine; the 11 fights decide what they cast, and the rest stays the
    references' idea, said so.
  - Naming the states' and cooldowns' messages in this session: their buffs are 9d's, and the raw read sufficed to measure.
  - The rules on the bot's `Fighter`: the engine stays below the bot (D13), and an AI's search builds fighters it imagines.

## D186: Range and line of sight

*2026-10-30, step 9, session 9c.*

- **Range** (`SpellRange`): on the map's coordinates, between the level's least and most, the most boosted by the caster's
  range, characteristic 19, when the level says so, never below the least nor 0; a diamond, or the caster's row and
  column in line, or its diagonals in diagonal steps, counted as the distance when proportional. Measured: the 79 casts lie
  within, one only once boosted, the 13 cast in line in line. No capture casts in diagonal.
- **Line of sight** (`LineOfSight`): the cells the line between the two cells' centres enters, on the coordinates, counted
  in integers so that a line and its reverse agree; through a corner it enters the cell across, and only touches the two
  beside, which do not block. A cell between blocks when its data lets no sight through or a fighter stands on it; the
  ends never do, nor a point beyond the map. Measured: 53 casts test it, 40 with cells between, none blocked; 2 pass a
  corner beside a cell that blocks, which the server let through.
- **The cell cast on** must be walkable in a fight, `mov` and not `nonWalkableDuringFight` (`FightMap`): every cell cast
  on and walked through in the captures is.
- **Benchmark**: on a captured fight's map, every cell a level may be cast on in 6.3 µs, a line of sight across the map
  in 30 ns.
- **Rejected.**
  - The Dofus 2 client's sweep, which keeps a cell's answer from a farther line and so depends on the order the cells are
    tried: one cell, one line, the same both ways. Whether the server agrees where they differ is Q16's.

## D187: Zones and target masks

*2026-10-30, step 9, session 9c.*

- **Zones** (`SpellZones`): the 26 shapes the effects use, the pack's sizes as the client's getters read them, centred on
  the cell cast on, directed from the caster: along the axis it lies farthest on, or a diagonal when exactly on one,
  unless the zone forces a direction of the map. `LineFromCaster` starts at the caster, from `param1` on for `param2`
  cells, its two numbers read the other way, as its 1 and 63 say; it stops at the first fighter when the zone stops at
  its target. A zone keeping to the line of sight keeps what its centre sees; a custom zone lists cells of the map.
  Measured on the six shapes cast, point, circle, line, cross, square and whole map: the 63 fighters damaged lie in the
  zone of an effect whose mask selects them, and the 62 an effect damaging at once foresaw were all damaged. The others
  are the references' idea and the client's names; boomerang, checkerboard, the complex circle, the diagonal lines and
  the rectangle least certain.
- **Masks** (`TargetMask`): parsed once, answered on three values as criteria are (D71): kinds include, uppercase enemies
  and lowercase allies, the caster by `a`, `c` or `C`; conditions must all hold, a monster, a breed or the caster's kinds
  among several being alternatives. What the fight's state does not tell, a fighter carried, a portal, a teleport, the
  triggering target, and the tokens Dofus 3 added (`Atq`, `Def`, `Sce`, `PB`, `PR`, `AP`, `MP`, `u`, `x`), is unknown.
- **The criterion on states**, `HS=` and `HS!` joined by `&`, `|` and parentheses, the only code of the levels' 2,409
  criteria, is answered by `Criterion` (D71) on a fighter's states, `CriterionFacts.States`.
- **Heals and teleports** a cast caused are not checked against its zone: a steal heals its caster, and 24576 teleports
  its; what each effect does is 9d's.

## D188: A cast's conditions, costs and cooldowns

*2026-10-30, step 9, session 9c.*

- **The checks**, in order (`CastRules.Check`, `CastRefusal`): the caster lives on a cell; no state of its prevents
  casting; it has the AP; the level's states criterion holds; its first cast waits `InitialCooldown` rounds;
  `MinCastInterval` rounds pass between two casts; no more than `MaxCastPerTurn` a turn; a level summoning, by effect 181
  or 1011, leaves a summon's slot, characteristic 26, unless its spell bypasses the limit; the cell is walkable, free or
  taken as asked, its fighter seen when asked, cast on no more than `MaxCastPerTarget` this turn; then the range and the
  line of sight. `CastRules.Targets` gives the cells allowed, `CastRules.Reached` the fighters each effect reaches.
- **History** (`CastHistory`): each spell's last round, and this turn's casts and casts on each target, a new round
  starting the turn's counts again; only the casts a fighter chose count.
- **Measured**: of the 79 casts, 65 are limited a turn, 39 a target, 12 have an interval, one cast again after it, one an
  initial cooldown, 9 need a free cell, 5 a taken one, 5 a states criterion, one met by the state the server gave.
- **Not checked, as not measured**: the levels' global cooldowns and casts shared by a team; traps and portals on the
  cell, which the fight's state does not hold yet; the character's least level, which the server's own list of its
  spells answers (D179).

## D189: Walking in a fight, and tackle

*2026-10-30, step 9, session 9c.*

- **Measured** on the 32 walks: each step leads to one of the four cells at a distance of 1, walkable in a fight, no
  fighter on it, one MP a step, the walk no longer than the MP the walker had (`FightMoves.Steps`, `Reachable`).
- **Tackle** (`FightMoves.KeptLeaving`, `Lost`): leaving a cell beside enemies that tackle, a fighter keeps the product
  over them of `(dodge + 2) / (2 (lock + 2))`, each at most 1, of its MP and AP, and loses the rest, rounded to the
  nearest, half away from zero; an enemy tackles when it lives, is seen, has no state preventing it and, a monster, its
  monster tackles. Dodge is characteristic 78 and lock 79. The one walk captured that left an enemy kept everything,
  dodge 40 against lock 0, as the formula says; a loss was never captured.
- `Reachable` keeps, for each cell, the path leaving the most MP, then AP, a tackle never taking more from more points.
- **Benchmark**: the cells a fighter with 6 MP walks to among four enemies in 1.2 µs.

## D190: The casts the server refuses, captures to make

*2026-10-30, step 9, session 9c. Opens Q16.*

The captures hold no refusal: the client never sends a cast its own rules refuse, nor a walk. What D3B refuses, the
server may accept: D3B's rules are as strict as measured, not proven exact. To measure the refusals, D3B would send what
the client does not, as it sends its requests between the client's (D61), in a fight the owner plays against a weak
group, each request answered by the server's refusal or its cast:

- a cast just out of range, then just in range with the range bonus, then in diagonal for a level cast in diagonal;
- a cast through a fighter and through an obstacle, and past corners beside each;
- a cast on a free cell for a level needing a taken one, and the reverse;
- a cast beyond the casts a turn and a target, and before its interval;
- a walk away from a monster that tackles, its MP and AP lost noted, and a walk through a fighter.

It needs a measuring mode that sends a request the owner chose, which 9e's sending of casts makes, or a small tool before
it; the owner decides when (Q16).

## D191: 9c measured again on 38 fights the owner played for it

*2026-10-30, step 9, session 9c, on 12 game connections the owner captured with D3B after D190, 11 breeds among them.
Supersedes in part D186 (the diagonal), D188 (the states preventing casts, the modifiers), D189 (the tackle measured) and
D190 (what remains).*

- **The fights**: 49 in all with the 11 of D185, 396 casts the fighters chose, 581 their effects cast, 2 weapon's blows,
  298 walks; Steamer, Xélor, Iop, Eliotrope, Zobal, Roublard, Pandawa, Crâ, Osamodas, Forgelance and Sadida, at low
  levels, and their monsters and summons. The measuring tool, never committed, follows what the readable protocol does
  not name yet, read raw: slides (action 5), a carry and a throw (50, 51), a tackle and its losses (104, 101, 127), the
  removal of a spell's buffs (406) and of all (132), and the buffs of `FightEffectsEvent` with the spell that gave each.
- **Casts**: 392 of the 396 pass the rules; the 4 others an Eliotrope cast beyond their range through its portals, which
  9f measures (D177). 286 test the line of sight, 199 with cells between, none blocked, 44 past a corner beside a cell
  that blocks.
- **Diagonals**: 4 casts at a range of 1 reached the diagonal neighbour, at a distance of 2: a diagonal counts its steps,
  as D186 took from the references. No capture tells a proportional diagonal from the steps.
- **A state preventing casts lets through the levels asking for it**: a Pandawa carrying is in state 3, *Porteur*, which
  the pack says prevents casting, and threw what it carried 4 times, by levels whose criterion asks `HS=3`. `CastRules`
  so refuses a caster in such a state only for a level whose criterion does not ask for it, as Dofus 2's client did. The
  throw ends the carry without the server removing its states: the carried and carrier states go with it.
- **Spell modifiers** (`SpellModifier`, `SpellModifierKind`, `Combatant.Modifiers`, `CastRules.Modified`): buffs of the
  pack's effects 280 to 299, each naming a spell and a value, change its least and most range, whether it may be boosted,
  its AP cost, its interval or set it, its line, its line of sight, its casts a turn and a target, and the free or taken
  cell it needs. The rules read the level as modified. Measured: a Pandawa's buffs give its carrying spell 3 more range,
  no taken cell and a free one; 3 casts carried a modifier of their spell, none needing it. Those on damage, heals and
  critical hits are 9d's.
- **Zones**: of 388 losses of life, 380 lie in the zone of a cast of their sequence, the one chosen or one its effects
  triggered, whose mask selects the fighter; 4 others a mask of portals leaves unknown, 1 a fighter carried, 3 come with
  an Osamodas's summons or a spell triggered whose caster the tool did not place. 15 shapes explain losses alone: point
  161, cross 74, circle 41, perpendicular line 16, diagonal cross 13, line 11, square 10, cross without centre 7, fork 4,
  star 3, diagonal perpendicular line 3, line from the caster 2, half circle 2, cone 1, with the whole map. Of 343 fighters
  an effect damaging at once foresaw, 342 lost life. Not measured: ring, outside circles, checkerboard, square without
  diagonals, rectangle, boomerang, diagonal line, diagonal cross without centre, custom zones.
- **Tackle**, measured on the 3 walks tackled: the server announces the tackle, then each characteristic lowered, then
  each loss. 3 AP and 2 MP lost of 6 and 3 at a share kept of a half, 2 and 1 of 7 and 4 at two thirds, 1 and 1 of 2 and
  4 at three quarters: the formula of D189 and its rounding to the nearest, a half up. 6 walks leaving enemies lost
  nothing: monsters whose monster does not tackle, a state, a dodge outweighing the lock.
- **Walks**: the 298 step to cells at a distance of 1, walkable in a fight and free, one MP a step.
- **A caster on no cell**, one carried whose effects cast, reaches from the cell cast on (`CastRules.Reached`): the tool
  found it throwing.
- **What 9d takes from this**: the bot's fight state needs the slides, carries and throws, the tackles and the losses, the
  buffs, states and modifiers, and their removal, named in the readable protocol, so that the rules read what the server
  said; mapping format 10.

## D192: The fight's AI and the fighting activity, as the owner asks them

*2026-10-30, step 9, session 9c, the owner's words, for 9e and 9g. Builds D13, D144, D177.*

- **An AI autonomous and clever**, reacting to each situation, through the spells' effects and never per class unless a
  class needs it: it heals itself and its allies when it should, shields, moves cleverly, uses the right spells, prefers
  those of the elements its characteristics favour, and casts a strong zone spell where many monsters stand in it. Every
  case is to be taken into account (D177).
- **The fighting activity**, once the AI fights, made as the harvest is (D127, D160, D166): which monster to fight, the
  zone and sub-area, the least and most monsters of a group, a drop sought on a monster, and every other case a profile
  should say.

## D193: What the server announces of a fight, named and kept: mapping format 10

*2026-10-30, step 9, session 9d, as D191 asked. Builds D178, D179, D185. Superseded in part by D225 (a buff's end
announced).*

- **Measured** on the 52 game connections captured, 49 fights and 50,755 frames, by structure, by the client's names of the
  actions' ids (D183) and by values set against the fight's state, with a measuring tool never committed and
  `scripts/fights.cs` (D196): a slide (5: the fighter, the cell it leaves, the cell it reaches; 36, a push of several cells
  one slide); a carry (50: the carried and its cell) and a throw (51: the thrown and the free cell it lands on); a tackle
  (104: the enemies beside the walker); AP and MP dodged (308, 309); the buffs a spell gave removed (406: 36, 35 holding
  some), all of a fighter's (132) and one by its uid (514: 39, each a buff held, a shield broken or a bonus spent before its
  end). A loss's field 2 is what a shield took: with a shield up, the life lost was 0 and the field held the blow.
- **Buffs**: `FightEffectsEvent`, 843 in the captures, each a buff with its action and source: its uid on its fighter, its
  target, the spell's effect it comes from by its uid, every one in the pack, the spell and its grade, its kind (a boost, a
  state, a spell modifier, a triggered effect), a value (a boost's change, the spell a modifier changes or a triggered buff
  casts), a parameter (a state's id, a modifier's value, the effect's value on 357 states and 32 modifiers), its triggers,
  and when it ends: a round and a turn, the place in the turns' order of the fighter playing. Of the 125 buffs for rounds
  a chosen cast's own effects gave, 113 end at the cast's round plus the effect's duration, at the caster's turn. The
  server announces no buff's end of time: buffs held as fights ended had ended rounds before.
- **Cooldowns**: `RefreshCooldownEvent`, the character's and its summons' spells, each with the rounds it waits: of 213
  counts of spells cast, 197 are the level's interval less the caster's turns ended since the cast, 16 one fewer, sent as
  the turn ended.
- **Mapping format 10**, `mapping-10.binpb`, 13,670 bytes, the same bytes on two runs: 205 messages, 450 fields, 27 enums
  and 195 values audited, no failure; `FightEffectsEvent` and `RefreshCooldownEvent` pinned, the rest paired by fields; of
  the 50,755 frames, 21,082 understood whole, 8,876 more read, none misread. The release of 3.7.12.12 needs it, then a
  release of the app (D172), each on the owner's go.
- **The fight's state** keeps it (`Fighter`, `FightBuff`, `FightSequence`): each fighter's characteristics, whole, as the
  server last gave them; its buffs, from which come its states and its spells' modifiers; its cooldowns; the casts it
  chose (`CastHistory`), the first of each of its sequences of a spell; the sequences open; who carries a fighter carried,
  which stands on no cell. A slide and a throw move their fighter, a throw ending the carry's buffs on both, named by the
  states the carrier gave the carried (D191). A buff goes as the turn it ends at begins, as the references count a buff
  down at the start of the turn of the fighter playing as it was given; with its spell's removal, all, its uid, or its
  fighter's death. Measured: the 36 slides and the 298 walks start on the cell the state holds; of the 398 casts the state
  counts as chosen, 392 pass 9c's rules with the states and modifiers it holds, 4 an Eliotrope's through its portals and 2
  a Rogue's bomb's, 9f's.
- `FightCombatants` gives `D3B.Engine` the fight's fighters from the state, characteristics, states, modifiers and damage
  shares included. Two spell modifiers setting a range, 2905 and 2906, measured twice each, join `SpellModifierKind`.
- **Not kept yet**, as not measured: marks, portals, invisibility, what the triggered buffs do as they trigger, and the
  buffs a dead fighter gave others: 9f's.
- **Rejected.**
  - Counting each buff's duration down in the state: the server says when each ends.

## D194: A critical hit is the cast's field 8

*2026-10-30, step 9, session 9d. Corrects what the session first named.*

- **Measured**: field 8 of a cast, an enum, is set on 33 of the 398 casts the fighters chose; their 50 losses of life are
  the level's critical effects', 47 within the range they foresee. Field 1, set on 15 casts, is no critical hit: what it
  says is not measured, and the readable protocol leaves it out. Monsters' critical hits are marked as characters' are.
- `FightCast.critical` reads it (`FightCastCritical`).
- The chance of a critical hit is drawn by the server, never told: the engine foresees a cast either way (D195).

## D195: The effects' engine, Bubble's formulas measured on Dofus 3

*2026-10-30, step 9, session 9d, as D177 planned it. Settles Q5 in part. Builds D13, D181, D187. Superseded in part by
D226 (a mask naming the caster) and D227 (the spells effects cast).*

- **Every effect as data** (`D3B.Engine.Effects`): `EffectActions`, built once per pack, says what each effect id does
  (`EffectKind`): damage, a steal, damage or a heal no characteristic raises, a heal, a shield, a push or a pull, a
  teleport, a swap, a summon, a state set, removed or disabled, a boost or a deboost of a characteristic, a spell modifier,
  a spell cast, buffs removed, a kill. The pack gives the elements, the characteristic and its operator, and the modifiers'
  category; D3B's table, named as the client's actions are, gives the rest, and the best and worst elements. An id it does
  not name is `Other`, left out.
- **Foreseen** (`SpellEffects.Foresee`): each effect of the level, its critical effects on a critical hit, on each fighter
  `CastRules.Reached` finds, with its value through the formulas (`EffectFormulas`), a range from the least roll to the
  most; whether its mask surely selects the fighter, whether it is drawn at random, whether it applies at once. A mask
  naming the caster (`c`, `C`) reaches it outside the zone: 16 buffs went to a caster so. Nothing is drawn.
- **Damage**, the references' order, each step rounded down: the roll between the dice; the spell's base damage modifiers;
  times 100 plus the element's characteristic (strength for neutral and earth, intelligence, chance, agility) plus power,
  in percent; plus the damage bonus, the element's and on a critical hit the critical bonus; plus the spell's damage
  modifiers; less 10% a step away from the zone's centre, at most 4 steps, the steps counted as the zone counts its size,
  past its least, a square's diagonal one step and a circle's two; less the target's fixed resistance, then times its
  resistance in percent; times the multipliers of damage dealt and received, by spells, in melee or at a distance, 100 when
  the server gives none; times the shares of the target's buffs 1163, 85% in the captures; never below 0. Measured with
  `scripts/fights.cs` (D196), the losses of each target by element against what the cast foresaw: of the casts alone, 334
  of 335 within, 99 of them the target's whole life; the other one more than foreseen, under two buffs of final damage, a
  rounding not settled. With the spells the effects cast, which are not foreseen yet, 51 within, 7 more, 1 less, 14 not
  foreseen.
- **Heals**: the roll times 100 plus the element's characteristic, in percent, plus the heals bonus, times the heals
  multiplier; a steal heals its caster half the life it took of each target, rounded down. Measured: 27 heals within, 53
  foreseen on a fighter whose life was whole, which the server does not announce, 2 not announced.
- **Buffs** a cast's own effects gave: 20 boosts and 77 deboosts within their roll, 19 states as foreseen, 4 states of a
  carry the server sets itself.
- **Not settled**, as too few captures say: a push's damage, the references' `(level / 2 + 32 + bonus − reduction)` a cell
  not moved over 4, as foreseen 9 times, less twice, the pushes and pulls stopped short being 21 of 46; AP and MP removed, 1 within
  with a dodge, 4 fewer, the dodge being drawn; shields, whose buffs the captures foresee too rarely on their target; the
  erosion, read and never set against a loss; the effects on a cell, a summon, a teleport, a glyph or a trap, which reach
  no fighter: 9f's.
- **What still holds of Bubble's** (Q5): the order of damage and its roundings down, the zone's decrease, resistances
  fixed then in percent, the multipliers, critical effects as a list of their own, steals at half, heals; not its
  constants of caps, which no capture reached.
- **Benchmark**: a cast foreseen on a captured fight's map, spell 13858 on four enemies, in 40 µs and 11 KB, the zone's
  steps the most of it: 9e weighs it.
- **Rejected.**
  - Bubble's pipeline ported: GPL, and its bugs (D181).
  - The rolls and the critical hit drawn in the engine: a search weighs ranges, as D181 asked.

## D196: The bench of the effects, what it cannot measure, and the captures to make

*2026-10-30, step 9, session 9d. Builds D177, D190. Opens nothing: Q5 and Q16 hold what is left.*

- **`scripts/fights.cs`** replays the game captures through the bot's fight state, and for each cast a fighter chose sets
  what `D3B.Engine` foresees against what the server announced until the cast's sequence closed: damage by target and
  element, heals and steals, pushes and their damage, AP and MP removed, buffs, teleports, swaps and summons, the rules of
  the cast, the cooldowns, and the cells the fighters move from. It prints counts; `--details` lists, for each outcome
  the formulas do not explain, the spell and effects by id and the numbers compared, never an actor's id or name.
- **What it cannot measure**: what the server draws, the rolls, the critical hit's chance and the dodge of AP and MP,
  except as ranges; the effects no capture cast; what the client does not send (Q16).
- **Captures to make**, proposed to the owner, with D190's for Q16, in fights against weak groups:
  - pushes against a fighter and against an obstacle, at 1 and 2 cells, by characters of a known level, and pulls;
  - shields of a fixed value, of a share of the most life and of the level, then blows on them;
  - AP and MP removed and dodged, by spells that remove them, against monsters of known dodge;
  - a fighter with erosion, its losses and its most life after;
  - a heal on a wounded ally, an Eniripsa's or a Sadida's, and a heal with a heals bonus;
  - summons and their first turn: an Osamodas's, a Sadida's dolls and infection, a Steamer's turrets;
  - the Rogue's bombs and walls, the Huppermage's runes, the Eliotrope's portals, the Xelor's telefrags, the Zobal's masks
    and their spells' criteria, glyphs and traps, as 9f measures each;
  - a fight long enough that buffs end, so that their ends are seen in what the fighters may cast.

## D197: 9e as the owner asks it

*2026-10-30, step 9, session 9e, the owner's words, recorded as they asked. Builds D177, D192.*

- **Asked** for the session: the placement, then each turn a search for the best sequences of walks and casts on 9c's
  rules and 9d's effects, the effects foreseen applied to an imagined fight, thought again after each answer of the server.
  The bot sends by itself the placement, its readiness, its casts, its walks and the turn's end; when it has nothing useful
  left to do, or cannot act, it ends its turn at once instead of waiting for the turn's time. An autonomous AI that heals,
  shields, places itself and chooses its spells and elements by the situation, through the effects and never per class.
  During a harvest, a fight is played by the AI instead of the player, then the harvest resumes, pauses or stops as the
  profile says (D144). `Foresee` made fast for the search, 40 µs a cast before. Measured on real fights the owner starts
  against weak groups, captures kept, with D196's and Q16's.
- **Asked** as the owner watched the AI fight (D203): fewer turns passed doing nothing, no flight that serves nothing; a
  pace the player can follow, 0.9 s, the owner's choice; the AI cleverer still; the fight closed to other players, the
  group's later, and to spectators.

## D198: A fight judged too risky, a fight lost, and who plays the player's fights

*2026-10-30, step 9, session 9e, chosen by the owner among three options each, as the lead recommended them. Builds
D144. Superseded in part by D230 (the switch on the bot's card).*

- **Too risky**, the AI judging as the fight starts that the enemies would beat the character's side first
  (`FightOdds`, D201): the AI fights it anyway, prudently (`TacticWeights.Prudent`: the enemies' blows feared 1.6 times,
  its own life held three times an ally's, next turn's damage weighed less), and the log says so. In a fight against
  monsters, giving up is a loss and fleeing does not exist: fighting never costs more. 9g's fighting activity attacks no
  group judged too risky.
- **Lost**: the character comes back to its save point with little life (D178). A profile's setting, "After a lost fight",
  as "After a fight" is (D144): resume once healed, the default, the life regained to the profile's share it heals under,
  by its food (D148) or by waiting for the regeneration (D153); pause; stop. Three lost in a row stop the activity, so
  that it does not lose again and again.
- **The player's own fights**, out of an activity: a switch "The AI fights" on the bot's card, off at first; on, the AI
  plays every fight of the character. An activity that fights its own, harvesting, has them played either way
  (`BotActivity.FightsItsFights`). The player's hand in a fight pauses the bot, and the fight is the player's (D66, D202).
  `D3B.Host`, which has no card, reads `Fights:Alone`.
- **Rejected.**
  - Giving a risky fight up at once: a loss all the same, only time saved.
  - Leaving a risky fight to the player, the bot paused: the turns pass by the timer when nobody is there.
  - Always pausing or always stopping after a loss: a protector's loss would cost the tour.
  - The AI on every fight with no switch: the owner playing by hand would see the AI place and ready the character
    before acting.

## D199: The character's spells, mapping format 11

*2026-10-30, step 9, session 9e. Builds D178, D193. Superseded in part by D203 (the fight's own list).*

- **Measured** on the 52 game connections captured before 9e: `SpellsEvent` (`hkh`, pinned), sent once as the character
  enters the game, before its first map, 50 times, each listing 5 to 28 spells, each with its grade (`SpellItem`: the spell,
  field 2; the grade, field 4). Of the 231 casts the characters chose, 213 were of a spell it listed, at the grade cast; the
  18 others of a spell a level gained during the capture unlocked, which no message named, the base of its pair at the grade
  its least level allows. So the rule (`CharacterSpells`): each spell listed at the highest grade the character's level
  allows, and each of its breed's spells its level unlocks, the base of its pair unless the list holds the other: 231 of
  231. At each character's first fight, the rule added no spell to the list. Spell 0, a weapon's blow, listed too, is left
  out until the AI knows the character's weapon.
- **Mapping format 11**: the spells, then the fight's context of spells (D203) and the fight's options (D203); 210
  messages, 459 fields, 28 enums and 199 values audited, no failure, 13,954 bytes. The release of 3.7.12.12 needs it,
  then a release of the app (D172), each on the owner's go.
- **Rejected.**
  - The spells from the breed alone: a pair's spell is the player's choice, which only the list says.
  - The shortcuts' bar the fight's context gives: a spell may be cast that is on no shortcut.

## D200: `Foresee` for the search

*2026-10-30, step 9, session 9e. Builds D195.*

- **Measured** with BenchmarkDotNet on 9d's bench (spell 13858 on four enemies): 39.6 µs and 11.1 KB a cast before. The
  zone's steps were reckoned for each target and each size, the fighters' cells for each effect, the masks' groups for each
  fighter, and the formulas allocated closures.
- **Now**: the fighters' cells once a cast; each effect's zone once, and its decrease's steps once for all its targets; the
  masks' kinds and groups parsed once (`TargetMask`); no closure in the formulas; a zone's cells kept per thread, for the
  zones that depend on no fighter, 50,000 at most then all let go (`ZoneCells`); and an overload adding to the caller's
  list. 1.5 µs and 1.2 KB a cast, 1.2 µs and nothing allocated into a list, 4.0 µs cold, its zones reckoned again; then,
  an effect for the client only (`SpellEffectProperties.ForClientOnly`) left out (D203), 0.69 µs and 560 B, 0.66 µs into a
  list, 3.7 µs cold.
- **A turn planned** (`FightTacticsBenchmarks`, D201): on the same map, a character with two spells against four enemies
  casting back, the search and the enemies' reply on its finalists, in 1.6 ms and 2.75 MB.

## D201: The AI: an imagined fight, a judge, a beam search and the enemies' reply

*2026-10-30, step 9, session 9e. Builds D13, D185 to D196. Settles nothing of Q5.*

- **`D3B.Engine.Tactics`**, pure. `ImaginedFight`: the fighters as the engine sees them, the fighter playing, the round and
  its casts; the others hold the AP and MP of their next turn. `FightKnowledge`: each fighter's spells, the character's
  from the server's lists (D199, D203), a monster's and a summon's from the pack at its grade; the points each starts a turn
  with; the character's cooldowns.
- **`Imagination`**: a walk takes the MP and AP the path keeps, tackles included (9c); a cast spends its AP, counts in the
  history, and applies what `Foresee` gives, each effect at the middle of its range: damage from a shield first, a fighter
  dying at none, a steal healing half, heals to the most life, shields, pushes and pulls along their line, stopped by what
  blocks them and hurting by the cells left, swaps, boosts and deboosts, states, kills. An effect its mask only maybe
  selects counts half, one of a random group half, one waiting for its trigger half of its damage; a critical hit is never
  counted on; a teleport and a summon come from the level, on a free cell.
- **`FightJudge`**, in life: an ally's life, shields counted, concave, so that a point healed at a quarter of the life is
  worth twice one at the most; its being alive, half its most life; the character's counted `Survival` times. An enemy's
  life weighed by how hard it hits, and its being alive one and a half of its best turn. The threat: what each enemy may
  take next from the ally it hurts most, if its MP and its spells' range reach it. The approach: what the character may take
  off an enemy next turn from where it stands, never more than killing it now (D203), and a little for each cell nearer an
  enemy it cannot reach yet. A fighter's best turn on another is reckoned once, its spells' damage through the formulas, the
  AP shared at their limits a turn.
- **`FightTactics.Plan`**, a beam search: from the fight as it stands, every cast of the character's spells, from where it
  stands or from one of the two cells keeping the most MP it walks to first, on the 24 cells nearest the enemies where the
  level's effects reach fighters, imagined and judged; the 16 best kept, then the best 8 go on to their next cast, six casts
  deep, in 0.8 s at most. Each sequence ends on the walk judged best. The best 16 sequences found, ending the turn now among
  them, are weighed again once each enemy replied (`Replies`, D203): each plays its next turn as greedily, walking to one of
  the 12 cells nearest its targets, casting through the rules what lowers the side's worth most, three casts at most. The
  plan is the best sequence once they replied; empty when ending the turn now is best, or as good.
- **`FightTactics.Place`**: the free placement cell judged best, as a turn's end is. **`Odds`**: the turns each side takes
  to beat the other at its best a turn (D198).
- **Measured** by replaying every turn of the characters in the 66 fights captured: 161 of 166 turns plan a cast, 2 plan
  nothing, none walks away from the enemies without casting; a search takes 9 ms halfway, 69 ms at 90%, 158 ms at most.
- **Rejected.**
  - Minimax over several turns: the enemies' turns are many and their choices the server's; one reply, greedy, on the
  finalists alone, keeps a search within a second.
  - Monte Carlo over the rolls: the ranges' middles suffice to choose; the rolls are the server's (D181).
  - Weights per breed or per spell: every term is the effects', never a class's (D192).

## D202: The bot fights: placement, turns, the player's hand, and harvesting

*2026-10-30, step 9, session 9e. Builds D60, D66, D88, D144, D198.*

- **`FightPlay`**, one behavior from the placement to the end: once the fight's map is drawn and the server showed the
  character and its cells, it judges the odds, closes the fight and blocks spectators when the server has not (D203),
  places the character on the cell judged best and says it is ready. Each turn it plans, sends the plan's first action,
  waits for the server's answer and for the client's acknowledgement of its sequence, its showing it, then plans again
  from what the server said; it ends its turn as soon as the plan is empty, 30 actions at most. Every request waits the
  fight's pace first, `BotDelays.FightAction`, 0.9 s, the owner's choice, raised in the settings page; every wait ends with
  the fight.
- **Requests**: `FightPlacementPositionRequest`, `FightOptionToggleRequest`, `FightReadyRequest`,
  `GameActionFightCastRequest`, `MapMovementRequest` with the walk's turning points, `FightTurnFinishRequest`. The client
  follows the server's answers and acknowledges D3B's sequences as its own (D60). A cast the server refuses with an error
  text, or leaves unanswered, is not cast again that fight, and two in a turn end it.
- **`FightPilot`**, one per bot: while the bot's switch is on or its activity fights its own (D198), it waits for a fight not
  over, the bot connected and not paused, then runs `FightPlay` once the step the fight cut is over
  (`BotActor.RunInFightAsync`). A failure leaves that fight to the player.
- **The player's hand**: the placement's move and readiness, a cast, a walk and the turn's end are the player's
  (`PlayerActions`), the client sending by itself only its acknowledgements and its readiness for each turn: they pause the
  bot.
- **Harvesting** fights its own fights: the run answers the fight once it is over, as the profile says, a lost fight by
  "After a lost fight" (`BotActivity.OnFightOver`, D198); a pause or a stop before the end would stop the AI too.
- **Rejected.**
  - An activity "Fight" to start for the player's fights: its waiting step is a behavior the player's walk would pause.
  - Sending the whole plan at once: the server's answers change the fight, and a refusal would carry the rest.

## D203: 9e measured on the fights the owner started, and what they taught

*2026-10-30, step 9, session 9e, on the game connections the owner captured with D3B's host and app, a Xelor at level
91 and others. Builds D197 to D202. Supersedes in part D199 (the spells cast in a fight), D200 (the client's effects).*

- **Played**: 13 fights the AI played through `D3B.Host` and the app, 11 won and 2 lost; 177 casts sent, 167 cast, 2 refused
  with an error text, 3 unanswered; 81 walks, 49 turns' ends, 12 placements, 14 readinesses; every action the client showed.
  9 fights the player played meanwhile.
- **What the owner saw, and what changed**:
  - The AI spared an enemy at 14 life it could kill, then walked away: next turn's damage, unbounded, outweighed the kill.
    The approach is now worth no more than what it would take off the enemy (D201): replayed, the owner's fight went from
    10 turns of 19 without a cast to none.
  - It fled when it beat its enemies, and passed: the threat's guess, by reach alone. The enemies' reply is now played on
    the finalists (D201): replayed, no flight left.
  - Too fast to follow: the fight's pace, measured on the player's 49 fights, 1.3 s halfway between two actions; 0.9 s, the
    owner's choice.
  - Placed nowhere: the fight's context came before the server showed the character; the AI now waits for it.
  - Paused with nobody's hand: a cast killed the last enemy, the AI sent its turn's end after the fight, then waited, and the
    player's walk out of the fight paused it. Every pace and wait now ends with the fight.
  - Stuck in a fight rejoined: a client coming back mid-fight gets no fight's start; a turn now says the fight is fought.
  - Transformed into a bouftou by an alteration, its own spells went unanswered: `FightSpellsContextEvent` (`juf`, pinned),
    sent as the fight starts, lists the spells the client plays the fighter with (field 5) and the fighter (field 4): of 65,
    64 named the character, one a turret; in the 55 fights listing the character's spells, its 300 casts were of them, at
    their grade. `SpellsEvent` sent again as the alteration transformed the character, its field 3 its form's 2 spells. The
    AI casts the fight's list first, then a form's, then the rule of D199.
  - A Xelor's Refraction cast into nothing three times: its circle of damage is `ForClientOnly`, the client's preview, the
    server dealing it through a spell its telefrag state triggers, as the owner said. Left out, the bench of D196 on the 66
    fights captured: damage of the casts alone within 300 then 331, below 32 then 1, foreseen and not dealt 104 then 1.
  - A Zobal's masks refused: spells whose criterion asks a mask's state, 9f's (D177).
- **The fight's options**, the owner's ask: `FightOptionToggleRequest` (`jxf`) and `FightOptionUpdateEvent` (`jxm`), pinned.
  As each placement starts, the server says the four options unset; the owner's lock toggled option 0, which the server kept
  from one fight to the next, the eye option 2, answered with a text; the group's, the owner in no group, and option 3 not
  measured. The AI sets 0 and 2 as it places the character, always for now: the owner wants both settings later, a profile's
  or a bot's, with the group's for step 10.
- **Refusals** (Q16): the server refuses a cast with a `TextInformationEvent` of the error kind 29 ms after the request,
  text 175 twice; a text of the message kind, 188, came with 8 casts it made.
- **Left**: the AI cleverer still, as the owner says: the weights measured on more fights, the enemies' reply beyond the
  greedy, buffs and states worth what they do, summons, glyphs and the breeds' mechanisms (9f), the profile's strategies
  (9g). D196's and Q16's captures, but the refusals above, remain to make.

## D204: The repository's public face, its community files and the tool manifest in `.config/`

*2026-10-30, between steps 9e and 9e2, a session of its own, as the owner asked it. Builds D174, and the local tools of
D100 and D171.*

- **The tool manifest** moves to `.config/dotnet-tools.json`. Microsoft Learn: `dotnet new tool-manifest` writes it
  there, `dotnet tool install` creates it there in .NET 10 when none exists, and the SDK looks in each directory up the
  tree for `.config/dotnet-tools.json` or `dotnet-tools.json`, stopping at a manifest with `isRoot`. The root kept one
  more file for no gain. Checked: `dotnet tool restore`, then `dotnet-ef` and `vpk` run, `dotnet tool list` naming the
  manifest in `.config/`. Nothing names its path but the docs: the release workflow only runs `dotnet tool restore`.
- **The README**, rewritten for a public repository: what D3B does, what it never does (D2), its status, installing it
  (D171 to D173), getting started, building and running from source, the tools, how it works, the docs and the license.
  Its status is a short summary pointing to the roadmap, where it was the history of every step: a step's session now
  changes it only when what a user sees changes.
- **Community files**, written for D3B: `CONTRIBUTING.md` restates `CLAUDE.md`'s conventions and the rules never bent
  (D2, D3, nothing extracted from the client, no capture); `CODE_OF_CONDUCT.md`, D3B's own on the Contributor Covenant's
  ideas; `SECURITY.md`, naming what matters in D3B, the relay, the login tickets, the client's configuration, untrusted
  input and the updates; issue forms for a bug and a feature, blank issues off, and a pull request template.
- **Reports go through GitHub's private vulnerability reporting**, the security policy's and the code of conduct's,
  since the repository publishes no e-mail address (D174). It must be enabled in the repository's settings.
- **Found while checking the commands**: the release `client-3.7.12.12` holds mapping formats 3 to 10, and `main` reads
  11 (D199), so `scripts/embed.cs` fails on it, and so would the release workflow at the next tag, until the build's
  release gets mapping 11 through `scripts/release.cs`. Without captures, `scripts/mapping.cs` cannot map the build:
  `d3b.game.AuthenticationRequest` finds no counterpart, so a contributor takes the mapping from the release.
- **Rejected.**
  - The Contributor Covenant as it stands: generic, where D3B's spaces need rules of their own, such as on players'
    identities and captures.
  - An e-mail address for reports: none is published, and GitHub's form keeps a report private without one.
  - The manifest left at the root: it works, but `.config/` is where the SDK writes it.

## D205: 9e2 as the owner asks it

*2026-10-30, step 9, session 9e2, the owner's words and choices, as the lead recommended them. Builds D197, D203.*

- **Asked**: the AI of 9e cleverer, measured fight by fight on the owner's: the judge's weights measured on more fights,
  the enemies' reply beyond the greedy, buffs and states worth what they do over several turns, critical hits weighed,
  line of sight in the threat, positions over two turns, the weapon's blow; `FightPlay` and the pilot tested end to end
  in `TestPlay` on a pack that carries spells; the switch "The AI fights" kept for each character. Each change of the
  judgement proposed to the owner first, measured on the captures by replaying the turns, then tried in real fights.
- **Chosen by the owner**: the six changes of the judgement in this order, critical hits, line of sight, buffs and
  states, two turns, the reply, the weights, each measured on the replays and kept only when the measure says so; real
  fights once the first three are in, then at the end; the weapon's blow in this session, with the pack's format 6 it
  needs.

## D206: The turns and the fights replayed: `scripts/fights.cs --turns` and `--live`

*2026-10-30, step 9, session 9e2. Builds D196, D201. Supersedes the tools of D201 and D203 never committed.*

- **`--turns`** replays every turn a character played in the captures through the AI, from the fight as the bot would
  imagine it (`ImaginedFights`): the AI's plan against the turn played, both imagined and judged by the same numbers
  (the damage, kills and heals foreseen, the life the enemies' reply takes, the damage over two turns); and from where
  each turn really ended, what the judge reckons against what came: the threat (`FightJudge.Threat`) and the reply
  against the life the character lost before its next turn, the approach (`FightJudge.NextTurnDamage`) and the next turn
  searched (D210) against the damage it dealt in that turn, whom the enemies' blows took life from, the casts played and
  planned by what they do, the buffs played, and how long each search took.
- **`--live`** counts what D3B did in the fights: a fight is the AI's when D3B sent a request of the fight's itself (an
  injected frame), and the player's otherwise; how each ended, the character's turns and those with nothing done, the
  casts the server announced, refused by their text or left unanswered, D3B's requests by kind, the player's hand.
  Measured on 9e's captures: the 13 fights of D203, 11 won and 2 lost.
- Both print counts and sums, never a value of one fight, as the bench does (D196); `CastBench` also measures the chance
  of a critical hit (D207) and the client's previews of buffs (D209).
- **The base**, 9e's AI on the 61 captures of 3.7.12.12 before 9e2, 145 turns: its plans foresaw more damage than the
  turn played on 120, 32,380 against 22,069, the server dealing 25,925 on those turns; the threat foresaw 6,916 lost
  where the character lost 4,639, off by 2,918 summed, 35 turns foreseen with none lost; the reply 4,558, off by 1,315;
  the approach 12,260 where it dealt 14,112. Its plans cast 312 damage, 2 boosts, no heal, 5 summons; the turns played
  262 damage, 13 boosts, 12 heals, 19 summons. A search took 21 ms halfway, 159 ms at most.

## D207: A critical hit weighed by its chance

*2026-10-30, step 9, session 9e2. Supersedes in part D201 (a critical hit never counted on). Settles Q5 for the chance.*

- **Measured** on the 595 casts captured of levels with a chance of one, then on 131 more the owner played for 9e2: the
  chance is the level's plus the caster's characteristic 18, in percent, within 0 and 100
  (`EffectFormulas.CriticalChance`). It foresaw 43 critical hits, then 21, and the server drew 40, then 17; none of the
  217 casts of monsters whose characteristic 18 is below the level's chance was one, nor any cast of a level without a
  chance.
- **The imagination** counts what a cast gives in life, damage, heals, shields and characteristics as its share of a
  normal cast and of a critical hit, the level's critical effects where it has some; a push, a swap or a state is the
  normal cast's alone. The judge's best turns weigh it the same.
- **Replayed**: the damage foreseen of the turns played from 22,069 to 22,102, as many blows already take their target's
  whole life; the threat 5% higher. Little, and right.

## D208: Line of sight in the threat and the approach

*2026-10-30, step 9, session 9e2, as proposed to the owner. Supersedes in part D201 (the threat by reach).*

- **A spell lands** on a fighter when one of the cells its caster walks to with its MP has the fighter within the
  level's range and, for a level that tests it, in its line of sight; the threat counts each enemy's best turn with the
  spells that land on the ally it hurts most, the approach the fighter playing's with its next turn's MP and AP. An
  enemy's walk is reckoned once wherever the fighter playing stands, which the search moves many times: without it.
  `FightJudge` keeps the damaging levels, the damage of each, the cells walked to and what lands, once per search.
- **Replayed** on the 61 captures: the threat from 7,278 foreseen to 5,048, where the character lost 4,639; off by 3,224
  then 1,841; 35 turns foreseen with none lost then 8, 20 over twice then 12, 2 lost with none foreseen then 7. On the
  69 captures with the owner's 9e2 fights, 6,642 foreseen where 6,882 were lost. A search 25 ms halfway, 184 ms at most.
- **Rejected.**
  - A share of the blow for an enemy a cell or two beyond what lands, as the reach was weighed: off by 1,932, and 18
    turns foreseen with none lost.

## D209: Buffs and states worth what they do over the rounds they last

*2026-10-30, step 9, session 9e2, as proposed to the owner. Builds D193, D201.*

- **The imagination keeps each buff** a cast gives with its rounds (`ImaginedBuff`, `ImaginedFight.Buffs`, the effect's
  duration in `ForeseenEffect`): a boost, a deboost, a state.
- **The judge reads the fighters as the search left them**: the threat and the approach reckon each pair's damage from
  their characteristics and states as they stand, from the start's tables when nothing changed them, a state that
  prevents casting or dealing damage dealing none, one invulnerable, or at that range, taking none. A buff of the
  character's lasts until its turn of the last round begins (D193): it counts on the enemies' turns that come first, and
  on the character's next turn when it lasts two rounds or more. Each round it lasts beyond, up to the rounds the fight
  is foreseen to last still (its odds, 4 at most), it is worth the damage it adds to the character's side's turn, a
  point as an enemy's life costs it, or takes off the other side's, as the threat weighs it.
- **A multiplier the server left out is 100** (`EffectFormulas.ValueOf`): a boost of one adds to 100.
- **Measured**: the 13 boosts the owner cast in the captures, 7 of them spell 14574's damage and heals for 3 rounds. Its
  boosts are the client's previews (`ForClientOnly`): on its 8 casts the server gave the caster no buff (`CastBench`,
  the client's previews of buffs on the caster: 16 not given), its effect a breed's mechanism, 9f's. The other boosts
  were few; the replays plan 5 to 6. Tested: a rage that costs a blow now is cast in a long fight and not in a short
  one.

## D210: Positions over two turns: the next turn searched

*2026-10-30, step 9, session 9e2, as proposed to the owner. Supersedes in part D201 (the finalists judged by the
approach).*

- **Each finalist**, once the enemies replied, has the character's next turn played from there
  (`FightTactics.NextTurn`): its turn's AP and MP, those its buffs lasting add, the next round, the cooldowns one round
  less, the buffs of one round gone (`FightJudge.NextTurnStart`); two casts at most, each the one, from where it stands
  or a cell it walks to first, that raises the side's worth most (`FightJudge.Standing`), through the rules, the zones
  and the effects, as many fighters as it reaches. Its gain is weighed as the approach is; the approach where it finds
  nothing.
- **Replayed** on the 61 captures, against the same AI without it: over two turns 48,193 damage foreseen and 343 kills
  against 46,599 and 330, the reply taking 3,328 against 2,949; the next turn searched foresaw 11,869 where the
  character dealt 14,112, 2 under half against the approach's 11. A search 56 ms halfway, 422 ms at most, the next turn
  weighing 12 cells a level and one walk a cell.

## D211: The enemies' reply beyond the greedy, measured: no gain

*2026-10-30, step 9, session 9e2. Keeps D201's reply.*

- **Measured**: each enemy's three casts searched three wide rather than one at a time gave the same replies, to the
  life, on the 145 turns: the monsters of these fights cast one or two spells a turn. Whom they hit is as the reply
  foresees: between two of the character's turns, the enemies took 4,710 of the character's life and 231 of its
  summons', the reply 4,745 and 262; on the owner's 9e2 fights, 1,866 and 1,102, the reply 1,623 and 1,083. What is
  left, off by 1,377 on 84 turns, is each turn's: the rolls, the critical hits and what the server's monsters choose.
- **Rejected.**
  - The wider reply: as good, slower.

## D212: The judge's weights, measured on 69 fights

*2026-10-30, step 9, session 9e2. Keeps D198's and D203's weights.*

- **Measured** on the 69 captures, the owner's 9e2 fights among them, by replaying the turns with one weight changed at
  a time, against the defaults (the threat 1, the approach 0.8, the character's life 2): over two turns 58,472 damage
  foreseen, 411 kills, the reply taking 4,745 of the character's life. The threat at 0.9: 401 more damage, 2 more kills,
  135 more life lost; at 1.2: 554 less, 2 fewer, 2 more lost. The approach at 0.6: 799 less, 2 fewer, 14 less lost. The
  character's life at 3: 2,343 less damage, 7 fewer kills, 91 less lost.
- **So**: the defaults stand where the measure puts them. Fearing the threat more takes damage off and spares no life;
  holding the character's life dearer spares 2% of it for 4% of the damage, which the prudent weights keep for a fight
  judged too risky (D198). The threat's own foresight is unbiased on the 69 fights (D208), so no weight corrects it.
- **Left**: the prudent weights, no capture holding a fight judged too risky; measured as the owner's fights bring some.

## D213: The switch "The AI fights" kept for each character, and the fight played end to end in the tests

*2026-10-30, step 9, session 9e2, as the owner asked. Builds D198, D202. Superseded in part by D230 (the switch on the
bot's card).*

- **Kept** in the database (`KnownCharacter.FightsAlone`, migration `FightSwitch`): the card saves it as it is set
  (`AccountBook.SetFightsAloneAsync`), a failure said in a toast; a bot made for a character whose switch was left on
  has the AI play its fights from its first (`RememberedFightSwitch`); the host's `Fights:Alone` turns every bot's on.
  Checked on the owner's machine: set on one character of seven, it was there at the app's next start.
- **`TestPlay`'s pack** carries a spell, an earth blow, its effects and empty states, monsters and breeds;
  `FightPlayTests` plays a fight as the captures measured it: the AI closes the fight and blocks spectators, places the
  character on the cell it reaches the monster from, says it is ready, casts twice on its turn, the client acknowledging
  each, then ends its turn; without the switch nothing is sent; the player's placement pauses the bot.

## D214: The owner's fights for D196 and Q16, measured

*2026-10-30, step 9, session 9e2, on 9 game connections the owner captured by hand with their characters, 8 fights, 35
of the characters' turns, characters of level 20 to 150 and their summons. Builds D195, D196. Settles Q5 in part.*

- **Damage** of the casts alone: 91 within what D3B foresaw, 13 within capped by the target's life, 4 below, 1 dealt and
  not foreseen; with the spells the effects cast, which are not foreseen yet, 38 dealt and not foreseen (9f).
- **Pushes and pulls**: 9 pushes as far as foreseen, 11 stopped short, 3 on a target dead first; 5 pulls as far, 3
  short. **A push's damage**: 8 as foreseen and 3 none as foreseen, after 9 of 11 in 9d: the references' formula holds
  (Q5).
- **Heals** on a wounded ally: 8 within, 2 within capped by its missing life, 6 on a fighter whose life was whole, 1 not
  healed. **Steals**: 7, each life taken as foreseen. **Swaps**: 4 as foreseen.
- **AP and MP removed**: 1 AP and 4 MP below what was foreseen, 3 and 8 removed by the spells the effects cast: the
  dodge, which the server draws, left a range to weigh (Q5).
- **Critical hits**: 17 of 131 casts with a chance, 21 foreseen (D207).
- **The rules** (Q16): of 208 casts chosen, 197 pass; the server let 11 through that D3B's rules refuse: 8 for want of
  AP, of a breed whose spells' cost falls with a state it gathers (states 2484 on), 2 out of range and 1 beyond the
  summons, of breeds whose spells a state changes. Each a breed's mechanism, 9f's, which the spells' modifiers do not
  carry yet.
- **The client's previews of buffs** (D209): of 10 on the caster, 1 boost and 1 state given as previewed, 8 not.
- **Not captured yet**: a shield and the blows on it, erosion, a fight long enough for buffs to end; a weapon's blow
  (D215).

## D215: The weapon's blow in a session of its own

*2026-10-30, step 9, session 9e2, chosen by the owner, as the lead recommended it. Supersedes in part D205.*

- **Measured**: none of the 180 turns of the characters captured holds a weapon's blow; the AI cannot be measured on
  one.
- **So**: the weapon's blow gets a session of its own, once the owner captured blows with several kinds of weapon: the
  pack's format 6, the 769 weapons with their AP, ranges, critical chance and bonus, casts a turn, effects and their
  type's zone (client.md); the character's worn weapon from its inventory, its position measured; spell 0 as a level the
  AI casts, measured blow by blow; then a release of 3.7.12.12 and of the app on the owner's go.
- **Rejected.**
  - The weapon now, its formulas unmeasured on Dofus 3: a pack's format and a release for what no capture checks.

## D216: 9e2 measured on the fights the AI played, and what the owner saw

*2026-10-30, step 9, session 9e2, on 10 game connections the owner captured with the app while the AI played every one
of their characters on their accounts, 9 breeds, levels 20 to 150. Builds D205 to D215. Supersedes in part D203 (the
options' numbers).*

- **Played** (`scripts/fights.cs --live`): 17 fights the AI played, 16 won and 1 lost, against 11 won and 2 lost in 9e;
  180 casts sent, 176 cast, 4 refused with text 175, none unanswered; 92 walks, 62 turns' ends, 16 placements, 17
  readinesses, 18 options set; of the character's 81 turns, 5 with nothing cast nor walked, against 11 of 62 in 9e.
- **Replayed** (`--turns`, 71 turns): the reply foresaw 1,841 of the enemies' blows on the character where they took
  1,660; a search 86 ms halfway, 403 ms at most. Benchmarked, a turn planned against four enemies in 3.1 ms and 6.5 MB,
  against 1.6 ms and 2.75 MB in 9e, BenchmarkDotNet run in its process as other sessions' worktrees hold the project
  too.
- **The options' numbers were the other way round**: the owner saw the AI refuse spectators first and close the fight
  second, where it sent option 0 then option 2. Option 0 refuses spectators, the server answering with text 40, and
  option 2 closes the fight, with text 95, as Dofus 2 numbered them; `FightOption` names them so, the mapping pairing
  their numbers unchanged. The AI now closes the fight first, then refuses spectators, as the owner asked; both to be
  the profile's or the bot's settings in 9g (D203).
- **The element of a blow**, which the owner saw not always that of the character's highest characteristic: of the 116
  blows the AI cast, 59 were of it; 48 of another where no spell of it could be cast on that cell or none was foreseen
  dealing more a point of AP, the target's resistances, the range and the AP left deciding; 2 in a sequence dealing more
  in all, 1 as a spell's casts a turn allowed, and 6 of a character of level 150 whose four elements' characteristics
  are 0, every element alike. The formulas read the characteristics as the server gives them, every character's 54 at
  its first turn, and the damage of the casts alone fell within what D3B foresaw 226 times of 229.
- **What the owner saw is a breed's**: a Zobal loops on its mask of MP and shields the enemies, where its mask of damage
  would let it cast Furia: 28 of the AI's casts planned and 29 played were shields. The breeds' mechanisms are 9f's
  (D177), this one among the first.

## D217: 9e3 as the owner asks it

*2026-10-30, step 9, session 9e3, the owner's words and choices. Builds D215.*

- **Asked**: the weapon's blow, measured first on the blows the owner captured with several kinds of weapon: what the
  server announces, where the worn weapon is in the inventory, what the cast's request sends; the pack's format 6, the
  769 weapons with their AP, ranges, line and diagonal, line of sight, critical chance and bonus, casts a turn, effects
  and their type's zone; the character's worn weapon; the blow as a level the AI casts, measured with
  `scripts/fights.cs`; then real fights.
- **Found**: the captures hold 2 blows, both a sword's, those of 9a and 9c (D178, D191); none was captured since 9e2.
- **Chosen by the owner**: swords only for now, the owner having few weapons to strike with; no fight in game for this
  session, the branch pushed and its pull request opened at once. As the owner said, a weapon's blow pays off at very
  high level only: the AI weighs it as it weighs a spell, by what it foresees for its AP, never forcing it.
- **Rejected.**
  - The 13 kinds of weapon at once: their zones and their decrease are measured on no blow.

## D218: The weapon's blow, measured on the captures

*2026-10-30, step 9, session 9e3, on the 80 game connections of 3.7.12.12 captured, with measuring tools never
committed that print counts.*

- **The request**: `GameActionFightCastRequest` naming spell 0, whose field proto3 leaves out, and the cell struck.
- **The server**, 28 and 29 ms later: a sequence of type 1, a weapon's, its first action 303, a cast naming no spell, no
  level and no critical field, the cell and the fighter on it; then the AP the weapon costs spent, 3 and 4, and what its
  effects did: a sword's neutral steal of 5 to 6 took 5 and healed its caster 2, a sword's earth blow of 16 to 25 took
  8, the target's whole life. The client acknowledged both sequences as it does a spell's.
- **The worn weapon**: of the 75 inventories the server sent as the character entered the game, the 63 holding a
  weapon worn held it at position 1, 45 a bow and 18 a sword, and position 1 held nothing else; each other position, 0
  and 2 to 15, held objects of one super type. A stack carries its own
  effects (its field 7, each an action and its dice): the 6 weapons worn, 5 swords and a bow, rolled as their item does,
  so that the pack's weapon is the blow's.
- **Equipping**: `ObjectMovementEvent` (`iqn`, as BillBot names it), a stack's uid and its new position: 2 in the
  captures, a pet taken off to 63 as another came, and worn at 8 after the client's request (`ito`). No weapon changed
  in the captures.
- **Spell 0 in the lists**: every list of the character's own spells names it, 77 as it entered the game and 96 as a
  fight started; the 16 lists of a form or a turret do not. Its own level is the punch: 3 AP, range 1, neutral 6 to 7,
  9 to 10 on a critical hit, a chance of 5.
- **The weapons' characteristics**: the pack names 122 and 142 "Armes (%)", the damage weapons deal and receive where a
  spell's are 123 and 141, 103 "Bonus de puissance pour les armes" and 31 "Maîtrise d'arme". On the 358 turns of the
  characters captured, 122 and 142 were 100 and the server never gave 103 nor 31.

## D219: The pack's format 6, the weapons

*2026-10-30, step 9, session 9e3. Builds D29, D182.*

- **`WeaponCatalog`**, in `D3B.Data.Items`, its own entry (`items/weapons`), read once by `GameTables.Weapons`: each
  weapon of the client's `WeaponData` by its item, its type, AP, least and most range, line, diagonal and line of sight as
  a level's bits, critical chance and bonus, casts a turn, and its effects as a slice, each an effect and its dice and
  value; the types of super type 2, each with its zone as the client writes it (`rawZone`), kept as text until blows of
  each shape are measured. `ItemCatalog` is unchanged.
- **Measured**: 769 weapons of 13 types, 6 zones: the cell struck (`P`) for 8 types and 479 weapons, `T1,10,1` the 94
  staffs, `X1,0,10,1` the 99 hammers, `V1,10,2` the 61 shovels, `U1,10,1` the 16 scythes, `L3,10,3` the 20 lances. AP 2
  to 7 on 763; ranges 0 to 20 and 0 to 23; 270 in line, 77 in diagonal, every one testing the line of sight; a chance of
  30 on 438, 0 to 100; a bonus of 0 to 30; once a turn on 611, twice on 148. 6,637 effects, 1,311 used in fights, 2
  naming effect 205, which the client lacks, kept and counted. Every other field of a weapon's effect is the same on all,
  a circle of 1 its zone, which is not the blow's: the extractor fails when one changes.
- **Read** through `UnityDataTable.ReadByIdWithReferences`: a weapon names its effects by reference, which only the
  table's registry holds. The items' table is read once for both catalogs.
- **The pack**: 7,452,604 bytes, 19,946 more than format 5, the same bytes on two extractions; its layout recorded in
  `PackFormatTests`. The release of 3.7.12.12 needs it and mapping 12 (D221), then a release of the app (D172), each on
  the owner's go.

## D220: The weapon's blow as a level of spell 0

*2026-10-30, step 9, session 9e3. Builds D185, D195, D201, D207. Settles nothing of Q5.*

- **`WeaponBlows`**, in `D3B.Engine.Fights`, built once per pack: each sword's blow a level of spell 0, the punch's with
  the sword's fields, its AP, ranges, line, diagonal and line of sight, critical chance and casts a turn, and as effects
  those of the sword's the server uses in fights, rolled as the sword rolls them, reaching what the punch reaches: the
  cell struck and any fighter there. 122 blows, the 126 swords but the 4 that do nothing in a fight. Its levels follow
  the pack's, from id 1,000,000, which no cast names (`SpellCatalog.WithLevels`): the rules, the effects, the imagination
  and the judge share one catalog, the pack's spells and the blows, copied once, 14 ms and 8.7 MiB.
- **The damage**, the references' order (D181), the damage calculation the Dofus 2 client ran, which Bubble ported: a
  level of spell 0 deals a weapon's damage (`EffectFormulas.DealtByWeapon`), raised by the weapons' power too, a critical
  hit adding the weapon's bonus to the fixed bonuses, then the weapon mastery in percent; the multipliers of weapons,
  122 and 142, take the place of the spells'. A critical blow's effects are the normal ones. On the characters
  captured, only the critical bonus is left to measure (D218).
- **The AI** weighs a blow as a spell (D217): `FightTactics.Blows`; tested, a strong sword is struck beside the spell,
  a weak one leaves its AP to the spell.
- **Rejected.**
  - The blows written into the pack's spells: the pack holds the client's data, and a rule to correct would need a new
    pack and a release.
  - The worn object's own effects read from the protocol: equal to its item's on the 6 weapons worn, a mapping to make
    for nothing measured.
  - The other kinds of weapon now: their zones' text is read by no measured rule (D217).

## D221: The bot strikes with its sword: mapping format 12

*2026-10-30, step 9, session 9e3. Builds D199, D202, D203. Supersedes in part D199 (spell 0 left out).*

- **The worn weapon**: the stack at position 1 (`InventoryItem.Weapon`, `CharacterState.Weapon`), kept as the server
  moves it, `ObjectMovementEvent` named and pinned for 3.7.12.12.
- **Mapping format 12**: 211 messages, 461 fields, 28 enums and 199 values audited, no failure, 14,015 bytes; the 80
  game connections, 102,323 frames, decode through it, 53,016 understood whole, 17,631 more read, none misread.
- **The cast's history** counts a weapon's blow as spell 0's, the first action of a weapon's sequence, so that its casts a
  turn hold.
- **The character's spells** (`CharacterSpells.InFight`): the worn sword's blow when the list the AI casts from names
  spell 0, its own, the fight's or a form's; a form's names none. The punch is never cast, unmeasured.
- **`FightPlay`** sends spell 0 on the cell and waits for the weapon's sequence to be shown; tested end to end through
  `TestPlay`, whose pack carries the punch and a sword: the AI strikes the sword and casts its spell, each once.

## D222: 9e3 measured with `scripts/fights.cs`

*2026-10-30, step 9, session 9e3, on the 80 game connections of 3.7.12.12 captured, 100 fights. No fight in game, the
owner's choice (D217).*

- **The bench**: the 2 blows captured, both a sword's D3B strikes: the rules accept both, the AP spent are the sword's,
  the damage falls within what D3B foresaw once and within capped by the target's life once, and the steal's heal
  within.
- **`--turns`**: 255 turns, 338 searches; on the 109 turns of a character wearing a sword D3B strikes, the AI planned 8
  blows, and the players struck none. Against the same AI without the blows, on the same turns: this turn's damage
  foreseen 46,834 against 47,075, kills 307 against 303, heals 11,517 against 11,741, the reply taking 7,168 of the
  character's life against 7,379; over two turns 78,748 damage against 78,985 and 532 kills against 531. A search 64 ms
  halfway, 221 ms at 90%, 666 ms at most, against 62, 177 and 485. The sword rarely pays: as the owner said (D217). The
  2 blows captured fall in turns the replay does not reach, as D215 found.
- **`--live`** counts the blows D3B sent and the server announced: none, no fight of the AI's having had a sword.
- **Left**, for captures to make: a critical blow and the weapon's bonus, the other kinds of weapon and their zones, a
  weapon changed and struck in one session, and the AI's blows in real fights.

## D223: 9f as the owner asks it

*2026-10-30, step 9, session 9f, the owner's words and choices. Builds D177, D216.*

- **Asked**: the hard cases, the Zobal's masks first: measured on the owner's captures, which states the server sets,
  when, and what each mask changes of the spells, their cost, range and effects; then the rules of a cast, the effects
  and the AI taking them in, mechanism by mechanism, measured blow by blow with `scripts/fights.cs`, its bench, `--turns`
  and `--live`; the captures missing said before the next mechanism.
- **Added by the owner as the session ran**: many spells work through criteria, so every spell whose criterion asks a
  state is measured and taken in, not the Zobal's alone; Bubble's emulator and BubbleBot read for every breed and effect,
  for ideas (D3).
- **Measured in the pack**: of the 836 spells and variants of the 19 breeds, 132 have a criterion on states, asking 43
  states, 27 of which the breed's own spells set, directly or through a spell an effect casts: the Forgelance 41 spells
  (armed and disarmed, Éclipse), the Zobal 32 (its masks), the Pandawa 16 (carried, drunk and sober), the Féca 10 (its
  glyphs), the Ouginak 8 (its rage and beast form), and fewer for the Ecaflip, Xélor, Sacrieur, Roublard, Eliotrope,
  Eniripsa, Osamodas, Iop, Crâ, Steamer and Huppermage, most of them forbidding state 7, *Pesanteur*. The Enutrof, the
  Sram and the Sadida have none.
- **So**: one mechanism, a fighter's states, as the server sets and ends them and as the spells' effects set and end them
  through the spells they cast, read by the rules, the effects and the AI alike; the Zobal's masks measure it first, as
  the only breed whose captures hold every step.

## D224: The Zobal's masks, measured

*2026-10-30, step 9, session 9f, on the pack of format 6 and the 80 game connections of 3.7.12.12, 6 fights of a Zobal
among them, 38 of its turns, with measuring tools never committed that print counts.*

- **The states**: 98 *Intrépide*, 99 *Psychopathe*, 100 *Pleutre*, one a mask, two mask spells each, a spell and its
  variant: 13386 and 13409 for 98, 13388 and 13414 for 99, 13387 and 13406 for 100. Spell 18633, *Le Masque du Zobal*,
  sets 98 for the whole fight as each fight starts: 6 times in 6 fights.
- **A mask, as the pack writes it and the server played it 14 times** (13386 once, 13387 seven times, 13388 six): 1 AP on
  the Zobal's cell, every other round, of 1 stack. It casts, by effect 2794 on itself, spell 18634, whose grade 1 casts,
  by 792, its grade 2, 3 or 4 for the state worn, which sets the old mask's cooldown to 1 and casts grade 5: the three
  states taken off where worn (`C,E98` and so on), two spells' cooldowns lowered by 1. The mask then sets its own state for
  the whole fight, casts 18636, which dispels (406) the buffs of every mask whose state the Zobal is not in (`C,*e98` and
  so on), and 18637, its look; then its boosts, on the Zobal for the whole fight and on its allies in a cross for 2
  rounds: 1 AP for *Intrépide*, 1 MP and 20 to 40 dodge for *Pleutre*, 7 to 10% of melee damage and 10 to 20 lock for
  *Psychopathe*; then a spell of its summon's.
- **What the server announces**: the casts of each step, the new state's buff, the boosts, and the end of the old
  state's and boosts' buffs by their uid in a message D3B did not read, `juu` (D225). A mask cast again, its state worn,
  had the server end its earlier boosts before giving them anew: 4 of the 14.
- **What a mask changes of the spells**: which the Zobal may cast, not their cost or range. 32 of its 44 spells and
  variants ask a mask's state, 16 *Intrépide*'s, 11 *Psychopathe*'s and 12 *Pleutre*'s, some of two; no mask gives a
  spell modifier. Two spells deal otherwise by the mask worn, through their effects' masks on the caster's state:
  *Carnavalo*, its shield, damage or push larger in the matching mask, and *Transfiguration*; none was cast in the
  captures.
- **The refusals of D203**: the 6 casts of the AI's the server refused with text 175 asked state 98, the Zobal wearing
  99 or 100: the bot's state still held 98, its buff's end unread. Read (D225), the 6 criteria fail and the 38 other
  casts the AI asked with a mask's criterion hold, all cast; the client's 59 such casts of all breeds hold too.
- **Rejected.**
  - The masks written as a Zobal's rule, a state per mask and the spells it opens: every breed's states work so (D223),
    and the pack says it, effect by effect (D169).

## D225: A buff's end: mapping format 13

*2026-10-30, step 9, session 9f. Builds D193. Supersedes in part D193 (no buff's end announced).*

- **`FightBuffEndEvent`** (`juu`, pinned): a buff's uid and its fighter, two of its four fields, with no action of its
  own. Of the 1,171 the captures hold: 334 as a turn started, on buffs the fight's state had let go at their end; 184 on
  buffs already gone, dispelled by spell or ended by their action; and 653 the state held, 270 boosts, 267 states, 64
  spell modifiers and 52 triggered effects, in a spell's sequence or what it triggered. The fight's state lets the buff go.
- **Mapping format 13**: 212 messages, 463 fields, 28 enums and 199 values audited, no failure, 14,064 bytes; the 80
  game connections, 102,323 frames, decode through it, 54,187 understood whole, against 53,016 with format 12, 17,631
  more read, none misread. The release of 3.7.12.12 needs it, on the owner's go (D172).

## D226: A caster outside the zone: `C` alone

*2026-10-30, step 9, session 9f. Supersedes in part D195 (a mask naming the caster).*

- **Measured**: 32 casts of an effect of mask `c` without `C` with the caster outside its zone, such as a Zobal's
  Tortoruga, a shield of mask `l,m,c` cast on an enemy at 4 cells or more: none buffed the caster. The references read
  `C` alone outside the zone (D181), and D195's 16 buffs to a caster outside its zone were of `C`.
- **So**: `TargetMask.NamesTheCaster` is `C`'s; `c` names the caster in the zone. This was the loop the owner saw in
  9e2 (D216): the AI foresaw each Tortoruga on an enemy shielding the Zobal, which it never did, and cast it 32 times.
  Replayed, the AI plans 1 cast doing nothing on the Zobal's 38 turns, against the 32 the turns played.

## D227: The spells effects cast, imagined, and a spell's buffs taken back

*2026-10-30, step 9, session 9f. Builds D195, D201, D209. Supersedes in part D195 (the spells effects cast not counted
on).*

- **The spells an effect casts at once** (`Imagination`): of the pack's levels, 792 casts at once 6,423 times, 1160 4,881,
  2160 944, 1017 426, 2960 383, 2792 348, 2794 234 and others fewer. A spell an effect casts at once, on a fighter its
  mask surely selects, is cast in turn, by whom and where the client's action says, as the references read them
  (`SpellCaster`, `EffectActions.CasterOf`): 792 and its kin by the fighter reached on itself, 1160 and 2160 by the caster
  on it, 2794 by the fighter reached on the cell cast on, 2960 by the caster there, 1017 by the fighter reached on the
  caster, 1019 by the caster on itself. Its effects are foreseen on the fight as the effects before left it, so that each
  step reads the states the step before set or ended, four deep at most, as a Zobal's mask chains three. Its own critical
  hit is not weighed; one a trigger waits for, one drawn, or one a mask only maybe selects is not cast.
- **States ended** (951) and **a spell's buffs dispelled** (406): a fighter holds its buffs by the spell that gave each
  (`Combatant.Buffs`, `HeldBuff`), as the server gave them or as the search's casts did; a dispel takes back what each
  gave, a characteristic, a state no other buff gives, a spell modifier, and the search's lasting buffs of it
  (`ImaginedBuff.SpellId`). A buff past its level's stacks replaces the oldest of its spell and effect on the fighter, as
  the server did for a mask cast again.
- **Measured** on every cast the fighters chose (D229): the 20 states the Zobal's 10 changes of mask set and ended, as
  foreseen.
- **Rejected.**
  - The spells effects cast foreseen in `SpellEffects`, which the bench reads, on the fight before the cast: a mask's
    dispel reads the caster's state before the mask sets its own, and would take the new mask's buffs instead of the
    old.
  - A table of each breed's switching spells: the effects say it (D169).

## D228: The judge reads the criteria on states

*2026-10-30, step 9, session 9f. Builds D201, D209.*

- **`CastRules.StatesAllow`**: whether a fighter's states let it cast a level, none preventing casts but those the level
  asks, and its criterion on states holding, wherever and whenever; `Check` reads the same.
- **`FightJudge`**: a spell its states forbid deals nothing in the threat, the approach, the next turn and a buff's worth
  over its rounds. A mask's state is so worth the damage of the spells it lets the Zobal cast more than the others, and
  an enemy's threat counts the spells its states let it cast.
- **Replayed** on the Zobal's 38 turns: the AI puts on *Psychopathe* and casts Furia where the turns played cast
  Tortoruga on enemies, and changes mask 29 times in its plans against 9 in the turns played, at a turn's end for the next
  turn's AP or MP too.

## D229: 9f's states measured with `scripts/fights.cs`

*2026-10-30, step 9, session 9f, on the 80 game connections of 3.7.12.12, 100 fights. No fight in game: no new capture.*

- **The bench** counts, for each cast a fighter chose, the states each living fighter holds once its sequence ended
  against those the AI imagines (`Imagination`), and the life it took, imagined and announced; and the casts of a mask
  naming the caster in its zone only with the caster outside (D226). The rest of the bench is unchanged: 1,197 of 1,229
  casts pass the rules, the 32 others the Eliotrope's portals, the Steamer's traps, the Roublard's bombs and a summon.
- **States**: of the 244 states the casts whose effects cast spells at once set or ended on living fighters, the AI
  foresees 111, against 24 without the spells effects cast: 83 set and 28 ended as foreseen, the 20 of the Zobal's masks
  among them; 98 set and 35 ended it did not foresee: a carry's states, which the server sets itself (D195), the
  Eliotrope's portals, the Xélor's telefrags, whose masks ask what the fight's state does not tell (`T`), and the
  Roublard's bombs, the mechanisms to come; 4 foreseen and not so. Of the casts casting none, 30 states ended on others
  the AI did not foresee, the Eliotrope's portals going, and 5 set.
- **Life a cast took**: of the 382 casts casting spells at once, 36,503 imagined against 39,327 announced, where the AI
  without the spells effects cast imagined 36,218; of the 847 casting none, 22,973 against 27,202.
- **`--turns`**: 255 turns, 338 searches, 67 ms halfway, 173 ms at 90%, 374 ms at most, against 84, 245 and 681 before
  9f. Shields planned 19 against 37; casts planned doing nothing but spend AP 6, against 118 in the turns played, 32 of
  them the Zobal's; the life the casts played were foreseen to heal or shield 5,328 against 9,240, the Tortorugas' shields
  gone (D226). The
  spells effects cast add 4,775 of heals and shields to what the plans foresee, 11,815 against 7,040 without them, and
  change the damage foreseen by less than half a percent, the searches as long.
- **Benchmarked**, BenchmarkDotNet in its process: a turn planned against four enemies in 3.1 ms and 4.79 MB, against 3.1
  ms and 6.5 MB in 9e2; a cast foreseen in 0.72 µs, 0.67 µs into a list, 3.6 µs cold, as before.
- **`--live`**: no fight of the AI's since 9e2.

## D230: The fights' profiles before the next mechanism, and the AI's fights a harvest profile's option

*2026-10-30, step 9, session 9f, the owner's choice. Supersedes in part D177 (9f before 9g), D198 and D213 (the switch on
the bot's card).*

- **Asked by the owner** as 9f ran: before the next breed's mechanism, the fights' profiles, made as the harvest's are,
  with many useful options; and the AI fighting the fights a harvest meets set in the harvest's profile, an option
  checked by default, instead of the switch on the bot's card.
- **So**: the next session makes the fights' profiles, 9g's settings (D192, D203), and moves the switch into the harvest
  profile; the hard cases go on after it, one mechanism a session.
- **Left to that session**: which options, the AI's weights and prudence, spells preferred or left out, a role, the
  fight closed and spectators refused, among them; what becomes of the player's own fights out of an activity, which
  the card's switch had the AI play (D198), and of the switch kept in the database (D213), each asked of the owner.

## D231: The owner's logo as the app's icon

*2026-10-30, between steps 9f and 9g, a session of its own, as the owner asked it; the small frames chosen by the owner,
as the lead recommended them. Builds D171.*

- **What**: the owner's logo, a green egg and its swirl above the white letters "D3B", is the icon of the window, of
  `D3B.App.exe` and of the installer. In `src/D3B.App/Assets/`: `d3b.ico`, frames of 16, 24, 32, 48, 64, 128 and 256
  pixels, and `d3b.png`, its frame of 256.
- **Where**: `Window.Icon` names the `.ico`, from which Avalonia's Win32 backend takes a small and a big icon, the
  taskbar and Alt+Tab showing them. ShadUI draws its own title bar, so the `.png` replaces Lucide's bot in its
  `LogoContent`, 20 pixels as the scale's largest icon (D159). `ApplicationIcon` puts the `.ico` in the executable,
  which Explorer and the shortcuts show. `vpk pack --icon` gives it to the package, in the release workflow and in the
  README's command.
- **Made once**, from the owner's PNG of 1314 by 1197, by a throwaway file-based app on SkiaSharp, Avalonia's own from
  the NuGet cache, outside the repository: the logo cut to its opaque bounds, 1106 by 1062, and centred in a transparent
  square of 1106; each frame the mean of the pixels it covers, weighed by how much of them it covers, in premultiplied
  alpha so that no edge darkens; the frames of 16, 24 and 32 sharpened by an unsharp mask of 3 by 3, amount 0.8; the
  frames under 256 as 32-bit bitmaps with their mask, the 256 as PNG, as Windows' own icons.
- **Checked**: WPF's `IconBitmapDecoder` reads the seven frames; the icons Windows takes from the built and the
  published `D3B.App.exe`, and from a `D3B.App-win-Setup.exe` packed by vpk 1.2.161 with `--icon`, are the logo at 16,
  32, 48 and 256; the app running, `WM_GETICON` gives its window a small icon of 16 and a big one of 24, and the title
  bar and the taskbar show the logo. The views' tests count the pack's icons in the window's content, the title bar's
  logo outside it.
- **Legibility, looked at** on dark and light backgrounds, magnified and at their size: "D3B" reads at 32 once
  sharpened, is guessed at 24, and is lost at 16, where the green egg over a white band still tells the app apart.
- **The owner chose** the whole logo at every size, its small frames sharpened, among three: that, the frames reduced as
  they are, and a simpler drawing of the owner's for the small frames.
- **Rejected.**
  - The egg alone in the small frames, cut from the logo: the letters hide its lower half, so a crop is half an egg, its
    swirl cut. A whole egg is a drawing of its own, which the owner may give later.
  - The letters alone: twice as wide as high, they would be some 7 pixels high at 16.
  - The frames reduced without sharpening: "D3B" softer at 24 and 32.
  - A tool in `scripts/` making the frames: a tool for what is done once, whose package pinned centrally would pin
    Avalonia's SkiaSharp too (`CentralPackageTransitivePinningEnabled`).
  - A PNG as `Window.Icon`: one image scaled to every size Windows asks for, where the `.ico` gives each its frame.

## D232: 9g as the owner asks it

*2026-10-30, step 9, session 9g, the owner's words, recorded as they asked. Builds D192, D230. D231 is the app's icon, a
session of its own.*

- **Asked** for the session: the fights' profiles made as the harvest's, with many useful options, among them the AI's
  weights and prudence, spells preferred or left out, a role, the fight closed and spectators refused, and what follows a
  lost fight; "the AI fights" in the harvest profile, an option checked by default, instead of the switch on the bot's
  card, and what becomes of the fights out of an activity and of the switch kept in the database; the AI measured with
  the default settings on `scripts/fights.cs`, its bench, `--turns` and `--live`, nothing regressing.
- **Asked** as the session ran: the spells' icons in the pack, as the resources' are (D135), since the profiles choose
  spells; and, in the same session, the fighting activity made as the harvest is: a monster or a group of monsters
  focused, an area or a sub-area, the bot finding the groups by itself and attacking them.

## D233: A fight's strategy, a profile of its own that each character plays

*2026-10-30, step 9, session 9g, chosen by the owner among three options, as the lead recommended it; the name the owner's
choice among the lead's. Builds D99, D168.*

- **A strategy** is a profile of a kind of its own on the profiles page, edited, copied and given presets as an activity's
  are, never started: how a character fights. Each character plays one, chosen on the accounts page beside its activity's
  profile, "Standard", D3B's defaults, when none is chosen; the AI fights every fight it plays for the character with it.
  An activity's profile says whether the AI plays its fights, and what follows a fight and a lost fight.
- **Why**: spells and a role are a breed's and a character's; a character fights the same way whatever it does; groups,
  at step 10, ask a role of each character.
- **Named** "Strategy" (« Stratégie »), so that it is not mistaken for the fighting activity, "Fight" (« Combat »).
- **Rejected.**
  - A strategy chosen in each activity's profile: one profile shared by breeds mixes their spells, a role means nothing
    there, and no profile answers a fight out of an activity.
  - Both, the character's overridden by an activity's: the most flexible, the most complex; it can come later over this.

## D234: A strategy's options

*2026-10-30, step 9, session 9g, the lead's list, kept by the owner, who asked for the spells' icons (D232). Builds D198,
D203, D212, D216.*

- **Strategy**: a role, attacker by default, healer, the allies' life twice as dear and heals and shields first, or
  protector, the allies dearer, its own life less, closer to the enemies; a style, prudent, balanced by default, or
  offensive, the judge's three weights; prudent when the fight is judged too risky, on by default as D198 has it; prudent
  under a share of life, off by default, 30 %.
- **Spells**: spells preferred, each worth a quarter more than what it brings, a cast that brings nothing never made worth
  making; spells left out, never cast; the weapon's blow, on by default (D220).
- **Placement**: automatic by default, as the AI judges it, near the enemies or far from them.
- **The fight**: closed and spectators refused, both on by default (D216).
- **Presets**, stored once (D168): "Prudent", "Offensive", "Healer" and "Protector"; "Standard" is D3B's defaults.
- **Elsewhere**: what follows a fight and a lost fight stays the activity's (D233); the pace, 0.9 s, the machine's
  settings page; a fight closed to all but the group, step 10; which groups to attack, the fighting activity's.
- **Rejected.**
  - The weights as numbers: unreadable, the style sums them up.
  - A search's length: no search reaches its 800 ms, 374 ms at most (D229).
  - A target first: the judge kills already (D203).
  - Giving a fight up: D198.
  - The game's 8 roles, placement and hindering among them, which the AI does not play apart yet.

## D235: The fights out of an activity, a strategy's option; the switch gone from the card and the database

*2026-10-30, step 9, session 9g, chosen by the owner among three options, as the lead recommended it. Supersedes D198 and
D213 on the switch.*

- **Measured** in the code: the player's hand pauses a bot only during its behavior (D66), so the switch had the AI take
  the fights of a character the owner played by hand too, as soon as one began.
- **A strategy's option**, "The AI plays the fights out of an activity", off in "Standard": a strategy the owner gives the
  characters they test the AI with, attacking groups themselves. An activity's own fights follow its profile; harvesting's
  "The AI fights", checked by default.
- **The switch** leaves the bot's card, and its column the database, by a migration. The lead said one character of
  seven had it on (D213); a copy of the owner's database, migrated for the check, held 9 of 15 with it on. Told so, the
  owner kept the migration as it is and set their database aside, to test 9g from a new one. `D3B.Host`'s `Fights:Alone` stays, every bot's fights played, for the measures.
- **Rejected.**
  - Always the player out of an activity: the owner would test the AI from the command line alone.
  - An activity "Fight" with nothing to do but the fights the player starts: beside "Strategy", two names for one thing.

## D236: The fighting activity's options, and the monsters' icons at 64 pixels

*2026-10-30, step 9, session 9g, the lead's list, kept by the owner, who chose the monsters' icons at their 64 pixels.
Builds D127, D144, D148, D149, D165 to D168, D192, D198.*

- **What**: automatic, the groups at the character's level, by default, or monsters chosen, a group attacked when it holds
  one of them; monsters left out, a group holding one never attacked; a group of 1 to 8 monsters; only the groups the AI
  judges it wins, on by default, as D198 has it.
- **Where**: automatic, the sub-area holding the most monsters at the character's level, as D166 chooses a harvest's, by
  default, or chosen, an area and sub-areas suggested by the monsters chosen; isolated maps and zaaps as harvesting's.
- **Upkeep**: healing under 70 % by default, by the profile's food, else by waiting; banking full, the loot, as
  harvesting's.
- **After a lost fight**: resumed once healed by default, paused or stopped, three in a row ending the activity (D198).
- **A preset**, "Automatic fight". The activity fights with each character's strategy (D233).
- **In the pack**, format 7: the monsters' icons at 64 pixels and where the monsters live, beside the spells' icons.
- **Later**: a drop sought (D192), the pack holding no drops; aggressive monsters.

## D237: The breeds' spells' icons in the pack, format 7

*2026-10-30, step 9, session 9g, the owner's ask (D232), measured by the lead and a delegated agent. Builds D135 to D139.*

- **Measured** on 3.7.12.12: `Picto/Spells` holds 3,128 icons at 1x, 48 pixels, and the same at 2x, 96, BC7 of one level
  as the items' are, each texture and sprite named `sort_<iconId>`; the 19 breeds' spells and both spells of every variant
  are 862 spells with 862 icons, none missing.
- **In the pack**: those 862 at their 48 pixels, raw RGBA (D136), a third table of `IconCatalog`, keyed by `Spell.IconId`;
  `IconTable` carries its own size, items and professions staying at 64. An icon missing fails the extraction, naming
  the spells by breed, as the items' do.
- **Measured**: 1.16 MB more in the pack, the icons' entry 1.74 MB compressed and 10.3 MB read in 45 ms, the spells' bundle
  decoded in 0.36 s; two extractions give the same SHA-256; a dozen icons looked at, upright and whole.
- **Rejected.**
  - The 2x icons, 96 pixels: four times the bytes, drawn at 24 to 32 pixels.
  - Every spell's icon, the monsters' among them: 3,128, which no setting chooses.

## D238: The monsters' icons and the sub-areas they live in, format 7

*2026-10-30, step 9, session 9g, the owner's choice of 64 pixels (D236), measured by a delegated agent. Builds D27, D182,
D237.*

- **Icons**: `Picto/Monsters` names its 2,397 textures of 64 pixels by the monster's look, `Monster.GfxId`, not its id: an
  archmonster shows its monster's picture. The pack keeps the pictures of the 1,663 monsters a sub-area lists, but bombs
  and those the bestiary hides, 1,099 pictures, a fourth table of `IconCatalog` keyed by gfx; a picture missing is
  skipped and counted, the client lacking 4 of monsters met nowhere.
- **Where monsters live**: the client's sub-areas list 3,039 pairs, 444 sub-areas and 1,681 monsters; the monsters' own
  lists add 266 pairs of 36 monsters, events' eggs and leprechauns, two quest monsters and a sub-area that does not
  exist, left out. `WorldAtlas.MonstersOf(SubArea)` gives a sub-area's, its slice of `WorldAtlas.Monsters`.
- **Measured**: `UseSummonSlot` is set on 4,655 of 5,129 monsters, 1,650 of the 1,716 a sub-area lists: it says what a
  monster does once summoned, never that it is met only so. The pack is 11,748,845 bytes, against 7,452,604 at format 6;
  the icons' entry 4.87 MB compressed, read in about 110 ms the first time an icon is drawn; extracted in 23 to 30 s,
  two extractions alike.
- **The release** of 3.7.12.12 needs `data-7.pack`, then the app's, each on the owner's go.

## D239: The monster groups on a map, mapping format 14

*2026-10-30, step 9, session 9g, measured by a delegated agent on the 80 game connections of 3.7.12.12. Settles Q15.*

- **Measured**: 919 roleplay maps, 593 with groups, 1,801 groups of 1 to 8 monsters, 640 of one; each a negative id, a
  cell, a main monster and up to 7 others, each with its monster, grade and level. Every one of the 5,755 monsters seen is
  a grade of the pack's monster at that grade's level: a group's fight is imagined from the pack alone, which settles
  Q15 for choosing a group.
- **Named and pinned**: `RoleplayActor.monster_group` and its monsters; `MapActorShownEvent` (`jns`), one actor shown,
  1,701 times, 147 groups, 116 of them shown again just before they leave; `MapActorRemovedEvent` (`kjz`), 1,198 times,
  116 groups; a group walks 1 to 4 cells, its path its first and last cell. Mapping format 14: 217 messages, 472 fields;
  55,385 frames of 102,323 understood whole, against 54,187.
- **The attack**: 98 the client sent, 97 fights; the walk ended on the group's own cell 94 times, the character stood on
  it twice, and once the group had walked 1 cell, accepted. From 3 cells away the server answered nothing but the walk,
  and the client walked again and attacked 0.9 s later. The fight's defender is the group, its monsters the group's.
- **Not measurable**: an aggressive group, the group's alignment (1 on every group), the map's flag of aggressive
  monsters, true on 159 maps without groups.
- `CharacterState.MonsterGroups` keeps them (`MapMonsterGroup`, `MonsterInGroup`), from the map's information, the actors
  shown and removed, and their walks; a new map empties them.

## D240: The strategies made

*2026-10-30, step 9, session 9g. Makes D233 to D235.*

- **A kind of profile** (`ProfileKind.Strategy`), registered by the bots module with `AddStrategy`, listed by
  `ActivityCatalog.Strategy` beside its `Activities`, edited as any profile, its kind named "Type" in the dialog making
  one; never started, by the card or `ProfileStarter`. Four presets stored once: "Prudent", "Offensive", "Healer", far
  from the enemies, and "Protector", near them.
- **Each character's**: `KnownCharacter.StrategyId`, set on the accounts page, null for "Standard"; deleting a strategy
  leaves its characters with Standard. Migration `Strategies` drops `FightsAlone` and adds the column with its
  reference by SQLite's own `ALTER TABLE`, measured: EF rebuilds a SQLite table to drop a column or add a foreign key,
  outside the migration's transaction, which it warns of. Checked on a copy of the owner's database, its 15 characters
  kept.
- **Played**: `IStrategySource` gives a character's, D3B's database's (`StoredStrategies`, read from the books' memory,
  once per save) or the defaults; `FightPilot` reads it as a fight starts, `BotActor.Fights` says who plays: the host's
  `Fights:Alone`, else the running activity's own fights, else the strategy's fights out of an activity.
- **The weights**: the style's, Standard's D212's, Prudent D198's, Offensive the threat at 0.8, next turn's damage at 1
  and the character's life at 1.5; prudent when risky or under its share of life; a healer holds its allies' life twice
  as dear (`TacticWeights.Allies`) and next turn's damage at 0.6 of its style's, a protector its allies' at 1.5, its own
  at 0.6 and next turn's damage at 1.25. A preferred spell's cast adds a quarter of what it raises the judge's worth by,
  carried by the sequence to its end and its reply; spells left out and the weapon, the character's alone
  (`FightKnowledge.Without`); a placement near or far by the nearest enemy's distance first (`TacticPlacement`).
- **In the app**: the card's switch gone; the accounts page a column "Strategy"; spells and monsters chosen as the
  resources are, in one picker filtered by breed or super race, with their icons (`SettingKind.Spell`, `Monster`).
- **Harvesting**: "The AI fights", checked by default; unchecked, the fight is the player's and the harvest waits as before
  9e, "After a lost fight" hidden.

## D241: The fighting activity made, to measure in game

*2026-10-30, step 9, session 9g. Makes D236.*

- **`FightActivity`**, registered as `fight` by `FightingModule`, a default module. Each step heals under its share, then,
  on a map of the zone, walks onto the cell of the nearest group the settings allow and the AI judges it beats, and sends
  the attack, the walk made again while the group walks off, three times at most; a group the server answers nothing
  for, within the answer's delay, is left for the visit. A map without one is gone through, and the step travels to the
  nearest map left; once every map was, the tour starts again, the place chosen again.
- **Judged** (`GroupJudge`): a group within its size, none of its monsters left out, one chosen or none above the
  character's level; then, when only the groups it beats are asked, the odds of D198 on a fight imagined from the
  pack's grades, their life, points, characteristics and spells, and the character as its last fight's placement
  showed it (`BotActor.LastFighter`), at its life now. Before its first fight, by levels alone.
- **The place** (`HuntingGround`): the sub-areas chosen, an area's own with monsters, or the one whose monsters are worth
  the most at the character's level, each 20 levels below halving a monster's worth, none above it, the sub-area's level
  not above the character's, a basic account's sub-areas alone when unsubscribed; its maps the world graph reaches.
- **Its fights**: the AI plays them with the character's strategy; won and lost counted on the card, with the loot,
  banked when full; a lost fight answered as harvesting's is (D198).
- **Left**: measuring it in game, the first attacks D3B sends; a sub-area's suggestion by the monsters chosen; isolated
  maps, which D236 listed; aggressive monsters and a drop sought (D236).

## D242: 9g measured with `scripts/fights.cs`

*2026-10-30, step 9, session 9g, on the 80 game connections of 3.7.12.12, pack format 7 and mapping format 14. No fight
in game.*

- **With Standard**, the defaults: the bench and `--live` print what they printed before the session, line for line;
  `--turns` the same plans, judgements and foresight on its 255 turns and 338 searches, the searches 69 ms halfway, 171 ms
  at 90 % and 402 ms at most, against 66, 177 and 388.
- **Benchmarked**, BenchmarkDotNet in its process: a turn planned against four enemies in 2.6 ms and 4.79 MB, against 3.1
  ms and 4.79 MB in 9f.
- **Not measured**: the other strategies on the captures, and the fighting activity, which no capture holds.

## D243: 9g2 as the owner asks it

*2026-10-30, step 9, session 9g2, the owner's words, recorded as they asked. Builds D232, D241, D242.*

- **Asked** for the session: the fighting activity and the strategies measured in game, the owner running "Automatic
  fight" and a profile of monsters chosen on characters of several breeds, with "Standard", then "Healer" and "Prudent",
  the app recording the captures; measured on them with `scripts/fights.cs --live` and what tools it needs, the attacks
  sent, accepted or unanswered, the walks made again, the groups judged winnable against the fights won or lost, the
  sub-area chosen, the healing and the bank, and what the game shows fixed; then what D241 left that serves, the
  sub-areas suggested by the monsters chosen and isolated maps, and the styles and roles measured on `--turns`.
- **Asked** as the session ran: a Discord badge in the README, linking the project's server; and no strategy at all, one
  generic AI that uses every breed's potential and farms fast, complete and clever, every spell and every breed's
  mechanism (D244).

## D244: The strategies removed, one AI for every character

*2026-10-30, step 9, session 9g2, chosen by the owner among two options, as the lead recommended it. Supersedes D233, D234
and D240, D235 on the fights out of an activity, and D236 and D241 where they name a strategy.*

- **Removed**: the strategy, a kind of profile (`ProfileKind`, `AddStrategy`, `ActivityCatalog.Strategy`), its settings,
  presets and texts, `IStrategySource`, `DefaultStrategies` and `StoredStrategies`, the accounts page's column, and
  `KnownCharacter.StrategyId`, by migration `NoStrategy`, which deletes the strategies' profiles and presets first. The
  engine is as before 9g: no placement near or far (`TacticPlacement`), no allies' weight, no spell preferred, no
  fighter's spells left out. "Standard" played as the AI did before 9g (D242): nothing the AI plays changes.
- **What the AI does, every fight**: prudent when it judges the fight too risky as it starts (D198); the fight closed,
  then spectators refused (D216); its sword's blow when it is worth it (D220); placed where the judge says.
- **Out of an activity**, the fights are the player's again; `Fights:Alone` has the AI play every one, for the measures.
  Harvesting's "The AI fights" and the fighting activity's own fights stay as D235 and D241 have them.
- **The spell setting** (`SettingKind.Spell`), its picker by breed and the spells' icons drawn in the app go with the
  strategy, their only user. The spells' icons stay in the pack's format 7, which no release holds yet, rather than a
  format 8 for 1.16 MB; nothing reads them now.
- **Measured**: SQLite drops a column whose reference is its own, once its index is dropped, inside the migration's
  transaction; what the `Strategies` migration's way down says is not so. Checked with EF's own SQLite on a copy of a
  database of 6f migrated to `Strategies`, a character given a strategy: the column, its index and its reference gone,
  its profile and preset deleted, the integrity and the references whole, down and up again.
- **A complete AI**: what it lacks for every breed is 9f's other mechanisms, summons, bombs, glyphs, runes, telefrags,
  traps and invisibility, portals, carrying, sacrifice, shared damage, and the other kinds of weapon; one or two a
  session, each captured, imagined, searched and measured. At step 10, a character's role in a group is the AI's to
  judge, from its breed and the fight. Farming fast is the fighting activity's: the pace, the groups, the healing and the
  journeys, measured in game.
- **Rejected.**
  - The strategies kept, hidden, "Standard" for all: code that nobody uses, to maintain.
  - The spell setting kept for plugins: none asks for it, and its tests were the strategy's.

## D245: What D241 left, made: sub-areas suggested by the monsters, isolated maps, and the place chosen on maps shown

*2026-10-30, step 9, session 9g2, the owner's ask (D243), measured by the lead on the pack of 3.7.12.12 with tools never
committed that print counts. Builds D149, D166, D236, D238, D241.*

- **Suggested by the monsters**: a sub-area setting may be suggested by a monster setting as by a resource setting
  (`SettingBuilder.SuggestedBy`, `SettingDefinition.SuggestedKind`). The fighting activity's sub-areas offer first those
  where the monsters chosen live, the most of them first, then the lowest, each with its level, its maps and its
  monsters (`GameReferences.SubAreasLiving`, `SubAreaMonsters`); with the monsters hidden, "Automatic", none.
- **Isolated maps**: harvesting's reckoning, shared (`IsolatedMaps`, in `D3B.Bot.Movement`). The fighting activity's
  ground leaves out the maps reached from their sub-area's main part only far round, unless its "Isolated maps", off by
  default as harvesting's, takes them, travelled to last. Measured: of the 311 sub-areas listing monsters with maps the
  world graph reaches, 9,791 maps, 83 hold isolated maps, 660 in all, 1 to 80 a sub-area and 4 halfway.
- **The place chosen for the character, measured**: their monsters' worth alone chose a sub-area of one map shown on no
  world map at level 10, and one of 13 maps on none, a dungeon's likely, at level 150. The activity now chooses among the
  sub-areas holding at least 10 maps the world graph reaches on a world map (`HuntingGround.LeastMapsShown`), else the
  richest of the others: each level from 5 to 200 gets one of 10 to 217 maps, levels 1 to 4 the only one at theirs, 9
  maps; a basic account's characters the same sub-area of 121 maps from level 55.
- **Rejected.**
  - A sub-area's worth weighed by its maps: the groups a map holds are the server's, which no pack gives; the floor
    leaves the choice D166's.
  - The isolated maps taken by default: the harvest leaves them out, measured far round (D149).

## D246: The fight profile's editor as the harvest's, and the monsters' picker filtered by a drop-down

*2026-10-30, step 9, session 9g2, the owner's words as they tried the app: the monsters' picker too big to validate, and
the fight profile's editor not good enough. Builds D127, D159, D166, D236, D245.*

- **The picker**: its filter's groups are chips up to 12, the professions that gather being 7; past them, as the
  monsters' super races, some 40, are, a drop-down beside the search (`GamePickerViewModel.MostChips`), so that the
  dialog keeps its height and its buttons in the window.
- **The fight profile's preview**, as harvesting's (`FightPreviews`): the ground's mini-map, its isolated maps marked;
  the maps to go through, the monsters living there, or the chosen of them that do, their levels, the way to the nearest
  bank on foot (`NearestBank`, shared with harvesting's) and the isolated maps; and for each character playing the
  profile, the sub-area the activity would choose at its level, a subscribed character's, the editor knowing no
  subscription. The mini-map's legend names probable and empty maps only for a zone that tells them apart, a harvest's.
- **Then, as the owner tried it**: a profile no character plays yet, its place automatic, drew a lonely "Auto" and the
  harvest's "Choose resources and a place". With no character's level known, the monsters chosen give one, the highest of
  their grades', and the preview draws the place at it, saying so; else the activity says why no zone shows
  (`SettingsPreview.Message`), as harvesting's does too, and the app's own word is "Choose what and where". The rows' names
  wrap beside their controls in a narrow window, where they ran under them (`DockPanel.FieldName`).
- **And**: the monsters' pictures, a whole body on 64 pixels, are drawn at 36 beside a name, items and professions
  staying at 24 (`OptionViewModel.PortraitSize`); every option's icon is drawn once first shown, where opening the picker
  drew them all. "All" of the monsters, some 1,600, laid out at once in a panel that does not virtualize, made the picker
  stutter: it shows the first 120 that match and says how to see the others, searching or filtering
  (`GamePickerViewModel.MostShown`).
- **And as the window narrows**, the preview, beside the settings at 320 pixels whatever the width, squeezed them until
  their controls were cut: it stands beside them while they keep 520 pixels, under them below (`SidePanel`). The profile's
  name, a box of 280 pixels in a row that never shrank, ran under the page's buttons: it shrinks to 128.
- **Rejected.**
  - A scrolling row of chips: the 40 groups still unreadable.
  - Every editor rebuilt: the owner's words name the picker and the fight profile; the preview is what it lacked
    beside the harvest's.

## D247: The loot's icons in the pack, and families of monsters chosen

*2026-10-30, step 9, session 9g2, the owner's words as the fighting activity ran in game: the loot on the bot's card
showed no icon, and a family of monsters should be chosen as a monster is. Builds D135, D236, D238, D246.*

- **Measured** on 3.7.12.12: the client's monsters list their drops, `drops`, each an item and its chances by grade;
  2,348 of the 5,129 monsters drop 3,513 items. Those the 1,663 monsters a hunter meets drop are 2,761, every one with its
  icon in `Picto/Items`.
- **In the pack**: their icons, in the items' table at its 64 pixels beside the harvests', 2,294 icons in all against
  287; a drop's icon the client lacks is counted and left out, as a monster's picture is. The pack weighs 19.4 MB against
  11.7, extracted in 36 s, two extractions alike; its format stays 7, its structure unchanged and no release holding it yet, an older pack
  drawing the loot without its icons. The drops themselves stay out (D236).
- **Families**: a family is a monster's race, as a player names them, the bouftous or the animalades; its super race, the
  picker's filter, groups races by where they live. The fighting activity's "Families to fight", beside its monsters,
  attacks a group holding a monster of one of them (`ChosenMonsters`, `SettingKind.MonsterFamily`); each is offered with
  its super race beside it. Measured on 3.7.12.12: 265 races, 208 a hunter meets a monster of, which are offered, in 35
  super races; the animalades a race of 4 monsters. The place chosen, the preview and the sub-areas suggested count their
  monsters with those chosen. The super races, first offered as the families, were not what the owner meant: a family
  the owner sought, the animalades, was a race.
- **And**: one fight won is said in the singular on the card.
- **Rejected.**
  - The loot's icons at 32 pixels in a table of their own: some 5 MB less for a structure of its own, a format 8, and a
    table the app draws at another size than the items'.
  - A family checked as all its monsters at once in the picker: a list of a hundred monsters on the card, and a family's
    new monster left out.

## D248: Every interactive transition of the world graph followed, a refused teleport remembered, and NPCs' ways not yet

*2026-10-30, step 9, session 9g2, what the owner's Sadida showed in game: stuck in Incarnam, every profile failing at once
with "No map of the zone can be reached"; the owner asked whether the ways that need an NPC are handled, and to look at
how BillBot does it. Builds D73, D76, D83, D84.*

- **Measured** on its capture: in Incarnam, D3B asked a teleport from the map nine times; the server refused each, text
  416, though the pack's flag (bit 3) lets 147 of Incarnam's 163 maps teleport. A journey that fails at its teleport
  goes on foot, and found no way: the next journeys tried the teleport again, three journeys failed, and the activity
  gave the zone up.
- **The way on foot**, measured on the graph: from the Sadida's map, every transition followed, criteria aside, 77 maps
  of Incarnam are reached and no other area's; the room whose doors lead to five areas, Astrub's among them, has no way
  in but doors with quests' criteria. The way out is an NPC's dialog, which the client's graph does not hold: 3.7.12.12's
  transitions are scrolls, scrolls with an action, map actions and interactive elements. BillBot, read for the owner,
  leaves Incarnam the same way, by a case of its own: the NPC "Portail vers Astrub" on its map, two replies matched by
  their text; it handles no other NPC's way but two dungeons'.
- **Made**: the pathfinder follows every interactive transition of the graph, the doors' and some 300 of 50 other
  skills, stairs, portals and "go to"s, used as a door is, their criteria strict, a use that fails banned for the journey
  (D76's doors alone before); a teleport the server refuses from a map is never asked from it again for the bot's life
  (`WorldPathRules.NoTeleportFrom`, `BotActor.NoTeleportFrom`). Neither measured in game yet.
- **Not made**: the ways through an NPC's dialog, Incarnam's portal first, the next session's (D250).

## D249: A group judged over the fight's course, and only the hopeless turned away

*2026-10-30, step 9, session 9g2, the owner's words in game: the bot takes almost only small groups where it could beat
big ones, and should be much laxer. Builds D198, D236, D241.*

- **Measured** with `scripts/fights.cs --live` on the 98 attacks the owner made by hand and those D3B made: of the 54
  groups the character was known for, the owner won 50; the judge of D241, every enemy striking every turn
  (`FightTactics.Odds`), turned 25 away, 21 of them won.
- **Over the fight's course** (`FightTactics.OddsOverTheFight`): the enemies die one after another, the most damage for
  their life first, each striking only while it lives; the judge's damage a turn and life as before.
- **Turned away**: a group the AI expects to take the character's life in under a quarter of the turns it needs to beat
  the group (`GroupJudge`), measured: 13 of the 54, 11 of them won, against 25. The ratio tells the losses apart badly: 2
  of the 4 losses fell among the groups judged won first. "Only winnable groups" unchecked attacks every group the
  settings allow; "Monsters per group, at least" aims at big ones.
- **Unchanged**: the fight's own prudence as it starts (D198) keeps `Odds`.
- **Rejected**: the judge removed by default: a lost fight costs the regeneration and three stop the activity (D198),
  and the judge's error is measured on a player's fights, not yet on the AI's big ones.

## D250: 9g2 measured in game, and what it leaves

*2026-10-30, step 9, session 9g2, on the 9 game connections the owner captured with the app as the fighting activity ran
on five characters of two accounts, breeds Sadida, Zobal, Eliotrope, Enutrof and Xelor, levels 45 to 150. Builds D241 to
D249.*

- **Hunted**: 22 attacks sent, 22 accepted, each after one walk onto the group's cell, none made again; 22 fights, the
  AI's, won; from a fight's end to the next attack 6.2 s halfway, 34 s at 90 %, the healing and the journeys within; an
  attack's fight starting 0.58 s after it halfway.
- **Fought**: 106 casts sent, every one cast, none unanswered nor refused; 56 turns, none without a cast or a walk; 2
  sword's blows struck.
- **What the owner saw, made**: strategies gone, one AI (D244); the monsters' picker, the fight profile's preview and its
  layout in a narrow window (D246); the loot's icons and families of monsters (D247); the ways out of Incarnam and
  refused teleports (D248); big groups (D249).
- **Left, for the next sessions**: the ways through an NPC's dialog, Incarnam's portal first; the Zobal's masks, which
  the AI puts on at its turn's end for the next turn rather than first to cast their spells, its earth Zobal casting
  Furia 3 times in 11 turns; every item's icon, 11,755 against 2,294, which the owner asked for and whose size is the
  owner's choice.

## D251: A banker who opens no bank where he stands, refused there

*2026-10-30, after release 0.3.0, what a friend of the owner met in game: banking in Sarakech, the bot entered the wrong
building of the bank's map and talked to the wrong NPC, again and again, the owner's words; a blocking bug, to publish
again. Builds D29, D79, D80.*

- **Measured** on the friend's capture: Sarakech's bank, in front of map 173278211, has three insides at its coordinates.
  In the port's, the NPC 3597 stands, whom the pack names a banker; his question there offers one reply, none of the
  bank's. D3B took him for the bank's banker, left the dialog, talked to him again three times, and gave the banking up:
  a map holding a banker's NPC counted as the bank's whatever his question offered.
- **Made**: a banker's question offering none of the bank's replies refuses his map for the host's run
  (`BankDirectory.Refuse`): no banker is found there any more, and its inside is no longer sought, so that the next
  banking goes to the bank's nearest other inside.
- **Not measured in game yet**: which of Sarakech's two other insides holds the bank.

## D252: A turn's end the client marks as its own, not the player's hand

*2026-10-30, after release 0.3.1, what the owner and a friend met in game: in a fight, the client's inactivity popup
showed and the bot paused, though no one had touched the client, the owner's words. Builds D66, D202.*

- **Measured** on the friend's capture (client.md): the client ended 4 of the character's turns by itself,
  `FightTurnFinishRequest` with its first field set, which none of the 170 turns' ends the owner sent by hand nor D3B's
  146 sets. D3B counted every turn's end the client sent as the player's hand (`PlayerActions`): two of them cut the
  AI's turn and paused the bot, and a turn after ran its 60 s, no one playing.
- **Made**: a turn's end the client marks as its own is not the player's, and the field is named `automatic`; the
  mapping pairs fields by number, so that the published mapping 14 still reads it. The AI's turn cut, it plays the next
  one. A turn's end by hand still pauses the bot.
- **Left**: while its popup shows, the client may still end the AI's turns by itself. Keeping the popup away would mean
  feigning a player at the client, too near what D2 puts out of scope. When the client shows it is not measured.

## D253: The ways through an NPC's dialog, measured, known by their ids, Incarnam's portal first

*2026-11-01, step 9, session 9g3, the owner's ask: a character in Incarnam must leave it, and the journey through the
world must go through an NPC's dialog, Incarnam's portal first, the harvest and the fights leaving Incarnam; measured on
captures the owner made, BillBot read for the owner, and the client's data searched for what would know these NPCs and
their replies without a text written in the code. Builds D73, D80, D81, D248.*

- **The client's data, measured** on 3.7.12.12: an NPC holds its messages and replies, each reply its id and its text,
  and `dialogData`, which places its bubble and mood; nothing says what a reply does or where it leads, and no table
  places an NPC on a map but the world map's hints. The 189 hints of category 9, Transports, name places, boats, zaaps,
  portals and carts, never an NPC; one, "Portail vers Astrub", stands on map 153880835, the map BillBot names, where the
  server places NPC 4398, of the same name. What a dialog does only the server knows.
- **Measured by hand**, a character of level 1 (client.md): NPC 4398, talked to with action 3, asks question 30637,
  offering reply 36979 alone, then question 30638, offering 36977 to leave and 36978 to stay; 36977 ends the dialog and
  the server moves the character to map 192416776, Astrub's temple, in the same millisecond, 29 ms after the reply.
  Astrub's way back to Incarnam is two doors, which the graph holds.
- **Measured through D3B**, a character of level 92: the portal moves it as soon as it is talked to, no question asked,
  then opens on the new map a dialog whose question, 30636, offers no reply, which the client closes by itself 2 to 9 ms
  after. D3B took that leaving for the player's hand (D81) and paused the bot.
- **Made**: a way through an NPC's dialog is data the code holds, measured, by the client's ids: the NPC's map, the NPC,
  the replies that lead on whichever question offers them, and the map it leads to (`NpcWay`, `NpcWays.Measured`).
  No text is matched, as BillBot's patterns are: the server names the NPC on the map and lists each question's replies by
  their ids. `WorldPathfinder` goes through the ways it is given, from any vertex of the NPC's map to the only vertex of
  the map it leads to, one change as a scroll is, a way to a map of several zones never taken as a zaap's
  (`WorldStep.IsNpcWay`, `WorldStep.OfNpcWay`). `MapChange` talks to the NPC from where the character stands, as to a
  banker, waits the client's display time before each reply (D132), gives the offered reply that leads on, and waits
  for the new map, whether it comes with the dialog's end or as the NPC is talked to; a question offering none of the
  way's replies is left, and the way refused for the bot's life (`BotActor.NoNpcWays`, `WorldPathRules.NoNpcWays`), as a
  teleport the server refused (D248). A question offering no reply that the client leaves open on the new map is left
  by the bot. The client's leaving of a dialog whose question offers no reply is not the player's (`PlayerActions`).
  The journal says it (`MapChangeKind.Npc`).
- **Reached**, on the pack of 3.7.12.12 from Incarnam's arrival, level 1 and no item: 77 maps without the portal, 530
  with it without a subscription, 5,420 with one; Astrub's bank in 12 changes. The harvest, the fights, the bank and the
  editor's previews take the ways, every journey going through the pathfinder.
- **Not made**: no other way. The other portals, 6357, 6358 and 7240, speak of a dimension; the boats, carts and
  bells of the hints name no NPC, no map and no destination measured, and some cost kamas. Each is a way to measure in
  game, then one line of `NpcWays.Measured`. BillBot's replies 36982 then 36980, which the portal may offer at another
  level, are in the way, not measured.
- **Rejected.**
  - The NPCs and their replies extracted into the pack, known by their French texts as the bankers are (D29): a format
    and a release for replies the code still could not choose, and destinations the data does not hold.
  - A case of its own for Incarnam, as BillBot's: the pathfinder would not know the way, and the activities would still
    find no map of a zone outside Incarnam.

## D254: 9g3 measured in game, and what it leaves

*2026-11-01, step 9, session 9g3, on the 4 game connections the owner captured, two by hand on a character of level 1,
two through D3B on a character of level 92, which took the portal three times. Builds D248 to D253.*

- **Incarnam left by the bot**: walked to the portal, talked to it, moved to Astrub, the client closing the question
  without reply, the bot walking on 0.5 s after the map's information, not paused; the bot also went back into Incarnam
  by Astrub's two doors.
- **Not measured in game**: the interactive transitions of skills other than the doors' (D248), none on the journeys
  captured; a teleport refused from Incarnam, none asked; big groups (D249); which of Sarakech's insides holds the bank
  (D251); the client's inactivity popup without a pause (D252).

## D255: A change of the fighter's states kept in the turn's search

*2026-11-01, step 9, session 9g4, what the owner saw in 9g2 (D250): the AI puts the Zobal's masks on at its turn's end
for the next turn rather than first to cast their spells, its earth Zobal casting Furia 3 times in 11 turns. Measured on
the 94 game connections of 3.7.12.12, 323 turns replayed, 36 of a Zobal in three captures, 12 of them the AI's own of 9g2.
Builds D201, D228.*

- **The Zobal's spells, as the pack writes them**: a mask 1 AP; Furia, earth, 3 AP, three a turn, at range 3, in
  *Psychopathe* (99); Catalepsie, an earth steal, and Plastron, a shield, in *Intrépide* (98); Martelo and Apathie, earth,
  in *Pleutre* (100). An earth Zobal has an earth spell in each mask: Furia's count alone says little.
- **Measured** with `scripts/fights.cs --turns`, which now tells a change of the character's states by its place among
  the turn's casts: the plans put the mask on last 14 times, first 13, between two casts 5. The same turns searched six
  times as wide, 48 sequences a step for 20 s, found a plan the judge rates higher in 18 of the 30 turns of two of the
  captures, nearly always the mask first then its spells, such as the psychopath's mask and two Furias where the plan
  struck its weapon alone.
- **Why**: each step of the beam keeps its best sequences only (D201). A mask deals nothing as it is put on, and the
  spells it opens count only a cast later, so that every sequence starting with it fell out of the beam at the first step,
  behind the blows of the mask worn; once those were cast, the mask came last with the AP left.
- **Made**: each step keeps, beside its best sequences, the best one of each set of states the fighter playing holds that
  they leave out (`FightTactics.WithEachStates`), through the effects, whatever the breed. On the Zobal's 36 turns, the
  plans are judged 29,149 against 26,260, the search six times as wide 30,981; the mask is put on first 26 times, last 5.
  On the 323 turns: the plans judged 490,979 against 485,081, the damage foreseen over two turns 124,523 against 125,872,
  the heals and shields 19,239 against 15,236; a search 90 ms halfway and 256 ms at 90% against 80 and 216, 538 ms at
  most; benchmarked, in its process, a turn planned against four enemies in 2.83 ms and 4.81 MB against 2.85 ms and
  4.79 MB. The earth Zobal's plans put the brave mask on first more often and cast Catalepsie and Plastron, which the
  judge rates higher: Furia is planned 18 times against 31.
- **Not made**:
  - The sequences told apart by the fighters' states too (`Signature`), as two masks of one cost leave the fight alike
    but for them: measured, no plan changed.
  - Measured in game: the owner could not capture in this session.
- **Seen, left**: on a fight made for a test, the judge rates killing the last enemy, one that strikes nothing, under
  leaving it a few points of life with the next turn's damage to come: its being alive weighs its blows, none. An enemy
  that strikes, as every monster does, is not so.
- **Rejected.**
  - A wider beam for every search: 6 times as wide takes seconds, and the turn's 30 s are shared by the searches after
    each answer of the server (D201).
  - A case of the Zobal's own, its masks first: every breed's states work through the effects (D223, D224).

## D256: 9g4 measured, without the game, and what the owner met

*2026-11-01, step 9, session 9g4, the owner's words in game during the session, measured on the captures of 9g2 and 9g3
with measuring tools never committed that print counts. Builds D236, D241, D148, D145, D250, D254.*

- **Not measured in game**, the owner unable to capture: what D254 left, the interactive transitions other than doors, a
  teleport refused, big groups, Sarakech's bank and the inactivity popup; and the Zobal's masks of D255.
- **The hunt, slow where monsters abound**, the owner's words: of the 36 attacks of D3B's after a fight of its own, most
  came 2 to 15 s after it; four after 34, 49, 95 and 95 s, the bot crossing 12, 9, 37 and 21 maps, most holding groups
  the settings of "Combat automatique" allow and the judge beats; one after 245 s, as the owner made a profile. The
  fighting activity looks for a group as its step starts on a map of its zone only, then journeys to the nearest map of
  the tour still to go through: the maps a journey crosses, those already gone through, isolated ones and others'
  sub-areas, are never looked at, so that the bot walks to and fro past groups it would attack to reach the tour's last
  maps. The settings of the captures are not known; the owner's own case was not captured.
- **Healing without food**, the owner's words: with the share of life set and no item chosen, the bot should eat what the
  inventory holds. The pack knows an item's type, not its effects: of the types the profile offers as food, potions hold
  recall potions too, so that eating anything could teleport the character.
- **Healing, slow**, the owner's words: nearly whole, the bot does not go on. Read in the code: without food, it waits the
  time its whole life takes to come back, 5 minutes when the server gave no interval, without looking at its life again,
  and heals to its most.
- **Harvesting, resources left**, the owner's words: the bot waits for resources to grow back without counting from when
  they were harvested, and goes through maps leaving what could be harvested. Not read yet.
- **For the next session**, the owner's choice: these four, measured on captures of the owner's cases, then the
  measures in game of D254 and D255.

## D257: The food the inventory holds eaten when the profile chose none: pack format 8

*2026-11-02, step 9, session 9g5, the owner's words in game (D256) and choice among three ways, as the lead recommended
it; measured on the client's data and on the owner's capture of a Xelor. Builds D148, D153, D156.*

- **Measured** on the client 3.7.12.12's items: effect 110, "Rend #1 à #2 points de vie", alone says that an item heals,
  on 294 items. The types the profile offers as food hold no recall potion, which is type 43, "Potion de
  téléportation", but type 12, "Potion", holds the potions that change a class, a sex or a name (854, 852, 850); some
  breads launch a fight (905) or give characteristics for good (606 to 611), some candies give items (193, 209), and an
  item may teleport (600, 826). An energy bread heals nothing, and eating its stack whole would have eaten it all.
- **Measured** on the owner's capture: a Xelor ended a fight at 87 % of its life, under its share of 95 %, with no food
  chosen, and waited for its life to regenerate, 98 s, with "Pain d'Incarnam" and "Carasau" in its bag; it also held a
  "Multygely", which teleports, and a "Bonbon Zaï", which gives items.
- **Made**: the pack, format 8, keeps on each item the life one use gives back, a least and a most, only when its use
  does nothing else: effect 110 and none but 139, energy, 981, bound to the character, and 983, exchangeable
  (`Item.HealMin`, `Item.HealMax`, `Item.Heals`): 211 items of 9 types, 178 of them a single value. With no food chosen,
  harvesting and fighting eat the stack that heals the least first, whole, as the server eats only what the life lacks
  (D156) (`Food.StackToEat`). Food chosen is eaten as before.
- **To publish**: the pack of 3.7.12.12 in format 8, on the owner's go; the app published before reads format 7 only.
- **Rejected.**
  - The types alone, bread, meat and fish, one item at a time: no release, but energy breads and breads launching a
    fight would have been eaten.
  - The effects the server gives each object of the inventory, which the readable protocol does not read: a mapping
    format and a release too, for data the client's tables hold.

## D258: A character with nothing to eat regenerates back to its share alone

*2026-11-02, step 9, session 9g5, the owner's words in game (D256) and choice, as the lead recommended it. Supersedes in
part D148: without food, the life no longer regenerates until it is whole.*

- **Measured** on the owner's captures: the server gave the regeneration's interval, 5 tenths of a second, after every
  fight of the game connections read, so that the bot reckoned its wait right; but it waited for its whole life, without
  looking at it again, 98 s for the Xelor of D257 at 87 %. A lost fight leaves the character at half its life.
- **Made**: without food, harvesting and fighting wait for the share they heal under alone, as after a lost fight (D198),
  looking at the life again each 10 seconds at least. Food still heals until the life is whole.
- **Rejected.** A second setting, "Heal up to": one more number for what the share already says.

## D259: A hunt's journey stops on the first map of its zone holding a group it attacks

*2026-11-02, step 9, session 9g5, the owner's words in game (D256) and choice among three, as the lead recommended it;
measured on the owner's capture of a Zobal of level 45 hunting. Builds D74, D241, D245.*

- **Measured**: 13 attacks of D3B's in sub-area 30, all accepted, 11 fights won in a row, 5.9 s halfway from a fight's end
  to the next attack; on the zone's maps, no group the settings allow and the judge beats was left. The slowness came
  from the journeys: after each lost fight, from Astrub's temple, the bot crossed 16 maps outside its zone, about 90 s,
  each holding groups of levels 11 to 20 it would have attacked. A journey looked at no map it crossed (D256).
- **Made**: `WorldTravel.TravelAsync` takes a predicate asked on each map a journey reaches, once its information came,
  never the map it starts from, and ends the journey there as arrived. The fighting activity stops on a map of its zone,
  gone through or not, holding a group the settings allow and the judge beats; the groups it leaves are kept per map, a
  group's identifier being its map's. The rest of a shortest path being the shortest from where it stops, the next
  journeys do not come back through the maps it left.
- **Rejected.**
  - Attacking on every map of the way: monsters far weaker than those the place was chosen for.
  - An option for the maps outside the zone: not asked for.

## D260: No walk ends on the cell the previous walk ended on, which the client does not follow

*2026-11-02, step 9, session 9g5, the owner's words in game: at times the bot stands on a map for 10 or 20 seconds, no one
knows why. Measured on the 17 game connections of 9g2 to 9g5. Supersedes D85 on its cause, not found then.*

- **Measured**: the client did not follow 45 of the 734 walks D3B sent outside fights. 36 of the 39 walks ending on the
  cell the character's previous walk ended on were not followed, even on another map, against 9 of the 695 others. On
  a journey straight on, each map is left by the same cell of its edge, such as 540 going south. On the Huppermage's
  capture, D3B sent the path from 302 to 397 four times on one map, never followed; the owner then sent it from the
  client, the same bytes, followed. Neither the time since the map's information, 0.5 s for both, the path's length or
  shape, the cells' data nor an actor in the way told them apart. The bot waited each walk's whole delay, up to 11 s,
  then walked the same path again: in a mine, over a minute on one map.
- **Made**: the state keeps the last cell of the character's last walk the server showed, whatever the map
  (`CharacterState.LastWalkEnd`). A map change leaves by another cell of its exits when there is one; a walk that must
  still end there goes in two, to the path's cell before its last, then the last step, or a step aside first when it has
  one step (`MapWalk`).
- **Not known**: why the client keeps that cell, and what may reset it; 3 such walks were followed.

## D261: 9g5 measured in game, and what it leaves

*2026-11-02, step 9, session 9g5, on the game connections the owner captured with the app: a Zobal of level 45 hunting,
a Xelor hunting, a Huppermage of level 21 harvesting. Builds D254, D255, D256.*

- **The Zobal's masks** (D255), with `scripts/fights.cs --turns`: 61 turns of 14 fights, 12 won. A mask put on first 12
  times, 11 of them before a spell it opens; put on last 22 times, 16 of them before the next turn's first cast asking
  it, the AP left spent on the next turn, a mask lasting until changed; Furia cast 18 times. Nothing to change.
- **Big groups** (D249): the hunt's first attack, before any fight, judged by levels alone (D241), was on a group of 8
  monsters, lost; the judge then turned away the groups of 3 to 8 monsters of level 30 for a character of 45, and the
  AI's groups were of 1 or 2.
- **Harvesting**: the miner's automatic place sent the Huppermage into a mine of rooms without ore, nothing harvested
  in 4 minutes; the alchemist harvested 65 nettles, leaving none on the maps it went through.
- **Not measured in game**: the interactive transitions other than doors, a teleport refused, Sarakech's bank and the
  inactivity popup, none on the journeys captured.

## D262: An element another harvested grows back from when a bot first saw it so, and harvesting measured in game

*2026-11-02, step 9, session 9g5, the owner's words in game (D256), measured on the owner's capture of a Xelor harvesting
iron for 16 minutes in Incarnam's mine, with every fix of the session. Builds D128, D145, D260.*

- **Measured**: 119 iron, the mine's seven rooms gone through by their doors; no map gone through or crossed left with
  iron the bot could harvest. Iron grew back 729 and 731 s after the bot's own harvests, and was still harvested 779 s
  after one, another player having maybe harvested it again; most of the iron seen harvested was not the bot's. Before
  the host knew iron's time, a tour that harvested nothing waited D128's minute, 62 s, five times; once it knew 729 s, the
  bot waited 101 s for an iron it had harvested 729 s before, and found it grown back.
- **Read in the code**: a tour reckoned when an element harvested by another, unseen, should grow back from the map's
  last sighting, so that each visit put it off again.
- **Made**: each element sighted keeps when a bot first saw it not offering its skill, of the sightings since it last did
  (`SightedElement.DisabledSince`, `HarvestSightings`), and the tour reckons from there; a harvest a bot saw still wins.
- **The walks** of D260, measured on the same capture: none of the 190 walks D3B sent ended where the previous one did,
  and the client followed 189; the last, back along the previous walk to the cell the character entered the room on,
  it did not, as 7 others of the 17 earlier game connections, 1 % of the walks, whose cause is not found.
- **A teleport refused** (D248), the capture's last request: from a room of the mine, the bot's teleport was refused
  with text 474 of the error kind, where Incarnam's maps gave text 416; the owner stopped the run 6 s later, so that
  the journey after it is not measured.
- **Risk**: an element harvested again unseen between two sightings grows back later than reckoned; its map is then
  gone through each tour, as before D145.

## D263: A group judged from the characteristics the server gives out of fights, before the first fight too

*2026-11-02, step 9, session 9g6, the owner's ask: the hunt's first fight, judged by levels alone (D241, D261), was on a
group of 8, lost. Measured on the 99 game connections of 3.7.12.12. Supersedes D241 on the character the judge imagines.*

- **Measured** (client.md): the characteristics the server gives out of fights equal those of the character's first turn
  in a fight, each one both give, 158 fights of 158, the life lacking aside; at the placement, whose fighter the judge
  took (`BotActor.LastFighter`), 163 fights, those it gives are equal too, but it lacks strength, intelligence, chance,
  agility, the damage bonus, the critical hits, the range, the power and the summons. So the judge imagined the
  character without its elements, from its second fight on, and by levels before.
- **Made**: the character's state keeps every characteristic the server gives, each its whole value
  (`CharacterState.Characteristics`, `CharacteristicTotals`, shared with the fight's state). `GroupJudge` imagines the
  character from them, at its life, AP and MP 1 and 23, its breed the one whose spells the server listed
  (`GroupJudge.Character`), no message the readable protocol names giving it out of fights; by levels only before the
  server gave them. `BotActor.LastFighter` is gone; `scripts/fights.cs --live` judges as the activity does.
- **Measured** with `--live`: no group judged by levels. D3B's 60 attacks: 57 judged winnable, all won; 3 turned away, 2
  won, and 9g5's group of 8, lost. The player's 96: 89 judged winnable, 79 won; 7 turned away, 5 won. The margin of
  D249, a quarter, kept.
- **Left**: in a fight, the server never gives the heals (49), the critical damage (86) nor the AP and MP removal (82,
  83), which out of fights it does; the fight's AI reads the first two from the fighter's own, zero. Not measured on what
  the AI foresees.
- **Rejected.**
  - The placement's fighter filled from the characteristics out of fights: two sources for what one gives whole.
  - The breed from the character's look or the list's unread fields: a mapping format for what the spells tell.

## D264: The harvest's automatic place counts no isolated map the tour leaves out

*2026-11-02, step 9, session 9g6, the owner's ask: the miner's automatic place led into a mine without ore (D261),
measured on the pack and the 222 graphics the owner's bots learned. Builds D138, D149, D166.*

- **Measured** (client.md): the Huppermage of 9g5, a miner of 4, was sent to the Souterrains, sub-area 29, 12
  undergrounds apart: the 22 maps where iron's graphic is drawn are all isolated, which a tour leaves out by default
  (D149), and its main part, the 6 maps it went through, holds no ore. The place counted them all, 19.8 against 14.4 for
  the Carrière d'Astrub.
- **Made**: `AutomaticPlace.Rank` takes whether the tour goes through the isolated maps, the profile's "Isolated maps";
  when not, a map isolated from its sub-area's main part counts for nothing. The harvest and the editor's preview pass it.
- **Measured**: a miner of 4 at level 21 goes to the Carrière d'Astrub, 46 maps; of 1 to sub-area 1034, where the bot
  found ore on 15 of the 16 maps it went through on 2026-10-29; the first ranking 380 ms, the graphics' index built, then
  2 ms.
- **Rejected.** The maps counted by part, the richest part taken: the tour goes through a sub-area's main part, and the
  isolated ones last, as D149 has it.

## D265: Every item's icon in the pack, at 32 pixels, the owner's choice

*2026-11-02, step 9, session 9g6, the owner's ask (D250) and choice of the size among three, as the lead recommended it.
Builds D135, D136, D247.*

- **Measured** (client.md): the pack's 21,554 items name 11,755 icons; the client's 1x bundle holds 11,751, three of them
  not of 64 pixels in BC7. Kept at 64, the pack would weigh 53.5 MB and the icons 192 MB once read; at 48, 43.8 MB and
  108; at 32, 26.9 and 48. The app draws an item at 16, 24 or 32 pixels.
- **Made**: the extractor keeps every item's icon, halved to 32 pixels, each pixel the mean of four weighed by their
  alpha, so that the transparent texels' colour does not darken the edges; an icon missing or of another size or format
  is counted and left out, but a harvest's, which fails the extraction as before. The items' table carries its size, so
  that the pack stays at format 8, which no release holds yet.
- **Measured**: 11,748 icons, the pack 26,870,528 bytes against 19,420,668, extracted in 34 to 38 s; the icons read in
  240 ms; two extractions alike; 96 icons looked at, upright, whole, their edges clean.
- **To publish**: the pack of 3.7.12.12 in format 8, on the owner's go (D257).
- **Rejected.**
  - 64 or 48 pixels: twice to four times the bytes for pictures drawn at 24.
  - Two tables, the harvest's and the loot's at 64: a size the app would draw no better.

## D266: 9g6 measured in game, the boat to Sarakech, and what it leaves

*2026-11-02, step 9, session 9g6, on the game connection the owner captured with the app built from the session's
branch, 26 minutes, a character of level 92 running three profiles the lead made for the measures. Builds D251, D253,
D262, D263, D264.*

- **The profiles**, added to the owner's database at the owner's ask, the database saved before: harvesting in the
  Routes Rocailleuses, whose maps 49 transitions of other skills than doors join; a hunt in the Dunes des ossements,
  banking its loot at 50 % of the pods; iron in Incarnam's mine, banking at 50 %. The lead advised loading the character
  over 50 %; what it carried was neither loot nor harvest, so that the hunt stopped once banked, "still full", three
  times, as D129 has it.
- **Sarakech**: the hunt failed as it started, from Incarnam's mine. Measured with the pathfinder on the capture: without
  Sarakech's zaap, no way led there. The owner took the boat by hand: NPC 3464 in the Port de Madrestam, question 25721,
  reply 30858, the character moved to Sarakech's port in the same millisecond, no kamas taken. **Made**: that way is one
  line of `NpcWays.Measured`; from the mine, a journey of 15 steps then.
- **Sarakech's bank** (D251): the bot talked to NPC 3597 outside, whose question offered no reply, then in the inside of
  12 NPCs, offering one reply, none of the bank's, each refused; the third inside, map 173937154, holds the banker, NPC
  3516, question 26002, reply 31177 opening the bank. D251 works; two talks lost, about 15 s.
- **The judge** (D263): two attacks on groups of 4 in the Dunes des ossements, each judged winnable, the group taking
  the character's life in under half the turns it needs, both lost, one after the other. On every capture, that share
  holds 14 groups, 4 lost.
- **Harvesting** in the Routes Rocailleuses, 12 minutes: 748 resources, a protector's fight won; the session's journeys
  took 116 changes of maps and 22 doors, none of the other skills' transitions. The mine's profile, full from that
  harvest, banked at La Cuirasse first, then harvested 47 iron. A teleport from the mine was accepted as the capture
  began, 1,250 kamas; none refused.
- **Not measured**: an aggression, none met; the transitions other than doors; the journey after a refused teleport;
  the inactivity popup, which no turn's end the client marked as its own showed.

## D267: After a lost fight, the hunt turns away a group no less risky, the owner's choice

*2026-11-02, step 9, session 9g6, chosen by the owner among two options, as the lead recommended it. Builds D198, D249,
D266.*

- **Measured** (D266): the two groups of 4 lost one after the other in the Dunes des ossements were each to take the
  character's life in 0.463 of the turns it needed to beat them, over the quarter D249 turns away. On every capture,
  the groups between a quarter and a half are 14, 4 lost.
- **Made**: the fighting activity keeps the odds of the group it attacks; a fight lost against it keeps, for the run,
  the greatest such share (`GroupJudge.Share`), and the judge turns away a group whose share is no greater
  (`GroupJudge.Winnable`), the quarter of D249 still standing. A fight it did not attack, a protector's or an
  aggression, or a group judged by levels, teaches nothing. On the capture, the second group would have been left.
- **Rejected.** The margin raised from a quarter to a half: it would also have left 10 of those 14 groups, all won,
  against the laxer judge the owner asked for (D249).

## D268: 9f2 as the owner asks it

*2026-11-02, step 9, session 9f2, the owner's words. Builds D223, D244, D263, D266.*

- **Asked**: summons, those the character plays and those that play alone, as their spells and variants say, measured
  on captures the owner makes, imagined in `D3B.Engine`, searched by the AI and measured with `scripts/fights.cs`; then
  what 9g6 left: the heals (49) and critical damage (86) the server never gives in a fight, measured on what the AI
  foresees, taken out of fights if it counts (D263); the protectors' count of a harvest's statistics, 0 though a
  protector fought (D266); with the owner in game if it comes: an aggression, stairs and portals, the journey after a
  refused teleport, the inactivity popup.
- **Added by the owner as the session ran**: Bubble-Bot, Bubble-Emu and BillBot read for ideas (D3). Bubble-Emu models
  summons whole, their characteristics, limit, turns and control; Bubble-Bot leaves its summons stubbed and casts none;
  BillBot casts one Sacrieur summon. Their formulas were measured on the captures before any was taken.
- **So**: the 100 game connections captured hold 57 summons, 20 of their turns the client played: the session measures
  and builds on them, and the captures the owner makes, an Osamodas's and "Maîtrise des invocations" on and off, measure
  it again.

## D269: Summons measured: characteristics, turns and control

*2026-11-02, step 9, session 9f2, on the 100 game connections of 3.7.12.12 and the pack of format 8 (client.md, "The game,
summoned").*

- **Characteristics**: a summon's life is its grade's, raised by a hundredth a level of a character summoning, plus its
  vitality and the share its grade's bonus names of its summoner's base life, characteristic 0, 50 and 5 a level for a
  character; strength, intelligence, chance, agility and wisdom the grade's so raised, plus the bonus's share of the
  summoner's; every other characteristic the grade's plus that share; AP and range the grade's, MP too but never below
  0; dodge a tenth of the agility plus the grade's, lock the same never below 0; the dodge of AP and MP removed a tenth
  of the wisdom plus the share of the summoner's. Of the 57: life, AP and MP 57, and 1,992 of 1,995 other
  characteristics, the 3 others a turret's damage bonus the server did not give. A monster's summons are not raised.
  Bubble-Emu's formula, read, adds the share; it raises no characteristic but the life.
- **Turns**: a summon plays right after its summoner, the latest first, from the round it comes (78 of 103 orders right
  after, the others behind later summons).
- **Control**: the server lets the client play a summon of 1011 or 2796, a slave, and those "Maîtrise des invocations"
  controls: the spell puts the character in state 2131 and gives control (2027) and state 2130 to its summons that the
  spell each casts as it comes, 18648, put in state 2132, 35 monsters, a Sadida's dolls among them. 20 turns of 6
  monsters. The client sends the character's requests for it, the server takes them as the summon's.
- **Whom enemies hit**: while a summon of the character's lived, 1,627 life went to summons and 2,295 to the character;
  a summon nearer the enemy drew 45 of 73 turns that hit.
- **Rejected.** A table of each breed's summons: the pack's effects and states say it (D169).

## D270: Summons in the AI: brought, judged and replying

*2026-11-02, step 9, session 9f2, measured with `scripts/fights.cs` on the 100 game connections, 387 turns of the
characters and 20 of controlled summons. Builds D201, D206, D210, D269. Supersedes in part D201 (a summon's life as an
ally's) and D195 (a summon of the grade alone).*

- **Brought**: `Summons.Of`, the fighter a summon brings, as D269 measured it; `Combatant.Grade`; `MonsterSpells`, a
  monster's levels at a grade. Of summons drawn at random, such as a Xélor's spell 373, 80 and 20 in a hundred, the
  likeliest alone comes, where both came.
- **Judged**: the judge starts with the summons its fighter's spells bring, so that one the search brings has its damage
  and threat (`FightJudge`); a summon's life is worth nothing of itself, its being alive one and a half of its turn's
  damage with its own AP, as an enemy's, and the blow it strikes next, before the enemies, weighed as the approach is
  (`Strike`); an enemy that would hit a summon hits it half the time, the ally it hurts most of the others the other
  half; a summon playing protects its summoner.
- **Replying**: the character's summons that play strike first in the reply, the latest first, as greedily as the
  enemies, on any fighter, then the enemies.
- **Measured**, the turns played against the AI's plans, before and after: summons planned 9 and 71, the players' 42;
  damage the plans foresee 86,641 and 77,887, with 1,610 the summons deal in the reply; over two turns 146,668 and 137,605,
  the turns played 120,561; the life the reply takes 16,336 and 15,507; kills 399 and 355. The 71: a Xélor's 373, 48, a
  Steamer's turrets, 17, decoys dealing nothing 6. Controlled summons' 20 turns: 20 casts planned dealing 532, 23 played
  dealing 542. Searches 92 ms halfway, 837 ms at most, against 86 and 645, each run beside another; a turn planned in
  2.79 ms and 4.9 MB, as in 9g4.
- **Rejected**, measured:
  - A summon's life as an ally's, at 0.3 and half of it alive: 369 summons planned, decoys first, the damage foreseen
    falling to 57,189.
  - Its being alive by its damage at 24 AP, the judge's table's end: a Xélor's three summons of one turn judged worth
    1,286, against 517 by their own AP.
  - Its life at a tenth: 201 summons, decoys first, 71,102 damage foreseen.
  - The enemy hitting the summon it hurts most and nothing else: the character spared whole by any decoy, against the
    41% of the life the captures saw summons take.
- **Left**: a summon replacing another (405, 2796), a double (180) and an illusion (1097) are not imagined; a bomb's
  explosion, the Roublard's, waits for the bombs; which summons the client will control is not foreseen, both kinds
  replying the same.

## D271: The bot plays the summons the client controls

*2026-11-02, step 9, session 9f2. Builds D202, D269.*

- **Made**: the fight's state lets the client play a summon of the character's whose turn the server lets the client
  play (`FightState.IsOwnTurn`); the fight is imagined from the summon's view, its spells its grade's, its cooldowns and
  casts its own (`ImaginedFights`), and `FightPlay` plays its turn with the requests it sends for the character, the
  summon's casts waited for. Before, the bot left such a turn to its 15 s.
- **Measured** on the 20 turns the player played: the AI plans 20 casts foreseen to deal 532, the player cast 23 dealing
  542 as the AI imagines them; not measured in game.

## D272: Heals and critical damage out of fights, not taken

*2026-11-02, step 9, session 9f2, on the 100 game connections. Settles D263's "left".*

- **Measured**: of the characters captured, the Xélor of levels 91 and 92 alone holds any heals (28) or critical damage
  (-3) out of fights. Its 501 casts foreseen with them: no damage nor heal otherwise than without; none of its casts a
  heal or a critical hit they change. `scripts/fights.cs` foresees the character's casts both ways.
- **So**: nothing taken, the fight's characteristics alone, until a capture shows a difference.

## D273: A fight begun between two steps is the activity's

*2026-11-02, step 9, session 9f2, the owner's ask: the protectors' count of D266's harvest was 0, a protector having
fought. Builds D144, D146.*

- **Measured** (client.md): the protector's fight of D266 began 0.7 s after the harvest ended, so once the step that
  harvested was over and before the next began: no behavior ran, no step was cut, and the activity never knew the fight,
  neither counting it nor answering it as its settings say, a lost one included.
- **Made**: a step that starts after a fight began since the run last knew of one is cut by it as one that began during
  a step (`ActivityRun`); a protector's is told by when the fight began, within a second of the use's end
  (`BotContext.FightBeganAt`), no more by when the activity hears of it.
- **Checked**: a test plays a fight begun between two steps, answered then recovered; without the change, the activity
  is told of nothing. Not measured in game.

## D274: 9f3 as the owner asks it

*2026-11-02, step 9, session 9f3, the owner's words. Builds D244, D268.*

- **Asked**: the summons measured in game on captures the owner makes, an Osamodas's and "Maîtrise des invocations" off
  then on, and fights the AI plays with `Fights:Alone`: the formula and the control on those breeds, the summons' turns
  the bot plays, how often the AI summons against what the owner does, with `scripts/fights.cs --turns` and `--live`;
  then the Roublard's bombs, their explosion, walls and limit, measured, imagined, searched by the AI and measured; with
  the owner in game if it comes: D273's protectors, an aggression, stairs and portals, the journey after a refused
  teleport, the inactivity popup.
- **Added by the owner as the session ran**:
  - Bubble-Bot and Bubble-Emu read for the fights' AI (D3): Bubble-Bot leaves the bombs out, every Roublard spell banned
    from its AI; Bubble-Emu sets a chain off depth first through the chain reaction spell's 1009, sums the chain's
    combos into one multiplier, rebuilds the walls after every effect, stops pushes on them and lends a wall's bombs'
    combo to the summoner as it strikes.
  - What every breed's AI must take in, each mechanism in its session: spells by states, by triggers and by conditions;
    the Eniripsa's paint, the Ouginak's prey and form, the Féca's glyphs, the Enutrof's and summons' procs, the Sram's
    traps, their networks and invisibility, the Forgelance's zones, the Zobal's masks, the Pandawa's tanking, the Sadida's
    infection, the Osamodas's summons, the Steamer's turrets and the spells tied to them, the Huppermage's runes, the
    Ecaflip's cards, the Roublard's bombs, the Eliotrope's portals and redirections, the Xélor's telefrags, dial and
    Synchro.
  - A mechanism is taken in with every way fighters come into it: pushes, pulls, teleports, swaps, walks, and what a
    summon such as a Roublabot does, never the mechanism's own effect alone (D277).
  - Every fight's map and placement cells kept, at once, in the database or a file (D275).
- **So**: the owner's summon captures were not made in the session, so the summons stay as D269 to D271 measured them;
  the bombs are measured on the 5 fights of a Roublard the 100 game connections hold.

## D275: Every fight's placement cells kept, with the map fought on and the one left

*2026-11-02, step 9, session 9f3, the owner's ask and columns, the database the lead's choice among the two the owner
named. Builds D88, D138, D145, D180.*

- **Measured** on the 100 game connections: as a fight begins, the server destroys the roleplay's context, creates the
  fight's and names the map fought on, before the fight's start (D88 saw it twice). Of the 163 fights both maps are known
  of, 107 were fought on another map than the one left, 56 on it; the 89 maps fought on are all the pack's and the
  atlas's, 145 in the sub-area of the map left; 32 of the 65 maps left led to more than one. The 163 fights gave 159 sets
  of placement cells; of the 35 maps fought on more than once, 2 gave one set every time. None of the 3,424 placement
  cells is flagged red or blue in the pack, whose flags stay Dofus 2's (D180): the server draws a fight's map and cells,
  and nothing in the pack tells a map fights may begin on from another.
- **Made**: `FightPlacements`, taught by `FightPlacementLearner` from every fight any bot begins, whoever starts it: the
  map left, the one the character was on as the roleplay's context was destroyed, its area, sub-area and coordinates from
  the atlas; the map fought on, the one the server names last before the cells; the first cells it gives once the fight
  starts, counted once a fight. Kept by `PlacementRecorder` in `d3b.db`, table `Placements`, migration `Placements`, in
  the owner's columns: `FromMapId`, `FromAreaId`, `FromSubAreaId`, `PosX`, `PosY`, `MapId`, `RedCells` (the
  challengers'), `BlueCells` (the defenders'), in ascending order, then how many fights began so, the first and the last
  time; one row a map left, map fought on and set of cells.
- **A lead, not taken yet**: Ankama's protocol (`vendor/dofus-unity-proto-main`) has a `FightMapInformationResponse`, a
  map, its fight map and its placement cells, which the client likely asks for when the player turns on the fight map's
  preview, never on in the captures. A capture with the preview on would tell whether it gives the map a fight from a
  map would be fought on, and its cells, before any fight. Astria, read for it, holds 5 maps and the client's red and blue
  flags only.
- **Rejected.**
  - One set a map, the latest: 159 sets for 163 fights.
  - The map fought on alone: the owner wants to know where fights come from, and 107 of 163 were fought elsewhere.
  - A JSON file beside the database: the host's learned facts live in the database (D138, D145), which the host already
    reads before it starts.

## D276: The Roublard's bombs, measured

*2026-11-02, step 9, session 9f3, on the 5 fights of a Roublard of level 45 in the 100 game connections of 3.7.12.12, 17
bombs, 6 Détonateurs, 12 explosions and 5 wall hits, with measuring tools never committed that print fighters by
aliases; and the pack of format 8. Builds D182, D227, D269.*

- **The pack**: 10 bombs keyed by monster, 4 a Roublard's (3112 Explobombe, 3113 Tornabombe, 3114 Bombe à Eau, 5161
  Sismobombe), each naming its explosion, chain reaction, instant spell and wall; their walls linear, 1 to 7 hops. A bomb
  takes a bomb's slot; the characters hold characteristic 93, *Nombre max d'invocations de bombes*, 3; 94 is *Bonus de
  combo de bombe*, which effect 1027 gives, the pack naming no characteristic for it.
- **Set off**: Détonateur raises the bomb's combo a level (spell 20497), gives its AP back (effect 120, 1 AP), then sets it
  off (1009); the bomb casts its explosion at its grade, then the bombs its chain reaction's 1009 reaches explode in turn,
  depth first: a circle of 2 for any bomb of its summoner's, a cross of 7 for those of its kind. Each explosion kills its
  bomb. The bombs set off as derived on every Détonateur but one, whose fight ended with the third bomb's last enemy dead.
- **Combo**: each bomb comes in state *Combo I* and rises a level as its summoner's turn begins and when a Détonateur
  reaches it, its buff of 1027 then giving 20 a level past the first. An explosion's damage, raised by the characteristics
  as any spell's, is raised again by the chain's combos summed: 51 and 39 announced against 25..28 and 19..21 without
  them, the chain's combos 60 and 40.
- **Walls**: on every cell between two bombs of a kind and a summoner in a line, as near as the hops allow, no bomb of
  that kind between; the server marked 12 such cells as bombs came and took 11 back as they went or moved, all as derived,
  3 of each a wall it built again whole. A fighter a wall came onto took its spell, cast by the summoner, 3 times; one a
  push left on a wall, 2 times, each push stopping on the wall's first cell; a bomb pushed went through. 38 walks crossed
  no wall. A wall's damage is raised by its bombs' combos: on the one hit of two bombs of unlike combos, 40 and 80, by
  their half, the sum and the larger foreseeing too much.
- **A bomb on a fighter**: thrown on a taken cell, no bomb comes, its caster casting the bomb's instant spell there.
- **The Roublabot's Poussette** (effect 783, *Pousse jusqu'à la case visée*): the first fighter in line from the caster
  pushed to the cell cast on, which D3B did not foresee.

## D277: The bombs in the AI: set off, walled, every move counted, judged

*2026-11-02, step 9, session 9f3, measured with `scripts/fights.cs` on the 100 game connections. Builds D201, D227, D270,
D276.*

- **Made**: `Bombs`, the pack's bombs, their chains, combos and walls; effect 1009 as `EffectKind.Detonate`, 1027 a
  boost of characteristic 94, 120 a boost of AP, 783 `EffectKind.PushToCell`; `EffectFormulas.Dealt` raised by
  characteristic 94. `Imagination` sets a bomb off with its chain, their combos summed; casts the instant spell of a bomb
  thrown on a fighter; stops a push on a wall; pushes the first fighter in line to the cell of a 783; and, after every
  cast and walk, casts a wall's spell on each fighter left on a wall, whether the wall came or the fighter did, raised by
  its bombs' combos halved. `CastRules` gives bombs their own slots, characteristic 93. The judge weighs a bomb alive by
  what its explosion would take off the enemies about it, its combo a level up, as the approach weighs the next turn's
  blows; the search tries a bomb on an enemy's cell too.
- **The principle, the owner's**: a mechanism is taken in through every move that brings fighters into it, a push, a
  pull, a push to a cell, a teleport, a swap, a walk, a bomb moved, a summon's spells such as a Roublabot's; the walls
  read the fighters as every action leaves them, so that the next mechanism's moves count without more.
- **Measured**, against the session's start:
  - the Détonateurs' targets: 3 killed as imagined, 5 within a tenth, 450 life imagined against 434 announced;
  - the wall hits: the 5 within a tenth or killed as imagined; the Roublabot's push into a wall, 38 announced, imagined
    0 before, killed now;
  - the Roublard's 32 turns: the damage the AI foresees of the turns played 3,012 against 2,499, 3,507 announced; its
    plans' 5,273 against 4,466, over two turns 9,034 against 8,288; bombs planned 31, played 17; Détonateurs planned 8,
    played 5;
  - the 387 turns of every capture: nothing changed but the Roublard's spells, cast by cast; searches 104 ms halfway, 294
    ms at 90% and 666 ms at most, against 85, 285 and 511 at the session's start, each run alone.
- **Left**: a wall struck at the start of a turn and walks across a wall, not measured, not imagined, nor the bombs' own
  starting spell, which raises the next bomb's AP by 1 (the server's state holds it once cast); the pushes the server
  stopped short where D3B foresaw them go on, such as a Pulsar's, D214's push model, every breed's.
- **Rejected.**
  - The combos counted bomb by bomb: 25..28 foreseen against 51.
  - A wall raised by its bombs' combos summed, as Bubble-Emu lends them: 31 foreseen against 21.

## D278: 9f4 as the owner asks it

*2026-11-02, step 9, session 9f4, the owner's words. Builds D274, D277.*

- **Asked**: the owner's new captures measured, an Osamodas with "Maîtrise des invocations" off then on, a Roublard whose
  fights the AI plays with `Fights:Alone`, the fight map's preview on, for the summons' formula and control, the bombs the
  bot plays and `FightMapInformationResponse`; what 9f3 left, walls at a turn's start, walks across them, the bombs'
  starting spell, the pushes the server stops short; then the Féca's glyphs, measured, imagined through every move
  (D277), searched by the AI and measured; with the owner in game if it comes, D273, an aggression, stairs and portals,
  the inactivity popup.
- **Added by the owner as the session ran**: the Roublard plays badly, building few walls and putting few monsters in
  them, and in its last turns passed its turn again and again doing nothing; then: before another breed, the Roublard
  measured working well.
- **So**: of the captures, only the Roublard's was made, 6 fights, 5 the AI's; the session measures and mends the
  Roublard first, glyphs coming through the marks every breed shares, and leaves the Féca's glyphs, the Osamodas and the
  preview to captures the owner makes.

## D279: A summon's spells kept apart, and the marks read: mapping format 15

*2026-11-02, step 9, session 9f4. Supersedes in part D271 (no list naming a summon) and D225 (mapping format 13 then 14).*

- **Measured**: `FightSpellsContextEvent`'s field 4 names the character whose client plays the fighter, its field 6, which
  the readable protocol lacked, the fighter whose spells it lists: a summon the client controls on 15 of the
  182 lists captured, each sent as the summon came. D3B took every list for the character's, so that from a
  Roublabot's coming its Roublard held the Roublabot's 4 spells: in the owner's sixth fight it passed 21 turns, a bomb of
  combo 360 beside the last enemy, at 53 life. Every turn after a controlled summon came since 9f2 was so.
- **Made**: field 4 is `character_id`, field 6 `fighter_id`; the fight's state takes a list as the character's when field 6
  names it (`FightEvents`). The marks, `FightMarkSet` (401, 400), `FightMarkStruck` (306, 307) and `FightMarkRemoved`
  (310), with `FightMark`, `FightMarkedCell` and `FightMarkType` (client.md, "The game, marked"), kept by the fight's state
  (`FightState.Marks`, `GroundMark`).
- **Mapping format 15**: 222 messages, 492 fields, 29 enums and 206 values audited, no failure, 14,687 bytes; the 101 game
  connections, 173,439 frames, decode through it, 94,517 understood whole, none misread. The release of 3.7.12.12 needs it,
  on the owner's go (D172).
- **`FightMapInformationResponse`**, in this build `jvk`, two `int64`, the placement cells and a third `int64`, which the
  client asks for with the empty `FightMapRequest`: not mapped, no capture holding it to tell its fields apart.

## D280: Glyphs, walls and the marks as a turn begins and ends

*2026-11-02, step 9, session 9f4, on the 101 game connections. Builds D195, D276, D277.*

- **Measured** (client.md): a glyph of 401 strikes a fighter whose turn begins in it, before its turn's own sequence, its
  spell cast by its author, its effects on that fighter alone and whole, with no zone's decrease; a glyph goes as its
  author's turn of the round its end names begins. A walk onto a glyph struck no one, 37 times. A bombs' wall strikes as a
  glyph does a fighter whose turn begins on it, 2 times. The one walk through a wall was its Roublard's, struck by
  nothing.
- **Made**: `Glyphs`, the marks the pack's effects set (401 as a turn begins, 402 as it ends, 1091 a glyph-aura, 400 a
  trap); `CellMark`, `CellMarkKind`, `ImaginedFight.Marks`, the server's glyphs and traps (`ImaginedFights.MarksOf`) and
  those the casts imagined set. `Imagination.TurnStarted` takes back the author's marks whose time is out after they
  strike, then strikes the fighter with the glyphs and bombs' walls on its cell; `TurnEnded` with the glyphs of 402; a
  glyph-aura strikes the fighters it covers as it comes and one a cast or a walk brings into it. The reply begins and ends
  each fighter's turn so, the fighter playing's next turn beginning so too; the judge counts the marks of a side on a
  fighter of the other as its turn will begin, as sure as a blow now on an enemy, feared on an ally.
- **Measured with `scripts/fights.cs`**, marks at a turn's start: 6 turns' starts, the marks struck as many as imagined
  every time, 12, the life 296 announced against 297 imagined, all 6 within a tenth.
- **Rejected**: the glyph's spell cast on its middle with its zone's decrease, as the references cast it (D3): 86
  foreseen against 120 on three rings.
- **Left**: the Féca's glyphs, which no capture holds, measured in game; a glyph-aura's buffs taken back as a fighter leaves
  it; traps and portals, the Sram's and the Eliotrope's sessions'.

## D281: The bombs' promise counted once, and a kill now worth more

*2026-11-02, step 9, session 9f4, measured on the owner's Roublard. Supersedes in part D277 (each bomb weighed alone).*

- **Measured**: in the owner's fifth fight, two bombs about the last enemy, at 51 life, each weighed its kill, so that
  keeping them was judged worth more than the Détonateur that won the fight, which the search left out of its finalists;
  in the sixth, once the Roublard held its spells again, the approach and the bombs each counted the last enemy's kill.
- **Made**: the bombs' explosions on an enemy are summed, with what the approach foresees of the fighter playing's next
  turn on it, and never take more than its life, its kill counted once, weighed as the approach is
  (`FightJudge.Approach`); the marks' sure blows come first.
- **Checked**: a test sets two bombs off to kill the last enemy, judged above keeping them; both of the owner's turns now
  plan the Détonateur.

## D282: A bomb's starting spell, and the buffs a fighter gave taken back as it dies

*2026-11-02, step 9, session 9f4. Settles D277's "left" on the starting spell.*

- **Measured**: every bomb casts its grade's starting spell, *Allumage*, as it comes, which gives its summoner through
  spell 20509 a buff of effect 296, its kind's spell 1 AP dearer; the server takes it back as the bomb dies, a chain's
  every bomb with it.
- **Made**: a summon the search brings casts its grade's starting spell; a spell modifier's effect is imagined, the spell
  it changes its first die and its value the effect's; each buff held names who gave it (`HeldBuff.CasterId`, the
  server's source for its own), and the buffs a fighter gave go as it dies (`Imagination`).
- **Measured with `scripts/fights.cs`**: on the Roublard's 74 turns, bombs planned 45 then 37, the second of a kind paying
  its AP.

## D283: Pushes and pulls as the server moves them

*2026-11-02, step 9, session 9f4, on the 101 game connections, the cell where each target ended against the one D3B
imagines. Settles D277's "left" on the pushes stopped short. Builds D214.*

- **Measured**: of the 154 pushes and 86 pulls whose target lived, a fighter out of line with what pushed or pulled it
  went along the axis it stood farthest along, across only when as far along both, where D3B went across always; a
  Bombarde's and an Espingole's pushes, along a line across the cast, went the way it was cast, from the caster to the
  cell cast on, 6 of 6; 4 characters a state keeps from being moved, and monsters the pack does not let be pushed, did not
  move; a Botte's push the mask only maybe selects, of another Roublard's bombs, moved no one, where D3B rounded its half
  to a cell.
- **Made**: `Imagination` slides along the farthest axis, the way of the cast for a line across it; no push or pull moves
  a fighter `CastRules.CanBePushed` refuses, nor one its mask only maybe selects.
- **Measured**: on the cell imagined, pushes 116 then 138 of 154, pulls 57 then 79 of 86, none past it now. "Stopped
  short" counts the cells the effect names, not those imagined: 78 pushes, most against what D3B already foresaw blocking.
- **Left**: 15 pushes stop short of the cell imagined, a Pulsar's 6 among them moving no monster the pack lets be pushed.

## D284: 9f4 measured with `scripts/fights.cs`

*2026-11-02, step 9, session 9f4, against `main` at the session's start, each run alone.*

- **The Roublard's 74 turns**, the owner's 4 captures of one: plans casting 66 then 83, empty 13 then 0; damage the plans
  foresee 6,910 then 8,669, over two turns 12,204 then 13,006; kills 40 then 77; the life the reply takes 680 then 399;
  Détonateurs planned 18 then 28, played 8; enemies on a wall as the turn ends, planned 6, played 2; searches 161 then 140
  ms halfway, 476 then 419 at most.
- **Every capture's 429 turns**, against `main`: plans empty 21 then 5; damage the plans foresee 80,532 then 82,027, over
  two turns 140,958 then 142,738; kills 371 then 408; the life the reply takes 15,777 then 15,054; searches 102 then 109
  ms halfway, 669 then 694 at most; the marks and the pushes as D280 and D283 say.
- **Not measured in game**: the owner's next fights of the Roublard with `Fights:Alone` measure it, before another breed,
  as the owner asks.

## D285: The owner's second Roublard capture: walls struck once, Botte harmless, the search kept

*2026-11-02, step 9, session 9f4, the owner's ask: the Roublard throws its bombs on the enemies instead of pushing and
pulling them into its walls. On the owner's capture of 4 fights the AI played, 19 turns, then the Roublard's 5 captures,
89 turns. Builds D277, D280.*

- **Measured**: of the casts with walls standing, D3B imagined 1,167 life lost against 811 announced: a monster a Botte
  moved from a wall's cell onto another wall's was struck by nothing, 3 times, where D3B struck it again for 96 to 111;
  and a Botte's pushes, "without damage" (effect 1103), hurt no one they stopped short, where D3B foresaw a push's damage.
  Both overvalued pushing a monster about the walls.
- **Made**: a fighter that stood on a wall's cell before a cast or a walk is not struck by a wall after it; effect 1103
  hurts no one it stops. Imagined then 788 against 811.
- **The bombs on the enemies**: replayed with these, the AI's plans on the owner's 19 turns throw a bomb on an enemy on 3
  turns, where it foresees more than any other way; with every bomb on an enemy refused, it plans the same on 14 turns,
  worse on those 2 of the 3 it threw so, and better on 3, a sequence its search had left out. Kept: 32 finalists
  and, beside the best sequences, the best of each layout of the enemies, judged the 89 turns' plans 99,849 against
  100,303, the searches 225 ms halfway against 139.
- **Measured** on the 89 turns: enemies on a wall as the turn ends, planned 8, played 4; wall cells as the turn ends,
  planned 15, played 27; Détonateurs planned 38, played 13.
- **Left**: a wall's spell on an ally, its masks' conditions on states, not measured; the search's misses, a wider search
  costing more than it finds; the Roublard measured in game again.

## D286: Walls and glyphs worth the rounds they strike, the owner's observation

*2026-11-02, step 9, session 9f4, the owner's words: walls pay more against big enemies. Builds D209, D280, D285.*

- **The owner**: against enemies of much life, a Roublard gains more by walling them than by throwing its bombs on them.
- **Why, in the AI's terms**: a fight against such enemies lasts several rounds, and a wall strikes an enemy on it at each
  of its turns' starts while its bombs' combos rise, a glyph for the rounds it lasts; the judge counts a mark's blow at the
  next turn's start alone and the bombs' explosion a level up, where a buff is weighed over the rounds the fight is
  foreseen to last (D209). Against small enemies a blow now kills, and the fight ends before a wall pays.
- **So**: the next session weighs a mark, a wall while its bombs live, a glyph for its rounds, over the rounds the fight
  is foreseen to last, as a buff is, the enemy kept there as likely as the measures say, and measures it on the Roublard's
  captures and in game against big enemies, before the Féca's glyphs, which it serves too.

## Open questions

Settled questions leave this table, and the decision that settles one says so.

| Id | Question | Settled in |
| --- | --- | --- |
| Q5 | Which fight formulas of Bubble emu (Dofus 2) still hold in Dofus 3? Measured in 9d for damage, steals, heals, critical hits and the zone's decrease (D195), in 9e2 for a critical hit's chance (D207) and a push's damage (D214), in 9e3 for a sword's blow (D222); left: shields, AP and MP removed and their dodge, erosion, the final damage's rounding, a weapon's critical bonus, power and mastery (D220). | The captures of D196 left (D214), then 9f |
| Q10 | Does every client build shuffle and renumber the game's fields, and how is a build's game mapped from the previous build's: messages by order and shape, fields of one type told apart, numbers rewritten on the wire (D54, D56)? | Step 3, the session after the next client update |
| Q12 | Which facts must the bot learn, beyond its state, for the criteria it cannot answer: quests, breed, alignment, achievements, the server's own codes; from which messages, and what does each code mean, measured (D71)? On doors, achievements would open the most, then `Sc` and `SC`, then quests (D76). | The large effort on criteria the owner noted, then as each step needs them |
| Q13 | What is a blueprint mod: who writes it, where it lives, what a node is over D3B's behaviors and state, how a graph runs as an activity, interrupted and resumed from the state, its settings, and which canvas in Avalonia (D117)? | Step 7 |
| Q14 | Does a client update renumber the maps' graphics, so that a skill learned on one (D138), which is never forgotten, would foretell another's maps wrongly: are the graphics learned kept as they are, checked, or forgotten at a new build? | The session after the next client update, with Q10 |
| Q16 | Which casts and walks does the server refuse that D3B's rules allow, or allow that they refuse: how it counts a corner beside what blocks, a proportional diagonal, the 11 zones no capture cast (D190, D191)? It refuses with an error text, 29 ms after the request (D203); which casts, but a Zobal's masks, is left. It allowed 11 casts the rules refuse, each a breed's mechanism: spells whose cost or range a gathered state changes, and summons (D214). The Zobal's 6 refused were the masks' states the bot kept, whose end it did not read (D224, D225). | Captures the owner makes, the AI sending its casts (D202), with those of D196; the breeds' mechanisms in 9f |
