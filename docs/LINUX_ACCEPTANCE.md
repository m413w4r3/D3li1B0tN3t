# Linux client acceptance runbook

**Status: MANUAL/SUPERVISED; no host or live-client attempt has been made.** The mapping audit now passes offline for Linux build `3.7.12.12` through the evidenced pin. The client-facing port plan remains unresolved, and this runbook does not claim live compatibility.

## 1. Scope and supervision boundary

**Action:** Treat this as a short, human-supervised connection check. A human must be present at the client and host for the entire run. Do not run unattended, as root, or with a bot activity enabled. Stop immediately on an unexpected account prompt, repeated reconnect, unexplained host error, missing restoration pointer, or any sign of character activity. Do not proceed past a failed prerequisite.

**Command:** None. This work updated the offline mapping gate only. A future live attempt still requires a resolved port plan, verified client restoration, and a present supervisor. No relay-only option exists today.

**Expected observable / oracle:** No live compatibility result is claimed. A run is acceptable only when every gate is satisfied and the client is restored afterward.

**Record:** Supervisor, date/time, intended scope, stop conditions encountered, and final gate result. Mark any real-client result **MANUAL/SUPERVISED**.

## 2. Prerequisites

**Action:** Before scheduling a live attempt, obtain the identity record, a passing mapping audit for this exact client build, a viable port plan, a verified config backup and restore procedure, and a human supervisor. Keep `Network:Captures:Enabled` off unless capture collection is explicitly approved; captures may contain login tickets.

Use the integrated repository's [build identity record](BUILD_IDENTITY.md), including client version/platform/Unity/metadata; SHA-256 for executable, `GameAssembly.so`, `UnityPlayer.so`, metadata, pack and schema; readable protocol and pins; and mapping identity if produced.

**Current gate status:**

| Gate | Status | Evidence / oracle |
| --- | --- | --- |
| Redirect mechanism | READY; verified offline | ConfigUrl redirect, pointer and restore behavior characterized; this is not live-client acceptance. |
| Relay simulated | PASS | Simulation only; not a native client result. |
| Mapping audit | **PASS offline** | Build `3.7.12.12` schema SHA-256 `e383c947…be7e0b0`; the evidenced `AuthenticationRequest` → `knc` pin audits 222 messages, 492 fields, 29 enums and 206 values with 0 failures, 2 notes and 100 partial. See [P2_AUTH_MAPPING](P2_AUTH_MAPPING.md). |
| Host start | **UNVERIFIED; unattempted** | `ProtocolSource.Load` succeeds offline in tests. The production host was not started; the client-facing port plan is unresolved. |
| Live client | **UNVERIFIED** | No real-client compatibility claim. |

The audit examined 222 messages, 492 fields, 29 enums and 206 values; all message and enum pairs are present. `D3B.Host` uses DefaultModules → ProtocolModule; NetworkModule also adds ProtocolModule and the relay decoder requires ProtocolTranslation. P2A completed the offline mapping fix and audit. The production host and relay remain unstarted; the port plan and live acceptance are still open.

Port plan: defaults are `Network:Address=127.0.13.59`, `Network:Ports=[5555,443]`, and `Network:Game:Ports=[5556]`. Redirect preserves `connectionHosts` ports and changes only the host; a high `Network:Ports` override alone may not match the client. No setting rewrites `connectionHosts` ports today. Least privilege is high relay ports plus a matching supported client rewrite (not currently available), or a narrowly scoped `cap_net_bind_service` on a dedicated D3B executable/apphost run as the normal user. Do not set that capability on shared `dotnet`; do not run the bot or client as root. A reverse proxy still binds 443 and adds a component. Do not apply capabilities in this task.

**Command:** No live-start command is currently authorized by the gates. See sections 6–7.

**Expected observable / oracle:** Do not schedule live execution until identity and mapping pass, and the client-facing ports have a demonstrated match.

**Record:** Identity artifacts and hashes, audit report, selected port plan and its evidence, config backup location/hash, supervisor, and capture approval state.

## 3. Verify client identity (read-only)

**Action:** Inspect the installed client read-only. Expected identity: Version `3.7.12.12`, BuildDate `2026-10-19T14:46:54+00:00`, Unity `6000.3.16f1`, metadata v39; `Dofus.x64` 4,472 bytes; `GameAssembly.so` 175,931,264 bytes; `UnityPlayer.so` 41,372,064 bytes; `global-metadata.dat` 43,605,684 bytes.

**Command:**

```sh
CLIENT=/home/nill/.config/Ankama/Dofus-dofus3
CLIENT_EXE="$CLIENT/Dofus.x64"
cat "$CLIENT/Dofus_Data/StreamingAssets/version"
sha256sum "$CLIENT_EXE" "$CLIENT/GameAssembly.so" "$CLIENT/UnityPlayer.so" "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
od -An -tu4 -j4 -N4 "$CLIENT/Dofus_Data/il2cpp_data/Metadata/global-metadata.dat"
```

Expected SHA-256 values: `Dofus.x64` `6d7b634873034db988b3eb0a3399e0effec7b491dd7c1fcd9efcd9f2c6d0baff`; `GameAssembly.so` `0af08ea7dcc7fd15907be5761ff8b0eab62ddb020fc38ad588c06bc7f2e34da8`; `UnityPlayer.so` `b01fc32db9ac268c6a99f5f105cfb5d52423b953030f17e8e106e7a23621649f`; metadata `fc135ad151e13adf297cc07b50c37680de4a367b8b757fdf958b4344c09d6c66`. The `od` value is the metadata version oracle and should be 39. Do not infer identity from the version string alone.

**Expected observable / oracle:** Version file, sizes, hashes and metadata version match the values above; Unity version is `6000.3.16f1`.

**Record:** Client directory, version/build date/platform/Unity/metadata, exact file sizes and SHA-256 values above. Record pack, schema, readable protocol/pins and mapping identities as available.

## 4. Inspect original client configuration (read-only)

**Action:** Before redirect, read the `ConfigUrl` line in `Dofus_Data/StreamingAssets/version`. Privately record the complete original URL and the file's SHA-256; do not put the URL or account-specific details in shared logs.

**Command:** With `CLIENT` set as in section 3: `cat "$CLIENT/Dofus_Data/StreamingAssets/version"` and `sha256sum "$CLIENT/Dofus_Data/StreamingAssets/version"`.

**Expected observable / oracle:** The version file is readable and has a `ConfigUrl=...` line. If unreadable, stop and repair through the Ankama launcher.

**Record:** Original URL in a private, access-restricted note; version-file hash and readability result in the shareable report. Redact the URL from shared evidence.

## 5. Back up and prove restore before redirect

**Action:** Make a byte-for-byte backup of the version file and record hashes for original and backup. Confirm the D3B redirect pointer state before changing anything. The redirect directory is `Network:Redirect:Directory`; redirect writes `config.json`, `config-url` (the original URL) and `ankama-config.json` there. Normal host stop restores the URL; abrupt host death does not. The restore command restores only if ConfigUrl still names D3B. A missing/unreadable `config-url` or unreadable version file requires launcher repair. `ClientVersionFile.WriteConfigUrl` writes in place; recovery from a partial write is **UNKNOWN**.

**Command:** Choose a private backup directory that is not shared, replace `<private-dir>` with its path, then run the following with `CLIENT` set as in section 3:

```sh
BACKUP=<private-dir>
mkdir -p "$BACKUP"
cp -p "$CLIENT/Dofus_Data/StreamingAssets/version" "$BACKUP/version.original"
sha256sum "$BACKUP/version.original" "$CLIENT/Dofus_Data/StreamingAssets/version"
```

The verified pointer check is `/home/nill/.dotnet/dotnet run --file scripts/client.cs -- check --client "$CLIENT" --redirect-directory <private-redirect-dir>`; replace the placeholder with the configured redirect directory. It reads the redirect pointer. Do not run `restore` yet.

**Expected observable / oracle:** Backup and original hashes match before redirect; `check` reports the current pointer/client state without redirecting. If check fails or the pointer is missing/unreadable, stop.

**Record:** Private backup path, original and backup hashes, check output/status with sensitive paths redacted, and the exact redirect directory.

## 6. Verify ports and listeners

**Action:** Confirm the chosen relay endpoints are available and match the client connection hosts. The default includes privileged TCP 443; a high-port host override alone is not evidence that the client will use those ports. Keep the process a normal user. Do not set capabilities automatically.

**Command:** `ss -ltnp` to inspect TCP listeners and `ss -ltnp 'sport = :443'` to inspect the privileged port specifically. Before host start, expected D3B listeners: none. After a future valid start, expected listeners must match the approved port plan.

**Expected observable / oracle:** No conflicting listener on planned ports before start; after a future valid start, all planned ports are bound. Do not use a shared `dotnet` capability or root as a workaround.

**Record:** `ss -ltnp` output before/after, user identity, selected address/ports, and evidence that each port matches the client's `connectionHosts`.

## 7. Controlled relay/host start — FUTURE / UNVERIFIED

**Action:** **No host start was performed for P2A.** Offline protocol preparation now passes, but the client-facing port plan remains unresolved. Proceed only in a separately supervised session after the port plan, identity record, redirect/restore plan, and all section 2 gates are reviewed. This runbook is not authorization to start the host.

**Command (FUTURE; not run in P2A):**

```sh
/home/nill/.dotnet/dotnet run --project src/D3B.Host -- --Client:Directory "$CLIENT" --Fights:Alone false
```

Set `Network:Redirect:Directory` explicitly to a private path for any future test. This command is not a go-ahead under the current blocker. No bot activity should be selected; `Fights:Alone=false`. This host composition is not transport-only.

**Expected observable / oracle:** On a future permitted run, host startup completes and expected listeners appear in `ss -ltnp`; logs show connection-phase message names and lengths without payloads. Game-frame log visibility is **UNKNOWN**. Any protocol, bind, or startup error means stop; do not proceed to the client.

**Record:** Exact binary/command/config and settings, audit result and mapping identity, exit status, sanitized startup logs, listener output, and any startup error. Keep captures disabled.

## 8. Supervised client connection and observation — FUTURE / UNVERIFIED

**Action:** Only after all gates pass, have the supervisor launch the installed client through the Ankama/Zaap UI. There is no verified shell launch command. Do not start an activity; keep `Fights:Alone=false`. Watch the host and client continuously. Stop at the first unexpected prompt, reconnect loop, protocol error, or sign of character activity.

**Command:** Ankama/Zaap UI launch; shell command **UNKNOWN**. Keep `Network:Captures:Enabled` off. Connection-phase logs expose message names and lengths without payloads. Game-frame logs are Debug-level and whether they will be visible is **UNKNOWN**. This observation does not make the host transport-only.

**Expected observable / oracle:** Future connection-phase-only test: client reaches the expected connection phase through the local relay, with no unexplained errors or activity. A relay-only composition cannot establish that D3B's game route works. No current live result or compatibility is claimed.

**Record:** MANUAL/SUPERVISED start/end time, client/build identity, supervisor observations, sanitized logs and outcomes. Never share login tickets, original ConfigUrl, credentials, or captures without explicit approval and sanitization.

## 9. Clean stop and restoration verification

**Action:** Log out through the client UI, then stop the host with Ctrl+C. Check that the original ConfigUrl is restored and compare the version-file hash with the pre-redirect hash. Do not assume clean stop restored it; verify.

**Command:** `/home/nill/.dotnet/dotnet run --file scripts/client.cs -- check --client <dir> --redirect-directory <dir>` reads the pointer. If still redirected, run `/home/nill/.dotnet/dotnet run --file scripts/client.cs -- restore --client <dir> --redirect-directory <dir>`, then run `check` again. Compare with `sha256sum <client-dir>/Dofus_Data/StreamingAssets/version` and the recorded original hash.

**Expected observable / oracle:** `check` reports the client no longer points at D3B; version file hash matches pre-redirect hash. Restore only acts when ConfigUrl still names D3B.

**Record:** Logout/stop times, host exit status, check/restore output and status, before/after version-file hashes, and final listener output. Redact private URLs and paths.

## 10. Interruption recovery and retained diagnostics

**Action:** If host or client exits unexpectedly, assume redirect may remain. Do not relaunch the client. Run `check`; if still redirected, run `restore` and check again. If `config-url` is absent/unreadable, or the version file is unreadable, repair through the Ankama launcher. Partial-write recovery is **UNKNOWN**; preserve the backup and use launcher repair rather than guessing at file contents. If restoration cannot be proved, leave the client closed and escalate to the supervisor.

**Command:** Use the `check` and conditional `restore` commands from section 9. Launcher repair is through the Ankama UI; shell command **UNKNOWN**.

**Expected observable / oracle:** Client ConfigUrl is no longer D3B, check passes, and original version-file hash matches. If not, recovery remains incomplete; do not claim acceptance.

**Record:** Sanitized logs, timestamps, host/client exit statuses, client identity hashes, original/backup/current version-file hashes, check/restore results, settings, `ss -ltnp` evidence, mapping audit, port/connectionHosts evidence, and a concise supervisor timeline. Do not retain or share captures by default; they may contain login tickets. Keep original URL and any sensitive data private.
