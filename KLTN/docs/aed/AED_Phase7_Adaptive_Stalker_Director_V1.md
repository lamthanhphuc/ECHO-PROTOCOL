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
