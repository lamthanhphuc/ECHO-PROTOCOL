using EchoProtocol.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static EchoProtocol.UI.SettingsMenuWidgets;

namespace EchoProtocol.Voice
{
    /// <summary>Editable ESC settings canvas, bound by VoiceSettingsPanel.</summary>
    public static class VoiceSettingsCanvasFactory
    {
        public static GameObject Create()
        {
            var root = new GameObject("VoiceSettingsCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var overlay = Panel("Overlay", root.transform, 0, 0, 0, 0, new Color(0.012f, 0.012f, 0.015f, 0.78f));
            Stretch(overlay);
            var window = Rect("Window", overlay, 0, 0, 1480, 982);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
            Label("Title", window, 380, 0, 720, 76, "CÀI ĐẶT", 52, Foreground, TextAlignmentOptions.Center, true).characterSpacing = 7;
            Icon("SettingsIcon", window, 510, 17, 48, 48, SettingsIcon.Gear, Foreground);
            Label("Subtitle", window, 400, 76, 680, 22, "E C H O   P R O T O C O L", 12, Muted, TextAlignmentOptions.Center);
            Button("CloseButton", window, 1438, 15, 42, 42, "×", false, 28);
            Line("TopRule", window, 0, 110, 1480);
            Line("BottomRule", window, 0, 161, 1480);
            var tabs = Rect("SettingsTabs", window, 250, 111, 980, 50);
            string[] names = { "General", "Graphics", "Audio", "Controls" };
            string[] labels = { "CHUNG", "ĐỒ HỌA", "ÂM THANH", "ĐIỀU KHIỂN" };
            SettingsIcon[] tabIcons = { SettingsIcon.Gear, SettingsIcon.Monitor, SettingsIcon.Audio, SettingsIcon.Keyboard };
            for (int i = 0; i < names.Length; i++)
            {
                var tab = Button(names[i], tabs, i * 245, 0, 245, 50, labels[i], i == 0, 18);
                Icon("Icon", tab.transform, 27, 13, 25, 25, tabIcons[i]);
                tab.GetComponentInChildren<TMP_Text>().rectTransform.anchoredPosition = new Vector2(32, 0);
                tab.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = new Vector2(205, 50);
                var line = Panel("Active", tab.transform, 0, 48, 245, 2, Accent);
                line.GetComponent<Image>().raycastTarget = false;
                line.gameObject.SetActive(i == 0);
            }
            var body = Rect("Body", window, 0, 171, 1480, 738);
            BuildVoice(body);
            BuildMouse(body);
            BuildAudio(body);
            BuildControls(body);
            BuildDevices(body);
            BuildGraphics(body);
            BuildControlsNote(body);
            var footer = Rect("Footer", window, 0, 929, 1480, 53);
            Button("Leave", footer, 0, 5, 170, 43, "RỜI PHÒNG", false, 16);
            Button("Inventory", footer, 184, 5, 165, 43, "TÚI ĐỒ", false, 16);
            var quit = Button("Quit", footer, 418, 0, 304, 53, "THOÁT GAME", true, 23);
            Icon("Icon", quit.transform, 22, 12, 29, 29, SettingsIcon.Exit, Accent);
            var resume = Button("Resume", footer, 744, 0, 304, 53, "TIẾP TỤC", false, 23);
            Icon("Icon", resume.transform, 29, 13, 27, 27, SettingsIcon.Play, Foreground);
            Label("Saved", footer, 1090, 0, 390, 53, "ESC  ·  QUAY LẠI GAME", 14, Muted, TextAlignmentOptions.Right);
            var confirm = Panel("Confirm", overlay, 0, 0, 0, 0, new Color(0, 0, 0, 0.88f));
            Stretch(confirm);
            var prompt = Frame("Prompt", confirm, 0, 0, 630, 240, true);
            prompt.anchorMin = prompt.anchorMax = prompt.pivot = new Vector2(0.5f, 0.5f);
            prompt.anchoredPosition = Vector2.zero;
            Label("Title", prompt, 30, 25, 570, 48, "THOÁT GAME?", 30, Foreground, TextAlignmentOptions.Center, true);
            Label("Message", prompt, 30, 81, 570, 43, "Bạn sẽ rời phiên chơi hiện tại.", 21, Muted, TextAlignmentOptions.Center);
            Button("Cancel", prompt, 40, 155, 260, 51, "QUAY LẠI");
            Button("Accept", prompt, 330, 155, 260, 51, "THOÁT GAME", true);
            confirm.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
            return root;
        }

        private static void BuildVoice(Transform body)
        {
            var card = Card("Voice", body, 0, 0, 780, 330, "Trò chuyện thoại", "◉");
            Label("MicTitle", card, 34, 62, 400, 32, "Micrô");
            Toggle("MicToggle", card, 520, 61);
            Label("InputTitle", card, 34, 103, 390, 30, "Đầu vào micrô");
            var meter = Rect("Meter", card, 443, 113, 285, 13);
            for (int i = 0; i < 24; i++)
                Panel("Bar" + i, meter, i * 12, 0, 7, 13, new Color(0.19f, 0.21f, 0.20f)).GetComponent<Image>().raycastTarget = false;
            Label("VolumeTitle", card, 34, 144, 390, 30, "Âm lượng đầu ra");
            Slider("VolumeSlider", card, 443, 144, 240);
            Label("VolumeText", card, 697, 144, 55, 30, "100%", 18, Foreground, TextAlignmentOptions.Right);
            Label("PushToTalkTitle", card, 34, 185, 460, 30, "Nhấn giữ để nói");
            Toggle("PushToTalkToggle", card, 520, 183);
            Label("KeyTitle", card, 34, 226, 395, 30, "Phím bật / tắt mic");
            Button("KeyButton", card, 520, 222, 210, 35, "V", false, 18);
            Button("DeviceButton", card, 34, 276, 422, 33, "Chọn microphone", false, 15);
            Button("AdvancedButton", card, 475, 276, 255, 33, "THIẾT BỊ & ĐỒNG ĐỘI", false, 14);
        }

        private static void BuildMouse(Transform body)
        {
            var card = Card("Mouse", body, 0, 342, 780, 180, "Tốc độ chuột", "◐");
            Label("SensitivityTitle", card, 34, 60, 350, 30, "Độ nhạy chuột");
            Slider("Sensitivity", card, 405, 60, 278, 0.1f, 3);
            Label("SensitivityText", card, 695, 60, 57, 30, "1.00", 18, Foreground, TextAlignmentOptions.Right);
            Label("InvertTitle", card, 34, 98, 410, 30, "Đảo trục Y");
            Toggle("Invert", card, 520, 96);
            Label("AccelerationTitle", card, 34, 136, 410, 30, "Tăng tốc chuột");
            Toggle("Acceleration", card, 520, 134);
        }

        private static void BuildAudio(Transform body)
        {
            var card = Card("Audio", body, 0, 534, 780, 204, "Âm thanh", "♪");
            string[] names = { "Master", "Music", "Effects", "Voice" };
            string[] titles = { "Tổng âm lượng", "Nhạc nền", "Hiệu ứng", "Trò chuyện thoại" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 58 + i * 33;
                Label(names[i] + "Title", card, 34, y, 355, 28, titles[i], 20);
                Slider(names[i], card, 405, y, 278);
                Label(names[i] + "Value", card, 693, y, 59, 28, "100%", 18, Foreground, TextAlignmentOptions.Right);
            }
        }

        private static void BuildControls(Transform body)
        {
            var card = Card("Controls", body, 800, 0, 680, 738, "Phím điều khiển", "⌨");
            string[] names = { "MoveForward", "MoveBackward", "MoveLeft", "MoveRight", "Interact", "Sprint", "Crouch", "Flashlight", "Inventory" };
            string[] titles = { "Di chuyển tiến", "Lùi", "Sang trái", "Sang phải", "Tương tác", "Chạy", "Cúi", "Bật đèn pin", "Túi đồ" };
            SettingsIcon[] icons = { SettingsIcon.Up, SettingsIcon.Down, SettingsIcon.Left, SettingsIcon.Right,
                SettingsIcon.Interact, SettingsIcon.Run, SettingsIcon.Crouch, SettingsIcon.Flashlight, SettingsIcon.Inventory };
            string[] keys = { "W", "S", "A", "D", "E", "Shift", "Ctrl / C", "F", "Tab" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 70 + i * 66;
                Icon(names[i] + "Icon", card, 39, y + 6, 32, 32, icons[i]);
                Label(names[i] + "Title", card, 106, y, 352, 44, titles[i], 22);
                Button(names[i], card, 478, y, 173, 44, keys[i], false, 20);
                Line(names[i] + "Rule", card, 25, y + 55, 630);
            }
            Label("Hint", card, 28, 680, 624, 40, "Chọn một phím để thay đổi  ·  ESC để hủy", 16, Muted, TextAlignmentOptions.Center);
        }

        private static void BuildDevices(Transform body)
        {
            var devices = Card("Devices", body, 800, 0, 680, 350, "Thiết bị thu âm", "◉");
            Button("Refresh", devices, 493, 12, 161, 31, "LÀM MỚI", false, 14);
            Label("DeviceText", devices, 27, 61, 626, 32, "Chọn microphone để bắt đầu nói", 18, Muted);
            ScrollArea("List", devices, 23, 102, 634, 166);
            Button("Test", devices, 28, 284, 225, 42, "THỬ MICROPHONE", false, 16);
            Label("LevelText", devices, 272, 283, 380, 42, "Mức đầu vào: 0%", 18, Muted, TextAlignmentOptions.Right);
            var team = Card("Team", body, 800, 362, 680, 376, "Đồng đội", "◌");
            Label("Status", team, 27, 62, 626, 31, "Vào phòng để kết nối thoại", 18, Muted);
            Label("Hint", team, 27, 99, 626, 28, "Chọn một người để tắt / bật tiếng.", 18, Muted);
            ScrollArea("List", team, 23, 141, 634, 151);
            Button("Retry", team, 424, 314, 228, 37, "KẾT NỐI LẠI", false, 15);
            devices.gameObject.SetActive(false);
            team.gameObject.SetActive(false);
        }

        private static void BuildGraphics(Transform body)
        {
            var card = Card("Graphics", body, 0, 0, 780, 390, "Đồ họa & hiển thị", "▣");
            Label("QualityTitle", card, 34, 77, 370, 39, "Chất lượng");
            Button("Quality", card, 425, 76, 302, 41, "Hiện tại", false, 18);
            Label("ResolutionTitle", card, 34, 140, 370, 39, "Độ phân giải");
            Button("Resolution", card, 425, 139, 302, 41, "1920 × 1080", false, 18);
            Label("FullscreenTitle", card, 34, 209, 370, 34, "Toàn màn hình");
            Toggle("Fullscreen", card, 520, 209);
            Label("VSyncTitle", card, 34, 268, 420, 34, "Đồng bộ khung hình (VSync)");
            Toggle("VSync", card, 520, 268);
            Label("Hint", card, 34, 330, 700, 29, "Chọn giá trị để chuyển sang tùy chọn tiếp theo.", 17, Muted);
            var display = Card("Display", body, 800, 0, 680, 738, "Hiển thị hiện tại", "▣");
            var monitor = Frame("Monitor", display, 89, 146, 502, 284);
            Label("Resolution", monitor, 20, 60, 462, 90, "1920 × 1080", 39, Foreground, TextAlignmentOptions.Center, true);
            Label("Quality", monitor, 20, 156, 462, 55, "CHẤT LƯỢNG", 19, Accent, TextAlignmentOptions.Center);
            Panel("Stand", display, 319, 430, 42, 55, Rule);
            Panel("Base", display, 244, 485, 192, 4, Muted);
            Label("Description", display, 50, 548, 580, 70, "Điều chỉnh chất lượng phù hợp với máy của bạn.", 19, Muted, TextAlignmentOptions.Center)
                .textWrappingMode = TextWrappingModes.Normal;
            Label("SaveHint", display, 50, 643, 580, 32, "Thay đổi được áp dụng và lưu tự động.", 17, Muted, TextAlignmentOptions.Center);
            card.gameObject.SetActive(false);
            display.gameObject.SetActive(false);
        }

        private static void BuildControlsNote(Transform body)
        {
            var note = Card("ControlsNote", body, 0, 196, 780, 280, "Tùy chỉnh điều khiển", "⌨");
            Label("Body", note, 34, 77, 705, 160,
                EchoProtocol.Settings.GameLanguage.Choose("Chọn ô phím bên phải để đổi phím. ESC để hủy.\n\nPhím cố định:\nG · Thả đồ    J · Nhiệm vụ    F2 · Hướng dẫn công cụ\nH · Gọi hỗ trợ    1 / 2 · Chọn vật phẩm\nChuột trái · Dùng công cụ\nChuột phải · Xem trước vị trí đặt / Đổi chế độ quét", "Select a key on the right to rebind. ESC cancels.\n\nFixed controls:\nG · Drop    J · Missions    F2 · Tool guide\nH · Call for help    1 / 2 · Select item\nLeft click · Use tool\nRight click · Placement preview / Scanner mode"),
                16, Muted).textWrappingMode = TextWrappingModes.Normal;
            note.gameObject.SetActive(false);
        }

        public static Button AddListButton(Transform parent, string name, string title, bool selected)
        {
            var button = Button(name, parent, 0, 0, 570, 40, title, selected, 17);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            var label = button.GetComponentInChildren<TMP_Text>();
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(13, 0);
            label.rectTransform.offsetMax = new Vector2(-13, 0);
            label.alignment = TextAlignmentOptions.Left;
            label.fontStyle = FontStyles.Normal;
            return button;
        }
    }
}
