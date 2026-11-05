# P2K-2 diagnostic contract — protocol version 1

*2026-11-05. Normative contract between D3B (C#) and the Rust diagnostic engine. Any change to an enum value, a message
shape or a limit requires a new `protocolVersion`. The engine and the C# side reject a version they do not know.*

## 1. Principles

- JSON, UTF-8, one compact object per line (JSONL) for replay; length-prefixed JSON for IPC.
- Byte strings are lowercase hex, no `0x`, no base64.
- The schema is always sent in-band by D3B; the engine never reads `.binpb`, never touches the filesystem for protocol
  data, and never persists a frame.
- Output is redacted: no payload bytes, no string content, no numeric field values, no account data.
- Optional properties may be omitted when their value is null; a reader treats an absent optional property as null.
  Required booleans and strings are always present.
- Ordering is deterministic: schema messages sorted by `name`, fields by `number`; analyses in input order.

## 2. Replay (Mode A)

### 2.1 Process

```sh
rust/d3b-diagnostic/target/debug/d3b-diagnostic replay [--pretty]
```

Reads JSONL on stdin, writes JSONL on stdout. `--pretty` only affects whitespace. There are no path arguments.

- Exit `0`: every line was consumed and the summary written.
- Exit `2`: contract violation before any analysis (unknown version, malformed schema, schema fingerprint mismatch).
  One `error` line is written.
- Exit `3`: internal failure. One `error` line is written; no payload appears in it.

`stderr` carries counts and stable codes only, never frame bytes, field values, type URLs carrying personal data or
schema content.

### 2.2 Input lines, in order

1. Exactly one `hello`.
2. Exactly one `schema`.
3. Zero or more `frame`.
4. Exactly one `end`.

```json
{"kind":"hello","protocolVersion":1,"clientVersion":"3.7.13.17","schemaSha256":"<64 lowercase hex>"}
{"kind":"schema","messages":[...]}
{"kind":"frame","index":0,"direction":"server","typeUrl":"type.ankama.com/lok","carriedType":"lok","payloadHex":"089e08"}
{"kind":"end"}
```

- `index`: non-negative, strictly increasing, unique. The analysis echoes it.
- `direction`: `"client"`, `"server"` or `"d3b"`.
- `typeUrl`: the envelope `Any` type URL as observed, or `null` when the frame carries no `Any`.
- `carriedType`: the build token from the type URL, or `null`.
- `payloadHex`: the `Any` value bytes, or empty when the frame carries none. Maximum 1,048,576 bytes after decoding.
- `typeUrlOk` is computed by the engine: `typeUrl == "type.ankama.com/" + carriedType`, exact and case-sensitive.

The schema fingerprint must be exactly 64 lowercase hex characters. `clientVersion` is opaque to the engine and is
echoed in the summary.

### 2.3 Output lines, in order

1. One `hello`.
2. One `analysis` per input frame, same order.
3. One `summary`.
4. `error` only instead of the above on exit 2 or 3.

```json
{"kind":"hello","protocolVersion":1,"engine":"d3b-diagnostic","engineVersion":"0.1.0","snsCommit":"c780035c9020058a772ac8a2eef09555acd5a173"}
```

```json
{"kind":"analysis","index":0,"direction":"server","carriedType":"lok","typeUrlOk":true,
 "status":"VALID","message":"lok",
 "fields":[{"number":1,"name":"geei","declaredKind":"INT64","repeated":false,"packed":false,
            "occurrences":1,"wireTypes":["VARINT"],"observed":{"varintBytes":[3]},
            "problems":[]}],
 "problems":[]}
```

```json
{"kind":"summary","protocolVersion":1,"clientVersion":"3.7.13.17","schemaSha256":"<hex>",
 "frames":1,"statuses":{"VALID":1},"problems":{},"dropped":0,"coverage":"complete"}
```

### 2.4 Analysis object

| Field | Type | Meaning |
|---|---|---|
| `kind` | string | `"analysis"`. |
| `index` | integer | Echo of the input index. |
| `direction` | string | Echo. |
| `carriedType` | string or null | Echo. |
| `typeUrlOk` | boolean | Exact type-URL check; `false` when either side is null. |
| `message` | string or null | Schema message full name used, or null when unknown. |
| `status` | string | See below. |
| `fields` | array | Observed top-level fields, sorted by first occurrence. |
| `problems` | array | Frame-level problems, sorted by `code`, then `fieldPath`. |
| `detailTruncated` | boolean | Present and true when the 1 MiB output cap cut nested detail. |

`status` values:

- `VALID`: every byte walked, every tag names a field, declared and observed encodings agree, no problem.
- `SCHEMA_MISMATCH`: the payload walked to its end but at least one field-level problem exists.
- `MALFORMED`: a tag, varint or length could not be read.
- `TRUNCATED`: the payload ends inside a field (a subset of malformed, reported distinctly).
- `TRAILING_BYTES`: the last field ended after the payload end, or an inner length overruns its container.
- `UNKNOWN_TYPE`: `carriedType` is null or absent from the schema; `fields` is empty and `problems` holds
  `UNKNOWN_TYPE`. The engine still reports the payload length in `problems[].detail` as a decimal byte count only.
- `LIMIT_EXCEEDED`: a contract limit stopped the walk; `detailTruncated` may also be true.

### 2.5 Field object

| Field | Type | Meaning |
|---|---|---|
| `number` | integer | Protobuf field number, 1..536870911. |
| `name` | string or null | Schema field name; null for an unknown number. |
| `declaredKind` | string or null | One of the protobuf kind names (section 3.2); null when unknown. |
| `repeated` | boolean | Declared repetition; false when unknown. |
| `packed` | boolean | Effective packed encoding declared for the field; false when unknown. |
| `occurrences` | integer | Number of times the field appeared. |
| `wireTypes` | array of string | Distinct observed wire types, in first-seen order. |
| `observed` | object | Redacted shape, see 2.6. |
| `nested` | array or absent | For `MESSAGE` fields: recursive field arrays, at most 32 levels. |
| `problems` | array of string | Problem codes for this field, sorted. |

### 2.6 Observed shape (redacted)

Observed wire types are named `VARINT` (0), `I64` (1), `LEN` (2), `GROUP` (3 and 4, always reported with
`GROUP_UNSUPPORTED`), `I32` (5), and `UNKNOWN` for any other value.

Only structural facts. No value, no string content, no bytes.

| Key | Applies to | Meaning |
|---|---|---|
| `varintBytes` | VARINT | Sorted distinct encoded widths, 1..10, one entry per distinct occurrence width. |
| `fixedLength` | I64, I32 | Sorted distinct lengths observed (always 8 or 4). |
| `length` | LEN | Sorted distinct payload lengths, capped at 8 entries; if capped, `lengthCapped: true`. |
| `utf8` | LEN for `STRING` | `true` if every observed payload was valid UTF-8, `false` otherwise, null if no payload was a string candidate. |
| `fieldCount` | LEN for `MESSAGE` | Distinct nested field counts, capped at 8. |
| `zeroLength` | LEN | `true` if any occurrence had length 0. |
| `truncated` | any | `true` if an occurrence could not be fully read. |

`length` and `fieldCount` are lengths and counts, never content. A repeated field aggregates all occurrences into one
field object.

### 2.7 Problem codes

Stable, uppercase, snake-free:

| Code | Level | Meaning |
|---|---|---|
| `UNKNOWN_TYPE` | frame | No schema message for `carriedType`. |
| `UNKNOWN_FIELD_NUMBER` | field | The tag's number is absent from the message descriptor. |
| `WIRE_TYPE_MISMATCH` | field | Observed wire type cannot encode the declared kind. |
| `PACKED_NOT_ALLOWED` | field | A packed LEN arrived for a field the descriptor declares unpacked. |
| `INVALID_UTF8` | field | A `STRING` payload is not valid UTF-8. |
| `GROUP_UNSUPPORTED` | field | Wire type 3 or 4 observed; groups are refused. |
| `DEPTH_EXCEEDED` | frame | Nesting deeper than 32. |
| `LENGTH_OVERFLOW` | field | A length-delimited value overruns its container or the payload. |
| `VARINT_OVERFLOW` | field | A varint exceeds 10 bytes or 64 bits. |
| `TRUNCATED` | frame/field | The payload or a nested value ends early. |
| `MALFORMED` | frame | A tag, wire type or value could not be decoded. |
| `LIMIT_EXCEEDED` | frame | A size/count/output limit was hit. |
| `SCHEMA_MESSAGE_MISSING` | field | A `MESSAGE` field's `messageType` is not in the schema. |
| `SCHEMA_INVALID` | frame | The schema line is internally invalid (duplicate numbers, dangling references). |

Wire compatibility follows protobuf, with D3B's strict packed rule (P2K D307):

- VARINT encodes INT32/INT64/UINT32/UINT64/SINT32/SINT64/BOOL/ENUM.
- I32 encodes FIXED32/SFIXED32/FLOAT; I64 encodes FIXED64/SFIXED64/DOUBLE.
- LEN encodes STRING/BYTES/MESSAGE and packed repeated scalars.
- A repeated packable scalar may arrive packed or unpacked, whatever the descriptor's `packed` option says: protobuf
  wire compatibility requires a parser to accept both, and D3B's P2K `SchemaPayload` does (`APackedPayloadMayBePackedOrUnpacked`).
- A repeated packable scalar the descriptor declares unpacked must not arrive packed: that is `PACKED_NOT_ALLOWED`,
  D3B's stricter fail-closed rule (P2K D307). A non-packable kind (STRING, BYTES, MESSAGE) never accepts a packed value.
- Unknown fields are walked by their wire type, reported, and make the frame `SCHEMA_MISMATCH`.

### 2.8 Limits

| Limit | Value |
|---|---|
| Payload bytes per frame | 1,048,576 |
| Schema messages | 20,000 |
| Fields per message | 4,096 |
| Nesting depth | 32 |
| Distinct lengths/field counts kept per field | 8 |
| Output bytes per analysis | 1,048,576 |
| Replay frames per process | no hard limit; the summary reports the count |

Exceeding a limit yields `LIMIT_EXCEEDED` (or `detailTruncated` for output only), never a crash and never a silent
success.

### 2.9 Error lines

An error is `{"kind":"error","code":"<CODE>"}`. The engine may omit `detail`; when present it is redacted (counts and
codes only, never payload content). Exit `2` for a contract error, `3` for `INTERNAL_ERROR`. Error codes are a separate
vocabulary from analysis problem codes:

| Code | Meaning |
|---|---|
| `CONTRACT_INVALID` | The line sequence, a JSON envelope, a field type or an IPC frame violates this contract. |
| `VERSION_UNSUPPORTED` | `protocolVersion` is not 1, or the engine `hello` version is not 1. |
| `SCHEMA_FINGERPRINT_INVALID` | `schemaSha256` is not 64 lowercase hex characters, or it conflicts with a later echo. |
| `SCHEMA_INVALID` | The schema line is internally invalid. |
| `UNKNOWN_COMMAND` | The subcommand or an argument is unknown. |
| `INTERNAL_ERROR` | The engine failed outside the contract; exit 3. |
| `IO_ERROR` | `serve`: the socket read or write failed. |
| `SOCKET_FAILED` | `serve`: the socket could not be created, bound or listened on. |
| `UNSUPPORTED_PLATFORM` | `serve` on a platform without Unix domain sockets. |

The C# side parses only these values for an error line; an unknown error code is a protocol violation. The same
vocabulary is used in replay and IPC.

## 3. Schema line

```json
{"kind":"schema","messages":[
  {"name":"lok","mapEntry":false,"fields":[
    {"number":1,"name":"geei","kind":"INT64","repeated":false,"packed":false,
     "messageType":null,"enumType":null,"oneof":null,"hasPresence":true},
    {"number":2,"name":"geej","kind":"INT32","repeated":false,"packed":false,
     "messageType":null,"enumType":null,"oneof":null,"hasPresence":true}
  ]}
]}
```

### 3.1 Message object

| Field | Type | Meaning |
|---|---|---|
| `name` | string | Full name in the build's `FileDescriptorSet`, e.g. `lok`, `lok.sub`. |
| `mapEntry` | boolean | True for protobuf map-entry synthetic messages. |
| `fields` | array | Fields sorted by `number`; duplicate numbers are a schema error. |

### 3.2 Field object

| Field | Type | Meaning |
|---|---|---|
| `number` | integer | 1..536870911. |
| `name` | string | Obfuscated field name from the build. |
| `kind` | string | `DOUBLE`, `FLOAT`, `INT64`, `UINT64`, `INT32`, `FIXED64`, `FIXED32`, `BOOL`, `STRING`, `GROUP`, `MESSAGE`, `BYTES`, `UINT32`, `ENUM`, `SFIXED32`, `SFIXED64`, `SINT32`, `SINT64`. |
| `repeated` | boolean | Repetition from the descriptor. |
| `packed` | boolean | Effective packed encoding: true for a repeated packable scalar whose descriptor declares it packed; false for a singular, non-packable or explicitly unpacked field. |
| `messageType` | string or null | Full name for `MESSAGE`; null otherwise. |
| `enumType` | string or null | Full name for `ENUM`; null otherwise. |
| `oneof` | string or null | Real oneof name; null for a synthetic proto3-optional oneof. |
| `hasPresence` | boolean | Descriptor presence, informational only in v1. |

The C# exporter is the only producer. It emits every message of the build's `FileDescriptorSet`, including protobuf's
own messages that the build references (`google.protobuf.Any` and any other referenced well-known type) and map-entry
messages. A field's `messageType` must always name an exported message; a dangling reference is a schema error.
Message and field order must be stable so the
export is byte-for-byte reproducible; the schema fingerprint remains the SHA-256 of the original `.binpb`, not of the
JSON.

## 4. IPC (Mode B)

### 4.1 Process and socket

```sh
d3b-diagnostic serve --socket /run/user/<uid>/d3b/diagnostic.sock
```

The engine creates a Unix domain stream socket at the given path (creating no parent directory), accepts exactly one
concurrent client, and exits when the client closes or sends `bye`. The C# side sets the parent directory and socket
mode 0600. No TCP, no network, no pid file, no daemonization.

### 4.2 Framing

Each message is `u32` big-endian length followed by exactly that many UTF-8 JSON bytes. Maximum 1,048,576 bytes per
message. Zero length, an over-limit length, invalid UTF-8 or invalid JSON closes the connection with an `error` message
when possible.

### 4.3 Session

1. Client sends `hello` then `schema` (same objects as replay, plus `"kind"`).
2. Engine replies one `hello` (same as replay).
3. Client sends `frame` with `"sequence"` (non-negative, strictly increasing) instead of `index`; engine replies one
   `analysis` echoing `sequence`.
4. Client sends `bye`; engine replies `summary` and closes with exit 0.

A connection that sends frames before the handshake or a version the engine does not know receives `error` and is
closed. The engine processes frames in order and does not pipeline responses.

### 4.4 Client obligations (C#)

- The observer only enqueues. The queue is bounded (256 frames by default), drop-newest, and counts drops.
- A dedicated pump sends and receives; no relay thread waits for the sidecar.
- Connect timeout 2 s, handshake timeout 5 s, response timeout 10 s. A timeout or IO error disables the sidecar for
  the session, records the failure and the count of frames not analysed; the relay continues untouched.
- The client reports `dropped` and `coverage: "partial"` whenever frames were dropped or the sidecar failed. An
  incomplete observation is never presented as complete evidence.
- The client spawns the sidecar only when `Diagnostic:Enabled` is true; otherwise it has no process, socket or thread.

## 5. Resolver output

The resolver consumes analyses, the build schema, readable descriptors and reference corpora. It produces one
document per run:

```json
{
  "protocolVersion": 1,
  "clientVersion": "3.7.13.17",
  "schemaSha256": "<hex>",
  "observations": {"frames": 0, "coverage": "complete", "dropped": 0},
  "candidates": [
    {
      "token": "jpe",
      "direction": "client",
      "readable": "d3b.game.MapInformationRequest",
      "classification": "CONFLICT",
      "score": {"value": 0.0, "kind": "heuristic"},
      "evidence": [
        {"source": "schema", "build": "3.7.13.17", "verified": true,
         "detail": "field 1 is INT32"},
        {"source": "readable", "build": "3.7.13.17", "verified": true,
         "detail": "MapInformationRequest.map_id is INT64"}
      ],
      "contradictions": [
        {"code": "FIELD_TYPE_MISMATCH", "detail": "field 1: schema INT32 vs readable INT64"}
      ],
      "uncertainty": ["reference names jpe MapInformationRequest in another build"]
    }
  ],
  "summary": {"verified": 0, "candidate": 0, "conflict": 1, "unknown": 0}
}
```

### 5.1 Classification (normative)

- `CONFLICT`: at least one hard contradiction (field number/type/repetition/graph mismatch, direction contradiction,
  schema validity failure on the only observation, or sources naming different messages).
- `VERIFIED`: no contradiction; every field and nested field matches the readable descriptor; exactly one readable
  message matches unambiguously; the build is exact; at least one reproducible observation or synthetic fixture
  validates; and at least two independent evidence sources exist, one of them the build's schema or readable
  descriptor. An external reference from another build is never one of the two.
- `CANDIDATE`: structurally plausible but one or more of the above is missing or ambiguous.
- `UNKNOWN`: insufficient evidence to name any readable message.

`score` is ranking only. It never changes the classification and is never described as proof.

### 5.2 Evidence sources

`source` is one of `schema`, `readable`, `reference`, `observation`, `prior_mapping`. Each item carries `build`,
`verified` and a human-readable `detail`. Reference items from another build set `verified: false` and must carry the
build they came from. A report never contains a raw payload or a numeric field value.

### 5.3 Fixture proposal and promotion

For a `VERIFIED` candidate the resolver may propose a synthetic fixture: the schema message name, an expected engine
status (`VALID`), and the expected redacted field structure. It does not write a mapping. `PromotionGate` refuses any
candidate that is not `VERIFIED`, has a contradiction, lacks the fixture RED/GREEN pair, fails `ContractAudit`, or lacks
an explicit owner approval token. It never clears the P2K latch and never edits a production mapping.

## 6. Versioning

`protocolVersion` is `1`. Adding an optional output field is allowed only when the engine tolerates its absence; any
required field, enum value, limit change or semantic change requires version 2 and a matching C# change. The C# client
refuses an engine `hello` with a different version and disables diagnostics for the session.
