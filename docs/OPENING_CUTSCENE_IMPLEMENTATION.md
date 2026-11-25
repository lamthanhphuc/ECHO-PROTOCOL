# Opening cutscene — triển khai và kiểm chứng

Ngày thực hiện: 26/11/2026. Unity 6000.5.8f1, URP 17.5.0, Timeline 1.8.12, Fusion 2 hiện có. Không thêm package, sửa backend, commit hoặc push.

## Kết quả trong project

`Assets/Scenes/SciFi.unity` có root `OpeningCutscene`, PlayableDirector, CinematicCamera/Animator/AudioListener, đèn cinematic local, năm cặp shot anchors Start/End, CinematicAudio và Canvas gồm letterbox, subtitle, fade, SkipButton và vote counter. Timeline 30 giây dùng năm AnimationClip riêng; không dùng video.

Các pose lấy từ scene đang hoạt động và NavMesh qua Editor Tool. Root mẫu bị tắt không được dùng. Storyboard, nguồn lore, khác biệt tài liệu cũ và tọa độ cụ thể: [OPENING_CUTSCENE_STORYBOARD.md](OPENING_CUTSCENE_STORYBOARD.md).

## Lifecycle và các giới hạn

- Giữ nguyên loading barrier và quy trình authorization/spawn. `GameplayReadinessReleased` chỉ cho intro biết barrier đã được host release; không đánh dấu complete thay barrier.
- Network state nằm trong partial của NetworkMatchState, không thêm NetworkObject/Runner, không đổi giá trị NetworkMatchPhase hoặc NetworkMatchStatus. Host công bố tick bắt đầu trước 1.5 giây và end timer replicated. Mỗi máy tự Evaluate cùng Timeline theo thời gian Fusion; không replicate transform camera.
- Timer nhiệm vụ của SciFi không chạy trong loading/intro khi configuration hợp lệ. Host tạo đủ duration tại Completed. Map không có intro tiếp tục flow cũ.
- Input lock dùng cơ chế hiện có. Movement, teleport/hiding/flashlight RPC, interactor/team tool validation, objective/phase/end commit, damage, monster simulation/navigation, gameplay noise và AED observations bị gate trên authority. Không dùng Time.timeScale.
- Gameplay telemetry bắt đầu sau intro trên map được hỗ trợ để initial phase/AED evidence không chứa thời gian cinematic. Backend start/lease và pre-match authorization vẫn chạy như trước. Spawn facts dùng hàng đợi initial spawns sẵn có.
- Hold Space một giây hoặc nhấn Skip để vote. Host kiểm tra người gửi còn kết nối, có đúng gameplay PlayerObject và spawn ready; mỗi người một vote. Denominator là toàn bộ ActivePlayers thực. Disconnect tự thay đổi electorate. Unanimity/solo đặt end deadline chung sau 0.65 giây.
- End timer là giới hạn playback độc lập với callback PlayableDirector. Host không chờ client gửi “đã phát xong”. NotStarted vẫn phụ thuộc loading readiness/timeout hiện có; intro không vượt qua loading thất bại.
- Nếu host thiếu definition/Timeline/reference, intro không được bật. Nếu một peer thiếu presentation trong khi host đang phát, peer chỉ báo về host; host lên lịch fallback chung, peer không tự mở gameplay.
- Completed/start tick/end timer/votes nằm trong snapshot. Host migration không đi lại nhánh khởi tạo mới; proxy vào giữa seek thời gian hiện tại. Đây là thiết kế đã kiểm tra bằng rules/asset tests, chưa phải bằng chứng chạy migration qua nhiều máy.
- Cleanup khôi phục đúng enabled state của camera, AudioListener, HUD Canvas; release riêng input lock để không phá modal khác, dừng audio/director và tắt đèn cinematic. Cursor dùng snapshot/ref-count của PlayerInteractionControlLock hiện hữu. Scene disable/destroy và network recovery đều cleanup.
- PlayerFirstPersonVisibility chuyển local player sang body view trong cinematic để không hiện tay FPS rời khỏi nhân vật; cleanup trả đúng mode đã lưu. Không đổi renderer/prefab mặc định.
- Đèn cinematic sao chép Light và beam settings từ PlayerNetwork, intensity giảm 50%; không phát gameplay noise, không có NetworkObject và chỉ bật khi cinematic render. Không chỉnh global lighting hoặc URP.

## File mới

Các path Unity dưới đây tính từ `KLTN/`. Unity đã sinh `.meta` cho scripts, thư mục và assets mới.

| File | Vai trò |
| --- | --- |
| `Assets/Scripts/Cinematics/OpeningCutsceneDefinition.cs` | ScriptableObject cho từng map, shots, subtitles Việt/Anh, audio keys và skip hold. |
| `Assets/Scripts/Cinematics/OpeningCutsceneController.cs` | Playback local, camera handoff, UI/input/audio snapshot và cleanup. |
| `Assets/Scripts/Cinematics/OpeningCutsceneCoordinator.cs` | Quan sát lifecycle, báo presentation lỗi và giữ navigation host trong intro. |
| `Assets/Scripts/Cinematics/OpeningCutscenePresentation.cs` | Canvas, letterbox, fade, subtitle và skip vote UI. |
| `Assets/Scripts/Cinematics/OpeningCutsceneShotAnchor.cs` | Start/End pose authoring, source reference và frustum gizmo. |
| `Assets/_Project/Scripts/Networking/Match/NetworkMatchState.Opening.cs` | Replicated state/tick/timer/votes, authority RPC validation và gameplay release. |
| `Assets/_Project/Scripts/Networking/Rules/OpeningCutsceneRules.cs` | Deterministic timing, duration validation, electorate/vote và release rules. |
| `Assets/Editor/OpeningCutsceneSceneSetup.cs` | Khảo sát, setup SciFi, bake animation, URP preview, test runner và request bridge. |
| `Assets/_Project/Cinematics/SciFiOpeningDefinition.asset` | Configuration SciFi. |
| `Assets/_Project/Cinematics/SciFiOpening.playable` | Timeline 30 giây. |
| `Assets/_Project/Cinematics/Shot01_Establishing.anim` | Camera path shot 1. |
| `Assets/_Project/Cinematics/Shot02_Incident.anim` | Camera path shot 2. |
| `Assets/_Project/Cinematics/Shot03_Mission.anim` | Camera path shot 3. |
| `Assets/_Project/Cinematics/Shot04_Threat.anim` | Camera path shot 4. |
| `Assets/_Project/Cinematics/Shot05_PlayerSpawn.anim` | Camera path shot 5. |
| `Assets/Tests/EditMode/Networking/OpeningCutsceneRulesTests.cs` | Timing, snapshot arithmetic, votes, disconnect, solo, invalid duration, supported/unsupported release. |
| `Assets/Tests/EditMode/Controls/Editor/OpeningCutsceneLifecycleTests.cs` | Missing Timeline, cleanup idempotence, asset/scene bindings, actual Timeline pose evaluation và Fusion RPC weaving. |
| `Assets/Tests/EditMode/Controls/Editor/OpeningCutsceneRequestFileTests.cs` | Khóa writer/reader, tránh đọc nội dung ghi dở, UTF-8/BOM, consume một lần và file rỗng/đã biến mất. |
| `docs/OPENING_CUTSCENE_STORYBOARD.md` | Storyboard và hướng dẫn Inspector. |
| `docs/OPENING_CUTSCENE_IMPLEMENTATION.md` | Báo cáo này. |

## File sửa

| File | Thay đổi |
| --- | --- |
| `Assets/Scenes/SciFi.unity` | Cài root cinematic và references; giữ geometry/gameplay objects hiện hữu. |
| `Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs` | Khởi tạo intro, defer mission timer, authority tick và mutation gates. |
| `Assets/_Project/Scripts/Networking/Session/MatchLoadingCoordinator.cs` | Expose readiness release và runner ownership read-only. |
| `Assets/Scripts/Interaction/PlayerInteractionControlLock.cs` | Cinematic gate dùng chung cho local gameplay input. |
| `Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs` | Chặn movement và gameplay movement RPC trong intro. |
| `Assets/_Project/Scripts/Networking/Player/NetworkPlayerFlashlight.cs` | Chặn toggle RPC trong intro. |
| `Assets/Scripts/Player/PlayerFirstPersonVisibility.cs` | Cho cinematic dùng body view; restore mode khi trở về FPS. |
| `Assets/_Project/Scripts/Networking/Player/NetworkPlayerLifeState.cs` | Chặn authoritative damage/down trong intro. |
| `Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs` | Reject gameplay/tool commands tại validation boundary. |
| `Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs` | Đợi release trước population maintenance; giữ spawn lifecycle hiện hữu. |
| `Assets/_Project/Scripts/Networking/Authority/HostRuntimeNoiseService.cs` | Không nhận gameplay noise trong intro. |
| `Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs` | Defer gameplay telemetry start/AED sampling, giữ backend/outbox lifecycle. |
| `Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs` | Gate authoritative simulation. |
| `Assets/Scripts/AI/Minions/CreepMinionRuntime.cs` | Gate authoritative simulation. |

Không sửa Scene_A/Scene_B, LobbyManager, MatchLoadingRpc, MatchLoadingBarrier, package manifest, NetworkMatchPhase/Status, rewards/API schemas hoặc prefab PlayerNetwork/monster. Ba font assets đã dirty khi bắt đầu không được revert. Font cache của loading UI phát sinh từ EditMode tests được dọn riêng về trạng thái ban đầu.

## Kiểm thử đã chạy

Biên dịch thực tế trong Unity, gồm Fusion weaving. Bộ EditMode chạy qua TestRunnerApi ngay trong Editor: **126 passed, 0 failed, 0 skipped**; lần cuối khoảng 18.2 giây. Có kiểm tra actual Timeline Evaluate camera ở giữa từng shot, continuity/duration, saved scene bindings, missing UI fallback, body-view cleanup, generated RPC invokers và xử lý khóa file request.

| Test fixture | Passed | Failed |
| --- | ---: | ---: |
| CinematicMatchLoadingScreenTests | 11 | 0 |
| MatchLoadingBarrierTests | 13 | 0 |
| MatchLoadingLifecycleTests | 6 | 0 |
| OpeningCutsceneLifecycleTests | 7 | 0 |
| OpeningCutsceneRequestFileTests | 6 | 0 |
| FusionPlayerLifecycleContractTests | 21 | 0 |
| NetworkMatchStateTests (gồm các contract AED hiện có) | 28 | 0 |
| OpeningCutsceneRulesTests | 21 | 0 |
| PlayerSpawnerContractTests | 13 | 0 |

Kết quả XML cục bộ: `KLTN/Temp/OpeningCutscene.tests.xml`. Scene survey: `KLTN/Temp/OpeningCutscene.report.txt` ở thời điểm survey và bảng pose trong storyboard; report là status gần nhất của tool và sẽ bị thay thế bởi command tiếp theo. Ảnh preview: `KLTN/Temp/OpeningCutscene/`.

Roslyn cũng được dùng để kiểm tra C# với references Unity/Fusion đã cài. `rtk`, Unity MCP, CodeGraph/context7/gamecodex không có trong phiên này; sử dụng PowerShell/rg, source package local và Editor Tool thay thế. Dùng Editor Tool, không sửa tay scene YAML.

## Log Editor và file request

`OpeningCutsceneSceneSetup` dùng handle chỉ cho phép delete để đọc/xóa request trong cùng một scope; không cho reader/writer khác truy cập đồng thời. Khi producer còn giữ file, tool giữ nguyên request và thử lại mỗi 0.2 giây, không ném IOException ra Editor update. Chỉ dispatch sau khi đọc và xóa thành công; file rỗng được giữ để chờ producer ghi xong.

Đã kiểm tra trực tiếp bằng cách giữ khóa độc quyền file `Temp/OpeningCutscene.v2.request` trong 1.6 giây, sau đó nhả lệnh `tests`: tool tiếp tục chạy, không có Sharing violation mới và 126 test đều đạt. Khi gửi command từ bên ngoài, có thể ghi xong vào file tạm rồi rename sang request để publish nguyên nội dung.

`FPL|COMMIT_OBSERVER_ERROR|...observer boom` trong stack của `FND_NET_LIFECYCLE_CommittedObserverExceptionsAreIsolatedPerSubscriber` là log cố ý: test gắn một subscriber ném exception, khai báo `LogAssert.Expect` và kiểm tra subscriber tiếp theo vẫn được gọi. Test này đã Passed. Không sửa hoặc tắt báo lỗi observer của runtime để che log test. Console có thể giữ log từ các lần chạy trước; kết quả XML và dòng `[OpeningCutscene] EditMode tests: ...` là tổng kết lần chạy mới nhất.

## Cần kiểm chứng trong Play mode / nhiều máy

Chưa chạy host + client, build player hoặc test migration thực qua Photon/backend. Không xem EditMode rules tests là bằng chứng đồng bộ multiplayer thành công.

1. Chạy Bootstrap → Lobby → SciFi với host và 1–3 client, tài khoản khác nhau. Kiểm tra một match/player/monster lifecycle hiện hữu, cùng start tick/duration, input/tool/AI không hoạt động trong intro và timer còn đủ ngay khi FPS được release.
2. Hold Space/chọn Skip trên từng máy; một vote không release cả đội, vote trùng không tăng số, đủ vote mới cùng fade. Solo vote một lần. Thử người chưa vote disconnect, người đã vote disconnect và một người join trong intro.
3. Rút client vào lại lúc Playing và sau Completed: Playing phải seek, Completed không phát lại. Thử host migration giữa intro và trong fade: giữ state/timer/votes, không initialize intro lần hai. Kiểm tra lease/backend recovery vốn có.
4. Bỏ Timeline/reference trên host: không softlock. Bỏ Timeline/reference trên một client: host điều khiển fallback chung. Giữ client ở loading chưa ready: không vượt qua barrier để bắt đầu phim.
5. Kiểm tra camera/audio listener chỉ một bộ render local; HUD/cursor/input được trả đúng sau end, skip, disconnect, unload, disable và Result. Nghe cue/room tone, kiểm tra clipping và độ sáng ở đầu/cuối cả năm shot.
6. Chạy một trận Scene_A và Scene_B để xác nhận flow cũ; hoàn tất/thua SciFi để kiểm tra result submission/reward/AED telemetry.

Preview Editor từng gặp lỗi driver/DirectX 12 khi dùng Camera.Render lồng trong cùng update. Tool đã đổi sang URP SingleCameraRequest, mỗi update một request và AsyncGPUReadback; không đổi graphics API hoặc URP của project. Đã render và xem đủ năm ảnh mới. Shot hành lang được đặt lại từ bounds của Tunnel Floor và raycast đo hướng thoáng 14.22m; shot bộ tiếp nhận giảm fill để giữ chi tiết bề mặt. Chưa kiểm tra hình ảnh chuyển động, âm thanh hoặc end/skip handoff trong Play mode.
