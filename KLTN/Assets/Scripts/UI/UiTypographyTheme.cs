using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>One typography source for every runtime canvas.</summary>
    public static class UiTypographyTheme
    {
        private const string VisualsPath = "LeaderboardArchive/Visuals";
        private const string BodyTmpPath = "UI/LobbyReference/LobbySerif";
        private const string ButtonTmpPath = "UI/MainMenuTypography/OswaldTMP";

        private static LeaderboardArchiveVisuals _visuals;
        private static TMP_FontAsset _bodyTmp;
        private static TMP_FontAsset _buttonTmp;

        private static LeaderboardArchiveVisuals Visuals =>
            _visuals != null ? _visuals : _visuals = Resources.Load<LeaderboardArchiveVisuals>(VisualsPath);

        public static TMP_FontAsset BodyTmp =>
            _bodyTmp != null ? _bodyTmp : _bodyTmp = Resources.Load<TMP_FontAsset>(BodyTmpPath) ?? TMP_Settings.defaultFontAsset;

        public static TMP_FontAsset ButtonTmp =>
            _buttonTmp != null ? _buttonTmp : _buttonTmp = Resources.Load<TMP_FontAsset>(ButtonTmpPath) ?? BodyTmp;

        public static Font BodyLegacy => Visuals != null && Visuals.spectralFont != null
            ? Visuals.spectralFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Font ButtonLegacy => Visuals != null && Visuals.buttonFont != null
            ? Visuals.buttonFont : BodyLegacy;

        public static void ApplyScreenHeading(Text label)
        {
            if (Visuals != null && Visuals.headingFont != null) label.font = Visuals.headingFont;
            else label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 36;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = IndustrialUiStyle.Text;
            label.raycastTarget = false;

            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(119, 33, 31, 166);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        public static void Apply(TMP_Text label, bool button = false)
        {
            if (label != null) label.font = button ? ButtonTmp : BodyTmp;
        }


        public static void Apply(Text label, bool button = false)
        {
            if (label != null) label.font = button ? ButtonLegacy : BodyLegacy;
        }
    }
}
