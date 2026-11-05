using EchoProtocol.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using EchoProtocol.UI;
using EchoProtocol.UI.Result;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class GameLanguageTests
    {
        private bool _hadPreference;
        private int _previousLanguage;
        [SetUp]
        public void SetUp()
        {
            _hadPreference = PlayerPrefs.HasKey("Echo.Language");
            _previousLanguage = PlayerPrefs.GetInt("Echo.Language");
            GameLanguage.Set(GameLocale.Vietnamese);
        }
        [TearDown]
        public void TearDown()
        {
            GameLanguage.Set((GameLocale)Mathf.Clamp(_previousLanguage, 0, 1));
            if (!_hadPreference) PlayerPrefs.DeleteKey("Echo.Language");
            else PlayerPrefs.SetInt("Echo.Language", _previousLanguage);
            PlayerPrefs.Save();
        }
        [Test]
        public void SelectionIsPersistedAndNotifiedOnce()
        {
            int changes = 0;
            System.Action callback = () => changes++;
            GameLanguage.Changed += callback;
            try
            {
                GameLanguage.Set(GameLocale.English);
                GameLanguage.Set(GameLocale.English);
                Assert.That(PlayerPrefs.GetInt("Echo.Language"), Is.EqualTo(1));
                Assert.That(changes, Is.EqualTo(1));
            }
            finally { GameLanguage.Changed -= callback; }
        }
        [Test]
        public void DynamicCountersTranslateInBothDirectionsWithoutChangingNumbers()
        {
            const string vietnamese = "Lắp Energy Core vào Sector Box    2/4";
            GameLanguage.Set(GameLocale.English);
            string english = GameLanguage.Translate(vietnamese);
            Assert.That(english, Is.EqualTo("Insert Energy Cores into the Sector Box    2/4"));
            GameLanguage.Set(GameLocale.Vietnamese);
            Assert.That(GameLanguage.Translate(english), Is.EqualTo(vietnamese));
        }
        [Test]
        public void RichTextAndMultilineStatusKeepTheirLayout()
        {
            GameLanguage.Set(GameLocale.English);
            Assert.That(GameLanguage.Translate("<b>Nhiệm vụ</b>\nCần cứu · 12s"),
                Is.EqualTo("<b>Objectives</b>\nNeeds revival · 12s"));
        }
        [TestCase("02  DECODE", "02 Giải mã", "02 DECODE")]
        [TestCase("03  SYNC", "03 Đồng bộ", "03 SYNC")]
        [TestCase("SYNC SIGNAL  >", "ĐỒNG BỘ TÍN HIỆU  >", "SYNC SIGNAL  >")]
        [TestCase("EMERGENCY STOP", "DỪNG KHẨN CẤP", "EMERGENCY STOP")]
        [TestCase("THE SIGNAL IS STILL ALIVE", "TÍN HIỆU VẪN ĐANG HOẠT ĐỘNG", "THE SIGNAL IS STILL ALIVE")]
        [TestCase("MAIN POWER RESTORED", "ĐÃ KHÔI PHỤC NGUỒN ĐIỆN CHÍNH", "MAIN POWER RESTORED")]
        [TestCase("FACILITY GRID ONLINE", "LƯỚI ĐIỆN CƠ SỞ ĐÃ HOẠT ĐỘNG", "FACILITY GRID ONLINE")]
        [TestCase("SECURITY LOCKDOWN PROTOCOLS ENGAGED", "ĐÃ KÍCH HOẠT QUY TRÌNH PHONG TỎA AN NINH", "SECURITY LOCKDOWN PROTOCOLS ENGAGED")]
        public void RelaySerializedLabelsTranslateInBothDirections(
            string serialized, string vietnamese, string english)
        {
            Assert.That(GameLanguage.Translate(serialized), Is.EqualTo(vietnamese));
            GameLanguage.Set(GameLocale.English);
            Assert.That(GameLanguage.Translate(vietnamese), Is.EqualTo(english));
            GameLanguage.Set(GameLocale.Vietnamese);
            Assert.That(GameLanguage.Translate(english), Is.EqualTo(vietnamese));
        }

        [TestCase("ROOM_ABC123")]
        [TestCase("NeinMon")]
        [TestCase("01001000")]
        public void UnrecognizedDataIsPreserved(string value)
        {
            GameLanguage.Set(GameLocale.English);
            Assert.That(GameLanguage.Translate(value), Is.EqualTo(value));
        }
        [Test]
        public void ResultPlayerNameSurvivesLanguageChanges()
        {
            var root = new GameObject("ResultRoot", typeof(RectTransform), typeof(Canvas));
            var child = new GameObject("TeamName0", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(root.transform);
            var status = new GameObject("StatusLabel", typeof(RectTransform), typeof(Text));
            status.transform.SetParent(root.transform);
            root.AddComponent<CinematicResultSceneController>();
            var localization = root.AddComponent<LocalizedCanvas>();
            var name = child.GetComponent<Text>();
            var statusText = status.GetComponent<Text>();
            name.text = "READY";
            statusText.text = "READY";
            var update = typeof(LocalizedCanvas).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            try
            {
                update.Invoke(localization, null);
                Assert.That(name.text, Is.EqualTo("READY"));
                Assert.That(statusText.text, Is.EqualTo("SẴN SÀNG"));
                GameLanguage.Set(GameLocale.English);
                update.Invoke(localization, null);
                Assert.That(name.text, Is.EqualTo("READY"));
                Assert.That(statusText.text, Is.EqualTo("READY"));
                GameLanguage.Set(GameLocale.Vietnamese);
                update.Invoke(localization, null);
                Assert.That(name.text, Is.EqualTo("READY"));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
