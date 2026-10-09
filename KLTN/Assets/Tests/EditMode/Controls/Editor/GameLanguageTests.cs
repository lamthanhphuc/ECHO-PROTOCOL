using EchoProtocol.Settings;
using NUnit.Framework;
using UnityEngine;

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
        [TestCase("ROOM_ABC123")]
        [TestCase("NeinMon")]
        [TestCase("01001000")]
        public void UnrecognizedDataIsPreserved(string value)
        {
            GameLanguage.Set(GameLocale.English);
            Assert.That(GameLanguage.Translate(value), Is.EqualTo(value));
        }
    }
}
