# Rev02 pilot: unresolved contract and implementation gates

This feature is **not ready for gameplay acceptance**. Approval of A–G authorizes the published values; it does not supply the definitions that Rev02 explicitly leaves uncalibrated. No entry below is converted to Verified by a fixture, an admin observation row or a client boolean.

| Gate | What is established | Missing definition / work | Current behavior |
|---|---|---|---|
| Historical competence | Comparable-context dimensions, resolved opportunities, minimum research sample rules | Versioned conversion of resolved outcomes/opportunities to normalized competence, confidence, weakest-member threshold and recency rules; authoritative collection of the resulting approved opportunity families | New adverse changes HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED. Raw/admin observations do not certify competence. |
| Player Resource pressure | Revive need, accepted Down/FAK consume/revive events, world/inventory/custody/charges, independent mesh/door access | Exhaustive need/opportunity definitions for the other required tools; identity and separation of scarcity windows; rules for consumed versus intentionally unused supply | Revive scarcity contributes to a partial breakdown; ResourceNeedOutsideRevive remains missing. |
| Player Objective pressure | Real active operator/unit identity in authoritative snapshots | Complete operation start/end/interruption events and independently joined cause/outcome intervals; credit when another operator resumes/completes the unit, carrier fatigue and overlapping consequences | ObjectiveThreatInterruption remains missing. A sampled operator or slow progress alone never proves threat-caused blocked seconds. |
| Team pressure TP0–TP4 | The spec distinguishes team TP from PP and global budget | Aggregate formula, band boundaries, confidence and weakest-player protection; Rev02 explicitly says Quiet is not a calibrated TP band | TeamPressureStatus=Unsupported; Cabinet has an explicit HOLD_TEAM_PRESSURE_UNSUPPORTED gate. Low individual PP alone cannot bypass this. |
| Long chase Seek pause | Approved pause is 15 seconds | Definition of a qualifying long chase and its resolved episode cutoff | No invented long-chase threshold is added. Existing baseline Seek/recovery behavior remains. |
| Placement R-1/R1/R2/R3 | Approved semantic goals, production zone entries, real geometry and doors; required types cannot be reduced | Scene roles identifying main/first/mandatory/safe routes and the exact placement preference/validation contract; native non-R0 planner and independent effect verification are unfinished | Non-R0 changes HOLD_RESOURCE_PLACEMENT_SOURCE_REQUIRED on Backend and Host. R0 deferred reservation/spawn has its own source proof. |
| Support/emergency spawn | At most one extra per relief transaction; one emergency event in the pilot; existing items immutable | Total ordinary-support allowance, eligible need-to-tool mapping and safe-window treatment of an actively Downed player. Generic safe windows currently require all players Alive. Separate action/receipt implementation is still required | No unsupported support event is issued or acknowledged. Initial v1 support carried into the new optional budget is a different, implemented path. |

## Evidence that can already be verified independently

- Live Tool and Minion population, including zero Minions; actual spawn/despawn and tool ownership/charge history.
- Minion steal/drop joined to a real live actor and tool lineage, without treating a carried object as missing world supply.
- Registered tool markers, the actual baked mesh, closed-door footprints and the two real Zone2 progression gates.
- Required runtime supply separately from scene-placed items. Optional reservations, initial allowance and materialization counters are replayed against accepted spawn events.
- Cabinet entry clue identity, authenticated Host ingestion, range and registered cabinet bounds, known Stalker/player before/after lineage. The actual Host sensor supplies FOV/LOS; Backend validation is not a second physics raycast or full physics replay.
- Stalker/Minion exposure and canonical Down/revive consequences, with current freshness, roster, revision and source fingerprints.

## Engineering/runtime proof still outstanding

The following are **not** replaced by a contract decision or by green automated tests:

1. A single native Host run joining real Backend approval, NetworkMatchState commit, actual gameplay effect, remote Client reconstruction and Backend receipt. Backend service integration, Host guard/consumer tests, physical sensor tests and Fusion Single storage tests are currently separate proofs.
2. Live P4 grant and Cabinet success from fully canonical five-channel PP and calibrated team TP. Their mechanics are implemented and tested with controlled policy inputs; those inputs are not proof that the missing authoritative aggregate exists.
3. Non-R0 resource placement and regular/emergency support implementation after the missing semantics above are fixed. These features remain unfinished, rather than being called completed behind a disabled flag.
4. Actual Photon reconnect/late join/Host shutdown, Voice initialization and input-buffer behavior with matched feature builds. Existing lower-level regression coverage is not a transport E2E result.
5. Pilot activation and the twelve owner acceptance scenarios. Keep all gameplay defaults OFF; do not ask the owner to accept this checkpoint as an E2E-ready pilot.
6. Final fetch: main advanced to `31ca1603` while LTP remains `10391b1d`. Its Resonance/Minion changes and new medkits Lobby toggle are not in this feature base. Future synchronization must preserve Fixed's new settings while formally reconciling the Adaptive five-required-types invariant. Current-main gameplay parity is UNVERIFIED; no automatic merge was performed.

The existing V1 Phase8 verifier remains fail-closed. Nothing in the new source protocol changes Telemetry 1.1 or AED_V2_PHASE_EVIDENCE_V2 semantics.
