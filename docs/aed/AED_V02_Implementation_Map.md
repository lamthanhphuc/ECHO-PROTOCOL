# AED v0.2 Rev02 implementation checkpoint

Base: `10391b1d820a9f25d2352d925dbf5bfc39962e50` (`LTP`). Feature: `feature/aed-v02-full-integration`. A–G are approved for the pilot by the owner; merge and production activation are not authorized.

**Status: partial implementation, NOT E2E ready.** Green suites do not supply missing contract semantics. See [remaining decisions](AED_V02_Remaining_Contract_Decisions.md), [source contract](AED_V02_Source_Contract_Rev02.md), [verification report](AED_V02_Verification_Report.md) and [scenario traceability](AED_V02_Traceability.md).

| Work | Implemented | Proof / remaining gate |
|---|---|---|
| W0 | Reproducible local test script, isolated PostgreSQL/MongoDB, Backend PR workflow, Windows Development build | Real local suites executed; hosted CI separately reported |
| W1 | Versioned API, SQL migration, distinct windows, independent world/custody/Minion/route/supply replay, approval/receipt reconciliation | Real Backend verifier integration; history and complete PP/TP remain unsupported |
| W2 | PreMatch/Boundary/SafeWindow, one-key transactions, budget, actual-live-Minions handoff guard, Host revision/lease/roster guards, retries/reset | Separate Backend, Host consumer and Fusion Single proofs; joined remote-client transaction UNVERIFIED |
| W3 | Physical Vision/Hearing, bounded target scoring, frozen encounter profile, reveal timing, known-clue PP3 priority, P4 guard/token lifecycle | Controlled-policy tests; real fully canonical pressure activation blocked; qualifying long-chase definition missing |
| W4 | Cabinet FSM in existing Special runtime, observed entry clue, real client audio/visual ACK, path/escape checks, timed inspection, abort/recovery/quota and bidirectional shared cooldown | Mechanics and real ingestion tested; native success blocked by unsupported team TP/incomplete PP |
| W5 | Adaptive Zone3 cap and population readback, no forced despawn; five required tools/zone, deferred R0 reservations, initial optional allowance, immutable materialized supply | Non-R0 placements and ordinary/emergency support unfinished pending formal semantics |
| W6 | Feature source/test/report, draft PR into LTP, defaults OFF | Twelve E2E scenarios NOT RUN; no merge or pilot activation |

## Boundaries

- Telemetry 1.1 and `AED_V2_PHASE_EVIDENCE_V2` retain their meanings; old Phase8 verification remains fail-closed. Unsupported is never relabeled Verified.
- Manifest `d3754db5cde76047c443b84a8fd5788076e00203cbeef57f66d155ce2f16e6c8`: 222 markers (112 tools, 107 cabinets, 3 zone entries), baked mesh and 20 doors.
- Scene feature changes: three zone-entry components and one Zone3 marker child at the existing transition. Stalker and Minion prefabs have no tuning diff versus `origin/main`.
- Fixed Normal Minion caps stay `1/2/0`. Feature gameplay requires Adaptive Normal. The Lobby scene/UI is preserved; LobbyManager adds v0.2 PreMatch staging/cleanup. Whole-tree LTP/main parity is not claimed.
- User RuntimeLogSettings/TMP fallback edits are preserved and excluded. No secrets, local Docker override, production flags or production data changed.

This checkpoint is not a completed pilot awaiting only human acceptance.
