# P2E — supervised Linux client session (Dofus 3.7.13.17)

**Result: PASS** for connection and relay, authentication, character selection, the supervised game session, and stop/restore. Game-frame decoding was mixed, and the session did not validate the host's map or cell against the visible client. This report records aggregate evidence only; it contains no raw log lines, connection or character identifiers, tickets, payloads, account identifiers, or private messages.

## A. Scope, build identity, and timeline

P2E exercised the prepared Linux client build `3.7.13.17` with the P2D runtime artifacts. The recorded client identity is Linux x86_64, Unity `6000.3.16f1`, IL2CPP metadata version `39`; the corresponding pack, schema, mapping and apphost identity are recorded in [BUILD_IDENTITY](BUILD_IDENTITY.md) and [P2D_REPORT](P2D_REPORT.md). This was a supervised manual-client session, not bot movement or automated activity.

| Step | Observation |
| --- | --- |
| Preflight | The supervised run used the `3.7.13.17` identity and its prepared runtime. The private version-file backup was kept outside the repository. |
| Connection | The client connected through the relay; authentication and character selection completed. |
| Game session | The game connection remained open for `00:04:27.4068308`. |
| Frames | 848 game frames were logged: 499 understood, 139 read/partial, and 210 not read. |
| Stop | The relay connection closed; the supervisor stopped the client and host and restored the client's own configuration. |

The host recorded no D3B-injected frame. No D3B activity or D3B movement was exercised; movement traffic in the session was manual client traffic.

## B. Results by layer

| Layer | Result | Evidence |
| --- | --- | --- |
| Connection and relay | **PASS** | Connection established through the relay and closed normally after the game session. |
| Authentication | **PASS** | Authentication completed. |
| Character selection | **PASS** | Character selection completed and the game connection followed. |
| Game session | **PASS** | 4 min 27 s (`00:04:27.4068308`) observed; no D3B activity or injected frame. |
| Game frames | **PARTIAL** | 848 total: 499 understood, 139 read/partial, 210 not read. These are message-level results, not a claim that every field was available. |
| Stop and restore | **PASS** | Relay closed; the final host restoration message said the client's own configuration was restored and clients connect straight to Ankama. |

## C. Decoding limits

The aggregate frame states were 499 **understood**, 139 **read/partial**, and 210 **not read**. Separately, the log contained 349 `NotUnderstood` warnings, with repeated partial movement messages. The warning total is a separate log count; it is not an additional frame bucket. A partial message can still decode mapped fields that a consumer uses, while unmapped or unavailable fields remain absent. The word “partial” therefore does not by itself mean that the whole message was unusable.

The character-state consumer's relevant field use is visible in `src/D3B.Bot/Characters/CharacterEvents.cs`:

- `MapCurrentEvent.MapId` updates the current map and clears the cell until placement is supplied (`:103–118`).
- A matching `MapInformationEvent` supplies `SubAreaId` and sets the current character's `Cell` from its valid `Disposition.CellId`; absent or invalid self placement clears `Cell` rather than retaining the prior value (`:120`, `:191–215`, cell range `:321–324`).
- A self `MapMovementEvent` is accepted only when the character has a known cell and the path begins at that cell. It stores the path and its last cell as `LastWalkEnd`; it does not set the current `Cell` (`:131`).
- `MapMovementConfirmResponse` sets `Cell` to the last stored path cell and clears the path while a walk is pending (`:142–143`).
- `MapMovementRequest` and `MapMovementConfirmRequest` have no matching state update in `CharacterEvents`; their presence alone is not server confirmation. The client cancel request is handled separately (`:84–90`).

**P2F update.** P2F also clears `Cell` when a refusal coordinate cannot be mapped, applies a refusal only when the current cell is known, and accepts a client `MapMovementCancelRequest` only during an active walk. These are later state guards, not claims about events seen in P2E. See [P2F_REPORT](P2F_REPORT.md) for the complete state and observability behavior.

These source paths describe what the current code would consume from successfully translated
fields. The P2E aggregate log proves message names and message-level read states and counts. It
does not expose the decoded map or cell values, prove which optional fields were populated in each
partial message, or show that a particular `CharacterState` update occurred. Runtime map/cell
correctness was unverified in P2E; the later owner-confirmed visual result is recorded in
[P2G_REPORT.md](P2G_REPORT.md).

## D. Restoration confirmation

The pre-session version-file backup was byte-for-byte checked against the original before redirect; the backup and original remain private. After the relay closed, the host's final restoration message reported that it restored the client's own configuration so clients connect straight to Ankama. The supervised stop/restore result was recorded as PASS.

The retained aggregate evidence proves normal relay closure and the restoration message. The supplied stop/restore result is PASS, but it does not include a post-stop process listing, listener snapshot, redirect check, or post-restore `cmp` result. Therefore the aggregate cannot independently prove the absence of every leftover process/listener or independently re-check the restored file byte-for-byte after stop. Keep those distinctions when reusing this evidence.

## E. Movement-related messages

| Message | Count and state | Meaning for `CharacterState` |
| --- | --- | --- |
| `MapCurrentEvent` | 4, all understood | Server map ID is consumed; the state clears its cell pending map information. |
| `MapInformationEvent` | 4, all read/partial | Can supply sub-area and the character's valid disposition cell, but the aggregate does not show whether those fields were present in each frame. |
| `MapMovementEvent` | 40, all read/partial | P2F stores a self path only from a known matching start cell; the announcement alone does not advance the current cell. |
| `MapMovementConfirmResponse` | 13, all understood | While a path is pending, the state advances its cell to the path's last cell and clears the path. The log has no cell value to verify. |
| `MapMovementRequest` | 17, all understood | Client-originated request; the request itself does not update the character's current cell. |
| `MapMovementConfirmRequest` | 13, all understood | Client-originated confirmation request; it has no direct `CharacterState` update. |

The aggregate contains no `MapMovementRefusedEvent` and no `MapMovementCancelRequest`. These observations are consistent with manual client movement, but they do not provide server-confirmed cell values in the log. No D3B movement was tested.

## F. Residual risks

- P2E did not log the observed map or cell, so visual agreement with the real client was not measured.
- `MapInformationEvent` and `MapMovementEvent` were only read/partial; their usable fields were not inspected in this aggregate report.
- No server-confirmed cell value is present in the retained log. The source describes how a valid server path and confirmation update state, not what happened to a particular runtime state.
- No D3B movement or automated activity was exercised. P2E validates manual relay traffic only.
- The 349 warnings add noise, including repeated partial movement messages.
- The client update guard remains important: a version or build-date change requires stopping and rechecking the client identity and matching runtime artifacts before another session. The full binary fingerprint remains a manual preflight gate.

## Next (P2E plan, later completed in P2G)

At the time of P2E, the next supervised validation was planned to use the host's transition logs
to distinguish server-observed placement and server-confirmed movement from estimated position,
then compare the map/cell with the visible client. That observation-only validation was
subsequently executed and passed in P2G; see [P2G_REPORT.md](P2G_REPORT.md). The reproducible
procedure is retained in [LINUX_ACCEPTANCE.md](LINUX_ACCEPTANCE.md#p2f-visual-validation-procedure).
P2E's authorization did not carry forward to that later session.
