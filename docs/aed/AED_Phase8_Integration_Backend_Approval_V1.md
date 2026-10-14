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

### Source verification boundary (2026-10-14)

`AED_PHASE8_LIFECYCLE_SIDECARS_V1` can reconcile accepted phase events, Tool ownership/charges/drop lineage, and Minion spawn/despawn transitions against Host sidecars. This proves the submitted lifecycle rows match accepted telemetry; it does not prove every world object or threat episode was emitted. `AEDPhase8AuthoritySourceCollectorV1.CoverageComplete` remains false for that reason.

| Approval input | Current authoritative evidence | Missing contract or source | Approval status |
|---|---|---|---|
| Historical skill | Canonical down/revive/objective/noise counts and match-scoped profiles | Production metric observations with resolved opportunity, comparable cross-match context, profile lineage and calibrated confidence; several episode metrics are explicitly Unsupported in `AED_Metric_Opportunity_Contract_V1` | Unsupported |
| Current pressure | Host gameplay state and research Minion/Stalker facts; canonical consequences | Versioned Host threat windows, causal joins, source coverage and calibrated policy; raw noise count and phase DownCount are insufficient | Unsupported |
| Supply reachability | Accepted Tool lifecycle and charges, including `WorldSpawn`, `ScenePlaced`, and `LobbyLoadout` origins | Complete world census, NavMesh path to required objective/resource, immutable placement and accessibility receipts | Unsupported |
| Minion population | Accepted spawn/despawn lifecycle with identity, zone and tick | Host world census at boundary, including zero-population evidence and reconciliation with all live Fusion objects; population adaptation remains research-only under Phase 6 | Unsupported |

The Backend must reject a submitted metric row or `reachable=true` claim under this source version because neither can be reconstructed independently. A valid lifecycle upload may return `P8_SOURCE_AUTHORITATIVE_COVERAGE_INCOMPLETE`; it must not set the four extended verification flags. Gameplay approval remains blocked until the missing contracts and versioned producers/verifiers are approved together. The existing plan-authorization tests that seed all flags verify the downstream gate in isolation; they are not proof that live evidence can reach it.

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
