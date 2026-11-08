# Fork architecture

This page describes the Linux fork's changes and boundaries. The upstream design and its decisions remain in
[`architecture.md`](architecture.md); the fork's integration strategy and known risks are in
[`UPSTREAM.md`](UPSTREAM.md) and [`RISKS.md`](RISKS.md).

## Project map

| Path | Responsibility in this fork |
| --- | --- |
| `src/D3B.Network/` | Socket relay, frame boundaries, client redirect files, and binary capture format/replay. |
| `src/D3B.Protocol/` | Readable connection and game messages, per-build schemas and mappings, pins, and translation between readable messages and obfuscated frames. |
| `src/D3B.Core/Modules/` | `IModule` and `BotBuilder`, the composition contract for host features. |
| `src/D3B.Bot/` | Runtime and one `BotActor` per selected character; network and protocol integration; character state; movement, harvesting, fighting, interactives, inventory, and activities. |
| `src/D3B.Gui/` | X11 windowing and capture, affine calibration, bounded template localization, input and safety contracts, and offline preview. |
| `src/D3B.Engine/` | I/O-free map and world pathfinding, fight rules, tactics, and effects. |
| `src/D3B.Data/` | Client build identity, versioned packs, maps, world graph, and game data readers. |
| `src/D3B.Storage/` | SQLite persistence through EF Core for accounts, profiles, runs, and related records. |
| `src/D3B.Host/` | Command-line host entry point. |
| `src/D3B.App/` | Avalonia desktop application; it is a separate executable project and hosts the bot runtime in its process. |
| `scripts/` | File-based inspection, extraction, schema, mapping, capture, client redirect, release, and offline preflight tools. Shared Unity-client readers live in `scripts/unity/`. |
| `tests/` | Automated tests for the compiled `src/` projects, plus the narrow linked-source exception described below. |
| `tests/D3B.Gui.Tests/` | Synthetic GUI contracts and Linux Xvfb tests against fake windows. |

The layers still follow the upstream module model: `src/D3B.Bot/DefaultModules.cs` composes features, and modules register
services through `src/D3B.Core/Modules/IModule.cs` and `src/D3B.Core/Modules/BotBuilder.cs`. The Linux changes do not
add a platform-specific host or a second runtime stack. Both host entry points validate `Gui` options at startup and
expose a `GuiHostCapability`; the hosts register no GUI executor, input, capture loop, or gameplay trigger.

## Caller-facing contracts

- **Installed client tools:** `scripts/unity/ClientInstall.cs` is the script-level view of one installation. The
  commands accept `--client` and pass the selected directory to `ClientInstall.Locate(...)`; its properties expose the
  build version, platform binary paths, metadata path, asset bundle paths, and a stable input fingerprint. It is an
  internal helper shared by file-based tools, not a runtime service in `src/`.
- **Relay:** `src/D3B.Network/Relay/IRelayInterceptor.cs` receives each accepted connection and can return
  `src/D3B.Network/Relay/IConnectionInterceptor.cs` to inspect complete frames.
  `src/D3B.Network/Relay/IRelayConnection.cs` exposes `SendToServerAsync(...)`, the narrow way to inject a D3B request
  between client frames. Without an interceptor, the relay forwards the byte streams unchanged.
- **Capture replay:** `src/D3B.Network/Captures/CaptureReader.cs` reads ordered records and
  `CaptureReplay.PlayAsync(...)` writes client and server records into pipes; D3B-injected records can be supplied to a
  separate pipe. Replay is deterministic and un-timed. It does not open sockets or contact the game server.
- **Protocol:** `src/D3B.Protocol/Mapping/ProtocolMatcher.cs` matches the readable `.proto` contracts to a build's
  extracted schema, while mapping audits check the result. `src/D3B.Protocol/Translation/ProtocolTranslation.cs`
  exposes decoded connection/game frames and encodes readable game requests. Runtime loading is through
  `src/D3B.Bot/Protocol/ProtocolModule.cs`, which requires the installed build's schema and mapping.
- **Bot control:** A feature implements `IModule.Build(BotBuilder)`. `src/D3B.Bot/Runtime/Bots.cs` observes game frames
  and exposes actors; `src/D3B.Bot/Runtime/BotActor.cs` provides `RunAsync(...)` and `Resume()`, while
  `src/D3B.Bot/Runtime/BotContext.cs` is passed to serialized behaviors. Activities are registered by modules under
  `src/D3B.Bot/Activities/`.

## Client platform and ABI seams

The seam is intentionally small and sits in `scripts/unity/ClientInstall.cs` and `ClientPlatform.cs`, with consumers in
`scripts/extract/WorldExtractor.cs` and `scripts/schema/ClientCode.cs`. The Windows client has `GameAssembly.dll` and
`UnityPlayer.dll` and uses Addressables platform `StandaloneWindows64`; the Linux client has `GameAssembly.so` and
`UnityPlayer.so` and uses `StandaloneLinux64`. Both layouts keep `Dofus_Data/StreamingAssets/version` and
`Dofus_Data/il2cpp_data/Metadata/global-metadata.dat`. The world bundle is selected from
`Dofus_Data/StreamingAssets/aa/<platform>/worldassets_assets_all.bundle`.

These path branches reflect two measured client layouts, including a successful extraction from the real Linux client
(17,722 maps and 31,329 transitions). They select files and bundle names; runtime protocol handling and bot behavior
remain shared. On Linux, the install must be named with `--client`; the Windows launcher default is not assumed to exist.
`scripts/schema/ClientCode.cs` opens the selected `.dll` or `.so` and metadata through Cpp2IL, with `--unity-version`
available when automatic Unity-version detection needs an override.

Schema reading has a separate ABI seam in `scripts/schema/CallRegisters.cs`, consumed by
`scripts/schema/IsilCalls.cs`. `ClientCode.Platform` flows through `scripts/schema/ProtocolReader.cs` and
`scripts/schema/WireEncodings.cs` to `IsilCalls`, which reads the argument and caller-saved registers for the selected
client. The Windows binary uses the Microsoft x64 ABI (`rcx`, `rdx`, `r8`, `r9` for arguments); the Linux binary uses
SysV x64 (`rdi`, `rsi`, `rdx`, `rcx`, `r8`, `r9`). Microsoft x64 is the Windows binaries' ABI based on upstream
behavior, not re-measured in Phase 0; SysV x64 is the ABI of the Linux binary measured in Phase 0. Only Linux build
`3.7.12.12` was exercised in Phase 0, and Windows schema generation remains unverified. Preserving each ABI's register
set is necessary to read protobuf writer calls and their tags correctly; the Windows register set is covered by
`tests/D3B.Scripts.Tests/Schema/CallRegistersTests.cs`.

`scripts/schema/WireEncodings.cs` now pairs writer calls with field tags independent of their order. It requires exact
counts and a unique assignment compatible with both field type and wire type, and fails when there is no unique match.
This keeps the schema reader strict while allowing the distinct call ABIs.

## Network to character state

1. `src/D3B.Network/Relay/` accepts the redirected client connection, frames the stream, and relays bytes to the
   upstream connection or game server. The connection-phase session rewrites the server selection so the game
   connection returns through D3B, then associates its ticket with the account.
2. `src/D3B.Bot/Network/RelayDecoder.cs` selects a connection-phase or game session. Game frames are decoded once by
   `ProtocolTranslation` and delivered to `IGameObserver` implementations through `GameSession`; game bytes continue
   to the other side unchanged.
3. `src/D3B.Bot/Runtime/Bots.cs` binds a successful character selection to a `CharacterKey` and creates or reuses that
   character's `BotActor` with an initial `CharacterState`.
4. Each actor serializes observed messages through its mailbox. `src/D3B.Bot/Characters/CharacterEvents.cs` applies
   readable client requests and server events to the prior snapshot, and the actor keeps the resulting state. Behaviors
   send readable requests through `GameSession`, which encodes and injects them through the relay.

## Diagnostics boundary

Saved captures are an offline diagnostic input. `scripts/capture.cs` describes their structure and its `--json` mode
reports capture and frame health, game-message counts, and schema/mapping compatibility when `--schema` and `--mapping`
are supplied. The report includes stable diagnostic codes and artifact SHA-256 values, but no payload values or bytes.
The diagnostics come from the script layer,
`src/D3B.App/ViewModels/Inspector/InspectorViewModel.cs` inspects them, and
`src/D3B.Network/Captures/CaptureReplay.cs` feeds recorded bytes into in-memory pipes for replay and tests; record timing
is not recreated. Live runtime diagnostics are separate: the relay can record connections when capture is enabled,
while `src/D3B.Bot/Network/GameSession.cs` and observers process real frames and log message names and lengths without
logging payload values by default. An offline replay does not establish Linux client redirect or real-client acceptance;
that acceptance remains MANUAL/SUPERVISED. The default relay configuration includes port 443, which requires privilege
on Linux.

`tests/D3B.Scripts.Tests/` is a scoped exception to D10's scripts-have-no-tests rule: it links the Windows/Linux
`scripts/unity/ClientInstall.cs` and `ClientPlatform.cs` seam, `scripts/schema/CallRegisters.cs`, the capture
diagnostic DTOs and reader in `scripts/capture/`, and the offline manifest logic in `scripts/preflight/`. These tests
exercise linked script sources without adding another CLI or protocol dependency to `D3B.Network`. The file-based
tools remain outside the compiled application projects; this exception does not establish a general scripts test
project.

## Deliberate non-goals and incomplete work

This fork has no `IClientEnvironment` framework, second protocol engine, Frida runtime, or Rust FFI layer. Its platform
seams cover the two measured client layouts and ABIs without adding a general environment abstraction.

Schema generation now passes for the Linux client build `3.7.12.12` (Unity `6000.3.16f1`, IL2CPP metadata v39), both
with explicit `--unity-version` and with automatic Unity-version detection. The generated schema is 170,287 bytes;
`connection.proto` contains 37 messages, 9 enums, and 92 fields, while `game.proto` contains 2,175 messages, 423 enums,
and 6,302 fields. The earlier `mew: field 1 is written 0 times` failure came from reading SysV x64 writer arguments as
Microsoft x64 registers; the ABI seam fixes that mismatch.

Remaining validation is bounded: only Linux build `3.7.12.12` has been measured, Windows schema generation has not been
rerun after the stricter writer matching, and real-client relay/redirect acceptance remains MANUAL/SUPERVISED. These
limits are above `src/D3B.Protocol/`; the runtime still has one protocol translation path.
