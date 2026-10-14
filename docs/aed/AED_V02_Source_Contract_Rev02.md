# AED v0.2 Rev02 source and approval contract

This document records the implementation of the owner-approved pilot. It does not change Telemetry 1.1, `AED_V2_PHASE_EVIDENCE_V2`, or the incomplete coverage status of `AEDPhase8SourceVerifierV1`.

## Source version

`AED_V02_WORLD_SOURCE_REV02`, transported as research Telemetry 1.3, contains authoritative ticks, phase/zone, roster, mode/difficulty, plan revision/fingerprint, player life/inventory, threat states/targets/episode identities, live tools/charges/positions, and door states. `HideSpotId` is a decimal string because BSON integers cannot represent every Fusion unsigned 64-bit identity.

The producer uses the existing Match authority, actual Fusion objects, verified backend player bindings and existing pursuit/Minion episode collectors. It does not upgrade V1 coverage flags. Missing objects, receipts, identities, unsupported action attribution and invalidated debug/inventory evidence prevent verification.

The Backend independently replays accepted lifecycle events, checks census equality including zero Minions, validates life transitions and down ticks, requires complete event sequences and bounded tick/UTC cadence, and reconstructs the five Rev02 pressure channels. A source window needs 30 observed seconds and expires after 10 seconds. Current observations remain distinct from historical skill.

## Trusted geometry

`AgentScripts/ExportAEDV02Scene.cs` loads the existing SciFi scene additively and exports its baked NavMesh without modifying the scene. The artifact contains 7,719 nondegenerate triangles, 15,732 directed portal links, authored tool masks/positions, three existing entry positions and door footprints. Portals must share a triangle edge. Door footprints remain present when a collider is disabled or the door starts open.

The JSON is copied byte-for-byte to the Unity resource and Backend build content; `scripts/Sync-AEDV02Contract.ps1 -Check` checks equality. Only the shipped content is registered automatically, only in Development with `AEDV02Pilot:Enabled=true`. Client requests cannot register manifests.

Backend reachability follows actual mesh portals and requires each crossed door to be open, broken or usable while closed/unlocked. Unknown/locked/jammed doors, positions outside the mesh, another floor, and disallowed spawn types fail closed. The Host must still revalidate live NavMesh, doors and safety at apply time; the exported graph does not prove transient physics conditions.

## Decision ledger

The v0.2 ledger is separate from the existing V2 ledger. It binds the authenticated Host/lease, current scenario snapshot, roster, scene/source fingerprints, parameter versions, previous plan, revision and `(phase, point, window sequence)`. PreMatch is one atomic composite; subsequent transactions change one key. Ordered plan revisions distinguish multiple SafeWindows in one phase.

PreMatch approval expires with its authority lease because scene loading consumes time and no current gameplay source is used. In-match approval expires after at most 10 seconds or with the lease. Both require Host revalidation. This does not extend source freshness.

An apply receipt requires an accepted effect emitted by the named consumer and a later accepted world sample with the same revision, fingerprint, roster and scene. The Backend compares actual float consumer values within 0.000001 of the registered value. Decision/receipt/abort writes use serializable transactions; conflicts return failure instead of acknowledging competing commits.

## Current verification limits

| Input | Status in this implementation checkpoint | Remaining requirement |
|---|---|---|
| S exposure | Reconstructed from authoritative CHASE/ATTACK state windows and actual episode IDs | Live Host evidence still required |
| M exposure/population | HARASS windows and lifecycle/census reconciliation, including zero population | Transfer and counterplay attribution remain incomplete |
| D consequences | Canonical down/revive/eliminate ordering and tick joins | Live binding proof still required |
| Supply geometry | Real exported mesh, placement masks and door-sensitive verification tested | Live dynamic route revalidation and producer scenarios remain incomplete |
| R/O pressure | Backend reconstruction exists; producer holds unsupported active objective/transfer attribution | Authoritative opportunity/need/interruption collection is unfinished |
| Historical skill | Unsupported observations remain Unsupported | Approved normalized competence mapping and complete resolved opportunity sources are still missing; `UNVERIFIED_ESCAPE` is not success |

These limits prevent claiming Gameplay E2E readiness. They are not resolved by seeding verified flags, disabling validation or treating a missing opportunity as skill evidence.
