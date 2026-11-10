# P2O element candidates runbook

**Status: PREPARED / NOT EXECUTED / OWNER GO REQUIRED.** This runbook records preparation only. It is not
authorization. Do not execute it without a distinct, explicit owner GO for passive observation. Live action remains
separately **BLOCKED** under D340. This runbook grants no GO.

## 1. Scope and non-goals

This protocol prepares passive discovery of harvest candidates on the character's current map. It records exact
identifiers and the source of each datum so an operator can assess the historical Walnut reference target and decide
whether a later observation is justified. It does not claim visual identity from protocol, pack, or catalog data.

The candidate command reads one coherent BotActor snapshot and current map data. It joins server-observed elements
with pack-declared interactives and job/item catalog evidence. GraphicSkills may contribute a labelled hint for an
element without server skills; a graphic hint is not visual proof.

This procedure does not issue p2j element use, move, click, harvest, start an activity/profile/schedule, change a
mapping, promote a resolver, change the translation latch, widen NonPositionMessages, or grant a live GO. Keep the
operator present throughout any separately authorized observation.

## 2. Independent gates and observation-only settings

The two settings control different things:

- Supervised:ElementUse:Enabled registers the console and both passive commands: p2j element candidates and
  p2j element readiness.
- Supervised:ElementUse:ActionEnabled gates p2j element use. It must remain false for the entire S2B pass.

Both settings default to false. S2B is the only planned place where Enabled is true, and only under a distinct owner GO
for passive observation. Do not set ActionEnabled true during S2B. Any later one-action GO is a separate decision under
D340; this runbook grants none.

The candidate report has two independent verdicts. TARGET_IDENTIFIED, TARGET_AMBIGUOUS, or TARGET_UNKNOWN describes
resource identity evidence. ACTION_READY or ACTION_BLOCKED describes whether action preconditions and the action gate
would allow the supervised action. In S2B, ActionEnabled stays false, so the expected action verdict is
ACTION_BLOCKED even when a resource candidate is identified and its per-candidate preparation says Ready.

## 3. Build identity: publish a new S2A apphost

Do not reuse the P2O apphost apphost-p2o-b82780a or the installed P2L apphost. Neither contains S2A. Publish a new
private apphost from the reviewed S2A source on branch d3b-s2a-element-candidates, based on
34b3b0538bdef8b6b7a669d68bb703f248bf917d. If the owner authorizes a commit, publish that exact commit.

Choose a fresh UTC-stamped private root outside Git for this observation; replace the example suffix before use. Keep
the session database, manifests, reports, screenshots, and publish output under that root:

~~~sh
OBS=/home/nill/.local/share/D3B/p2o-element-candidates-YYYYMMDDTHHMMSSZ
STAGE="$OBS/apphost-build"
APPHOST="$STAGE/publish"
SESSION_DB="$OBS/session.db"
ANKAMA_BEFORE="$OBS/ankama-before.json"
D3B_BEFORE="$OBS/d3b-before.json"
ANKAMA_CONFIG_ROOT=/home/nill/.config/Ankama/Dofus-dofus3
BACKUP="$OBS/ankama-backup"
REPORTS="$OBS/diagnostic-reports"
UI="$OBS/ui"
install -d -m 0700 "$OBS" "$REPORTS" "$UI" "$STAGE" "$APPHOST"
~~~

Record outside Git the complete reviewed source identity and publish details. From the reviewed source root, use the
pinned SDK. Use already-restored packages; if a restore is required, use a private offline/empty feed as in
[P2L_DEPLOYMENT.md](P2L_DEPLOYMENT.md), and do not fetch or add a dependency.

~~~sh
/home/nill/.dotnet/dotnet publish src/D3B.Host/D3B.Host.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:UseAppHost=true \
  --artifacts-path "$STAGE/build" \
  --output "$APPHOST" \
  --verbosity minimal
~~~

Record privately, before any future session:

- The source commit SHA, or the base SHA plus a SHA-256 of the complete reviewed diff when it is uncommitted.
- The exact publish command and the apphost informational version.
- A full sorted SHA-256 manifest, file count, total size, launcher and managed-payload hashes, and directory/file modes.
- Evidence from an offline, no-client verification that the enabled console banner contains p2j element candidates,
  and that the focused tests pass from the same source:

  ~~~sh
  /home/nill/.dotnet/dotnet build
  /home/nill/.dotnet/dotnet test tests/D3B.Bot.Tests/D3B.Bot.Tests.csproj --no-build --max-parallel-test-modules 1 --filter 'FullyQualifiedName~ElementCandidates|FullyQualifiedName~SupervisedElementUse'
  ~~~

  Do not infer the build identity from a directory or executable name.

Set the apphost directory and launcher to mode 0700 and managed/runtime payload files to 0600. Check that the launcher
has no file capability. Verify the staged copy against the recorded manifest. Do not launch this apphost against the
live client during the S2A mission.

## 4. Client and data identity to verify on the day

The historical P2O reference identity is client 3.7.13.17, build 2026-11-03T14:11:23+00:00, Linux x86_64,
Unity 6000.3.16f1, metadata version 39. Re-verify the client version/build and the full client tree against a fresh
private before manifest on the day; do not copy this reference as current fact.

Match the pack, schema, mapping, and diagnostic-engine hashes to fresh private before records, and stop on any
mismatch. The prior preflight report's data values are historical references only: data-8.pack
28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0, schema-1.binpb
e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0, mapping-15.binpb
f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e, and diagnostic engine
2ac365be2ac70cd5a3fe5e338179ab58cf1a83fdffedcf283e522b3d5200836b. Use
/home/nill/.dotnet/dotnet (SDK 10.0.403) for every script.

## 5. Ankama directories, backup, and restore

Use private paths outside Git for the observation root, session database, manifests, reports, logs, and screenshots.
Before starting, require the client check to exit 0 and name Ankama's configuration. Create fresh manifests, then create
and verify the private backup. The backup command creates its output directory, so BACKUP must not already exist:

~~~sh
/home/nill/.dotnet/dotnet run --file scripts/client.cs -- check --client /home/nill/.config/Ankama/Dofus-dofus3
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- snapshot --root "$ANKAMA_CONFIG_ROOT" --output "$ANKAMA_BEFORE"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- snapshot --root /home/nill/.local/share/D3B/client --output "$D3B_BEFORE"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- backup --root "$ANKAMA_CONFIG_ROOT" --output "$BACKUP"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$BACKUP/tree" --manifest "$BACKUP/manifest.json"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$ANKAMA_BEFORE"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root /home/nill/.local/share/D3B/client --manifest "$D3B_BEFORE"
~~~

Require every snapshot and verification to succeed. Record the backup and manifest hashes and modes. Do not restore or
write client configuration during preflight, and do not modify Ankama files during this mission. After the session,
compare the tree with the before manifest:

~~~sh
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- compare --root "$ANKAMA_CONFIG_ROOT" --before "$ANKAMA_BEFORE" --after-output "$OBS/ankama-after.json"
~~~

If it changed, restore from the verified backup and verify against the before manifest:

~~~sh
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- restore --backup "$BACKUP" --manifest "$BACKUP/manifest.json" --root "$ANKAMA_CONFIG_ROOT"
/home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$ANKAMA_BEFORE"
~~~

Record the comparison and any restore result. A missing, failed, or unverified comparison, restore, or verification is
**BLOCKED**.

## 6. Pre-start process and listener gates

Before any separately authorized start, confirm that no Dofus, zaap, D3B host/app, diagnostic engine, test, or build
process is running. Confirm that no listener exists on the planned relay/game ports. Confirm that captures are disabled
and remain absent. Require private modes: directories 0700 and session database, manifests, reports, and logs 0600.
Any failed or unverified gate is **BLOCKED**; do not start.

## 7. Start and observation-only command set

The following is the prepared host shape for a future, separately authorized passive observation. It is not an
instruction to start a host in this S2A documentation task. Replace the private path variables only with verified
private locations. Keep activity, profile, and schedule settings absent.

~~~sh
"$APPHOST/D3B.Host" \
  --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
  --Data:Directory /home/nill/.local/share/D3B/data \
  --Storage:Path "$SESSION_DB" \
  --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
  --Network:Captures:Enabled false \
  --Fights:Alone false \
  --Diagnostic:Enabled true \
  --Diagnostic:EnginePath /home/nill/.local/share/D3B/diagnostic/d3b-diagnostic \
  --Diagnostic:ReportEnabled true \
  --Diagnostic:ReportDirectory "$REPORTS" \
  --Diagnostic:ReportMaxBytes 4194304 \
  --Diagnostic:IncludeReadable false \
  --Network:Address 127.0.13.59 \
  --Network:Ports:0 5555 \
  --Network:Game:Ports:0 5556 \
  --Supervised:ElementUse:Enabled true \
  --Supervised:ElementUse:ActionEnabled false
~~~

Before launching the client, require only the planned listeners on 127.0.13.59:5555 and 127.0.13.59:5556. Check
the client configuration again and require that it names D3B's configuration and the expected listener. If it names
Ankama's configuration, shows no expected listener, or shows an unexpected listener, stop and record **BLOCKED**.
Launch the client manually through the Ankama/Zaap UI only after those checks pass; select and log in to the character
manually.

Allowed passive commands:

~~~text
p2j element candidates
p2j element readiness <mapId> <cell> <elementId> <skillId>
~~~

Run candidates first; it takes no ElementId and lists candidates on the current map. Issue readiness only for an exact
candidate selected from that output. Both commands are passive: they read state and tables, send nothing, move nothing,
start no activity, and do not consume the one-shot. Forbidden throughout observation are p2j element use,
Supervised:ElementUse:ActionEnabled=true, movement, clicks, activities, HarvestActivity, a second relay or planner,
mapping changes, and resolver promotion.

## 8. Reading the output and recording the observation

The command prints one header followed by zero or more candidate lines, in ascending ElementId order. All fields are
space-separated key=value tokens. Booleans are true/false, missing integers are unknown, and enum values use their
C# names.

Header shape:

~~~text
P2J ELEMENT CANDIDATES version=1 reason=<code> session_epoch=<n|unknown> state_revision=<n|unknown> connected=<true|false> session=<true|false> translation_blocked=<true|false> paused=<true|false> acting=<true|false> input_mode=<name|unknown> action_enabled=<true|false> consumed=<true|false> map=<id|unknown> cell=<n|unknown> provenance=<name|unknown> trusted=<true|false> candidates=<n> target=<TARGET_IDENTIFIED|TARGET_AMBIGUOUS|TARGET_UNKNOWN> action=<ACTION_READY|ACTION_BLOCKED> action_reason=<code>
~~~

When no snapshot is available, session/revision/map/cell/provenance/input fields are unknown and connection, session,
latch, paused, acting, and trusted flags are false. The reason is READY, NO_CONNECTED_BOT, or AMBIGUOUS_BOT.
The report's action_enabled and consumed fields reflect the action gate and one-shot state; candidates never consumes
that state.

Candidate shape:

~~~text
P2J ELEMENT CANDIDATE version=1 element=<id> map=<id|unknown> server_known=<true|false> pack_known=<true|false> interactive=<n|unknown> server_cell=<n|unknown> pack_cell=<n|unknown> gfx=<n|unknown> state=<n|unknown> skill=<n|unknown> instance=<n|unknown> skills=<csv|unknown> skill_enabled=<true|false|unknown> catalog=<Matched|Partial|Unmatched|Unknown> item=<n|unknown> item_name=<"name"|unknown> possible_items=<csv|unknown> min_level=<n|unknown> uses_from_current_cell=<true|false|unknown> identity=<Verified|Inferred|Ambiguous|Unknown> identity_source=<server+catalog|pack+graphics+catalog|server|pack+graphics|pack|unknown> cell_conflict=<true|false> visual_identity=unconfirmed prep=<Ready|Blocked|Unavailable> prep_reason=<code>
~~~

skills and possible_items are ascending comma-separated ids, or unknown when empty. item_name is unknown or
double-quoted; backslashes and double quotes inside the name are escaped.

Interpret the provenance fields as follows:

- Server-observed evidence: server_known, server_cell, state, skill, instance, skills, and skill_enabled.
- Pack evidence: pack_known, interactive, pack_cell, and gfx.
- Job/item catalog evidence: catalog, item, item_name, possible_items, and min_level.
- Range and consistency: uses_from_current_cell and cell_conflict.
- Identity: Verified is server-plus-catalog protocol identity; Inferred comes from labelled graphics hints;
  Ambiguous means item or cell evidence conflicts; Unknown means no reliable item identity. identity_source names the
  evidence route. Verified is never visual identity, and visual_identity is always unconfirmed.
- Per-candidate preparation: prep and prep_reason. A Ready candidate does not open the action gate.

A server-observed element whose skills were not sent, or a pack-only element whose graphic the host has not learned, is
listed as identity=Unknown. This is a diagnostic possibility, not a claim that the element is harvestable; it can make
the target ambiguous. interactive=-1 means the server did not name the interactive; it is not a usable identifier.

The report's target verdict is structural: TARGET_IDENTIFIED means exactly one candidate has Verified protocol/catalog
identity. It does not include position trust or visual identity, and it does not establish a visible resource's name.
An untrusted position can still be TARGET_IDENTIFIED while the action verdict is ACTION_BLOCKED with a GUARD_* reason.
For the operator's resource identification record, require exactly one verified server-plus-catalog candidate, exactly
one gathered item, no cell conflict, a trusted position recorded separately, and a matching manual visual observation.
The command has no visual input: its target verdict or a GfxId alone does not prove that a visible tree is Walnut.
Record map label, tooltip, visible screen position, and a private screenshot hash separately. If candidates remain
plural, record TARGET_AMBIGUOUS and do not choose arbitrarily; the operator may record a manual correlation with one
candidate separately, without changing the command's ambiguous verdict. If structural or visual evidence is
insufficient, record TARGET_UNKNOWN.

ACTION_READY concerns action availability only. It must not be claimed with ActionEnabled=false, a closed translation
latch, an untrusted position, or an ambiguous target. During S2B, ActionEnabled remains false, so record ACTION_BLOCKED
and its action_reason even if target identity is established. Neither verdict grants permission to issue p2j element use.

Copy and complete this private relevé; do not put it in Git:

~~~text
session_epoch=<...>
state_revision=<...>
client_version=<...>
client_build=<...>
apphost_version=<...>
apphost_sha256=<...>
map=<...>
cell=<...>
provenance=<...>
trusted=<true|false>
translation_blocked=<true|false>
action_enabled=false
candidates=<n>
target=<TARGET_IDENTIFIED|TARGET_AMBIGUOUS|TARGET_UNKNOWN>
action=<ACTION_READY|ACTION_BLOCKED>
action_reason=<code>
candidate: element=<...> server_known=<...> pack_known=<...> interactive=<...> server_cell=<...> pack_cell=<...> gfx=<...> state=<...> skill=<...> instance=<...> skills=<...> skill_enabled=<...> catalog=<...> item=<...> item_name=<...> possible_items=<...> min_level=<...> uses_from_current_cell=<...> identity=<...> identity_source=<...> cell_conflict=<...> visual_identity=unconfirmed prep=<...> prep_reason=<...>
visual_observation: map_label=<...> tooltip=<...> screenshot_sha256=<...>
confidence: VERIFIED | OBSERVED | INFERRED | UNKNOWN
~~~

The historical reference scenario is map 120063492, character cell 103, a visible Walnut tree, and Lumberjack level 40
(P2O owner-confirmed). It is reference context only, never a command default or a value to copy into a new observation.
S2B must re-observe and re-record current values; the feature embeds none of them.

## 9. Identifying the Walnut and resolving ambiguity

Compare the manual observation with the candidate's gfx, pack_cell, and server_cell, and record the visible map label,
tooltip, and screen position. A GfxId or GraphicSkills hint alone is not visual proof and must never be written as
Walnut confirmed. If several candidates remain possible, retain TARGET_AMBIGUOUS; do not select one by name or
historical session. The operator may record a manual correlation between the screenshot and one candidate, but that
manual correlation is a separate observation and does not change the command's ambiguous verdict. If evidence cannot
structurally and visually identify the element, retain TARGET_UNKNOWN and **BLOCKED**.

## 10. Stop conditions and first latch-closing message

Stop on the first inconsistency. At minimum, stop for:

- Any unread, non-allowlisted carried message that closes the movement translation latch.
- A build, schema, mapping, diagnostic-engine, apphost, or client-tree mismatch.
- A map or cell without trusted server provenance, or a stale epoch/revision.
- Multiple or absent bot sessions, or inconsistent session state.
- Contradictory server/pack cells, an unknown catalog match, or several gathered items; record the ambiguity and stop.
- Any D3B-originated game frame, movement, click, activity, p2j element use, or opening of the action gate.
- An unexpected listener, capture file, diagnostic/report error, or client configuration change.

When the translation latch closes, privately record the first message that triggered it:

~~~text
first_latch_closing_message:
  timestamp=<...>
  direction=<FromClient|FromServer>
  type=<type URL or obfuscated token>
  decoded=<fully|partially|not>
  carrier=<...>
  client_version=<...>
  client_build=<...>
  evidence_sha256=<...>
~~~

Do not extrapolate, promote a resolver, change a mapping, or disable the latch. If evidence is insufficient to process
the message, keep the result **BLOCKED**.

## 11. Shutdown, comparison, restoration, and evidence

Close Dofus through its UI and stop the host with Ctrl+C. Wait for the bounded diagnostic flush. Confirm all processes
and listeners are gone, then rerun the read-only client check and require Ankama's configuration. Compare the Ankama
tree against the before manifest; if changed, restore only from the verified backup and verify the restored tree
against the before manifest. A failed or missing comparison, restore, or verification is **BLOCKED**.

Confirm captures remained disabled and absent; set Supervised:ElementUse:ActionEnabled=false and verify it. Keep logs,
screenshots, reports, manifests, session database, and hashes outside Git. Do not record tokens, account/character
names, connection identifiers, private inventory, or other secrets. Finish with separate results for resource
identification and action availability. No retry is allowed in the same session.
