# P2L GO 1 evidence artifacts

The JSON files below were copied from the local diagnostic reports directory. Each copied file was parsed as JSON and its SHA-256 was verified against its source.

| Artifact | Size (bytes) | SHA-256 | Status |
|---|---:|---|---|
| `diagnostic-report-20261106T145842529Z-af40abcc.json` | 74,247 | `6ec3e668629424e086d0e6f3016054190160a88f3cef5e61cf144bfefc491a3d` | **Clean pass 2**; complete live observation. |
| `diagnostic-report-20261106T145625195Z-c508f92a.json` | 67,545 | `81e68280fc98e9ed96fc0e13e3a5b1cc44aa83769ef3b1760d926805a0d26027` | **Contaminated pass 1**; manual movement after placement; excluded from clean evidence. |
| `candidates-3.7.13.17-go1-pass2-pins.json` | 3,784,034 | `9ac81efb42af0fd0c41efd556553dbf4c1c3c2a4de873a301fb1d657baf3b94f` | Pass-2 resolver output with build pins; contains structurally aliased `VERIFIED` assignments and is not promotion evidence. |

## Evidence interpretation

Pass 2 is the clean observation: 124/124 frames, complete coverage, and no drops, unattributed analyses, truncation, or contradictions. Its resolver summary is 8 `VERIFIED`, 4,987 `CANDIDATE`, 149 `CONFLICT`, and 48 `UNKNOWN`. The eight `VERIFIED` entries are not trustworthy because multiple schema tokens resolve to the same readable targets. See [P2L_GO1_REPORT.md](../P2L_GO1_REPORT.md) for the run record, exact duplicate-target groups, and blocked verdict.

Pass 1 is retained only for audit history and is explicitly contaminated by manual movement. It must not be combined with pass-2 evidence. No raw logs, terminal output, SQLite database, or client payloads are included.
