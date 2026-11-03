# P2A: `AuthenticationRequest` mapping

## Finding

The Linux schema and readable descriptor do not disagree. The missing pair was caused by two facts together: no capture was supplied to the matcher, and `AuthenticationRequest` has the same two-field structure as many unpaired game messages in this build. The matcher therefore had no structural basis for choosing one name.

The readable descriptor is `d3b.game.AuthenticationRequest` in `Game/authentication.proto`. It has singular `string` fields `ticket` at field 1 and `language` at field 2. The Linux schema's `.knc` has exactly those two string fields at numbers 1 and 2.

The no-capture diagnostic found 20 matcher-eligible game messages with the exact same field shape. Each scored 1.0 against `AuthenticationRequest`:

| Obfuscated candidate | Fields | Score | Row margin |
| --- | --- | ---: | ---: |
| `hio` | string #1, string #2 | 1.000 | 0.000 |
| `hwx` | string #1, string #2 | 1.000 | 0.000 |
| `hxf` | string #1, string #2 | 1.000 | 0.000 |
| `hym` | string #1, string #2 | 1.000 | 0.000 |
| `iab` | string #1, string #2 | 1.000 | 0.000 |
| `icy` | string #1, string #2 | 1.000 | 0.000 |
| `ief` | string #1, string #2 | 1.000 | 0.000 |
| `ift.ifr` | string #1, string #2 | 1.000 | 0.000 |
| `izn` | string #1, string #2 | 1.000 | 0.000 |
| `jls` | string #1, string #2 | 1.000 | 0.000 |
| `jms` | string #1, string #2 | 1.000 | 0.000 |
| `kfb` | string #1, string #2 | 1.000 | 0.000 |
| **`knc`** | string #1, string #2 | 1.000 | 0.000 |
| `ksp` | string #1, string #2 | 1.000 | 0.000 |
| `lfo` | string #1, string #2 | 1.000 | 0.000 |
| `lhk` | string #1, string #2 | 1.000 | 0.000 |
| `lpp` | string #1, string #2 | 1.000 | 0.000 |
| `lvo` | string #1, string #2 | 1.000 | 0.000 |
| `lyj` | string #1, string #2 | 1.000 | 0.000 |
| `mag` | string #1, string #2 | 1.000 | 0.000 |

`PinProposals` reported 26 candidates because it also counted six map-entry descriptors. Those entries are not eligible message candidates for `ProtocolMatcher`:

| Map entry counted by `PinProposals` |
| --- |
| `heo.hem.FmzwEntry` |
| `huu.hus.FpifEntry` |
| `iap.ian.FqdcEntry` |
| `kao.kaj.FyfcEntry` |
| `kct.kcr.FyowEntry` |
| `lef.led.GchxEntry` |

`StructuralScore.Message` gives one point for each compatible field and divides by the larger field count. Both sides have two fields, so each candidate scores `2 / 2 = 1.0`. The best row score and next score are both `1.0`, giving a margin of zero. `ProtocolMatcher` requires a score of at least 0.8 and a margin of at least 0.1 in both directions. Its candidate columns clear, but the readable row does not. With no capture anchor, no pin, and no conflict, `AuthenticationRequest` correctly remains unpaired. The diagnostic shows no schema difference or matcher defect.

Each eligible candidate's two obfuscated field names differ, but their matcher-visible shape is identical: singular optional strings at fields 1 and 2, with no presence, oneof, or field options.

## Evidence and provenance

The Linux schema at `/tmp/p011/schema/3.7.12.12/schema-1.binpb` is 170,287 bytes with SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`. The public release schema at `/tmp/p2a-evidence/release/schema-1.binpb` has the same size and hash; a byte comparison also matched.

The public release mapping `/tmp/p2a-evidence/release/mapping-15.binpb` is format 15 for client `3.7.12.12`, 14,687 bytes, SHA-256 `6ce5a2dc148db21ef91f3e1c5cfcf8eda99ecbe0e4e9e7c54cdfa9333af6a7f0`. Its readable digest is `4e36426ac4d31cd517b8cb57d4b66d184f2ec9eaa85ed4ffc1448fb2d7c4cda9`, equal to the local `ReadableProtocol.Digest`. It pairs `d3b.game.AuthenticationRequest` with `knc`, records `Capture` evidence, and pairs fields 1→1 and 2→2. Its capture's platform provenance is not recorded.

This evidence follows the existing decisions:

- [D52](decisions.md#d52-the-game-connection-is-matched-by-the-ticket-it-presents) describes the first game request presenting the ticket from server selection.
- [D54](decisions.md#d54-the-game-is-paired-by-pins-where-its-structure-is-silent-then-from-build-to-build) explains why structure alone leaves ambiguous messages to independent evidence and build pins.
- [D57](decisions.md#d57-the-games-anchors-and-the-messages-its-frames-carry) describes capture anchors from the first request's `Any` type URL and the requirement that its payload read as the named schema message.

The platform limit is exact: the Linux schema bytes are identical to the public release schema, but the release mapping does not record whether its capture came from Windows or Linux. No direct Linux client capture exists. The mapping identity therefore rests on the release mapping's capture evidence plus schema byte identity; it is not a claim of Linux live observation or acceptance.

## Minimal change

Added one entry immediately after `AuthenticationAcceptedEvent` in `src/D3B.Protocol/Pins/3.7.12.12.json`:

```json
{ "readable": "d3b.game.AuthenticationRequest", "obfuscated": "knc" }
```

The pin takes the counterpart and field numbers evidenced in the public release mapping against the byte-identical schema. It resolves this build's structural tie while leaving matcher thresholds, readable `.proto` files, and binary formats unchanged.

## TDD and synthetic mechanism coverage

The regression test `TheBuildPinsAuthenticationRequestToKnc` was added first and run alone against the pinned SDK. RED failed as expected: one test ran and failed because no committed pin matched the pair. After adding the pin, the same test was rerun and passed 1/1. The command and complete output are recorded in `/tmp/p2a-impl/tdd.log`.

The mechanism tests use the existing `Obfuscation` fixture and capture helpers:

- `ProtocolMatcherTests` adds synthetic same-shape decoys and proves an unpinned `AuthenticationRequest` stays unpaired without a conflict; a pin that contradicts a capture anchor produces a conflict.
- `CaptureAnchorsTests` uses a synthetic ticket value and language `fr`; a first client frame carrying `AuthenticationRequest` in an `Any` is measured and matched with `Capture` evidence, and the resulting complete mapping passes `ContractAudit`. Two synthetic connections whose first `Any` URLs disagree report a problem and omit that anchor.
- `ContractAuditTests` rejects a wrong pair whose field 1 is `int32`. A separate test documents that a wrong name with the exact same shape passes a structural audit; independent evidence is required to distinguish it.
- `ProtocolTranslationTests` refuses a mapping without `AuthenticationRequest` and accepts a complete mapping that encodes its request.
- `ProtocolSourceTests` refuses a mapping without the pair and loads a complete mapping offline. This tests protocol preparation only; it does not start the production host.

The protocol test project passed 88/88 tests. The `ProtocolSourceTests` class passed 8/8 tests. Final post-correction verification on 2026-11-03, using the pinned SDK, passed the full solution build with 0 warnings and 0 errors, the full suite with 1,243/1,243 tests passed, and `dotnet format D3B.slnx --verify-no-changes --no-restore`. An intermediate direct `D3B.Bot.Tests` run inside the restricted sandbox could not bind five synthetic local sockets; the final full suite passed when local test IPC and sockets were available. No production host or live relay was started.

## Offline mapping result

The mapping tool ran with `--without-captures` against the Linux schema and the committed pins. It exited 0, printed `Numbers: every readable field and value has its own number on the wire`, and wrote `/tmp/p2a-impl/data/3.7.12.12/mapping-15.binpb`.

| Check | Result |
| --- | --- |
| SHA-256 / size | `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` / 14,687 bytes |
| Format / client / tool | 15 / `3.7.12.12` / `0.0.0-alpha.0.8+0dec0dbe9d65dca9037ae9f216b5f180881be937` |
| Audit | 222 messages, 492 fields, 29 enums, 206 values; 0 failures, 2 notes, 100 partial |
| Translation | `ProtocolTranslation.Create` succeeded; audit passed |
| AuthenticationRequest | `d3b.game.AuthenticationRequest` → `knc`, evidence `Pin`, fields 1→1 and 2→2 |
| Release pair comparison | All 222 message pairs and 29 enum pairs match; message field pairs and enum value pairs also match |

The mapping is a temporary verification artifact and is not committed.

## Reproduction

Run the commands from the repository root. The release schema and mapping are public artifacts. The schema copied from
`/tmp/p011` is byte-identical to the downloaded release schema, so either copy can be used. The produced mapping and
synthetic capture stay under `/tmp` and are not committed. No real ticket or client data is included; the synthetic
generator uses only the literal value `synthetic`.

For the pinned SDK commands on a read-only `$HOME`, prepare writable CLI and NuGet state first. The offline package
cache is reused by the later commands:

```sh
mkdir -p /tmp/p2a-impl/xdg-data /tmp/p2a-impl/runtime /tmp/p2a-impl/dotnet-cli /tmp/p2a-impl/nuget /tmp/p2a-impl/emptyfeed /tmp/p2a-impl/artifacts
chmod 700 /tmp/p2a-impl/runtime
```

### Download and verify the release artifacts

```sh
mkdir -p /tmp/p2a-evidence/release
gh release download client-3.7.12.12 -R AerafalDev/D3B -p 'schema-1.binpb' -p 'schema-1.binpb.sha256' -p 'mapping-15.binpb' -p 'mapping-15.binpb.sha256' -D /tmp/p2a-evidence/release
(cd /tmp/p2a-evidence/release && sha256sum -c schema-1.binpb.sha256 mapping-15.binpb.sha256)
if test -f /tmp/p011/schema/3.7.12.12/schema-1.binpb; then cmp /tmp/p2a-evidence/release/schema-1.binpb /tmp/p011/schema/3.7.12.12/schema-1.binpb; fi
```

Expected: both checksum entries report `OK`; when the local schema exists, `cmp` exits 0. The release and local schema files both have SHA-256
`e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`; the release mapping has its own verified
SHA-256 `6ce5a2dc148db21ef91f3e1c5cfcf8eda99ecbe0e4e9e7c54cdfa9333af6a7f0`.

### Reproduce the pre-pin failure

This uses a temporary copy of the base commit's pins, so the committed pin remains in place:

```sh
mkdir -p /tmp/p2a-fix/base-pins /tmp/p2a-fix/base-data/3.7.12.12
git show 0dec0dbe9d65dca9037ae9f216b5f180881be937:src/D3B.Protocol/Pins/3.7.12.12.json > /tmp/p2a-fix/base-pins/3.7.12.12.json
cp /tmp/p2a-evidence/release/schema-1.binpb /tmp/p2a-fix/base-data/3.7.12.12/schema-1.binpb
env XDG_DATA_HOME=/tmp/p2a-impl/xdg-data XDG_RUNTIME_DIR=/tmp/p2a-impl/runtime DOTNET_CLI_HOME=/tmp/p2a-impl/dotnet-cli NUGET_PACKAGES=/tmp/p2a-impl/nuget DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2a-impl/artifacts --property:RestoreSources=/tmp/p2a-impl/emptyfeed --property:NuGetAudit=false --property:PublishAot=false --file scripts/mapping.cs -- --client-version 3.7.12.12 --data /tmp/p2a-fix/base-data --pins /tmp/p2a-fix/base-pins --without-captures
test ! -e /tmp/p2a-fix/base-data/3.7.12.12/mapping-15.binpb
```

Expected: the mapping command exits 1, reports 26 proposals for `d3b.game.AuthenticationRequest` and 1 audit failure
(`d3b.game.AuthenticationRequest has no counterpart`), and writes no mapping.

### Produce and hash the mapping with the fixed pins

The temporary data directory contains only the byte-verified schema; `--without-captures` makes the run independent of
captures.

```sh
mkdir -p /tmp/p2a-impl/data/3.7.12.12
cp /tmp/p2a-evidence/release/schema-1.binpb /tmp/p2a-impl/data/3.7.12.12/schema-1.binpb
env XDG_DATA_HOME=/tmp/p2a-impl/xdg-data XDG_RUNTIME_DIR=/tmp/p2a-impl/runtime DOTNET_CLI_HOME=/tmp/p2a-impl/dotnet-cli NUGET_PACKAGES=/tmp/p2a-impl/nuget DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2a-impl/artifacts --property:RestoreSources=/tmp/p2a-impl/emptyfeed --property:NuGetAudit=false --property:PublishAot=false --file scripts/mapping.cs -- --client-version 3.7.12.12 --data /tmp/p2a-impl/data --pins src/D3B.Protocol/Pins --without-captures
sha256sum /tmp/p2a-impl/data/3.7.12.12/mapping-15.binpb
```

Expected: exit 0, `Audit: ... 0 failures`, and `Numbers: every readable field and value has its own number on the wire`.
The mapping SHA-256 is
`c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d`.

### Read, audit, translate, and compare the mapping

Run these commands from the repository root. The file-based probe reads both mappings with `MappingFile.Read`, checks
the produced mapping with `ContractAudit.Audit`, calls `ProtocolTranslation.Create`, and compares sorted message and
enum pair signatures, including their field and enum value pairs:

```sh
mkdir -p /tmp/p2a-fix
REPO=$(pwd)
sed "s|@REPO@|$REPO|g" > /tmp/p2a-fix/MappingProbe.cs <<'EOF'
#!/usr/bin/env dotnet
#:project @REPO@/src/D3B.Protocol/D3B.Protocol.csproj

using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;

var schema = ProtocolSchema.Load(File.ReadAllBytes(args[0]));
var produced = MappingFile.Read(args[1]);
var release = MappingFile.Read(args[2]);
var audit = ContractAudit.Audit(ReadableProtocol.Files, schema, produced);
var translation = ProtocolTranslation.Create(schema, produced);
var authentication = produced.Messages.Single(pair => pair.Readable == "d3b.game.AuthenticationRequest");
var releaseAuthentication = release.Messages.Single(pair => pair.Readable == "d3b.game.AuthenticationRequest");
var messagePairsMatch = Signatures(produced.Messages, MessagePairSignature).SetEquals(Signatures(release.Messages, MessagePairSignature));
var enumPairsMatch = Signatures(produced.Enums, EnumPairSignature).SetEquals(Signatures(release.Enums, EnumPairSignature));

Console.WriteLine($"Client: {produced.ClientVersion}; format {produced.FormatVersion}; tool {produced.ToolVersion}");
Console.WriteLine($"Audit: {audit.Failures.Count} failures, {audit.Notes.Count} notes, {audit.Unread.Count} partial; {audit.Messages} messages, {audit.Fields} fields, {audit.Enums} enums, {audit.Values} values");
Console.WriteLine($"Translation: created; audit passed={translation.Audit.Passed}");
Console.WriteLine($"AuthenticationRequest: {authentication.Readable} -> {authentication.Obfuscated}; evidence={authentication.Evidence}; fields={FieldSignature(authentication)}");
Console.WriteLine($"Release AuthenticationRequest: {releaseAuthentication.Readable} -> {releaseAuthentication.Obfuscated}; evidence={releaseAuthentication.Evidence}; fields={FieldSignature(releaseAuthentication)}");
Console.WriteLine($"Release comparison: {produced.Messages.Count} message pairs={messagePairsMatch}, {produced.Enums.Count} enum pairs={enumPairsMatch}");
Console.WriteLine($"Pair fields and enum values: messages identical={messagePairsMatch}, enums identical={enumPairsMatch}");

static HashSet<string> Signatures<T>(IEnumerable<T> pairs, Func<T, string> signature)
{
    return pairs.Select(signature).ToHashSet(StringComparer.Ordinal);
}

static string MessagePairSignature(MessagePair pair)
{
    return string.Concat(pair.Readable, "->", pair.Obfuscated, "|", FieldSignature(pair));
}

static string FieldSignature(MessagePair pair)
{
    return string.Join(',', pair.Fields.OrderBy(field => field.Readable).Select(field => string.Concat(field.Readable, ":", field.Obfuscated)));
}

static string EnumPairSignature(EnumPair pair)
{
    return string.Concat(pair.Readable, "->", pair.Obfuscated, "|", string.Join(',', pair.Values.OrderBy(value => value.Readable).Select(value => string.Concat(value.Readable, ":", value.Obfuscated))));
}
EOF
/home/nill/.dotnet/dotnet run --file /tmp/p2a-fix/MappingProbe.cs -- /tmp/p2a-impl/data/3.7.12.12/schema-1.binpb /tmp/p2a-impl/data/3.7.12.12/mapping-15.binpb /tmp/p2a-evidence/release/mapping-15.binpb
```

Expected output includes:

```text
Audit: 0 failures, 2 notes, 100 partial; 222 messages, 492 fields, 29 enums, 206 values
Translation: created; audit passed=True
AuthenticationRequest: d3b.game.AuthenticationRequest -> knc; evidence=Pin; fields=1:1,2:2
Release AuthenticationRequest: d3b.game.AuthenticationRequest -> knc; evidence=Capture; fields=1:1,2:2
Release comparison: 222 message pairs=True, 29 enum pairs=True
Pair fields and enum values: messages identical=True, enums identical=True
```

### Contradictory evidence refusal

Run these commands from the repository root. The synthetic capture generator uses `CaptureHeader` and `CaptureWriter`
to write `/tmp/p2a-fix/refusal/captures/game/synthetic.d3bcap`. Its header says client `3.7.12.12`; it has three
synthetic client requests and three server events so the game root is unique. Each side's frames are varint-length-
prefixed as `CaptureFrames` expects. The first client request carries a structurally valid `AuthenticationRequest`
payload under `type.ankama.com/hio`; the ticket field contains only the literal `synthetic`. The remaining
request/event frames match the synthetic sequence used by `CaptureAnchorsTests`.

```sh
mkdir -p /tmp/p2a-fix/refusal/captures/game /tmp/p2a-fix/refusal/data/3.7.12.12 /tmp/p2a-fix/refusal/xdg-data /tmp/p2a-fix/refusal/runtime /tmp/p2a-fix/refusal/dotnet-cli /tmp/p2a-fix/refusal/artifacts
chmod 700 /tmp/p2a-fix/refusal/runtime
cp /tmp/p2a-evidence/release/schema-1.binpb /tmp/p2a-fix/refusal/data/3.7.12.12/schema-1.binpb
REPO=$(pwd)
sed "s|@REPO@|$REPO|g" > /tmp/p2a-fix/refusal/generate.cs <<'EOF'
#!/usr/bin/env dotnet
#:project @REPO@/src/D3B.Network/D3B.Network.csproj
#:project @REPO@/src/D3B.Protocol/D3B.Protocol.csproj

using System.Buffers;
using D3B.Network.Captures;
using D3B.Protocol.Game;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

var header = new CaptureHeader
{
    StartedAt = DateTimeOffset.Parse("2026-11-02T12:00:00Z"),
    ClientVersion = "3.7.12.12",
    Listener = "127.0.0.1:5555",
    ServerHost = "synthetic.invalid",
    Server = "192.0.2.1:5555",
};

static Any Carry(IMessage payload, string? typeName = null)
{
    return new Any
    {
        TypeUrl = "type.ankama.com/" + (typeName ?? payload.Descriptor.FullName),
        Value = payload.ToByteString(),
    };
}

static byte[] Frame(IMessage message)
{
    var bytes = message.ToByteArray();
    using var stream = new MemoryStream();
    var value = (uint)bytes.Length;
    while (value >= 0x80)
    {
        stream.WriteByte((byte)(value | 0x80));
        value >>= 7;
    }

    stream.WriteByte((byte)value);
    stream.Write(bytes);
    return stream.ToArray();
}

static byte[] Frames(params IMessage[] messages)
{
    using var stream = new MemoryStream();
    foreach (var message in messages)
        stream.Write(Frame(message));
    return stream.ToArray();
}

var client = Frames(
    new GameMessage { Request = new GameRequest { Id = -1, Content = Carry(new AuthenticationRequest { Ticket = "synthetic", Language = "fr" }, "hio") } },
    new GameMessage { Request = new GameRequest { Id = -1, Content = Carry(new CharacterListRequest()) } },
    new GameMessage { Request = new GameRequest { Id = -1, Content = Carry(new CharacterSelectionRequest { CharacterId = 7 }) } });
var server = Frames(
    new GameMessage { Event = new GameEvent { Content = Carry(new AuthenticationAcceptedEvent()) } },
    new GameMessage { Event = new GameEvent { Content = Carry(new CharacterListEvent { Characters = { new Character { Id = 7, Basics = new CharacterBasics { Level = 3, Name = "synthetic" } } } }) } },
    new GameMessage { Event = new GameEvent { Content = Carry(new MapCurrentEvent { MapId = 8 }) } });

var path = "/tmp/p2a-fix/refusal/captures/game/synthetic.d3bcap";
var (writer, _) = CaptureWriter.Create(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path), header, TimeProvider.System);
using (writer)
{
    writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(client));
    writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(server));
    writer.WriteClosed(CaptureRecordKind.ClientClosed);
    writer.WriteClosed(CaptureRecordKind.ServerClosed);
}

Console.WriteLine("Wrote one synthetic 3.7.12.12 game capture with hio as its first Any type URL; ticket value is synthetic.");
EOF
env XDG_DATA_HOME=/tmp/p2a-fix/refusal/xdg-data XDG_RUNTIME_DIR=/tmp/p2a-fix/refusal/runtime DOTNET_CLI_HOME=/tmp/p2a-fix/refusal/dotnet-cli NUGET_PACKAGES=/tmp/p2a-impl/nuget DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2a-fix/refusal/artifacts --property:RestoreSources=/tmp/p2a-impl/emptyfeed --property:NuGetAudit=false --property:PublishAot=false --file /tmp/p2a-fix/refusal/generate.cs
env XDG_DATA_HOME=/tmp/p2a-fix/refusal/xdg-data XDG_RUNTIME_DIR=/tmp/p2a-fix/refusal/runtime DOTNET_CLI_HOME=/tmp/p2a-fix/refusal/dotnet-cli NUGET_PACKAGES=/tmp/p2a-impl/nuget DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/nill/.dotnet/dotnet run --artifacts-path /tmp/p2a-fix/refusal/artifacts --property:RestoreSources=/tmp/p2a-impl/emptyfeed --property:NuGetAudit=false --property:PublishAot=false --file scripts/mapping.cs -- --client-version 3.7.12.12 --data /tmp/p2a-fix/refusal/data --pins src/D3B.Protocol/Pins --captures /tmp/p2a-fix/refusal/captures
test ! -e /tmp/p2a-fix/refusal/data/3.7.12.12/mapping-15.binpb
```

Expected: the mapping command exits 1 and prints a conflict naming `d3b.game.AuthenticationRequest` as `knc` by a pin
and `hio` by the captures; it does not write `mapping-15.binpb`. The observed output excerpt was:

```text
Captures: 0 of the connection phase and 1 of the game, of client 3.7.12.12; 0 skipped
conflict: d3b.game.AuthenticationRequest is knc by a pin, and hio by the captures.
Audit: 222 messages, 492 fields, 29 enums and 206 values checked; 0 failures, 2 notes, 100 partial messages
The mapping does not hold, and was not written: pin what the structure leaves open, or fix the readable .proto files.
```

The CLI refusal follows `MappingCommand.cs`: any matcher conflict marks the run failed, and failure returns 1 before
`MappingFile.Write`.

### Targeted and final verification

When `$HOME` is read-only, keep the CLI, NuGet, and runtime state under `/tmp`:

```sh
mkdir -p /tmp/p2a-impl/xdg-data /tmp/p2a-impl/runtime /tmp/p2a-impl/dotnet-cli /tmp/p2a-impl/nuget /tmp/p2a-impl/emptyfeed
chmod 700 /tmp/p2a-impl/runtime
export XDG_DATA_HOME=/tmp/p2a-impl/xdg-data
export XDG_RUNTIME_DIR=/tmp/p2a-impl/runtime
export DOTNET_CLI_HOME=/tmp/p2a-impl/dotnet-cli
export NUGET_PACKAGES=/tmp/p2a-impl/nuget
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
```

The following final post-correction commands were run on 2026-11-03 with the pinned SDK. They use the cached NuGet
packages and an empty restore feed, disable NuGet audit network access, and build serially without the shared compiler:

```sh
/home/nill/.dotnet/dotnet build D3B.slnx --maxcpucount:1 --property:UseSharedCompilation=false --property:RestoreSources=/tmp/p2a-impl/emptyfeed --property:NuGetAudit=false
/home/nill/.dotnet/dotnet test --no-build --max-parallel-test-modules 1
/home/nill/.dotnet/dotnet format D3B.slnx --verify-no-changes --no-restore
```

The final run passed all three commands: the build exited 0 with 0 warnings and 0 errors; the full suite exited 0 with
1,243 succeeded, 0 failed, and 0 skipped; and format exited 0 with empty stdout and stderr. The build writes its outputs
to the repository's gitignored `artifacts/` directory. The logs are `/tmp/p2a-final/build.stdout`,
`/tmp/p2a-final/full-test.stdout`, `/tmp/p2a-final/format.stdout`, and `/tmp/p2a-final/format.stderr`.

An intermediate correction-agent attempt in its restricted sandbox encountered environment-specific NamedPipe and
socket permission failures while starting tests and format. Those describe that intermediate sandbox only; the final
post-correction run above passed.

After verification, `git diff --check` exited 0 with no output. On the reviewed worktree, `git status --short` remained
limited to the intended documentation, pin, and tests; no `artifacts/` output appeared:

```text
 M docs/BUILD_IDENTITY.md
 M docs/LINUX_ACCEPTANCE.md
 M docs/RISKS.md
 M docs/decisions.md
 M src/D3B.Protocol/Pins/3.7.12.12.json
 M tests/D3B.Bot.Tests/Protocol/ProtocolSourceTests.cs
 M tests/D3B.Protocol.Tests/Mapping/CaptureAnchorsTests.cs
 M tests/D3B.Protocol.Tests/Mapping/ContractAuditTests.cs
 M tests/D3B.Protocol.Tests/Mapping/PinnedPairsFileTests.cs
 M tests/D3B.Protocol.Tests/Mapping/ProtocolMatcherTests.cs
 M tests/D3B.Protocol.Tests/Translation/ProtocolTranslationTests.cs
?? docs/P2_AUTH_MAPPING.md
```

## Build identity and remaining limits

| Identity | Value |
| --- | --- |
| Linux client | Version `3.7.12.12`; build date `2026-10-19T14:46:54+00:00`; Unity `6000.3.16f1`; IL2CPP metadata v39 |
| Linux schema / release schema | 170,287 bytes each; SHA-256 `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`; byte-identical |
| Build pins | 99 message pins, 0 enum pins; `3.7.12.12.json` SHA-256 `53a51f30cdb54d0122ad5add9ad47cfff1d31e90b386159283402ee42ceec018` |
| Readable protocol digest | `4e36426ac4d31cd517b8cb57d4b66d184f2ec9eaa85ed4ffc1448fb2d7c4cda9` |
| Public release mapping | Format 15; 14,687 bytes; SHA-256 `6ce5a2dc148db21ef91f3e1c5cfcf8eda99ecbe0e4e9e7c54cdfa9333af6a7f0`; capture platform unknown |
| Produced offline mapping | Format 15; 14,687 bytes; SHA-256 `c3ec03cef5fd07aca4d0de601f7444bd66258032c82a050067b719a0dfddea7d` |
| Windows provenance | Windows schema generation for this version was not verified; the release mapping's capture platform is not recorded |

No Linux live capture or client session was performed. No host, relay, or sniffer was started, and no client configuration was changed. The schema audit establishes structural agreement. The semantic `AuthenticationRequest` identity relies on upstream capture evidence and schema byte identity; same-shape alternatives cannot be distinguished by shape audit alone.

### Future Linux observation fallback — MANUAL/SUPERVISED, not executed

Use this only in a separately supervised session after the build identity and port plan are reviewed. Do not change client configuration. Do not use Frida, injection, TLS interception, or payload modification.

1. While the native Linux client is connected, identify its actual TCP connections and endpoints using `ss -ntp` and `lsof -nP -iTCP -sTCP:ESTABLISHED`. Match the process/PID to `Dofus.x64`, then record the observed local and peer addresses and ports. Do not assume port 5555. SniffSniffSquared's default filter is `tcp port 5555`; configure its filter from the endpoints just observed.
2. Before launching the passive libpcap observer, check its help and confirm its output and database locations. SniffSniffSquared does not decrypt TLS. It needs root or `cap_net_raw` plus `cap_net_admin` on Linux; keep the client and host running as the normal user, and grant capture privileges only to the capture process if the supervisor approves.
3. Create a private, permission-restricted ephemeral directory for the capture and database. SniffSniffSquared archives message bodies by default, including a possible login ticket, and does not redact tickets. Do not use a shared directory or publish either file. If the observed traffic is TLS, stop: this tool cannot expose the `Any` type URL through TLS.
4. During the supervised observation, collect only the first client game frame's final `Any` type URL segment and minimal frame metadata: connection index, direction, order, timestamp, observed endpoints, and whether the payload read structurally. Do not copy, print, log, or retain the `Any` value or ticket. Do not retain message bodies after extracting the required metadata.
5. Label the record precisely: **observation** is the type URL segment and frame metadata seen; **hypothesis** is that the first game request is `AuthenticationRequest`, based on D52/D57; **proof of the pair for that session** requires the observed segment, the expected first-request sequence, and a structural read against the reviewed schema to agree. If any element is missing, keep the result as a hypothesis. One session does not establish cross-platform identity.
6. Stop the observer, verify that the retained note contains only the approved metadata, then delete the capture, database, and temporary directory. Record deletion and the supervisor's outcome. Keep tickets, bodies, original client configuration, and credentials out of shared reports.

## Decision and follow-up

D287 records the pin and its provenance. P2B should establish Linux capture/platform provenance under the MANUAL/SUPERVISED procedure if required, resolve the actual client-facing port plan, and only then consider host/client acceptance. The port plan remains unresolved.
