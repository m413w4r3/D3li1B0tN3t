// Adapted from SniffSniffSquared sniffer/src/pb.rs at commit
// c780035c9020058a772ac8a2eef09555acd5a173 (MIT; Copyright (c) 2026 Miouzora).
// Modified for D3B with bounded reads and explicit failure categories.

use std::convert::TryFrom;

pub const MAX_WIRE_BYTES: usize = 1_048_576;
pub const MAX_GROUP_DEPTH: usize = 32;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ReaderLimits {
    pub max_bytes: usize,
}

impl Default for ReaderLimits {
    fn default() -> Self {
        Self {
            max_bytes: MAX_WIRE_BYTES,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ReaderError {
    Truncated,
    VarintOverflow,
    LengthOverflow,
    InvalidTag,
    InvalidWireType(u8),
    LimitExceeded,
    GroupMismatch,
}

#[derive(Debug)]
pub struct Reader<'a> {
    pub buf: &'a [u8],
    pub pos: usize,
    limits: ReaderLimits,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum WireType {
    Varint,
    I64,
    Len,
    StartGroup,
    EndGroup,
    I32,
    Unknown(u8),
}

impl From<u8> for WireType {
    fn from(value: u8) -> Self {
        match value {
            0 => Self::Varint,
            1 => Self::I64,
            2 => Self::Len,
            3 => Self::StartGroup,
            4 => Self::EndGroup,
            5 => Self::I32,
            other => Self::Unknown(other),
        }
    }
}

impl WireType {
    pub fn label(self) -> &'static str {
        match self {
            Self::Varint => "VARINT",
            Self::I64 => "I64",
            Self::Len => "LEN",
            Self::StartGroup | Self::EndGroup => "GROUP",
            Self::I32 => "I32",
            Self::Unknown(_) => "UNKNOWN",
        }
    }
}

impl<'a> Reader<'a> {
    pub fn new(buf: &'a [u8]) -> Self {
        Self::with_limits(buf, ReaderLimits::default())
    }

    pub fn with_limits(buf: &'a [u8], limits: ReaderLimits) -> Self {
        Self {
            buf,
            pos: 0,
            limits,
        }
    }

    pub fn eof(&self) -> bool {
        self.pos >= self.buf.len()
    }

    pub fn remaining(&self) -> usize {
        self.buf.len().saturating_sub(self.pos)
    }

    pub fn is_within_limit(&self) -> bool {
        self.buf.len() <= self.limits.max_bytes
    }

    /// Read a base-128 varint and its encoded width without shifting by 64 or
    /// indexing beyond the input when the wire data is malformed.
    pub fn read_varint(&mut self) -> Result<(u64, u8), ReaderError> {
        if !self.is_within_limit() {
            return Err(ReaderError::LimitExceeded);
        }

        let mut value = 0_u64;
        for index in 0..10_u8 {
            let byte = *self.buf.get(self.pos).ok_or(ReaderError::Truncated)?;
            self.pos = self.pos.saturating_add(1);

            if index == 9 && byte > 1 {
                return Err(ReaderError::VarintOverflow);
            }
            value |= u64::from(byte & 0x7f) << (u32::from(index) * 7);
            if byte & 0x80 == 0 {
                return Ok((value, index + 1));
            }
        }
        Err(ReaderError::VarintOverflow)
    }

    /// Compatibility helper matching the original SNS reader API.
    pub fn varint(&mut self) -> Option<u64> {
        self.read_varint().ok().map(|(value, _)| value)
    }

    pub fn read_tag(&mut self) -> Result<(u32, WireType), ReaderError> {
        let (key, _) = self.read_varint()?;
        let number = key >> 3;
        if number == 0 || number > 536_870_911 {
            return Err(ReaderError::InvalidTag);
        }
        let wire_value = (key & 0x07) as u8;
        if wire_value == 6 || wire_value == 7 {
            return Err(ReaderError::InvalidWireType(wire_value));
        }
        Ok((
            u32::try_from(number).map_err(|_| ReaderError::InvalidTag)?,
            wire_value.into(),
        ))
    }

    /// Compatibility helper matching the original SNS reader API.
    pub fn tag(&mut self) -> Option<(u32, WireType)> {
        self.read_tag().ok()
    }

    pub fn read_bytes_checked(&mut self, count: usize) -> Result<&'a [u8], ReaderError> {
        if !self.is_within_limit() {
            return Err(ReaderError::LimitExceeded);
        }
        let end = self
            .pos
            .checked_add(count)
            .ok_or(ReaderError::LengthOverflow)?;
        if end > self.buf.len() {
            return Err(ReaderError::Truncated);
        }
        let bytes = self.buf.get(self.pos..end).ok_or(ReaderError::Truncated)?;
        self.pos = end;
        Ok(bytes)
    }

    /// Compatibility helper matching the original SNS reader API.
    pub fn read_bytes(&mut self, count: usize) -> Option<&'a [u8]> {
        self.read_bytes_checked(count).ok()
    }

    pub fn read_len_field(&mut self) -> Result<(&'a [u8], u8), ReaderError> {
        let (raw_length, width) = self.read_varint()?;
        let length = usize::try_from(raw_length).map_err(|_| ReaderError::LengthOverflow)?;
        if length > self.limits.max_bytes {
            return Err(ReaderError::LimitExceeded);
        }
        let bytes = self.read_bytes_checked(length)?;
        Ok((bytes, width))
    }

    /// Compatibility helper matching the original SNS reader API.
    pub fn len_field(&mut self) -> Option<&'a [u8]> {
        self.read_len_field().ok().map(|(bytes, _)| bytes)
    }

    pub fn skip_wire(&mut self, wire_type: WireType) -> Result<(), ReaderError> {
        match wire_type {
            WireType::Varint => self.read_varint().map(|_| ()),
            WireType::I64 => self.read_bytes_checked(8).map(|_| ()),
            WireType::Len => self.read_len_field().map(|_| ()),
            WireType::I32 => self.read_bytes_checked(4).map(|_| ()),
            WireType::StartGroup | WireType::EndGroup | WireType::Unknown(_) => {
                Err(ReaderError::InvalidWireType(0xff))
            }
        }
    }

    /// Compatibility helper matching the original SNS reader API.
    pub fn skip(&mut self, wire_type: WireType) -> bool {
        self.skip_wire(wire_type).is_ok()
    }

    /// Skip a group structurally while refusing to interpret its contents.
    /// The small explicit stack prevents hostile group nesting from recursing.
    pub fn skip_group(&mut self, start_field: u32) -> Result<(), ReaderError> {
        let mut fields = Vec::with_capacity(4);
        fields.push(start_field);

        while let Some(expected_end) = fields.last().copied() {
            let (number, wire_type) = self.read_tag()?;
            match wire_type {
                WireType::StartGroup => {
                    if fields.len() >= MAX_GROUP_DEPTH {
                        return Err(ReaderError::LimitExceeded);
                    }
                    fields.push(number);
                }
                WireType::EndGroup => {
                    if number != expected_end {
                        return Err(ReaderError::GroupMismatch);
                    }
                    fields.pop();
                }
                scalar_wire => self.skip_wire(scalar_wire)?,
            }
        }
        Ok(())
    }
}
