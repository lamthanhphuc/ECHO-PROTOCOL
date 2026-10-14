# AED v0.2 Rev02 deviations and conservative choices

- Owner approval A–G supersedes the attached documents' older "awaiting approval" status, for pilot configuration only.
- Existing local RuntimeLogSettings and TMP fallback font changes are retained and excluded from commits.
- Normal Zone3 minion cap is read from current source; stale historical notes are not used as baseline.
- No production activation or merge is authorized. New behavior requires Adaptive Normal, the pilot feature gate and a valid Backend policy envelope.
- The Backend Docker build context contains only `EchoProtocol.Backend`. Pure v0.2 policy/plan sources are mirrored there with `scripts/Sync-AEDV02Contract.ps1 -Check` enforcing byte equality, so Docker builds do not depend on the Unity project being present.

No implementation deviation has been accepted implicitly. Missing evidence produces HOLD; gameplay progression continues.
