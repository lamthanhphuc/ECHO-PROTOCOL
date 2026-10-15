# AED v0.2 Rev02 source and approval contract

This document records the implementation of the owner-approved pilot. It does not change Telemetry 1.1, `AED_V2_PHASE_EVIDENCE_V2`, or the incomplete coverage status of `AEDPhase8SourceVerifierV1`.

## Source version

`AED_V02_WORLD_SOURCE_REV02_2`, transported as research Telemetry 1.3, contains authoritative ticks, phase/zone, roster, mode/difficulty, plan revision/fingerprint, player life/inventory, threat states/targets/episode identities, live tools/charges/positions, door states and optional typed supply reservations. `HideSpotId` is a decimal string because BSON integers cannot represent every Fusion unsigned 64-bit identity. This is a coordinated pilot schema update; older V1/V2 protocols retain their contracts.

The producer collects both NetworkTeamToolPickup and NetworkToolPickup (including Field Scanner), using the existing Match authority and actual Fusion objects, verified backend player bindings and existing pursuit/Minion episode collectors. It does not upgrade V1 coverage flags. Missing objects, receipts, identities, unsupported action attribution and invalidated debug/inventory evidence prevent verification.

The Backend independently replays accepted lifecycle events, checks census equality including zero Minions, validates life transitions and down ticks, requires complete event sequences and bounded tick/UTC cadence, and reconstructs S/M/D plus revive scarcity. A source window needs 30 observed seconds and expires after 10 seconds. The aggregate remains partial (pressureComplete=false, tier -1) while broader resource need and objective interruption coverage is absent. Revive completion is matched to the reviver First Aid consume receipt; known Noise Maker/Door Jammer spawn charges survive replay. Current observations remain distinct from historical skill.

## Trusted geometry

`AgentScripts/ExportAEDV02Scene.cs` loads the existing SciFi scene additively and exports its baked NavMesh without modifying the scene. The artifact contains 7,719 nondegenerate triangles, 15,732 directed portal links, 222 markers and 20 door identities. Markers include 112 tools, three explicitly authored production zone entries and 107 Cabinets (36/31/40). Cabinet zone assignment uses the same compatible semantic graph and nearest-node lookup as the actual Stalker. Cabinet interaction bounds and the actual prefab vision/interaction ranges are exported. No authored Ambush entry exists; runtime fallback still requires a valid live route. Portals share a triangle edge. Door footprints, including triangles intersecting a closed door, remain represented when a collider is disabled or starts open.

The JSON is copied byte-for-byte to the Unity resource and Backend build content; `scripts/Sync-AEDV02Contract.ps1 -Check` checks equality. Only the shipped content is registered automatically, only in Development with `AEDV02Pilot:Enabled=true`. Client requests cannot register manifests.

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
| R/O pressure | Revive scarcity is reconstructed; active objective ownership is collected; full aggregate remains partial/Unknown | Broader need mapping and causal opportunity/interruption replay remain unfinished |
| Historical skill | Unsupported observations remain Unsupported | Approved normalized competence mapping and complete resolved opportunity sources are still missing; `UNVERIFIED_ESCAPE` is not success |

These limits prevent claiming Gameplay E2E readiness. They are not resolved by seeding verified flags, disabling validation or treating a missing opportunity as skill evidence.

See [remaining contract decisions](AED_V02_Remaining_Contract_Decisions.md) for exact unsupported definitions and unfinished implementation. Current manifest fingerprint: `d3754db5cde76047c443b84a8fd5788076e00203cbeef57f66d155ce2f16e6c8`.

Late receipts after Backend closure require accepted effect and world records preceding the canonical MATCH_ENDED boundary. Wrong host, missing terminal proof and post-terminal effects fail closed. No new effect/world is emitted after terminal gameplay state.

Tool custody is recorded as consent-gated Telemetry 1.3 AED_V02_TOOL_CARRIER (STEAL/DROP), at the existing authoritative carry setters. Independent replay checks a real tool lineage and live Minion at STEAL, one item per Minion, the matching owner at DROP and equality with world census. Carried tools are unavailable supply; dropping restores the actual authoritative drop pose. Missing/forged custody remains a Hold. Post-Down ATTACK retains unsafe state coverage without inventing an active episode or counting more exposure on a Downed/Eliminated player.
