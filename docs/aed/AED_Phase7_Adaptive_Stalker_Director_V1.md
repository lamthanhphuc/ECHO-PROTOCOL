# AED Phase 7 — Adaptive Stalker Director V1

## Status

Research-only. Gameplay application disabled.

Uses the existing Normal `MatchDifficultyProfile` and `AEDv2Catalog` bounds. No changes are made to Stalker AI state, Fusion state, combat, Safe Room, or NavMesh behavior.

## Detection, pursuit and search

Research candidates cover detection acquire/forget, hearing multiplier, chase/patrol speed, search duration, player seeking, and core-carrier pursuit timing.

## Door and special encounters

Research candidates cover door break, special cooldown, post-chase/attack cooldowns, same-room cooldown, jump-entry reuse, and post-special cooldown. Special candidates require a downed trigger, two other eligible alive Players, Safe Room exclusion, complete NavMesh route, verified escape route, and the existing 7–10m jump distance.

## Safety and fairness

Host authority, current match/phase evidence, full roster verification, source verification, inactive combat action, weakest-player protection, and a 45-second increase cooldown are required. Target recommendations are deterministic, prefer lower recent pressure, protect recently targeted Players for 15 seconds, and cannot select downed, eliminated, Safe Room, unreachable, or illegal-LOS targets.

## Integration limitations

Phase 7 emits research proposals only. It does not call `AEDv2Authority`, `AEDv2BoundaryPolicy`, or `StalkerFusionRuntime`, and never applies gameplay. Phase 8 must provide verified evidence, authority/revision, action state, target receipts, and Backend approval. Phase 9 must calibrate thresholds and verify Host/Client behavior.

`CanApplyGameplay = false`.

## P7.6 — Integration Safety Integrity

### Phase transition contract

- CompletedEvidencePhaseOrdinal: ordinal of frozen evidence.
- TargetPhaseOrdinal: ordinal of next phase.
- TargetPhaseOrdinal = CompletedEvidencePhaseOrdinal + 1.
- Both ordinals must be valid and nonzero.
- MatchId must equal EvidenceMatchId.
- Invalid, stale or skipped transitions => HOLD.

At integration time:
- Read CompletedEvidencePhaseOrdinal from the frozen
  AEDv2CurrentMatchEvidence.PhaseOrdinal.
- Read TargetPhaseOrdinal from the authoritative
  NetworkMatchState transition.
- Never substitute the target phase ordinal for the
  completed evidence ordinal.

### Unsafe Stalker state

Research adjustment is forbidden during:
- CHASE
- ATTACK
- RECOVER
- Active Special Encounter

Target fairness must not override these states.

### Regression requirements

- Valid completed-to-next phase
- Same-phase mismatch
- Skipped phase
- Zero or overflowing phase ordinal
- Cross-match evidence
- Active CHASE/RECOVER/ATTACK/Special
- Unverified skill and weakest-player protection
- Safe Room and invalid target evidence
- Special encounter and door escape-route safety

Phase 7 remains research-only.

CanApplyGameplay = false.
