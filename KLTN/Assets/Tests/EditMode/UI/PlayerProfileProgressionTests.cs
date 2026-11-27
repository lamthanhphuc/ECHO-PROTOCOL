using System;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.UI.Tests
{
    public sealed class PlayerProfileProgressionTests
    {
        private bool _hadLanguagePreference;
        private int _previousLanguage;

        [SetUp]
        public void SetUp()
        {
            _hadLanguagePreference = PlayerPrefs.HasKey("Echo.Language");
            _previousLanguage = PlayerPrefs.GetInt("Echo.Language");
            SetLanguage(1);
        }

        [Test]
        public void ServerConfirmedLevelUpShowsUnlocksAndLegacyRewardsStayQuiet()
        {
            var dto = Type.GetType("EchoProtocol.Networking.Authority.RewardMeResponseDto, Assembly-CSharp", true);
            var info = Type.GetType("EchoProtocol.UI.Result.ResultRewardInfo, Assembly-CSharp", true);
            var method = info.GetMethod("BuildLevelNotification");
            var reward = JsonUtility.FromJson(@"{""levelBefore"":5,""levelAfter"":6,""unlockedItems"":[{""itemName"":""Cursed Priest""},{""itemName"":""Field Scanner""}]}", dto);
            string text = (string)method.Invoke(null, new[] { reward });
            StringAssert.Contains("6", text); StringAssert.Contains("Cursed Priest", text); StringAssert.Contains("Field Scanner", text);
            var legacy = JsonUtility.FromJson(@"{""currentLevel"":6}", dto);
            Assert.That((string)method.Invoke(null, new[] { legacy }), Is.Empty);
            var same = JsonUtility.FromJson(@"{""levelBefore"":6,""levelAfter"":6}", dto);
            Assert.That((string)method.Invoke(null, new[] { same }), Is.Empty);
        }

        [Test]
        public void ProfileShowsNextUnlockAndScrollableMilestones()
        {
            Apply(@"{""userId"":""test"",""level"":5,""levelCurveVersion"":""LEVEL_V3"",""experiencePoints"":1300,""experienceInLevel"":50,""experienceRequiredForNextLevel"":400,""levelCap"":30,""levelUnlocks"":[{""itemName"":""Cursed Priest"",""requiredLevel"":6,""experienceRemaining"":350},{""itemName"":""Field Scanner"",""requiredLevel"":6,""experienceRemaining"":350},{""itemName"":""Soul Eater"",""requiredLevel"":12,""experienceRemaining"":4075},{""itemName"":""Milestone 15"",""requiredLevel"":15,""experienceRemaining"":5000},{""itemName"":""Milestone 18"",""requiredLevel"":18,""experienceRemaining"":6000},{""itemName"":""Milestone 21"",""requiredLevel"":21,""experienceRemaining"":7000},{""itemName"":""Milestone 24"",""requiredLevel"":24,""experienceRemaining"":8000},{""itemName"":""Milestone 27"",""requiredLevel"":27,""experienceRemaining"":9000}]}");
            var root = new GameObject("Progression UI test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var type = Type.GetType("EchoProtocol.UI.MainMenu.UtilityProfileOverlay, Assembly-CSharp", true);
                var overlay = root.AddComponent(type);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                type.GetMethod("Build", flags).Invoke(overlay, null);
                type.GetMethod("Render", flags).Invoke(overlay, null);
                var labels = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                var next = Array.Find(labels, x => x.name == "NextUnlock");
                StringAssert.Contains("Cursed Priest", next.text); StringAssert.Contains("Field Scanner", next.text); StringAssert.Contains("350", next.text);
                var list = Array.Find(labels, x => x.name == "UnlockList");
                StringAssert.Contains("Soul Eater", list.text);
                var scroll = root.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
                Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Type Session => Type.GetType("EchoProtocol.Profile.PlayerProfileSession, Assembly-CSharp", true);
        [Test]
        public void CosmeticDialogFitsItsPanelAndDoesNotEnableMissingArtwork()
        {
            var root = new GameObject("CosmeticDialogTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                var panel = root.GetComponent<RectTransform>(); panel.sizeDelta = new Vector2(1260, 740);
                var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                portrait.transform.SetParent(root.transform, false);
                var type = Type.GetType("EchoProtocol.Profile.ProfileCosmeticRewardsPanel, Assembly-CSharp", true);
                var component = root.AddComponent(type);
                type.GetMethod("Initialize").Invoke(component, new object[] { portrait.GetComponent<UnityEngine.UI.Image>(), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") });
                Apply(@"{""userId"":""test"",""level"":30,""cosmeticRewards"":[{""rewardKey"":""level15_badge"",""category"":""BADGE"",""displayName"":""Badge"",""requiredLevel"":15,""owned"":true},{""rewardKey"":""level20_frame"",""category"":""PROFILE_FRAME"",""displayName"":""Frame"",""requiredLevel"":20,""owned"":true},{""rewardKey"":""level25_tool"",""category"":""TOOL_SKIN"",""displayName"":""Tool"",""requiredLevel"":25,""owned"":true},{""rewardKey"":""level30_outfit"",""category"":""CHARACTER_OUTFIT"",""displayName"":""Outfit"",""requiredLevel"":30,""owned"":true}]}");
                type.GetMethod("Open", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(component, null);
                var dialog = root.transform.Find("ProgressionCosmeticsDialog/Panel").GetComponent<RectTransform>();
                var buttons = dialog.GetComponentsInChildren<UnityEngine.UI.Button>();
                Assert.That(buttons.Length, Is.EqualTo(5));
                foreach (var button in buttons)
                {
                    if (button.name.StartsWith("Equip")) Assert.That(button.interactable, Is.False);
                    var rect = button.GetComponent<RectTransform>();
                    Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = dialog.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(dialog.rect.xMin, dialog.rect.xMax));
                        Assert.That(local.y, Is.InRange(dialog.rect.yMin, dialog.rect.yMax));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void MilestoneAssetsHaveFourStableEmptySlotsAndSessionClearsAccountCosmetics()
        {
            var type = Type.GetType("EchoProtocol.Profile.ProgressionCosmeticAssets, Assembly-CSharp", true);
            var asset = ScriptableObject.CreateInstance(type);
            try
            {
                var entries = (Array)type.GetField("entries").GetValue(asset);
                Assert.That(entries.Length, Is.EqualTo(4));
                foreach (var entry in entries)
                    Assert.That((bool)entry.GetType().GetProperty("Ready").GetValue(entry), Is.False);
                Apply(@"{""userId"":""milestone-test"",""level"":30,""cosmeticRewards"":[{""rewardKey"":""level15_badge"",""owned"":true,""equipped"":true}]}");
                Assert.That(((Array)Value("CosmeticRewards")).Length, Is.EqualTo(1));
                Session.GetMethod("Clear").Invoke(null, null);
                Assert.That(((Array)Value("CosmeticRewards")).Length, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ConfirmedCosmeticRewardIsShownEvenForRetroactiveCatchupWithoutLevelUp()
        {
            var dto = Type.GetType("EchoProtocol.Networking.Authority.RewardMeResponseDto, Assembly-CSharp", true);
            var method = Type.GetType("EchoProtocol.UI.Result.ResultRewardInfo, Assembly-CSharp", true).GetMethod("BuildLevelNotification");
            var reward = JsonUtility.FromJson(@"{""levelBefore"":30,""levelAfter"":30,""cosmeticRewards"":[{""displayName"":""Echo outfit""}]}", dto);
            StringAssert.Contains("Echo outfit", (string)method.Invoke(null, new[] { reward }));
        }

        private static object Value(string name) => Session.GetProperty(name).GetValue(null);
        private static void Apply(string json)
        {
            var dto = Type.GetType("EchoProtocol.Profile.PlayerProfileDataDto, Assembly-CSharp", true);
            Session.GetMethod("Apply").Invoke(null, new[] { JsonUtility.FromJson(json, dto) });
        }
        [TearDown]
        public void Clear()
        {
            Session.GetMethod("Clear").Invoke(null, null);
            SetLanguage(Mathf.Clamp(_previousLanguage, 0, 1));
            if (!_hadLanguagePreference) PlayerPrefs.DeleteKey("Echo.Language");
            else PlayerPrefs.SetInt("Echo.Language", _previousLanguage);
            PlayerPrefs.Save();
        }

        private static void SetLanguage(int value)
        {
            var language = Type.GetType("EchoProtocol.Settings.GameLanguage, Assembly-CSharp", true);
            var method = language.GetMethod("Set");
            method.Invoke(null, new[] { Enum.ToObject(method.GetParameters()[0].ParameterType, value) });
        }

        [Test]
        public void V3UsesExplicitLevelProgressInsteadOfFiveHundredXp()
        {
            Apply(@"{""userId"":""test"",""level"":7,""levelCurveVersion"":""LEVEL_V3"",""experiencePoints"":2362,""lifetimeExperiencePoints"":3249,""experienceInLevel"":262,""experienceRequiredForNextLevel"":525,""levelCap"":30}");
            Assert.That((float)Value("LevelProgress"), Is.EqualTo(262f / 525).Within(.0001f));
            Assert.That((long)Value("LifetimeExperiencePoints"), Is.EqualTo(3249));
            Assert.That((bool)Value("AtLevelCap"), Is.False);
        }

        [Test]
        public void OlderServerRetainsLinearProgressFallback()
        {
            Apply(@"{""userId"":""test"",""level"":7,""experiencePoints"":3495}");
            Assert.That((long)Value("ExperienceInLevel"), Is.EqualTo(495));
            Assert.That((float)Value("LevelProgress"), Is.EqualTo(.99f).Within(.0001f));
            Assert.That((long)Value("LifetimeExperiencePoints"), Is.EqualTo(3495));
        }

        [TestCase(30)]
        [TestCase(35)]
        public void LevelCapAndGrandfatheredLevelsShowFullBar(int level)
        {
            Apply("{\"userId\":\"test\",\"level\":" + level + ",\"levelCurveVersion\":\"LEVEL_V3\",\"experiencePoints\":40850,\"levelCap\":30}");
            Assert.That((bool)Value("AtLevelCap"), Is.True);
            Assert.That((float)Value("LevelProgress"), Is.EqualTo(1f));
            Assert.That((int)Value("Level"), Is.EqualTo(level));
        }
    }
}
