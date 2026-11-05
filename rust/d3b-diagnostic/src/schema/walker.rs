// Adapted from the schema-walk approach in SniffSniffSquared sniffer/src/schema.rs
// at commit c780035c9020058a772ac8a2eef09555acd5a173 (MIT; Copyright (c) 2026 Miouzora).
// Modified for D3B contract-v1 kinds, strict packed rules, limits and redaction.

use std::collections::{BTreeMap, BTreeSet};

use serde_json::{json, Value};

use crate::analysis::mismatch::{is_packable_wire, mismatch_codes};
use crate::schema::model::{Field, Kind, Message, Schema, MAX_FIELDS_PER_MESSAGE};
use crate::wire::reader::{Reader, ReaderError, WireType, MAX_WIRE_BYTES};

pub const MAX_DEPTH: usize = 32;
const MAX_DISTINCT_SHAPES: usize = 8;
type LengthReadError = (ReaderError, Option<usize>);

#[derive(Debug, Clone)]
pub struct WalkAnalysis {
    pub status: &'static str,
    pub fields: Vec<Value>,
    pub problems: Vec<Value>,
}

#[derive(Debug, Default, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
enum StopStatus {
    #[default]
    None,
    Malformed,
    Truncated,
    TrailingBytes,
    LimitExceeded,
}

#[derive(Debug, Default)]
struct WalkState {
    stop: StopStatus,
    schema_mismatch: bool,
    problems: BTreeSet<(String, String, Option<String>)>,
}

impl WalkState {
    fn add_problem(&mut self, code: &str, field_path: Option<&str>, detail: Option<String>) {
        let path = field_path.unwrap_or("").to_owned();
        self.problems.insert((code.to_owned(), path, detail));
    }

    fn add_field_problem(&mut self, field: &mut FieldAccumulator, code: &str, path: &str) {
        field.problem_codes.insert(code.to_owned());
        self.add_problem(code, Some(path), None);
        if !matches!(
            code,
            "TRUNCATED" | "VARINT_OVERFLOW" | "LENGTH_OVERFLOW" | "LIMIT_EXCEEDED"
        ) {
            self.schema_mismatch = true;
        }
    }

    fn add_frame_problem(&mut self, code: &str, path: Option<&str>) {
        self.add_problem(code, path, None);
    }

    fn stop(&mut self, status: StopStatus) {
        self.stop = self.stop.max(status);
    }
}

#[derive(Debug, Clone)]
struct FieldAccumulator {
    number: u32,
    name: Option<String>,
    declared_kind: Option<String>,
    repeated: bool,
    packed: bool,
    occurrences: u64,
    wire_types: Vec<String>,
    varint_widths: BTreeSet<usize>,
    fixed_lengths: BTreeSet<usize>,
    lengths: BTreeSet<usize>,
    length_capped: bool,
    utf8: Option<bool>,
    field_counts: BTreeSet<usize>,
    field_count_capped: bool,
    zero_length: bool,
    saw_length: bool,
    truncated: bool,
    is_message: bool,
    nested: Vec<FieldAccumulator>,
    problem_codes: BTreeSet<String>,
}

impl FieldAccumulator {
    fn new(number: u32, declared: Option<&Field>) -> Self {
        Self {
            number,
            name: declared.map(|field| field.name.clone()),
            declared_kind: declared.map(|field| field.kind.as_str().to_owned()),
            repeated: declared.is_some_and(|field| field.repeated),
            packed: declared.is_some_and(|field| field.packed),
            occurrences: 0,
            wire_types: Vec::new(),
            varint_widths: BTreeSet::new(),
            fixed_lengths: BTreeSet::new(),
            lengths: BTreeSet::new(),
            length_capped: false,
            utf8: None,
            field_counts: BTreeSet::new(),
            field_count_capped: false,
            zero_length: false,
            saw_length: false,
            truncated: false,
            is_message: declared.is_some_and(|field| field.kind == Kind::Message),
            nested: Vec::new(),
            problem_codes: BTreeSet::new(),
        }
    }

    fn add_wire_type(&mut self, wire_type: WireType) {
        let label = wire_type.label().to_owned();
        if !self.wire_types.contains(&label) {
            self.wire_types.push(label);
        }
    }

    fn add_length(&mut self, length: usize) {
        self.saw_length = true;
        self.zero_length |= length == 0;
        add_capped_shape(&mut self.lengths, &mut self.length_capped, length);
    }

    fn add_field_count(&mut self, count: usize) {
        add_capped_shape(&mut self.field_counts, &mut self.field_count_capped, count);
    }

    fn merge_nested(&mut self, incoming: Vec<FieldAccumulator>) {
        for child in incoming {
            if let Some(existing) = self
                .nested
                .iter_mut()
                .find(|field| field.number == child.number)
            {
                existing.merge(child);
            } else {
                self.nested.push(child);
            }
        }
    }

    fn merge(&mut self, other: FieldAccumulator) {
        self.occurrences = self.occurrences.saturating_add(other.occurrences);
        for wire_type in other.wire_types {
            if !self.wire_types.contains(&wire_type) {
                self.wire_types.push(wire_type);
            }
        }
        self.varint_widths.extend(other.varint_widths);
        self.fixed_lengths.extend(other.fixed_lengths);
        for length in other.lengths {
            add_capped_shape(&mut self.lengths, &mut self.length_capped, length);
        }
        self.length_capped |= other.length_capped;
        self.utf8 = match (self.utf8, other.utf8) {
            (None, value) | (value, None) => value,
            (Some(left), Some(right)) => Some(left && right),
        };
        for count in other.field_counts {
            add_capped_shape(&mut self.field_counts, &mut self.field_count_capped, count);
        }
        self.field_count_capped |= other.field_count_capped;
        self.zero_length |= other.zero_length;
        self.saw_length |= other.saw_length;
        self.truncated |= other.truncated;
        self.problem_codes.extend(other.problem_codes);
        self.merge_nested(other.nested);
    }

    fn to_value(&self) -> Value {
        let mut observed = serde_json::Map::new();
        if !self.varint_widths.is_empty() {
            observed.insert(
                "varintBytes".to_owned(),
                json!(self.varint_widths.iter().copied().collect::<Vec<_>>()),
            );
        }
        if !self.fixed_lengths.is_empty() {
            observed.insert(
                "fixedLength".to_owned(),
                json!(self.fixed_lengths.iter().copied().collect::<Vec<_>>()),
            );
        }
        if self.saw_length {
            observed.insert(
                "length".to_owned(),
                json!(self.lengths.iter().copied().collect::<Vec<_>>()),
            );
            if self.length_capped {
                observed.insert("lengthCapped".to_owned(), json!(true));
            }
            observed.insert("zeroLength".to_owned(), json!(self.zero_length));
        }
        if self.declared_kind.as_deref() == Some("STRING") {
            observed.insert("utf8".to_owned(), json!(self.utf8));
        }
        if self.is_message {
            observed.insert(
                "fieldCount".to_owned(),
                json!(self.field_counts.iter().copied().collect::<Vec<_>>()),
            );
        }
        if self.truncated {
            observed.insert("truncated".to_owned(), json!(true));
        }

        let mut value = json!({
            "number": self.number,
            "name": self.name,
            "declaredKind": self.declared_kind,
            "repeated": self.repeated,
            "packed": self.packed,
            "occurrences": self.occurrences,
            "wireTypes": self.wire_types,
            "observed": Value::Object(observed),
            "problems": self.problem_codes,
        });
        if self.is_message {
            value["nested"] = json!(self
                .nested
                .iter()
                .map(FieldAccumulator::to_value)
                .collect::<Vec<_>>());
        }
        value
    }
}

fn add_capped_shape(values: &mut BTreeSet<usize>, capped: &mut bool, value: usize) {
    if values.contains(&value) {
        return;
    }
    if values.len() < MAX_DISTINCT_SHAPES {
        values.insert(value);
        return;
    }
    *capped = true;
    if let Some(largest) = values.last().copied() {
        if value < largest {
            values.remove(&largest);
            values.insert(value);
        }
    }
}

pub fn analyze_payload(schema: &Schema, message: Option<&Message>, payload: &[u8]) -> WalkAnalysis {
    if payload.len() > MAX_WIRE_BYTES {
        let mut state = WalkState::default();
        state.stop(StopStatus::LimitExceeded);
        state.add_frame_problem("LIMIT_EXCEEDED", None);
        return finish_analysis(Vec::new(), state, "LIMIT_EXCEEDED");
    }
    let Some(message) = message else {
        let mut state = WalkState::default();
        state.add_problem("UNKNOWN_TYPE", None, Some(payload.len().to_string()));
        return finish_analysis(Vec::new(), state, "UNKNOWN_TYPE");
    };

    let mut state = WalkState::default();
    let fields = walk_message(payload, schema, message, 0, "", &mut state);
    finish_analysis(fields, state, "VALID")
}

fn finish_analysis(
    fields: Vec<FieldAccumulator>,
    state: WalkState,
    default_status: &'static str,
) -> WalkAnalysis {
    let status = match state.stop {
        StopStatus::LimitExceeded => "LIMIT_EXCEEDED",
        StopStatus::TrailingBytes => "TRAILING_BYTES",
        StopStatus::Truncated => "TRUNCATED",
        StopStatus::Malformed => "MALFORMED",
        StopStatus::None if state.schema_mismatch => "SCHEMA_MISMATCH",
        StopStatus::None => default_status,
    };
    let problems = state
        .problems
        .into_iter()
        .map(|(code, path, detail)| {
            let path_value = if path.is_empty() {
                Value::Null
            } else {
                json!(path)
            };
            let mut problem = json!({"code": code, "fieldPath": path_value});
            if let Some(detail) = detail {
                problem["detail"] = json!(detail);
            }
            problem
        })
        .collect();
    WalkAnalysis {
        status,
        fields: fields.iter().map(FieldAccumulator::to_value).collect(),
        problems,
    }
}

fn walk_message(
    payload: &[u8],
    schema: &Schema,
    schema_message: &Message,
    depth: usize,
    path_prefix: &str,
    state: &mut WalkState,
) -> Vec<FieldAccumulator> {
    let mut reader = Reader::new(payload);
    let mut fields = Vec::<FieldAccumulator>::new();
    let mut field_indexes = BTreeMap::<u32, usize>::new();

    while !reader.eof() {
        let (number, wire_type) = match reader.read_tag() {
            Ok(tag) => tag,
            Err(error) => {
                record_reader_failure(error, None, path_prefix, state);
                break;
            }
        };
        let field_path = if path_prefix.is_empty() {
            number.to_string()
        } else {
            format!("{path_prefix}.{number}")
        };
        let declared = schema_message.field(number);
        let index = match field_indexes.get(&number).copied() {
            Some(index) => index,
            None => {
                if fields.len() >= MAX_FIELDS_PER_MESSAGE {
                    state.stop(StopStatus::LimitExceeded);
                    state.add_frame_problem("LIMIT_EXCEEDED", Some(&field_path));
                    break;
                }
                let index = fields.len();
                fields.push(FieldAccumulator::new(number, declared));
                field_indexes.insert(number, index);
                index
            }
        };
        let Some(observation) = fields.get_mut(index) else {
            state.stop(StopStatus::Malformed);
            state.add_frame_problem("MALFORMED", Some(&field_path));
            break;
        };
        observation.occurrences = observation.occurrences.saturating_add(1);
        observation.add_wire_type(wire_type);

        if let Some(field) = declared {
            for code in mismatch_codes(field, wire_type) {
                state.add_field_problem(observation, code, &field_path);
            }
        } else {
            state.add_field_problem(observation, "UNKNOWN_FIELD_NUMBER", &field_path);
        }

        match wire_type {
            WireType::Varint => match reader.read_varint() {
                Ok((_, width)) => {
                    observation.varint_widths.insert(usize::from(width));
                }
                Err(error) => {
                    observation.truncated = true;
                    record_reader_failure(error, Some(observation), &field_path, state);
                    break;
                }
            },
            WireType::I64 => match reader.read_bytes_checked(8) {
                Ok(_) => {
                    observation.fixed_lengths.insert(8);
                }
                Err(error) => {
                    observation.truncated = true;
                    record_reader_failure(error, Some(observation), &field_path, state);
                    break;
                }
            },
            WireType::I32 => match reader.read_bytes_checked(4) {
                Ok(_) => {
                    observation.fixed_lengths.insert(4);
                }
                Err(error) => {
                    observation.truncated = true;
                    record_reader_failure(error, Some(observation), &field_path, state);
                    break;
                }
            },
            WireType::Len => {
                let (bytes, length) = match read_length(&mut reader) {
                    Ok(result) => result,
                    Err((error, declared_length)) => {
                        if let Some(length) = declared_length {
                            observation.add_length(length);
                        }
                        observation.truncated = true;
                        record_reader_failure(error, Some(observation), &field_path, state);
                        break;
                    }
                };
                observation.add_length(length);
                match declared {
                    Some(field) if field.kind == Kind::String => {
                        let valid = std::str::from_utf8(bytes).is_ok();
                        observation.utf8 = Some(observation.utf8.unwrap_or(true) && valid);
                        if !valid {
                            state.add_field_problem(observation, "INVALID_UTF8", &field_path);
                        }
                    }
                    Some(field) if field.kind == Kind::Message => {
                        if depth >= MAX_DEPTH {
                            state.add_field_problem(observation, "DEPTH_EXCEEDED", &field_path);
                            state.add_frame_problem("LIMIT_EXCEEDED", Some(&field_path));
                            state.stop(StopStatus::LimitExceeded);
                        } else if let Some(message_type) = field.message_type.as_deref() {
                            if let Some(nested_schema) = schema.message(message_type) {
                                let nested_fields = walk_message(
                                    bytes,
                                    schema,
                                    nested_schema,
                                    depth + 1,
                                    &field_path,
                                    state,
                                );
                                observation.add_field_count(nested_fields.len());
                                observation.merge_nested(nested_fields);
                            } else {
                                state.add_field_problem(
                                    observation,
                                    "SCHEMA_MESSAGE_MISSING",
                                    &field_path,
                                );
                            }
                        } else {
                            state.add_field_problem(
                                observation,
                                "SCHEMA_MESSAGE_MISSING",
                                &field_path,
                            );
                        }
                    }
                    Some(field) if field.repeated && field.kind.is_packable() => {
                        scan_packed(bytes, field.kind, observation, &field_path, state);
                    }
                    _ => {}
                }
            }
            WireType::StartGroup => {
                state.add_field_problem(observation, "GROUP_UNSUPPORTED", &field_path);
                match reader.skip_group(number) {
                    Ok(()) => {}
                    Err(error) => {
                        observation.truncated = true;
                        record_reader_failure(error, Some(observation), &field_path, state);
                        break;
                    }
                }
            }
            WireType::EndGroup => {
                state.add_field_problem(observation, "GROUP_UNSUPPORTED", &field_path);
            }
            WireType::Unknown(_) => {
                state.stop(StopStatus::Malformed);
                state.add_frame_problem("MALFORMED", Some(&field_path));
                break;
            }
        }

        if state.stop != StopStatus::None {
            break;
        }
    }

    fields
}

fn read_length<'a>(reader: &mut Reader<'a>) -> Result<(&'a [u8], usize), LengthReadError> {
    let (raw_length, _) = reader.read_varint().map_err(|error| (error, None))?;
    let length = usize::try_from(raw_length).map_err(|_| (ReaderError::LengthOverflow, None))?;
    if length > reader.remaining() {
        return Err((ReaderError::LengthOverflow, Some(length)));
    }
    let bytes = reader
        .read_bytes_checked(length)
        .map_err(|error| (error, Some(length)))?;
    Ok((bytes, length))
}

fn scan_packed(
    bytes: &[u8],
    kind: Kind,
    observation: &mut FieldAccumulator,
    field_path: &str,
    state: &mut WalkState,
) {
    let wire_type = wire_for_kind(kind);
    if !is_packable_wire(wire_type) {
        state.add_field_problem(observation, "WIRE_TYPE_MISMATCH", field_path);
        return;
    }
    let mut reader = Reader::new(bytes);
    while !reader.eof() {
        let result = match wire_type {
            WireType::Varint => reader.read_varint().map(|(_, width)| {
                observation.varint_widths.insert(usize::from(width));
            }),
            WireType::I32 => reader.read_bytes_checked(4).map(|_| {
                observation.fixed_lengths.insert(4);
            }),
            WireType::I64 => reader.read_bytes_checked(8).map(|_| {
                observation.fixed_lengths.insert(8);
            }),
            _ => Ok(()),
        };
        if let Err(error) = result {
            observation.truncated = true;
            state.add_field_problem(observation, "TRUNCATED", field_path);
            state.stop(match error {
                ReaderError::VarintOverflow => StopStatus::Malformed,
                ReaderError::LengthOverflow => StopStatus::TrailingBytes,
                ReaderError::LimitExceeded => StopStatus::LimitExceeded,
                _ => StopStatus::Truncated,
            });
            break;
        }
    }
}

fn wire_for_kind(kind: Kind) -> WireType {
    match kind {
        Kind::Double | Kind::Fixed64 | Kind::Sfixed64 => WireType::I64,
        Kind::Float | Kind::Fixed32 | Kind::Sfixed32 => WireType::I32,
        Kind::Int64
        | Kind::Uint64
        | Kind::Int32
        | Kind::Uint32
        | Kind::Bool
        | Kind::Enum
        | Kind::Sint32
        | Kind::Sint64 => WireType::Varint,
        Kind::String | Kind::Group | Kind::Message | Kind::Bytes => WireType::Len,
    }
}

fn record_reader_failure(
    error: ReaderError,
    observation: Option<&mut FieldAccumulator>,
    field_path: &str,
    state: &mut WalkState,
) {
    let (code, stop) = match error {
        ReaderError::Truncated => ("TRUNCATED", StopStatus::Truncated),
        ReaderError::VarintOverflow => ("VARINT_OVERFLOW", StopStatus::Malformed),
        ReaderError::LengthOverflow => ("LENGTH_OVERFLOW", StopStatus::TrailingBytes),
        ReaderError::InvalidTag | ReaderError::InvalidWireType(_) | ReaderError::GroupMismatch => {
            ("MALFORMED", StopStatus::Malformed)
        }
        ReaderError::LimitExceeded => ("LIMIT_EXCEEDED", StopStatus::LimitExceeded),
    };
    if let Some(observation) = observation {
        state.add_field_problem(observation, code, field_path);
    } else {
        state.add_frame_problem(
            code,
            if field_path.is_empty() {
                None
            } else {
                Some(field_path)
            },
        );
    }
    if stop == StopStatus::LimitExceeded {
        state.add_frame_problem(
            "LIMIT_EXCEEDED",
            if field_path.is_empty() {
                None
            } else {
                Some(field_path)
            },
        );
    }
    state.stop(stop);
}
