using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class AEDV02CabinetCuePlayModeTests
    {
        [UnityTest]
        public IEnumerator AEDV02_MissingClipCannotAcknowledgePresentedWarning()
        {
            var owner = new GameObject("AEDV02_NoCue");
            var type = Type.GetType("EchoProtocol.UI.HUD.HUDCabinetWarning, Assembly-CSharp", true);
            var cue = Activator.CreateInstance(type, owner.transform, null);
            try { Assert.That(type.GetMethod("Present").Invoke(cue, null).ToString(), Is.EqualTo("Unavailable")); }
            finally { ((IDisposable)cue).Dispose(); UnityEngine.Object.Destroy(owner); }
            yield return null;
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AEDV02_ShippedStalkerAudioAndRealCanvasMustPresentBeforeCueReady()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork.prefab");
            var audioType = Type.GetType("EchoProtocol.AI.Stalker.Presentation.StalkerAudioController, Assembly-CSharp", true);
            var audio = prefab.GetComponentInChildren(audioType, true);
            var clip = (AudioClip)audioType.GetProperty("CabinetWarningClip").GetValue(audio);
            Assert.That(clip, Is.Not.Null, "The pilot requires a shipped warning asset.");
            var owner = new GameObject("AEDV02_CuePresentation");
            var type = Type.GetType("EchoProtocol.UI.HUD.HUDCabinetWarning, Assembly-CSharp", true);
            var cue = Activator.CreateInstance(type, owner.transform, clip);
            try
            {
                string status = type.GetMethod("Present").Invoke(cue, null).ToString();
                Assert.That(status, Is.Not.EqualTo("Ready"));
                for (int i = 0; i < 60 && status == "Pending"; i++)
                { yield return null; status = type.GetMethod("Present").Invoke(cue, null).ToString(); }
                Assert.That(status, Is.EqualTo("Ready"));
                var canvas = owner.GetComponentInChildren<Canvas>(); Assert.That(canvas.isActiveAndEnabled, Is.True);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            }
            finally { ((IDisposable)cue).Dispose(); UnityEngine.Object.Destroy(owner); }
            yield return null;
        }
#endif
    }
}
