# Network Lobby UI — setup và kiểm thử

## Cập nhật giao diện Industrial Terminal

Với Canvas đã dựng: mở Lobby ở Edit mode, chọn **ECHO PROTOCOL > Lobby > Restyle Existing Network Terminal**, sau đó Ctrl+S. Không cần xóa Canvas hoặc gán lại networking reference. Có thể Ctrl+Z để hoàn tác; chạy lại menu không tạo trùng các chi tiết trang trí. Menu Build cũng áp dụng style mới cho Canvas tạo lần đầu.

Style nằm trong `Assets/Editor/NetworkLobbyUIStyle.cs` (partial của builder):

- Top Left, x=45, y=35, rộng 460, cao 1010 tại 1920×1080; khoảng dưới 35. CanvasScaler giữ tỷ lệ khi đổi độ phân giải.
- Image chính #080A0DEB và CanvasGroup. Background cũ được tắt để không chồng hai lớp nền.
- Viền mảnh, góc đỏ, rivet hình học; header 32 px, subtitle 11 px, FacilityInfo 10 px và line đỏ 2 px.
- Input 48 px, nền #0D1113, viền #596166; focus đổi viền #9DA5AA. Hai nút chính 52 px, cách nhau 12 px, có icon hình học.
- StatusText tách NetworkMessage. Dot đỏ #D51D27, vàng #C99C54 khi connecting, xanh #69A886 khi online. Offline/connecting nhấp nháy chu kỳ 0.8 giây trong Play mode, online sáng ổn định.
- MemberList nền #050708 alpha 70%, bracket mờ và EmptyState căn giữa; danh sách vẫn scroll khi dài.
- Nút bị disable có CanvasGroup alpha 35%. Client vẫn thấy Start Match nhưng không thể bấm; quyền start giữ nguyên trong LobbyManager.

Thiết kế này dùng UI native, không thêm PNG noise/scratches hoặc ảnh viền sliced. Icon và viền vẫn sắc nét khi CanvasScaler đổi tỷ lệ. `NetworkMessage` và `EmptyState` được menu tự gán vào controller.

Kiểm chứng redesign: runtime và hai phần builder đã biên dịch C# với DLL Unity hiện có. Chưa kiểm tra trực tiếp hình ảnh hoặc Play mode. Sau khi chạy menu, kiểm tra focus input, nhấp nháy dot, trạng thái nút, hai lần chạy restyle không sinh object trùng và Undo trước khi lưu.

## Phạm vi và mã nguồn đầy đủ

- [NetworkLobbyUI.cs](../KLTN/Assets/Scripts/UI/NetworkLobbyUI.cs): toàn bộ controller Canvas/TMP; nhận input, khóa nút khi bận, gọi networking hiện có, hiển thị trạng thái và thành viên.
- [NetworkLobbyUIBuilder.cs](../KLTN/Assets/Editor/NetworkLobbyUIBuilder.cs): toàn bộ công cụ Editor tạo hierarchy và gán reference, có Undo.
- [LobbyPlayerState.cs](../KLTN/Assets/_Project/Scripts/Networking/Player/LobbyPlayerState.cs): thêm OperatorName, chuẩn hóa tên tối đa 32 ký tự và RPC kiểm tra quyền sở hữu.
- [LobbyManager.cs](../KLTN/Assets/_Project/Scripts/Networking/Session/LobbyManager.cs): giữ tên local, gửi tên một lần cho mỗi player object khi object sẵn sàng, đưa tên vào snapshot.
- [RoomInfoViewModel.cs](../KLTN/Assets/_Project/Scripts/Networking/Session/RoomInfoViewModel.cs): hiển thị OperatorName, fallback về Player {ActorId}.
- Hai file `.meta` mới đi kèm các script mới; tài liệu này là file mới.

NetworkBootstrap và NetworkTestPanel không sửa. Host/Join tiếp tục gọi CreateRoomAsync/JoinRoomAsync; StartGame, xác thực backend, Runner, scene loading và shutdown vẫn thuộc networking hiện có. Ready/Start tiếp tục đi qua LobbyManager. Operator ID chỉ là tên hiển thị, không thay thế BackendUserId/JWT.

## Setup trong Unity

1. Mở đúng project `KLTN`, Unity 6000.5.8f1. Chờ import và compile, kiểm tra Console không có lỗi C# hoặc Fusion Weaver.
2. Mở `Assets/Scenes/Lobby.unity`, ở Edit mode. Giữ các thay đổi scene hiện có; công cụ không tự mở scene khác hoặc tự lưu scene.
3. Chọn **ECHO PROTOCOL > Lobby > Build Network Terminal**.
4. Công cụ tạo Canvas riêng, toàn bộ UI và reference. Canvas dùng Screen Space Overlay, Scale With Screen Size, reference 1920×1080, match 0.5. Terminal ở góc trái, offset (32,32), kích thước 460×1016.
5. Công cụ tạo hoặc bổ sung EventSystem với InputSystemUIInputModule. Nếu project thiếu font TMP mặc định, import TMP Essential Resources và đặt default font trong TMP Settings rồi chạy lại menu.
6. Công cụ chỉ disable component NetworkTestPanel trong Lobby, giữ nguyên GameObject chứa nó và mọi con (bao gồm warehouse). Không xóa debug script. Có thể Undo toàn bộ thao tác hoặc tắt Canvas mới và bật lại component debug để so sánh.
7. Lưu Lobby bằng Ctrl+S. Công cụ từ chối tạo thêm nếu đã có NetworkLobbyUI trong scene, tránh UI trùng.
8. Test từ scene Bootstrap qua luồng login/menu hiện có. Không thêm NetworkBootstrap mới vào Lobby: các service đang sống qua scene bằng DontDestroyOnLoad. Play trực tiếp Lobby thiếu service sẽ hiển thị lỗi khi bấm Host/Join.
9. Không thêm handler thủ công trong Button On Click: controller đăng ký và hủy đăng ký listener ở OnEnable/OnDisable. Giữ On Click trống để tránh gọi trùng.

## Hierarchy được tạo

```text
NetworkLobbyCanvas
└── NetworkTerminal [NetworkLobbyUI]
    ├── Background
    ├── Border (Top, Bottom, Left, Right)
    ├── Header (Title, Subtitle, Divider)
    ├── OperatorSection
    │   ├── Label
    │   └── PlayerNameInput [TMP_InputField]
    │       └── TextArea (Text, Placeholder)
    ├── SessionSection
    │   ├── Label
    │   └── SessionInput [TMP_InputField]
    │       └── TextArea (Text, Placeholder)
    ├── ActionButtons (HostButton, JoinButton)
    ├── StatusSection (StatusIndicator, StatusText, MemberCount)
    ├── MemberDivider
    ├── MemberList [ScrollRect]
    │   └── Viewport [RectMask2D]
    │       └── Content [TMP_Text, ContentSizeFitter]
    └── LobbyControls (ReadyButton, StartButton, LeaveButton)
EventSystem [EventSystem, InputSystemUIInputModule]
```

Các Button có con Label (TMP). Background #080A0D alpha 0.9; text #D8D8D8; secondary #7E8589; offline/error #8F1D1D; online #659A7A. Button normal/hover/pressed lần lượt #171A1D / #292D31 / #650F14. Không dùng ảnh hoặc shader mới.

## Reference Inspector (menu đã gán sẵn)

Các đường dẫn dưới đây tính từ NetworkTerminal.

| Field | Component cần kéo nếu setup thủ công |
|---|---|
| playerNameInput | OperatorSection/PlayerNameInput — TMP_InputField |
| sessionNameInput | SessionSection/SessionInput — TMP_InputField |
| hostButton | ActionButtons/HostButton — Button |
| joinButton | ActionButtons/JoinButton — Button |
| readyButton | LobbyControls/ReadyButton — Button |
| startButton | LobbyControls/StartButton — Button |
| leaveButton | LobbyControls/LeaveButton — Button |
| statusText | StatusSection/StatusText — TMP_Text |
| memberCountText | StatusSection/MemberCount — TMP_Text |
| memberListText | MemberList/Viewport/Content — TMP_Text |
| statusIndicator | StatusSection/StatusIndicator — Image |
| bootstrap | Để None khi service thuộc scene Bootstrap; tự tìm lúc runtime |
| lobbyManager | Để None khi service thuộc scene Bootstrap; tự tìm lúc runtime |
| maxPlayers | 4 (hỗ trợ 2–4 như API hiện tại) |

Nếu service thực sự nằm trong cùng scene, có thể kéo component tương ứng vào hai field networking. Không serialize reference chéo scene. Không nhân bản service để lấp field None.

## Test hai instance

1. Sau khi Unity compile và Fusion weave thành công, build executable mới. Dùng **Editor + executable mới** hoặc hai executable từ cùng build. Cả hai phải có thay đổi OperatorName vì layout NetworkBehaviour đã thay đổi.
2. Khởi động qua Bootstrap, đăng nhập hai tài khoản khác nhau; dùng backend/Photon App ID và region như luồng Host/Join đang hoạt động. UI không tự cấu hình hay chạy backend/Docker.
3. Instance A: Operator ID `ALPHA`, Session Code `echo-ui-01`, chọn INITIALIZE HOST. Trong lúc chờ, hai input và Host/Join phải bị khóa; click liên tiếp không tạo thêm request.
4. Instance B: Operator ID `BRAVO`, cùng session code, chọn CONNECT TO SESSION. Khi thành công, cả hai hiển thị SIGNAL: ESTABLISHED, 2 / 4 và tên ALPHA/BRAVO. Tên có thể xuất hiện sau một nhịp replication; fallback Player {ActorId} là bình thường trong thời gian đó.
5. Mỗi bên bấm SET READY. Kiểm tra ready cập nhật ở cả hai; Start chỉ tương tác ở host khi LobbyManager cho phép. Bấm START MATCH để kiểm tra flow scene/game hiện có.
6. Bắt đầu phiên mới và cho client DISCONNECT. Host phải cập nhật member count/list. Shutdown giữ hành vi cũ: quay về Bootstrap.
7. Dùng session code không tồn tại: UI hiển thị thông báo lỗi gốc từ NetworkBootstrap, Console có `[NetworkLobbyUI]`, và Host/Join được mở lại khi thao tác hoàn tất. Sau đó sửa code và join lại.
8. Để trống/nhập toàn khoảng trắng vào tên hoặc session: có lỗi rõ ràng, không bắt đầu networking. Thử tên chứa `<b>`, xuống dòng hoặc dài hơn 32 ký tự: không diễn giải TMP rich text; tên được chuẩn hóa/giới hạn.
9. Trong lúc connecting, tắt/bật Canvas hoặc chuyển scene theo flow hiện có: kiểm tra Console không có MissingReferenceException/NullReferenceException. Listener được hủy khi disable; async continuation kiểm tra object trước khi cập nhật UI.
10. Tắt host hoặc mất mạng: kiểm tra lỗi/shutdown theo NetworkBootstrap; scene có thể quay về Bootstrap ngay nên thông báo Lobby có thể không còn hiển thị sau chuyển scene. Không có reconnect hoặc host migration mới trong thay đổi này.
11. Kiểm tra Game view 1920×1080 và 1280×720: panel bên trái, không che toàn cảnh; tab/click input, hover/press button, scroll danh sách và tên dài đều đọc được.

Chỉ cân nhắc xóa debug component/script sau khi các ca kiểm thử trên đạt. Hiện component cũ vẫn là phương án rollback.

## Kiểm chứng đã thực hiện và giới hạn

- Biên dịch toàn bộ source runtime trong Assembly-CSharp.csproj cộng NetworkLobbyUI bằng Roslyn, tham chiếu DLL Unity 6000.5.8f1/Photon/TMP/Input System hiện có: không có lỗi C# (có warning field serialized như project hiện tại).
- Biên dịch NetworkLobbyUIBuilder với DLL Editor và runtime vừa kiểm tra: không có lỗi C#.
- Kiểm tra diff các file networking: không có lỗi whitespace.
- Unity Editor MCP/TCP localhost:6400 timeout, kể cả ping; chưa chạy menu dựng scene, chưa kiểm tra hình ảnh trực tiếp, chưa thực hiện Fusion Weaver hoặc test hai instance. Lobby.unity chưa được sửa tự động. Các bước Unity và runtime phía trên vẫn cần thực hiện.
