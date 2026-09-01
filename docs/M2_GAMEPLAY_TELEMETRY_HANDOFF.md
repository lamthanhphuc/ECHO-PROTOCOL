# M2 gameplay telemetry handoff

## Implemented authoritative owners

| Event group | Authoritative mutation source |
|---|---|
| `CORE_PICKED_UP`, `CORE_DROPPED`, `CORE_PLACED` | `NetworkPickupItem` and `LobbyPlayerState.CarriedCoreId` |
| Phase, puzzle, security hold | `NetworkSectorBox` |
| Down, revive, eliminate, escape | `NetworkPlayerLifeState` |
| `TEAM_TOOL_USED`, `HELP_PING_USED` | `NetworkPlayerInteractor` Host RPC handlers |
| `RUNTIME_NOISE_ACCEPTED` | `HostRuntimeNoiseService` after Host acceptance |
| Stalker attack research callback | `StalkerNetworkLifeStateConsequenceSink` |

All production callbacks run after the Host commits the corresponding Fusion networked state. Event occurrence keys include the owning `NetworkObject` and a Host-maintained transition ordinal.

## Manual controls

- `E`: interact, pick up/place a core, advance the M2 objective console, or revive the player under the crosshair.
- `G`: drop the carried core.
- `T`: use the selected team tool; Tool ID 2 is the Noise Maker.
- `H`: send a help ping while Downed.
- Sprint: emits accepted movement noise at a Host-limited interval.

The gameplay scene spawns a Host-owned Sector Box from `Resources/Network/NetworkSectorBox.prefab`. The M2 validation flow is core collection, power puzzle, security hold, final hunt, then match completion after all tracked players escape or are eliminated.

## Remaining runtime gate

Run Host plus at least one Client with different backend accounts. Confirm event order and per-item acknowledgements in Host logs and MongoDB. A successful terminal flow must contain `PLAYER_ESCAPED` or `PLAYER_ELIMINATED` before `MATCH_ENDED` and must not later emit `MATCH_ABORTED`.

Research Capture remains disabled by default. The client callback and adapter exist, but the backend intentionally rejects research events until a consent/configuration policy is approved and enabled. Do not enable `_researchCaptureEnabled` alone.
