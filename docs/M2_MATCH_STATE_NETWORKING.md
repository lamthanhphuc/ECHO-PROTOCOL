# Authoritative Match State Networking

## Ownership and source of truth

`NetworkMatchState` is spawned once by the Host when the gameplay scene is ready. In the Host topology, the Host owns State Authority and is the only peer allowed to advance the match FSM, start or expire timers, and commit a terminal result.

Core progress is deliberately not copied into the match object. `NetworkSectorBox.PlacedCoreCount` remains the authoritative Core objective value; `NetworkMatchState.ObjectiveSourceId` points to that object and `TryGetObjectiveProgress` reads it directly. The existing `EnergyCoreObjectiveProgress`, `MatchFlowController`, and `EscapeDoorCountdown` become presentation adapters while a network match exists.

## Match FSM

```text
CoreObjective -> Puzzle -> SecurityHold -> FinalHunt -> Escape -> MatchEnded/Win
       |            |            |             |          |
       +------------+------------+-------------+----------+-> MatchEnded/Lose
                                                             (match timeout,
                                                              escape timeout,
                                                              all eliminated)
```

Every transition checks current phase and `Status == Running`. `TryEndMatch` changes `Status`, `CurrentPhase`, `Result`, `EndReason`, clears both timers, and increments `EndOrdinal` in one State Authority simulation path. Later transition/end attempts are rejected.

## Replicated fields

- `CurrentPhase`, `Status`, `Result`, and `EndReason`: semantic match state used by all clients and late joiners.
- `ObjectiveSourceId` and `EscapeDoorId`: authoritative object bindings without duplicating their gameplay state.
- `LastActor`, `FinalSurvivorCount`, `PhaseOrdinal`, and `EndOrdinal`: final context and exactly-once/debug ordering.
- `MatchTimer` and `EscapeTimer`: Fusion `TickTimer` values based on Runner simulation time.
- `NetworkSectorBox.PlacedCoreCount`: the only authoritative Core count.

Clients derive countdown text from `EscapeTimer.RemainingTime(Runner)`. They do not decrement an independent gameplay timer. `OnChangedRender` updates legacy UI/gameplay presentation, so late joiners render the current phase, result, and remaining time from the replicated snapshot.

## Integration boundaries

- Core placement calls `TryCompleteCoreObjective` only after authoritative `PlacedCoreCount` reaches the configured requirement.
- The current network objective adapter calls `TryCompletePuzzle` and `TryCompleteSecurityHold`. Owner gameplay can call these same authority-only completion boundaries when its network puzzle/security components are connected.
- `NetworkDoor` accepts interaction only in `FinalHunt`; a valid Host-side interaction calls `TryEnterEscape`, opens the replicated semantic door state, and starts the authoritative escape timer.
- `NetworkPlayerLifeState.StateChanged` is observed only by the Host match state. A loss is committed only when at least one gameplay player is tracked and none remain Alive/Downed/Escaped.
- A valid escape request first commits the player's replicated `Escaped` life state, then commits one Win result.

RPCs remain requests in the existing M2-023 interaction layer. None of the phase, timer, result, door, player-life, or objective persistence depends on an RPC having been observed.

## Win and lose rules

- Win: an active player escapes during `Escape`.
- Lose: the 15-minute authoritative match timer expires, the authoritative escape timer expires, or every tracked gameplay player becomes Eliminated.
- After `MatchEnded`, objective placement, door mutation, phase advancement, and repeated result commits are rejected.

## Unity setup

1. Keep `Assets/Resources/Network/NetworkMatchState.prefab` in Fusion's Network Prefab table (the prefab carries the `FusionPrefab` label).
2. Keep one `PlayerSpawner` on the persistent Bootstrap object. `_matchStatePrefab` may be assigned explicitly; otherwise it loads `Resources/Network/NetworkMatchState`.
3. Assign the existing Network Door and Network Sector Box prefabs on `PlayerSpawner`. At runtime the Host spawns and binds them to the match object.
4. Configure `_matchDurationSeconds` and `_escapeDurationSeconds` on the match prefab.
5. Keep Owner A's `MatchFlowController`, `EnergyCoreObjectiveProgress`, and `EscapeDoorCountdown` in the gameplay scene for UI/presentation. They are automatically switched to network-presentation mode.

## Two-client test plan

1. Start Host and Client in the same room, then start gameplay. Verify both log one Match State NetworkObject with the same ID and show Core phase/progress.
2. Place the required Core(s) from either player. Verify both clients advance once to Puzzle and show the same Core count.
3. Complete Puzzle and Security Hold through the current authoritative interaction adapter. Verify identical phase order and no client-local phase jump.
4. In Final Hunt, have the Client interact with the escape door. Verify the Host validates it, both see the door open, and both countdown displays remain aligned.
5. Escape before expiry. Verify both receive `MatchEnded`, `Win`, `PlayerEscaped`, the same final survivor count, and no later objective mutation.
6. Repeat and let the escape timer expire. Verify one `Lose/EscapeTimeout` commit on both peers.
7. Repeat with all gameplay players eliminated. Verify one `Lose/AllPlayersEliminated` commit; Downed players must not count as eliminated before bleedout.
8. Join a second client after phases have advanced or during Escape. Verify it immediately receives the current phase, Core count, door state, remaining network timer, and final result if already ended.
