# Authoritative Energy Core Networking

## Source of truth

- `NetworkPickupItem`: Fusion State Authority owns `State`, `Holder`, `WorldPosition`,
  `WorldRotation`, `PlacedSectorId`, `PlacementSlot`, and `TransitionOrdinal`.
- `LobbyPlayerState`: State Authority owns the player's single `CarriedCoreId`.
- `NetworkSectorBox`: State Authority owns `PlacedCoreCount`, match `Phase`, and
  `ObjectiveOrdinal`.
- `EnergyCoreObjectiveProgress` is presentation-only during a Fusion match. It mirrors the
  replicated count for the existing HUD and never increments the network objective.

## Command flow

1. The Input Authority client raycasts locally and sends the target `NetworkId` plus a
   monotonic sequence through `NetworkPlayerInteractor`.
2. The State Authority resolves the RPC sender, verifies ownership of the requesting player
   object, rejects replayed sequences, resolves the target, and checks distance/cooldown/tool.
3. Pickup atomically sets the player's `CarriedCoreId`, then changes the Core to `Carried`
   with its `Holder` set to that `PlayerRef`.
4. While carried, every peer derives the Core presentation pose from the replicated holder
   player transform. State Authority retains the latest pose for disconnect recovery.
5. Drop is requested by the owning client. State Authority verifies the holder, calculates
   a grounded drop pose, clears `CarriedCoreId`, and commits `Dropped` plus the networked pose.
6. Place is requested by interacting with the Network Sector Box. State Authority resolves
   the carried Core, verifies its holder and the authoritative Sector Box, chooses the next
   placement slot, commits `Placed`, then increments `PlacedCoreCount` exactly once.

Late joiners reconstruct all Core and objective presentation from the current Fusion snapshot;
no RPC represents persistent state.

## Runtime setup

- `PlayerSpawner` spawns three instances of the registered Network Core prefab on the Host.
- If `EnergyCoreSpawnCandidates` exists in `Game`, its first three children are used; otherwise
  deterministic fallback positions are used.
- The Network Sector Box is placed at Owner A's scene Sector Box when present.
- Legacy local Core/Sector mutation components are disabled on every peer during the Fusion
  gameplay scene. Existing objective UI receives presentation snapshots from Network Sector Box.
