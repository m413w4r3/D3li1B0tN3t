# Phase 0 report

D3B is a viable Linux base; no architectural rewrite is needed. The host, offline suite, Linux client detection, extraction, schema generation, and simulated relay checks work. Real-client compatibility is BLOCKED pending MANUAL/SUPERVISED Linux acceptance. Verified environment: Ubuntu 24.04, linux-x64, kernel 6.8.0-147-generic, runtime 10.0.14, SDK 10.0.403. System `dotnet` 10.0.114 fails the pinned SDK selection. The report describes the original Phase 0 snapshot `29b8fb1` (absent from this re-imported checkout); this checkout is `38db76b`. Upstream base `0a995ad8a99f4417c99f2cf5eafe46cf13eb709b` and remote `https://github.com/AerafalDev/D3B.git` are historical report facts. Current checkout topology is recorded in [UPSTREAM](UPSTREAM.md#checkout-reality-phase-1). License: MIT.

| Capacity | Status and evidence | Next |
|---|---|---|
| Build host Linux | PASS; Host and 20-project solution, 0 warnings/errors | keep pinned SDK |
| Tests réseau | PASS; network tests 112/112 | sequential full-suite runs |
| Détection client | PASS; Dofus 3.7.12.12, Unity 6000.3.16f1, metadata v39 | other builds |
| Extraction des données | PASS; 17,722 maps, 11,327 vertices, 30,909 edges, 31,329 transitions | other builds |
| Extraction du schéma | PASS; connection 37 messages; game 2,175 messages/6,302 fields | Windows unverified |
| Relais local simulé | PASS; loopback, framing/split-write, both-side close, upstream refusal, redirect-restore characterization; network 112/112 | supervised Linux client check |
| Compatibilité client réel | BLOCKED / MANUAL-SUPERVISED; no live session attempted | relay, redirect, restore proof |

## P0.1–P0.6

- **P0.1 PASS:** upstream/base and MIT reuse strategy documented; current remote topology and publication history are not established by the original report.
- **P0.2 PASS:** Linux build/test baseline recorded.
- **P0.3 PASS:** Linux install detection, version and fingerprint; 9 linked-script tests at P0.3. Two ABI tests added by P0.5/P0.11 bring the linked-script total to 11. Verified client files: `GameAssembly.so` 175,931,264 B; `UnityPlayer.so` 41,372,064 B; metadata 43,605,684 B.
- **P0.4 PASS:** Linux Addressables bundle selection; extraction completed in 61.53 s.
- **P0.5 PASS:** Cpp2IL preview `2022.1.0-pre-release.21` opens Linux IL2CPP and schema extraction passes with explicit or automatic Unity detection.
- **P0.6 PASS:** four relay/redirect characterization tests; `D3B.Network.Tests` 112/112. Linux real-client redirect remains unverified.

## Decisions and change inventory

Reuse-first; keep Linux path and ABI seams localized; no `IClientEnvironment` framework; allow only the narrow linked-script exception to D10. An earlier schema failure came from Windows-register assumptions (`rcx/rdx/r8/r9`) applied to SysV (`rdi/rsi/rdx/rcx/r8/r9`), not a protocol change; ABI-aware register handling resolved it, and strict matching rejects ambiguity. Cpp2IL Linux support works.

Reused paths: `src/D3B.Network/Relay/`, `src/D3B.Network/Framing/`, `src/D3B.Network/Captures/`, `src/D3B.Protocol/Mapping/`, `src/D3B.Protocol/Translation/`, `src/D3B.Protocol/Pins/`, `src/D3B.Bot/Runtime/`, `src/D3B.Bot/Activities/`, `src/D3B.Bot/Movement/`, `src/D3B.Bot/Harvesting/`, `src/D3B.Bot/Fighting/`, `src/D3B.Engine/World/`, `src/D3B.Engine/Fights/`, `src/D3B.Data/Packs/`, `src/D3B.Data/Maps/`, `src/D3B.Data/World/`, `src/D3B.Storage/`, `src/D3B.Host/Program.cs`, `tests/`.

Modified paths: `scripts/unity/ClientInstall.cs`, `scripts/extract/WorldExtractor.cs`, `scripts/schema/ClientCode.cs`, `scripts/schema/ProtocolReader.cs`, `scripts/schema/IsilCall.cs`, `scripts/schema/IsilCalls.cs`, `scripts/schema/SchemaCommand.cs`, `scripts/schema/WireEncodings.cs`, `scripts/schema.cs`, `scripts/client.cs`, `scripts/extract.cs`, `scripts/inspect.cs`, `scripts/mapping.cs`, `scripts/release.cs`. Entrypoints include `scripts/unity/ClientPlatform.cs`; `client.cs` and `inspect.cs` add the Data project reference.

New paths: `AGENTS.md`, `docs/UPSTREAM.md`, `docs/BASELINE.md`, `docs/TESTING.md`, `docs/RISKS.md`, `docs/ARCHITECTURE.md`, `docs/PHASE0.md`, `scripts/unity/ClientPlatform.cs`, `scripts/schema/CallRegisters.cs`, `tests/D3B.Scripts.Tests/D3B.Scripts.Tests.csproj`, `tests/D3B.Scripts.Tests/Unity/ClientInstallTests.cs`, `tests/D3B.Scripts.Tests/Schema/CallRegistersTests.cs`.

## Refuted hypotheses

- “Cpp2IL cannot process Linux/Unity 6.3”: REFUTED; pinned preview exits 0 and produces the schema.
- “The schema failure is protocol or field renumbering”: REFUTED; SysV x64 argument registers were misread as Windows registers.
- “Explicit `--client` is enough”: REFUTED; Linux `.so` discovery and ABI handling were also required.
- “Client redirect is Windows-only”: UNKNOWN / MANUAL; mechanism is file-based, but Linux acceptance is unverified.

## Checks recorded

`/home/nill/.dotnet/dotnet build src/D3B.Host/D3B.Host.csproj`: PASS, 0/0, 24.94 s. `/home/nill/.dotnet/dotnet build D3B.slnx`: PASS, 20 projects, 0/0, 22.66 s. `/home/nill/.dotnet/dotnet format --verify-no-changes`: PASS. All six script help commands exited 0:

```sh
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/d3b-p03-artifacts/inspect --file scripts/inspect.cs -- --help
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/d3b-p03-artifacts/extract --file scripts/extract.cs -- --help
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/d3b-p03-artifacts/schema --file scripts/schema.cs -- --help
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/d3b-p03-artifacts/mapping --file scripts/mapping.cs -- --help
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/d3b-p03-artifacts/release --file scripts/release.cs -- --help
/home/nill/.dotnet/dotnet run --artifacts-path /tmp/d3b-p03-artifacts/client --file scripts/client.cs -- --help
```

Base `dotnet test --nologo -v minimal`: 1,186/1,187 (Inspector ordering failure); `dotnet test --no-build`: 1,187/1,187. P0.8 fixed that test deterministically, test-only (20/20 agent; 10/10 parent). Default-parallel full run under 128 inotify instances: 1,197/1,202, five environment failures (ClientRedirect, HostRunner, StoragePreparation). Final `/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1`: 1,202/1,202, zero failed/skipped; count = 1,187 base + 4 P0.6 + 11 linked-script tests, including 2 P0.11 ABI tests.

## Risks, stop criteria, and P1 plan

See [RISKS](RISKS.md): live client acceptance; Windows schema unverified; Cpp2IL preview pin; privileged Linux port 443; captures may contain tickets/secrets; upstream divergence; inotify exhaustion. Stop before P1 if client identity is unpinned, mapping audit fails, extraction is incomplete, relay requires privileged-port assumptions, or Linux restore is unaccepted.

P1 is plan only: first slice uses versioned synthetic/sanitized fixtures and offline capture diagnostics reusing `CaptureReader`, `CaptureReplay`, and `TestRelay`; compare independently with SniffSniffSquared in the laboratory. Test fragmentation, multiple frames, partial/invalid frames, unknown messages, temporal order, identical replay, close, and annotations. Emit machine-readable and human diagnostics; exit when deterministic fixtures pass. Gate live protocol use behind supervised Linux relay/redirect acceptance with restore proof. P3–P5 navigation/harvest/combat are NOT STARTED.

P1 was implemented and is documented in [PHASE1](PHASE1.md).

## Artifacts and references

Build identity and the later offline mapping diagnostic are recorded in [BUILD_IDENTITY](BUILD_IDENTITY.md): the schema reached matching/audit, `AuthenticationRequest` remained unmatched, and no mapping was produced. This is partial diagnostic evidence, not mapping acceptance.

Pack: `/tmp/p04b/packs/3.7.12.12/data-8.pack`, format 8, 26,870,514 B, SHA-256 `fdd6e182a62308ef9d4d7d92a3a0e157c864d3c5784f85ebad64234bb5a1e175`. Schema: `schema-1.binpb`, 170,287 B, SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`; connection.proto 37 messages/9 enums/92 fields; game.proto 2,175 messages/423 enums/6,302 fields. [BASELINE](BASELINE.md) · [TESTING](TESTING.md) · [ARCHITECTURE](ARCHITECTURE.md) · [UPSTREAM](UPSTREAM.md). This report describes the original Phase 0 snapshot `29b8fb1` (`29b8fb130dd4715f33cfd7b9a9885fe09f79852c`), absent from this re-imported checkout at `38db76b`; see [UPSTREAM](UPSTREAM.md#checkout-reality-phase-1) for checkout facts.
