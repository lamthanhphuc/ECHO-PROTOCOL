# Unity CI Phase 1 — current status

## CI design and security

The Unity project is `KLTN/`, Editor `6000.5.8f1`, Unity Test Framework `1.7.0`. `.github/workflows/unity-editmode.yml` runs only the Python NUnit validator tests on pull requests targeting `LTP`. Its separate Unity jobs run actual EditMode and PlayMode tests only on `workflow_dispatch` from `LTP` or `ci/unity-editmode-phase1`. They require a dedicated Windows x64 runner labeled `echo-unity-ci` and run one at a time. No Unity job triggers from pull requests; neither mode is a passing PR gate yet.

The runner checks out Git LFS, then invokes its installed Unity Editor directly. EditMode uses `-batchmode -nographics`; PlayMode uses `-batchmode` with graphics available. Each mode writes `artifacts/<mode>/results.xml` and `Editor.log`, uploads the directory even after failure, and passes the Unity step outcome to the strict XML validator. The gate rejects a failed runner, missing or invalid XML, zero tests, failed or inconclusive tests, inconsistent counters, or a non-passing NUnit root; it reports passed, failed, skipped, inconclusive, and total counts. Backend, Web, and Docker checks stay in `.github/workflows/aed-backend.yml`.

The six EditMode asmdefs cover Common, Networking, Player, Stalker, Telemetry, and UI; other EditMode tests compile into the default Editor assembly. Three PlayMode asmdefs cover Networking, Stalker, and Voice. Photon Fusion/Voice and Unity MCP are Git package dependencies. Git LFS tracks 1,698 assets, about 5.25 GiB. The test sources include offline Fusion `GameMode.Single`, Stalker scene/graphics tests, and Voice component tests; they do not require production API endpoints or live multiplayer sessions. Package import and disk space must still be proven on the dedicated runner.

The demo targets Windows 10/11 64-bit (`docs/SRS.md`). No build job is enabled: the local Windows64 build did not finish, and the dedicated runner and its Windows build module have not been verified.

## Unity Personal licensing decision

Unity 6000.5 states that **Unity Hub is the only activation and return method for Unity Personal**; its manual `.alf`/`.ulf` process does not support Personal. [Unity 6000.5 activation methods](https://docs.unity.com/en-us/engine/6000.5/manual/get-started/install-and-upgrade/licenses-and-activation/license-activation-methods), [Unity Hub licensing](https://docs.unity.com/en-us/hub/manage-license). Current [GameCI test-runner documentation](https://game.ci/docs/github/test-runner/) still describes a `UNITY_LICENSE` secret for Personal. In this project, that path failed before test execution. We will not retry the same ULF, edit its timestamp/signature, or copy a workstation license into a hosted container.

The new Windows workflow uses the Personal entitlement activated by Unity Hub **on the runner itself**, under the same Windows account that launches the runner and Editor. It does not read `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, or `UNITY_SERIAL` from GitHub Actions. Existing secret values are not needed by this workflow; an operator can remove obsolete secrets after checking other workflows that may use them. A service under `LocalSystem` may not see the Hub user's entitlement or graphics desktop, so the first run must use the signed-in runner account and verify licensing and graphics there.

GitHub warns that untrusted pull requests can execute hostile code on self-hosted runners. Use a dedicated machine or VM with a standard user, no production credentials or access, and a unique runner label. A label and this workflow's branch guard do **not** prevent another repository workflow from targeting the runner; review Actions approval and runner access policies before registration. Do not enable automatic PR execution or `pull_request_target` for this runner. See [GitHub's self-hosted runner warning](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners) and [runner labels](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/use-in-a-workflow).

## Operator setup and execution

1. Provision an isolated Windows x64 runner with enough free disk for the 5.25 GiB LFS checkout, Unity Library, and artifacts. Install Unity Hub, Editor `6000.5.8f1` at `C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe`, the Windows standalone module if build verification is later requested, Git LFS, Python 3 (`python` on `PATH`), and the GitHub Actions runner. Do not reuse the developer workstation or an account with production credentials.
2. Sign in to Unity Hub **as the dedicated runner user** and confirm Unity Personal is active. Keep the runner process under that user; for PlayMode, verify an available graphics desktop. Close any Editor instance for the same project before a batchmode run. First run the commands below locally on that account and inspect both XML and logs.
3. Register the runner with the unique label `echo-unity-ci` using GitHub's [Add runner](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners) instructions. Review whether other workflows in this public repository could target it. Do not add production secrets or open production network access.
4. Manually dispatch **Unity CI Phase 1** (`unity-editmode.yml`) on `ci/unity-editmode-phase1` (or `LTP` after review). Download `unity-EditMode-results` and `unity-PlayMode-results`; inspect `results.xml`, `Editor.log`, runner exit status, and the validator summary. Keep PR #32 Draft while EditMode is red or hosted execution is unverified. Changing required checks or branch protection needs operator approval.

From the repository root, with the Editor closed, the equivalent local commands are:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'D:\Bin\KLTN\KLTN' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Bin\KLTN\KLTN\Logs\CIEditMode.xml' `
  -logFile 'D:\Bin\KLTN\KLTN\Logs\CIEditMode.log'
```

For PlayMode, change `-testPlatform` to `PlayMode`, use distinct output names, and omit `-nographics`. Then run `python scripts/check_unity_editmode_results.py <results-directory> <unity-step-outcome> EditMode` (or `PlayMode`). Preserve Unity's process exit code. For a later Windows64 player check, run `unity build KLTN --target StandaloneWindows64 --output-path <temporary-directory>/EchoProtocol.exe` with the Editor closed, then require a successful exit, build report, and player output. `AEDV02BatchBuild.WindowsDevelopment` is an AED pilot build, not a general release build.

## Actual test evidence and remaining failures

The first local EditMode run compiled scripts but crashed `EditModeRunTask` without XML. `AlienHuntSceneTests` and `PurgeDirectorTests` requested `EnterPlayMode(false)` while Editor settings enabled domain reload. Their EnterPlayMode arguments now match the setting. A full local run then produced `KLTN/Logs/CIEditModeFinalCI.xml` and `.log`: **1,508 passed, 21 failed, 0 skipped, 0 inconclusive, 1,529 total**. The runner now exports valid XML, but EditMode is **FAIL**, not a clean baseline. The four protected local WIP files were present during this run.

| EditMode failures | Count | Evidence from local XML |
| --- | ---: | --- |
| Alien Hunt scene | 2 | Scene validation expected true; Play transition test raised `NullReferenceException` at `AlienHuntSceneTests.cs:101`. More scene/runtime isolation is needed. |
| Relay B contract | 1 | Source contract expected a specific authority guard string that is absent. |
| Zone 1/2 flow | 2 | `RelayAUIController.Close()` null reference during debug skip; Zone 2 authorization codes differ (`1774` versus actual value). |
| Main menu leaderboard | 12 | All fail in shared SetUp at `BuildOverlay():248`. `_canvas` is read there but assigned only by `Awake`; the EditMode test calls `Install()` immediately after `AddComponent`. This is the likely null reference; confirm with a focused run before changing code. |
| Outbox serialization | 1 | Expected null payload after restart, but `SubmitMatchResultRequestDto` remained. |
| Purge director/gameplay | 2 | `AED_CONTENT_CONTRACT_MISSING` during authoritative spawn; another test attempts to rename a saved `Scene_B.unity`, which Unity rejects. |
| Map 2 Stalker spawn | 1 | `PlayerStart_EMPTY` path was `PathPartial`, expected `PathComplete`. |

PlayMode with `-nographics` produced `KLTN/Logs/CIPlayModeFull.xml` and `.log`: **191 passed, 5 failed, 0 skipped, 0 inconclusive, 196 total**. Four Stalker flow tests stayed in `DETECT` instead of reaching `CHASE` within 30 frames; the SciFi locker occlusion test failed in SetUp after `RenderTexture.Create failed`. These are headless-only observations, not proven timing or rendering root causes. A Windows batchmode run **with graphics** produced `KLTN/Logs/CIPlayModeGraphics.xml` and `.log`: **196 passed, 0 failed, 0 skipped, 0 inconclusive**. Hosted PlayMode remains unverified.

A local Windows64 build compiled scripts but spent over 40 minutes compiling shader variants and was stopped without a player or final report. The diagnostic log is `KLTN/Logs/build-StandaloneWindows64-1793284823651.log`. Build status is **BLOCKED/UNVERIFIED**.

## Hosted attempts and troubleshooting

| Run | Result before tests |
| --- | --- |
| [40999716204](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/40999716204) | GameCI CLI rejected `--no-coverageEnabled`. |
| [41000165967](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41000165967) | Docker image pull exhausted hosted runner disk. |
| [41000598607](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41000598607) | Image downloaded; Unity rejected ULF root data as invalid. |
| [41003468687](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41003468687) | Unity Licensing Code 400: `TimeStamp validation failed` in EditMode and PlayMode. |

All four hosted attempts executed **zero Unity tests**, produced no NUnit XML or Unity test artifacts, and failed the XML gate. The Python validator unit tests passed independently; they are not Unity execution evidence. The self-hosted Windows workflow has not yet run. If it fails before XML, inspect its GitHub step log and uploaded `Editor.log` (if created), first checking runner account, Hub entitlement, exact Editor version, LFS, package import, and graphics desktop. Do not interpret a skipped PR Unity check or a green Python gate as passing Unity tests.
