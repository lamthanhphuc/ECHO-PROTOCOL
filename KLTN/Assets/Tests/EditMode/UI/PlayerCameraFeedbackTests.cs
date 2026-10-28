using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD.Tests
{
    public sealed class PlayerCameraFeedbackTests
    {
        private static Type Feedback => Type.GetType("PlayerCameraFeedback, Assembly-CSharp", true);
        private static object Static(string method, params object[] args) => Feedback.GetMethod(method).Invoke(null, args);

        [Test]
        public void DamageSkipsInitialSnapshotHealingAndUnchangedHealth()
        {
            Assert.That(Static("IsDamage", -1f, 20f), Is.False);
            Assert.That(Static("IsDamage", 20f, 35f), Is.False);
            Assert.That(Static("IsDamage", 35f, 35f), Is.False);
            Assert.That(Static("IsDamage", 100f, 80f), Is.True);
        }

        [Test]
        public void MotionStopsForAirborneDownedModalAndReduceMotion()
        {
            Assert.That(Static("MotionWeight", false, true, true, false, 1f), Is.EqualTo(1f));
            Assert.That(Static("MotionWeight", true, true, true, false, 1f), Is.EqualTo(0f));
            Assert.That(Static("MotionWeight", false, false, true, false, 1f), Is.EqualTo(0f));
            Assert.That(Static("MotionWeight", false, true, false, false, 1f), Is.EqualTo(0f));
            Assert.That(Static("MotionWeight", false, true, true, true, 1f), Is.EqualTo(0f));
            Assert.That(Static("MotionWeight", false, true, true, false, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void SpectateSuppressesOverlayAndNeverBlocksInteraction()
        {
            var camera = new GameObject("Camera feedback test");
            var target = new GameObject("Local target");
            try
            {
                var feedback = camera.AddComponent(Feedback);
                Feedback.GetMethod("Apply").Invoke(feedback, new object[] { target.transform, true, false });
                var image = camera.GetComponentInChildren<Image>(true);
                Assert.That(image, Is.Not.Null);
                Assert.That(image.raycastTarget, Is.False);
                Assert.That(image.sprite.texture.width, Is.EqualTo(128));
                Feedback.GetMethod("Apply").Invoke(feedback, new object[] { target.transform, false, false });
                Assert.That(camera.GetComponentInChildren<Canvas>(true).enabled, Is.False, "Spectate must suppress local damage overlay");
            }
            finally { UnityEngine.Object.DestroyImmediate(camera); UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test]
        public void DamageAndDownedRecoveryPreserveGameplayDirection()
        {
            var camera = new GameObject("Camera damage test");
            var target = new GameObject("Local health test");
            try
            {
                var lifeType = Type.GetType("PlayerDownState, Assembly-CSharp", true);
                var stateType = Type.GetType("PlayerLifeState, Assembly-CSharp", true);
                var life = target.AddComponent(lifeType);
                var snapshot = lifeType.GetMethod("ApplyAuthoritativeSnapshot");
                var feedback = camera.AddComponent(Feedback);
                var apply = Feedback.GetMethod("Apply");
                camera.transform.rotation = Quaternion.Euler(23, 64, 0);
                Vector3 forward = camera.transform.forward;
                snapshot.Invoke(life, new object[] { Enum.Parse(stateType, "Active"), 100f, 0f, 0f, false });
                apply.Invoke(feedback, new object[] { target.transform, true, false });
                snapshot.Invoke(life, new object[] { Enum.Parse(stateType, "Active"), 20f, 0f, 0f, false });
                apply.Invoke(feedback, new object[] { target.transform, true, false });
                Assert.That((float)Feedback.GetField("_hit", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(feedback), Is.GreaterThan(0));
                Assert.That(Vector3.Angle(forward, camera.transform.forward), Is.LessThan(.01f));
                snapshot.Invoke(life, new object[] { Enum.Parse(stateType, "Downed"), 0f, 90f, 0f, false });
                apply.Invoke(feedback, new object[] { target.transform, true, false });
                Assert.That(camera.GetComponentInChildren<Canvas>().enabled, Is.True);
                snapshot.Invoke(life, new object[] { Enum.Parse(stateType, "Active"), 35f, 0f, 0f, false });
                apply.Invoke(feedback, new object[] { target.transform, true, false });
                Assert.That(Vector3.Angle(forward, camera.transform.forward), Is.LessThan(.01f));
            }
            finally { UnityEngine.Object.DestroyImmediate(camera); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
