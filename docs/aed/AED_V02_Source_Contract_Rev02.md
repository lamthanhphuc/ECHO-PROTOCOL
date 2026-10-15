# AED v0.2 Rev02 source and approval contract

This document records the implementation of the owner-approved pilot. It does not change Telemetry 1.1, `AED_V2_PHASE_EVIDENCE_V2`, or the incomplete coverage status of `AEDPhase8SourceVerifierV1`.

## Source version

`AED_V02_WORLD_SOURCE_REV02_4`, transported as research Telemetry 1.3, contains authoritative ticks, phase/zone, roster, mode/difficulty, plan revision/fingerprint, player life/inventory, named revive helper, threat states/targets/episode identities, actual Stalker consumer values, live tools/charges/positions, door states and typed supply reservations with path corners and placement grade. `HideSpotId` is a decimal string because BSON integers cannot represent every Fusion unsigned 64-bit identity. This is a coordinated pilot schema update; older V1/V2 protocols retain their contracts. The new implementation is still in Stage B and has NOT been compiled or tested.

Stalker `ConsumerValues` is empty when unobserved or contains six actual runtime reads in `AEDV02Key` order (keys 0–5). Minions carry no Stalker values. The producer reads the controller, hearing multiplier and Special runtime; it never copies plan values into evidence. Detection/Special targets retain their authoritative identity without inventing a Chase episode. Verification identity is `AED_V02_SOURCE_VERIFICATION_REV02_V3`. PreMatch and Stalker receipts require a later reconciled world census whose observed values all match the approved plan; matching revision/fingerprint alone is insufficient. Composite PreMatch Stalker/Cabinet/optional changes wait for all native consumers rather than stalling the single-key receipt path.

Historical learning requires one accepted `AED_V02_CAPTURE_SEAL` with `exclusion=NONE`, after the verified source and before the canonical terminal boundary. Debug/inventory/source invalidation emits `AED_V02_CAPTURE_EXCLUDED`; a later exclusion invalidates earlier windows for learning. Sealing is best-effort and never blocks normal match closure: missing seal yields no eligible history. `MatchPlayerBindings.HadGameplayDisconnect` persists an actual InMatch disconnect across reconnect, excluding that match from historical competence without preventing the current reconnect lifecycle. Malformed stored source is ineligible, not a reason to grant skill or crash the profile route. These 1.3 records do not alter frozen Telemetry 1.1.

The producer collects both NetworkTeamToolPickup and NetworkToolPickup (including Field Scanner), using the existing Match authority and actual Fusion objects, verified backend player bindings and existing pursuit/Minion episode collectors. It does not upgrade V1 coverage flags. Missing objects, receipts, identities, unsupported action attribution and invalidated debug/inventory evidence prevent verification.

The Backend independently replays accepted lifecycle events, checks census equality including zero Minions, validates life transitions and down ticks, and requires complete event sequences and bounded tick/UTC cadence. Under `AED_V02_PILOT_OPPORTUNITIES_1`, accepted causal impacts are joined to before/after authoritative world samples, actual actors/victims and canonical consequences. Resource needs cover actual Down/revive capacity and typed Minion tool loss; objective interruption requires a previously observed operation and a continuing actual constraint. Normal pressure needs 30 observed seconds and expires after 10 seconds. Missing/old opportunity protocol remains partial (pressureComplete=false, tier -1), rather than certifying unsupported channels. The five-second emergency path does not certify PP/TP. Revive completion is matched to the reviver First Aid consume receipt; known Noise Maker/Door Jammer spawn charges survive replay. Current observations remain distinct from historical skill.

## Trusted geometry

`Assets/Editor/AED/ExportAEDV02Scene.cs` exports the existing SciFi baked NavMesh without modifying the scene; the old AgentScripts entry is a compatibility wrapper. `AEDV02BatchBuild.ExportManifest` opens SciFi alone to exclude other scenes' NavMesh. The previous artifact contains 7,719 nondegenerate triangles, 15,732 directed portal links, 222 markers and 20 door identities, including 112 tools, three production zone entries and 107 Cabinets (36/31/40). It still needs regeneration for the new registered resource roles. Cabinet zone assignment uses the actual compatible Stalker semantic graph and nearest-node lookup. Prefab interaction/vision ranges and door footprints are exported. No authored Ambush entry exists; runtime fallback still requires a valid live route.

The JSON is copied byte-for-byte to the Unity resource and Backend build content; `scripts/Sync-AEDV02Contract.ps1 -Check` checks equality. Only the shipped content is registered automatically, only in Development with `AEDV02Pilot:Enabled=true`. Client requests cannot register manifests.

The next export also includes `MinimumToolSpacing`, read from the serialized production NetworkMatchState prefab. A legacy manifest without this constraint is unknown (`-1`) and cannot approve new graded/support placement. Before approval, Backend loads the exact source-end world (phase/roster/plan/scene bound), checks required-type multisets, unmaterialized zones, allowed roles, actual entry/door-aware geometry, pairwise spacing and existing world items. Player-carried items are not world spawn occupancy. These checks supplement Host live physics and later effect verification; export and execution are still pending.

Backend reachability follows actual mesh portals and requires each crossed door to be open, broken or usable while closed/unlocked. Known locked/jammed routes are verified as inaccessible. Missing door state, positions outside the mesh, another floor, and disallowed spawn types fail closed. Revive scarcity uses living helpers, usable First Aid and paths to the tool and downed player. The Host must still revalidate live NavMesh, doors and safety at apply time; the exported graph does not prove transient physics conditions.

## Decision ledger

The v0.2 ledger is separate from the existing V2 ledger. It binds the authenticated Host/lease, current scenario snapshot, roster, scene/source fingerprints, parameter versions, previous plan, revision and `(phase, point, window sequence)`. PreMatch is one atomic composite; subsequent transactions change one key and one tier. Ordered plan revisions distinguish multiple SafeWindows in one phase.

PreMatch approval expires with its authority lease because scene loading consumes time and no current gameplay source is used. In-match approval expires after at most 10 seconds or with the lease. Both require Host revalidation. This does not extend source freshness.

An apply receipt requires an accepted effect emitted by the named consumer and a later accepted world sample with the same revision, fingerprint, roster and scene. The Backend compares actual float consumer values within 0.000001 of the registered value. Decision/receipt/abort writes use serializable transactions; conflicts return failure instead of acknowledging competing commits.

The later world also independently reconciles its complete player roster, Tool lineage/charges and Minion population. Supply effects require verified initial/remaining optional budgets, materialization counts, five required runtime types and registered unspawned reservations for each future zone. Scene-placed items do not replace required runtime allocations. Lowering a cap does not prove an existing Zone3 Minion despawned: another positive axis waits for actual world population proof.

PreMatch preserves an already issued v1 support allowance in the new optional budget (0..2), without adding it twice. It rechecks the actual persisted candidate fingerprint and refuses unmapped monster deltas. A composite optional/Cabinet policy receipt waits for the real zone supply consumer and a later world sample; a policy envelope alone is insufficient for the supply allocation. Missing optional slots retain the five required items and do not become new pressure-squeeze opportunities after materialization.

Cabinet clues are emitted at the real observed entry, before hiding teleports the player. Host checks the actual sensor and interaction colliders; Backend joins the registered cabinet, bounded player entry position, known observer and player life/hide transition. Missing aggregate PP or Unsupported TP still prevents an encounter. Its native inspection reuses the actual inspection duration/resolver and revalidates before force-exit; all relevant clients must acknowledge actual audio/visual presentation before the reaction timer starts.

## Current verification limits

| Input | Status in this implementation checkpoint | Remaining requirement |
|---|---|---|
| S exposure | Reconstructed from authoritative CHASE/ATTACK state windows and actual episode IDs | Live Host evidence still required |
| M exposure/population | HARASS windows and lifecycle/census reconciliation, including zero population | Transfer and counterplay attribution remain incomplete |
| D consequences | Canonical down/revive/eliminate ordering and tick joins | Live binding proof still required |
| Supply geometry | Real exported mesh, placement masks and door-sensitive verification tested | Live dynamic route revalidation and producer scenarios remain incomplete |
| R/O pressure | Pilot causal collection and reconstruction implemented, NOT TESTED; legacy/no-protocol sources remain partial | Run positive/negative source integration and native collection proof |
| Historical skill | Pilot match-balanced/context/recency/confidence mapping implemented, NOT TESTED. S/M encounters, core transport, named revive attempts and linked First Aid effects are reconstructed | Complete useful Scanner detection, Noise Maker distraction, Door Jammer delay and Core Stabilizer protection outcomes. A First Aid success cannot certify the other four tool families; `UNVERIFIED_ESCAPE` is not success |

These limits prevent claiming Gameplay E2E readiness. They are not resolved by seeding verified flags, disabling validation or treating a missing opportunity as skill evidence.

The new action envelope binds support/placement fingerprints to the decision, Host commit and actual effect. A receipt requires an accepted `AED_V02_PLAN_COMMIT`; failure after a real commit is `FAILED_EFFECT`, retaining the committed revision. `AEDV02Cancellations` records authenticated cancellation before approval so a delayed submission cannot create an orphan approval after timeout. A claimed commit without accepted evidence stays Hold. These source and migration changes have NOT RUN through the test campaign yet.

See [pilot defaults](AED_V02_Pilot_Contract_Defaults.md) and [execution checklist](AED_V02_Execution_Checklist.md) for the current implementation state. No new test result supersedes the previously tested checkpoint until Stage C/D actually runs.

See [remaining contract decisions](AED_V02_Remaining_Contract_Decisions.md) for exact unsupported definitions and unfinished implementation. Current manifest fingerprint: `d3754db5cde76047c443b84a8fd5788076e00203cbeef57f66d155ce2f16e6c8`.

Late receipts after Backend closure require accepted effect and world records preceding the canonical MATCH_ENDED boundary. Wrong host, missing terminal proof and post-terminal effects fail closed. No new effect/world is emitted after terminal gameplay state.

Tool custody is recorded as consent-gated Telemetry 1.3 AED_V02_TOOL_CARRIER (STEAL/DROP), at the existing authoritative carry setters. Independent replay checks a real tool lineage and live Minion at STEAL, one item per Minion, the matching owner at DROP and equality with world census. Carried tools are unavailable supply; dropping restores the actual authoritative drop pose. Missing/forged custody remains a Hold. Post-Down ATTACK retains unsafe state coverage without inventing an active episode or counting more exposure on a Downed/Eliminated player.
