using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Tests
{
    public sealed class UiTypographyThemeTests
    {
        [TestCase("HeaderText")]
        [TestCase("SystemLabel")]
        [TestCase("TitleRedEcho")]
        public void RuntimeLocalizationPreservesAuthoredMainMenuWordmark(string labelName)
        {
            Type controllerType = Type.GetType("EchoProtocol.UI.MainMenu.MainMenuTypographyController, Assembly-CSharp");
            Type localizationType = Type.GetType("EchoProtocol.UI.LocalizedCanvas, Assembly-CSharp");
            Type labelType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(localizationType, Is.Not.Null);
            Assert.That(labelType, Is.Not.Null);
            var root = new GameObject("MainMenuTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.AddComponent(controllerType);
                var labelObject = new GameObject(labelName, typeof(RectTransform));
                labelObject.transform.SetParent(root.transform, false);
                Component label = labelObject.AddComponent(labelType);
                var font = Resources.Load("UI/MainMenuTypography/IndustrialTitleTMP");
                var material = Resources.Load<Material>("UI/MainMenuTypography/TitleMetal");
                Assert.That(font, Is.Not.Null);
                Assert.That(material, Is.Not.Null);
                labelType.GetProperty("font").SetValue(label, font);
                labelType.GetProperty("fontSharedMaterial").SetValue(label, material);
                var ink = new Color(0.81f, 0.12f, 0.16f, 0.8f);
                ((Graphic)label).color = ink;
                Component localization = root.AddComponent(localizationType);
                localizationType.GetMethod("Register", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(localization, new object[] { label });
                Assert.That(labelType.GetProperty("font").GetValue(label), Is.SameAs(font));
                Assert.That(labelType.GetProperty("fontSharedMaterial").GetValue(label), Is.SameAs(material));
                Assert.That(((Graphic)label).color, Is.EqualTo(ink));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ThemeUsesLobbyBodyAndMainMenuButtonFonts()
        {
            Type theme = Type.GetType("EchoProtocol.UI.UiTypographyTheme, Assembly-CSharp");
            Assert.That(theme, Is.Not.Null, "A shared UI typography theme must exist.");

            var bodyTmp = theme.GetProperty("BodyTmp")?.GetValue(null) as UnityEngine.Object;
            var buttonTmp = theme.GetProperty("ButtonTmp")?.GetValue(null) as UnityEngine.Object;
            var bodyLegacy = theme.GetProperty("BodyLegacy")?.GetValue(null) as Font;
            var buttonLegacy = theme.GetProperty("ButtonLegacy")?.GetValue(null) as Font;

            Assert.That(bodyTmp, Is.Not.Null);
            Assert.That(bodyTmp.name, Does.Contain("LobbySerif"));
            Assert.That(buttonTmp, Is.Not.Null);
            Assert.That(buttonTmp.name, Does.Contain("Oswald"));
            Assert.That(bodyLegacy, Is.Not.Null);
            Assert.That(buttonLegacy, Is.Not.Null);
        }

        [Test]
        public void PolicyIconSetContainsDedicatedAge18Badge()
        {
            Type iconType = Type.GetType("EchoProtocol.UI.SettingsIcon, Assembly-CSharp");
            Assert.That(iconType, Is.Not.Null);
            Assert.That(Enum.GetNames(iconType), Does.Contain("Age18"));
        }

        [TestCase("Cường độ chuyển động", "Motion intensity")]
        [TestCase("Giảm chuyển động", "Reduce motion")]
        [TestCase("Bắt đầu nhiệm vụ", "Start mission")]
        public void EnglishLocaleTranslatesRuntimeUiCopy(string vietnamese, string english)
        {
            Type language = Type.GetType("EchoProtocol.Settings.GameLanguage, Assembly-CSharp");
            Type locale = Type.GetType("EchoProtocol.Settings.GameLocale, Assembly-CSharp");
            Assert.That(language, Is.Not.Null);
            Assert.That(locale, Is.Not.Null);

            PropertyInfo current = language.GetProperty("Current");
            MethodInfo set = language.GetMethod("Set", BindingFlags.Public | BindingFlags.Static);
            MethodInfo translate = language.GetMethod("Translate", BindingFlags.Public | BindingFlags.Static);
            object previous = current.GetValue(null);
            try
            {
                set.Invoke(null, new[] { Enum.Parse(locale, "English") });
                Assert.That(translate.Invoke(null, new object[] { vietnamese }), Is.EqualTo(english));
            }
            finally
            {
                set.Invoke(null, new[] { previous });
            }
        }

        [TestCase("Profile", "PROFILE")]
        [TestCase("Web", "WEBSITE")]
        [TestCase("Shop", "SHOP")]
        [TestCase("Gear", "SETTINGS")]
        public void UtilityButtonsCreateConsistentHoverTooltips(string iconName, string expected)
        {
            Type style = Type.GetType("EchoProtocol.UI.UtilityBarStyle, Assembly-CSharp");
            Type iconType = Type.GetType("EchoProtocol.UI.SettingsIcon, Assembly-CSharp");
            Type language = Type.GetType("EchoProtocol.Settings.GameLanguage, Assembly-CSharp");
            Type locale = Type.GetType("EchoProtocol.Settings.GameLocale, Assembly-CSharp");
            Assert.That(style, Is.Not.Null);
            Assert.That(iconType, Is.Not.Null);

            MethodInfo set = language.GetMethod("Set", BindingFlags.Public | BindingFlags.Static);
            object previous = language.GetProperty("Current").GetValue(null);
            var root = new GameObject("UtilityButtonTest", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                set.Invoke(null, new[] { Enum.Parse(locale, "English") });
                var button = root.GetComponent<Button>();
                style.GetMethod("Button", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { button, Enum.Parse(iconType, iconName) });
                style.GetMethod("Button", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { button, Enum.Parse(iconType, iconName) });

                Transform tooltip = root.transform.Find("UtilityTooltip");
                Assert.That(tooltip, Is.Not.Null, $"{iconName} should have a shared tooltip.");
                Assert.That(tooltip.GetComponent<Image>().enabled, Is.True,
                    "Restyling a utility button must not hide its tooltip background.");
                Component label = tooltip.Find("Label").GetComponent("TextMeshProUGUI");
                Assert.That(label, Is.Not.Null);
                Assert.That(label.GetType().GetProperty("text").GetValue(label), Is.EqualTo(expected));
            }
            finally
            {
                set.Invoke(null, new[] { previous });
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProfileStatisticValuesUseTheDigitSafeBodyFont()
        {
            Type overlayType = Type.GetType("EchoProtocol.UI.MainMenu.UtilityProfileOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);

            var root = new GameObject("ProfileCanvasTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                overlayType.GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(overlay, null);

                var progress = overlayType.GetField("_progress", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(overlay) as Text;
                var values = overlayType.GetField("_statValues", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(overlay) as Text[];

                Assert.That(progress, Is.Not.Null);
                Assert.That(values, Is.Not.Null.And.Length.EqualTo(4));
                Assert.That(values, Has.All.Matches<Text>(value => value != null && value.font == progress.font),
                    "Numeric profile statistics must use the same digit-safe font as XP/progress text.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalizationPreservesProfileStatisticDigitFont()
        {
            Type overlayType = Type.GetType("EchoProtocol.UI.MainMenu.UtilityProfileOverlay, Assembly-CSharp");
            Type localizationType = Type.GetType("EchoProtocol.UI.LocalizedCanvas, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);
            Assert.That(localizationType, Is.Not.Null);

            var root = new GameObject("ProfileCanvasTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                overlayType.GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(overlay, null);
                var progress = overlayType.GetField("_progress", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(overlay) as Text;
                var values = overlayType.GetField("_statValues", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(overlay) as Text[];
                Component localization = root.AddComponent(localizationType);

                localizationType.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(localization, null);

                Assert.That(values, Has.All.Matches<Text>(value => value != null && value.font == progress.font),
                    "Localization must not replace the digit-safe font selected by the profile overlay.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
