// Adapted from the schema-walk approach in SniffSniffSquared sniffer/src/schema.rs
// at commit c780035c9020058a772ac8a2eef09555acd5a173 (MIT; Copyright (c) 2026 Miouzora).
// Modified for D3B contract-v1 schema validation and deterministic ordering.

use std::collections::BTreeMap;

use serde_json::Value;

pub const MAX_SCHEMA_MESSAGES: usize = 20_000;
pub const MAX_FIELDS_PER_MESSAGE: usize = 4_096;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Kind {
    Double,
    Float,
    Int64,
    Uint64,
    Int32,
    Fixed64,
    Fixed32,
    Bool,
    String,
    Group,
    Message,
    Bytes,
    Uint32,
    Enum,
    Sfixed32,
    Sfixed64,
    Sint32,
    Sint64,
}

impl Kind {
    pub fn parse(value: &str) -> Option<Self> {
        match value {
            "DOUBLE" => Some(Self::Double),
            "FLOAT" => Some(Self::Float),
            "INT64" => Some(Self::Int64),
            "UINT64" => Some(Self::Uint64),
            "INT32" => Some(Self::Int32),
            "FIXED64" => Some(Self::Fixed64),
            "FIXED32" => Some(Self::Fixed32),
            "BOOL" => Some(Self::Bool),
            "STRING" => Some(Self::String),
            "GROUP" => Some(Self::Group),
            "MESSAGE" => Some(Self::Message),
            "BYTES" => Some(Self::Bytes),
            "UINT32" => Some(Self::Uint32),
            "ENUM" => Some(Self::Enum),
            "SFIXED32" => Some(Self::Sfixed32),
            "SFIXED64" => Some(Self::Sfixed64),
            "SINT32" => Some(Self::Sint32),
            "SINT64" => Some(Self::Sint64),
            _ => None,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::Double => "DOUBLE",
            Self::Float => "FLOAT",
            Self::Int64 => "INT64",
            Self::Uint64 => "UINT64",
            Self::Int32 => "INT32",
            Self::Fixed64 => "FIXED64",
            Self::Fixed32 => "FIXED32",
            Self::Bool => "BOOL",
            Self::String => "STRING",
            Self::Group => "GROUP",
            Self::Message => "MESSAGE",
            Self::Bytes => "BYTES",
            Self::Uint32 => "UINT32",
            Self::Enum => "ENUM",
            Self::Sfixed32 => "SFIXED32",
            Self::Sfixed64 => "SFIXED64",
            Self::Sint32 => "SINT32",
            Self::Sint64 => "SINT64",
        }
    }

    pub fn is_packable(self) -> bool {
        !matches!(
            self,
            Self::String | Self::Group | Self::Message | Self::Bytes
        )
    }
}

#[derive(Debug, Clone)]
pub struct Field {
    pub number: u32,
    pub name: String,
    pub kind: Kind,
    pub repeated: bool,
    pub packed: bool,
    pub message_type: Option<String>,
}

#[derive(Debug, Clone)]
pub struct Message {
    pub name: String,
    pub map_entry: bool,
    pub fields: Vec<Field>,
    field_indexes: BTreeMap<u32, usize>,
}

impl Message {
    pub fn field(&self, number: u32) -> Option<&Field> {
        self.field_indexes
            .get(&number)
            .and_then(|index| self.fields.get(*index))
    }
}

#[derive(Debug, Clone)]
pub struct Schema {
    pub messages: BTreeMap<String, Message>,
}

impl Schema {
    pub fn parse(value: &Value) -> Result<Self, &'static str> {
        let messages_value = value
            .get("messages")
            .and_then(Value::as_array)
            .ok_or("SCHEMA_INVALID")?;
        if messages_value.len() > MAX_SCHEMA_MESSAGES {
            return Err("SCHEMA_INVALID");
        }

        let mut messages = BTreeMap::new();
        for message_value in messages_value {
            let message = parse_message(message_value)?;
            if messages.insert(message.name.clone(), message).is_some() {
                return Err("SCHEMA_INVALID");
            }
        }

        for message in messages.values() {
            for field in &message.fields {
                if field.kind == Kind::Message {
                    let referenced = field.message_type.as_deref().ok_or("SCHEMA_INVALID")?;
                    if !messages.contains_key(referenced) {
                        return Err("SCHEMA_INVALID");
                    }
                }
            }
        }

        Ok(Self { messages })
    }

    pub fn message(&self, name: &str) -> Option<&Message> {
        self.messages.get(name)
    }
}

fn parse_message(value: &Value) -> Result<Message, &'static str> {
    let name = required_string(value, "name")?;
    if name.is_empty() {
        return Err("SCHEMA_INVALID");
    }
    let map_entry = required_bool(value, "mapEntry")?;
    let field_values = value
        .get("fields")
        .and_then(Value::as_array)
        .ok_or("SCHEMA_INVALID")?;
    if field_values.len() > MAX_FIELDS_PER_MESSAGE {
        return Err("SCHEMA_INVALID");
    }

    let mut fields = Vec::with_capacity(field_values.len());
    let mut field_indexes = BTreeMap::new();
    for field_value in field_values {
        let field = parse_field(field_value)?;
        let index = fields.len();
        if field_indexes.insert(field.number, index).is_some() {
            return Err("SCHEMA_INVALID");
        }
        fields.push(field);
    }
    fields.sort_by_key(|field| field.number);
    field_indexes.clear();
    for (index, field) in fields.iter().enumerate() {
        field_indexes.insert(field.number, index);
    }

    Ok(Message {
        name,
        map_entry,
        fields,
        field_indexes,
    })
}

fn parse_field(value: &Value) -> Result<Field, &'static str> {
    let number = value
        .get("number")
        .and_then(Value::as_u64)
        .filter(|number| (1..=536_870_911).contains(number))
        .and_then(|number| u32::try_from(number).ok())
        .ok_or("SCHEMA_INVALID")?;
    let name = required_string(value, "name")?;
    if name.is_empty() {
        return Err("SCHEMA_INVALID");
    }
    let kind_name = required_string(value, "kind")?;
    let kind = Kind::parse(&kind_name).ok_or("SCHEMA_INVALID")?;
    let repeated = required_bool(value, "repeated")?;
    let packed = required_bool(value, "packed")?;
    let message_type = optional_string(value, "messageType")?;
    let enum_type = optional_string(value, "enumType")?;
    let _oneof = optional_string(value, "oneof")?;
    let _has_presence = required_bool(value, "hasPresence")?;

    if kind != Kind::Enum && enum_type.is_some() {
        return Err("SCHEMA_INVALID");
    }
    if kind == Kind::Message && message_type.is_none() {
        return Err("SCHEMA_INVALID");
    }
    if kind != Kind::Message && message_type.is_some() {
        return Err("SCHEMA_INVALID");
    }
    if packed && (!repeated || !kind.is_packable()) {
        return Err("SCHEMA_INVALID");
    }

    Ok(Field {
        number,
        name,
        kind,
        repeated,
        packed,
        message_type,
    })
}

fn required_string(value: &Value, key: &str) -> Result<String, &'static str> {
    value
        .get(key)
        .and_then(Value::as_str)
        .map(str::to_owned)
        .ok_or("SCHEMA_INVALID")
}

fn required_bool(value: &Value, key: &str) -> Result<bool, &'static str> {
    value
        .get(key)
        .and_then(Value::as_bool)
        .ok_or("SCHEMA_INVALID")
}

fn optional_string(value: &Value, key: &str) -> Result<Option<String>, &'static str> {
    match value.get(key) {
        None => Ok(None),
        Some(Value::Null) => Ok(None),
        Some(Value::String(value)) => Ok(Some(value.clone())),
        _ => Err("SCHEMA_INVALID"),
    }
}
