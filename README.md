# AED v2 E2E logging — no gameplay changes

1. Copy `KLTN/Assets/Scripts/AI/AED/AEDv2E2ELog.cs` to the same path under your Unity repository; if this file already exists, add only `State(...)` helper from this version. Unity automatically generates a `.meta` file for new scripts.
2. Place `AEDv2E2ELog.State("BOUNDARY_BEGIN")`, `State("BOUNDARY_HOLD")`, `State("PROPOSAL")`, `State("REVALIDATE_PASS")`, `State("REVALIDATE_FAIL")`, `State("LOCAL_COMMIT")`, `State("PHASE_ADVANCED")`, `State("RECEIPT_WAIT")`, `State("ABORT")` at REAL event/transition sites in your local `AEDv2BoundaryCoordinator.cs` and `NetworkMatchState.cs`. Include decisionId via named argument only if the in-scope transaction has it. Never put these calls in Update/FixedUpdate/Render.
3. Instrument `AEDSnapshotApiService.SubmitPlanAsync()` before HTTP POST, after receiving HTTP result, and in canceled catch; instrument `ConfirmPlanAppliedAsync()` likewise. Use `Write()` and existing request/response metadata only, not Authorization headers or request/response bodies.
4. For gameplay consumer proof, log once when an AED revision changes and the Stalker/revive consumer ACTUALLY updates its tuning, not on each frame.
5. Run Fixed / Shadow / Gameplay with distinct match IDs and save logs using `collect-aed-v2-logs.ps1` and `query-aed-v2-status.sql`.

All traces are editor/development-only. `AEDv2E2ELog.State` captures phase, ordinal, revision and fingerprint, and infers role from Fusion state authority. It is *not* a substitute for HTTP approval/receipt instrumentation.

Requires Unity sources/types used by the previous code snapshot: `NetworkMatchState`, `MatchAuthorityRuntime`, `ScenarioConfigAuthorityRuntime`, `AEDv2Authority`, `AEDv2Plan`. The local uncommitted coordinator has not been reviewed; paste event calls into the actual branch transitions yourself.
