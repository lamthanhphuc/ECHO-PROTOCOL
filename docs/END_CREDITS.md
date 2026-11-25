# Cinematic rolling credits — AfterCredit

Triển khai trên scene hiện có `KLTN/Assets/Scenes/AfterCredit.unity`, theo yêu cầu cuối của người dùng. Không tạo scene Credits.unity thứ hai. Scene đã được thêm vào scene list, giữ nguyên thứ tự các scene cũ để không đổi scene indices của Fusion.

## Dữ liệu thành viên và nguồn

Tên được người dùng xác nhận ngày 26/11/2026. Vai trò và phạm vi đóng góp lấy theo mapping A–D trong `docs/04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.md`, mục `01_MODULE_OWNERSHIP`; không dùng phân công cũ khác trong SRS để tự gán lại tên. Bản revised ghi các owner là đề xuất; mapping danh tính A–D được người dùng xác nhận trong yêu cầu credits này. Chưa kiểm toán lịch sử commit để xác nhận từng thay đổi cá nhân.

| Mapping | Tên hiển thị | Vai trò / phạm vi trong credits |
| --- | --- | --- |
| A | Đặng Huy Hoàng | Gameplay, UI, level design; player systems, mission flow, maps và presentation. |
| B | Hứa Thành Đạt | Multiplayer/networking; lobby, host authority, replication và scene synchronization. |
| C | Lâm Thanh Phúc | Monster AI, AED, telemetry; player/team modeling, adaptation và research. |
| D | Nguyễn Nhật Lệ Thuyên | Backend, database, economy; authentication, profile, API, reward, shop và payment sandbox. |
| Giảng viên hướng dẫn | Đào Quốc Tuấn | Academic supervisor. Không thêm học hàm/học vị vì người dùng không cung cấp. |

ScriptableObject: `KLTN/Assets/Resources/UI/Credits/EchoCreditsData.asset`. Mỗi section có role, names, contribution và evidence. Evidence chỉ dùng trong Inspector, không hiển thị trong giao diện. Chữ ký tên giữ nguyên dấu tiếng Việt, không uppercase tên người hoặc xử lý qua dữ liệu network.

## Giao diện

- Nền đen, logo chính thức đang có tại `Resources/UI/Loading/ReferenceEchoLogo`, tên trắng ngà, role viết hoa màu xám.
- TextMeshPro; tên lớn hơn role; VerticalLayoutGroup và ContentSizeFitter dựng từng section theo dữ liệu. Không chèn placeholder hoặc tên suy đoán.
- RectMask2D cắt nội dung vào viewport. CreditsEdgeFade vẽ hai dải đen gradient để chữ và logo mờ dần tại hai cạnh.
- CanvasScaler dùng 1920×1080 design units và ScaleWithScreenSize; tốc độ cuộn theo design units, không theo số pixel vật lý của màn hình.
- Tự cuộn từ dưới lên, dừng khi THANK YOU FOR PLAYING tới giữa viewport, giữ ba giây rồi fade out.
- Giữ Space liên tục hai giây để skip; nhả phím sẽ reset progress. Thanh progress nằm cạnh hướng dẫn ở góc dưới bên phải. Không dùng Time.timeScale hoặc thời gian Fusion.
- AudioSource 2D local dùng `horror_ambience/tension_drone_loop` hiện có, volume mặc định 0.14, route qua Music mixer/settings. Fade âm thanh cùng nội dung. Các nguồn âm thanh nền khác được mute local rồi khôi phục; không mute Photon Voice.

## Flow Result → AfterCredit → Lobby

1. Result vẫn đọc snapshot terminal và poll `ResultRewardBridge` theo lifecycle hiện có. Host outbox vẫn là bên duy nhất gửi kết quả; credits không gửi result hoặc tính/phát thưởng.
2. Chỉ khi `HasConfirmedReward` thành công cho đúng match, Result mới bật credits. Nút Return đổi thành VIEW CREDITS/XEM CREDITS. Mặc định sau tám giây đọc Result (tính từ cả intro Result hoàn tất và reward confirmed), credits tự mở; nút cho phép mở ngay.
3. Nếu reward còn pending, unavailable, unauthenticated hoặc bị reject, không mở credits. Đường Return/Lobby và F7/F8 recovery hiện có vẫn được giữ để không làm người chơi kẹt ở Result. Retry Mission vẫn dùng flow Lobby/ready/start cũ và không bị ép xem credits.
4. AfterCredit được load **local additive**, không gọi runner.LoadScene cho scene credits. UI root dùng DontDestroyOnLoad để mỗi client tiếp tục cuộn nếu host đã đưa scene network Result về Lobby.
5. Mỗi máy có playback clock, skip progress, audio và completion task riêng. Client skip sớm không gửi skip RPC hoặc hoàn thành credits thay người khác. Không tạo NetworkRunner hoặc NetworkObject, không spawn gameplay player, không gửi transform chữ lên Fusion.
6. Khi local credits xong, dùng lại navigation của Result và lấy runner hiện tại. Host chuyển network scene về Lobby bằng flow cũ; client còn ở Result dùng đường leave/Shutdown cũ. Nếu Lobby đã sẵn sàng trên máy đó, chỉ gỡ credits và giữ trạng thái Lobby hiện tại, không shutdown/reload thêm.
7. Sau handoff, root credits được hủy và scene additive rỗng được unload nếu còn tồn tại. Audio mute states, AudioListener fallback và cursor/input lock được khôi phục. Lock được lấy lại khi scene-unload driver của project nhả modal lock trong một lần chuyển scene của host.

Chạy trực tiếp AfterCredit trong Editor sẽ phát credits như một scene UI độc lập. Khi hoàn tất, nó dùng đường Lobby/authentication sẵn có; nếu chưa có session/auth, đi qua Bootstrap như Result preview, không tự đăng nhập hoặc tạo phòng.

## Chỉnh trong Inspector

1. Mở AfterCredit, chọn `CinematicEndCredits` → `CreditsController`.
2. Chỉnh Credits Data, Name Font, Role Font, Font Size, Section Spacing, Scroll Speed, Start Delay, Fade Duration, Closing Hold, Hold To Skip Seconds, Background Music và Music Volume. Skip mặc định đúng hai giây.
3. Chọn `EchoCreditsData.asset` để sửa tên, role, mô tả đóng góp, thứ tự section, logo và câu kết. Nội dung được dựng lại khi scene bắt đầu; thay đổi data không cần sửa code hoặc gán lại references.
4. Các UI references đã được gán trong scene. Không thêm NetworkObject/Runner cho credits.
5. Trong Result, component CinematicResultSceneController có `autoPlayCredits` và `creditsResultReadSeconds` để đổi hành vi tự mở; xác nhận reward vẫn là điều kiện bắt buộc.
6. Menu **ECHO Protocol → Credits → Build AfterCredit Scene** tạo setup nếu thiếu; nếu đã có thì giữ các chỉnh sửa Inspector và bảo đảm scene list. **Run Credits And Result Tests** chạy EditMode tests. **Run Local Result Credits Lobby PlayMode Test** chạy kiểm tra runtime local với reward boundary và Lobby handoff được mô phỏng. **Render 1080p 1440p And 4K Previews** xuất ảnh vào Temp/Credits, không sửa scene đang làm việc; ba ảnh hiển thị progress skip lần lượt 0%, 50%, 100%.

## File triển khai

Các path Unity tính từ KLTN:

| File | Vai trò |
| --- | --- |
| `Assets/Scripts/UI/Credits/CreditsController.cs` | Local scrolling UI, input, music, completion và cleanup. |
| `Assets/Scripts/UI/Credits/CreditsData.cs` | ScriptableObject / credit sections. |
| `Assets/Scripts/UI/Credits/CreditsRollPlayback.cs` | Clock/state machine local và reward gate thuần dữ liệu. |
| `Assets/Scripts/UI/Credits/CreditsFlow.cs` | Local additive scene và handoff sau completion. |
| `Assets/Scripts/UI/Credits/CreditsEdgeFade.cs` | Gradient ở cạnh viewport. |
| `Assets/Editor/CreditsSceneSetup.cs` | Native scene authoring, build scene lists, preview và test tools. |
| `Assets/Tests/EditMode/Controls/Editor/CreditsTests.cs` | Reward gate, delay/scroll/skip/end, independence, handoff ordering, data/scene/layout/cleanup. |
| `Assets/Tests/EditMode/Controls/Editor/CreditsLocalFlowSmoke.cs` | PlayMode fixture dùng Result và AfterCredit thật, mô phỏng reward boundary và Lobby handoff. |
| `Assets/Tests/PlayMode/Networking/CreditsFlowPlayModeTests.cs` | Entry point PlayMode theo reflection pattern hiện có của project. |
| `Assets/Resources/UI/Credits/EchoCreditsData.asset` | Tên/role được xác nhận và nguồn. |
| `Assets/Scenes/AfterCredit.unity` | Scene hiện có được bổ sung giao diện và references. |
| `Assets/Scripts/UI/Result/CinematicResultSceneController.cs` | Verified reward gate, auto/manual credits và dùng chung navigation cũ. |
| `Assets/Scripts/Core/GameConstants.cs` | SceneCredits = AfterCredit. |
| `ProjectSettings/EditorBuildSettings.asset` | Thêm AfterCredit, không đổi scene indices cũ. |

Unity sinh các `.meta` tương ứng. Không sửa reward API, outbox serialization, backend, gameplay enums, package hoặc URP settings. Không commit/push. Unity skill được áp dụng; Unity MCP/CodeGraph không có trong phiên nên dùng Editor Tool và rg. Tool dùng reader request đã có xử lý file-sharing/retry, không đọc file đang được ghi.

## Kiểm chứng

Unity đã biên dịch runtime và Editor assemblies. **56 EditMode tests passed, 0 failed, 0 skipped**, gồm 25 Credits tests, 15 GameLanguage tests, 10 MatchResultOutbox serialization tests và 6 request-file sharing tests. Kết quả lần chạy cuối được ghi trong `KLTN/Temp/Credits.tests.xml`; log các test thất bại nếu có: `KLTN/Temp/Credits.test-failures.txt`.

**1 PlayMode local flow test passed** (3.82 giây): load Result thật, mô phỏng snapshot/reward boundary đã xác nhận, gọi nút Return, kiểm tra AfterCredit load local additive, root DontDestroyOnLoad, chữ thực sự di chuyển và AudioSource 2D đang phát. Test mô phỏng Lobby bằng scene local rỗng, unload Result khi credits còn chạy, xác nhận credits sống tiếp đến completion, rồi dọn ResultSession, scene additive, UI và input lock; số NetworkRunner không tăng. XML: `KLTN/Temp/Credits.flow-tests.xml`. Không thay reward API hoặc xác thực tài khoản để chạy test.

Preview dùng bản sao scene trong PreviewScene, Canvas world-space cho camera URP orthographic; scene game vẫn dùng ScreenSpaceOverlay. Đã quan sát bố cục và tên tiếng Việt ở 1920×1080, 2560×1440, 3840×2160. Ảnh và viewport metrics: `KLTN/Temp/Credits/`. Đây là ảnh tĩnh để kiểm tra typography/mask/edge fade, không phải video chứng minh tốc độ cuộn. Filled Image cho progress đã được gán sprite để Unity thực sự tôn trọng fillAmount.

EditMode kiểm tra thứ tự Result reward gate → local completion → navigation bằng actual component/data và task completion được điều khiển trong test. PlayMode dùng Lobby fixture để kiểm tra local handoff. Chưa chạy match thật qua Photon/backend, hai client xác thực thật, host disconnect/migration trong credits hoặc nghe nhạc thực tế.

Khi test Play mode, hoàn tất một trận và xác nhận reward, xem tự mở sau read delay; giữ Space đủ hai giây trên một client, kiểm tra client khác tiếp tục; host về Lobby trước, kiểm tra root credits trên client không bị mất và client vào đúng Lobby sau khi kết thúc. Kiểm tra thêm pending/rejected reward, Retry Mission, scene direct preview và thay đổi độ phân giải.

Tài liệu API dùng khi triển khai Editor tooling: [Unity BuildProfile và scene overrides](https://docs.unity3d.com/ja/6000.0/ScriptReference/Build.Profile.BuildProfile-overrideGlobalScenes.html), [Camera.scene cho preview scene](https://docs.unity3d.com/kr/6000.0/ScriptReference/Camera-scene.html).
