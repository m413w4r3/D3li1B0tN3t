# P2O passive observation runbook

**Status: PREPARED / NOT EXECUTED / OWNER GO REQUIRED.** This runbook records preparation only. It is not authorization. Do not execute it without a distinct, explicit owner GO for passive observation. Live action remains separately BLOCKED under D340.

## Private paths and identities

Use these existing private paths:

~~~sh
OBS=/home/nill/.local/share/D3B/p2o-observation-20261109T080730Z
APPHOST="$OBS/apphost-p2o-b82780a"
SESSION_DB="$OBS/session.db"
ANKAMA_BEFORE="$OBS/ankama-before.json"
D3B_BEFORE="$OBS/d3b-before.json"
REPORTS="$OBS/diagnostic-reports"
UI="$OBS/ui"
BACKUP=/tmp/p2o-preflight-f.0X4Q1S/ankama-backup
ANKAMA_CONFIG_ROOT=/home/nill/.config/Ankama/Dofus-dofus3
~~~

Require the exact identities in [P2O_PREFLIGHT_3_REPORT.md](P2O_PREFLIGHT_3_REPORT.md) before each run:

- Client: Ankama 3.7.13.17, build 2026-11-03T14:11:23+00:00, ConfigUrl https://dofus2.cdn.ankama.com/config/dofus3.json, Linux x86_64, Unity 6000.3.16f1, metadata version 39. Match the report’s hashes for Dofus.x64 (6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff), GameAssembly.so (f2aa597d4ca5550d4188d878f6dd577d79fbfc01615bc48f073a1c4d8d9c76a0), UnityPlayer.so (b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f), global-metadata.dat (dea17c3c465a1937737e848a290057db9812bc2e7f4f8176d8ae1bf2616cdc9d), and the version file (796d68f12d5278d7253818d3d4ff52e08b731307769c64e3355cc7a4e16f3fae). Re-verify the full 27,826-entry client tree against the private before manifest.
- Staged P2O apphost: 0.0.0-alpha.0.144+b82780a1f2cdf9dd6e1de2e2491e5042e8189f0f, 377 files, 116 M; full sorted SHA-256 manifest 87454f0aa8b9d30e40496645a066cf923e0aa8b8570355d426426bc86e307057. Match launcher de95655755f64725635fadefcd8a31e541d44d093edf519ff78785ddd964e1c0 and the managed payload hashes in the report. Require directory 0700, launcher 0700, payload 0600, and no file capability. Do not use the installed P2L apphost.
- Diagnostic engine: /home/nill/.local/share/D3B/diagnostic/d3b-diagnostic, 833,256 bytes, SHA-256 2ac365be2ac70cd5a3fe5e338179ab58cf1a83fdffedcf283e522b3d5200836b, mode 0700, no capability.
- Data: /home/nill/.local/share/D3B/data/3.7.13.17; data-8.pack SHA-256 28db5da4893ef7599740fb9001d74451b774ef7bd446c35a5b18977a6a87c4d0, schema-1.binpb e383c947b2c916c10d28636f73ebed0edfe556c61c3403dacf879b215be7e0b0, mapping-15.binpb f42deadf5d5d84e4e8c2213f6dd730a66e8a9324bacac264bcad4c3805eea97e; directory 0700, files 0600.
- The build check recorded for this preparation was dotnet build D3B.slnx --no-restore, 0 warnings and 0 errors. Recheck that the preflight manifest, session DB, and output directories are private. Disk was 719 GiB free on /home and /tmp (8% used).

## Pre-start gates

Run the file-based commands from the repository root. At the start of these gates, change to it:

~~~sh
cd /home/nill/perso/dofus/D3B_linux
~~~

With the owner GO recorded and Dofus closed:

1. Recheck the client version, build, file hashes, and Phase F manifest. Stop on any mismatch.
2. Check both client configurations without restoring or writing:

   ~~~sh
   /home/nill/.dotnet/dotnet run --file scripts/client.cs -- check --client /home/nill/.config/Ankama/Dofus-dofus3
   /home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$ANKAMA_BEFORE"
   /home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root /home/nill/.local/share/D3B/client --manifest "$D3B_BEFORE"
   ~~~

   Require the client check to exit 0 and say it names Ankama’s configuration. The D3B config.json target at 127.0.13.59:5555 and :443 is inactive while Ankama’s version file names Ankama’s configuration. Do not restore or write during preflight.
3. Confirm no Dofus.x64, zaap, D3B.Host, D3B.App, d3b-diagnostic, diagnose, D3B.Bot.Tests, dotnet-test, or D3B build process is running. Confirm no listener on 127.0.13.59:5555, :443, or :5556.
4. Confirm ~/.local/share/D3B/app.json and ~/.local/share/D3B/captures remain absent. Captures must stay disabled. Recheck the staged session DB: 7 profiles, 1 character, 0 Characters.StartsOnConnection=1, 0 Characters.ProfileId set, and 0 Runs. Require the staged DB and manifests at 0600 and private directories at 0700.
5. Recheck the staged apphost full sorted manifest, engine hash, and data hashes above. The apphost --help probe initialized 10 modules and exited before services because its default client directory is wrong on this Linux host. The command below supplies the correct client directory.
6. Create only the private output directories if absent:

   ~~~sh
   install -d -m 0700 "$REPORTS" "$UI"
   ~~~

Any failed or unverified precondition is **BLOCKED**. Do not start the host.

## Start and observe

Start the host in the foreground. Keep all activity/profile/schedule settings absent.

~~~sh
"$APPHOST/D3B.Host" --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 --Data:Directory /home/nill/.local/share/D3B/data --Storage:Path "$SESSION_DB" --Network:Redirect:Directory /home/nill/.local/share/D3B/client --Network:Captures:Enabled false --Fights:Alone false --Diagnostic:Enabled true --Diagnostic:EnginePath /home/nill/.local/share/D3B/diagnostic/d3b-diagnostic --Diagnostic:ReportEnabled true --Diagnostic:ReportDirectory "$REPORTS" --Diagnostic:ReportMaxBytes 4194304 --Diagnostic:IncludeReadable false --Network:Address 127.0.13.59 --Network:Ports:0 5555 --Network:Game:Ports:0 5556
~~~

Before launching the client, verify that the only listeners are 127.0.13.59:5555 and 127.0.13.59:5556. Stop the host if any other listener appears or either expected listener is absent. In a second terminal, from the repository root, run this read-only check:

~~~sh
cd /home/nill/perso/dofus/D3B_linux
/home/nill/.dotnet/dotnet run --file scripts/client.cs -- check --client /home/nill/.config/Ankama/Dofus-dofus3
~~~

Require the check to report that the version file names D3B’s configuration and that D3B listens on 127.0.13.59. If it still names Ankama’s configuration, reports nothing listening, or shows any unexpected listener, stop the host and record **BLOCKED**. This check is read-only; do not restore or write any client configuration. Only after it passes, launch Dofus manually through the Ankama/Zaap UI, then log in and select the character manually. Do not launch it from a shell or automate the UI.

Observe without input. Use the existing diagnostic report, host logs, manual UI screenshots saved privately under $UI with timestamps, and existing map/inspect tooling. Classic network capture files remain disabled; protocol-to-UI correspondence means correlating observed protocol/report events with manual UI evidence.

| Observation goal | Evidence to record | Stop condition |
| --- | --- | --- |
| Effective character connection | Host logs and diagnostic report session/connection fields; manual UI confirmation of the selected character. | No effective character session, missing identity evidence, or inconsistent connection state. |
| Session and build identification | Diagnostic report and host log session/build identity, checked against the client and staged runtime identities above. | Any build, session, schema, mapping, or engine mismatch. |
| Current map and server position | Diagnostic/report fields and parsed server messages with provenance; compare current map and server-confirmed position with existing map data. | Position lacks server provenance, is stale, unknown, or contradicts the map/session state. |
| Protocol-to-UI correspondence | Timestamped protocol/report event and a manual screenshot of the corresponding UI state. | Event and visible state disagree, or timestamps/evidence cannot be correlated. |
| Visible harvest elements | Manual screenshot plus existing map/inspect data for the visible elements. Observe only; do not click or harvest. | Element is not clearly visible or cannot be distinguished from map/UI evidence. |
| ElementId, CellId, GfxId, and screen position | Existing report/server-message fields, map data, and manual screenshot showing the screen position. Record unknown fields as unknown. | IDs or position are absent, ambiguous, contradictory, or would require a mapping change to reconcile. |
| Map changes and transitions | Server-message provenance and diagnostic/report map fields, correlated with a timestamped UI screenshot and existing map data. | Transition is unexplained, map identity is uncertain, or position evidence is stale. |
| Unknown messages and real diagnostic coverage | Diagnostic report coverage, unknown-message metadata, drops, and engine status; corroborate with host logs. | Any unknown unallowlisted message, partial/absent coverage, dropped observation, or report/engine error. |

Hard stop on any inconsistency, closed translation latch, build mismatch, unexpected listener, capture file, automatic profile/activity, report or engine error, unexpected client behavior, or automatic movement/harvest. Do not click, move, harvest, emit a game message, start an activity/profile, change a mapping, or promote a resolver candidate. Stop immediately and preserve the private evidence if a gate closes.

## Shutdown and restore check

1. Close Dofus through its UI. Stop the foreground host with Ctrl+C and wait for the bounded diagnostic flush to finish.
2. Confirm Dofus, host, relay, engine, and diagnostic processes are gone and the three planned ports have no listeners. Re-run the read-only client check and require Ankama’s configuration.
3. Compare the client tree with the original manifest:

   ~~~sh
   /home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- compare --root "$ANKAMA_CONFIG_ROOT" --before "$ANKAMA_BEFORE" --after-output "$OBS/ankama-after.json"
   ~~~

4. If it changed, restore from the verified Phase F backup with the current preflight tool, then verify against the original before manifest:

   ~~~sh
   /home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- restore --backup "$BACKUP" --manifest "$BACKUP/manifest.json" --root "$ANKAMA_CONFIG_ROOT"
   /home/nill/.dotnet/dotnet run --file scripts/preflight.cs -- verify --root "$ANKAMA_CONFIG_ROOT" --manifest "$ANKAMA_BEFORE"
   ~~~

   If the backup is absent or comparison, restore, or verification fails, record **BLOCKED** and stop for manual recovery.
5. Keep diagnostic reports, logs, manifests, database, and UI evidence private. Do not add them to Git or commit them.

This runbook is prepared but not executed. Its existence does not authorize a session; execution requires a distinct owner GO.
