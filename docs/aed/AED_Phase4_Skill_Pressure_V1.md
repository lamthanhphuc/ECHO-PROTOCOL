# AED Phase 4 — Skill & Pressure V1

Historical Skill:
- Source: Backend-verified AED metric observations only
- Required: comparable context, distinct matches, resolved opportunity
- Dimensions: Survival, Evasion, Objective, MinionCounterplay,
  ToolEffectiveness, ResourceManagement, Teamwork, RiskStyle
- RiskStyle: descriptive, not competence
- Legacy NoiseScore: not skill evidence
- ActiveObservedSeconds: coverage only
- MinimumDistinctMatches: 3 (research candidate)
- MinimumResolvedOpportunities: 8 (research candidate)
- HistoricalSkillVersion: AED_HISTORICAL_SKILL_V1

Team:
- TeamKey: MatchId
- Full roster required
- Mean, weakest, variance retained
- Cold-start member blocks increase
- TeamSkillVersion: AED_TEAM_SKILL_V1

Current Pressure:
- Host-authoritative Stalker/Minion evidence
- Recent consequences and threat state windows
- Survival DownCount: phase-level only, not recent timestamp
- No raw noise-based skill/pressure scoring
- Invalid/incomplete source => Unknown
- Version: AED_CURRENT_PRESSURE_V1_RESEARCH

Policy:
- Phase 4 outputs research candidates only
- No AED gameplay commit
- No backend verification of Unity sidecar yet
- Phase 8 will integrate verified metric transport/approval
- Phase 9 calibrates thresholds and compares Fixed vs Adaptive

Unsupported:
- Stalker verified escape rate
- Stalker full reacquisition denominator
- Minion evasion rate
- Minion distraction resolved-failure rate
- Flashlight opportunity denominator
- Cross-match TeamId
- Production historical skill ingestion of new sidecars
