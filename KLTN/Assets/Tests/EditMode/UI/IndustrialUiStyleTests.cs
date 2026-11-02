using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Tests
{
    public sealed class IndustrialUiStyleTests
    {
        private static Type Style => Type.GetType("EchoProtocol.UI.IndustrialUiStyle, Assembly-CSharp", true);

        [Test]
        public void ButtonsUseSymmetricAccentsAndRestylingDoesNotDuplicateThem()
        {
            var root = new GameObject("TestButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var caption = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            caption.transform.SetParent(root.transform, false);
            try
            {
                MethodInfo apply = Style.GetMethod("Button");
                apply.Invoke(null, new object[] { root.GetComponent<Button>(), false });
                apply.Invoke(null, new object[] { root.GetComponent<Button>(), false });
                var left = root.transform.Find("IndustrialAccentLeft") as RectTransform;
                var right = root.transform.Find("IndustrialAccentRight") as RectTransform;
                Assert.That(left, Is.Not.Null);
                Assert.That(right, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(3));
                Assert.That(left.rect.width, Is.EqualTo(right.rect.width));
                Assert.That(left.offsetMin.y, Is.EqualTo(right.offsetMin.y));
                Assert.That(left.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(right.GetComponent<Image>().raycastTarget, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void LabelPalettePreservesGameplayStatusAndUsesThemeAccentForHeadings()
        {
            var copy = Style.GetMethod("CopyColor");
            Color status = new Color(.2f,.8f,.3f,.5f);
            Assert.That((Color)copy.Invoke(null, new object[] { "HealthStatus", status, false }), Is.EqualTo(status));
            Color accent = (Color)Style.GetField("Accent").GetValue(null);
            Assert.That((Color)copy.Invoke(null, new object[] { "Heading", Color.white, false }), Is.EqualTo(accent));
            Assert.That((Color)copy.Invoke(null, new object[] { "Caption", Color.red, true }), Is.EqualTo(accent));
        }

        [Test]
        public void NewNestedLabelsArePreparedBeforeRenderingEvenWithinTheSameFrame()
        {
            var root = new GameObject("ThemeCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var type = Type.GetType("EchoProtocol.UI.LocalizedCanvas, Assembly-CSharp", true);
                Component localization = root.AddComponent(type);
                MethodInfo prepare = type.GetMethod("PrepareBeforeRender");
                prepare.Invoke(localization, null);
                var panel = new GameObject("Window", typeof(RectTransform));
                panel.transform.SetParent(root.transform, false);
                var heading = new GameObject("Heading", typeof(RectTransform), typeof(Text));
                heading.transform.SetParent(panel.transform, false);
                var label = heading.GetComponent<Text>();
                label.text = "Example";
                label.color = Color.white;
                prepare.Invoke(localization, null);
                Type typography = Type.GetType("EchoProtocol.UI.UiTypographyTheme, Assembly-CSharp", true);
                Assert.That(label.font, Is.EqualTo(typography.GetProperty("BodyLegacy").GetValue(null)));
                Assert.That(label.color, Is.EqualTo((Color)Style.GetField("Accent").GetValue(null)));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
