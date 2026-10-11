# AED Phase 8 — Integration & Backend Approval

## Ordinal contract

- Evidence first phase ordinal = 1
- NetworkMatchState initial PhaseOrdinal = 0
- First post-transition network ordinal = 1
- Backend decision ordinal = completed evidence ordinal
- Stalker research target ordinal = completed ordinal + 1

Do not subtract one when calling the existing `AEDv2PhaseEvidenceVerifier`.

## Existing production approval

`ScenarioAdaptivePlanV2Service` retains host/lease, snapshot/roster/revision, canonical fingerprint, bounded one-key decisions, replay, and PENDING → APPLIED/ABORTED validation.

## Extended directors

Historical Skill, Team Skill, pressure sidecars, TeamTool Supply, Minion Population, and Target Fairness remain unverified or research-only.

## Canonical probe

- Endpoint: `POST director-evidence-v1`
- Authenticated host only; InMatch only
- Requires accepted canonical telemetry
- Does not approve extended metrics or gameplay

## Activation requirements

Gameplay adaptation remains disabled until extended evidence, resource/minion receipts, independent backend verification, approval binding, host revalidation, and replay/apply/abort tests exist.

## Rollout

1. Offline unit tests
2. Backend integration tests
3. Shadow Host/Client
4. Production approval tests
5. Later gated gameplay activation

Default:

- `extendedPolicyShadowEnabled = false`
- `extendedPolicyGameplayEnabled = false`
