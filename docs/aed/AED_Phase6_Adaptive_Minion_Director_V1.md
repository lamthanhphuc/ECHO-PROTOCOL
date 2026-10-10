# AED Phase 6 — Adaptive Minion Director V1

## Status

Research-only. Gameplay application disabled.

## Production baseline

Normal Minion population: Zone 1: 1, Zone 2: 2, Zone 3: 1.

Spawn uses a 3-second check interval, 4–28m Stalker distance, 18m minimum Player distance, and a complete NavMesh path.

## Population and behavior candidates

- Hold keeps the baseline and never force-despawns.
- Relieve proposes baseline minus one and never force-despawns existing Minions.
- Increase proposes at most baseline plus one, capped at three, after full verification, no active encounter, and a 45-second research cooldown.

| Parameter | Relieve | Hold | Increase |
|---|---:|---:|---:|
| VisionRange | 18 | 20 | 22 |
| TrackSpeed | 7.5 | 8 | 8.5 |
| HarassSpeed | 9 | 10 | 10.5 |
| AttackCooldownSeconds | 0.85 | 0.65 | 0.60 |
| AlertCooldownSeconds | 5 | 4 | 3.5 |

Values are uncalibrated research candidates. Flashlight, slow, theft, attack consequences, Safe Room traversal, and NavMesh legality are not adapted here.

## Placement

Placement stays in the Stalker zone, outside Safe Rooms, with a complete NavMesh path, at least 18m from alive Players, 4–28m from the Stalker, 8m from existing Minions, and 20m from the Zone 1 resource entry. Selection is deterministic and all-or-nothing.

## Integration limitations

No production application, Fusion RPC, AEDv2 tuning changes, or Backend approval changes are made in Phase 6. Phase 8 must verify skill, roster, pressure, source freshness, and proposal revision; Phase 9 must calibrate values and test Host/Client.

`CanApplyGameplay = false`.
