# Opening cutscene Thanh Trừng

Cutscene chạy trong `KLTN/Assets/Scenes/Scene_B.unity`, dài 28 giây. `LobbyMapCatalog` định danh map là `PURGE-SCENE-B`. Bối cảnh lấy từ cảnh ngập hiện có trong Unity. Lời radio Việt và Anh được viết dựa trên cơ chế trong `AlienHuntMission` và bối cảnh đội ECHO trong `SRS.md`. Từng câu xuất hiện dưới dạng phụ đề, kèm cue âm thanh trong project. Có thể gán bản thu thoại sau qua `Audio Override`.

| Thời gian | Cảnh | Hình ảnh và lời radio |
| --- | --- | --- |
| 00–06s | Contaminated Zone | Góc cao hướng ra vùng ngập và cầu gỗ. “Chỉ huy ECHO gọi đội Thanh Trừng. Đã tới khu vực ngập nước.” |
| 06–11s | Hostile Detection | Alien non thật trong nest của `Scene_B`. “Phát hiện Alien non. Dùng thịt làm mồi nhử để tiếp cận và thu giữ.” |
| 11–18s | Purge Protocol | Lò thật, can nhiên liệu và mồi thịt dựng cạnh lò từ mesh/material của vật phẩm thật. “Nạp nhiên liệu vào lò, rồi đưa Alien đã bắt tới. Mục tiêu: thiêu hủy 9 Alien.” |
| 18–23s | Unknown Threat | Lizard hiện thoáng qua cùng tiếng gầm từ prefab Boss. “Có chuyển động lớn trong vùng ngập. Giữ đội hình và quan sát.” |
| 23–28s | Mission Start | Góc trống ở lối vào từ spawn. “Đội Thanh Trừng, triển khai.” Fade đen, đổi sang FPS, rồi mở hình. |

## Nguồn hình ảnh trong Scene_B

Điểm xuất phát chính `SpawnPoints/PlayerStart_EMPTY` ở `(618.700, 18.060, 379.200)`. Ba điểm đồng đội nằm quanh cùng khu bến. Sân, cầu, thuyền, cây và mặt nước là geometry của map. Survey trong Unity ghi nhận `GameObject/WaterPlane 2`, các emitter Water, Wind, LeafRustle, DeepRattle và 16 đèn active.

| Nguồn | Vị trí đã khảo sát |
| --- | --- |
| `AlienHunt_Gameplay/AlienNests/AlienNest_01/AlienYoung_01` | `(458.148, 18.152, 426.845)` |
| `Furnace/Furnace_Export` | `(638.690, 16.891, 510.320)` |
| `Furnace/Furnace_Export (1)` | `(512.740, 16.794, 529.440)` |
| `Furnace/Furnace_Export (2)` | `(467.890, 16.811, 422.310)` |
| `SpawnPoints/MonsterSpawn_Stalker_EMPTY` | `(469.459, 16.762, 427.506)` |

Alien dùng renderer và animator có sẵn trước khi host khởi tạo/move các nest cho trận. Can và mồi ở shot 03, Lizard ở shot 04 chỉ có visual, được bật theo từng shot và tắt khi trả camera. Mesh Lizard được bake từ `Assets/Prefabs/StalkerNetwork_Lizard.prefab`. Scene dùng ánh sáng gốc, cộng đèn fill riêng trên camera cho cận cảnh.

<!-- SHOT_POSES_START -->

| Shot | Reference | Start position / Euler | End position / Euler | Seconds | Audio key |
| --- | --- | --- | --- | --- | --- |
| Shot01_ContaminatedZone | `GameObject/WaterPlane 2` | `(598.700, 48.060, 396.200)` / `(23.804, 162.897, 0.000)` | `(597.171, 46.060, 395.730)` / `(22.372, 161.550, 0.000)` | 6 | `horror_ambience/tension_drone_loop` |
| Shot02_HostileDetection | `AlienHunt_Gameplay/AlienNests/AlienNest_01/AlienYoung_01` | `(460.178, 19.265, 428.973)` / `(10.125, 225.000, 0.000)` | `(460.580, 19.265, 428.571)` / `(9.926, 236.484, 0.000)` | 5 | `purge_alien/idle_01` |
| Shot03_PurgeProtocol | `Furnace/Furnace_Export (2)` | `(474.688, 19.119, 420.150)` / `(10.784, 290.000, 0.000)` | `(475.127, 19.119, 421.355)` / `(10.572, 278.492, 0.000)` | 7 | `ui/objective_update` |
| Shot04_UnknownThreat | `SpawnPoints/MonsterSpawn_Stalker_EMPTY` | `(467.359, 18.662, 433.506)` / `(358.044, 193.721, 0.000)` | `(466.152, 18.662, 433.506)` / `(357.997, 174.137, 0.000)` | 5 | `horror_ambience/distant_monster_sound` |
| Shot05_MissionStart | `SpawnPoints/PlayerStart_EMPTY` | `(618.700, 21.060, 381.200)` / `(7.407, 180.000, 0.000)` | `(617.100, 21.060, 381.200)` / `(7.315, 170.910, 0.000)` | 5 | `ui/match_start` |

<!-- SHOT_POSES_END -->

## Luồng multiplayer

Host đợi `MatchLoadingCoordinator` release readiness và mọi player còn kết nối có `GameplaySpawnReady`. `NetworkMatchState` thống nhất tick bắt đầu và timer kết thúc. Mỗi máy dùng tick đó để seek cùng Timeline. Giữ Space hoặc bấm **Skip** để gửi phiếu. Các player còn kết nối phải đồng thuận. Replica thiếu asset có thể báo authority dùng fallback đồng bộ. Timer phía host kết thúc cinematic ngay cả khi callback Timeline không chạy.

Trong opening, simulation của trận chưa qua `TickOpeningAuthoritative`. `TickAlienHunt`, `TryHuntAction`, `RecordHuntBurn` và `TickPurgeDirector` kiểm tra authority và gameplay release. Movement, interaction, flashlight, Stalker và Fishcat dùng guard chung. Hunt chưa Ready nên phần trình bày Alien và HUD chưa đếm hint. Sau release, host khởi tạo Hunt và bắt đầu đủ thời lượng MatchTimer. Kết quả và thưởng tiếp tục dùng luồng Result hiện có.

Purge dùng mission đã dựng và fixed balance của mình. Khởi tạo trận bỏ qua registry scenario AED dành cho SciFi. Điều này khắc phục `AED_CONTENT_CONTRACT_MISSING` được phát hiện khi test Scene_B.

Với Purge, controller ẩn renderers của cả player local và proxy, kể cả tay FPS, renderer của đồ gắn trên nhân vật, đèn và canvas gắn trên player. Cờ render chỉ tác động trên máy đang xem. Controller lưu trạng thái để khôi phục khi kết thúc, skip, disable hoặc scene đổi. Góc cuối dừng ở khu spawn cho tới lúc đổi camera dưới fade.

## Chỉnh trong Unity

1. Mở `Scene_B`, chọn `OpeningCutscene_Purge > ShotAnchors`. Transform cha là đầu cảnh, `End` là cuối cảnh. Di chuyển và xoay bằng Scene view.
2. Trong `PurgeOpeningDefinition.asset`, chỉnh `Duration`, `Subtitle`, `Subtitle English`, `Audio Key` hoặc `Audio Override` của từng shot. `Field Of View` áp dụng khi bake. Giữ `Hide Players` bật cho Purge.
3. Chọn root hoặc anchor rồi chạy **ECHO Protocol → Cinematics → Rebuild Timeline From Anchors**. Lưu `Scene_B`. Camera track được tạo lại từ anchors. Nếu chỉnh keyframe trực tiếp trong Timeline, lần bake tiếp theo sẽ dùng lại anchors.
4. Dùng **ECHO Protocol → Cinematics → Render Purge Shot Previews** trong Edit mode để lấy ảnh từng shot tại `KLTN/Temp/PurgeOpening/`. Có thể scrub camera trong Timeline khi chọn `PlayableDirector`. Play scene offline thông thường vẫn dùng sandbox gameplay của map.

## File triển khai

- `KLTN/Assets/Scenes/Scene_B.unity` chứa `OpeningCutscene_Purge`, camera, ánh sáng, UI, anchors và visual props.
- `KLTN/Assets/_Project/Cinematics/Scene_B/` chứa definition, `PurgeOpeningTimeline.playable`, 5 clip camera và mesh Lizard.
- `KLTN/Assets/Editor/PurgeOpeningSceneSetup.cs` khảo sát và tạo setup qua Unity Editor.
- `KLTN/Assets/Editor/OpeningCutsceneSceneSetup.cs` dùng lại bake, render preview và test runner.
- `KLTN/Assets/Scripts/Cinematics/OpeningCutsceneController.cs` và `OpeningCutsceneDefinition.cs` bổ sung ẩn model player và điều khiển visual/cue theo shot.
- `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.AlienHunt.cs` và `NetworkMatchState.Purge.cs` chặn progression và hành động trước release. `NetworkMatchState.cs` giữ Purge ngoài registry scenario AED của Expedition.
- `KLTN/Assets/Tests/EditMode/Controls/Editor/OpeningCutsceneLifecycleTests.cs` kiểm tra binding và Timeline hai map. `KLTN/Assets/Tests/EditMode/MatchFlow/Editor/PurgeDirectorTests.cs` mở rộng PlayMode fixture kiểm tra opening và gameplay release.
- `KLTN/Assets/Tests/EditMode/MatchFlow/Editor/PurgeGameplayFixTests.cs` dùng chính scene của hai mode để test stamina trong Editor có scene Untitled.

## Kiểm chứng

Unity compile thành công. **182 EditMode tests và 2 PlayMode tests đạt**. PlayMode chạy chu trình dụ–bắt–thiêu Alien và Purge Director trên Fusion Single. Bài Director kiểm tra đợi spawn readiness, renderer player bị ẩn trong Play mode, camera FPS được trả, các yêu cầu tương tác và callback burn không đổi nhiệm vụ, Boss giữ vị trí, trạng thái lò và đồng hồ giữ nguyên, rồi gameplay được mở sau skip và xử lý đến mục tiêu thứ 9. Báo cáo Unity ở `KLTN/Temp/OpeningCutscene.play-tests.xml` và `KLTN/Temp/OpeningCutscene.tests.xml`.

Test Fusion Single kiểm tra state authority local. EditMode bao phủ quy tắc phiếu của client và roster thay đổi. Phiên Photon host/client thật và backend nhận thưởng chưa được chạy trong lượt này.

Đã kiểm tra 5 ảnh render từ camera thật trong `KLTN/Temp/PurgeOpening/`. Khung hình có bến ngập, Alien, lò và vật phẩm, Lizard, rồi lối vào ở spawn. Unity Editor có ghi cảnh báo fence DirectX 12 trong lúc preview. Lượt render cuối đã ghi đủ 5 ảnh.
