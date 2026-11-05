use std::collections::BTreeMap;

use serde_json::{json, Value};

#[derive(Debug, Default)]
pub struct Summary {
    pub frames: u64,
    pub statuses: BTreeMap<String, u64>,
    pub problems: BTreeMap<String, u64>,
}

impl Summary {
    pub fn observe(&mut self, status: &str, problems: &[Value]) {
        self.frames = self.frames.saturating_add(1);
        let status_count = self.statuses.entry(status.to_owned()).or_default();
        *status_count = status_count.saturating_add(1);
        for problem in problems {
            if let Some(code) = problem.get("code").and_then(Value::as_str) {
                let problem_count = self.problems.entry(code.to_owned()).or_default();
                *problem_count = problem_count.saturating_add(1);
            }
        }
    }

    pub fn to_value(&self, client_version: &str, schema_sha256: &str) -> Value {
        json!({
            "kind": "summary",
            "protocolVersion": 1,
            "clientVersion": client_version,
            "schemaSha256": schema_sha256,
            "frames": self.frames,
            "statuses": self.statuses,
            "problems": self.problems,
            "dropped": 0,
            "coverage": "complete",
        })
    }

    pub fn stderr_line(&self) -> String {
        let statuses = self
            .statuses
            .iter()
            .map(|(code, count)| format!("{code}:{count}"))
            .collect::<Vec<_>>()
            .join(",");
        let problems = self
            .problems
            .iter()
            .map(|(code, count)| format!("{code}:{count}"))
            .collect::<Vec<_>>()
            .join(",");
        format!(
            "frames={} statuses=[{}] problems=[{}]",
            self.frames, statuses, problems
        )
    }
}
