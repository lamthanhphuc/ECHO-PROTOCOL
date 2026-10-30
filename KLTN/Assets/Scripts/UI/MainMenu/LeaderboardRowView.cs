using System;
using System.Linq;
using EchoProtocol.Settings;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>One reusable archive record row; the same bindings work in a prefab or fallback row.</summary>
    public sealed class LeaderboardRowView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image medalRule;
        [SerializeField] private Text rankText;
        [SerializeField] private Text avatarInitialsText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text durationText;
        private Color _baseColor;
        private Color _targetColor;

        private void Awake() => ResolveBindings();

        public void Bind(LeaderboardEntryDto entry, LeaderboardMode mode,
            Sprite portrait, int stripeIndex)
        {
            ResolveBindings();
            if (entry == null) return;
            Color accent = entry.rank == 1 ? Hex("D2AF69")
                : entry.rank == 2 ? Hex("BEC5C6")
                : entry.rank == 3 ? Hex("A97850") : Hex("929792");
            if (backgroundImage != null)
            {
                _baseColor = stripeIndex % 2 == 0
                    ? Hex("161818", 0.92f) : Hex("111414", 0.92f);
                _targetColor = _baseColor;
                backgroundImage.color = _baseColor;
                backgroundImage.raycastTarget = true;
            }
            if (medalRule != null)
            {
                medalRule.gameObject.SetActive(entry.rank <= 3);
                medalRule.color = accent;
                medalRule.raycastTarget = false;
            }
            if (rankText != null)
            {
                rankText.text = entry.rank.ToString("00");
                rankText.color = accent;
                rankText.supportRichText = false;
            }
            if (avatarImage != null)
            {
                avatarImage.sprite = null;
                avatarImage.preserveAspect = true;
                avatarImage.color = Hex("37302A");
                avatarImage.raycastTarget = false;
            }
            if (avatarInitialsText != null)
            {
                avatarInitialsText.text = mode == LeaderboardMode.Team
                    ? Math.Max(1, entry.players?.Length ?? 0).ToString()
                    : Initials(entry.displayName);
                avatarInitialsText.supportRichText = false;
            }
            if (nameText != null)
            {
                nameText.text = mode == LeaderboardMode.Team
                    ? (string.IsNullOrWhiteSpace(entry.squadLabel)
                        ? GameLanguage.Choose("ĐỘI CHƯA ĐẶT TÊN", "UNNAMED SQUAD") : entry.squadLabel)
                        + "\n" + SquadMembers(entry.players)
                    : (string.IsNullOrWhiteSpace(entry.displayName)
                        ? GameLanguage.Choose("NGƯỜI CHƠI", "UNKNOWN OPERATIVE") : entry.displayName);
                nameText.fontSize = mode == LeaderboardMode.Team ? 12 : 15;
                nameText.fontStyle = mode == LeaderboardMode.Team
                    ? FontStyle.Normal : FontStyle.Bold;
                nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
                nameText.verticalOverflow = VerticalWrapMode.Truncate;
                nameText.supportRichText = false;
            }
            if (durationText != null)
            {
                durationText.text = LeaderboardPresentation.FormatDuration(
                    entry.durationCentiseconds);
                durationText.color = accent;
                durationText.supportRichText = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetColor = Hex("35201F", 0.98f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetColor = _baseColor;
        }

        private void Update()
        {
            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(backgroundImage.color,
                    _targetColor, Time.unscaledDeltaTime * 12f);
        }

        private void ResolveBindings()
        {
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (rankText == null) rankText = transform.Find("Rank")?.GetComponent<Text>();
            if (avatarImage == null) avatarImage = transform.Find("Avatar")?.GetComponent<Image>();
            if (avatarInitialsText == null)
                avatarInitialsText = transform.Find("Avatar/AvatarInitials")?.GetComponent<Text>();
            if (nameText == null) nameText = transform.Find("Name")?.GetComponent<Text>();
            if (durationText == null)
                durationText = transform.Find("Duration")?.GetComponent<Text>();
            if (medalRule == null) medalRule = transform.Find("MedalRule")?.GetComponent<Image>();
        }

        public static LeaderboardRowView CreateFallback(Transform parent, Font font)
        {
            var row = NewRect("LeaderboardRow", parent);
            row.gameObject.AddComponent<Image>();
            var medal = NewRect("MedalRule", row);
            medal.anchorMin = new Vector2(0f, 0f);
            medal.anchorMax = new Vector2(0f, 1f);
            medal.pivot = new Vector2(0f, 0.5f);
            medal.sizeDelta = new Vector2(3f, 0f);
            medal.anchoredPosition = Vector2.zero;
            medal.gameObject.AddComponent<Image>();
            var rank = NewText("Rank", row, font, 16, TextAnchor.MiddleCenter, true);
            MiddleLeft(rank.rectTransform, 9f, 38f, 37f);
            var avatar = NewRect("Avatar", row);
            MiddleLeft(avatar, 53f, 31f, 31f);
            avatar.gameObject.AddComponent<Image>();
            var initials = NewText("AvatarInitials", avatar, font, 11,
                TextAnchor.MiddleCenter, true);
            Stretch(initials.rectTransform, 0f, 0f);
            var name = NewText("Name", row, font, 15, TextAnchor.MiddleLeft, false);
            name.color = Hex("DEE0D9");
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(93f, 0f);
            name.rectTransform.offsetMax = new Vector2(-155f, 0f);
            var time = NewText("Duration", row, font, 17, TextAnchor.MiddleRight, true);
            time.rectTransform.anchorMin = time.rectTransform.anchorMax =
                new Vector2(1f, 0.5f);
            time.rectTransform.pivot = new Vector2(1f, 0.5f);
            time.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
            time.rectTransform.sizeDelta = new Vector2(136f, 36f);
            return row.gameObject.AddComponent<LeaderboardRowView>();
        }

        private static string SquadMembers(LeaderboardPlayerDto[] players)
        {
            if (players == null || players.Length == 0) return "ROSTER UNAVAILABLE";
            return string.Join("  /  ", players.Select(player =>
                string.IsNullOrWhiteSpace(player?.displayName)
                    ? "UNKNOWN" : player.displayName));
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var words = name.Trim().Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 1
                ? (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant()
                : words[0].Substring(0, Math.Min(2, words[0].Length)).ToUpperInvariant();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform))
                .GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text NewText(string name, Transform parent, Font font,
            int size, TextAnchor alignment, bool bold)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = Hex("A58255");
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static void MiddleLeft(RectTransform rect, float x,
            float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static Color Hex(string rgb, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            color.a = alpha;
            return color;
        }
    }
}
