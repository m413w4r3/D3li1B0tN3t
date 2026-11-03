# P2G — supervised visual validation (Dofus Linux 3.7.13.17)

**P2G VISUAL TEST : PASS — confirmation du superviseur reçue.**

P2G completed real-client, observation-only validation on Dofus Linux build `3.7.13.17`. The game
connection lasted 6 min 00.82 s. The supervisor confirms that every journaled event and position
matched the supervisor's observation of the game.

## Journal evidence and visual confirmation

Network events and server confirmations were observed in the D3B journals. The counts below
summarize those journal entries; coherence with the real client is attested by the supervisor and
is not inferred from the journals.

| Journaled event | Aggregate result |
| --- | --- |
| Map changes | 11 correctly observed. |
| Server placements | 11; the supervisor confirmed they were coherent with the client. |
| Server-confirmed movements | 23; the supervisor confirmed they matched the visible movements. |
| Client cancellation | 1; it was not confirmed by the server and was correctly distinguished. |
| Fight and return to roleplay | Both were correctly tracked. |

The last position was Banque d'Astrub, map `192415750`, cell `329`; the supervisor confirmed that
it matched the game observation.

No automated movement was executed. This was an observation-only session, and D3B emitted 0 frames. The stop and Ankama configuration restoration were validated.

This report is a sanitized summary. No private journal, identifier, payload, or capture is committed.

## What P2G does not prove

P2G does not prove automated movement safety and does not authorize D3B to move. It does not replace
the P2H offline trust work. The next live step, one supervised automated move to a single
neighbouring map, is **NOT authorized or executed by P2G**; it requires a new owner GO.
