using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.UI.HUD.Tests
{
    public sealed class PurgeHUDPresentationTests
    {
        private static object Call(string method, params object[] args)
        {
            var type = Type.GetType("EchoProtocol.UI.HUD.HUDPurgePresentation, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "PURGE presentation is missing");
            return type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }

        [Test]
        public void ModalAndResultSuppressGameplayEvenWithCriticalMeters()
        {
            Assert.That(Call("CanShowGameplay", true, true, false, false), Is.EqualTo(false));
            Assert.That(Call("CanShowGameplay", true, false, true, false), Is.EqualTo(false));
            Assert.That(Call("CanShowGameplay", true, false, false, true), Is.EqualTo(false));
            Assert.That(Call("CanShowGameplay", true, false, false, false), Is.EqualTo(true));
            Assert.That(Call("CanShowGameplay", false, false, false, false), Is.EqualTo(false));
        }

        [Test]
        public void CriticalOrRecoveringMeterStaysVisibleUntilFullAndGraceExpires()
        {
            Assert.That(Call("MeterVisible", .2f, false, 10f, 0f), Is.EqualTo(true));
            Assert.That(Call("MeterVisible", .99f, false, 10f, 0f), Is.EqualTo(true));
            Assert.That(Call("MeterVisible", 1f, true, 10f, 0f), Is.EqualTo(true));
            Assert.That(Call("MeterVisible", 1f, false, 10f, 11f), Is.EqualTo(true));
            Assert.That(Call("MeterVisible", 1f, false, 12f, 11f), Is.EqualTo(false));
        }

        [Test]
        public void ExistingPrefabPanelsAcquireMissingCanvasGroup()
        {
            var panel = new GameObject("HUD panel without CanvasGroup");
            try
            {
                var type = Type.GetType("EchoProtocol.UI.HUD.HUDPurgePresentation, Assembly-CSharp");
                var group = type.GetMethod("Group", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { panel });
                Assert.That(group, Is.Not.Null);
                Assert.That(panel.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(panel.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); }
        }

        [Test]
        public void RealPrefabBuildsOnceAndBindsHealthWithoutShowingLegacyVitals()
        {
            var missionType = Type.GetType("EchoProtocol.Networking.AlienHuntMission, Assembly-CSharp");
            var presentationType = Type.GetType("EchoProtocol.UI.HUD.HUDPurgePresentation, Assembly-CSharp");
            var previousMission = missionType.GetProperty("Instance").GetValue(null) as Component;
            var missionObject = new GameObject("Purge HUD test mission");
            var player = new GameObject("Purge HUD test player");
            GameObject hud = null;
            try
            {
                var mission = missionObject.AddComponent(missionType);
                missionType.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(mission, null);
                var prefab = Resources.Load<GameObject>("PF_GameplayHUD_Canvas");
                Assert.That(prefab, Is.Not.Null);
                hud = UnityEngine.Object.Instantiate(prefab);
                var manager = hud.GetComponent("GameplayHUDManager");
                manager.GetType().GetMethod("EnsureSubModuleReferences").Invoke(manager, null);
                var presentation = hud.AddComponent(presentationType);
                presentationType.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(presentation, null);
                var down = player.AddComponent(Type.GetType("PlayerDownState, Assembly-CSharp"));
                var state = Enum.Parse(Type.GetType("PlayerLifeState, Assembly-CSharp"), "Active");
                down.GetType().GetMethod("ApplyAuthoritativeSnapshot").Invoke(down, new object[] { state,30f,90f,0f,false });
                presentationType.GetMethod("BindPlayer").Invoke(presentation, new object[] { player });
                var hotbar = manager.GetType().GetProperty("Hotbar").GetValue(manager) as Component;
                hotbar.GetType().GetMethod("UpdatePurgeSlots", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hotbar, null);
                presentationType.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(presentation, null);
                var vitals = manager.GetType().GetProperty("PlayerVitals").GetValue(manager) as Component;
                var objective = manager.GetType().GetProperty("ObjectiveTracker").GetValue(manager) as Component;
                Assert.That(vitals.gameObject.activeSelf, Is.False);
                Assert.That(objective.gameObject.activeSelf, Is.False);
                Assert.That(hud.transform.Find("PurgeHUD/PurgeHealth"), Is.Not.Null);
                Assert.That(hud.transform.Find("PurgeHUD/PurgeUV"), Is.Not.Null);
                var label = Array.Find(hud.transform.Find("PurgeHUD/PurgeHealth/Label").GetComponents<Component>(),
                    component => component.GetType().FullName == "UnityEngine.UI.Text");
                Assert.That(label, Is.Not.Null);
                string healthText = (string)label.GetType().GetProperty("text").GetValue(label);
                StringAssert.Contains("30%", healthText);
                var heldRect = hud.transform.Find("PurgeHUD/PurgeHeldItem") as RectTransform;
                Assert.That(heldRect.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(heldRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 12f)),
                    "An empty hotbar must not reserve space below the carried-item HUD");
                foreach (Transform slot in hotbar.transform) slot.gameObject.SetActive(true);
                hotbar.gameObject.SetActive(false);
                presentationType.GetMethod("LayoutCorners", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(presentation, null);
                Assert.That(heldRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 12f)),
                    "Children of a hidden hotbar must not reserve corner space");
                int roots = 0;
                foreach (Transform child in hud.transform) if (child.name == "PurgeHUD") roots++;
                Assert.That(roots, Is.EqualTo(1));
            }
            finally
            {
                if (hud != null) UnityEngine.Object.DestroyImmediate(hud);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(missionObject);
                if (previousMission != null)
                    missionType.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(previousMission, null);
            }
        }

        [Test]
        public void ProgressNotificationsDoNotReplayOnJoinOrRollback()
        {
            Assert.That(Call("ProgressAdvanced", -1, 4), Is.EqualTo(false));
            Assert.That(Call("ProgressAdvanced", 4, 4), Is.EqualTo(false));
            Assert.That(Call("ProgressAdvanced", 4, 3), Is.EqualTo(false));
            Assert.That(Call("ProgressAdvanced", 4, 5), Is.EqualTo(true));
        }
    }
}
