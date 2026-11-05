use std::io::Cursor;
use std::io::Write;
use std::process::{Command, Stdio};

use d3b_diagnostic::error::ReplayError;
use d3b_diagnostic::replay::{replay, ReplayOutput};
use d3b_diagnostic::schema::model::{Kind, Schema, MAX_FIELDS_PER_MESSAGE, MAX_SCHEMA_MESSAGES};
use d3b_diagnostic::schema::walker::{analyze_payload, MAX_DEPTH};
use d3b_diagnostic::wire::reader::{Reader, WireType};
use serde_json::{json, Value};

const FINGERPRINT: &str = "0000000000000000000000000000000000000000000000000000000000000000";

fn field(
    number: u64,
    name: &str,
    kind: &str,
    repeated: bool,
    packed: bool,
    message_type: Value,
) -> Value {
    json!({
        "number": number,
        "name": name,
        "kind": kind,
        "repeated": repeated,
        "packed": packed,
        "messageType": message_type,
        "enumType": null,
        "oneof": null,
        "hasPresence": true,
    })
}

fn message(name: &str, map_entry: bool, fields: Vec<Value>) -> Value {
    json!({"name": name, "mapEntry": map_entry, "fields": fields})
}

fn lok_schema() -> Value {
    json!({"kind":"schema","messages":[message(
        "lok",
        false,
        vec![
            field(1, "geei", "INT64", false, false, Value::Null),
            field(2, "geej", "INT32", false, false, Value::Null),
        ],
    )]})
}

fn line(value: &Value) -> String {
    serde_json::to_string(value).expect("test JSON serializes")
}

fn hello() -> Value {
    json!({
        "kind": "hello",
        "protocolVersion": 1,
        "clientVersion": "3.7.13.17",
        "schemaSha256": FINGERPRINT,
    })
}

fn frame(index: u64, carried_type: Value, type_url: Value, payload: &[u8]) -> Value {
    json!({
        "kind": "frame",
        "index": index,
        "direction": "server",
        "typeUrl": type_url,
        "carriedType": carried_type,
        "payloadHex": hex(payload),
    })
}

fn hex(bytes: &[u8]) -> String {
    const DIGITS: &[u8; 16] = b"0123456789abcdef";
    let mut result = String::with_capacity(bytes.len().saturating_mul(2));
    for byte in bytes {
        result.push(char::from(DIGITS[usize::from(byte >> 4)]));
        result.push(char::from(DIGITS[usize::from(byte & 0x0f)]));
    }
    result
}

fn corpus(schema: Value, frames: &[Value]) -> String {
    let mut lines = vec![line(&hello()), line(&schema)];
    lines.extend(frames.iter().map(line));
    lines.push(line(&json!({"kind":"end"})));
    format!("{}\n", lines.join("\n"))
}

fn run(input: &str) -> Result<ReplayOutput, ReplayError> {
    replay(&mut Cursor::new(input.as_bytes()), false)
}

fn analysis(output: &ReplayOutput, index: usize) -> Value {
    serde_json::from_slice(output.lines.get(index + 1).expect("analysis output exists"))
        .expect("analysis output is JSON")
}

fn has_problem(value: &Value, code: &str) -> bool {
    value
        .get("problems")
        .and_then(Value::as_array)
        .is_some_and(|problems| problems.iter().any(|problem| problem["code"] == code))
}

fn encode_length_field(number: u8, bytes: &[u8]) -> Vec<u8> {
    let mut result = vec![(number << 3) | 2, bytes.len() as u8];
    result.extend_from_slice(bytes);
    result
}

#[test]
fn valid_lok_payload_produces_two_fields() {
    let output = run(&corpus(
        lok_schema(),
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x08, 0x9e, 0x08, 0x10, 0x07],
        )],
    ))
    .expect("valid replay succeeds");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "VALID");
    assert_eq!(value["typeUrlOk"], true);
    assert_eq!(value["fields"].as_array().map(Vec::len), Some(2));
    assert_eq!(value["fields"][0]["name"], "geei");
    assert_eq!(value["fields"][1]["name"], "geej");
}

#[test]
fn omitted_nullable_schema_and_frame_properties_are_accepted() {
    let mut schema = lok_schema();
    for field in schema["messages"][0]["fields"]
        .as_array_mut()
        .expect("schema fields are an array")
    {
        let object = field.as_object_mut().expect("field is an object");
        object.remove("messageType");
        object.remove("enumType");
        object.remove("oneof");
    }

    let mut without_type_url = frame(0, json!("lok"), json!("type.ankama.com/lok"), &[0x08, 0x01]);
    without_type_url
        .as_object_mut()
        .expect("frame is an object")
        .remove("typeUrl");

    let mut without_carried_type =
        frame(1, json!("lok"), json!("type.ankama.com/lok"), &[0x08, 0x01]);
    without_carried_type
        .as_object_mut()
        .expect("frame is an object")
        .remove("carriedType");

    let output = run(&corpus(schema, &[without_type_url, without_carried_type]))
        .expect("omitted nullable properties are accepted");
    assert_eq!(analysis(&output, 0)["status"], "VALID");
    assert_eq!(analysis(&output, 0)["typeUrlOk"], false);
    assert_eq!(analysis(&output, 1)["status"], "UNKNOWN_TYPE");
    assert_eq!(analysis(&output, 1)["carriedType"], Value::Null);
}

#[test]
fn explicit_null_schema_properties_are_accepted() {
    let output = run(&corpus(
        lok_schema(),
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x08, 0x01],
        )],
    ))
    .expect("explicit null nullable properties are accepted");
    assert_eq!(analysis(&output, 0)["status"], "VALID");
}

#[test]
fn non_string_optional_schema_property_is_rejected() {
    let mut schema = lok_schema();
    schema["messages"][0]["fields"][0]["oneof"] = json!(7);
    let error = run(&corpus(schema, &[])).expect_err("optional string of wrong type is rejected");
    assert_eq!(error.exit_code(), 2);
    assert_eq!(error.code, "SCHEMA_INVALID");
}

#[test]
fn unknown_field_number_is_schema_mismatch() {
    let output = run(&corpus(
        lok_schema(),
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x18, 0x01],
        )],
    ))
    .expect("unknown fields are observations");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "SCHEMA_MISMATCH");
    assert!(has_problem(&value, "UNKNOWN_FIELD_NUMBER"));
    assert_eq!(value["fields"][0]["name"], Value::Null);
}

#[test]
fn wrong_wire_type_is_reported_and_walk_continues() {
    let output = run(&corpus(
        lok_schema(),
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x0a, 0x01, b'x', 0x10, 0x02],
        )],
    ))
    .expect("wire mismatches are observations");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "SCHEMA_MISMATCH");
    assert!(has_problem(&value, "WIRE_TYPE_MISMATCH"));
    assert_eq!(value["fields"].as_array().map(Vec::len), Some(2));
    assert_eq!(value["fields"][1]["number"], 2);
}

#[test]
fn truncated_varint_and_length_overrun_are_distinguished() {
    let output = run(&corpus(
        lok_schema(),
        &[
            frame(0, json!("lok"), json!("type.ankama.com/lok"), &[0x08, 0x80]),
            frame(
                1,
                json!("lok"),
                json!("type.ankama.com/lok"),
                &[0x12, 0x05, 0x41],
            ),
        ],
    ))
    .expect("truncated observations are analyses");
    let truncated = analysis(&output, 0);
    let overrun = analysis(&output, 1);
    assert_eq!(truncated["status"], "TRUNCATED");
    assert!(has_problem(&truncated, "TRUNCATED"));
    assert_eq!(overrun["status"], "TRAILING_BYTES");
    assert!(has_problem(&overrun, "LENGTH_OVERFLOW"));
}

#[test]
fn nested_messages_and_map_entries_are_walked() {
    let schema = json!({
        "kind":"schema",
        "messages":[
            message("parent", false, vec![
                field(1, "child", "MESSAGE", false, false, json!("child")),
                field(2, "entries", "MESSAGE", true, false, json!("entry")),
            ]),
            message("child", false, vec![field(1, "value", "INT32", false, false, Value::Null)]),
            message("entry", true, vec![
                field(1, "key", "STRING", false, false, Value::Null),
                field(2, "value", "INT32", false, false, Value::Null),
            ]),
        ],
    });
    let payload = [
        0x0a, 0x02, 0x08, 0x01, 0x12, 0x05, 0x0a, 0x01, b'k', 0x10, 0x03,
    ];
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("parent"),
            json!("type.ankama.com/parent"),
            &payload,
        )],
    ))
    .expect("nested messages are walked");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "VALID");
    assert_eq!(value["fields"][0]["nested"][0]["number"], 1);
    assert_eq!(
        value["fields"][1]["nested"].as_array().map(Vec::len),
        Some(2)
    );
}

#[test]
fn packed_repeated_scalar_is_accepted() {
    let schema = json!({
        "kind":"schema",
        "messages":[message("lok", false, vec![field(1, "values", "INT32", true, true, Value::Null)])],
    });
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x0a, 0x02, 0x01, 0x02],
        )],
    ))
    .expect("packed repeated scalar is valid");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "VALID");
    assert_eq!(value["fields"][0]["occurrences"], 1);
    assert_eq!(value["fields"][0]["observed"]["varintBytes"], json!([1]));
}

#[test]
fn packed_payload_on_declared_unpacked_field_is_rejected() {
    let schema = json!({
        "kind":"schema",
        "messages":[message("lok", false, vec![field(1, "values", "INT32", true, false, Value::Null)])],
    });
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x0a, 0x02, 0x01, 0x02],
        )],
    ))
    .expect("packed mismatch is an analysis");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "SCHEMA_MISMATCH");
    assert!(has_problem(&value, "PACKED_NOT_ALLOWED"));
}

#[test]
fn unpacked_scalar_on_declared_packed_field_is_accepted() {
    let schema = json!({
        "kind":"schema",
        "messages":[message("lok", false, vec![field(1, "values", "INT32", true, true, Value::Null)])],
    });
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x08, 0x01],
        )],
    ))
    .expect("unpacked encoding is valid for a declared-packed field");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "VALID");
    assert_eq!(value["fields"][0]["occurrences"], 1);
    assert_eq!(value["fields"][0]["problems"], json!([]));
    assert_eq!(value["problems"], json!([]));
}

#[test]
fn invalid_utf8_string_is_redacted_and_reported() {
    let schema = json!({
        "kind":"schema",
        "messages":[message("lok", false, vec![field(1, "label", "STRING", false, false, Value::Null)])],
    });
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x0a, 0x01, 0xff],
        )],
    ))
    .expect("invalid UTF-8 is analyzed");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "SCHEMA_MISMATCH");
    assert!(has_problem(&value, "INVALID_UTF8"));
    assert_eq!(value["fields"][0]["observed"]["utf8"], false);
}

#[test]
fn group_wire_type_is_unsupported_and_following_fields_are_read() {
    let output = run(&corpus(
        lok_schema(),
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x0b, 0x10, 0x01, 0x0c, 0x10, 0x02],
        )],
    ))
    .expect("groups are reported");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "SCHEMA_MISMATCH");
    assert!(has_problem(&value, "GROUP_UNSUPPORTED"));
    assert_eq!(value["fields"].as_array().map(Vec::len), Some(2));
}

#[test]
fn unknown_carried_type_reports_only_payload_length() {
    let payload = [0x0a, 0x04, b'P', b'I', b'I', b'I'];
    let payload_hex = hex(&payload);
    let text = String::from_utf8_lossy(&payload[2..]).to_string();
    let output = run(&corpus(
        lok_schema(),
        &[frame(
            0,
            json!("not-in-schema"),
            json!("type.ankama.com/not-in-schema"),
            &payload,
        )],
    ))
    .expect("unknown carried types are analyzed");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "UNKNOWN_TYPE");
    assert_eq!(value["fields"].as_array().map(Vec::len), Some(0));
    assert!(has_problem(&value, "UNKNOWN_TYPE"));
    let stdout = output
        .lines
        .iter()
        .flat_map(|line| line.iter().copied())
        .collect::<Vec<_>>();
    let stdout = String::from_utf8(stdout).expect("redacted output is UTF-8");
    assert!(!stdout.contains(&payload_hex));
    assert!(!stdout.contains(&text));
    assert_eq!(value["problems"][0]["detail"], payload.len().to_string());
}

#[test]
fn type_url_mismatch_and_null_are_false() {
    let output = run(&corpus(
        lok_schema(),
        &[
            frame(
                0,
                json!("lok"),
                json!("type.ankama.com/other"),
                &[0x08, 0x01],
            ),
            frame(1, Value::Null, Value::Null, &[0x08, 0x01]),
        ],
    ))
    .expect("URL mismatches do not prevent analysis");
    assert_eq!(analysis(&output, 0)["typeUrlOk"], false);
    let null = analysis(&output, 1);
    assert_eq!(null["typeUrlOk"], false);
    assert_eq!(null["status"], "UNKNOWN_TYPE");
}

#[test]
fn depth_limit_stops_recursive_walk() {
    let schema = json!({
        "kind":"schema",
        "messages":[message("node", false, vec![field(1, "next", "MESSAGE", false, false, json!("node"))])],
    });
    let mut payload = Vec::new();
    for _ in 0..=MAX_DEPTH {
        payload = encode_length_field(1, &payload);
    }
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("node"),
            json!("type.ankama.com/node"),
            &payload,
        )],
    ))
    .expect("depth limit is an analysis result");
    let value = analysis(&output, 0);
    assert_eq!(value["status"], "LIMIT_EXCEEDED");
    assert!(has_problem(&value, "DEPTH_EXCEEDED"));
    assert!(has_problem(&value, "LIMIT_EXCEEDED"));
}

#[test]
fn payload_size_limit_is_reported() {
    let schema_value = lok_schema();
    let schema = Schema::parse(&schema_value).expect("test schema is valid");
    let payload = vec![0; 1_048_577];
    let result = analyze_payload(&schema, schema.message("lok"), &payload);
    assert_eq!(result.status, "LIMIT_EXCEEDED");
    assert!(result
        .problems
        .iter()
        .any(|problem| problem["code"] == "LIMIT_EXCEEDED"));
}

#[test]
fn duplicate_field_number_is_schema_error() {
    let schema = json!({"kind":"schema","messages":[message("lok", false, vec![
        field(1, "one", "INT32", false, false, Value::Null),
        field(1, "two", "INT64", false, false, Value::Null),
    ])]});
    let error = run(&corpus(schema, &[])).expect_err("duplicate number is rejected");
    assert_eq!(error.exit_code(), 2);
    assert_eq!(error.code, "SCHEMA_INVALID");
}

#[test]
fn dangling_message_type_is_schema_error() {
    let schema = json!({"kind":"schema","messages":[message("lok", false, vec![
        field(1, "child", "MESSAGE", false, false, json!("missing")),
    ])]});
    let error = run(&corpus(schema, &[])).expect_err("dangling type is rejected");
    assert_eq!(error.exit_code(), 2);
    assert_eq!(error.code, "SCHEMA_INVALID");
}

#[test]
fn bad_kind_is_schema_error() {
    let schema = json!({"kind":"schema","messages":[message("lok", false, vec![
        field(1, "value", "F32", false, false, Value::Null),
    ])]});
    let error = run(&corpus(schema, &[])).expect_err("unknown kind is rejected");
    assert_eq!(error.exit_code(), 2);
    assert_eq!(error.code, "SCHEMA_INVALID");
}

#[test]
fn bad_fingerprint_is_contract_error() {
    let mut bad_hello = hello();
    bad_hello["schemaSha256"] = json!("abcdef");
    let input = format!(
        "{}\n{}\n{}\n",
        line(&bad_hello),
        line(&lok_schema()),
        line(&json!({"kind":"end"}))
    );
    let error = run(&input).expect_err("bad fingerprint is rejected");
    assert_eq!(error.exit_code(), 2);
    assert_eq!(error.code, "SCHEMA_FINGERPRINT_INVALID");
}

#[test]
fn frame_before_schema_is_contract_error() {
    let input = format!(
        "{}\n{}\n",
        line(&hello()),
        line(&frame(0, json!("lok"), json!("type.ankama.com/lok"), &[]))
    );
    let error = run(&input).expect_err("frame before schema is rejected");
    assert_eq!(error.exit_code(), 2);
}

#[test]
fn missing_end_is_contract_error() {
    let input = format!("{}\n{}\n", line(&hello()), line(&lok_schema()));
    let error = run(&input).expect_err("missing end is rejected");
    assert_eq!(error.exit_code(), 2);
}

#[test]
fn bad_version_is_contract_error() {
    let mut bad_hello = hello();
    bad_hello["protocolVersion"] = json!(2);
    let input = format!("{}\n{}\n", line(&bad_hello), line(&lok_schema()));
    let error = run(&input).expect_err("unknown version is rejected");
    assert_eq!(error.exit_code(), 2);
    assert_eq!(error.code, "VERSION_UNSUPPORTED");
}

#[test]
fn identical_input_produces_identical_output_bytes() {
    let input = corpus(
        lok_schema(),
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &[0x10, 0x02, 0x08, 0x01],
        )],
    );
    let first = run(&input).expect("first replay succeeds");
    let second = run(&input).expect("second replay succeeds");
    assert_eq!(first.lines, second.lines);
}

#[test]
fn output_never_contains_payload_hex_or_synthetic_text() {
    let secret = "SYNTHETIC_SECRET_VALUE";
    let schema = json!({"kind":"schema","messages":[message("lok", false, vec![
        field(1, "label", "STRING", false, false, Value::Null),
    ])]});
    let payload = encode_length_field(1, secret.as_bytes());
    let raw_hex = hex(&payload);
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &payload,
        )],
    ))
    .expect("synthetic replay succeeds");
    let stdout = output
        .lines
        .iter()
        .flat_map(|line| line.iter().copied())
        .collect::<Vec<_>>();
    let stdout = String::from_utf8(stdout).expect("JSON output is UTF-8");
    assert!(!stdout.contains(&raw_hex));
    assert!(!stdout.contains(secret));
}

#[test]
fn schema_message_limit_is_enforced() {
    let too_many = json!({"messages": vec![message("m", false, vec![]); MAX_SCHEMA_MESSAGES + 1]});
    assert_eq!(
        Schema::parse(&too_many).expect_err("schema message limit applies"),
        "SCHEMA_INVALID"
    );
}

#[test]
fn all_protobuf_kinds_are_recognized() {
    for kind in [
        "DOUBLE", "FLOAT", "INT64", "UINT64", "INT32", "FIXED64", "FIXED32", "BOOL", "STRING",
        "GROUP", "MESSAGE", "BYTES", "UINT32", "ENUM", "SFIXED32", "SFIXED64", "SINT32", "SINT64",
    ] {
        assert!(Kind::parse(kind).is_some(), "{kind} is part of contract v1");
    }
    assert_eq!(Kind::parse("F32"), None);
}

#[test]
fn bounded_reader_keeps_generic_reader_helpers() {
    let mut reader = Reader::new(&[0x08, 0x01]);
    assert_eq!(reader.tag(), Some((1, WireType::Varint)));
    assert_eq!(reader.varint(), Some(1));
    assert_eq!(reader.read_bytes(1), None);
    assert_eq!(WireType::from(3), WireType::StartGroup);
}

#[test]
fn schema_field_limit_is_enforced() {
    let fields = (1..=MAX_FIELDS_PER_MESSAGE + 1)
        .map(|number| field(number as u64, "field", "INT32", false, false, Value::Null))
        .collect::<Vec<_>>();
    let schema = json!({"messages":[message("lok", false, fields)]});
    assert_eq!(
        Schema::parse(&schema).expect_err("schema field limit applies"),
        "SCHEMA_INVALID"
    );
}

#[test]
fn analysis_output_limit_sets_detail_truncated() {
    let fields = (1..=4096)
        .map(|number| field(number, &"n".repeat(300), "INT32", false, false, Value::Null))
        .collect::<Vec<_>>();
    let schema = json!({"kind":"schema","messages":[message("lok", false, fields)]});
    let mut payload = Vec::new();
    for number in 1..=4096_u64 {
        encode_varint(number << 3, &mut payload);
        payload.push(1);
    }
    let output = run(&corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &payload,
        )],
    ))
    .expect("oversized detail is truncated");
    let line = output.lines.get(1).expect("analysis exists");
    let value: Value = serde_json::from_slice(line).expect("analysis remains JSON");
    assert_eq!(value["detailTruncated"], true);
    assert!(line.len() < 1_048_576);
}

#[test]
fn binary_writes_only_counts_and_stable_codes_to_stderr() {
    let secret = "PAYLOAD_SECRET";
    let schema = json!({"kind":"schema","messages":[message("lok", false, vec![
        field(1, "label", "STRING", false, false, Value::Null),
    ])]});
    let payload = encode_length_field(1, secret.as_bytes());
    let input = corpus(
        schema,
        &[frame(
            0,
            json!("lok"),
            json!("type.ankama.com/lok"),
            &payload,
        )],
    );
    let mut child = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
        .arg("replay")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .expect("diagnostic binary starts");
    child
        .stdin
        .take()
        .expect("stdin is piped")
        .write_all(input.as_bytes())
        .expect("input writes");
    let result = child
        .wait_with_output()
        .expect("binary output is available");
    assert!(result.status.success());
    let stdout = String::from_utf8(result.stdout).expect("JSON output is UTF-8");
    let stderr = String::from_utf8(result.stderr).expect("diagnostics are UTF-8");
    assert!(stderr.contains("frames=1"));
    assert!(!stderr.contains(secret));
    assert!(!stdout.contains(secret));
    assert!(!stdout.contains(&hex(&payload)));
}

#[test]
fn serve_subcommand_is_rejected_with_exit_two() {
    let result = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
        .arg("serve")
        .output()
        .expect("diagnostic binary starts");
    assert_eq!(result.status.code(), Some(2));
    let stdout = String::from_utf8(result.stdout).expect("error is JSON");
    let stderr = String::from_utf8(result.stderr).expect("error code is UTF-8");
    assert!(stdout.contains("UNKNOWN_COMMAND"));
    assert_eq!(stderr.trim(), "code=UNKNOWN_COMMAND");
}

fn encode_varint(mut value: u64, output: &mut Vec<u8>) {
    while value >= 0x80 {
        output.push((value as u8) | 0x80);
        value >>= 7;
    }
    output.push(value as u8);
}
