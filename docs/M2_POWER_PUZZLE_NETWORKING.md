# Authoritative Co-op Power Puzzle networking

## Runtime source of truth

`NetworkPowerPuzzle` is spawned by the Host and owns the authoritative semantic state. Its
`State`, `CurrentSequenceIndex`, `FailureCount`, `LastInputId`, `LastInputWasCorrect`,
`LastInteractor`, `SectorBoxId`, timers and transition ordinal are Fusion `[Networked]`
properties. A late joiner receives the current snapshot instead of reconstructing progress
from local puzzle events.

The sequence and number of station inputs are prefab configuration. They are identical on
all peers, but only State Authority reads them to decide whether an input is correct.

## Request flow

1. The Input Authority player raycasts a `NetworkPowerPuzzleStation` locally.
2. `NetworkPlayerInteractor` sends the existing M2-023 interaction RPC to State Authority.
3. State Authority resolves the RPC source, verifies that it owns the requesting player
   object, rejects replayed sequences, resolves the station NetworkObject, and validates
   player distance plus the station cooldown.
4. The station resolves its networked `PuzzleId` and asks `NetworkPowerPuzzle` to apply its
   configurable `InputId`.
5. `NetworkPowerPuzzle` validates its state and the expected sequence step, then changes
   only networked semantic state. The RPC is a command, never persistent puzzle state.
6. Replication updates every peer. `PowerPuzzleController.ApplyAuthoritativeSnapshot` is a
   presentation-only compatibility bridge and does not invoke legacy completion gameplay.

## Failure, reset and completion

An incorrect valid station input commits `Failed` once and starts an authoritative
`TickTimer`. Further inputs are rejected. The Host changes to `Resetting` when the lockout
expires, then returns to `InProgress`; after the configured number of failures it also resets
sequence progress and the failure count, matching Owner A's reset rule.

Completion is guarded by `State == Completed`. The puzzle then asks `NetworkSectorBox` to
validate the completed puzzle object before advancing from `PowerPuzzle` to `SecurityHold`.
The former one-click Sector Box shortcut is disabled.

## Race handling

Fusion delivers the clients' RPC requests to the single State Authority. Each request is
validated and applied serially against the latest authoritative `CurrentSequenceIndex`.
Therefore two players pressing close together cannot both commit the same sequence step.
The generic per-player request sequence prevents replay, while each station's networked
cooldown prevents rapid duplicate use of that station.

## Multiplayer setup

`PlayerSpawner` loads the two Fusion-prefab-labelled resources automatically:

- `Resources/Network/NetworkPowerPuzzle.prefab`
- `Resources/Network/NetworkPowerPuzzleStation.prefab`

The first two stations are overlaid on Owner A's existing station transforms. Their fallback
meshes are hidden, while their network colliders receive interaction raycasts. If a station
transform is missing, a visible deterministic fallback station is spawned near the Sector
Box. In multiplayer, legacy `PowerPuzzleController` and `PowerPuzzleStation` behaviours are
disabled as mutators; the controller continues to receive presentation snapshots.
