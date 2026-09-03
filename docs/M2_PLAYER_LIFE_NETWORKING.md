# Authoritative Down / Revive / Eliminated synchronization

## State machine

```text
Alive
  |
  | State Authority commits lethal damage/down
  v
Downed
  |\
  | \ authoritative BleedoutTimer expires
  |  v
  |  Eliminated
  |
  | valid revive request + authoritative ReviveTimer completes
  v
Alive (temporary authority-owned protection timer)
```

`Revive` is an action while the target remains `Downed`; it is not a separate life state.
Post-revive protection is also a networked timer while the player is `Alive`, not a separate
life state. `Escaped` remains as the existing terminal match-flow state.

## Authority ownership

`NetworkPlayerLifeState` on each player NetworkObject is the runtime source of truth. In Host
Mode, Host/State Authority exclusively owns every transition:

- damage source calls `TryApplyAuthoritativeDamage` or the existing host-only Stalker sink;
- reaching zero commits Alive to Downed and starts the bleedout timer;
- an accepted revive request sets networked Reviver and ReviveTimer while status stays Downed;
- revive completion commits Downed to Alive and starts ProtectionTimer;
- bleedout commits Downed to Eliminated;
- protection expiry only clears the protection timer; status remains Alive.

The owning client has Input Authority only for input and requests. It never commits life state.

## Networked data

- `Status`: Alive, Downed, Eliminated or Escaped;
- authoritative `Health`;
- `Reviver` PlayerRef, which also identifies an active revive action;
- crawl flag;
- down/revive counters and transition ordinal;
- last transition cause;
- Fusion TickTimers for bleedout, revive progress and post-revive protection.

Clients derive display-only countdown/progress from replicated target ticks and the Runner
clock. They do not run transition timers locally. This also makes late-join snapshots complete.

## Revive request and cancellation

1. Reviver Input Authority detects a target and calls `RequestRevive`.
2. Existing InputAuthority-to-StateAuthority RPC sends target NetworkId and replay sequence.
3. Host verifies RPC source, ownership of the requesting player object and gameplay membership.
4. Host resolves target and validates: target Downed, no revive already active, not self,
   revive allowance, reviver Alive and within distance.
5. Host stores Reviver and starts ReviveTimer. Status remains Downed.
6. Every Fusion tick, Host revalidates reviver existence, life state and distance. Moving away,
   disconnecting or becoming Downed clears the revive progress. Gameplay may explicitly call
   `TryCancelRevive` for another interruption.

## Race prevention

State Authority processes requests serially. The first accepted request sets Reviver/ReviveTimer,
so the second request fails the already-in-progress check. Bleedout is evaluated before revive
completion in `FixedUpdateNetwork`; a same-tick timeout therefore ends in Eliminated. Terminal
state guards prevent duplicate elimination, revive and telemetry commits.

## Owner A presentation integration

Existing `PlayerDownState` and `PlayerReviveInteractable` are retained as presentation adapters.
On network players they run in presentation-only mode: their local damage, bleedout and revive
timers are disabled, while replicated snapshots drive HUD, crawl/down visuals, revive progress,
protection and local spectate presentation.
