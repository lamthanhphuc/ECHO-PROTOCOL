using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.UI.Tests
{
    public class ProfileAvatarTests
    {
        static bool ValidImageSize(byte[] bytes) => (bool)Type.GetType("EchoProtocol.UI.MainMenu.ProfileAvatarEditor, Assembly-CSharp")
            .GetMethod("ValidImageSize").Invoke(null, new object[] { bytes });

        [Test]
        public void CropProducesBoundedPixelsFromSelectedRegion()
        {
            var source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            source.wrapMode = TextureWrapMode.Clamp;
            try
            {
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) source.SetPixel(x, y, x < 4 ? Color.red : Color.blue);
                source.Apply();
                var method = Type.GetType("EchoProtocol.UI.MainMenu.ProfileAvatarEditor, Assembly-CSharp")
                    .GetMethod("CropPixels", BindingFlags.NonPublic | BindingFlags.Static);
                byte[] pixels = (byte[])method.Invoke(null, new object[] { source, new Rect(0, 0, .25f, 1) });
                Assert.That(pixels.Length, Is.EqualTo(256 * 332 * 3));
                Assert.That(pixels[0], Is.GreaterThan(250));
                Assert.That(pixels[pixels.Length - 3], Is.GreaterThan(250));
                Assert.That(pixels[pixels.Length - 1], Is.LessThan(5));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void SharedPortraitOpensCropEditorAndKeepsOriginalFrame()
        {
            var root = new GameObject("Profile", typeof(RectTransform));
            try
            {
                var label = new GameObject("Heading", typeof(UnityEngine.UI.Text));
                label.transform.SetParent(root.transform);
                label.GetComponent<UnityEngine.UI.Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                portrait.transform.SetParent(root.transform);
                portrait.GetComponent<RectTransform>().sizeDelta = new Vector2(278, 360);
                portrait.GetComponent<UnityEngine.UI.Image>().sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/UI/ProfileDossier/OperatorPortrait.png");
                Type type = Type.GetType("EchoProtocol.UI.MainMenu.ProfileAvatarEditor, Assembly-CSharp");
                var editor = portrait.AddComponent(type);
                type.GetMethod("Initialize").Invoke(editor, new object[] { portrait.GetComponent<UnityEngine.UI.Image>() });
                portrait.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var dialog = root.transform.Find("AvatarEditor");
                Assert.That(dialog, Is.Not.Null);
                Assert.That(dialog.gameObject.activeSelf, Is.True);
                var photo = portrait.transform.Find("CustomAvatar").GetComponent<RectTransform>();
                var preview = dialog.Find("CropPreview").GetComponent<RectTransform>();
                Assert.That(preview.sizeDelta, Is.EqualTo(photo.sizeDelta));
                Assert.That(photo.sizeDelta.x / photo.sizeDelta.y, Is.EqualTo(256f / 332).Within(.00001f));
                var frame = portrait.transform.Find("PortraitFrame").GetComponent<UnityEngine.UI.Image>();
                Assert.That(frame.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
                Assert.That(frame.fillCenter, Is.False);
                Assert.That(frame.raycastTarget, Is.False);
                Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(photo.GetSiblingIndex()));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void AcceptsRealPngAndRejectsOversizedHeaderBeforeDecode()
        {
            var texture = new Texture2D(20, 30);
            try
            {
                byte[] png = texture.EncodeToPNG();
                Assert.That(ValidImageSize(png), Is.True);
                png[16] = 127;
                Assert.That(ValidImageSize(png), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void AcceptsRealJpegAndRejectsTruncatedFiles()
        {
            var texture = new Texture2D(40, 20);
            try { Assert.That(ValidImageSize(texture.EncodeToJPG()), Is.True); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            for (int length = 0; length < 32; length++)
                Assert.That(ValidImageSize(new byte[length]), Is.False);
        }
    }
}
