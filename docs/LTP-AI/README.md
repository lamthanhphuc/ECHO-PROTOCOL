# ECHO PROTOCOL — AI / Telemetry / Research Canonical Documents

This directory is the canonical entry point for AI, Telemetry, Player / Team Profile, AED / ScenarioConfig, and Fixed-vs-Adaptive Experiment documentation.

## Current Canonical Sources

| Area | Canonical Source |
|---|---|
| AI Architecture | `canonical/AI_Architecture_v1.1.md` |
| Stalker AI | `canonical/Stalker_AI_Design_v1.1.md` |
| Listener AI | `canonical/Listener_AI_Design_v1.0.md` |
| Warden AI | `canonical/Warden_AI_Design_v1.0.md` |
| Telemetry | `canonical/Telemetry_Contract_v1.1.md` |
| Player / Team Profile | `canonical/Player_Team_Profile_Contract_v1.1.md` |
| AED / ScenarioConfig | `canonical/AED_ScenarioConfig_Contract_v1.1.md` |
| Fixed vs Adaptive Experiment | `canonical/Fixed_vs_Adaptive_Experiment_Contract_v1.1.md` |
| M2 AI Implementation | `canonical/M2_AI_Implementation_Plan_v1.0.md` |
| GenAI Mission Briefing | `canonical/M1-019_GenAI_Mission_Briefing_Scope_Safety_Contract_v0_FINAL.md` |

## Source Precedence

```text
current canonical BASELINED / LOCKED document
>
historical M1 predecessor
```

Current repository code is implementation evidence.

Repository code does NOT silently override canonical contracts.

If code conflicts with a canonical contract, classify the conflict as:

- implementation gap;
- migration requirement;
- bug;
- explicit escalation.

Filename suffixes such as `(1)`, `(2)`, and `(3)` are copy/upload suffixes and are NOT semantic contract versions.

## Archive Policy

Files under `docs/LTP-AI/archive/` are historical/reference evidence only.

They MUST NOT be used as current implementation authority when a canonical replacement exists.

They may remain for:

- provenance;
- migration reasoning;
- project history;
- comparison with predecessor designs.

## Historical Reference Rule

Canonical documents may still mention filenames such as:

- `AI_Architecture_Traditional_vs_Modern.md`
- `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md`
- `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`
- `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`
- `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`
- `Telemetry_Event_Schema_v0_FINAL.md`

These references are allowed when they represent historical predecessor, provenance, superseded dependency, or design lineage.

They are NOT active implementation authority.

Do not modify locked contracts merely to replace these historical citations.

## Current M2 Governance

Implementation Plan Target Scope Mode: `ACCELERATED_FEATURE_COMPLETE_ALPHA`

Formal PM M2 Acceptance Mode: `OFFICIAL_BASELINE`

Project-Management Rebaseline Required: `YES`

The accelerated Feature-Complete Alpha scope is the current implementation execution target. It does NOT automatically become the formal PM M2 acceptance gate.

It becomes formal M2 scope only after an approved Project Plan / PM baseline revision explicitly promotes it.

Do NOT edit Project Plan or PM Baseline as part of AI / Telemetry documentation governance cleanup.

## Current Implementation Status

Current Implementation: `PARTIAL`

M2 Accelerated Feature-Complete Alpha: `NOT READY`

Main Fixed-vs-Adaptive Experiment: `NOT READY`

Implementation work should follow `canonical/M2_AI_Implementation_Plan_v1.0.md`.

Documentation baseline COMPLETE does NOT mean implementation COMPLETE.
