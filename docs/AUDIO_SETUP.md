# Game audio

Audio starts automatically in Play mode and builds through `GameAudioRuntime`.
No scene migration or manual component attachment is required. The runtime
discovers active gameplay objects and UI buttons once per second and attaches
presentation-only emitters. Network snapshots are sampled every 50 ms; initial
snapshots establish a baseline without replaying historical one-shot sounds.
Short transitions that finish before discovery/sampling can be missed.

`KLTN/Assets/Resources/GameAudioCatalog.asset` references the original 130 WAV
assets under `Assets/Audio`, so player builds do not depend on AssetDatabase.
Select the catalog in the Inspector to adjust Effects Volume and Ambience Volume.
Unused alternate clips remain available in the catalog for later tuning.

| Trigger | Audio |
| --- | --- |
| Lobby/SciFi scene | Quiet facility room tone |
| Buttons, room connection, ready, failed interaction, tool pickup | UI feedback |
| Moving player | Alternating concrete/sprint footsteps; no steps while stationary or teleporting |
| Flashlight on/off | Switch clicks |
| Local hiding | Enter/exit and breathing loop |
| Damage/down/elimination | Grunt, down cue, elimination cue |
| Revive | Start/completion/cancel, progress loop; local downed heartbeat |
| Stalker | Patrol/chase footsteps, breathing/growl, detection/chase/search/recover cues, attack swing/hit/miss |
| Sliding door | Open/close; existing break/jammer RPCs use catalog fallback when no clip is assigned |
| Energy core | World hum, pickup/drop/deposit |
| Sector box | Partial insertion, fully powered, quiet machinery loop |
| Power puzzle | Correct/wrong input and completion |
| Security terminal | Download start/pause/resume/complete and progress loop |
| Noise maker | Spatial beacon loop while the deployed object exists |
| Match | Start, objective phase, final hunt, escape unlocked, final ten-second ticks, win/lose |

World effects use 3D attenuation (2 m minimum, 25 m maximum, no Doppler).
UI, local player footsteps, and local heartbeat use 2D playback. Audio never
submits gameplay noise to the AI or changes Fusion authority. Spatial room-specific
ambience and alternate surface footstep clips are not assigned arbitrarily.

## Validation

- C# compiled against installed Unity/Fusion references with Roslyn.
- Referenced sound keys resolve to catalog clips; all 130 WAV headers are valid.
- Listening and multiplayer checks still require Unity:
  1. Open Lobby through Bootstrap, create/join, toggle Ready; check UI feedback.
  2. Start SciFi with two clients. Walk/sprint, stop, toggle the flashlight;
     verify remote steps attenuate with distance and do not duplicate locally.
  3. Open/close a door, deploy/break a jammer, carry/drop/deposit a core, complete
     a puzzle, pause/resume a terminal, and deploy a noise maker.
  4. Trigger a Stalker chase/attack and down/revive; check loop cleanup on exit,
     elimination, despawn, and scene change.
  5. Finish and lose a match; verify outcome cues and final countdown ticks.
  6. Repeat a short smoke test in a player build to check clip inclusion.
