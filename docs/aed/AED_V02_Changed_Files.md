# Feature file inventory

Relative to LTP base `10391b1d`. Includes the four foundation commits and this continuation. User WIP is excluded. `.meta` entries keep Unity GUIDs; approved owner documents are retained verbatim. This inventory is a review aid, not runtime proof.

| File | Purpose |
|---|---|
| `.github/workflows/aed-backend.yml` | Reproducible regression / CI / byte-identical mirror validation |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Configurations/AEDV02PilotSettings.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Content/AEDV02/Rev02SceneManifest.json` | Registered baked geometry, markers, cabinet bounds and door topology |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Controllers/AEDV02Controller.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/DTOs/Scenarios/AEDV02Contracts.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Data/AppDbContext.cs` | Versioned pilot persistence, indexes and EF model |
| `EchoProtocol.Backend/src/EchoProtocol.Api/EchoProtocol.Api.csproj` | Pilot integration wiring; inspect feature diff for exact change |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Entities/AEDV02Decision.cs` | Versioned pilot persistence, indexes and EF model |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Migrations/20261014102100_AddAEDV02PilotLedger.Designer.cs` | Versioned pilot persistence, indexes and EF model |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Migrations/20261014102100_AddAEDV02PilotLedger.cs` | Versioned pilot persistence, indexes and EF model |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Migrations/AppDbContextModelSnapshot.cs` | Versioned pilot persistence, indexes and EF model |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Program.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDPhase8LifecycleReconcilerV1.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02DecisionService.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02SceneRegistry.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02SourceVerifier.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/TelemetryService.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/TelemetryV11Validator.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/TelemetryV12LifecycleValidator.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/TelemetryV13AEDValidator.cs` | Authenticated ingestion / verification / approval / receipt boundary |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02PilotPolicy.cs` | Mirrored versioned policy / source / budget contract |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02Plan.cs` | Mirrored versioned policy / source / budget contract |
| `EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02WorldSource.cs` | Mirrored versioned policy / source / budget contract |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02CabinetSourceTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02CarrierTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02DecisionTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02SceneSourceTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02SourceTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02SupplyTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02TelemetryTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/MatchResultPostgreSqlIntegrationTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/MongoTelemetryEventRepositoryIntegrationTests.cs` | Executable regression / integration proof for the adjacent feature |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/TelemetryServiceTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/AgentScripts/AuditAEDZoneGates.cs` | Inspect/export or author actual scene source for independent verification |
| `KLTN/AgentScripts/AuthorAEDZoneEntries.cs` | Inspect/export or author actual scene source for independent verification |
| `KLTN/AgentScripts/ExportAEDV02Scene.cs` | Inspect/export or author actual scene source for independent verification |
| `KLTN/AgentScripts/InspectAEDCabinets.cs` | Inspect/export or author actual scene source for independent verification |
| `KLTN/AgentScripts/InspectAEDZoneEntries.cs` | Inspect/export or author actual scene source for independent verification |
| `KLTN/Assets/Resources/AED/Rev02SceneManifest.json` | Registered baked geometry, markers, cabinet bounds and door topology |
| `KLTN/Assets/Resources/AED/Rev02SceneManifest.json.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scenes/SciFi.unity` | Three source zone-entry markers at existing production transitions |
| `KLTN/Assets/Scripts/AI/AED/AEDPhase8AuthoritySourceCollectorV1.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDRuntimeSettings.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDV02ApiService.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDV02ApiService.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/AED/AEDV02SceneSource.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDV02SceneSource.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/AED/AEDV02SourceJson.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDV02SourceJson.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/AED/AEDZoneEntryMarker.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDZoneEntryMarker.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/AED/AEDv2Authority.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDv2AuthorityV02.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDv2AuthorityV02.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/AED/AEDv2GameplayBridge.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/AED/AEDv2MatchEvidenceCollector.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDMinionEvidenceCollectorV1.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDPursuitEvidenceCollectorV1.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDResourceDirectorV1.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02CabinetEncounter.cs` | Mirrored versioned policy / source / budget contract |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02CabinetEncounter.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02PilotPolicy.cs` | Mirrored versioned policy / source / budget contract |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02PilotPolicy.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02Plan.cs` | Mirrored versioned policy / source / budget contract |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02Plan.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02SupplyLedger.cs` | Mirrored versioned policy / source / budget contract |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02SupplyLedger.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02WorldSource.cs` | Mirrored versioned policy / source / budget contract |
| `KLTN/Assets/Scripts/AI/Common/AED/AEDV02WorldSource.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Listener/Perception/ListenerHearingSensor.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/AdaptiveStalkerTargetPolicy.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionCabinetRuntime.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionCabinetRuntime.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/Presentation/StalkerAudioController.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/Special/StalkerCabinetEncounterRuntime.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/Special/StalkerCabinetEncounterRuntime.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/AI/Stalker/Special/StalkerSpecialEncounterRuntime.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerController.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerHideSpotMemory.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerHideSpotSelector.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerHideSpotTypes.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerHidingInvestigation.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerPerceptionEvaluator.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerUnityHideSpotAdapter.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerVisionSensor.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/Interaction/PlayerInteraction.cs` | Pilot integration wiring; inspect feature diff for exact change |
| `KLTN/Assets/Scripts/TeamTools/TeamToolWorldSpawn.cs` | Deferred required/optional zone supply and immutable item lifecycle |
| `KLTN/Assets/Scripts/TeamTools/TeamToolWorldSpawnV02.cs` | Deferred required/optional zone supply and immutable item lifecycle |
| `KLTN/Assets/Scripts/TeamTools/TeamToolWorldSpawnV02.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Scripts/Telemetry/Core/TelemetryContracts.cs` | Versioned authoritative source serialization and collection |
| `KLTN/Assets/Scripts/Telemetry/Core/TelemetryEventFactory.cs` | Versioned authoritative source serialization and collection |
| `KLTN/Assets/Scripts/Telemetry/Core/TelemetryJson.cs` | Versioned authoritative source serialization and collection |
| `KLTN/Assets/Scripts/Telemetry/Core/TelemetryProductionAdapters.cs` | Versioned authoritative source serialization and collection |
| `KLTN/Assets/Scripts/UI/HUD/HUDCabinetWarning.cs` | Guarded sensory, fairness, hiding, Cabinet runtime or real warning presentation |
| `KLTN/Assets/Scripts/UI/HUD/HUDCabinetWarning.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Common/AEDResourceDirectorV1Tests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02CabinetEncounterTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02CabinetEncounterTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02NavGeometryTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02NavGeometryTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02PilotPolicyTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02PilotPolicyTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02PlanTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02PlanTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02SupplyLedgerTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Common/AEDV02SupplyLedgerTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Listener/Editor/RuntimeNoiseAndHearingTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Networking/AEDV02HostGuardTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Networking/AEDV02HostGuardTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Networking/AEDV02SceneAuthoringTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Networking/AEDV02SceneAuthoringTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Networking/AEDV02SourceSerializationTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Networking/AEDV02SourceSerializationTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/EditMode/Stalker/AdaptiveStalkerTargetPolicyTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Stalker/StalkerPhase1HideSpotTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/EditMode/Stalker/StalkerSpecialEncounterRuntimeTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/PlayMode/Networking.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/PlayMode/Networking/AEDV02FusionStateTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/PlayMode/Networking/AEDV02FusionStateTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/PlayMode/Networking/EchoProtocol.Networking.PlayMode.Tests.asmdef` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/PlayMode/Networking/EchoProtocol.Networking.PlayMode.Tests.asmdef.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/PlayMode/Stalker/AEDV02CabinetCuePlayModeTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/Tests/PlayMode/Stalker/AEDV02CabinetCuePlayModeTests.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/Tests/PlayMode/Stalker/StalkerMultiCandidateVisionPlayModeTests.cs` | Executable regression / integration proof for the adjacent feature |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Coordinator.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Coordinator.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Source.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Source.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/_Project/Scripts/Networking/Interaction/NetworkInteractable.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Interaction/NetworkTeamToolPickup.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchStateV02.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchStateV02.cs.meta` | Stable Unity asset GUID / assembly metadata |
| `KLTN/Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `KLTN/Assets/_Project/Scripts/Networking/Session/LobbyManager.cs` | Host staging, canonical source, gameplay consumer, Fusion commit or lifecycle |
| `docs/aed/AED_V02_Deviation_Log.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/AED_V02_Implementation_Map.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/AED_V02_Remaining_Contract_Decisions.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/AED_V02_Source_Contract_Rev02.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/AED_V02_Traceability.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/AED_V02_Verification_Report.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/evidence/2026-10-15-local-verification.json` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/rev02/AED_Dynamic_Pacing_Design_v0.2_Stalker_Rev02_Complete.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/rev02/AED_Stalker_V0.2_Bang_Duyet_Rev02.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/rev02/AED_V02_AGENT_IMPLEMENTATION_MASTER_PROMPT.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `docs/aed/rev02/AED_V02_FINAL_E2E_ACCEPTANCE.md` | Approved requirements, traceability, proof or explicit remaining gate |
| `scripts/Sync-AEDV02Contract.ps1` | Reproducible regression / CI / byte-identical mirror validation |
| `scripts/Test-AEDV02.ps1` | Reproducible regression / CI / byte-identical mirror validation |

Excluded user changes: RuntimeLogSettings.asset and LiberationSans SDF - Fallback.asset. No environment secrets/local Docker override are included.

## Implementation-only continuation from 243eceec

All entries are source/asset/docs changes, UNTESTED. Owner assets, credentials and artifacts are excluded.

- EchoProtocol.Backend/src/EchoProtocol.Api/Content/AEDV02/Rev02SceneManifest.json
- EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02JammerVerification.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02OpportunityReconstruction.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02ScannerVerification.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDV02SourceVerifier.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02PilotOpportunities.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02PilotPolicy.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02Plan.cs
- EchoProtocol.Backend/src/EchoProtocol.Api/Shared/AEDV02WorldSource.cs
- EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02JammerOpportunityTests.cs
- EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/AEDV02ScannerOpportunityTests.cs
- KLTN/Assets/Editor/AED/ExportAEDV02Scene.cs
- KLTN/Assets/Resources/AED/Rev02SceneManifest.json
- KLTN/Assets/Scenes/SciFi.unity
- KLTN/Assets/Scripts/AI/AED/AEDV02SceneSource.cs
- KLTN/Assets/Scripts/AI/AED/AEDV02SourceJson.cs
- KLTN/Assets/Scripts/AI/Common/AED/AEDV02PilotOpportunities.cs
- KLTN/Assets/Scripts/AI/Common/AED/AEDV02PilotPolicy.cs
- KLTN/Assets/Scripts/AI/Common/AED/AEDV02Plan.cs
- KLTN/Assets/Scripts/AI/Common/AED/AEDV02WorldSource.cs
- KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs
- KLTN/Assets/Scripts/Tools/Scanner/FieldScannerCoreDetector.cs
- KLTN/Assets/Scripts/Tools/Scanner/NetworkFieldScanner.cs
- KLTN/Assets/Tests/EditMode/Networking/AEDV02HostGuardTests.cs
- KLTN/Assets/Tests/EditMode/Networking/AEDV02SourceSerializationTests.cs
- KLTN/Assets/_Project/Scripts/Networking/Authority/AEDV02NativeHarness.Controls.cs
- KLTN/Assets/_Project/Scripts/Networking/Authority/AEDV02NativeHarness.Controls.cs.meta
- KLTN/Assets/_Project/Scripts/Networking/Authority/AEDV02NativeHarness.cs
- KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Coordinator.cs
- KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Source.cs
- KLTN/Assets/_Project/Scripts/Networking/Interaction/NetworkDoorJammer.cs
- KLTN/Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs
- KLTN/Assets/_Project/Scripts/Networking/Player/NetworkPlayerFlashlight.cs
- KLTN/Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs
- KLTN/Assets/_Project/Scripts/Networking/Session/NetworkBootstrap.cs
- docs/aed/AED_V02_Changed_Files.md
- docs/aed/AED_V02_Execution_Checklist.md
- docs/aed/AED_V02_Implementation_Map.md
- docs/aed/AED_V02_OWNER_TEST_RUNBOOK.md
- docs/aed/AED_V02_Pilot_Contract_Defaults.md
- docs/aed/AED_V02_Remaining_Contract_Decisions.md
- docs/aed/AED_V02_Source_Contract_Rev02.md
- docs/aed/AED_V02_Traceability.md
- docs/aed/AED_V02_Verification_Report.md
- scripts/AEDV02NativeProof.ps1
- scripts/Assert-AEDV02LocalDocker.ps1
- scripts/Invoke-AEDV02AcceptanceScenario.ps1
- scripts/Invoke-AEDV02Campaign.ps1
- scripts/Invoke-AEDV02Native.ps1
- scripts/New-AEDV02NativeSession.ps1
- scripts/Send-AEDV02NativeCommand.ps1
- scripts/Start-AEDV02NativeBackend.ps1
- scripts/Stop-AEDV02NativeBackend.ps1
- scripts/Test-AEDV02.ps1
- scripts/Test-AEDV02NativeProof.ps1
