# Menu cài đặt ESC

Menu lấy bố cục từ ảnh tham chiếu: nền game được làm tối, bảng đen với viền xước đỏ, hai cột, chữ tiếng Việt và biểu tượng vector. Không có mục Nhảy/Jump. Canvas tự co theo cửa sổ, giữ đủ nội dung ở màn hình rộng và 4:3.

## Cách dùng

- **ESC** mở cài đặt trực tiếp trong trận hoặc Lobby. **ESC / Tiếp tục** đóng và trả điều khiển cho người chơi.
- Khi đang **Downed**, ESC vẫn mở/đóng cài đặt. Túi đồ bị khóa cho đến khi được hồi sinh; đóng cài đặt vẫn giữ các giới hạn điều khiển của trạng thái Downed.
- **Chung**: microphone, âm lượng voice, nhấn giữ để nói, phím mic, độ nhạy chuột, đảo Y, tăng tốc chuột, âm lượng và bảng phím.
- **Đồ họa**: chất lượng Unity, độ phân giải được màn hình hỗ trợ, toàn màn hình, VSync. Độ phân giải/toàn màn hình dùng trong bản build; hai điều khiển này được vô hiệu hóa trong Unity Editor.
- **Âm thanh**: các mức âm lượng, danh sách microphone, thử mic cục bộ, làm mới thiết bị, trạng thái kết nối, mute/unmute đồng đội và thử kết nối lại.
- **Điều khiển**: đổi WASD, tương tác, chạy, cúi, đèn pin và túi đồ. Tab mặc định mở túi đồ; game chưa có bản đồ trong mục này. Phím đã dùng cho hành động khác hoặc microphone bị từ chối. ESC hủy gán phím.
- **Túi đồ** mở inventory trong trận. **Rời phòng** và **Thoát game** có bước xác nhận. Trong Unity Editor, Thoát game dừng Play mode.

Mic mặc định tắt khi khởi động. Bật microphone rồi chọn chế độ: tắt “Nhấn giữ để nói” để phím mic bật/tắt như trước; bật chế độ này để chỉ truyền khi đang giữ phím. Menu vẫn nhận tiếng đồng đội; chỉ bài thử mic cục bộ được thu khi đang ở cài đặt.

Các thiết lập được áp dụng ngay và lưu vào PlayerPrefs khi đóng menu. Giữ nguyên phím gamepad khi đổi phím bàn phím. Độ nhạy/đảo Y/tăng tốc chỉ tác động chuột.

## Chỉnh giao diện

Prefab: `Assets/Resources/Voice/VoiceSettingsCanvas.prefab`.

`VoiceSettingsCanvasFactory` tạo bố cục; `SettingsMenuWidgets`, `HorrorFrameGraphic` và `SettingsIconGraphic` dựng các thành phần. `VoiceSettingsPanel` nối các nút vào chức năng và quản lý khóa điều khiển. Runtime dùng factory nếu prefab còn là phiên bản menu cũ.

Trong Edit mode, menu **ECHO Protocol > Setup > Redesign Voice Settings Canvas** ghi lại prefab từ factory. **ECHO Protocol > Tests > Render Settings Preview** xuất ảnh vào `Temp/SettingsMenu-1920x1080.png` và `Temp/SettingsMenu-1280x960.png` qua một scene preview riêng. **Settings Controls** chạy kiểm thử phím và điều kiện truyền voice.

## Kiểm tra trong Play mode / bản build

Đã biên dịch runtime và Editor với 0 lỗi; 22/22 kiểm thử EditMode về phím và điều kiện truyền voice đạt. Đã render Canvas trong Unity ở 1920×1080 và 1280×960; xem [ảnh menu](screenshots/settings-menu.png) và [ảnh 4:3](screenshots/settings-menu-4x3.png). Chưa kiểm tra thao tác trong Play mode hoặc nghe voice thực tế trên hai client.

1. Vào trận, ESC: bốn tab hoạt động, không có Jump; nhân vật/camera không nhận phím khi mở menu. Đóng bằng ESC hoặc Tiếp tục: điều khiển trở lại.
2. Gán tương tác sang phím còn trống; thử tương tác, ẩn nấp và kiểm tra gợi ý HUD. Gán phím trùng phải bị từ chối; ESC chỉ hủy gán. Enter/Space không được kích hoạt nút trong khi đang gán phím.
3. Chỉnh chuột và âm lượng khi âm thanh đang phát; nhạc nền, hiệu ứng và voice thay đổi đúng nhóm. Khởi động lại để kiểm tra thiết lập đã lưu.
4. Hai client chọn mic: thử chế độ bật/tắt và nhấn giữ để nói, thả phím phải dừng truyền; thử mute đồng đội. Thử mic trong menu chỉ hiển thị mức đầu vào.
5. Kiểm tra chất lượng/VSync; kiểm tra độ phân giải và toàn màn hình trong bản build. Thử Rời phòng/Thoát game rồi Hủy để bảo đảm phiên chơi vẫn tiếp tục.
6. Trên cả host và client, để nhân vật bị Downed rồi mở/đóng ESC vài lần; các tab vẫn dùng được, túi đồ bị vô hiệu hóa. Thử bị Downed khi menu đã mở và được đồng đội hồi sinh khi menu vẫn mở; túi đồ chỉ dùng lại sau hồi sinh, nhân vật chỉ nhận điều khiển sau khi đóng menu.
