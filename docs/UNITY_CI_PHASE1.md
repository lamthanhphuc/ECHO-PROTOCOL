# Unity CI — Phase 1 status

## What runs

The Unity project is `KLTN/`, pinned to Editor `6000.5.8f1` with Unity Test Framework `1.7.0`. The separate `.github/workflows/unity-editmode.yml` runs its XML validator on pull requests targeting `LTP`. Actual Unity EditMode and PlayMode executions are separate matrix jobs currently limited to `workflow_dispatch` while licensing and the clean hosted baseline are unresolved. They are not passing PR gates yet. Backend, Web, and Docker checks remain in `.github/workflows/aed-backend.yml`.

The six EditMode asmdefs cover Common, Networking, Player, Stalker, Telemetry, and UI. Other EditMode sources under Controls, Listener, MatchFlow, StalkerEditorTools, and Tools compile into the default Editor assembly. Three PlayMode asmdefs cover Networking, Stalker, and Voice. The test assemblies reference NUnit, Unity test runners, project assemblies, and Photon Fusion's runtime DLL. Package import also pulls Photon Voice/Fusion and Unity MCP from Git, plus Unity AI Inference and URP. A successful C# compilation does not prove test execution.

Git LFS tracks 1,698 assets totaling about 5.25 GiB. Each Unity job uses LFS checkout and verifies objects. GameCI selected `unityci/editor:ubuntu-6000.5.8f1-linux-il2cpp-3` on Ubuntu. Project import, tests, and build have not yet been verified in that image. The jobs remove unused Android, .NET, and hosted tool caches from their ephemeral runners to make room for the image and Unity Library. The GameCI test runner action and CLI are pinned. GameCI writes mode results and Editor logs below the configured `artifacts/<mode>/` directory; each job uploads that directory even after failures. The XML gate requires one valid NUnit `test-run` report, positive test count, consistent counters, no failed or inconclusive tests, and a successful runner step. It prints and summarizes passed, failed, skipped, inconclusive, and total counts.

## Local commands

Close every Editor instance for this project first. From the repository root on Windows PowerShell:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'D:\Bin\KLTN\KLTN' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Bin\KLTN\KLTN\Logs\CIEditMode.xml' `
  -logFile 'D:\Bin\KLTN\KLTN\Logs\CIEditMode.log'
```

For PlayMode, use `-testPlatform PlayMode`, distinct XML/log paths, and **omit `-nographics`**; a local no-graphics run failed tests that passed with graphics available. Run the XML validator against a directory containing only that run's XML: `python scripts/check_unity_editmode_results.py <directory> success PlayMode` (use `EditMode` for EditMode). Preserve the Unity process exit code and XML; an assertion failure legitimately returns a failing code.

The demo target is **Windows 10/11 64-bit** (`docs/SRS.md`). This machine has the Windows standalone module. For a local build check, use `unity build KLTN --target StandaloneWindows64 --output-path <temporary-directory>/EchoProtocol.exe` with the Editor closed. Check the build exit code, build report, and output player. The existing `AEDV02BatchBuild.WindowsDevelopment` uses an AED pilot define and is not a general release build.

## Results and troubleshooting

On 2026-10-29, the first full local EditMode run compiled but exited `RunError` without XML. An isolated `AlienHuntSceneTests.BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien` reproduced the same `EditModeRunTask` null reference. The test passed `EnterPlayMode(false)` while project settings enabled domain reload; Unity Test Framework's argument explicitly means no reload is expected. The updated tests match the actual Editor setting. The isolated run then produced NUnit XML, proving the runner reached a test result. The final local full run produced `KLTN/Logs/CIEditModeFinalCI.xml` and `.log`: **1,508 passed, 21 failed, 0 skipped, 0 inconclusive, 1,529 total**. This was run with four protected local WIP files present, so it is not a clean PR-head pass. Failures include Alien Hunt, Relay B source contract, Zone 1/2 flow, 12 leaderboard UI tests, outbox serialization, Purge director/gameplay, and Map 2 Stalker navigation. No assertions were disabled or weakened.

PlayMode with `-nographics` produced `KLTN/Logs/CIPlayModeFull.xml` and `.log`: **191 passed, 5 failed, 0 skipped, 0 inconclusive, 196 total**. Four Stalker timing assertions failed and one graphics test logged `RenderTexture.Create failed`. A second Windows batchmode run with graphics enabled produced `KLTN/Logs/CIPlayModeGraphics.xml` and `.log`: **196 passed, 0 failed, 0 skipped, 0 inconclusive, 196 total**. These tests include an offline Fusion `GameMode.Single` case, Stalker scene/graphics tests, and Voice component tests; no production API or live multiplayer session is required by the test sources. Linux GameCI display compatibility remains unverified. EditMode success cannot establish PlayMode or player build health.

A local Windows64 player build compiled scripts but remained in shader variant compilation after more than 40 minutes. It was stopped without a player output or final build report; `KLTN/Logs/build-StandaloneWindows64-1793284823651.log` is the diagnostic log. Build verification is therefore **blocked/unverified**, not passed. The hosted GameCI build and its Windows module are also unverified; no build job has been added on the strength of a partial local build.

The first hosted manual run, [40999716204](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/40999716204), passed the XML gate and Git LFS check but never started Unity: GameCI CLI v0.1.72 rejected the action's `--no-coverageEnabled` flag. Both Unity jobs correctly failed for missing XML; no artifacts were produced. The action input was removed for the next run. The Ubuntu runner had only 2.9 GiB free after LFS checkout, so image pull and import may need more disk.

The second hosted run, [41000165967](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41000165967), got past that flag but Docker could not pull the Unity image: `no space left on device`. Again, zero Unity tests executed and no NUnit artifacts existed. The workflow now frees unused tools on the ephemeral runner before checkout.

The third hosted run, [41000598607](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/41000598607), confirmed the disk fix: after LFS checkout, EditMode had 25 GiB free and downloaded the exact Unity image. Unity Licensing then rejected the configured `UNITY_LICENSE` with `Cannot load ULF license: Data at the root level is invalid`. Both Unity jobs failed before test execution; zero tests ran, and no XML or Editor log artifact was produced. Replace the secret with the complete activated `.ulf` contents without sharing them in chat, then rerun the manual workflow. Do not treat the green PR XML gate or the skipped Unity PR check as a successful Unity run.

## GitHub Actions and operator steps

GameCI needs either `UNITY_LICENSE` for an activated Personal license, or `UNITY_SERIAL` for Pro, plus `UNITY_EMAIL` and `UNITY_PASSWORD` as required by the chosen license path. Store them as GitHub Actions secrets; never put values in chat, source, artifacts, or logs. See [GameCI test runner](https://game.ci/docs/github/test-runner/) and [Unity licensing](https://docs.unity.com/en-us/engine/6000.6/manual/get-started/install-and-upgrade/licenses-and-activation/managing-your-unity-license). The repository now lists `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` secret names; their values and validity are unknown until a hosted run. Fork pull requests do not receive secrets. A repository license must not be exposed to arbitrary PR code; enable a trusted PR runner policy or manual approval before making Unity tests a required PR check.

After license setup, dispatch **Unity CI Phase 1** on PR #32's head, inspect the EditMode and PlayMode artifacts and job summaries, then enable the Unity jobs on trusted PRs only after successful hosted runs. Required checks and branch protection changes need operator approval. Keep PR #32 in Draft while hosted Unity runs are blocked or red. A missing XML after an import, licensing, or runner error is a failure; inspect the uploaded Editor log and action output for the earlier cause.
