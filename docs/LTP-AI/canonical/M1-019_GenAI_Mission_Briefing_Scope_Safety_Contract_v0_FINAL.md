# ECHO PROTOCOL — M1-019 GenAI Mission Briefing Scope & Safety Contract v0

**Task:** M1-019 — GenAI Mission Briefing scope  
**Owner:** C — AI / Telemetry / Research  
**Support:** D — Backend / Shop / Payment  
**Dependency:** M1-007  
**Priority:** P0  
**Status:** DONE / FROZEN  
**Contract Baseline:** v0

---

# 1. Purpose

Tài liệu này freeze **GenAI Mission Briefing Scope & Safety Contract v0** cho ECHO PROTOCOL.

Mục tiêu của M1-019 là định nghĩa đầy đủ boundary và contract cho luồng:

```text
Validated ScenarioConfig
+
Designer Content Registry
        ↓
Trusted Mission Facts Resolver
        ↓
MissionBriefingFacts
        ↓
Backend / GenAI Adapter
        ↓
LLM Provider
        ↓
MissionBriefingValidator
        ↓
VALID
├─ YES → MissionBriefingOutput
└─ NO  → retry / fallback path
        ↓
UI / Presentation Only
```

Tài liệu phải cho phép Backend / AI / Gameplay / UI developer triển khai mà không phải tự suy đoán:

- GenAI được phép làm gì;
- GenAI tuyệt đối không được làm gì;
- dữ liệu nào được phép gửi cho model;
- Mission Briefing facts do component nào sở hữu;
- request/output schema logic;
- invented gameplay fact được xử lý thế nào;
- validation behavior;
- timeout/retry behavior;
- cache identity và lookup order;
- template fallback behavior;
- GenAI failure có được block match hay không;
- versioning/traceability;
- gameplay authority boundary.

Critical invariant:

```text
MissionBriefingOutput
→ presentation only
→ MUST NOT become gameplay input
```

M1-019 không thay đổi ScenarioConfig, AED, Monster AI hoặc gameplay rule.

---

# 2. Scope

## 2.1. In Scope

M1-019 v0 freeze:

1. Mission Briefing là GenAI P0 use case.
2. Lore/narrative richness boundary.
3. Canonical GenAI architecture.
4. Trusted Mission Facts ownership.
5. MissionBriefingFacts logical contract.
6. MissionBriefingRequest contract.
7. Safe input rules.
8. MissionBriefingOutput contract.
9. Presentation-only authority boundary.
10. Allowed mission-facing content.
11. Forbidden/invented authoritative facts.
12. Prompt contract.
13. MissionBriefingValidator behavior.
14. Language/length policy.
15. Backend/client/provider boundary.
16. Cache contract.
17. Cache-first lookup/invalidation semantics.
18. Generation timeout/retry policy.
19. GenAI failure / match-start behavior.
20. Deterministic template fallback.
21. Fallback packaging/configuration error.
22. Versioning/traceability.
23. Stochastic-output semantics.
24. Data minimization.
25. Generated-content ownership.
26. Contract test cases.
27. Implementation constraints.
28. Completion criteria.
29. Frozen baseline summary.

## 2.2. Out of Scope

M1-019 v0 MUST NOT mở scope sang:

- NPC dialogue system;
- dynamic story system;
- procedural quest generation;
- procedural map generation;
- adaptive narrative engine;
- lore log generation system;
- gameplay strategy advisor;
- AI-controlled monster dialogue affecting gameplay;
- dynamic economy;
- item generation;
- code generation for gameplay;
- realtime GenAI gameplay decision;
- Monster FSM generation;
- AED policy generation;
- ScenarioConfig generation;
- hidden-player inference;
- realtime pacing estimator;
- Machine Learning gameplay policy;
- online reinforcement learning;
- telemetry analysis by the LLM;
- database schema chi tiết;
- provider-specific SDK implementation;
- retry transport implementation details;
- dashboard/analytics UI.

M1-019 không phải task “xây AI kể chuyện”.

M1-019 là:

```text
safe presentation-text generation boundary
```

---

# 3. Source of Truth / Dependencies

## 3.1. M1-007 — AI Architecture

M1-007 đã freeze:

```text
Monster runtime behavior = Traditional AI
AED = bounded Scenario Configuration layer
GenAI = Mission Briefing / content support only
```

GenAI nằm ngoài Monster AI và AED gameplay decision.

M1-019 MUST preserve boundary này.

## 3.2. M1-015 — ScenarioConfig / AED Fairness

M1-015 định nghĩa ScenarioConfig là validated/bounded configuration của gameplay.

M1-019 chỉ được đọc **validated ScenarioConfig** thông qua Trusted Mission Facts Resolver để tạo presentation facts.

M1-019 MUST NOT:

```text
MissionBriefingOutput
→ ScenarioConfig
```

hoặc:

```text
LLM output
→ AdaptiveDecision
```

## 3.3. Designer Content Registry

Designer Content Registry là source của player-facing display metadata và safe mission presentation content được project authoring pipeline cho phép.

Registry có thể cung cấp:

- map display name;
- threat display name;
- objective presentation facts;
- extraction presentation fact;
- mission theme;
- fallback briefing template.

Exact storage format nằm ngoài M1-019.

## 3.4. Backend/API Baseline

Backend là orchestration boundary cho GenAI Mission Briefing.

Logical endpoint có thể là:

```text
POST /ai/briefing
```

nhưng exact route không phải frozen requirement nếu Backend baseline dùng naming khác.

Semantic contract của request/response trong tài liệu này là FROZEN.

---

# 4. Architecture Boundary

Canonical architecture:

```text
Validated ScenarioConfig
+
Designer Content Registry
        ↓
Trusted Mission Facts Resolver
        ↓
MissionBriefingFacts
        ↓
Backend Mission Briefing Service
        ↓
GenAI Adapter
        ↓
LLM Provider
        ↓
MissionBriefingValidator
        ↓
MissionBriefingOutput
        ↓
UI
```

Fallback branch:

```text
Compatible VALID Cache
        ↓
MissionBriefingOutput(source=CACHE)
```

hoặc:

```text
GenAI generation unavailable / invalid
        ↓
TemplateMissionBriefing
        ↓
MissionBriefingOutput(source=TEMPLATE_FALLBACK)
```

## 4.1. Responsibility Table

| Component | Responsibility |
|---|---|
| Validated ScenarioConfig | Authoritative validated gameplay configuration; không do GenAI tạo |
| Designer Content Registry | Designer-authored presentation facts/templates |
| Trusted Mission Facts Resolver | Chuyển authoritative scenario data thành safe briefing facts |
| MissionBriefingFacts | Server-owned structured facts được phép gửi tới GenAI |
| Backend Mission Briefing Service | Orchestration, cache, generation, validation, fallback |
| GenAI Adapter | Provider abstraction và request execution |
| LLM Provider | Sinh candidate presentation text |
| MissionBriefingValidator | Kiểm tra candidate text/schema/fact compatibility |
| MissionBriefingOutput | Valid presentation artifact cho UI |
| UI | Render briefing; không parse thành gameplay authority |

## 4.2. Critical Authority Separation

Không tồn tại luồng:

```text
MissionBriefingOutput → ScenarioConfig
MissionBriefingOutput → AED
MissionBriefingOutput → Monster FSM
MissionBriefingOutput → Target Selection
MissionBriefingOutput → Spawn System
```

Validator là defense-in-depth.

Primary safety guarantee là architectural separation:

```text
free-form briefing text
→ never authoritative gameplay input
```

---

# 5. P0 vs P2 Feature Boundary

## 5.1. ACTIVE / P0

```text
GEN-01 Mission Briefing
→ ACTIVE
→ P0
→ System / KLTN requirement
```

P0 yêu cầu safe Mission Briefing generation/fallback pipeline.

## 5.2. DEFERRED / P2 FUTURE

```text
GEN-02 Lore variation / lore richness / richer narrative
→ DEFERRED
→ P2 / FUTURE
```

M1-019 DONE/FROZEN không phụ thuộc việc implement lore generator.

## 5.3. No Scope Expansion

Không activate trong v0:

- NPC dialogue;
- dynamic lore system;
- dynamic quest text that changes gameplay requirements;
- procedural narrative state;
- narrative-driven gameplay adaptation.

Future lore feature MUST retain:

```text
presentation only
no gameplay authority
```

---

# 6. Trusted Mission Facts

## 6.1. Purpose

LLM MUST NOT receive arbitrary gameplay object graphs or untrusted free-form scenario text.

M1-019 freeze một explicit trusted-facts layer:

```text
Authoritative Scenario Data
        ↓
Trusted Mission Facts Resolver
        ↓
MissionBriefingFacts
```

## 6.2. Logical Contract

```text
MissionBriefingFacts
{
    mapDisplayName,
    threatDisplayName,
    objectiveFacts[],
    extractionFact,
    missionTheme
}
```

Logical semantics:

```text
mapDisplayName
→ player-facing map/location name
```

```text
threatDisplayName
→ player-facing authoritative threat name
```

```text
objectiveFacts[]
→ designer/server-owned facts describing current authoritative objectives
```

```text
extractionFact
→ trusted presentation fact describing current extraction condition
```

```text
missionTheme
→ trusted stylistic/theme hint only
→ MUST NOT encode hidden gameplay rule
```

Exact class/JSON shape có thể khác nếu Backend naming convention yêu cầu, nhưng semantic fields trên MUST remain represented.

## 6.3. Ownership — FROZEN

MissionBriefingFacts MUST originate from one or more:

- validated ScenarioConfig;
- designer-authored content registry;
- server-owned mission metadata;
- deterministic mapping từ authoritative scenario data.

MissionBriefingFacts MUST NOT originate from:

- arbitrary client text;
- raw user prompt;
- raw LLM output;
- hidden runtime state;
- free-form unvalidated payload;
- raw telemetry stream.

## 6.4. Trusted Resolver Rule

Trusted Mission Facts Resolver MUST be deterministic for the same:

```text
validated ScenarioConfig
+
content registry version/content
```

unless source registry itself has an explicit versioned localization selection.

Resolver does not use an LLM to decide authoritative mission facts.

## 6.5. Client Text Rule

Không:

```text
client arbitrary objectiveSummary
→ MissionBriefingFacts
```

Không:

```text
client arbitrary missionTheme
→ MissionBriefingFacts
```

Nếu `missionTheme` tồn tại:

```text
→ server-owned / designer-authored / registry-resolved only
```

---

# 7. MissionBriefingRequest Contract

Logical request:

```text
MissionBriefingRequest
{
    requestId,

    scenarioConfigVersion,

    language,

    briefingContractVersion,
    promptTemplateVersion,
    validationPolicyVersion,

    missionFactsVersionOrHash,

    facts: MissionBriefingFacts
}
```

## 7.1. `requestId`

`requestId` identifies one briefing request for tracing/idempotency/log correlation.

M1-019 does not freeze ID generation implementation.

## 7.2. `scenarioConfigVersion`

References the validated ScenarioConfig version from which trusted facts were resolved.

It does not grant LLM access to the full runtime config.

## 7.3. `language`

Requested output language.

Must be allowed by active validation policy.

## 7.4. Contract Versions

Request MUST identify:

```text
briefingContractVersion
promptTemplateVersion
validationPolicyVersion
```

so validation/cache can determine compatibility.

## 7.5. `missionFactsVersionOrHash`

MUST identify the exact canonical fact set used for this request.

Implementation may use:

```text
missionFactsVersion
```

or:

```text
missionFactsHash
```

or an equivalent stable identity.

It MUST be sufficient to prevent cache reuse across incompatible facts.

---

# 8. Safe Input Rules

## 8.1. Structured Facts Only

GenAI input MUST be built from:

```text
System Instruction
+
MissionBriefingFacts
+
Output Constraints
```

No raw arbitrary client prompt is part of authoritative pipeline.

## 8.2. Hidden/Internal State Exclusion

MUST NOT include unnecessary hidden/internal runtime state such as:

```text
CurrentTarget
DetectionTarget
LastKnownPosition
Player hidden position
Player transform not intended for briefing
Monster FSM state
Detection Meter internal value
Attack internal state
Navigation internal state
Secret route state
Raw telemetry stream
```

## 8.3. Raw Telemetry Exclusion

Do not send raw `TelemetryEvent` or telemetry history to model so it can infer mission/gameplay facts.

Telemetry is not the Mission Briefing authority.

## 8.4. Data Minimization

Only send the minimum trusted facts needed to produce the briefing.

## 8.5. No Scenario Object Dump

Do not serialize the entire free-form/internal ScenarioConfig object into the LLM prompt merely for convenience.

Only fields intentionally resolved into MissionBriefingFacts are allowed.

---

# 9. MissionBriefingOutput Contract

Logical output:

```text
MissionBriefingOutput
{
    text,
    language,
    source,
    status,

    modelRef,
    generatedAt,

    briefingContractVersion,
    promptTemplateVersion,
    validationPolicyVersion,
    missionFactsVersionOrHash
}
```

## 9.1. `source`

Frozen enum:

```text
GENAI
CACHE
TEMPLATE_FALLBACK
```

Semantic:

```text
GENAI
→ candidate generated by model and passed MissionBriefingValidator
```

```text
CACHE
→ previously validated compatible MissionBriefingOutput reused
```

```text
TEMPLATE_FALLBACK
→ deterministic non-LLM fallback
```

## 9.2. `status`

Frozen enum:

```text
VALID
FALLBACK
```

Mapping:

| source | status |
|---|---|
| `GENAI` | `VALID` |
| `CACHE` | `VALID` |
| `TEMPLATE_FALLBACK` | `FALLBACK` |

`CACHE` is not fallback merely because provider was not called.

It is reuse of previously validated content.

## 9.3. `modelRef`

```text
source = GENAI
→ modelRef REQUIRED
```

```text
source = CACHE
→ preserve original modelRef if known
```

```text
source = TEMPLATE_FALLBACK
→ modelRef = null / NOT_APPLICABLE
```

## 9.4. `generatedAt`

Timestamp identifies generation/original artifact time according to backend convention.

For cache reuse, implementation may preserve original generation timestamp and separately log cache-hit time outside frozen output schema.

## 9.5. Text Authority

```text
MissionBriefingOutput.text
→ presentation only
→ non-executable
→ non-authoritative gameplay text
```

---

# 10. Presentation-Only / Gameplay Authority Boundary

This section is HARD FROZEN.

No gameplay subsystem MUST parse `MissionBriefingOutput.text` as authoritative gameplay configuration.

MissionBriefingOutput.text MUST NOT be consumed to modify:

- ScenarioConfig;
- AdaptiveDecision;
- AED;
- Traditional Monster AI;
- FSM;
- Sensor;
- Target Selection;
- CurrentTarget;
- DetectionTarget;
- LastKnownPosition;
- Attack;
- Navigation;
- Spawn System;
- Route System;
- Item Stat System;
- Player Stat System;
- Reward System;
- Economy;
- Shop;
- Payment.

Even if validator accidentally allows misleading/bad wording:

```text
briefing text
→ MUST NOT gain gameplay authority
```

Validator is defense-in-depth, not the primary authority barrier.

---

# 11. Allowed Briefing Content

Mission Briefing MAY:

- describe the mission;
- restate supplied objectives;
- identify supplied map/location context;
- describe supplied threat;
- describe supplied extraction condition;
- add bounded stylistic flavor;
- improve phrasing;
- improve tone;
- create concise narrative framing that does not add authoritative gameplay facts.

Allowed examples when supported by Trusted Mission Facts:

```text
Recover the Energy Cores.
```

```text
Reach the extraction point after completing the objectives.
```

```text
A Stalker is active inside the facility.
```

These are player-facing mission instructions/presentation text.

They are not ScenarioConfig.

## 11.1. Imperative Sentences Are Not Automatically Forbidden

Mission-facing imperative wording is allowed when it only restates trusted facts.

Example:

```text
"Recover the cores."
```

is allowed if the authoritative objective facts contain core recovery.

Validator MUST NOT reject every imperative sentence merely because it sounds like an instruction.

---

# 12. Forbidden / Invented Gameplay Facts

## 12.1. No Invented Authoritative Facts — FROZEN

LLM MAY describe provided authoritative facts.

LLM MUST NOT invent new authoritative gameplay facts.

Forbidden examples if absent from MissionBriefingFacts:

```text
"Stalker damage is now 40%."
```

```text
"Your sprint speed is increased."
```

```text
"The Stalker has increased AttackRange."
```

```text
"Three extra Energy Cores will spawn."
```

```text
"You now have an EMP ability."
```

```text
"The exit timer is reduced to 30 seconds."
```

```text
"The monster will target Player 2."
```

```text
"A new route will open after 60 seconds."
```

```text
"Reward increased to 500 credits."
```

If candidate content asserts a gameplay mechanic/stat/state not supported by trusted facts:

```text
→ INVALID
→ reject candidate
→ GenerationPolicy retry/fallback path
```

## 12.2. Forbidden Gameplay Authority Types

GenAI MUST NOT create output that gameplay interprets as:

```text
ScenarioConfig
AdaptiveDecision
FSM state
Target Selection
CurrentTarget
DetectionTarget
LastKnownPosition
map layout
spawn coordinate
route graph
gameplay stat
monster stat
player stat
item power
ability definition
damage value
reward value
economy value
payment value
shop pricing
executable gameplay code
```

Explicit forbidden patterns/examples:

```text
StalkerDamagePercent = ...
ChaseSpeed = ...
AttackRange = ...
SpawnPlayer(...)
SpawnMonster(...)
ChangeMap(...)
SetCurrentTarget(...)
EnterCHASE(...)
SetReward(...)
SetItemPower(...)
```

## 12.3. Trusted-Fact Compatibility

Allowed:

```text
"Recover five cores."
```

only if the trusted objective facts explicitly establish `five`.

Otherwise:

```text
→ invented authoritative detail
→ INVALID
```

Allowed:

```text
"Extraction closes after 60 seconds."
```

only if that exact extraction/timer fact is intentionally present in trusted briefing facts.

The LLM does not infer undisclosed internal ScenarioConfig values.

---

# 13. Prompt Contract

M1-019 does not freeze one exact long prompt string.

It freezes the prompt topology and safety requirements.

Canonical pattern:

```text
System Instruction
+
Trusted Mission Facts
+
Output Constraints
→ Candidate Mission Briefing text
```

## 13.1. Required System Instructions

Prompt MUST instruct model to:

- be brief;
- be mission-focused;
- use requested language;
- describe supplied facts;
- not invent gameplay facts;
- not invent mechanics;
- not invent numeric gameplay stats;
- not generate ScenarioConfig;
- not generate AdaptiveDecision;
- not generate map/layout;
- not generate spawn coordinates;
- not generate gameplay code;
- not change game rules;
- not claim authoritative facts absent from input.

Critical prompt rule:

```text
DESCRIBE PROVIDED FACTS
DO NOT CREATE NEW AUTHORITATIVE FACTS
```

## 13.2. Prompt Template Versioning

Prompt content is versioned by:

```text
promptTemplateVersion
```

Prompt semantic change MUST change prompt template version.

## 13.3. Prompt Is Not Sufficient Safety Alone

Prompt instruction does not replace:

- trusted-facts resolver;
- MissionBriefingValidator;
- presentation-only authority separation;
- fallback path.

---

# 14. MissionBriefingValidator

## 14.1. Pipeline

```text
LLM Candidate Output
        ↓
MissionBriefingValidator
        ↓
VALID
├─ YES → MissionBriefingOutput(source=GENAI,status=VALID)
└─ NO  → candidate rejected → retry/fallback according to GenerationPolicy
```

## 14.2. Required Checks

Validator MUST check at minimum:

1. output exists;
2. text is non-empty;
3. output/request association is valid;
4. output schema can be mapped to frozen MissionBriefingOutput contract;
5. text length is within active configured maximum;
6. output language matches requested/allowed language policy;
7. no forbidden structured gameplay fields;
8. no invented authoritative gameplay fact;
9. no map/layout generation;
10. no arbitrary spawn coordinate;
11. no stat modification/invention;
12. no item power/ability invention;
13. no reward/economy/payment/shop value invention;
14. no executable gameplay code used as game instruction;
15. no FSM/Target/Sensor command;
16. no hidden runtime-state claim;
17. mission-facing instructions only restate compatible trusted mission facts;
18. candidate is suitable for UI presentation.

## 14.3. Allowed Instruction vs Forbidden Authority

Validator MUST distinguish:

```text
A. allowed mission-facing instruction
that restates Trusted Mission Facts
```

from:

```text
B. invented authoritative gameplay claim
not present in Trusted Mission Facts
```

Example:

```text
Trusted fact: Recover Energy Cores
Candidate: "Recover the Energy Cores."
→ VALID with respect to fact grounding
```

Example:

```text
Trusted fact: Recover Energy Cores
Candidate: "Recover five Energy Cores."
Trusted facts do not specify five
→ INVALID
```

## 14.4. Validation Implementation Freedom Without Semantic Freedom

M1-019 freezes validator behavior, not one mandatory NLP algorithm.

Implementation MAY use:

- constrained structured provider output;
- deterministic allow/deny checks;
- fact IDs/internal validation metadata;
- deterministic text inspection;
- other backend validation mechanisms.

However implementation MUST produce the same semantic outcome required by this contract.

Internal validation metadata MUST NOT become gameplay authority.

## 14.5. No Text-to-Config Transformation

MissionBriefingValidator MUST NOT attempt to transform free-form model text into ScenarioConfig or gameplay commands.

---

# 15. Language / Length Policy

Logical validation policy contains at least:

```text
maxBriefingLength
allowedLanguages
```

These are:

```text
CONFIGURABLE
server-owned
versioned by validationPolicyVersion
```

If source does not freeze numerical max length:

```text
M1-019 MUST NOT invent a number
```

Required config rule:

```text
maxBriefingLength
→ MUST BE PROVIDED BY VERSIONED VALIDATION CONFIG
```

## 15.1. Unsupported/Wrong Language

If generated output language is unsupported or does not satisfy requested language policy:

```text
→ INVALID
→ retry/fallback according to GenerationPolicy
```

## 15.2. Excessive Length

If generated text exceeds configured max:

```text
→ INVALID
→ no silent acceptance
→ retry/fallback
```

M1-019 does not freeze truncation as a valid repair strategy.

If implementation wants deterministic truncation in a future version, it requires an explicit validation-policy contract change.

---

# 16. Backend / Client Boundary

## 16.1. Canonical Boundary

```text
Game / Client
        ↓
Backend Mission Briefing Service
        ↓
GenAI Adapter
        ↓
LLM Provider
        ↓
Validator
        ↓
MissionBriefingOutput / Fallback
        ↓
Client UI
```

Backend is authoritative orchestration boundary.

## 16.2. Direct Provider Call — Forbidden for Authoritative Pipeline

Client MUST NOT call external LLM provider directly for authoritative Mission Briefing pipeline.

## 16.3. Client Must Not Interpret AI Text as Config

Client MUST NOT:

- send raw provider response into gameplay systems;
- parse briefing text into ScenarioConfig;
- modify gameplay based on briefing content;
- treat model text as Spawn/Route/Stat instructions.

## 16.4. Logical API

Logical API contract may be represented as:

```text
POST /ai/briefing

Request  = MissionBriefingRequest
Response = MissionBriefingOutput
```

Exact URI/name can follow Backend canonical naming without changing contract semantics.

---

# 17. Cache Contract

Cache may store/reuse only:

```text
previously VALID compatible MissionBriefingOutput
```

Template fallback may be separately stored as designer content; it is not treated as generated cache content unless backend implementation intentionally stores the final output artifact.

## 17.1. Canonical Cache Identity

```text
MissionBriefingCacheKey
{
    missionFactsVersionOrHash,
    scenarioConfigVersion,
    language,
    briefingContractVersion,
    promptTemplateVersion,
    validationPolicyVersion
}
```

If provider/model identity is required by a project-specific cache policy, it MAY be included in a later compatible cache-key version.

It is not required by v0 unless operational policy demands it.

## 17.2. Cache Validity

Cache hit is valid only when all key dimensions required by current v0 identity match.

Do not:

```text
Research Facility + Stalker facts
→ reuse for incompatible map/threat/facts
```

Do not:

```text
old promptTemplateVersion
→ reuse as current prompt output
```

Do not:

```text
old validationPolicyVersion output
→ automatically treat as current valid output
```

## 17.3. Cache Content Rule

Cache MUST NOT store candidate text that failed validation as reusable valid briefing.

Only validated output can satisfy `source=CACHE` later.

---

# 18. Cache Lookup / Invalidation

## 18.1. Canonical Orchestration Order — FROZEN

M1-019 v0 uses:

```text
CACHE-FIRST
```

Flow:

```text
MissionBriefingRequest
        ↓
compatible VALID cache exists?
├─ YES
│   → return MissionBriefingOutput
│   → source = CACHE
│   → status = VALID
│   → provider call not required
│
└─ NO
    ↓
GenAI generation
    ↓
validate
    ├─ VALID
    │   → store/update compatible validated cache
    │   → return source = GENAI
    │   → status = VALID
    │
    └─ INVALID / timeout / provider error
        ↓
retry if GenerationPolicy allows
        ↓
still failure
        ↓
TemplateMissionBriefing
        ↓
source = TEMPLATE_FALLBACK
status = FALLBACK
```

## 18.2. Invalidation / Miss Conditions

Cache MUST be treated as miss when incompatible in any frozen key dimension, including:

- `missionFactsVersionOrHash`;
- `scenarioConfigVersion`;
- `language`;
- `briefingContractVersion`;
- `promptTemplateVersion`;
- `validationPolicyVersion`.

## 18.3. No Unnecessary Provider Call

Compatible validated cache hit:

```text
→ provider call not required
```

This is deterministic v0 behavior.

---

# 19. Generation Timeout / Retry Policy

Logical operational contract:

```text
GenerationPolicy
{
    generationPolicyVersion,
    timeoutMs,
    maxRetryCount
}
```

## 19.1. Configuration Ownership

```text
timeoutMs
maxRetryCount
→ CONFIGURABLE
→ server-owned
→ finite
→ versioned/traceable through generationPolicyVersion or equivalent config reference
```

M1-019 MUST NOT invent numerical values if source does not freeze them.

## 19.2. Timeout Constraint

```text
timeoutMs > 0
```

and finite.

No indefinite provider wait.

## 19.3. Retry Constraint

```text
maxRetryCount >= 0
```

and finite integer.

Semantic:

```text
maxRetryCount
= maximum number of retry attempts AFTER the initial generation attempt
```

Total provider attempts are bounded by:

```text
1 + maxRetryCount
```

when no cache hit exists.

## 19.4. Retry Exhaustion

After retries are exhausted:

```text
→ stop provider generation loop
→ TemplateMissionBriefing fallback
```

No infinite retry loop.

## 19.5. Validation Failure and Retry

Invalid candidate MAY consume a retry if active GenerationPolicy allows remaining retries.

If no retry remains:

```text
→ fallback
```

---

# 20. GenAI Failure / Match Start Contract

This section is HARD FROZEN.

```text
GenAI failure
≠ match failure
```

The following MUST NOT prevent normal match start for a correctly packaged supported P0 scenario:

- provider unavailable;
- provider timeout;
- network/API failure;
- invalid model output;
- empty output;
- wrong language output;
- excessive output length;
- validation failure;
- retry exhaustion;
- cache miss.

Canonical successful presentation path MUST resolve to one of:

```text
VALID GENAI briefing
OR
VALID compatible CACHE briefing
OR
valid deterministic TEMPLATE_FALLBACK briefing
```

Do not:

```text
wait indefinitely for LLM
→ block Player from starting match
```

Mission Briefing generation is not a gameplay authority dependency.

---

# 21. Template Fallback

## 21.1. Fallback Definition — FROZEN

```text
TemplateMissionBriefing
→ designer-authored
→ deterministic
→ LLM-independent
→ locally/backend resolvable
→ presentation-only
```

A valid fallback MUST exist for every supported P0 scenario/content package.

## 21.2. Compatibility

Fallback template MUST be compatible with:

- current MissionBriefingFacts; or
- current designer-authored scenario template context.

It MUST NOT invent incompatible gameplay facts.

## 21.3. Safe Field Insertion

Template MAY interpolate trusted display fields such as:

- map display name;
- threat display name;
- objective display facts;
- extraction display fact;

only from current trusted facts/registry.

## 21.4. Conceptual Example

Non-canonical example:

```text
Mission: Complete the assigned objectives.
Threat: A hostile entity is active in the facility.
Extraction: Complete the mission and reach the valid extraction route.
```

Exact wording is not frozen unless another source supplies canonical text.

## 21.5. Fallback Validation

Fallback template/content MUST be prevalidated as part of content/config packaging against its intended language and contract.

Runtime may run defensive schema/presentation checks, but fallback MUST NOT depend on an LLM to become valid.

---

# 22. Fallback Configuration Error

For supported P0 scenario:

```text
missing/invalid TemplateMissionBriefing
→ configuration/content packaging error
```

GenAI MUST NOT invent a special emergency gameplay scenario or missing authoritative data.

A correctly packaged supported P0 scenario MUST provide valid fallback before release/runtime use.

M1-019 does not define crash UX or deployment validation tooling.

If runtime discovers invalid/missing fallback despite packaging requirements:

- do not grant model gameplay authority;
- do not invent authoritative mission facts;
- surface configuration/content error according to host/backend operational policy.

---

# 23. Lore / Future Boundary

Frozen feature status:

```text
GEN-01 Mission Briefing
→ ACTIVE / P0
```

```text
GEN-02 Lore variation / richer narrative
→ DEFERRED / P2 FUTURE
```

Future examples MAY include:

- lore-log phrasing;
- richer mission flavor;
- optional narrative variation.

None are required for M1-019 completion.

Future lore features MUST preserve:

```text
presentation only
no gameplay authority
```

---

# 24. Versioning / Traceability

M1-019 freezes logical metadata sufficient to trace generation:

```text
briefingContractVersion
promptTemplateVersion
validationPolicyVersion
scenarioConfigVersion
missionFactsVersionOrHash
modelRef
providerRef if applicable
generationPolicyVersion/config reference if applicable
```

## 24.1. `briefingContractVersion`

Changes when MissionBriefingRequest/Output or authority/safety semantic changes incompatibly.

## 24.2. `promptTemplateVersion`

Changes when prompt instruction/template semantic changes.

## 24.3. `validationPolicyVersion`

Changes when language/length/validation rule configuration changes according to versioning policy.

## 24.4. `scenarioConfigVersion`

Identifies authoritative validated scenario configuration that produced trusted facts.

## 24.5. `missionFactsVersionOrHash`

Identifies exact canonical trusted fact set.

## 24.6. `modelRef`

Identifies model used when `source=GENAI`.

Provider/model choice is CONFIGURABLE unless another frozen project source selects a provider/model.

## 24.7. Provider Metadata

`providerRef` may be stored as trace metadata when applicable.

Exact provider is not frozen by M1-019 v0.

## 24.8. Generation Policy Metadata

GenerationPolicy version/config reference SHOULD be traceable for operational debugging of timeout/retry behavior.

---

# 25. Stochastic Output / Reproducibility

M1-019 v0 does NOT require identical text from every LLM call.

GenAI may be stochastic.

Frozen requirement is:

```text
traceability
≠ exact textual determinism
```

A generated briefing must be traceable through:

- trusted facts identity;
- ScenarioConfig version;
- prompt template version;
- validation policy version;
- model/provider metadata when applicable;
- generation policy/config when applicable.

## 25.1. Template Determinism

For same:

```text
fallback template version/content
+
trusted facts
+
language
```

Template fallback output MUST be deterministic.

## 25.2. Cache Determinism

For same compatible cache key:

```text
CACHE-FIRST
→ same stored valid cached artifact is returned according to cache implementation/version semantics
```

No provider call is required for a valid hit.

## 25.3. No Hidden Gameplay Randomness

GenAI stochasticity MUST NOT alter gameplay configuration/state because MissionBriefingOutput has no gameplay authority.

---

# 26. Data Minimization

Only send facts needed for Mission Briefing.

MUST NOT send unnecessary:

- secrets;
- payment data;
- payment tokens;
- shop/payment internal records;
- player account credentials;
- authentication tokens;
- unnecessary personal data;
- raw telemetry history;
- hidden Player positions;
- internal debug dumps;
- entire backend object graphs;
- internal Monster AI blackboard/state.

M1-019 does not define a full privacy/security program.

Data minimization is still a required contract.

---

# 27. Generated Content Ownership

Mission Briefing output is:

```text
GeneratedContent
→ presentation artifact
```

It is NOT:

```text
GameplayConfiguration
```

It is NOT:

```text
ScenarioConfig
```

It is NOT:

```text
AdaptiveDecision
```

It is NOT:

```text
AED input/output authority
```

It is NOT:

```text
FSM input
```

It is NOT:

```text
game rule
```

Backend MAY persist/cache briefing for presentation/audit.

Persistence does not grant gameplay authority.

UI owns presentation/rendering only.

---

# 28. GenAI Mission Briefing Contract Test Cases

The following cases are contract definitions for implementation verification.

> `[x]` means **contract case defined**, not that current implementation already passed integration testing.

- [x] Contract case defined — valid Trusted Mission Facts → GenAI generation may be attempted after cache miss.
- [x] Contract case defined — valid short output + requested/allowed language + no invented facts → VALID.
- [x] Contract case defined — GenAI output correctly restates provided objective → allowed.
- [x] Contract case defined — `Recover the Energy Cores` is allowed when supplied objective facts support it.
- [x] Contract case defined — imperative mission-facing sentence is not automatically rejected if grounded in trusted facts.
- [x] Contract case defined — LLM invents objective count not present in facts → INVALID.
- [x] Contract case defined — LLM invents `StalkerDamagePercent` → INVALID.
- [x] Contract case defined — LLM invents `ChaseSpeed` → INVALID.
- [x] Contract case defined — LLM invents `AttackRange` → INVALID.
- [x] Contract case defined — LLM invents new ability/item power → INVALID.
- [x] Contract case defined — LLM invents reward/economy/payment value → INVALID.
- [x] Contract case defined — LLM outputs arbitrary spawn coordinates → INVALID.
- [x] Contract case defined — LLM outputs arbitrary map/layout → INVALID.
- [x] Contract case defined — LLM outputs FSM command → INVALID.
- [x] Contract case defined — LLM outputs CurrentTarget/DetectionTarget command → INVALID.
- [x] Contract case defined — LLM asserts hidden Player position → INVALID.
- [x] Contract case defined — MissionBriefingOutput text is never parsed into ScenarioConfig.
- [x] Contract case defined — MissionBriefingOutput text is never consumed by AED/Monster AI.
- [x] Contract case defined — MissionBriefingOutput text is never used to update FSM/Sensor/Target Selection/Navigation/Attack.
- [x] Contract case defined — hidden Player position is not included in MissionBriefingFacts.
- [x] Contract case defined — raw telemetry stream is not included in MissionBriefingFacts/model prompt.
- [x] Contract case defined — client arbitrary `objectiveSummary` cannot become trusted facts.
- [x] Contract case defined — client arbitrary `missionTheme` cannot become trusted facts.
- [x] Contract case defined — trusted `missionTheme` from registry/server may be included as stylistic input.
- [x] Contract case defined — compatible validated cache hit → `source=CACHE`, `status=VALID`, provider call not required.
- [x] Contract case defined — cache with different missionFacts hash/version → miss.
- [x] Contract case defined — cache with incompatible ScenarioConfig version → miss.
- [x] Contract case defined — cache with incompatible language → miss.
- [x] Contract case defined — cache with incompatible briefingContractVersion → miss.
- [x] Contract case defined — cache with incompatible promptTemplateVersion → miss.
- [x] Contract case defined — cache with incompatible validationPolicyVersion → miss.
- [x] Contract case defined — invalid generated candidate is not stored as reusable valid cache.
- [x] Contract case defined — provider timeout → finite retry according to GenerationPolicy.
- [x] Contract case defined — `maxRetryCount` counts retry attempts after initial generation attempt.
- [x] Contract case defined — retry exhausted → template fallback.
- [x] Contract case defined — provider unavailable → template fallback.
- [x] Contract case defined — empty output → INVALID → retry/fallback.
- [x] Contract case defined — output exceeds configured max length → INVALID.
- [x] Contract case defined — wrong/unsupported language → INVALID.
- [x] Contract case defined — validation failure cannot affect gameplay state.
- [x] Contract case defined — template fallback does not require LLM.
- [x] Contract case defined — template fallback uses compatible trusted facts/scenario template.
- [x] Contract case defined — template fallback source maps to `TEMPLATE_FALLBACK/FALLBACK`.
- [x] Contract case defined — invalid/missing fallback package is configuration/content error; GenAI does not invent authoritative replacement facts.
- [x] Contract case defined — AI service down → correctly packaged supported match can still start through fallback.
- [x] Contract case defined — cache miss + provider failure → template fallback → match start not blocked.
- [x] Contract case defined — GenAI cannot generate authoritative ScenarioConfig.
- [x] Contract case defined — GenAI cannot generate authoritative AdaptiveDecision.
- [x] Contract case defined — GenAI cannot change Monster FSM.
- [x] Contract case defined — GenAI cannot change gameplay stats.
- [x] Contract case defined — GenAI cannot change spawn/route authority.
- [x] Contract case defined — client does not call provider directly for authoritative briefing pipeline.
- [x] Contract case defined — raw provider response is not rendered as valid GenAI briefing before validation.
- [x] Contract case defined — source `GENAI` requires modelRef.
- [x] Contract case defined — source `CACHE` preserves original modelRef if known.
- [x] Contract case defined — source `TEMPLATE_FALLBACK` uses null/NOT_APPLICABLE modelRef.
- [x] Contract case defined — lore richness is not required for M1-019 completion.
- [x] Contract case defined — generated GenAI text may vary between calls; exact deterministic text is not required.
- [x] Contract case defined — generation metadata remains traceable.
- [x] Contract case defined — template fallback is deterministic for same template/facts/language.
- [x] Contract case defined — CACHE-FIRST returns compatible cache without unnecessary provider generation.
- [x] Contract case defined — no secret/payment/account credential is sent to briefing model.

---

# 29. Implementation Constraints

1. Mission Briefing is the only ACTIVE GenAI P0 use case in M1-019.
2. Lore variation is DEFERRED / P2 FUTURE.
3. GenAI receives Trusted Mission Facts, not arbitrary free-form scenario state.
4. Mission facts are server/designer-owned or deterministically derived from validated authoritative data.
5. Trusted Mission Facts Resolver does not use GenAI to decide authoritative facts.
6. Client text cannot directly become authoritative MissionBriefingFacts.
7. Client arbitrary objective summary is not trusted input.
8. Client arbitrary mission theme is not trusted input.
9. Raw telemetry does not feed Mission Briefing model directly.
10. Hidden Player/Monster runtime state is excluded.
11. Full arbitrary ScenarioConfig object dump is not model input by default.
12. Only minimum safe structured facts are sent.
13. MissionBriefingRequest must carry facts identity/version/hash.
14. MissionBriefingRequest must carry contract/prompt/validation versions.
15. MissionBriefingOutput is presentation-only.
16. No gameplay subsystem parses briefing text into configuration.
17. GenAI cannot output authoritative ScenarioConfig.
18. GenAI cannot output authoritative AdaptiveDecision.
19. GenAI cannot command Monster FSM.
20. GenAI cannot set CurrentTarget/DetectionTarget.
21. GenAI cannot update LastKnownPosition.
22. GenAI cannot alter Monster/Player gameplay stats.
23. GenAI cannot generate authoritative map/layout.
24. GenAI cannot generate authoritative spawn coordinates.
25. GenAI cannot generate authoritative route graph.
26. GenAI cannot generate item power/ability definitions for gameplay.
27. GenAI cannot generate reward/economy/payment/shop values as authoritative facts.
28. GenAI cannot generate gameplay code used as authority.
29. Mission-facing instructions are allowed only when restating trusted facts.
30. New authoritative gameplay facts are forbidden.
31. Validator must distinguish grounded mission instruction from invented gameplay claim.
32. All model outputs pass MissionBriefingValidator before `source=GENAI` output is rendered.
33. Invalid output cannot be rendered as valid GenAI briefing.
34. Invalid output never affects gameplay.
35. Validator does not transform model text into ScenarioConfig.
36. maxBriefingLength is configurable/versioned; M1-019 does not invent a number.
37. allowedLanguages is configurable/versioned.
38. Wrong/unsupported language output is invalid.
39. Over-length output is invalid.
40. Client MUST NOT call external provider directly for authoritative Mission Briefing pipeline.
41. Backend is orchestration boundary.
42. Client MUST NOT interpret provider/briefing text as gameplay data.
43. Cache stores/reuses only validated compatible briefing artifacts.
44. Cache key includes facts/config/language/contract/prompt/validation identity.
45. Cache lookup is CACHE-FIRST in v0.
46. Compatible cache hit does not require provider call.
47. Incompatible facts/config/language/version produces cache miss.
48. Invalid candidate cannot become valid cache content.
49. timeoutMs must be finite and positive.
50. maxRetryCount must be finite and non-negative.
51. maxRetryCount counts retry attempts after initial attempt.
52. No infinite provider retry.
53. Retry exhaustion goes to template fallback.
54. Provider failure does not block match start for correctly packaged supported P0 scenario.
55. Cache miss does not block match start.
56. Validation failure does not block match start.
57. Template fallback is designer-authored.
58. Template fallback is deterministic.
59. Template fallback is LLM-independent.
60. Template fallback must be compatible with current trusted facts/scenario template.
61. Every supported P0 scenario/content package must include valid fallback.
62. Missing/invalid fallback is configuration/content packaging error.
63. GenAI does not invent emergency authoritative scenario data when fallback packaging is broken.
64. Exact LLM provider is configurable unless another frozen source selects one.
65. Exact LLM text reproducibility is not required.
66. Generation trace metadata is required.
67. modelRef is required for source `GENAI`.
68. source `CACHE` preserves original modelRef if known.
69. source `TEMPLATE_FALLBACK` has null/NOT_APPLICABLE modelRef.
70. Template behavior must be deterministic for same compatible inputs/version.
71. Cache behavior must be deterministic under same compatible key.
72. LLM stochasticity cannot affect gameplay configuration/state.
73. Data minimization applies.
74. No secrets/payment/account credentials sent to briefing model.
75. Mission Briefing persistence/cache does not grant gameplay authority.
76. UI renders briefing only; UI does not parse it into gameplay commands.
77. M1-019 does not create a new gameplay mechanic.
78. M1-019 does not replace AED.
79. M1-019 does not replace Traditional AI.
80. M1-019 does not activate realtime pacing or adaptive narrative policy.

---

# 30. Completion Criteria

Task **M1-019 — GenAI Mission Briefing scope** may retain `DONE / FROZEN` only when all following contracts are explicit and internally consistent:

- [x] Mission Briefing P0 scope explicit.
- [x] Mission Briefing identified as System/KLTN P0 use case.
- [x] Lore variation/richness DEFERRED / P2 FUTURE explicit.
- [x] Canonical GenAI architecture explicit.
- [x] GenAI is Mission Briefing/content support only.
- [x] GenAI is outside Traditional Monster AI authority.
- [x] GenAI is outside AED gameplay policy authority.
- [x] Trusted Mission Facts layer explicit.
- [x] Trusted Mission Facts ownership explicit.
- [x] Trusted facts are server/designer-owned or deterministically derived.
- [x] Arbitrary client text cannot become trusted mission facts.
- [x] Hidden runtime state excluded from model input.
- [x] Raw telemetry excluded from direct model input.
- [x] MissionBriefingFacts logical contract explicit.
- [x] MissionBriefingRequest explicit.
- [x] missionFactsVersionOrHash explicit.
- [x] MissionBriefingOutput explicit.
- [x] output source semantics explicit.
- [x] output status semantics explicit.
- [x] modelRef semantics explicit.
- [x] MissionBriefingOutput presentation-only authority explicit.
- [x] no gameplay subsystem consumes output as config.
- [x] allowed mission-facing instructions distinguished from invented facts.
- [x] invented authoritative gameplay facts forbidden.
- [x] ScenarioConfig/AdaptiveDecision generation forbidden as authority.
- [x] map/spawn/route/stat/item/reward/economy invention forbidden.
- [x] prompt topology explicit.
- [x] prompt requires describe-provided-facts / no-new-authoritative-facts.
- [x] validator contract explicit.
- [x] validator checks language/length/schema/fact compatibility.
- [x] validator does not reject every imperative sentence.
- [x] validator does not transform text into gameplay config.
- [x] language policy explicit.
- [x] length policy explicit.
- [x] no invented numerical max length.
- [x] Backend/client/provider boundary explicit.
- [x] client direct provider call forbidden for authoritative pipeline.
- [x] cache identity/version compatibility explicit.
- [x] cache stores/reuses only validated compatible briefing.
- [x] CACHE-FIRST lookup behavior explicit.
- [x] cache invalidation/miss semantics explicit.
- [x] timeout finite semantic explicit.
- [x] retry finite semantic explicit.
- [x] retry-count meaning explicit.
- [x] no infinite provider retry.
- [x] template fallback explicit.
- [x] template fallback deterministic/LLM-independent explicit.
- [x] template fallback scenario/facts compatibility explicit.
- [x] supported P0 content package must include fallback.
- [x] GenAI failure does not block match explicit.
- [x] fallback packaging failure does not grant GenAI authority.
- [x] versioning/traceability explicit.
- [x] stochastic LLM output semantics explicit.
- [x] exact textual determinism not required.
- [x] deterministic cache/template semantics explicit.
- [x] data minimization explicit.
- [x] generated content ownership boundary explicit.
- [x] no gameplay authority overlap with AED/Traditional AI.
- [x] GenAI Mission Briefing Contract Test Cases defined.
- [x] Backend / AI / Gameplay / UI implementation does not need to invent scope/safety/cache/fallback semantics.

**Final Status: DONE / FROZEN**

---

# 31. Frozen Baseline Summary

```text
Task
M1-019 — GenAI Mission Briefing scope

Owner
C — AI / Telemetry / Research

Support
D — Backend / Shop / Payment

Dependency
M1-007

Priority
P0
```

```text
Feature boundary

GEN-01 Mission Briefing
→ ACTIVE / P0

GEN-02 Lore variation / richer narrative
→ DEFERRED / P2 FUTURE
```

```text
Canonical architecture

Validated ScenarioConfig
+
Designer Content Registry
        ↓
Trusted Mission Facts Resolver
        ↓
MissionBriefingFacts
        ↓
Backend / GenAI Adapter
        ↓
LLM Provider
        ↓
MissionBriefingValidator
        ↓
MissionBriefingOutput
        ↓
UI / Presentation Only
```

```text
Critical invariant

MissionBriefingOutput
MUST NOT become gameplay input
```

```text
MissionBriefingFacts

mapDisplayName
threatDisplayName
objectiveFacts[]
extractionFact
missionTheme
```

```text
MissionBriefingFacts ownership

server-owned
OR designer-authored
OR deterministic derivation from validated authoritative scenario data
```

```text
NOT trusted input

arbitrary client text
raw user prompt
raw model output
hidden runtime state
raw telemetry stream
free-form unvalidated payload
```

```text
MissionBriefingRequest

requestId
scenarioConfigVersion
language
briefingContractVersion
promptTemplateVersion
validationPolicyVersion
missionFactsVersionOrHash
facts
```

```text
Safe GenAI input

System Instruction
+
Trusted Mission Facts
+
Output Constraints
```

```text
Hidden/internal runtime state excluded

CurrentTarget
DetectionTarget
LastKnownPosition
hidden Player position
FSM state
Detection Meter internals
Attack internals
Navigation internals
secret route state
raw telemetry
```

```text
MissionBriefingOutput

text
language
source
status
modelRef
generatedAt
briefingContractVersion
promptTemplateVersion
validationPolicyVersion
missionFactsVersionOrHash
```

```text
source

GENAI
CACHE
TEMPLATE_FALLBACK
```

```text
status mapping

GENAI → VALID
CACHE → VALID
TEMPLATE_FALLBACK → FALLBACK
```

```text
modelRef

GENAI → REQUIRED
CACHE → preserve original if known
TEMPLATE_FALLBACK → null / NOT_APPLICABLE
```

```text
Presentation-only authority

briefing text MUST NOT modify:
ScenarioConfig
AdaptiveDecision
AED
FSM
Sensor
Target Selection
CurrentTarget
DetectionTarget
LastKnownPosition
Attack
Navigation
Spawn/Route
Stats
Items
Rewards
Economy
Shop/Payment
```

```text
Allowed briefing behavior

describe supplied mission
restate supplied objectives
identify supplied map/threat
state supplied extraction condition
add bounded stylistic flavor
```

```text
Core grounding rule

DESCRIBE PROVIDED FACTS
DO NOT CREATE NEW AUTHORITATIVE FACTS
```

```text
Mission-facing instruction

"Recover the Energy Cores."
→ allowed when trusted objective facts support it
```

```text
Invented authoritative detail

"Recover five cores."
→ INVALID if trusted facts do not specify five
```

```text
Forbidden GenAI authority

ScenarioConfig
AdaptiveDecision
FSM command
Target command
hidden Player state
map/layout
spawn coordinate
route graph
gameplay stat
item power
reward/economy/payment value
gameplay code
```

```text
MissionBriefingValidator

check non-empty
check schema association
check configured length
check requested/allowed language
check forbidden structured gameplay fields
check invented authoritative facts
check map/spawn/stat/item/reward/code restrictions
check FSM/Target/Sensor command restrictions
check compatibility with Trusted Mission Facts
check UI suitability
```

```text
Validator semantic

Allowed mission instruction grounded in facts
≠ forbidden invented gameplay authority
```

```text
Length/language policy

maxBriefingLength
allowedLanguages
→ CONFIGURABLE
→ server-owned
→ validationPolicyVersion
→ no invented numerical max
```

```text
Backend boundary

Client
→ Backend Mission Briefing Service
→ GenAI Adapter
→ Provider
→ Validator
→ Output/Fallback
→ UI
```

```text
Client MUST NOT

call provider directly for authoritative briefing pipeline
parse briefing into ScenarioConfig
use raw provider output as gameplay data
```

```text
Cache key

missionFactsVersionOrHash
scenarioConfigVersion
language
briefingContractVersion
promptTemplateVersion
validationPolicyVersion
```

```text
Cache policy v0

CACHE-FIRST

compatible VALID cache hit
→ source=CACHE
→ provider call not required
```

```text
Cache miss conditions include incompatible

facts identity
ScenarioConfig version
language
contract version
prompt version
validation version
```

```text
GenerationPolicy

generationPolicyVersion
timeoutMs
maxRetryCount
```

```text
timeoutMs
→ positive finite configurable value

maxRetryCount
→ non-negative finite integer
→ retries AFTER initial attempt
```

```text
No infinite retry
```

```text
Failure path

cache miss
→ GenAI attempt
→ INVALID / timeout / provider error
→ finite retry if available
→ retry exhausted
→ TemplateMissionBriefing
```

```text
Critical availability rule

GenAI failure
≠ match failure
```

```text
Correctly packaged supported P0 match resolves briefing through

VALID GENAI
OR
VALID CACHE
OR
TEMPLATE_FALLBACK
```

```text
TemplateMissionBriefing

designer-authored
deterministic
LLM-independent
compatible with trusted facts/scenario
presentation-only
required for every supported P0 scenario package
```

```text
Missing/invalid fallback
→ configuration/content packaging error
→ GenAI does not invent authoritative replacement scenario/facts
```

```text
Versioning / traceability

briefingContractVersion
promptTemplateVersion
validationPolicyVersion
scenarioConfigVersion
missionFactsVersionOrHash
modelRef
providerRef if applicable
generationPolicyVersion/config reference if applicable
```

```text
GenAI output may be stochastic
→ exact text determinism NOT required
→ traceability IS required
```

```text
Template fallback
→ deterministic for same template/facts/language

Compatible cache hit
→ deterministic reuse behavior
```

```text
Data minimization

no secrets
no credentials
no payment data
no unnecessary personal data
no raw telemetry history
no hidden positions
no internal AI state dump
```

```text
GeneratedContent
→ presentation artifact only
→ not GameplayConfiguration
→ not ScenarioConfig
→ not AdaptiveDecision
→ not FSM input
→ not game rule
```

```text
M1-019 v0
→ safe presentation-text generation boundary
→ Mission Briefing P0 only
→ lore richness deferred
→ cache-first
→ bounded validation
→ finite timeout/retry
→ deterministic template fallback
→ match-start safe
→ traceable
→ no gameplay authority
```

**Final Status: DONE / FROZEN**
