# Opening cinematic — ECHO PROTOCOL

SciFi / Expedition. Thời lượng 30 giây, quay trực tiếp geometry và ánh sáng hiện có. Không dùng video, ảnh, nhân vật hay monster mới. Không thay đổi lighting/URP hoặc luật AED.

## Căn cứ nội dung và khác biệt tài liệu

- `docs/ECHO_PROTO.md`, §1.1: người chơi thuộc đội phản ứng ECHO, được cử tới khu vực phong tỏa sau một sự cố. Chưa có tài liệu xác nhận nguyên nhân sự cố, tên tổ chức, năm xảy ra hay danh tính người chỉ huy; cinematic không thêm các chi tiết đó.
- `docs/ECHO_PROTO.md`, §4, §7–8: Research Facility; thu thập/vận chuyển Energy Core, khôi phục hệ thống, phối hợp và thoát; có nguy cơ bị săn đuổi. Intro chỉ hé lộ môi trường và nhiệm vụ trước mắt, không giải thích toàn bộ quái vật/câu đố hoặc chỉ vị trí Core ngẫu nhiên.
- `docs/01_ECHO_PROTOCOL_Project_Scope_REVISED.md` và `docs/03_ECHO_PROTOCOL_Implementation_Spec_REVISED.md` xác định ECHO PROTO là nguồn gameplay chính. Baseline cũ có một Research Facility P0 và Underground Station P1. `docs/SRS.md` còn dùng investigator/fuse/access card; không dùng phiên bản này để viết thoại.
- `LobbyMapCatalog.cs` hiện có Expedition: `SciFi` (`M2-MAP-1`) và `Scene_A` (`EXPEDITION-SCENE-A`); Purge: `Scene_B` (`PURGE-SCENE-B`). Đây là phạm vi runtime rộng hơn baseline cũ. `FloodedMapMission` và `AlienHuntMission` là nguồn cơ chế hiện hành cho hai map mở rộng; không gán Research Facility lore hoặc cinematic SciFi cho chúng.
- SciFi runtime đã có Zone 2 relays/security và Zone 3 convoy/thoát. Intro không kể trước chi tiết các giai đoạn đó và không hứa chuỗi nhiệm vụ fuse/access card từ SRS cũ. Lời “tìm các Lõi Năng Lượng” tránh cố định số lượng khi cấu hình trận điều chỉnh mục tiêu.
- `PRODUCT.md` mô tả web administration, không bổ sung lore. `docs/02_ECHO_PROTOCOL_System_Architecture_REVISED.md`, `docs/API_SPEC.md`, `docs/M2_MATCH_STATE_NETWORKING.md` và `docs/NETWORKING_AUTHORITY_HANDOFF.md` được dùng cho ranh giới authority/lifecycle. Handoff cũ ghi host migration ngoài MVP; runtime hiện đã có reconciliation/snapshot, nên intro giữ nguyên state replicated khi migration, không khởi tạo lại.
- `docs/AUDIO_SETUP.md`: tái sử dụng GameAudioCatalog và room tone hiện hữu. Không có voice-over radio theo kịch bản này; lời chỉ huy thể hiện bằng phụ đề Việt/Anh. Không tạo giọng nói hoặc sự kiện gameplay giả.

| Map / mode hiện hành | Mục tiêu trong runtime | Giới hạn nội dung intro |
| --- | --- | --- |
| SciFi / Expedition | Core và bộ tiếp nhận; security/relay; convoy, nạp điện/nhiên liệu và thoát. | Chỉ nêu Core, khôi phục hệ thống, phối hợp và thoát; không tiết lộ mã/đường đi hay giai đoạn sau. |
| Scene_A / Expedition | FloodedMapMission: cấp điện hai trạm, security/relay, cấp điện và hộ tống đĩa bay tới bến, sơ tán; không tiếp nhiên liệu khi hộ tống. | Chưa có cinematic. Không suy diễn đây là Underground Station từ tài liệu cũ. |
| Scene_B / Purge | AlienHuntMission: dùng mồi nhử, thu giữ và thiêu hủy 9 alien qua furnace; hoàn tất mục tiêu để thắng. | Chưa có cinematic. Không tự thêm quan hệ cốt truyện với sự cố Research Facility. |

## Storyboard

| Shot | Hình ảnh và chuyển động | Phụ đề tiếng Việt | Audio | Transition | Nguồn |
| --- | --- | --- | --- | --- | --- |
| 01 / Establishing / 6s | Camera từ mốc AED resource entry nhìn vào phòng xuất phát đang hoạt động; dolly chậm tối đa 0.8m. | Đội phản ứng ECHO. Khu vực nghiên cứu đang bị phong tỏa. | Room tone từ audio runtime hiện hữu. | Fade từ đen 0.65s. | ECHO PROTO §1.1, §8.1. |
| 02 / Incident / 6s | Hành lang server nhìn vào shelving/thiết bị có sẵn. Không thêm xác người, cháy nổ hay dấu vết không có trong map. | Sau sự cố, nhiệm vụ của đội là khôi phục hệ thống và tìm đường thoát. | `security_terminal/terminal_boot`, âm lượng thấp, chỉ presentation. | Cắt camera; dolly chậm. | ECHO PROTO §1.1, §4. |
| 03 / Mission / 7s | Bộ tiếp nhận `NetworkSectorBox` thực tế; camera đặt theo NavMesh và nhìn vào bounds của model. Không spawn thêm Core. | Tìm các Lõi Năng Lượng và đưa chúng về bộ tiếp nhận. | `ui/objective_update`; room tone. | Cắt camera; dolly tới thiết bị. | ECHO PROTO §4.3–4.4. |
| 04 / Threat / 5s | Nhìn dọc corridor thật, giữ khoảng trống và ánh sáng hiện hữu. Không lộ toàn thân monster hoặc thay vị trí AI. | Không có. | Room tone, không jumpscare hoặc cue giả về vị trí monster. | Cắt camera; dolly chậm. | ECHO PROTO §4.3, §7; thể hiện không gian nguy hiểm mà không thêm sự kiện lore. |
| 05 / PlayerSpawn / 6s | Spawn P1 làm pose authoring; 1.8 giây cuối blend sang pose PlayerCamera của chính người xem, gồm offset spawn P2/P3/P4. | Phối hợp cùng đồng đội. Hoàn thành nhiệm vụ và thoát khỏi khu phong tỏa. | `ui/match_start`, mức thấp. | Fade đen 0.65s; restore camera/HUD/input; fade vào FPS 0.65s. | ECHO PROTO §1.1, §3–4. |

## Tọa độ từ Unity Editor

Nguồn: khảo sát live qua Editor Tool trên đúng project `KLTN`, Edit mode. `SciFi_Sample_Base_Disabled` là template bị tắt và không được chọn làm nguồn. Tool chọn vị trí bằng NavMesh gần reference thật, cộng eye height 1.65m, kiểm tra sphere và giới hạn dolly bằng sphere cast. Bảng dưới được xuất lại từ các shot anchors khi bake Timeline; không phải tọa độ đoán từ tên phòng.

`CinematicCamera/CinematicFill` dùng Light của PlayerNetwork và tham số beam runtime hiện hành, giảm intensity còn 50%. Shot 03 giảm thêm bằng `fillMultiplier = 0.35` để thiết bị trắng không chói, shot 04 dùng 0.5. Đèn local chỉ bật trong cinematic và được tắt khi cleanup; không sửa RenderSettings, lighting trong map, prefab đèn pin hoặc URP asset. Đây là fill cho camera quan sát trong môi trường vốn cần đèn pin để nhìn rõ. Shot 04 được đặt từ tâm bounds sàn Tunnel Floor và chọn hướng thoáng bằng raycast, tránh khung cửa che camera.

<!-- SHOT_POSES_START -->

| Shot | Reference | Start position / Euler | End position / Euler | Seconds | Audio key |
| --- | --- | --- | --- | --- | --- |
| Shot01_Establishing | `GameObject/Zone01_ResearchStorage/AED_Z1_RESOURCE_ENTRY` | `(36.378, 2.733, -81.000)` / `(0.431, 358.647, 0.000)` | `(36.359, 2.733, -80.200)` / `(0.452, 358.647, 0.000)` | 6 | `` |
| Shot02_Incident | `GameObject/Zone01_ResearchStorage/04_Server_Room_C2_EMPTY/route/Corridor Wide` | `(63.590, 2.733, -135.430)` / `(0.538, 13.635, 0.000)` | `(63.779, 2.733, -134.653)` / `(0.570, 13.635, 0.000)` | 6 | `security_terminal/terminal_boot` |
| Shot03_Mission | `NetworkSectorBox` | `(36.390, 2.733, -172.110)` / `(5.826, 0.000, 0.000)` | `(36.390, 2.733, -171.310)` / `(7.535, 0.000, 0.000)` | 7 | `ui/objective_update` |
| Shot04_Threat | `GameObject/Zone01_ResearchStorage/03_Central_Junction/route/Corridor Passthrough (1)/Tunnel Floor` | `(-5.500, 2.733, -165.600)` / `(0.000, 270.000, 0.000)` | `(-6.300, 2.733, -165.600)` / `(0.000, 270.000, 0.000)` | 5 | `` |
| Shot05_PlayerSpawn | `GameObject/SpawnPoints/PlayerStart_EMPTY` | `(33.670, 2.842, -70.060)` / `(358.855, 166.097, 0.000)` | `(33.862, 2.842, -70.837)` / `(358.767, 166.097, 0.000)` | 6 | `ui/match_start` |

<!-- SHOT_POSES_END -->

## Đồng bộ

Lobby → backend authorization → Fusion scene loading → player spawn → barrier tất cả ready → host chọn start tick → cinematic → gameplay release. Giữ nguyên enum NetworkMatchPhase/Status và MatchLoadingBarrier.

State intro nằm trên NetworkMatchState hiện hữu: NotStarted / Playing / Completed, duration/start tick, replicated end timer và bốn slot vote. Camera không có NetworkObject. Host kiểm tra nguồn RPC và membership; vote trùng bị bỏ. Khi toàn bộ electorate đang kết nối vote, host đặt chung cửa sổ fade 0.65s rồi release. Disconnect thay đổi electorate; duration/end timer giới hạn playback dù client không phát xong. Client lỗi asset chỉ gửi báo cáo, không tự release.

SciFi có configuration hợp lệ thì MatchTimer để None trong loading/intro và chỉ tạo đầy đủ duration tại gameplay release. Các map không có configuration giữ flow timer hiện tại. Snapshot giữ start tick, Completed và vote khi reconnect/migration; client đang vào giữa seek tới thời gian hiện hành. Không reset intro trong nhánh host migration Spawned.

Các gate chặn network movement, interactor/tool validation, objective mutations, monster simulation/damage và gameplay observations AED trong intro. Backend binding, pre-match config, lease, network transport và result outbox vẫn tiếp tục. Với map có cinematic, gameplay telemetry start/first-phase evidence được đợi tới release để duration và AED không tính 30 giây xem phim; các spawn facts ban đầu dùng hàng đợi sẵn có. Không đổi schema, occurrence key hoặc event order.

## Chỉnh trong Unity Inspector

1. Mở `Assets/Scenes/SciFi.unity`, chọn `OpeningCutscene` và mở Window → Sequencing → Timeline để scrub camera Animation Track. Chọn CinematicCamera để xem Camera Preview; camera mặc định tắt để không chiếm gameplay camera. Khi kiểm tra flow thật, chạy từ Bootstrap → Lobby → Start SciFi, vì cinematic phụ thuộc NetworkMatchState và readiness barrier.
2. Chọn `Assets/_Project/Cinematics/SciFiOpeningDefinition.asset`: sửa subtitle Việt/Anh, duration từng shot, audio key, fillMultiplier và hold-to-skip. Audio key phải có trong `GameAudioCatalog`.
3. Trong hierarchy `OpeningCutscene/ShotAnchors`, di chuyển/xoay từng shot (Start) và child `End`. Frustum gizmo hỗ trợ bố cục; `source` giữ reference môi trường đã khảo sát.
4. Chọn **ECHO Protocol → Cinematics → Rebuild Timeline From Anchors** để bake pose/duration vào các `.anim` và Timeline. Lưu scene. Bake sẽ thay camera track từ anchors; chỉnh keyframe Timeline trực tiếp chỉ nên làm sau lần bake cuối, hoặc cập nhật lại anchors trước lần bake tiếp theo.
5. **Render SciFi Shot Previews** xuất ảnh vào `KLTN/Temp/OpeningCutscene/`. **Run Opening And Loading Tests** chạy bộ kiểm tra liên quan ngay trong Editor.
6. Scene_A/Scene_B có thể thêm cùng controller, anchors và một definition/timeline riêng. Hiện chưa cài cinematic trên hai map này. Bake dùng Timeline đã gán trong definition; nếu chưa có, tạo asset theo sceneName trong thư mục riêng. Không dùng lại SciFi Timeline khi tạo definition cho map mới.

## Kiểm chứng và bước thủ công còn lại

Kết quả compile/test thực tế và danh sách file nằm trong `docs/OPENING_CUTSCENE_IMPLEMENTATION.md`. EditMode và render preview không chứng minh multiplayer chạy đúng. Cần chạy host + 1–3 client để kiểm tra start tick/seek, đủ thời gian nhiệm vụ, skip unanimity, disconnect, missing client asset, migration giữa intro, reconnect sau Completed và teardown về Lobby/Result. Nghe âm thanh và kiểm tra camera clipping tại cả đầu/cuối từng shot trong Play mode.
