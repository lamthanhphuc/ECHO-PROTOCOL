using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EchoProtocol.Settings
{
    public enum GameLocale { Vietnamese, English }

    public static class GameLanguage
    {
        private const string PreferenceKey = "Echo.Language";
        private static readonly Dictionary<string, string> Cache = new();
        private static bool _loaded;
        private static GameLocale _current;
        public static GameLocale Current
        {
            get
            {
                if (!_loaded) { _current = (GameLocale)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 1); _loaded = true; }
                return _current;
            }
        }
        public static event Action Changed;
        public static string Choose(string vietnamese, string english) => Current == GameLocale.English ? english : vietnamese;

        public static void Set(GameLocale language)
        {
            if (language != GameLocale.Vietnamese && language != GameLocale.English) throw new ArgumentOutOfRangeException(nameof(language));
            if (Current == language) return;
            _current = language;
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
            Cache.Clear();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Changed = null; Cache.Clear(); _loaded = false; }

        // Exact phrases only: never translate player names, room codes or puzzle answers.
        private static readonly Dictionary<string, string[]> Phrases = BuildPhrases();
        private static readonly List<(Regex Pattern, string Output, GameLocale Locale)> Templates = BuildTemplates();
        private static List<(Regex, string, GameLocale)> BuildTemplates()
        {
            var result = new List<(Regex, string, GameLocale)>();
            foreach (string row in Catalog.Split('\n'))
            {
                int split = row.IndexOf('|');
                if (split < 0 || !row.Contains("{0}")) continue;
                var pair = new[] { row.Substring(0, split).Trim(), row.Substring(split + 1).Trim() };
                for (int source = 0; source < 2; source++)
                {
                    string pattern = Regex.Escape(pair[source]);
                    for (int argument = 0; argument < 8; argument++)
                        pattern = pattern.Replace(Regex.Escape("{" + argument + "}"), "(?<arg" + argument + ">.*?)");
                    result.Add((new Regex("^" + pattern + "$", RegexOptions.CultureInvariant), pair[1 - source], (GameLocale)(1 - source)));
                }
            }
            return result;
        }

        private static string TranslateLine(string source)
        {
            if (Phrases.TryGetValue(source, out var pair)) return pair[(int)Current];
            foreach (var template in Templates)
            {
                if (template.Locale != Current) continue;
                var match = template.Pattern.Match(source);
                if (!match.Success) continue;
                string output = template.Output;
                for (int argument = 0; argument < 8; argument++)
                    output = output.Replace("{" + argument + "}", match.Groups["arg" + argument].Value);
                return output;
            }
            return source;
        }
        private static Dictionary<string, string[]> BuildPhrases()
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (string row in Catalog.Split('\n'))
            {
                int split = row.IndexOf('|');
                if (split < 0) continue;
                var pair = new[] { row.Substring(0, split).Trim(), row.Substring(split + 1).Trim() };
                result.TryAdd(pair[0], pair);
                result.TryAdd(pair[1], pair);
            }
            return result;
        }

        public static string Translate(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            string cacheKey = ((int)Current) + ":" + source;
            if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
            if (Phrases.TryGetValue(source, out var pair)) return pair[(int)Current];
            // Preserve layout while localizing multi-line status messages.
            var lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = TranslateLine(lines[i]);
                if (lines[i].Contains("<"))
                    lines[i] = Regex.Replace(lines[i], @"(^|>)([^<>]+)(?=<|$)", match =>
                    {
                        string segment = match.Groups[2].Value;
                        string trimmed = segment.Trim();
                        string translated = TranslateLine(trimmed);
                        return match.Groups[1].Value + (translated == trimmed ? segment : segment.Replace(trimmed, translated));
                    });
            }
            string output = string.Join("\n", lines);
            if (Cache.Count > 2048) Cache.Clear();
            Cache[cacheKey] = output;
            return output;
        }

        private const string Catalog = @"
CÀI ĐẶT|SETTINGS
CHUNG|GENERAL
ĐỒ HỌA|GRAPHICS
ÂM THANH|AUDIO
ĐIỀU KHIỂN|CONTROLS
Ngôn ngữ|Language
Tiếp tục|Resume
TIẾP TỤC|RESUME
RỜI PHÒNG|LEAVE ROOM
THOÁT GAME|QUIT GAME
RỜI PHÒNG?|LEAVE ROOM?
THOÁT GAME?|QUIT GAME?
HỦY|CANCEL
QUAY LẠI|BACK
ĐÓNG|CLOSE
Cài đặt|Options
Đăng nhập|LOGIN
Đăng ký|REGISTER
Tên đăng nhập|Username
Mật khẩu|Password
Xác nhận mật khẩu|Confirm password
Đăng xuất|LOGOUT
CHƠI|PLAY
TẠO PHÒNG|HOST GAME
VÀO PHÒNG|JOIN GAME
TẠO PHÒNG|CREATE ROOM
VÀO PHÒNG|JOIN ROOM
MÃ PHÒNG|ROOM CODE
ĐỘ KHÓ|DIFFICULTY
DỄ|EASY
THƯỜNG|NORMAL
KHÓ|HARD
ĐANG TẠO PHÒNG…|CREATING ROOM...
ĐANG VÀO PHÒNG…|JOINING ROOM...
ĐÃ KẾT NỐI. ĐANG VÀO LOBBY…|CONNECTED. ENTERING LOBBY...
CHƯA KẾT NỐI|OFFLINE
ĐANG KẾT NỐI…|CONNECTING...
PHÒNG ĐANG MỞ|ROOM OPEN
ĐANG BẮT ĐẦU TRẬN…|MISSION STARTING...
ĐANG RỜI PHÒNG…|LEAVING ROOM...
SẴN SÀNG|READY
CHƯA SẴN SÀNG|NOT READY
Microphone|Microphone
Microphone (đầu vào)|Microphone input
Âm lượng đầu ra|Output volume
Nhấn giữ để nói|Push to talk
Phím bật / tắt mic|Microphone toggle key
Chọn microphone|Select microphone
THIẾT BỊ & ĐỒNG ĐỘI|DEVICES & TEAMMATES
Tốc độ chuột|Mouse controls
Độ nhạy chuột|Mouse sensitivity
Đảo trục Y|Invert Y axis
Tăng tốc chuột|Mouse acceleration
Âm thanh|Audio
Tổng âm lượng|Master volume
Nhạc nền|Music
Hiệu ứng|Effects
Phím điều khiển|Key bindings
Di chuyển tiến|Move forward
Lùi|Move backward
Sang trái|Move left
Sang phải|Move right
Tương tác|Interact
Chạy|Sprint
Cúi|Crouch
Bật đèn pin|Toggle flashlight
Chất lượng|Quality
Độ phân giải|Resolution
Toàn màn hình|Fullscreen
Thiết bị thu âm|Input device
Đồng đội|Teammates
LÀM MỚI|REFRESH
THỬ MICROPHONE|TEST MICROPHONE
DỪNG THỬ|STOP TEST
THỬ KẾT NỐI LẠI|RECONNECT
Bật|On
Tắt|Off
NHẤN PHÍM...|PRESS A KEY...
Phím này đang dùng cho điều khiển khác.|This key is already used by another control.
Chọn một phím để thay đổi  ·  ESC để hủy|Select a binding to change  ·  ESC to cancel
Nhấn phím mới  ·  ESC để hủy|Press a new key  ·  ESC to cancel
ESC  ·  HỦY ĐỔI PHÍM|ESC  ·  CANCEL REBIND
ESC  ·  QUAY LẠI GAME|ESC  ·  RETURN TO GAME
Chưa có đồng đội trong voice.|No teammates connected to voice yet.
Đã kết nối voice trong phòng|Connected to room voice
Vào phòng để kết nối voice|Join a room to connect voice
Voice chưa kết nối. Có thể thử kết nối lại.|Voice is disconnected. Try reconnecting.
Đang đăng nhập…|Logging in...
Đang đăng ký…|Registering...
Đang xác nhận phiên đăng nhập…|Confirming session...
Đăng ký thành công. Hãy đăng nhập.|Registration successful. Please log in.
Không thể kết nối máy chủ. Kiểm tra mạng và thử lại.|Cannot connect to server. Check backend connection.
Yêu cầu quá thời gian. Vui lòng thử lại.|Request timed out. Please try again.
Đã xảy ra lỗi. Vui lòng thử lại.|Something went wrong. Please try again.
Email đã được đăng ký.|Email is already registered
Tên đăng nhập đã tồn tại.|Username already exists
Tên đăng nhập hoặc mật khẩu không đúng.|Invalid username or password
Tài khoản đã bị khóa.|Account is locked
Mật khẩu xác nhận không khớp.|Password confirmation does not match
Mật khẩu không được vượt quá 72 byte UTF-8.|Password must not exceed 72 UTF-8 bytes
Phiên đăng nhập hết hạn. Hãy đăng nhập lại.|Session expired. Please log in again.
THIẾU MÃ PHÒNG|ROOM CODE REQUIRED
THIẾU TÊN NGƯỜI CHƠI|PLAYER NAME REQUIRED
KHÔNG TÌM THẤY PHÒNG|ROOM NOT FOUND
PHÒNG ĐÃ ĐẦY|ROOM IS FULL
PHÒNG ĐÃ ĐÓNG|ROOM IS CLOSED
HOST ĐÃ NGẮT KẾT NỐI|HOST DISCONNECTED
KẾT NỐI QUÁ THỜI GIAN|CONNECTION TIMED OUT
DỊCH VỤ CHƯA CHO PHÉP KẾT NỐI|CONNECTION SERVICE UNAVAILABLE
DỊCH VỤ KẾT NỐI CHƯA SẴN SÀNG|NETWORK SERVICE NOT READY
CHƯA THỂ BẮT ĐẦU|CANNOT START YET
CHỈ HOST CÓ THỂ BẮT ĐẦU|ONLY THE HOST CAN START
KHÔNG CÓ KẾT NỐI MẠNG|NO INTERNET CONNECTION
KẾT NỐI BỊ GIÁN ĐOẠN|CONNECTION INTERRUPTED
CHƯA THỂ THỰC HIỆN|ACTION UNAVAILABLE
KHÔNG THỂ KẾT NỐI|UNABLE TO CONNECT
Nhập mã phòng, rồi chọn Tạo phòng hoặc Vào phòng.|Enter a room code, then select Create room or Join room.
Nhập tên của bạn trước khi kết nối.|Enter your player name before connecting.
Kiểm tra mã phòng và hỏi host đã mở phòng chưa. Sau đó chọn Vào phòng để thử lại.|Check the room code and ask whether the host has opened the room. Then select Join room to retry.
Nhờ host kiểm tra chỗ trống hoặc dùng mã phòng khác.|Ask the host to check for a free slot, or use another room code.
Trận có thể đã bắt đầu. Nhờ host mở phòng mới rồi nhập lại mã.|The match may have started. Ask the host to open a new room and enter its code.
Nhờ host tạo lại phòng rồi chọn Vào phòng với mã mới.|Ask the host to create a new room, then join using its new code.
Kiểm tra mạng và mã phòng. Chọn Tạo phòng hoặc Vào phòng để thử lại.|Check your network and room code. Select Create room or Join room to retry.
Thử lại sau ít phút. Nếu vẫn lỗi, liên hệ người quản lý game.|Try again in a few minutes. If it persists, contact the game administrator.
Về menu chính và mở lại lobby. Nếu vẫn lỗi, khởi động lại game.|Return to the main menu and reopen the lobby. Restart the game if it persists.
Tất cả người chơi cần bật Sẵn sàng trước khi host bắt đầu.|All players must be ready before the host starts the match.
Nhờ chủ phòng bắt đầu trận.|Ask the host to start the match.
Bật Wi-Fi hoặc cắm mạng rồi chọn Tạo phòng / Vào phòng để thử lại.|Connect to the internet, then select Create room / Join room to retry.
Kiểm tra mạng và hỏi host phòng còn mở không. Sau đó thử kết nối lại.|Check your network and ask whether the room is still open, then reconnect.
Kiểm tra trạng thái Sẵn sàng và kết nối của phòng rồi thử lại. Nếu vẫn lỗi, mở lại phòng.|Check player readiness and the room connection, then retry. Reopen the room if it persists.
Chưa xác định được nguyên nhân. Kiểm tra mạng, mã phòng rồi thử lại; nếu vẫn lỗi, khởi động lại game.|The cause is unknown. Check your network and room code, then retry. Restart the game if it persists.
NHIỆM VỤ|OBJECTIVES
THỂ LỰC|STAMINA
MÁU|HEALTH
THẤT BẠI|MISSION FAILED
HOÀN THÀNH|MISSION ACCOMPLISHED
Sơ tán thành công|Evacuation successful
Đội đã hoàn thành cuộc sơ tán.|The team completed the evacuation.
Nhiệm vụ thất bại|Mission failed
Không thể hoàn thành cuộc sơ tán.|The team could not complete the evacuation.
Thoát khỏi cơ sở|Escape the facility
Trở về cửa thoát hiểm|Return to the emergency exit
Khôi phục nguồn điện|Restore sector power
Khởi động tàu|Start the frigate
Tìm tàu|Find the frigate
SƠ TÁN|EVACUATE
TRUY ĐUỔI CUỐI|FINAL HUNT
MỤC TIÊU|OBJECTIVE
Đã gục|Downed
Đã thoát|Escaped
Đã chết|Eliminated
Đang hoạt động|Alive
CHỌN ĐỒNG ĐỘI|SELECT TEAMMATE
GIỮ ĐỂ TƯƠNG TÁC|HOLD TO INTERACT
KHÔNG CÓ NGƯỜI CHƠI|NO PLAYERS YET
Nhập tên và mã phòng.|Enter your name and room code.
CHẤT LƯỢNG|QUALITY
Hiển thị hiện tại|Current display
Hiện tại|Current
Đồ họa & hiển thị|Graphics & display
Đồng bộ khung hình (VSync)|Frame synchronization (VSync)
Điều chỉnh chất lượng phù hợp với máy của bạn.|Choose a quality level suitable for your computer.
Chọn giá trị để chuyển sang tùy chọn tiếp theo.|Select a value to cycle to the next option.
Thay đổi được áp dụng và lưu tự động.|Changes are applied and saved automatically.
Độ phân giải / toàn màn hình thay đổi trong bản game.|Resolution / fullscreen can be changed in the built game.
Tùy chỉnh điều khiển|Customize controls
Chọn ô phím bên phải, sau đó nhấn phím mới.|Select a binding on the right, then press a new key.
ESC hủy thao tác đổi phím.|ESC cancels rebinding.
Phím đã dùng cho hành động khác sẽ được giữ nguyên.|Keys already assigned to another action will not be changed.
Chọn một người để tắt / bật tiếng.|Select a teammate to mute / unmute.
CHỌN MICROPHONE...|SELECT MICROPHONE...
NHẤN PHÍM MỚI...|PRESS A NEW KEY...
DỪNG THỬ MIC|STOP MIC TEST
KẾT NỐI LẠI|RECONNECT
Không tìm thấy microphone.|No microphone found.
Không thể thu mic. Kiểm tra thiết bị và chọn lại.|Cannot capture microphone input. Check and select your device again.
Chọn microphone để bắt đầu nói|Select a microphone to start speaking
Phím nhấn giữ để nói|Push-to-talk key
Mức đầu vào: {0}%|Input level: {0}%
Tắt tiếng  ·  {0}|Mute  ·  {0}
Bật tiếng  ·  {0}|Unmute  ·  {0}
Trống|Empty
Bình thường|Healthy
Bị thương|Injured
Bị bắt|Captured
Cần cứu trợ|Needs revival
Đã tử vong|Eliminated
Đang mang core|Carrying a core
Đang quan sát|Spectating
Đang quan sát · {0}|Spectating · {0}
Cần cứu · {0}s|Needs revival · {0}s
Còn {0} giây|{0} seconds remaining
HỒI  {0}s|COOLDOWN  {0}s
ĐANG QUÉT  {0}s|SCANNING  {0}s
ĐANG QUÉT...|SCANNING...
ĐANG CHỜ KẾT NỐI|WAITING FOR CONNECTION
MẤT KẾT NỐI|DISCONNECTED
MÁY QUÉT ĐANG HỒI|SCANNER ON COOLDOWN
KHÔNG CÓ TÍN HIỆU|NO SIGNAL
PHÁT HIỆN TÍN HIỆU|SIGNAL DETECTED
LÕI NĂNG LƯỢNG|ENERGY CORE
Không có tín hiệu|No signal
Nhấn phím bất kỳ để tiếp tục|Press any key to continue
Máy quét hiện trường|Field Scanner
Máy tạo tiếng động|Noise Maker
Bộ sơ cứu|First Aid Kit
Bộ ổn định lõi|Core Stabilizer
Ván chèn cửa|Door Jammer
Vác nặng · Dễ bị phát hiện|Heavy load · Easier to detect
Chạy gây tiếng động|Sprinting makes noise
Chỉ dùng để cứu người bị gục.|Only used to revive downed teammates.
Không hồi máu cho người vẫn còn đứng.|Does not heal teammates who are still standing.
Người mang lõi trong vùng có thể chạy nước rút bình thường.|Core carriers in the area can sprint normally.
Hồi chiêu 45 giây; theo dõi trên ô trang bị.|45-second cooldown; watch the equipment slot.
Ván chặn Stalker trong thời gian ngắn.|The board blocks the Stalker briefly.
Stalker có thể phá ván để đi qua.|The Stalker can break the board to pass.
Đèn đỏ nhấp nháy báo khu vực đang thu hút Stalker.|A blinking red light indicates the area is attracting the Stalker.
Rời khỏi khu vực sau khi đặt thiết bị.|Leave the area after placing the device.
Vị trí xem trước cho biết nơi thiết bị sẽ được đặt.|The placement preview shows where the device will be placed.
Bạn ở giữa radar. Chế độ lõi tìm nguồn năng lượng gần bạn.|You are at the radar center. Core mode locates nearby energy sources.
Chế độ Stalker chỉ phát hiện Stalker đang di chuyển.|Stalker mode only detects a moving Stalker.
[Chuột trái]  Quét khu vực trong 10 giây|[Left click]  Scan the area for 10 seconds
[Chuột phải]  Đổi giữa lõi năng lượng và Stalker|[Right click]  Switch between energy cores and Stalker
[Chuột trái]  Đặt thiết bị|[Left click]  Place device
[Chuột trái]  Kích hoạt|[Left click]  Activate
Vùng ổn định bán kính 5 m, kéo dài 15 giây.|A 5 m stabilization area lasting 15 seconds.
[Chuột trái] Quét|[Left click] Scan
[Chuột phải] Đổi chế độ|[Right click] Switch mode
[Chuột trái] Quét · [Chuột phải] Đổi chế độ|[Left click] Scan · [Right click] Switch mode
[Chuột trái] Đổi người chơi|[Left click] Switch player
[Giữ {0} / Chuột trái]  Cứu đồng đội|[Hold {0} / Left click]  Revive teammate
Nhắm vào người bị gục và giữ đến khi cứu hoàn tất.|Aim at a downed teammate and hold until revival is complete.
[{0} / Chuột trái]  Chèn cửa|[{0} / Left click]  Jam door
Chỉ gắn ván vào cửa đã bị phá.|Only attach the board to a broken door.
CHỈ CÓ THỂ MANG 1 TEAM TOOL|ONLY ONE TEAM TOOL CAN BE CARRIED
<size=18>[G] THẢ TEAM TOOL ĐANG CẦM</size>|<size=18>[G] DROP HELD TEAM TOOL</size>
<size=18>{0}</size>\nPHÁT HIỆN|<size=18>{0}</size>\nDETECTED
TÍN HIỆU|SIGNAL
GẦN NHẤT|NEAREST
CƯỜNG ĐỘ|STRENGTH
01 Tìm|01 Find
02 Giải mã|02 Decode
03 Đồng bộ|03 Sync
Xác nhận|Confirm
Kiểm tra mã|Check code
Đặt lại|Reset
Chưa hoàn thành|Incomplete
Hoàn thành|Complete
Nhiệm vụ|Objectives
Nhiệm vụ đã kết thúc.|The mission has ended.
Khôi phục hệ thống relay|Restore the relay system
Mở lối sang khu vực tiếp theo|Open the next area
Xác thực bảo mật|Security authorization
Nhập mã truy cập|Enter access code
Tìm trạm an ninh|Find the security terminal
Tìm Spacefrigate|Find the Spacefrigate
Tìm Fuel Cell|Find a Fuel Cell
Hộ tống Spacefrigate|Escort the Spacefrigate
Nạp nhiên liệu cho Spacefrigate|Refuel the Spacefrigate
Chuyển nguồn điện|Transfer power
Đã nạp nhiên liệu|Refueled
Đã mở cửa an ninh|Security door opened
Trở về Security Terminal|Return to the Security Terminal
Trở về Doorexit để sơ tán.|Return to Doorexit to evacuate.
Cửa thoát đã mở. Trở về Doorexit để sơ tán.|The exit is open. Return to Doorexit to evacuate.
Cửa thoát đã mở · Còn {0}|The exit is open · {0} remaining
Trở về Doorexit · Còn {0}|Return to Doorexit · {0} remaining
Đi tiếp đến hành lang sơ tán.|Proceed to the evacuation corridor.
Đứng gần tàu để tiếp tục di chuyển.|Stay near the frigate to keep it moving.
Tìm nguồn dự phòng và chuẩn bị vận chuyển.|Find the backup power source and prepare transport.
Cấp điện để mở lối sơ tán.|Supply power to open the evacuation route.
Thả tay sẽ làm giảm tiến độ.|Releasing will reduce progress.
Cấp điện cho hệ thống sơ tán · {0}%|Power the evacuation system · {0}%
Lắp Energy Core vào Sector Box    {0}/{1}|Insert Energy Cores into the Sector Box    {0}/{1}
Sửa và đồng bộ relay    {0}/4|Repair and synchronize relays    {0}/4
Tải dữ liệu tại Security Terminal · {0}%|Download data at the Security Terminal · {0}%
Qua cửa airlock và tìm Security Terminal.|Go through the airlock and find the Security Terminal.
Nhập mã xác thực tại Access Panel.|Enter the authorization code at the Access Panel.
Nhập mã xác thực tại Power Control.|Enter the authorization code at Power Control.
Giữ để {0}|Hold to {0}
1 Trái · 2 Thẳng · 3 Phải · 4 Lùi|1 Left · 2 Forward · 3 Right · 4 Back
Chọn hướng di chuyển|Choose a direction
Tiếp tục hộ tống Spacefrigate.|Continue escorting the Spacefrigate.
Đội đã sơ tán|The team evacuated
Đội đã sơ tán thành công.|The team evacuated successfully.
Hết nhiên liệu. Tìm Fuel Cell trong khu bảo trì|Out of fuel. Find a Fuel Cell in the maintenance area
rồi mang về Fuel Port.|and bring it to the Fuel Port.
Mang Fuel Cell về Fuel Port.|Bring the Fuel Cell to the Fuel Port.
Giữ E để nạp khi hết nhiên liệu · G để thả.|Hold E to refuel when empty · G to drop.
SECURITY HOLD QUÁ HẠN|SECURITY HOLD EXPIRED
Cả 4 Relay đã bị đặt lại vì Security Hold quá hạn. Hãy sửa lại các Relay.|All 4 relays reset because the Security Hold expired. Repair the relays again.
Tên đăng nhập và mật khẩu không được để trống.|Username and password are required.
Vui lòng điền đầy đủ thông tin.|All fields are required.
Email không hợp lệ.|Email is invalid.
Vui lòng nhập email hợp lệ.|A valid email is required.
Vui lòng nhập tên đăng nhập.|Username is required.
Tên đăng nhập không được vượt quá 100 ký tự.|Username must not exceed 100 characters.
Vui lòng nhập mật khẩu và xác nhận mật khẩu.|Password and confirmation are required.
Đăng nhập|Login
Đăng ký|Register
Tạo tài khoản|Create account
Quay lại đăng nhập|Back to login
Thoát game|Exit Game
CÀI ĐẶT|OPTIONS
TÊN NGƯỜI CHƠI|PLAYER NAME
NHÂN VẬT|CHARACTER
SINH TỒN KINH DỊ HỢP TÁC|COOPERATIVE SURVIVAL HORROR
Mật khẩu phải có ít nhất 6 ký tự.|Password must be at least 6 characters.
ĐĂNG NHẬP|SIGN IN
ĐĂNG KÝ|SIGN UP
Tên đăng nhập|USERNAME
Mật khẩu|PASSWORD
Xác nhận mật khẩu|CONFIRM PASSWORD
ĐANG TẢI…|Loading...
TÀI KHOẢN|ACCOUNT
TRỞ VỀ|BACK
Tìm Security Terminal|Find Security Terminal
Mở Access Panel|Open Access Panel
Trở về Doorexit|Return to Doorexit
THOÁT HIỂM|EVACUATE
ZONE 01 · Khôi phục nguồn điện|ZONE 01 · Restore sector power
ZONE 02 · An ninh|ZONE 02 · Security
ZONE 03 · Nguồn dự phòng|ZONE 03 · Backup power
[J] Đóng|[J] Close
[J] Nhiệm vụ|[J] Objectives
Xác thực {0}%|Authorize {0}%
Tìm và lắp Energy Core vào Sector Box: {0}/{1}|Find and insert Energy Cores into the Sector Box: {0}/{1}
Tìm Security Terminal: {0}|Find the Security Terminal: {0}
Sửa 4 relay: {0}/4|Repair 4 relays: {0}/4
Xác thực bảo mật: {0}% {1}|Security authorization: {0}% {1}
Nhập mã tại Access Panel: {0}|Enter the Access Panel code: {0}
Tìm Spacefrigate: {0} · Đưa tàu về bến: {1}|Find the Spacefrigate: {0} · Bring it to the dock: {1}
Chuyển nguồn điện: {0}|Transfer power: {0}
Hoàn tất|Done
Giữ {0} · {1}% · {2}/4 người|Hold {0} · {1}% · {2}/4 players
Còn {0} · Đặt lại {1}|{0} remaining · Reset in {1}
Relay 4/4 · Trở về trạm|Relay 4/4 · Return to the station
Đặt lại sau {0}|Resets in {0}
ZONE 2 // Khôi phục hệ thống relay|ZONE 2 // Restore relays
ZONE 2 // Mở lối sang khu vực tiếp theo|ZONE 2 // Open next area
ZONE 2 // Xác thực bảo mật|ZONE 2 // Security authorization
PRIMARY OBJECTIVE // Xác thực bảo mật|PRIMARY OBJECTIVE // Security authorization
NHIÊN LIỆU: {0}|FUEL: {0}
ĐỒNG ĐỘI|TEAMMATES
Chuột trái|Left click
Trở về|Back
Đóng|Close
KHÔI PHỤC NGUỒN ĐIỆN|RESTORE SECTOR POWER
CHÀO MỪNG|WELCOME
ĐIỀU TRA VIÊN|OPERATOR
CẤP {0}|LEVEL {0}
SỐ TRẬN {0}|MATCHES {0}
THẮNG {0}|WINS {0}
NHÂN VẬT: {0}|CHARACTER: {0}
Đang nạp nhiên liệu · {0}%|Refueling · {0}%
TRANG BỊ|EQUIPMENT
TRANG BỊ VÀ NGOẠI HÌNH|EQUIPMENT AND COSMETICS
Trang bị & ngoại hình|Equipment & Cosmetics
Nhập mã phòng|Enter room code
NHẬP TÊN PHÒNG|ENTER ROOM NAME
NHẬP TÊN CỦA BẠN|ENTER YOUR NAME
TÊN CỦA BẠN|YOUR NAME
PHÒNG|ROOM
NHIỀU NGƯỜI CHƠI|MULTIPLAYER
CHUẨN BỊ NHIỆM VỤ|MISSION SETUP
KHU VỰC NHIỆM VỤ|MISSION SITE
CƠ SỞ KHOA HỌC|SCI-FI FACILITY
BẮT ĐẦU NHIỆM VỤ|START MISSION
CÔNG CỤ ĐỘI|TEAM TOOL
RỜI PHÒNG|LEAVE ROOM
KHÔNG CÓ|NONE
ĐANG CÓ THAO TÁC KẾT NỐI|NETWORK SESSION IS BUSY
Chờ thao tác hiện tại hoàn tất rồi thử lại.|Wait for the current operation to finish, then retry.
PHIÊN ĐĂNG NHẬP KHÔNG HỢP LỆ|SIGN-IN SESSION INVALID
Về menu chính và đăng nhập lại, sau đó thử kết nối.|Return to the main menu and sign in again, then reconnect.
Đang khôi phục phiên đăng nhập…|Restoring session...
Đang khôi phục phiên đăng nhập…|Restoring session
Đang kiểm tra phiên đăng nhập…|Checking session...
Trạng thái kết nối: CHƯA KẾT NỐI|CONNECTION STATUS: OFFLINE
NGƯỜI CHƠI: {0} / {1}|PLAYERS IN ROOM: {0} / {1}
ĐỘ KHÓ: {0}|DIFFICULTY: {0}
<  THƯỜNG  >|<  NORMAL  >
<  DỄ  >|<  EASY  >
<  KHÓ  >|<  HARD  >
> ĐANG TẠO PHÒNG…|> CREATING ROOM...
> ĐANG VÀO PHÒNG…|> JOINING ROOM...
01 · Nối mạch|01 · Routing
01 · Nối mạch — Cấp điện cho các đầu nối|01 · Routing — Power all terminals
01 · Nối mạch — Đi vòng ô hỏng|01 · Routing — Bypass faulty cells
02 · Ma trận cầu dao|02 · Breaker matrix
03 · Ổn định đầu ra|03 · Stabilize output
Relay A · Nguồn điện|Relay A · Power
Relay B · Bước {0}|Relay B · Step {0}
4 kênh tín hiệu|4 signal channels
6 chữ số khác nhau · Còn {0} lượt|6 distinct digits · {0} attempts remaining
Bù lỗi|Fault compensation
Bảo vệ ngắt điện · Bảng đã đặt lại|Protection tripped · Board reset
Bắt đầu đồng bộ|Start synchronization
Bắt đầu ổn định|Start stabilization
Chưa có dữ liệu|No data yet
Chưa hoạt động|Inactive
Chỉnh sóng hiện tại khớp sóng tham chiếu|Match the current wave to the reference wave
Chọn tín hiệu|Select signal
Chờ kiểm tra tải|Awaiting load test
Chờ quét|Awaiting scan
Cân bằng tải|Load balance
Cảnh báo trôi tín hiệu|Signal drift warning
Lỗi xuất hiện sau {0} s|Fault occurs in {0} s
Cửa thoát hiểm đang khóa|Emergency exit locked
Cửa đã mở - Chạy thoát!|Exit open - Run!
Dạng sóng|Waveform
Dừng khẩn cấp|Emergency stop
Giải mã|Decode
Giảm máy phát; kiểm tra tần số và tải|Reduce generator output; check frequency and load
Giảm tải; kiểm tra điện áp và tần số|Reduce load; check voltage and frequency
Giảm tần số; kiểm tra điện áp và tải|Reduce frequency; check voltage and load
Hoạt động|Active
Hết lượt thử · Chờ mã mới|No attempts left · Wait for a new code
Hủy đồng bộ|Cancel synchronization
Không có|Absent
trong mã|from the code
Kiểm tra mạch|Test circuit
Kiểm tra tải 0/1 · Xử lý lỗi|Load test 0/1 · Resolve fault
Kiểm tra tần số, dạng sóng và pilot|Check frequency, waveform and pilot
Lệch tần số|Frequency mismatch
Lỗi đang tác động|Active fault
Ma trận ổn định|Matrix stabilized
Mạch đứt · Chọn đường cấp điện khác|Circuit broken · Choose another power route
Mạch ổn định|Circuit stabilized
Mất cân bằng tải|Load imbalance
Mất kết nối|Disconnected
Mở Cửa Thoát Hiểm|Open Emergency Exit
Nối sai · Bảng đã đặt lại|Incorrect routing · Board reset
P  Nguồn điện|P  Power source
●  Đầu nối|●  Terminal
X  Ô hỏng|X  Faulty cell
Cấp điện cho mọi đầu nối.|Power every terminal.
Cách ly các ô hỏng.|Isolate faulty cells.
Kiểm tra thất bại sẽ|A failed test will
đặt lại hướng các ô.|reset cell orientations.
Pha bị trôi · Chỉnh lại pha|Phase drifted · Adjust phase
Pha · Khớp|Phase · Aligned
Phân phối tải|Load distribution
Quá tải|Overload
Quá áp|Overvoltage
Quét tín hiệu|Scan signal
Quét để xem 4 kênh|Scan to reveal 4 channels
Relay hoạt động|Relay online
Sóng hiện tại|Current wave
Sóng tham chiếu|Reference wave
Sắp có nhiễu tín hiệu|Signal interference incoming
Sẵn sàng kiểm tra|Ready to test
Theo dõi hệ thống|Monitor the system
Tìm tín hiệu|Find signal
Tín hiệu không khớp|Signal does not match
Tín hiệu lệch · Chỉnh lại slider|Signal misaligned · Adjust sliders
Tín hiệu đích|Target signal
Tín hiệu đích thay đổi · Quét lại|Target signal changed · Scan again
Tạm dừng|Paused
Tần số|Frequency
Tần số bị trôi · Chỉnh lại tần số|Frequency drifted · Adjust frequency
Tần số · Khớp|Frequency · Aligned
Giữ đầu ra an toàn|Keep output safe
Giữ an toàn {0} s|Stay safe for {0} s
Ô bị khóa · Không thể xoay|Cell locked · Cannot rotate
Ô ổn định {0}/{1} · {2} lượt|Stable cells {0}/{1} · {2} moves
Đang kiểm tra mạch|Testing circuit
Đang mở cửa|Opening exit
Đang quét tín hiệu…|Scanning signal…
Đang đọc tín hiệu|Reading signal
Đang đồng bộ|Synchronizing
Đang ổn định|Stabilizing
Điều chỉnh|Adjust
Điều chỉnh tần số|Adjust frequency
Đã chọn kênh|Channel selected
Đã chọn tín hiệu|Signal selected
Đã chọn · Xác nhận cả 3 dấu hiệu|Selected · Verify all 3 clues
Đã giải mã|Decoded
Đã khớp · Bắt đầu đồng bộ|Aligned · Start synchronization
Đã khớp · Giữ vị trí|Aligned · Hold position
Đã qua kiểm tra tải · Giữ đầu ra an toàn|Load test passed · Keep output safe
Đúng số|Correct digit
Sai vị trí|Wrong position
Đúng vị trí|Correct position
Đưa mọi ô về màu xanh.|Turn every cell green.
Nhấn đổi ô hiện tại và 4 ô cạnh nó.|Press to toggle this cell and its 4 neighbors.
Đường điện thay đổi · Đi vòng ô đỏ|Power route changed · Bypass red cells
Đầu ra chưa ổn định|Output unstable
Đầu ra máy phát|Generator output
Đặt lại ma trận|Reset matrix
Đồng bộ|Synchronize
Đồng bộ · {0} / {1} giây|Synchronizing · {0} / {1} seconds
Ổn định|Stabilize
3. Bắt đầu khi đã khớp. Nhiễu có thể đổi tần số hoặc pha.|3. Start when aligned. Interference may shift frequency or phase.
Mặc định hệ thống|System default
Bạn muốn đóng game?|Do you want to close the game?
Giữ thay đổi hiển thị?|Keep display changes?
Giữ thay đổi|Keep changes
Hủy|Cancel
Thoát game|Quit game
Rời phòng|Leave room
Rời phòng?|Leave room?
Thoát game?|Quit game?
Về menu|Return to menu
Về menu chính?|Return to main menu?
Bạn muốn về menu chính?|Return to the main menu?
Thử lại|Retry
";
    }
}
