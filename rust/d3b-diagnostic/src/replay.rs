use std::io::{BufRead, Write};

use serde_json::{json, Value};

use crate::error::ReplayError;
use crate::schema::model::Schema;
use crate::schema::walker::{analyze_payload, WalkAnalysis};
use crate::summary::Summary;
use crate::wire::reader::MAX_WIRE_BYTES;

const PROTOCOL_VERSION: u64 = 1;
const SNS_COMMIT: &str = "c780035c9020058a772ac8a2eef09555acd5a173";
const MAX_ANALYSIS_OUTPUT: usize = 1_048_576;

#[derive(Debug)]
pub struct ReplayOutput {
    pub stderr_summary: String,
}

pub(crate) struct HelloInput {
    pub(crate) client_version: String,
    pub(crate) schema_sha256: String,
}

pub(crate) struct FrameInput {
    pub(crate) index: u64,
    pub(crate) direction: String,
    pub(crate) type_url: Option<String>,
    pub(crate) carried_type: Option<String>,
    pub(crate) payload_hex: String,
}

pub fn replay<R: BufRead, W: Write>(
    reader: &mut R,
    writer: &mut W,
    pretty: bool,
) -> Result<ReplayOutput, ReplayError> {
    let first = read_json_line(reader)?.ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
    require_kind(&first, "hello")?;
    let hello = parse_hello(&first)?;

    let schema_line =
        read_json_line(reader)?.ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
    require_kind(&schema_line, "schema")?;
    let schema = Schema::parse(&schema_line).map_err(ReplayError::contract)?;

    let mut summary = Summary::default();
    let hello_line = serialize_value(
        &json!({
            "kind": "hello",
            "protocolVersion": PROTOCOL_VERSION,
            "engine": "d3b-diagnostic",
            "engineVersion": env!("CARGO_PKG_VERSION"),
            "snsCommit": SNS_COMMIT,
        }),
        pretty,
    )?;
    write_line(writer, &hello_line)?;

    let mut previous_index = None;
    let mut ended = false;
    while let Some(line) = read_json_line(reader)? {
        let kind = line
            .get("kind")
            .and_then(Value::as_str)
            .ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
        match kind {
            "frame" => {
                if ended {
                    return Err(ReplayError::contract("CONTRACT_INVALID"));
                }
                let frame = parse_frame(&line)?;
                if previous_index.is_some_and(|previous| frame.index <= previous) {
                    return Err(ReplayError::contract("CONTRACT_INVALID"));
                }
                previous_index = Some(frame.index);
                let (serialized, value) = analyze_frame(&schema, &frame, "index", pretty)?;
                let status = value
                    .get("status")
                    .and_then(Value::as_str)
                    .unwrap_or("MALFORMED");
                let problems = value
                    .get("problems")
                    .and_then(Value::as_array)
                    .map(Vec::as_slice)
                    .unwrap_or(&[]);
                write_line(writer, &serialized)?;
                summary.observe(status, problems);
            }
            "end" => {
                if line.as_object().is_none_or(|object| object.len() != 1) {
                    return Err(ReplayError::contract("CONTRACT_INVALID"));
                }
                ended = true;
                break;
            }
            _ => return Err(ReplayError::contract("CONTRACT_INVALID")),
        }
    }
    if !ended {
        return Err(ReplayError::contract("CONTRACT_INVALID"));
    }

    let summary_line = serialize_value(
        &summary.to_value(&hello.client_version, &hello.schema_sha256),
        pretty,
    )?;
    write_line(writer, &summary_line)?;
    Ok(ReplayOutput {
        stderr_summary: summary.stderr_line(),
    })
}

fn write_line<W: Write>(writer: &mut W, line: &[u8]) -> Result<(), ReplayError> {
    writer
        .write_all(line)
        .map_err(|_| ReplayError::internal())?;
    writer
        .write_all(b"\n")
        .map_err(|_| ReplayError::internal())?;
    writer.flush().map_err(|_| ReplayError::internal())?;
    Ok(())
}

pub fn error_line(code: &str, pretty: bool) -> Vec<u8> {
    serialize_value(&json!({"kind": "error", "code": code}), pretty)
        .unwrap_or_else(|_| b"{\"code\":\"INTERNAL_ERROR\",\"kind\":\"error\"}".to_vec())
}

fn read_json_line<R: BufRead>(reader: &mut R) -> Result<Option<Value>, ReplayError> {
    let mut line = String::new();
    let bytes = reader
        .read_line(&mut line)
        .map_err(|_| ReplayError::contract("CONTRACT_INVALID"))?;
    if bytes == 0 {
        return Ok(None);
    }
    let parsed = serde_json::from_str::<Value>(&line)
        .map_err(|_| ReplayError::contract("CONTRACT_INVALID"))?;
    if !parsed.is_object() {
        return Err(ReplayError::contract("CONTRACT_INVALID"));
    }
    Ok(Some(parsed))
}

fn require_kind(value: &Value, expected: &str) -> Result<(), ReplayError> {
    if value.get("kind").and_then(Value::as_str) == Some(expected) {
        Ok(())
    } else {
        Err(ReplayError::contract("CONTRACT_INVALID"))
    }
}

pub(crate) fn parse_hello(value: &Value) -> Result<HelloInput, ReplayError> {
    let version = value
        .get("protocolVersion")
        .and_then(Value::as_u64)
        .ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
    if version != PROTOCOL_VERSION {
        return Err(ReplayError::contract("VERSION_UNSUPPORTED"));
    }
    let client_version = value
        .get("clientVersion")
        .and_then(Value::as_str)
        .ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?
        .to_owned();
    let schema_sha256 = value
        .get("schemaSha256")
        .and_then(Value::as_str)
        .ok_or_else(|| ReplayError::contract("SCHEMA_FINGERPRINT_INVALID"))?;
    if schema_sha256.len() != 64
        || !schema_sha256
            .bytes()
            .all(|byte| byte.is_ascii_digit() || (b'a'..=b'f').contains(&byte))
    {
        return Err(ReplayError::contract("SCHEMA_FINGERPRINT_INVALID"));
    }
    Ok(HelloInput {
        client_version,
        schema_sha256: schema_sha256.to_owned(),
    })
}

fn parse_frame(value: &Value) -> Result<FrameInput, ReplayError> {
    parse_frame_with_key(value, "index")
}

pub(crate) fn parse_frame_with_key(
    value: &Value,
    sequence_key: &str,
) -> Result<FrameInput, ReplayError> {
    let index = value
        .get(sequence_key)
        .and_then(Value::as_u64)
        .ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
    let direction = value
        .get("direction")
        .and_then(Value::as_str)
        .filter(|direction| matches!(*direction, "client" | "server" | "d3b"))
        .ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?
        .to_owned();
    let type_url = optional_input_string(value, "typeUrl")?;
    let carried_type = optional_input_string(value, "carriedType")?;
    let payload_hex = value
        .get("payloadHex")
        .and_then(Value::as_str)
        .ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?
        .to_owned();
    Ok(FrameInput {
        index,
        direction,
        type_url,
        carried_type,
        payload_hex,
    })
}

pub(crate) fn analyze_frame(
    schema: &Schema,
    frame: &FrameInput,
    sequence_key: &str,
    pretty: bool,
) -> Result<(Vec<u8>, Value), ReplayError> {
    let payload = decode_payload(&frame.payload_hex)?;
    let message = frame
        .carried_type
        .as_deref()
        .and_then(|name| schema.message(name));
    let analysis = analyze_payload(schema, message, &payload);
    let type_url_ok = match (frame.type_url.as_deref(), frame.carried_type.as_deref()) {
        (Some(type_url), Some(carried_type)) => {
            type_url == format!("type.ankama.com/{carried_type}")
        }
        _ => false,
    };
    let mut value = analysis_value(
        frame,
        type_url_ok,
        message.map(|item| item.name.as_str()),
        analysis,
    );
    if sequence_key != "index" {
        let object = value.as_object_mut().ok_or_else(ReplayError::internal)?;
        let index = object.remove("index").ok_or_else(ReplayError::internal)?;
        object.insert(sequence_key.to_owned(), index);
    }
    let bytes = serialize_analysis(value, pretty)?;
    let redacted_value = serde_json::from_slice(&bytes).map_err(|_| ReplayError::internal())?;
    Ok((bytes, redacted_value))
}

fn optional_input_string(value: &Value, key: &str) -> Result<Option<String>, ReplayError> {
    match value.get(key) {
        None => Ok(None),
        Some(Value::Null) => Ok(None),
        Some(Value::String(value)) => Ok(Some(value.clone())),
        _ => Err(ReplayError::contract("CONTRACT_INVALID")),
    }
}

fn decode_payload(hex: &str) -> Result<Vec<u8>, ReplayError> {
    if hex.len() % 2 != 0
        || !hex
            .bytes()
            .all(|byte| byte.is_ascii_digit() || (b'a'..=b'f').contains(&byte))
    {
        return Err(ReplayError::contract("CONTRACT_INVALID"));
    }
    if hex.len() > MAX_WIRE_BYTES.saturating_mul(2) {
        return Ok(vec![0; MAX_WIRE_BYTES + 1]);
    }
    let bytes = hex.as_bytes();
    let mut payload = Vec::with_capacity(bytes.len() / 2);
    for pair in bytes.chunks_exact(2) {
        let high =
            decode_hex_digit(pair[0]).ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
        let low =
            decode_hex_digit(pair[1]).ok_or_else(|| ReplayError::contract("CONTRACT_INVALID"))?;
        payload.push((high << 4) | low);
    }
    Ok(payload)
}

fn decode_hex_digit(byte: u8) -> Option<u8> {
    match byte {
        b'0'..=b'9' => Some(byte - b'0'),
        b'a'..=b'f' => Some(byte - b'a' + 10),
        _ => None,
    }
}

fn analysis_value(
    frame: &FrameInput,
    type_url_ok: bool,
    message: Option<&str>,
    analysis: WalkAnalysis,
) -> Value {
    json!({
        "kind": "analysis",
        "index": frame.index,
        "direction": frame.direction,
        "carriedType": frame.carried_type,
        "typeUrlOk": type_url_ok,
        "status": analysis.status,
        "message": message,
        "fields": analysis.fields,
        "problems": analysis.problems,
        "detailTruncated": false,
    })
}

fn serialize_analysis(mut value: Value, pretty: bool) -> Result<Vec<u8>, ReplayError> {
    let mut bytes = serialize_value(&value, pretty)?;
    if bytes.len() < MAX_ANALYSIS_OUTPUT {
        return Ok(bytes);
    }

    value["detailTruncated"] = json!(true);
    remove_nested_details(&mut value);
    bytes = serialize_value(&value, pretty)?;
    if bytes.len() < MAX_ANALYSIS_OUTPUT {
        return Ok(bytes);
    }

    let original_fields = value
        .get("fields")
        .and_then(Value::as_array)
        .cloned()
        .unwrap_or_default();
    let mut low = 0_usize;
    let mut high = original_fields.len();
    while low < high {
        let keep = low + (high - low).div_ceil(2);
        value["fields"] = json!(original_fields[..keep]);
        bytes = serialize_value(&value, pretty)?;
        if bytes.len() < MAX_ANALYSIS_OUTPUT {
            low = keep;
        } else {
            high = keep - 1;
        }
    }
    value["fields"] = json!(original_fields[..low]);
    serialize_value(&value, pretty)
}

fn remove_nested_details(value: &mut Value) {
    if let Some(fields) = value.get_mut("fields").and_then(Value::as_array_mut) {
        for field in fields {
            remove_nested_from_field(field);
        }
    }
}

fn remove_nested_from_field(field: &mut Value) {
    if let Some(nested) = field.get_mut("nested").and_then(Value::as_array_mut) {
        for child in nested.iter_mut() {
            remove_nested_from_field(child);
        }
        nested.clear();
    }
}

fn serialize_value(value: &Value, pretty: bool) -> Result<Vec<u8>, ReplayError> {
    if pretty {
        serde_json::to_vec_pretty(value).map_err(|_| ReplayError::internal())
    } else {
        serde_json::to_vec(value).map_err(|_| ReplayError::internal())
    }
}

pub fn write_error<W: Write>(writer: &mut W, code: &str, pretty: bool) -> Result<(), ReplayError> {
    writer
        .write_all(&error_line(code, pretty))
        .map_err(|_| ReplayError::internal())?;
    writer
        .write_all(b"\n")
        .map_err(|_| ReplayError::internal())?;
    writer.flush().map_err(|_| ReplayError::internal())
}
