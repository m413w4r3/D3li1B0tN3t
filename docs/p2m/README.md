# P2M Lot 3 report bundle

This directory contains offline summaries of the completed GO 1 diagnostic and the corrected P2M resolver output. No client, live session, or new capture was used in this lot. The source report provenance says 124/124 analyses completed with no dropped frames.

## Inputs and hashes

SHA-256 values identify the exact source bytes used:

| Input | Path | SHA-256 |
|---|---|---|
| Corrected resolver output, protocol 2; 5,627,768 bytes | `/home/nill/.local/share/D3B/diagnostic/reports/p2m-resolved-20261106.json` | `a487bac837a2175d51226fdf5ecb832a033d916b5efa9e39fc89f1dd5cf5a2cd` |
| Clean GO 1 report | `docs/p2l-go1/diagnostic-report-20261106T145842529Z-af40abcc.json` | `6ec3e668629424e086d0e6f3016054190160a88f3cef5e61cf144bfefc491a3d` |
| Policy source | `src/D3B.Bot/Network/NonPositionMessages.cs` | `280d8ab1a72b7a123faab073b0c14e4f08c93fa8cf18e9308182bb3e4e7d0992` |
| BillBot reference | `/home/nill/perso/dofus/references/BillBot/DBDofusUnity/datas/protos/game_mappings_detailed.json` | `bfa440d5f88273bae59d995fdc2da516c22469b2ab30ba1fbcd88ee4c23f77c2` |
| SNS reference | `/home/nill/perso/dofus/references/SniffSniffSquared/sniffer/reference/Mapping.v2.json` | `935ea7f46190ad74204557a6832c5caaa0b735ef529e13b2276d9464c9122c96` |
| Prior family annotation | `docs/P2K_REPORT.md` | `e89f4f3bed10fe0a652a415b1ee96d33ac9c5041f38e3895bbd238ab3c2863aa` |

The handoff’s literal path `/home/nill/perso/dofus/references/SNS Mapping.v2.json` did not exist. The SNS file used is the `Mapping.v2.json` under the `SniffSniffSquared` reference checkout above. The schema SHA-256 recorded by both reports is `e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0`.

All external and historical message names in the blocker inventory are hypotheses. The external mappings are not build-exact identity proof.

## Regenerate the copied full inventory

Run from the repository root. This copies the input bytes without reformatting them:

```sh
mkdir -p docs/p2m
cp /home/nill/.local/share/D3B/diagnostic/reports/p2m-resolved-20261106.json docs/p2m/P2M_RESOLUTION_FULL.json
sha256sum docs/p2m/P2M_RESOLUTION_FULL.json
```

The expected hash is `a487bac837a2175d51226fdf5ecb832a033d916b5efa9e39fc89f1dd5cf5a2cd`.

## Deterministic jq summaries and validation

These commands reproduce the resolver totals, observation totals, policy inventory counts, and JSON integrity checks without reading payload values:

```sh
jq '{protocolVersion, clientVersion, schemaSha256, summary, pairRows: (.candidates | length)}' docs/p2m/P2M_RESOLUTION_FULL.json

jq '{clientVersion, protocolVersion, coverage, frames: .session.analyzedFrames, dropped: .session.droppedFrames, types: (.types | length), occurrences: ([.types[].occurrences] | add), statusOccurrences: ([.types[].statuses[]] | group_by(.status) | map({status: .[0].status, occurrences: ([.[].occurrences] | add)})), problems: ([.types[].problems[]] | length), typeUrlMismatches: ([.types[].typeUrlMismatchOccurrences] | add)}' docs/p2l-go1/diagnostic-report-20261106T145842529Z-af40abcc.json

jq -e '(.blockers | length) == 62 and (.policy.acceptedObserved | length) == 24 and (.policy.listedUnobserved | length) == 1 and (.formerAliases | length) == 8 and .resolution.demonstratedIdentities == 0' docs/p2m/P2M_BLOCKERS.json
jq -e '(.protocolVersion == 2) and (.summary == {"verified":0,"candidate":4995,"conflict":241,"unknown":48}) and ([.candidates[].semanticIdentity | select(. != null)] | length == 0) and ([.candidates[].proof | select(. != null)] | length == 0)' docs/p2m/P2M_RESOLUTION_FULL.json
```

The policy split is a deterministic exact-token and exact-direction comparison. The source uses `true` for client direction and `false` for server direction; the diagnostic report labels these `client` and `server`:

```sh
python3 - <<'PY'
import json, re
from pathlib import Path
report = json.loads(Path('docs/p2l-go1/diagnostic-report-20261106T145842529Z-af40abcc.json').read_text())
source = Path('src/D3B.Bot/Network/NonPositionMessages.cs').read_text()
allowed = {token: ('client' if side == 'true' else 'server') for token, _, side in re.findall(r'\["([a-z]{3})"\] = \("([^"]+)", (true|false)\)', source)}
accepted = [item for item in report['types'] if item['token'] in allowed and all(d['direction'] == allowed[item['token']] for d in item['directions'])]
blockers = [item for item in report['types'] if item not in accepted]
unobserved = sorted(set(allowed) - {item['token'] for item in report['types']})
print({'observed': len(report['types']), 'accepted': len(accepted), 'blockers': len(blockers), 'listed_unobserved': unobserved})
PY
```

The family hypotheses in `P2M_BLOCKERS.json` are drawn from the hash-pinned P2K table for previously annotated tokens, then external lookup for the three new GO 1 tokens: BillBot names `.hme` and `.ibs`, while neither supplied reference names `.ibw`. SNS has no mapping entry for those three observed tokens. `.ibw` therefore remains `unknown`. The blocker array uses the requested fixed first-seven order, then initialization/security/connection families, position-relevant families, and descending occurrence count with token tie-break. The Markdown resolution report is a human-readable summary of these exact source counts; the operational table is rendered from the blocker JSON.

## Bundle files

- [P2M_RESOLUTION_REPORT.md](P2M_RESOLUTION_REPORT.md): method, protocol, exact counts, proof gaps, alias caveat, and inventory pointer.
- [P2M_RESOLUTION_FULL.json](P2M_RESOLUTION_FULL.json): byte-identical copy of the corrected resolver output; retain on demand.
- [P2M_OPERATIONAL_BLOCKERS.md](P2M_OPERATIONAL_BLOCKERS.md): all 62 fail-closed blocker types, plus the 24 source-accepted observations and unobserved `kbi`.
- [P2M_BLOCKERS.json](P2M_BLOCKERS.json): per-type directions, occurrences, statuses/problems, hypotheses, identity status, priority, potential effects, and needed evidence.
