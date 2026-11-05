#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FailureKind {
    Contract,
    Internal,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReplayError {
    pub kind: FailureKind,
    pub code: &'static str,
}

impl ReplayError {
    pub const fn contract(code: &'static str) -> Self {
        Self {
            kind: FailureKind::Contract,
            code,
        }
    }

    pub const fn internal() -> Self {
        Self {
            kind: FailureKind::Internal,
            code: "INTERNAL_ERROR",
        }
    }

    pub const fn exit_code(&self) -> i32 {
        match self.kind {
            FailureKind::Contract => 2,
            FailureKind::Internal => 3,
        }
    }
}
