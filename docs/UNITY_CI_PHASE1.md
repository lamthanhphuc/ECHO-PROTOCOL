# Unity CI Phase 1 — GitHub checks and local Windows tests

## What runs where

GitHub Actions keeps the existing Backend, Web, and Docker checks in `.github/workflows/aed-backend.yml`. `.github/workflows/unity-editmode.yml` runs only the Python NUnit result-validator tests on pull requests to `LTP`. A green Python gate does **not** mean Unity tests passed. No GitHub-hosted Unity job, license secret, GameCI activation, or GitHub self-hosted runner is used.

Real Unity tests run on a local Windows machine with Unity Hub Personal activated for that machine and Editor `6000.5.8f1` installed. `scripts/Invoke-UnityLocalCI.ps1` checks the exact Editor build and project version, Test Framework `1.7.0`, Photon dependency, clean Git commit, LFS objects, project lock, and conflicting Editor processes. It runs EditMode with `-batchmode -nographics` and PlayMode with `-batchmode` and graphics. It preserves each Unity exit code, validates real NUnit XML with `scripts/check_unity_editmode_results.py`, and returns failure if either mode or its result gate fails. Each run prints the commit, Editor version, evidence directory, and actual pass/fail/skip/inconclusive/total counts.

The Unity project has six EditMode asmdefs (Common, Networking, Player, Stalker, Telemetry, UI), additional EditMode tests in the default Editor assembly, and three PlayMode asmdefs (Networking, Stalker, Voice). Its Git packages include Photon Fusion/Voice and Unity MCP. Git LFS tracks about 5.25 GiB of assets. A clean worktree and sufficient free disk for Unity's `Library` are required.

## Run on a clean commit

Close the Editor for the test project. Do **not** run tests against the developer's dirty `D:\Bin\KLTN\KLTN` project; four protected WIP files are present there. From `D:\Bin\KLTN`, create an isolated checkout at the exact commit to test:

```powershell
rtk git worktree add --detach D:\Bin\KLTN-unity-ci <full-commit-sha>
rtk git -C D:\Bin\KLTN-unity-ci lfs pull
powershell -NoProfile -File D:\Bin\KLTN-unity-ci\scripts\Invoke-UnityLocalCI.ps1 `
  -RepositoryRoot D:\Bin\KLTN-unity-ci -Commit <full-commit-sha> `
  -EvidenceRoot D:\Bin\UnityCI-Evidence
```

The script refuses a dirty or wrong-commit worktree and refuses to place evidence inside tracked source. Each run creates `D:\Bin\UnityCI-Evidence\<commit>-<timestamp>\EditMode\results.xml`, `Editor.log`, and matching `PlayMode` files. The console and XML provide counts; the log explains compilation, package, scene, or runner failures. Evidence is outside the repository and must not be committed. If EditMode fails, PlayMode still runs and the script returns nonzero. To review before merging, match the evidence directory's SHA to PR #32's head, verify both XML roots and Unity exits, read failed-case stacks and Editor logs, then check the GitHub Backend/Web/Docker/Python jobs on the same head. A clean Unity result is required before claiming Unity is green.

The demo target is Windows 10/11 64-bit (`docs/SRS.md`). A general player build is not part of this script. The prior local Windows64 build compiled scripts but spent over 40 minutes compiling shader variants and was stopped without a player or final report; `KLTN/Logs/build-StandaloneWindows64-1793284823651.log` is the diagnostic log. Build verification remains unverified.

## Existing local test evidence

The first EditMode run compiled but `EditModeRunTask` aborted without XML. `AlienHuntSceneTests` and `PurgeDirectorTests` requested `EnterPlayMode(false)` while project settings enabled domain reload. Their arguments now match the setting. The last full local run on 2026-10-29 produced `KLTN/Logs/CIEditModeFinalCI.xml` and `.log`: **1,508 passed, 21 failed, 0 skipped, 0 inconclusive / 1,529 total**. This run included the protected local WIP, so it is diagnostic, not a clean PR-head baseline.

| EditMode failure group | Count | Local XML evidence |
| --- | ---: | --- |
| Main menu leaderboard | 12 | All fail in SetUp at `BuildOverlay():248`; `_canvas` is assigned only in `Awake`, but EditMode calls `Install()` immediately after `AddComponent`. A minimal initialization fix is under test. |
| Alien Hunt | 2 | Meat position is not on NavMesh; Play transition test raises a null reference. |
| Relay B | 1 | Source contract expects a particular authority-guard string that is absent. |
| Zone 1/2 | 2 | `RelayAUIController.Close()` dereferences an uninitialized lock; an offline authorization code differs between director and flow. |
| Outbox | 1 | Serialized saved record has a non-null default request object, contrary to the test expectation. |
| Purge | 2 | `AED_CONTENT_CONTRACT_MISSING` during offline state-authority spawn; another test tries to rename saved `Scene_B.unity`. |
| Map 2 Stalker | 1 | `PlayerStart_EMPTY` path is `PathPartial`, expected `PathComplete`. |

PlayMode with `-nographics` produced `KLTN/Logs/CIPlayModeFull.xml` and `.log`: **191 passed, 5 failed / 196 total**. Four Stalker tests remained in `DETECT` instead of reaching `CHASE` within 30 frames; one SciFi locker test failed in SetUp after `RenderTexture.Create failed`. These are observed headless failures, not proven root causes. With graphics enabled, `KLTN/Logs/CIPlayModeGraphics.xml` and `.log` showed **196/196 passed**. The clean worktree must be retested; these older results do not prove the new commit passes.

## Why Unity is local

Four earlier hosted runs executed zero Unity tests. [40999716204](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/40999716204) hit a GameCI CLI flag error; [41000165967](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41000165967) ran out of Docker disk; [41000598607](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41000598607) rejected ULF data; [41003468687](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41003468687) failed Unity Licensing with Code 400, `TimeStamp validation failed`. None produced NUnit XML or Unity artifacts. We do not retry or alter that license.

[Unity 6000.5](https://docs.unity.com/en-us/engine/6000.5/manual/get-started/install-and-upgrade/licenses-and-activation/license-activation-methods) says Unity Personal activation and return use Unity Hub; manual ULF activation is unsupported for Personal. Current [GameCI documentation](https://game.ci/docs/github/test-runner/) still describes a Personal ULF secret, but this project has no successful hosted activation. Local CI uses the already licensed Windows Editor without transferring license material. No Student/Pro license or GitHub runner registration is required. PR #32 remains Draft until real clean-commit Unity evidence is reviewed; no merge or required-check change is automatic.
