# P2L future GO 1 passive diagnostic runbook

**Status: NOT EXECUTED in this mission.** No Dofus client, D3B host, relay, Ankama connection, live report, movement, or harvest was run. This is a future supervised procedure and does not grant the owner GO required to start it.

## Preflight — all gates must pass

1. **Get a separate owner GO and keep Dofus closed.** Confirm the requested scope is passive observation only. The installed client must still be `3.7.13.17`. If its version changed, STOP and treat the new build first. Recheck the client version/build date, Linux architecture, Unity version, metadata version, executable, `GameAssembly.so`, `UnityPlayer.so`, and metadata hashes using [BUILD_IDENTITY.md](BUILD_IDENTITY.md) and [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md). Keep the raw Ankama `version` file private because it contains the original `ConfigUrl`.

2. **Recheck runtime identity and permissions.** Verify the installed pack, schema, mapping, pins, readable protocol identity, apphost launcher, managed payload, and informational version. Re-record SHA-256 hashes and compare them with the current measured record in [RUNTIME_ARTIFACTS.md](RUNTIME_ARTIFACTS.md); do not rely on a version string or copy an old hash into a new run record. Data and apphost directories must be private (`0700`), data and managed payload files `0600`, and the launcher `0700`. `getcap` must be empty. The live P2J apphost recorded by R3 had 369 files and no `D3B.Diagnostic.dll`, so it is pre-P2K and must be republished and reviewed before a GO 1 host start.

3. **Check process, listener, and redirect state.** Confirm no Dofus, D3B host, or diagnostic-engine process is running. Inspect `ss -ltnp` and ensure no unexpected listener occupies the planned `127.0.13.59:5555` or `127.0.13.59:5556` endpoints. Run the read-only `scripts/client.cs -- check` command from [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md); do not start while the client is redirected to D3B. If any process, listener, or redirect state is unexpected, stop and resolve it under separate authorization.

4. **Make and verify a private client configuration backup before any redirect/configuration change.** A matching `go1-20261104T182159Z/version.original` backup was found by R3; verify its mode and that it matches the current version file. Create a fresh timestamped private backup for the new session as well. Store the `version` file under `~/.local/share/D3B/private-backups/` with directory mode `0700` and file mode `0600`; compare bytes and hashes locally. Never paste the file contents or original URL into shared logs or the bilan.

5. **Prepare the engine and apphost offline.** The current P2J apphost has no named backup under `private-backups`, so create a complete verified backup before any later install. Publish the integrated apphost and build Rust from audited source as described in [P2L_DEPLOYMENT.md](P2L_DEPLOYMENT.md). R3 recorded Rust workspace root `rust/`, toolchain `1.83.0`, crate version `0.1.0`, and binary name `d3b-diagnostic`; the engine hello does not contain a schema hash because the C# client supplies and validates it. Use a reviewed local executable at `/home/nill/.local/share/D3B/diagnostic/d3b-diagnostic` and verify its SHA-256 and private mode before the run.

6. **Check that no automatic activity is configured.** There must be no profile, activity, schedule, automatic command, movement, or harvest action configured. Do not run `p2j move`. Keep `Fights:Alone=false` and classic captures disabled for the entire procedure.

## Host command and exact settings

After the preflight gates pass and the owner GO is recorded, start the reviewed host in the foreground as the normal user. Leave all activity/profile/schedule settings absent.

```sh
/home/nill/.local/share/D3B/apphost/D3B.Host \
  --Client:Directory /home/nill/.config/Ankama/Dofus-dofus3 \
  --Data:Directory /home/nill/.local/share/D3B/data \
  --Network:Redirect:Directory /home/nill/.local/share/D3B/client \
  --Network:Captures:Enabled false \
  --Fights:Alone false \
  --Diagnostic:Enabled true \
  --Diagnostic:EnginePath /home/nill/.local/share/D3B/diagnostic/d3b-diagnostic \
  --Diagnostic:ReportEnabled true \
  --Diagnostic:ReportDirectory /home/nill/.local/share/D3B/diagnostic/reports \
  --Diagnostic:ReportMaxBytes 4194304 \
  --Diagnostic:IncludeReadable false \
  --Network:Address 127.0.13.59 \
  --Network:Ports:0 5555 \
  --Network:Game:Ports:0 5556
```

The report directory is mode `0700`; report files are mode `0600`. `IncludeReadable=false` limits observation to eligible unknown carried types. Do not enable classic capture files.

## Supervised procedure

1. Start the host in the foreground first. The supervisor checks the selected pack/schema/mapping identity and expected listeners before the client starts.
2. Launch Dofus manually through the Ankama/Zaap UI. Do not use a shell launch command or automated launcher.
3. Observe only whether the client connects and reaches its first placement. Stop on an unexpected prompt, error, reconnect loop, listener change, or any automatic character action.
4. Let the local Rust engine analyze eligible unknown message types. Relay behavior remains under the existing host path; do not intervene in translation or mapping.
5. Perform only manual observations explicitly authorized for this session. Do not issue a bot command, route, movement, harvest, or combat action.
6. Close the client through its UI, then stop the host with Ctrl+C. Allow bounded observer/session shutdown to finish.
7. Verify the client configuration has been restored to the Ankama state. Compare the current version-file bytes/hash with the private backup; use the `client.cs -- check`/`restore` procedure in [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md) only when its state check requires restoration. Confirm listeners and D3B processes are gone.
8. Produce the redacted diagnostic report and a local resolver report. Keep both in private directories. The resolver command is:

   ```sh
   REPORT=/home/nill/.local/share/D3B/diagnostic/reports/<diagnostic-report-file>.json
   SCHEMA=/home/nill/.local/share/D3B/data/3.7.13.17/schema-1.binpb
   OUT=/home/nill/.local/share/D3B/diagnostic/reports/candidates-3.7.13.17.json
   /home/nill/.dotnet/dotnet run --file scripts/diagnose.cs -- resolve \
     --report "$REPORT" \
     --schema "$SCHEMA" \
     --client-version 3.7.13.17 \
     --out "$OUT"
   ```

   If reviewed local references are available, add `--sns <local-Mapping.v2.json>`, `--billbot <local-game_mappings_detailed.json>`, `--corpus <local-corpus.json>`, and/or `--pins src/D3B.Protocol/Pins/3.7.13.17.json`. Reference files are read-only and their build provenance remains visible. The CLI verifies that report, schema, and requested client version identify the same build.

9. Classify unresolved blockers by priority from the candidate JSON, retaining each candidate's evidence, contradictions, uncertainty, coverage, drops, and provenance. Do not change the mapping during or after this session as part of GO 1.

Every lost observation, dropped frame, timeout, sidecar/engine error, report-write failure, or `coverage="partial"` result must appear in the sanitized bilan. Include the build identities and observed result; omit payloads, tickets, account/character identifiers, original URLs, and private messages. A report from this run is **MANUAL/SUPERVISED** evidence only.

## Stop conditions and GO boundary

STOP before or during the run if the client is not `3.7.13.17`, any binary/schema/mapping/apphost identity differs from the reviewed record, the P2K-capable apphost or local engine is absent, a private backup cannot be verified, the redirect pointer is unexpected, a required listener is occupied, or any automatic activity is enabled. If the sidecar times out/crashes, a report write fails, observations are dropped, coverage is partial, or the client/host behaves unexpectedly, close the client and stop the host; do not retry within the same session. Report the evidence as partial and review offline.

This procedure ends at **READY FOR P2L LIVE DIAGNOSTIC — AWAITING OWNER GO** only after the offline build/staging and all preflight review are complete. Any failed gate is **BLOCKED** with its exact cause. The current mission itself remains **NOT EXECUTED**. GO 1 never authorizes GO 2, movement, harvest, mapping promotion, or a push.
