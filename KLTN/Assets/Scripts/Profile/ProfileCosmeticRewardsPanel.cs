using EchoProtocol.Auth;
using EchoProtocol.Settings;
using EchoProtocol.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.Profile
{
    public sealed class ProfileCosmeticRewardsPanel : MonoBehaviour
    {
        RectTransform _dialog;
        Font _font;
        Image _badge, _frame;
        Text _message;
        readonly Text[] _labels = new Text[4];
        readonly Button[] _buttons = new Button[4];
        readonly Image[] _previews = new Image[4];
        bool _busy;

        public void Initialize(Image portrait, Font font)
        {
            _font = font;
            var open = Button("CosmeticRewardsButton", transform, new Vector2(350, -665), new Vector2(640, 28),
                GameLanguage.Choose("NGOẠI HÌNH THEO CẤP", "LEVEL COSMETICS"));
            open.onClick.AddListener(Open);
            _frame = Overlay("MilestoneFrame", portrait.transform);
            _badge = Overlay("MilestoneBadge", portrait.transform);
            var badge = _badge.rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(1, 0); badge.pivot = new Vector2(1, 0);
            badge.sizeDelta = new Vector2(42, 42); badge.anchoredPosition = new Vector2(-8, 8);
            Refresh();
        }

        void OnEnable() { PlayerProfileSession.ProfileChanged += Refresh; Refresh(); }
        void OnDisable() { PlayerProfileSession.ProfileChanged -= Refresh; _busy = false; if (_dialog) _dialog.gameObject.SetActive(false); }

        void Open()
        {
            if (!_dialog) Build();
            _dialog.gameObject.SetActive(true); Refresh();
        }

        void Build()
        {
            _dialog = Node("ProgressionCosmeticsDialog", transform, Vector2.zero, Vector2.zero);
            _dialog.anchorMin = Vector2.zero; _dialog.anchorMax = Vector2.one; _dialog.offsetMin = _dialog.offsetMax = Vector2.zero;
            var veil = _dialog.gameObject.AddComponent<Image>(); veil.color = new Color(0, 0, 0, .92f);
            var panel = Node("Panel", _dialog, Vector2.zero, new Vector2(960, 510), true);
            panel.gameObject.AddComponent<Image>().color = new Color(.025f, .025f, .035f, 1);
            var title = Label("Title", panel, new Vector2(40, -25), new Vector2(840, 40), 26);
            title.text = GameLanguage.Choose("NGOẠI HÌNH MIỄN PHÍ THEO CẤP", "FREE LEVEL COSMETICS");
            for (int i = 0; i < 4; i++)
            {
                int row = i; float y = -90 - i * 82;
                var thumb = Node("Preview" + i, panel, new Vector2(40, y), new Vector2(60, 60));
                _previews[i] = thumb.gameObject.AddComponent<Image>(); _previews[i].preserveAspect = true; _previews[i].raycastTarget = false;
                _labels[i] = Label("Reward" + i, panel, new Vector2(115, y), new Vector2(490, 68), 19);
                var button = Button("Equip" + i, panel, new Vector2(680, y - 10), new Vector2(230, 42), "");
                button.onClick.AddListener(() => Equip(row)); _buttons[i] = button;
            }
            _message = Label("Message", panel, new Vector2(40, -424), new Vector2(620, 60), 16);
            _message.text = GameLanguage.Choose("Nhận một lần, không tốn Credits. Ngoại hình sẽ được bổ sung sau.", "One-time rewards, no Credits cost. Appearance will be available in a future update.");
            Button("Close", panel, new Vector2(715, -436), new Vector2(195, 40), GameLanguage.Choose("ĐÓNG", "CLOSE"))
                .onClick.AddListener(() => { if (!_busy) _dialog.gameObject.SetActive(false); });
        }

        void Equip(int row)
        {
            var rewards = PlayerProfileSession.CosmeticRewards;
            if (_busy || row >= rewards.Length || rewards[row] == null) return;
            var reward = rewards[row];
            if (!reward.owned || (!reward.equipped && ProgressionCosmeticAssets.Load()?.Find(reward.rewardKey)?.Ready != true)) return;
            _busy = true; Refresh();
            AuthRuntime.EnsureExists().PlayerProfileService.EquipCosmetic(reward.category, reward.equipped ? null : reward.rewardKey, success =>
            {
                if (!this) return;
                _busy = false;
                if (_message) _message.text = success ? GameLanguage.Choose("ĐÃ LƯU", "SAVED") : GameLanguage.Choose("Không lưu được. Hãy thử lại.", "Could not save. Try again.");
                Refresh();
            });
        }

        void Refresh()
        {
            if (_frame) { _frame.sprite = ProgressionCosmeticAssets.Equipped("level20_frame")?.profileOverlay; _frame.enabled = _frame.sprite; }
            if (_badge) { _badge.sprite = ProgressionCosmeticAssets.Equipped("level15_badge")?.profileOverlay; _badge.enabled = _badge.sprite; }
            if (!_dialog) return;
            var rewards = PlayerProfileSession.CosmeticRewards;
            for (int i = 0; i < _labels.Length; i++)
            {
                bool present = i < rewards.Length && rewards[i] != null;
                _labels[i].gameObject.SetActive(present); _buttons[i].gameObject.SetActive(present);
                _previews[i].enabled = false;
                if (!present) continue;
                var reward = rewards[i]; var asset = ProgressionCosmeticAssets.Load()?.Find(reward.rewardKey);
                string state = !reward.owned ? GameLanguage.Choose("Chưa đạt cấp", "Level required")
                    : asset?.Ready != true ? GameLanguage.Choose("Đã nhận · Ngoại hình sắp có", "Owned · Appearance coming soon")
                    : reward.equipped ? GameLanguage.Choose("Đang dùng", "Equipped") : GameLanguage.Choose("Đã nhận", "Owned");
                _labels[i].text = GameLanguage.Choose($"Cấp {reward.requiredLevel}", $"Level {reward.requiredLevel}") + " · " + reward.displayName + "\n" + state;
                _previews[i].sprite = asset?.preview; _previews[i].enabled = _previews[i].sprite;
                _buttons[i].GetComponentInChildren<Text>().text = reward.equipped ? GameLanguage.Choose("BỎ CHỌN", "UNEQUIP")
                    : asset?.Ready == true ? GameLanguage.Choose("SỬ DỤNG", "EQUIP") : GameLanguage.Choose("SẮP CÓ", "COMING SOON");
                _buttons[i].interactable = !_busy && reward.owned && (reward.equipped || asset?.Ready == true);
            }
        }

        Image Overlay(string name, Transform parent)
        {
            var rect = Node(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.enabled = false; return image;
        }
        Button Button(string name, Transform parent, Vector2 position, Vector2 size, string text)
        {
            var node = Node(name, parent, position, size);
            node.gameObject.AddComponent<Image>();
            var label = Label("Label", node, Vector2.zero, size, 17);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter; label.text = text;
            var button = node.gameObject.AddComponent<Button>(); IndustrialUiStyle.ActionButton(button); return button;
        }
        Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Node(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = _font; text.fontSize = fontSize; text.color = IndustrialUiStyle.Text;
            text.supportRichText = false; text.raycastTarget = false; text.alignment = TextAnchor.UpperLeft;
            UiTypographyTheme.Apply(text); return text;
        }
        static RectTransform Node(string name, Transform parent, Vector2 position, Vector2 size, bool centered = false)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = centered ? new Vector2(.5f, .5f) : new Vector2(0, 1);
            rect.pivot = centered ? new Vector2(.5f, .5f) : new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
    }
}
