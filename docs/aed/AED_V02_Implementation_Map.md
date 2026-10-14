# AED v0.2 Rev02 implementation

Base: `10391b1d820a9f25d2352d925dbf5bfc39962e50` (`origin/LTP`, fetched 2026-10-14).
Feature: `feature/aed-v02-full-integration`. Owner approved A–G for the pilot in the implementation request. Production activation and merge are not authorized.

## Work and verification map

| Slice | Existing paths to extend | Verification | Status |
|---|---|---|---|
| W0 baseline and tooling | `scripts/`, Unity Test Runner, `Test-KLTNBackend` | Full backend / EditMode / PlayMode; artifacts | Backend 486/486; EditMode 1203/1203; PlayMode 190/190 |
| W1 versioned sources / decision ledger / receipts | `AppDbContext`, `ScenariosController`, `ScenarioAdaptivePlanV2Service`, Phase8 verifier and accepted telemetry repository | Real verifier, SQLite/PostgreSQL migration, replay/stale/timeout | In progress |
| W2 pilot policy / pressure / budget / windows | Existing AED common models, `AEDv2Authority`, `AEDv2BoundaryCoordinator`, `NetworkMatchState`, `MatchAuthorityRuntime` | Deterministic policy/simulation, two same-phase windows, progression fault injection | Pending |
| W3 Stalker fairness / P4 / sensory | `AdaptiveStalkerTargetPolicy`, sensors, `StalkerUnityHideSpotAdapter`, `StalkerHidingInvestigation`, `NetworkPlayerMovement` | PF/VH/HB/HT/CC/SK, protection zero-call tests, Fixed regression | Pending |
| W4 Cabinet Ambush | `StalkerController`, existing Special / jump entry / route / presentation paths | JP/CAB/SIM, cue/route abort, idempotency, client rendering | Pending |
| W5 Minion / Tool / markers | `PlayerSpawner`, `AEDv2GameplayBridge`, `TeamToolWorldSpawn`, scene marker and route authoring | MN/TT, 5 required tools, optional reservations, real census, NavMesh/door tests | Pending |
| W6 delivery | feature PR into LTP, reproducible local config / player build / trace artifacts | ACC and 12 E2E IDs; no merge | Pending |

The approved Rev02 sections XV–XVIII override earlier candidate values. Canonical Telemetry 1.1 and `AED_V2_PHASE_EVIDENCE_V2` retain their meanings. New evidence and decisions require a new version; V1/V2 verification stays fail-closed.

## Final human checks

`E2E-P4-01..03`, `E2E-CAB-01..03`, `E2E-PP-01..02`, `E2E-Z3-01`, `E2E-TT-01`, `E2E-F4-01`, `E2E-AED-01` remain NOT RUN until actual Host/Client scene evidence exists. Automated tests do not imply these checks passed.
