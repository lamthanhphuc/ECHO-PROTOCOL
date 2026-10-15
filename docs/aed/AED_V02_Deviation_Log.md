# AED v0.2 Rev02 deviations and conservative choices

- Owner approval A–G supersedes the attached documents' older "awaiting approval" status, for pilot configuration only.
- Existing local RuntimeLogSettings and TMP fallback font changes are retained and excluded from commits.
- Normal Zone3 minion cap is read from current source; stale historical notes are not used as baseline.
- No production activation or merge is authorized. New behavior requires Adaptive Normal, the pilot feature gate and a valid Backend policy envelope.
- The Backend Docker build context contains only `EchoProtocol.Backend`. Pure v0.2 policy/plan sources are mirrored there with `scripts/Sync-AEDV02Contract.ps1 -Check` enforcing byte equality, so Docker builds do not depend on the Unity project being present.

No implementation deviation has been accepted implicitly. Missing evidence produces HOLD; gameplay progression continues.

- PP3 reinspection lowers existing positive selection priority by 25% only for the player identity actually learned from observation/inspection. It never infers an occupant, grants immunity or excludes the sole eligible spot.
- Cabinet source verification independently checks accepted clue lineage, cabinet bounds, range, roster and world state; it does not claim a Backend replay of Unity physics LOS.
- History normalization, complete Resource/Objective opportunities and calibrated team TP remain unsupported. A–G approval does not provide these omitted definitions.
- R0 supply is implemented. Non-R0 placement and regular/emergency support remain explicit contract/engineering blockers, never silently replaced by R0.
- Cabinet activation reserves 300-second shared separation even if teleport/approach later aborts; a cue-only failure does not pretend a jump occurred. Ordinary Special cooldown/backoff stays unchanged.
