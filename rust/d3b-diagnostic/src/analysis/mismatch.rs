// Adapted from the mismatch handling stance in SniffSniffSquared sniffer/src/dump.rs
// at commit c780035c9020058a772ac8a2eef09555acd5a173 (MIT; Copyright (c) 2026 Miouzora).
// Modified for D3B: retain the wire observation and emit stable redacted codes.

use crate::schema::model::{Field, Kind};
use crate::wire::reader::WireType;

pub fn mismatch_codes(field: &Field, observed: WireType) -> Vec<&'static str> {
    if matches!(observed, WireType::StartGroup | WireType::EndGroup) {
        return vec!["GROUP_UNSUPPORTED"];
    }

    let expected = match field.kind {
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
        Kind::String | Kind::Message | Kind::Bytes => WireType::Len,
        Kind::Group => return vec!["GROUP_UNSUPPORTED"],
    };

    if observed == WireType::Len && field.repeated && field.kind.is_packable() {
        return if field.packed {
            Vec::new()
        } else {
            vec!["PACKED_NOT_ALLOWED"]
        };
    }

    if observed == expected {
        return Vec::new();
    }

    vec!["WIRE_TYPE_MISMATCH"]
}

pub fn is_packable_wire(wire_type: WireType) -> bool {
    matches!(wire_type, WireType::Varint | WireType::I32 | WireType::I64)
}
