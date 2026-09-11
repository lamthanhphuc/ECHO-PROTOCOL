# Ghost catch / local jumpscare

## Integration

`StalkerFusionRuntime` and `StalkerNetworkLifeStateConsequenceSink` use the existing authoritative attack hit moment. They check ghost authority, ATTACK state, cooldown, player authority on the same runner, Alive status, revive protection and current distance. Production hits do not also execute the legacy damage fallback.

`NetworkPlayerLifeState.Status` adds Caught (4), preserving existing serialized enum values. Eliminated (2) is this project's Dead state. The replicated catch timer, duration, ghost ID and outcome belong to the player, so destroying the ghost does not interrupt the transition. No jumpscare RPC exists. Caught players remain match-active until the authority resolves the outcome.

Movement and interaction retain the existing life-state gates. Caught cannot move, act, receive another catch, revive or escape. Downed retains the existing limited crawl and teammate first-aid revive. Eliminated maps to Spectating only on the input owner's client; the spectator follows an Alive teammate and reselects if that target becomes unavailable. With no living teammate the camera stays at the dead player's position until match UI takes over.

## Inspector setup

Configured in the Unity Editor on 2026-09-11:

| Asset | Saved configuration |
| --- | --- |
| `Assets/Prefabs/Player/Jumpscare/GhostJumpscare.prefab` | HumanDeer visual, local root Animator with `Jumpscare` trigger and procedural lunge animation; no network/gameplay components |
| `Assets/Prefabs/PlayerNetwork.prefab` | `PlayerJumpscareController` added, visual prefab and audio assigned |
| `Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab` | Same jumpscare references for the test player |
| `Assets/Prefabs/StalkerNetwork.prefab` | Catch range 2 m, duration 2 s, cooldown 2 s, Downed outcome |
| Audio | `Assets/Audio/stalker/chase_start.wav`, existing Stalker vocal used as a placeholder scream |
| HUD | Existing automatic `GameplayHUD_Canvas` lookup; no scene reference serialized into a prefab |

The reusable Editor menu is **ECHO Protocol > Setup > Ghost Jumpscare** (`GhostJumpscarePrefabSetup.cs`). Running it again restores the above player references and Downed defaults. The current lunge is a procedural presentation animation, not a bespoke skeletal jumpscare performance. Replace the placeholder vocal/animation when final art is available. Editor setup/reference validation passed; the seven multiplayer acceptance scenarios still require PlayMode testing.

1. On the existing Stalker network prefab's `StalkerFusionRuntime`, set `Maximum Damage Distance` (catch range), `Jumpscare Seconds` (default 2), `Catch Cooldown Seconds`, and `Catch Ends In Death` (off = Downed, on = Eliminated/Dead).
2. Add `PlayerJumpscareController` to the existing gameplay player prefab to configure it. Runtime adds a default component if absent, but that default has no model or scream asset.
3. Assign `Ghost Jumpscare Prefab`: a presentation-only model with an Animator controller exposing a Trigger parameter named `Jumpscare`. Do not include NetworkObject, NetworkTransform, NetworkMecanimAnimator, gameplay scripts, colliders or autoplay audio. Adjust Ghost Offset/Euler for the model's pivot and size.
4. Assign `Scream` AudioClip. The controller creates its own local 2D AudioSource. Assign the scene gameplay HUD root when configuring a scene instance, or retain the project's `GameplayHUD_Canvas` name for automatic discovery. Keep the jumpscare controller outside that HUD root.
5. The local camera must have `PlayerCamera` bound to the owning player, as done by `NetworkPlayerMovement`. Jumpscare waits for that binding. Shake, FOV and fade are created locally. Runtime also ensures `PlayerSpectateController` exists.
6. Let Unity recompile and Fusion weave the changed network layout, then rebuild all peers together. No backend or NetworkRunner changes are required.

The default timeline locks controls at Caught, shows the model at .05 s, triggers animation at .10 s, plays scream at .15 s, shakes at .20 s and fades between 1.5–2 s. A late Caught snapshot seeks the timeline using the replicated remaining time. The model hides at the deadline; black holds until authoritative Downed/Eliminated arrives. HUD and FOV restore on state change, disable or despawn. No event is replayed for clients joining after Caught ended.

## Validation

EditMode: `PlayerCaughtRulesTests` exercises non-Alive action/damage rejection, Caught movement/escape/early-revive rejection, and retained revive/protection rules. The existing telemetry contract now expects the catch sink.

Required two-peer PlayMode checks (Host Mode):

- Catch remote player: only its window shows model, scream, shake and fade; host observes Caught then Downed.
- Catch host player: remote camera/audio/HUD stay unchanged.
- Repeated hits/two ghosts in one tick: exactly one catch transition; cooldown and Caught reject retries.
- Out-of-range, protected, Downed, Eliminated or Escaped target: no catch.
- Default outcome: after 2 seconds, Downed and limited crawl; an Alive teammate can complete the existing first-aid revive.
- Death outcome: after 2 seconds, Eliminated and local spectator; spectator reselects when its target dies/disconnects.
- Despawn victim/stop runner during the effect: model, audio and overlay are removed, FOV/HUD restored.
- Destroy ghost mid-catch: player timer still resolves. Delay snapshots: local black waits for authoritative outcome without an RPC.

References: [Fusion 2 render change detection](https://doc.photonengine.com/fusion/v2/manual/data-transfer/change-detection), [TickTimer API](https://doc-api.photonengine.com/en/fusion/current/struct_fusion_1_1_tick_timer.html).
