using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    // Shared by existing HUD instances and the Editor builder.
    public static class HUDPresentationStyle
    {
        public static readonly Color Ink = new Color32(229, 227, 218, 255);
        public static readonly Color Muted = new Color32(154, 165, 163, 255);
        public static readonly Color Accent = new Color32(126, 166, 164, 255);
        public static readonly Color Track = new Color32(57, 65, 64, 255);
        public static readonly Color Warning = new Color32(216, 168, 94, 255);
        public static readonly Color Danger = new Color32(208, 104, 95, 255);

        public static void Apply(Transform root)
        {
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.referenceResolution = new Vector2(1600f, 900f);
            ApplyEquipment(root);
            ApplyPrompt(root);
            ApplyTeam(root);
            var objective = Find(root, "ObjectiveTracker_Panel");
            if (objective != null)
            {
                Place(objective, new Vector2(0, 1), new Vector2(32, -32), new Vector2(424, 152));
                Panel(objective);
                Label(objective, "PhaseBadge", new Vector2(16, -12), new Vector2(230, 18), 12, Muted);
                Label(objective, "ObjectiveTitle", new Vector2(16, -38), new Vector2(392, 28), 20, Ink, true);
                Label(objective, "ObjectiveDetail", new Vector2(16, -72), new Vector2(392, 52), 14, Muted);
                var hint = Find(objective, "MissionKeyHint");
                if (hint == null)
                {
                    var go = new GameObject("MissionKeyHint", typeof(RectTransform), typeof(Text));
                    go.transform.SetParent(objective, false);
                    var label = go.GetComponent<Text>();
                    var phase = Find(objective, "PhaseBadge");
                    label.font = phase != null ? phase.GetComponent<Text>()?.font : null;
                    if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.text = "[J] Nhiệm vụ";
                }
                Label(objective, "MissionKeyHint", new Vector2(282, -12), new Vector2(126, 18), 12, Muted);
                var bar = Find(objective, "ProgressBarBG");
                if (bar != null)
                {
                    Place(bar, new Vector2(0, 0), new Vector2(16, 12), new Vector2(392, 3));
                    bar.GetComponent<Image>().color = Track;
                }
                Hide(objective, "HeaderGlow");
            }

            var vitals = Find(root, "PlayerVitals_Panel");
            if (vitals == null) return;
            Place(vitals, Vector2.zero, new Vector2(32, 32), new Vector2(320, 116));
            Panel(vitals);
            Hide(vitals, "AccentBar");
            Label(vitals, "StaminaTitle", new Vector2(16, -69), new Vector2(200, 19), 12, Muted);
            var title = Find(vitals, "StaminaTitle")?.GetComponent<Text>();
            if (title != null) title.text = "THỂ LỰC";
            Label(vitals, "StaminaValue", new Vector2(236, -69), new Vector2(68, 19), 12, Muted);
            var stamina = Find(vitals, "StaminaBarBG");
            if (stamina != null)
            {
                Place(stamina, new Vector2(0, 1), new Vector2(16, -95), new Vector2(288, 4));
                stamina.GetComponent<Image>().color = Track;
            }
            var status = Find(vitals, "StatusBadge");
            if (status != null)
            {
                Place(status, new Vector2(0, 1), new Vector2(16, -14), new Vector2(288, 23));
                var bg = status.GetComponent<Image>();
                if (bg != null) { bg.sprite = null; bg.color = Color.clear; }
                foreach (var outline in status.GetComponents<Outline>()) outline.enabled = false;
                Label(status, "BadgeText", Vector2.zero, new Vector2(288, 23), 14, Ink);
            }
            var bleed = Find(vitals, "BleedoutContainer");
            if (bleed != null)
            {
                Place(bleed, new Vector2(0, 1), new Vector2(174, -14), new Vector2(130, 23));
                Label(bleed, "BleedText", Vector2.zero, new Vector2(130, 23), 13, Danger);
                Hide(bleed, "BleedBarBG");
            }
            var noise = Find(vitals, "NoiseContainer");
            if (noise != null)
            {
                Place(noise, new Vector2(0, 1), new Vector2(0, 34), new Vector2(320, 28));
                Panel(noise);
                Label(noise, "NoiseLabel", new Vector2(38, -3), new Vector2(268, 22), 12, Warning);
                var icon = Find(noise, "NoiseIcon");
                if (icon != null) Place(icon, new Vector2(0, 1), new Vector2(12, -5), new Vector2(18, 18));
            }
        }

        private static void ApplyEquipment(Transform root)
        {
            var hotbar = Find(root, "Hotbar_Panel");
            if (hotbar == null) return;
            Place(hotbar, new Vector2(1, 0), new Vector2(-32, 32), new Vector2(284, 92));
            if (hotbar.GetComponent<Image>() != null) hotbar.GetComponent<Image>().color = Color.clear;
            var names = new[] { "Slot1", "Slot2", "ToolSlot" };
            for (int i = 0; i < names.Length; i++)
            {
                var slot = Find(hotbar, names[i]);
                if (slot == null) continue;
                Place(slot, Vector2.zero, new Vector2(i * 98, 0), new Vector2(88, 92));
                Panel(slot);
                Label(slot, "Key", new Vector2(8, -6), new Vector2(28, 16), 11, Muted);
                if (i == 2)
                {
                    Label(slot, "Key", new Vector2(8, -6), new Vector2(72, 16), 10, Muted);
                    var key = Find(slot, "Key")?.GetComponent<Text>();
                    if (key != null) key.text = "Chuột trái";
                }
                Label(slot, "Label", new Vector2(6, -60), new Vector2(76, 28), 11, Ink);
                var label = Find(slot, "Label")?.GetComponent<Text>();
                if (label != null) label.alignment = TextAnchor.MiddleCenter;
                var icon = Find(slot, "Icon");
                if (icon != null) Place(icon, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(32, 32));
                var locked = Find(slot, "LockOverlay");
                if (locked != null)
                {
                    Panel(locked);
                    foreach (var text in locked.GetComponentsInChildren<Text>(true))
                    { text.fontSize = 12; text.fontStyle = FontStyle.Normal; text.color = Muted; }
                }
                var radial = Find(slot, "CooldownRadial");
                if (radial != null)
                {
                    radial.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
                    var cooldown = Find(radial, "CooldownText")?.GetComponent<Text>();
                    if (cooldown != null) { cooldown.fontSize = 16; cooldown.color = Ink; }
                }
            }
        }

        private static void ApplyPrompt(Transform root)
        {
            var prompt = Find(root, "InteractionPrompt_Panel");
            if (prompt == null) return;
            Place(prompt, new Vector2(0.5f, 0), new Vector2(0, 152), new Vector2(520, 64));
            Panel(prompt);
            Label(prompt, "PromptLabel", new Vector2(70, -8), new Vector2(388, 48), 15, Ink);
            var badge = Find(prompt, "KeyBadge");
            if (badge != null)
            {
                Place(badge, new Vector2(0, 1), new Vector2(16, -14), new Vector2(40, 36));
                Panel(badge);
                badge.GetComponent<Image>().color = new Color(0.22f, 0.25f, 0.25f, 0.9f);
                Label(badge, "KeyText", new Vector2(4, -4), new Vector2(32, 28), 14, Ink);
                var key = Find(badge, "KeyText")?.GetComponent<Text>();
                if (key != null) key.alignment = TextAnchor.MiddleCenter;
            }
            var ring = Find(prompt, "HoldRingContainer");
            if (ring != null)
            {
                Place(ring, new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(36, 36));
                foreach (var text in ring.GetComponentsInChildren<Text>(true))
                { text.fontSize = 12; text.fontStyle = FontStyle.Normal; text.color = Ink; }
                var bg = Find(ring, "RingBG")?.GetComponent<Image>();
                if (bg != null) bg.color = Track;
            }
        }

        private static void ApplyTeam(Transform root)
        {
            var team = Find(root, "TeammateStatus_Panel");
            if (team == null) return;
            Place(team, new Vector2(1, 1), new Vector2(-32, -32), new Vector2(276, 228));
            Panel(team);
            Label(team, "HeaderTitle", new Vector2(12, -8), new Vector2(252, 20), 12, Muted);
            var header = Find(team, "HeaderTitle")?.GetComponent<Text>();
            if (header != null) header.text = "ĐỒNG ĐỘI";
            for (int i = 0; i < 4; i++)
            {
                var slot = Find(team, "Slot_" + (i + 1));
                if (slot == null) continue;
                Place(slot, new Vector2(0, 1), new Vector2(12, -36 - i * 47), new Vector2(252, 41));
                if (slot.GetComponent<Image>() != null) slot.GetComponent<Image>().color = Color.clear;
                foreach (var outline in slot.GetComponentsInChildren<Outline>(true)) outline.enabled = false;
                Hide(slot, "Accent");
                Label(slot, "Name", Vector2.zero, new Vector2(168, 18), 13, Ink);
                Label(slot, "Distance", new Vector2(190, 0), new Vector2(62, 18), 11, Muted);
                var distance = Find(slot, "Distance")?.GetComponent<Text>();
                if (distance != null) distance.alignment = TextAnchor.MiddleRight;
                var status = Find(slot, "StatusBadge");
                if (status != null)
                {
                    Place(status, new Vector2(0, 1), new Vector2(0, -20), new Vector2(188, 16));
                    if (status.GetComponent<Image>() != null) status.GetComponent<Image>().color = Color.clear;
                    Label(status, "StatusText", Vector2.zero, new Vector2(188, 16), 10, Muted);
                }
                var hp = Find(slot, "HPBarBG");
                if (hp != null)
                {
                    Place(hp, new Vector2(0, 1), new Vector2(0, -38), new Vector2(252, 2));
                    hp.GetComponent<Image>().color = Track;
                }
                var core = Find(slot, "CoreCarryIcon");
                if (core != null) Place(core, new Vector2(1, 1), new Vector2(-70, -21), new Vector2(12, 12));
            }
        }

        private static void Panel(Transform transform)
        {
            var image = transform.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.045f, 0.055f, 0.055f, 0.78f);
                image.raycastTarget = false;
            }
            foreach (var outline in transform.GetComponents<Outline>()) outline.enabled = false;
        }

        private static void Label(Transform root, string name, Vector2 position, Vector2 size,
            int fontSize, Color color, bool bold = false)
        {
            var child = Find(root, name);
            if (child == null) return;
            Place(child, new Vector2(0, 1), position, size);
            var text = child.GetComponent<Text>();
            if (text != null)
            {
                text.fontSize = Mathf.Max(12, fontSize);
                text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                text.color = color;
                text.alignment = name == "StaminaValue" || name == "MissionKeyHint" || name == "BleedText"
                    ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.raycastTarget = false;
            }
            var tmp = child.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.fontSize = Mathf.Max(12, fontSize);
                tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
                tmp.color = color;
                tmp.alignment = name == "StaminaValue" || name == "MissionKeyHint" || name == "BleedText"
                    ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                tmp.enableAutoSizing = false;
                tmp.raycastTarget = false;
            }
            foreach (var shadow in child.GetComponents<Shadow>()) shadow.enabled = false;
        }

        private static void Place(Transform transform, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = transform as RectTransform;
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Hide(Transform root, string name)
        {
            var child = Find(root, name);
            if (child != null) child.gameObject.SetActive(false);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<RectTransform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
