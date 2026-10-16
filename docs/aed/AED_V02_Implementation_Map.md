# AED v0.2 Rev02 — implementation map


## Mandatory implementation blockers — final static review

**IMPLEMENTATION INCOMPLETE — BLOCKERS REMAINING.** The source commits above are useful implementation progress, not a claim that all B1–B8 requirements are complete.

1. **ResourceManagement verification:** OPPORTUNITIES_2 now has a distinct `ResourceManagement` axis. ResourceGrade and OptionalToolBudget decisions use it; a new accepted Telemetry 1.3 action event is tied to the verified minion-loss cause and canonical lifecycle event, and Backend reconstructs success/failure/censoring independently of general Tool skill. The Backend event validator, reconstruction and Unity producer are source-authored but have no focused regression tests and have not been compiled or exercised. Do not claim historical sufficiency or Verified eligibility until those gates pass.
2. **Protected-area / Safe Room lineage:** authoritative marker identity, strict world-space bounds, exporter requirements and fail-closed route/point checks are now source-authored in Unity and Backend, including Stalker navigation paths. The actual SciFi scene and manifests were not edited or exported in this shell-only continuation. Therefore the shipped manifest does not yet provide the required registry, and Adaptive V02 Safe Room-dependent operations intentionally Hold. Do not invent bounds or treat absent data as safe.
3. **Full deterministic twelve-case orchestration:** Invoke-AEDV02AcceptanceScenario contains CAPTURE_REQUIRED branches for fairness, supply, Zone3 relief, final-phase progression and multiple-window audit. They help capture actual conditions but do not fully drive/verify those scenarios. Native controls exist, including four identities/reconnect/outage, but complete scenario stimuli and correlation/assertions remain implementation work. Existing genuine historical prerequisites cannot be replaced by a fake verifier or pressure flags.

No owner E2E acceptance is requested. The owner runbook is preparation for later verification, not an instruction to accept this incomplete candidate. Compilation/tests/build/native remain NOT RUN as explicitly requested. ResourceManagement and Safe Room tests are authored but unexecuted.


Branch: feature/aed-v02-full-integration; PR #23 Draft into LTP. Continuation starts at 243eceec. This is a source implementation map, not execution proof. All new compile/test/build/native gates are NOT RUN by owner instruction.

| Area | Actual source path | Inspection result / execution gate |
|---|---|---|
| B1 history | Backend AEDV02HistoricalCompetenceService, AEDV02OpportunityReconstruction, five Tool verifiers plus independent ResourceManagement verifier, shared AEDV02PilotOpportunities | Ended/sealed comparable matches, context/recency/sample confidence, match balance, weakest guard and separate causal resource action replay; source added, focused coverage and execution UNVERIFIED |
| B2 PP/TP | MatchAuthorityRuntimeV02Source, AEDV02SourceVerifier, shared pilot policy/opportunities | Actual five channels and causal R/O, dedup/decay/hysteresis/dwell, complete-roster guards; missing data remains Unknown |
| B3 Stalker | StalkerFusionRuntime, StalkerNavigationController, StalkerFusionCabinetRuntime, StalkerCabinetEncounterRuntime, hide protection/force-exit guards | Per-player senses/fairness, Safe Room route exclusion, core/Seek recovery, P4 token, Cabinet cue/path/inspection/revalidation/shared budget; source added, native behavior UNVERIFIED |
| B4 Minion | PlayerSpawner, CreepMinionRuntime, NetworkMatchStateV02, source census/receipt verification | Adaptive Zone3 entitlement and actual census, no forced active despawn; Fixed Normal 1/2/0 retained |
| B5 resources | TeamToolWorldSpawnV02 and Support, shared ResourcePlacement/ActionContract, Backend placement/receipt | All grades, future reservations, five required types, ordinary/emergency support and physical checks; actual transactions UNVERIFIED |
| Scanner | NetworkFieldScanner, FieldScannerCoreDetector observation callback, world Scannables/Scans, AEDV02ScannerVerification | Separate authoritative census, actual detector result/HUD ACK, custody/use/timing/math replay, unique information outcomes; twelve ingested cases authored NOT RUN |
| Door Jammer | Existing Interactor deploy gateways, NetworkDoorJammer provenance, Stalker interaction readback, AEDV02JammerVerification | Real activation/charges/blocker progress/door, stable match/phase/user lineage, phase carry-over; eight ingested cases authored NOT RUN |
| Other Tools | Existing FirstAid revive lineage, AEDV02NoiseMakerVerification, AEDV02StabilizerVerification | Real family-specific effects; no activation-count proxy or substitution for another family |
| B6 Backend | AEDV02Controller, SourceVerifier, DecisionService, existing pilot/action-envelope migrations and validators | Auth/lease/roster/scene/source, PreMatch composite, distinct windows, approval/commit/effect/receipt/abort/retry/reconciliation; no validation bypass |
| B7 Fusion/lifecycle | NetworkMatchStateV02, Stalker consumer match/revision/fingerprint, NetworkBootstrap, Interactor Scanner reset | Host-only apply, Client actual presentation, old-match consumer rejection, same-revision rebind, baseline restore, stable reconnect token; transport UNVERIFIED |
| B8 automation | Existing CLI/campaign/build scripts, NativeHarness and Controls, native comparator, AcceptanceScenario, owner runbook | Development-only normal-input controls, 2–4 real processes, SessionOnly Fixed/Shadow capture, real outages/reconnect/shutdown, twelve case recipes; no runtime executed |
| Scene | SciFi.unity, new AEDV02SafeRoomMarker authoring component/exporter, shipped manifest | No Safe Room markers were authored and no manifest export was run in this continuation; existing shipped manifest lacks the required registry, so affected Adaptive V02 paths Hold until owner export |

## Fixed and main boundaries

- Latest fetched main: f849d45b. No main/LTP merge, deploy or production database action occurred.
- The main Scanner match-reset hook was selectively retained through Interactor; its display/cooldown/source resets are part of lifecycle preservation. Other latest-main UV/Resonance, localization, lobby input/medkits/Voice, fuel/convoy and movement changes have not been broadly imported. Whole-tree latest-main parity is UNVERIFIED.
- MatchDifficulty normal constants match main with the approved explicit Zone3 cap property added; Fixed Normal remains 1/2/0. New Cabinet authoring entries are outside the legacy Special registry.
- Gameplay defaults and the two protected owner assets are unchanged by this task.

See Source Contract, Pilot Defaults, Remaining Contract Decisions, Traceability, Verification Report and OWNER_TEST_RUNBOOK. No source row is a native E2E PASS.
