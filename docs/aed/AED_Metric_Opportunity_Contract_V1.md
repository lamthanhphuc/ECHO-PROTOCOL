# ECHO-PROTOCOL — AED Metric & Opportunity Contract V1

**Loại:** Hợp đồng thiết kế và kế hoạch triển khai Phase 1 (không phải bằng chứng rằng runtime đã triển khai).  
**Source baseline:** GitHub `lamthanhphuc/ECHO-PROTOCOL`, branch `LTP`, HEAD `c9577cc517b45f44cc0fe8a84bd314fae39e8adb` (2026-10-10).  
**Contract ID đề xuất:** `AED_METRIC_OPPORTUNITY_V1`.  
**Phạm vi:** Stalker, Minion, Survival, Objective, TeamTool, Resource, Noise, Movement, Teamwork, Player Profile, Team Profile, Current Match Pressure.  
**Không thuộc Phase 1:** sửa telemetry, schema DB, fingerprint, AI state machine, spawn, gameplay policy hoặc bật adaptation.

## 0. Các điều kiện bất biến

1. Mọi event dùng cho adaptive phải bắt nguồn từ Fusion Host/State Authority, có identity hợp lệ, provenance, phase/zone và căn cứ kiểm chứng.
2. Đếm một metric bằng **kết quả trong cơ hội hợp lệ**, không dùng raw activity count làm skill. Nếu mẫu số bằng 0: `NoOpportunity`, value `null` — tuyệt đối không thay bằng 0% hoặc 100%.
3. Không diễn giải player đứng yên/quiet 30 giây là giỏi: `ActiveObservedSeconds` chỉ là *coverage*, không phải sample gameplay.
4. Không cho AED học sai từ chính sự thay đổi nó đã áp dụng: lưu `configSource`, applied plan/revision và các tham số điều kiện tại episode.
5. Không cho tăng độ khó khi sample thấp/không đầy đủ/không công bằng; `Available` không mặc nhiên đồng nghĩa `DecisionEligible`.
6. Không cho episode trùng nhau tự nhân sample; mỗi outcome được commit tối đa một lần; `Censored` không biến thành thắng/thua.
7. `Fixed` phải có bản đo tương đương nhưng không được apply quyết định AED; không xóa, chỉnh vật phẩm đã sở hữu/đã xuất hiện.
8. Khi chưa implement nguồn dữ liệu chính thức: `Unsupported` (không giả dữ liệu); nếu nguồn hỗ trợ nhưng mất event bắt buộc: `Incomplete`.
9. Schema telemetry `1.1` và fingerprint `AED_V2_PHASE_EVIDENCE_V2` được giữ nguyên trong Phase 1.
10. Các ngưỡng confidence, thời gian grace, outcome window và trọng số trong tài liệu này là **tham số đề xuất cần calibrate ở Phase 3–4/9**, không phải giá trị tối ưu hoặc giá trị đang chạy.

## 1. Kiểm kê source ↔ bằng chứng hiện tại ↔ lỗ hổng

| Cơ chế | File/method / event thực sự có | Trạng thái nguồn đo hiện có | Thiếu để đạt mục tiêu |
|---|---|---|---|
| Match/phase | `NetworkMatchState.cs` → `RecordCompletedPhase`, `TryAdvancePhase`; `MatchAuthorityRuntime.RecordPhaseStarted/Completed`; telemetry `MATCH_STARTED`, `PHASE_STARTED`, `PHASE_COMPLETED`, `MATCH_ENDED` | Canonical v1.1 | Metric phase-specific objectives, exposure window/phase context |
| Core | `NetworkSectorBox.PlaceCarriedCore` → `NetworkMatchState.TryCompleteCoreObjective`; canonical `CORE_PICKED_UP`, `CORE_DROPPED`, `CORE_PLACED` | Events có; **không được tính vào `ObjectiveProgress` v2** | Core objective units và attribution, rejected placements, denominator |
| Puzzle | `MatchAuthorityRuntime.RecordPuzzleCompleted` → `AEDv2MatchEvidenceCollector.RecordAcceptedObjective` | Có, v2 `ObjectiveProgress` chỉ đếm `PUZZLE_COMPLETED` | Cần phân biệt Core, relay, Security Hold, Zone3 |
| Down/elimination | `NetworkPlayerLifeState.CommitDown/CommitEliminated`; `PLAYER_DOWNED`, `PLAYER_ELIMINATED`, `PLAYER_REVIVED` | Có | Revive limit có thể eliminate trực tiếp, không sinh Down; phải đo lethal consequence riêng |
| Noise | `NoiseTelemetryAdapter.SupportsNoiseType`: `SPRINT`, `INTERACTION`, `CORE_CARRY`, `CORE_DROP`, `NOISE_MAKER` | Chỉ năm loại canonical; accepted-noise count không phải noise gây phát hiện | Liên kết source noise với actual AI response/detection |
| Minion alert | `CreepMinionRuntime.TrySendStalkerAlert` → `HostRuntimeNoiseService.TryAccept`, `RuntimeNoiseType.MINION_ALERT` | Gameplay accepted nhưng ngoài schema telemetry 1.1 | Alert accepted và Stalker response episode; không giả làm SPRINT |
| Minion attack | `CreepMinionRuntime.AttackPlayer`, `CommitAttack` (`ShootSlow/StealTool/StealCore`) | Host gameplay confirmed; **chưa đủ per-player outcome telemetry** | Attack attempted/accepted/effect/target, episode id, recovery |
| Minion core | `CreepMinionRuntime.TryStealCore`: forced drop thành công vẫn trả true nếu monster carry thất bại | Có hành vi gameplay | `CORE_FORCED_DROP` tách `CORE_STOLEN`; recovered and outcome |
| Minion flashlight | `CreepMinionRuntime.TryGetFlashlightSource/BeginFlashlightDeath` | Host có kill | Multi-player contribution/source: hiện method không trả user-id cho scorer |
| Stalker FSM | `StalkerController` (Detection Decay, CHASE, SEARCH, target switch), `StalkerFusionRuntime.PublishAuthoritativeState` | Có state/replicated target; không có pursuit episode canonical | Episode start/lost/reacquired/end/censored, không coi SEARCH timeout là escape |
| Stalker research | `StalkerFusionRuntime.PublishCommittedTelemetryFacts`; `MONSTER_ATTACK_RESOLVED`, `MONSTER_SEARCH_ENDED` | ResearchCapture, không tự dùng AED production | Authoritative player-targeted chase evidence |
| Tool used | `NetworkPlayerInteractor`, `PlayerTelemetryAdapter.EmitTeamToolUsed`: Scanner/NoiseMaker/FirstAid/DoorJammer | `TEAM_TOOL_USED`; **CoreStabilizer chưa canonical** | ToolEffectResolved, opportunity, available stock, source & target |
| Tool world supply | `TeamToolWorldSpawn.TrySpawnInitial`, `RequiredToolIds` có đủ 5 tool mỗi zone | Spawn đủ 5 types/zone trong baseline | Spawn receipt/loadout/ownership, per-zone plan, distance/reachability |
| Scanner | `NetworkFieldScanner` có scan pulse và local result | Gameplay/local scanner result | Host-verifiable useful detection, không dùng local display làm outcome authority |
| Player skill | Backend `PlayerAIProfile` có Survival/Noise/Objective/ToolUsage và dimension fields | Có lineage/revision/samples | Evasion, Minion counterplay, resource effectiveness, confidence/opportunity |
| Team profile | Backend `TeamProfile` gắn `MatchId` (teamKey), có ObjectiveTime/ReviveSuccess/ResourceEfficiency | Match-scoped profile | Multi-dimensional team pressure & weakest-player guard |
| AED gate | `AEDv2BoundaryPolicy.TryPropose`, `AEDv2RosterSafety.FromEvidence` | Has safe boundary, 30s coverage; policy còn dùng noise count làm relieve | Granular HOLD & confidence gates, không tự suy skill từ sprint |
| Backend verifier | `AEDv2PhaseEvidenceVerifier.VerifyAsync` | V2 fingerprint tính Down/Revive/Eliminated/Noise/Tool/PUZZLE_COMPLETED; không tính CORE_PLACED | Đổi ngữ nghĩa chỉ ở Phase 2 với contract migration và tests hai phía |

**Các file cần đối chiếu khi triển khai:**

- `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs`
- `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs`
- `KLTN/Assets/_Project/Scripts/Networking/Player/NetworkPlayerLifeState.cs`
- `KLTN/Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs`
- `KLTN/Assets/Scripts/AI/Stalker/StalkerController.cs`
- `KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs`
- `KLTN/Assets/Scripts/AI/Minions/CreepMinionRuntime.cs`
- `KLTN/Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs`
- `KLTN/Assets/Scripts/Tools/Scanner/NetworkFieldScanner.cs`
- `KLTN/Assets/Scripts/TeamTools/TeamToolWorldSpawn.cs`
- `KLTN/Assets/Scripts/TeamTools/TeamToolSpawnPoint.cs`
- `KLTN/Assets/Scripts/Telemetry/Core/TelemetryContracts.cs`
- `KLTN/Assets/Scripts/Telemetry/Core/TelemetryProductionAdapters.cs`
- `KLTN/Assets/Scripts/AI/AED/AEDv2MatchEvidenceCollector.cs`
- `KLTN/Assets/Scripts/AI/Common/AED/AEDv2BoundaryPolicy.cs`
- `EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDv2PhaseEvidenceVerifier.cs`
- `EchoProtocol.Backend/src/EchoProtocol.Api/Entities/PlayerAIProfile.cs`
- `EchoProtocol.Backend/src/EchoProtocol.Api/Entities/TeamProfile.cs`

## 2. Metric result model (P1.1)

### 2.1 Trạng thái

| Status | Khi sử dụng | `value` | Được làm cơ sở tăng pressure? |
|---|---|---|---|
| `Available` | Nguồn hỗ trợ, cơ hội >0, dữ liệu đầy đủ, kết quả giải được | number | Chỉ sau confidence/fairness gate riêng |
| `NoOpportunity` | Cơ hội xác thực =0 (không bị chase; không có Scanner; không có teammate Down...) | null | Không |
| `Unsupported` | Có gameplay nhưng chưa có host-authoritative verified outcome contract | null | Không |
| `Incomplete` | Có cơ hội nhưng thiếu/lost required evidence; đang chờ resolve window; thiếu quyền Host | null | Không |
| `Invalid` | Identity/phase/timestamp/roster không hợp lệ, mâu thuẫn, impossible transition, unreconcilable duplicate | null | Không |

`Pending` có thể là trạng thái nội bộ trước khi window đóng; **không xuất thành `Available` trước khi resolved**. Episode `Censored` không đồng nghĩa `Incomplete` nếu lý do censor hợp lệ và được ghi nhận.

### 2.2 Logical record bắt buộc (không thay DB trong Phase 1)

```text
MetricResultV1 {
  metricId, metricVersion="AED_METRIC_OPPORTUNITY_V1",
  matchId, phaseOrdinal, phaseName, zone, userId?, teamKey=matchId,
  difficulty, scenarioResolutionMode, configSource,
  scenarioConfigVersion, policyVersion, sourceTelemetrySchemaVersion,
  appliedPlanRevision?, appliedParameterFingerprint?,
  sourceSystem, authorityActor, sourceEventIds[], occurrenceKeys[], episodeIds[],
  eligibleOpportunities, resolvedSuccesses, resolvedFailures, censoredOpportunities,
  numerator, denominator, measurementUnit, value?,
  observedDurationSeconds?, confidenceEvidenceCount, confidenceStatus,
  status, reasonCodes[], windowStartedAtUtc, windowEndedAtUtc
}
```

**Không log raw personal identifiers ra Unity console**; phân tích server có thể dùng IDs xác thực. `sourceEventIds` là references, không sao chép profile history nhạy cảm sang client.

### 2.3 Quy tắc tính

- Tỷ lệ: `value = numerator / denominator` khi `denominator > 0`, không round trước khi quyết định.
- Down/Elimination rate là negative outcome density: ghi đơn vị/denominator; **không mặc định đảo dấu rồi gọi Skill**.
- So sánh giữa trận dùng điều kiện tương đương: map/phase/objective count/difficulty/team size/applied AED parameters.
- `eligibleOpportunities = success + failure + censored` cho từng loại; `denominator = success + failure` nếu censor loại trừ. Ghi lý do loại bỏ.
- Tỷ lệ không cộng tùy tiện cùng metric có độ khó encounter khác nhau; giữ event-level context và tính profile sau.
- Source canonical, research-capture và gameplay-only phải được dán nhãn nguồn riêng.
- Một episode thay đổi target có thể tạo *participant segments* nhưng không tạo nhiều encounter thắng giả.

## 3. Metric catalog V1 (P1.1–P1.4)

**Legend:** `Canonical` = telemetry 1.1 có event liên quan; `Partial` = logic gameplay có nhưng thiếu semantic event đầy đủ; `Unsupported` = không có host-verifiable metric record. Trạng thái ghi ở đây chỉ nói về **độ sẵn sàng triển khai nguồn**, không phải kết quả của một trận.

### 3.1 Survival / Threat

| Metric ID | Numerator | Denominator / Opportunity | Khi nào success/failure/không áp dụng | Source và readiness | AED usage |
|---|---|---|---|---|---|
| `Survival.DownPerThreatMinute` | Down hợp lệ | Phút Alive trong confirmed threat exposure (không phải tổng thời gian đứng yên) | NoOpportunity nếu không exposure; eliminate vì revive limit tách riêng | LifeState canonical Down; **Partial** threat window | RELIEVE / safeguard |
| `Survival.EliminationRate` | Lượt eliminated trong threat encounter | Eligible threat encounters có resolved result | Không gộp MatchEnd/Disconnected; mỗi Player tối đa 1 elimination per match | `PLAYER_ELIMINATED`, LifeState; **Partial** opportunity | RELIEVE |
| `Survival.ReviveSuccessRate` | `PLAYER_REVIVED` confirmed | Đồng đội Downed và teammate Alive có cơ hội hợp lệ tiếp cận/FirstAid | Revive thất bại do phase end/cutoff → Censored; solo no opportunity | Canonical revive; **Partial** eligibility | RELIEVE / TEAMWORK |
| `Survival.PostEncounterRecoverySeconds` | Tổng thời gian hồi phục hợp lệ | Số episode đã thoát áp lực và có mốc kết thúc recovery | Không đánh giá khi chưa có dangerous encounter | Stalker/Minion episode; **Unsupported** | Pacing |
| `Survival.LethalConsequenceRate` | Hit chuyển sang Eliminated trực tiếp (revive limit...) | Committed lethal hits có state outcome | Không tạo `PLAYER_DOWNED` giả | `CommitDown`, `CommitEliminated`; **Partial** | RELIEVE |

### 3.2 Stalker Evasion / Stealth

| Metric ID | Numerator | Denominator / Opportunity | Success/Failure/NoOpportunity | Source & readiness | AED usage |
|---|---|---|---|---|---|
| `Stalker.PursuitEscapeRate` | Episode resolve `Escaped` | Episode confirmed CHASE có target player, outcome resolved | SEARCH timeout một mình không đủ để gọi `Escaped`; target switch không tự thắng | `StalkerController`, `StalkerFusionRuntime`; **Unsupported** | Skill/Evasion |
| `Stalker.ReacquisitionRate` | `Reacquired` cùng target trong confirm window | Lost-target windows eligible | Chưa từng mất dấu → NoOpportunity; đổi target → censored/segmented | Controller detection decay & SEARCH; **Unsupported** | Risk/stealth, negative |
| `Stalker.ChaseDownRate` | Chase resolved `Downed` hoặc direct lethal consequence | Confirmed eligible target chase | Down sau chase chỉ tính khi nối cause trong outcome window | Controller + LifeState; **Unsupported** join | Pressure/RELIEVE |
| `Stalker.PursuitDurationSeconds` | Tổng thời gian theo đuổi đã resolve | Số resolved pursuit episodes | Không suy stamina/performance chỉ từ duration; giữ per-episode distribution | FSM Host; **Unsupported** | Pressure/Pacing |
| `Stalker.HideEscapeSuccessRate` | Escape hợp lệ có hide action confirmed | Threatened hide windows player thực sự dùng hide spot | Không suy `escaped` chỉ vì Stalker rẽ hướng | hide state + episode; **Unsupported** | Stealth |
| `Stalker.NoiseDetectionAttributionRate` | Noise được liên kết Host-confirmed với Stalker acquire | Noise events có cơ hội được Stalker nghe/nhìn trong đúng window | Sprint không gây detection là negative attribution, không là weak skill | canonical noise + host hearing; **Unsupported** | Risk/Threat |

### 3.3 Objective / Progress

| Metric ID | Numerator | Denominator / Opportunity | Success/Failure/NoOpportunity | Source & readiness | AED usage |
|---|---|---|---|---|---|
| `Objective.UnitCompletionRate` | Confirmed objective units completed | Units thực sự mở, hợp lệ, reachable trong phase | Core placed, relay complete, security hold complete, zone3 units tách `unitType`; không tính request bị reject | CORE_PLACED canonical; NetworkMatchState; **Partial** | Objective skill |
| `Objective.TimeEfficiency` | Baseline expected time for matched objective units (or inverse calibrated time) | Observed eligible active objective seconds | Không so khác objective/map/team size; phase chưa hoàn tất → censored | phase telemetry; **Partial** | Skill, pressure |
| `Objective.StallTimeSeconds` | Thời gian cửa sổ objective mở không tiến triển theo rule | Tổng thời gian objective có thể làm với player Alive | NoOpportunity khi objective locked; không penalize khi threat buộc evade | network objective state; **Unsupported** | Current pressure |
| `Objective.CoreTransportLossRate` | Core bị forced drop/hoặc stolen trong eligible carry episode | Eligible core carried-and-threatened exposures | `CORE_FORCED_DROP` khác `CORE_STOLEN`; không gộp voluntary drop | Core carry + Minion; **Partial** | Resource skill |

### 3.4 Minion encounter / Counterplay

| Metric ID | Numerator | Denominator / Opportunity | Success/Failure/NoOpportunity | Source & readiness | AED usage |
|---|---|---|---|---|---|
| `Minion.EvasionRate` | Player escaped Track/Harass mà không bị attack-effect, sau confirm window | Eligible Minion target segments có outcome resolved | Target switch, spawn/despawn, phase end → censored; không encounter → NoOpportunity | CreepMinionRuntime states; **Unsupported** | Skill/counterplay |
| `Minion.FlashlightDefenseRate` | Counterplay commit kill/repel có attributable contribution | Confirmed flashlight defensive opportunities khi beam có reach/LOS | Multi-player dùng contribution credit, không double count same Minion death | `BeginFlashlightDeath`, `TryGetFlashlightSource`; **Partial** | Skill |
| `Minion.SlowHitRate` | `ShootSlow` accepted effect | Attack attempts có thể Slow và eligible target | Không tính lần không vào shootRange, cooldown block | `TryApplySlowAuthoritative`, `CommitAttack`; **Partial** | Pressure |
| `Minion.ToolProtectionRate` | Encounter resolved không mất Tool | Target đã có Tool, Minion có cơ hội steal | Không có Tool → NoOpportunity; steal/relocate confirmed là thất bại | `TryRelocateTeamTool`; **Partial** | Resource skill |
| `Minion.CoreProtectionRate` | Core vẫn được giữ sau eligible steal opportunity | Target mang Core trong steal-range encounter | `Drop` nhưng carry thất bại vẫn là forced-drop loss, không ghi stolen | `TryStealCore`; **Partial** | Resource skill |
| `Minion.RecoveryRate` | Core/Tool được player thu hồi sau confirmed loss | Theft/forced-drop episodes có object reachable và window | Despawn không do player → censored; phải liên kết same object identity | inventory/core world state; **Unsupported** | Skill/support |
| `Minion.AlertImpactRate` | MINION_ALERT accepted kéo theo eligible Stalker investigative reaction | Accepted Minion alert windows có Stalker hợp lệ | Accepted alert không tự là Stalker chase; dedup repeated alert window | `TrySendStalkerAlert`, HostRuntimeNoiseService; **Unsupported** | Pressure |
| `Minion.DistractionSuccessRate` | NoiseMaker làm Minion đổi target/retarget có hiệu quả | Eligible Minion có thể nghe và đã exposed to NoiseMaker | Không tính deploy ngoài range hoặc khi Minion đã flee | `OnNoise`, NoiseMaker; **Partial** | Tool effectiveness |
| `Minion.ObjectiveDisruptionSeconds` | Seconds objective operator bị slow/steal/forced interruption attributable to Minion | Seconds active in eligible objective interaction | Không penalize player phải đứng thao tác objective | Minion + objective phase state; **Unsupported** | Current pressure |

### 3.5 TeamTool & Resource

| Metric ID | Numerator | Denominator / Opportunity | Success/Failure/NoOpportunity | Source & readiness | AED usage |
|---|---|---|---|---|---|
| `Tool.ScannerUsefulDetectionRate` | Scan có Host-confirmed useful target mới/đúng loại | Scan pulses hợp lệ trong phase và có eligible detectable target | Local HUD result không tự là authoritative outcome | NetworkFieldScanner; **Unsupported** | Tool objective effectiveness |
| `Tool.FirstAidReviveSuccessRate` | FirstAid consumed/used gắn `PLAYER_REVIVED` confirmed | Eligible revive attempts có FirstAid hợp lệ | Team solo không có teammate: NoOpportunity | `PLAYER_REVIVED` + life/interactor; **Partial** | Teamwork/support |
| `Tool.NoiseMakerDistractionRate` | Stalker/Minion chuyển target/điều tra noise do tool một cách xác thực | Deploy có monster eligible to hear trong window | Tool dùng không có monster cạnh không tự là failure/skill yếu | canonical TEAM_TOOL_USED/NOISE_EMITTED + AI; **Partial** | Tool effectiveness |
| `Tool.DoorJammerPursuitDelaySeconds` | Verified delay từ encounter qua cửa có Jammer active | Eligible threatened door passages with Jammer | Không so ở zone không có door opportunity | network door/jammer + Stalker; **Unsupported** | Support |
| `Tool.CoreStabilizerProtectedSeconds` | Seconds active buff trùng confirmed threat to protected player | Eligible protection threat seconds khi Buff có hiệu lực | Chỉ `activated` không là protected; gameplay-only ở v1.1 | `NetworkPlayerInteractor` buff/target exclusion; **Partial** | High-impact resource |
| `Tool.EffectivenessByType` | Successful outcome đơn vị riêng từng tool | Eligible activation windows theo type, không cộng 5 loại trực tiếp | Không cho một tool mạnh thống trị count mọi tool | Tool-specific outcomes; **Unsupported** aggregate | Skill |
| `Resource.LossRate` | Accepted stolen/forced drop/consumed ngoài ý muốn | Eligible carried resource threats, theo `core/tool` | Resource chưa có/không reachable: NoOpportunity | core/inventory + Minion; **Partial** | Resource demand |
| `Resource.AvailabilityCoverage` | Intervals hoặc objectives có resource cần thiết reachable | Intervals/objectives thật sự yêu cầu resource | Scan/tool reachability dùng NavMesh path distance, không dùng Euclidean-only | TeamToolWorldSpawn/Point; **Unsupported** | Resource Director fairness |
| `Resource.RecoveryTimeSeconds` | Tổng elapsed từ loss→recovered | Loss episodes resolved có recovery valid | Không có loss → NoOpportunity | Minion + NetworkItem ownership; **Unsupported** | Recovery demand |
| `Resource.WorldSupplyByTypeZone` | Spawned and accessible confirmed world tool count | Desired/allowed world supply plan count | Phân biệt lobby loadout vs world, consumable charges, stolen/drop/respawn | TeamToolWorldSpawn; **Partial** | Director readiness |

### 3.6 Movement, Teamwork, Noise, Pacing

| Metric ID | Numerator | Denominator / Opportunity | Success/Failure/NoOpportunity | Source & readiness | AED usage |
|---|---|---|---|---|---|
| `Movement.RiskExposureTime` | Host-confirmed seconds in valid threat area | Alive observed time có position/threat state hợp lệ | Không xem sprint đơn độc là nguy hiểm | NetworkPlayerMovement + Stalker/Minion; **Unsupported** | Pressure |
| `Teamwork.ReviveOpportunitySuccessRate` | Confirmed team revives | Legit downed teammate episodes with other Alive teammates and reachable FirstAid | Không đánh giá team solo bằng 0% | PlayerRevived + roster + resource; **Partial** | Team skill |
| `Teamwork.ObjectiveContribution` | Proven objective unit contribution/participation | Eligible objective roles/episodes participant có quyền làm | Không chia đều credit cho toàn team chỉ vì cùng trận | match objective network authority; **Unsupported** | Team skill |
| `Noise.DetectionCausingNoiseRate` | Noise actually attributed to acquire/investigate AI | Host-accepted noise trong radius/LOS/hearing eligible windows | Chỉ đếm canonical 5 loại nếu sử dụng telemetry 1.1; MINION_ALERT tách research | Noise adapter + Stalker/Minion; **Unsupported** | Threat diagnosis |
| `Pacing.HighIntensitySeconds` | Confirmed time weighted by active chase/down/harass | Valid phase alive observation seconds | Không gán intensity cao chỉ do raw noise | episodes & current pressure; **Unsupported** | Horror pacing |
| `Pacing.ReliefAfterDangerSeconds` | Quiet recovery duration following confirmed danger | Resolved dangerous episodes with alive survivors | Không gán relief cho disconnect or match ended | episode resolution; **Unsupported** | Horror pacing |

## 4. Stalker Pursuit Episode Contract (P1.2)

```text
StalkerPursuitEpisodeV1 {
  episodeId, matchId, phaseOrdinal, zone, stalkerNetworkId,
  targetUserId, targetPlayerRef, startedTick, startedAtUtc,
  chaseStartCause, phaseContext, configSource, appliedPlanRevision,
  segments:[{segmentId,targetUserId,firstChaseTick,lastSeenTick,
             searchEnteredTick?,patrolReturnedTick?,decayExpiresTick?,
             targetSwitchedAtTick?,reacquiredAtTick?,downTick?}],
  outcome: Escaped | Reacquired | Downed | Eliminated | TargetSwitched | Cancelled | Censored,
  endedTick?, endedAtUtc?, outcomeReason, sourceOccurrenceKeys[],
  measurementStatus
}
```

**Start:** Host xác nhận `CHASE` với chính `targetUserId` hợp lệ, không phải chỉ bắt đầu detect animation/audio.  
**SEARCH:** episode vẫn còn pending; không coi SEARCH hoặc PATROL là escape tự động.  
**Decay:** cùng target tái phát hiện khi detection decay còn phải được nối với episode đúng.  
**Reacquired:** cùng player bị reacquired trong confirmation window phiên bản hóa.  
**Target switch:** kết thúc hoặc segment hóa target cũ với kết quả `TargetSwitched/Censored`, không tự cộng success.  
**Escaped:** chỉ sau grace window (đề xuất thử nghiệm, chưa chốt threshold) mà không reacquired/down và với bằng chứng target đã thực sự thoát.  
**Downed/Eliminated:** liên kết Host commit life consequence, không suy từ animation/overlap.  
**Censored:** MatchEnd/phase cut, authority disconnect, Stalker despawn hoặc observer loss.  
**Attribution:** mỗi committed outcome có unique `(matchId, stalkerNetworkId, episodeId, outcomeKind)`; không replay/duplicate.

## 5. Minion Encounter Episode Contract (P1.2)

```text
MinionEncounterEpisodeV1 {
  episodeId, matchId, phaseOrdinal, zone, minionNetworkId,
  targetUserId, startedTick, stateSegments:Roam/Track/Harass/Flee,
  targetSwitches[], alerts:[{sourceId,accepted,stalkerResponseId?}],
  attacks:[{attackOrdinal,kind,targetUserId,attempted,accepted,
            effectKind?,appliedSeconds?,coreId?,toolInstanceId?,
            forcedDrop?,monsterCarryStarted?,itemRecovered?}],
  flashlightContributions:[{userId,visibleExposureSeconds}],
  deathCommitted?, distractionEpisodes[],
  outcome: Evaded | Affected | Countered | ItemLost | ItemRecovered | Censored,
  endedTick?, outcomeReason, sourceEventIds[], measurementStatus
}
```

Quy tắc bắt buộc:

1. `Roam` không tự tính là encounter. Episode bắt đầu khi Host thực sự acquire target để `Track`/`Harass`.
2. Đổi target tạo participant segment; **không nhân 1 Minion death thành 2 success** khi hai player cùng chiếu flashlight.
3. `ShootSlow`: phân biệt attempted, `TryApplySlowAuthoritative` accepted, duration thực nhận.
4. `StealTool`: chỉ loss khi `DropTeamToolAuthoritative` trả success; giữ identity instance/charges và eventual recovery.
5. `StealCore`: `DropCarriedCoreAuthoritative` success → `CORE_FORCED_DROP`; chỉ `TryBeginMonsterCarryAuthoritative` success → `CORE_STOLEN`.
6. `MINION_ALERT`: `HostRuntimeNoiseService.TryAccept` true là accepted alert; chỉ khi Stalker có response rõ ràng mới tính AlertImpact success.
7. Flashlight: phải ghi candidate contributor theo authoritative beam/LOS/time, không từ client-only HUD.
8. NoiseMaker: thay đổi target của Minion phải trace về noise event cụ thể và giữ eligible-opportunity window.
9. Phase cut / despawn / disconnect hoặc thiếu authority → `Censored`/`Incomplete` tùy trường hợp.
10. Tác động AED lên cap, respawn, alert cooldown, slow duration... phải được lưu vào episode context để profile không so sai.

## 6. Tool & Resource Opportunity Contract (P1.3)

### 6.1 Thực thể logic

```text
ResourceSupplyStateV1 {
  matchId, phaseOrdinal, zone, toolInstanceId, toolType,
  origin: WorldSpawn | LobbyLoadout | Drop | Recovery | SupportSpawn,
  planned?, spawned?, discovered?, pickedUp?, ownedByUserId?,
  remainingUses?, consumed?, displacedByMinion?, relocatedPosition?,
  recovered?, zoneAccessible?, navmeshPathDistance?,
  planRevision?, sourceSpawnReceiptId?, changedAtTick
}
ToolEffectEpisodeV1 {
  episodeId, matchId, toolInstanceId, toolType, actorUserId,
  opportunityId, targetId?, activatedTick, outcomeWindowEndedTick?,
  state: ResolvedSuccess | ResolvedFailure | NoOpportunity | Censored,
  effectUnit, effectValue, sourceEventIds[], sourceAuthority
}
```

### 6.2 Từng loại Tool

| Tool | Opportunity | ResolvedSuccess phải có | Không được coi là skill |
|---|---|---|---|
| `CORE_STABILIZER` | Buff hoạt động trong confirmed threat/core carry window | Protected seconds trước hit/targeting/harass theo Host evidence | Chỉ nhấn nút buff |
| `FIELD_SCANNER` | Host scanner pulse có detectable target trong supported range | Target hữu ích thực sự mới được xác nhận tìm thấy | Chỉ mở UI/scan rỗng |
| `FIRST_AID_KIT` | Có đồng đội Downed, reachable, có tool/charges | Confirmed `PLAYER_REVIVED` với kit linked | Solo hoặc không có Downed |
| `NOISE_MAKER` | Có threat có thể nhận noise | Stalker/Minion distraction được xác nhận liên kết nguồn | Chỉ deploy một beacon |
| `DOOR_JAMMER` | Có usable door và Stalker pursuing/approaching | Delay/deny passage measured bởi Host | Đặt Jammer ở hành lang không threat |

### 6.3 Resource Director fairness contract (chỉ thiết kế, triển khai Phase 5)

- **First Aid:** `GuaranteedFloor` phải đảm bảo cơ hội revive tối thiểu đã được kiểm chứng, theo team size và phase; không áp dụng chính sách chỉ vì một Player giỏi.
- **Core Stabilizer:** world supply có thể bị giới hạn theo zone/match khi high confidence và tổng loadout được xét; không xóa đồ đã nhặt/mua. Cần test phương án chỉ **1 world spawn/match** như một *candidate*, không là production default.
- **Scanner:** Team objective mạnh có thể giảm số hoặc chuyển `SpawnDistanceBand` xa hơn trên NavMesh, **không đặt sau objective/door cần Scanner để hoàn thành**.
- **Noise Maker/Door Jammer:** dựa vào threat-opportunity tương ứng và cooldown/remaining uses, không chỉ dựa total use.
- `TrySpawnInitial()` hiện spawn đủ 5 types *trong cả ba zone ngay từ đầu*. Việc chuyển sang per-zone adaptive spawn cần kiểm chứng `visible/discovered/owned/reserved` và zone entry ở Phase 5.
- Quyết định tài nguyên phải gắn `worldSupply` + `lobbyLoadout` + `charges` + `placed/displaced/recovered` và immutable spawn receipt.
- Không giảm tool floor và đồng thời tăng Stalker + Minion trong một boundary.

## 7. Player, Team, Current Pressure, Confidence Contract (P1.4)

### 7.1 Ba mô hình tách biệt

**Historical Player Skill:** dimension-scoped aggregates theo nhiều trận có comparable configuration, profile lineage/revision/version, sample/confidence riêng: Survival, Evasion, Objective, MinionCounterplay, ToolEffectiveness, ResourceManagement, Teamwork, RiskStyle (RiskStyle là phong cách, không phải điểm giỏi/yếu). Mọi dimension chưa đủ cơ hội giữ Uncertain; không lấy NoiseScore hiện tại làm skill âm.

**Team Model:** dùng full distribution, minimum/weakest-player risk, variance, roster size, available revive/resource roles, objective contribution, concurrent threats. `TeamProfile.MatchId` hiện là match-scoped team key, **không giả một cross-match persistent TeamId đã tồn tại**.

**Current Match Pressure:** decayed-window pressure từ active Stalker pursuit/attack, Minion harass/slow/forced drop, recent down/elimination/revive, scarce First Aid/Core, objective stall, insufficient relief. Không gộp với historical skill. Episode do AED tạo phải có config context.

### 7.2 Confidence

```text
ConfidenceInput {
  eligibleN, resolvedN, censoredN, sourceCompleteness,
  distinctMatchN, timeWindowCoverage, comparableContextCoverage,
  sensitivityToAEDActions, dimensionStatus
}
```

- `Available` = metric value đo được; **`DecisionEligible` = Available && đủ distinct opportunity && đủ contextual confidence && không bất công**.
- Ngưỡng `minimumResolvedEpisodes`, `minimumDistinctMatches`, hysteresis/cooldown **để calibrate và ghi version ở Phase 4/9**, không gắn mặc định 30 giây như skill.
- Số episode từ cùng một threat/cùng một match có correlation; không coi tất cả sample là độc lập.
- Team mạnh không được làm mất quyền bảo vệ Player yếu/cold-start; increase pressure cần full roster coverage và safety guard.
- Đề xuất label `Struggling`, `Stable`, `Dominating`, `Uncertain` là **output mục tiêu**, chưa phải enum production.
- Dữ liệu thiếu `NoOpportunity` hoặc `Unsupported` → HOLD cho chiều tương ứng, không suy player yếu.

## 8. Event, identity, window, versioning (P1.5)

### 8.1 Identity

| Identity | Quy tắc |
|---|---|
| `matchId` | Non-empty Host match GUID, trùng backend binding |
| `phaseOrdinal` | Tăng theo accepted phase start; lưu cả canonical phaseName/zone |
| `userId` | Backend verified UserId, không suy từ `PlayerRef` nếu binding mất |
| `teamKey` | Match-scoped `matchId` hiện tại |
| `sourceEventId` | Canonical TelemetryEvent Id khi có; Host gameplay-only có deterministic occurrence identity riêng |
| `dedupKey` | `(matchId, eventType, sourceOccurrenceKey)` đối với canonical; **sát code emitter `EventType + "|" + SourceOccurrenceKey`** |
| `episodeId` | Stable Host ID theo entity/target/started tick/ordinal; không dùng Unity instance id tái sử dụng đơn lẻ |
| `sourceTick` | NetworkRunner authoritative tick; `occurredAtUtc` để audit/ordering phụ |
| `context` | Policy/version/config source, difficulty, roster snapshot, phase, zone, applied plan params |

### 8.2 Episode/window

- `startTick <= endTick`; lag/chuyển phase không ghi hai resolution cùng một episode.
- End thuộc `ResolvedSuccess`, `ResolvedFailure`, `Censored` hoặc `Incomplete`; không đo cùng một outcome thành nhiều chiến thắng.
- Chỉ xem `Censored` khi đã ghi lý do: MatchEnd, phase break, AI despawn, player disconnect, state authority loss.
- Nếu available opportunity mà source required mất/gap sequence → `Incomplete`, không bỏ silently.
- Source research-only không tự upgraded lên canonical; `MINION_ALERT` gameplay accepted vẫn loại khỏi `AcceptedNoiseCount` v1.1.

### 8.3 Version matrix

| Artifact | Hiện tại trên LTP | Phase 1 quyết định | Phase triển khai |
|---|---|---|---|
| Telemetry wire | `1.1` | Không sửa | Phase 2+ nếu cần additive/bumped version |
| AED phase fingerprint | `AED_V2_PHASE_EVIDENCE_V2` | Giữ nguyên để không mất parity | Phase 2, bump/version + both-side verifier tests nếu sửa |
| Metric semantics | Chưa có bộ Opportunity V1 đầy đủ | **`AED_METRIC_OPPORTUNITY_V1`** (đề xuất) | Phase 2–4 |
| Pursuit episode | Chưa canonical | **`AED_PURSUIT_EPISODE_V1`** (đề xuất) | Phase 3 |
| Minion episode | Chưa canonical | **`AED_MINION_EPISODE_V1`** (đề xuất) | Phase 3 |
| Tool effect episode | Chưa canonical | **`AED_TOOL_EFFECT_V1`** (đề xuất) | Phase 2/5 |
| Resource plan/receipt | Chưa | **`AED_RESOURCE_PLAN_V1`** (đề xuất) | Phase 5 |
| Backend profile | Có lineage/revision/formula version | Version dimensions độc lập; migration có kế hoạch | Phase 4 |
| Unified policy | AED v2 có boundary policy | Không coi raw noise là skill; explicit HOLD categories | Phase 8 |

**Quy tắc migration:** mọi thay đổi V2 fingerprint/ObjectiveProgress phải nâng cấp collector **cùng** Backend `AEDv2PhaseEvidenceVerifier`, `ScenarioAdaptivePlanV2Service` và regression tests. Không giữ tên fingerprint cũ nhưng đổi cách tính. Một policy có thể dùng metric V1 sidecar độc lập trước khi đổi canonical evidence.

## 9. HOLD taxonomy thiết kế (triển khai Phase 2/8)

| Proposed reason | Condition |
|---|---|
| `HOLD_UNSAFE_STALKER_STATE` | Active chase/attack chưa có safe apply |
| `HOLD_PLAYER_DOWNED_OR_REVIVING` | Roster life action nhạy cảm |
| `HOLD_EVIDENCE_INCOMPLETE` | Lost required canonical telemetry |
| `HOLD_FINGERPRINT_MISMATCH` | Unity–Backend mismatch |
| `HOLD_ROSTER_CHANGED` | Identity/roster inconsistent |
| `HOLD_PHASE_MISMATCH` | Phase ordinal/name mismatch |
| `HOLD_INSUFFICIENT_OBSERVATION` | Không đủ active coverage/eligible sample |
| `HOLD_METRIC_NO_OPPORTUNITY` | Metric candidate thiếu cơ hội hợp lệ |
| `HOLD_METRIC_UNSUPPORTED` | Metric chưa có valid production source |
| `HOLD_NO_ELIGIBLE_ADJUSTMENT` | Không có candidate qua policy |
| `HOLD_ADJUSTMENT_BUDGET_EXHAUSTED` | Boundary/match budget giới hạn |
| `HOLD_BACKEND_UNAVAILABLE` | Approval không sẵn sàng; khác intentional policy HOLD |

Các mã trên **là đề xuất thiết kế**, không phải enum/code hiện có. Không thay global `AED_V2_BOUNDARY_HOLD` trong Phase 1.

## 10. Quy trình hoàn thành toàn bộ Phase 1 trên máy developer

### P1.1 — Create contract & validate registry

1. Tạo thư mục `docs/aed/` trong repo nếu chưa có.
2. Sao chép **chính tài liệu này** thành `docs/aed/AED_Metric_Opportunity_Contract_V1.md`.
3. Rà từng metric: numerator, denominator, opportunity, success/failure, NoOpportunity, authority, readiness, AED use đều có.
4. Không viết C# enum trước khi ký chốt tên/status và measurement unit. Không gọi `ActiveObservedSeconds` là metric skill.

### P1.2 — Episode sign-off

1. Đối chiếu Stalker `CHASE/SEARCH/PATROL/DETECT` và target switch/decay đang chạy; không thêm telemetry ngay.
2. Đối chiếu Minion `Track/Harass/Flee`, Slow, forced-drop, stolen, Flashlight, MINION_ALERT.
3. Chốt một màn hình test logic review: TargetSwitch không success, SEARCH timeout không success, Flashlight contributors không nhân kill.
4. Đánh dấu episode nào chưa thể source-proof trong trạng thái Unsupported.

### P1.3 — Resource sign-off

1. Khóa source baseline `TeamToolWorldSpawn.TrySpawnInitial` và danh mục required five.
2. Lập decision candidate per type (Floor/MaxPerZone/MaxPerMatch/Loadout awareness) **không thay spawn production**.
3. Chuẩn bị Zone 1 baseline, Zone 2/3 lazy spawn future proposal; cấm chỉnh item đã được người chơi thấy/sở hữu.
4. Chốt tất cả ToolEffect success gắn actual threat/opportunity.

### P1.4 — Profile/Pressure/Confidence sign-off

1. Bản đồ ánh xạ các PlayerAIProfile fields hiện có vào dimensions mới; không đổi công thức sản xuất trước Phase 4.
2. TeamProfile hiện match-scoped (`MatchId`) nên future cross-match team profile là yêu cầu mới, không giả đã có.
3. Chốt historical skill vs current match pressure là hai aggregates khác nhau.
4. Ghi đề xuất confidence gating nhưng gắn mọi numerical thresholds là calibration-required.

### P1.5 — Freeze contract & handoff

1. Chốt semantic version `AED_METRIC_OPPORTUNITY_V1`; các contract episode V1 ở mục 8.
2. Chốt source identity, Host authority, phases, Censored vs NoOpportunity vs Incomplete.
3. **Không sửa `AEDv2CurrentMatchEvidence`, `TelemetryContracts`, Backend DB/verifier trong Phase 1**.
4. Commit docs riêng: `docs(aed): define phase-1 opportunity and episode contracts`.
5. Phase 2 làm theo thứ tự: CORE_PLACED/parity → Down lethal consequence → Tool Effect → Noise source tagging → detailed HOLD → regression.

### Kiểm tra tài liệu trước commit

- [ ] Tất cả metric trong tài liệu Phase 1 gốc đã được định nghĩa (Survival, Stalker, Objective, Minion, Tool, Resource, Teamwork, Movement, Noise).
- [ ] Core placement không bị cộng nhầm vào V2 canonical hiện tại; công việc nâng cấp được chuyển rõ sang Phase 2.
- [ ] Mỗi outcome đều có Host source; unsupported được đánh dấu rõ; không dùng event research-only làm production.
- [ ] NoOpportunity không là 0; Incomplete khác Unsafe; Censored không tự thành Failure.
- [ ] Tool 5 loại có outcome/eligible window riêng; FirstAid floor, CoreStabilizer limit, Scanner NavMesh distance là **đề xuất fairness**, chưa production.
- [ ] Stalker detection decay/target switch được tính trong episode.
- [ ] Minion forced-drop khác carry-steal; flashlight multi-contributor không double-count.
- [ ] Policy context để chống self-induced learning; Team player disparity safeguard.
- [ ] Backend fingerprint V2 schema giữ nguyên; mọi thay đổi tương lai yêu cầu version bump parity cả hai phía.
- [ ] Giữ `extendedPolicyGameplayEnabled: 0`; Shadow có thể trở lại `0` sau test P0.

**Phase 1 DONE = đủ 5 nhóm contract, source inventory, readiness, semantic version & review; không phải yêu cầu Unity/Backend E2E của Phase 2–8 đã chạy.**

## 11. Những việc chuyển hẳn sang Phase 2–9 (không làm sớm)

| Phase | Implementation backlog được chốt từ hợp đồng |
|---|---|
| Phase 2 | Core placements/ObjectiveProgress + verifier parity; revive-limit lethal evidence; tool/noise semantics; HOLD diagnostics; tests |
| Phase 3 | Host Stalker pursuit and Minion encounter capture + resolved outcome + source contribution, censor handling |
| Phase 4 | Player/Team dimension aggregators, confidence, current pressure, historical revision/fairness |
| Phase 5 | Resource Director: composition/quantity/location/timing, per-zone lazy spawn & receipt, loadout, reachability |
| Phase 6 | Minion Director: cap, budget, respawn cooldown, alert frequency, spawn fairness, incident pressure |
| Phase 7 | Stalker adaptation and pacing director, safe state, no hidden unfair tuning |
| Phase 8 | Unified Host/Backend approval/COMMITTED/APPLIED receipt/rollback/idempotency & Fixed isolation |
| Phase 9 | Fixed vs Adaptive paired experiments & calibration; subjective fairness/tension feedback |

