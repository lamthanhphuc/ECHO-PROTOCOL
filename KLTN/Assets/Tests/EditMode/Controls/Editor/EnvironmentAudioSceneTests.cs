using System.Linq;
using EchoProtocol.Audio;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class EnvironmentAudioSceneTests
{
    [TestCase("SciFi")]
    [TestCase("Scene_A")]
    [TestCase("Scene_B")]
    public void AuthoredAnchorsResolveClipsAndOutdoorAnchorsReplaceExistingWaterSources(string map)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + map + ".unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var anchors = roots.SelectMany(r => r.GetComponentsInChildren<EnvironmentAudioAnchor>(true)).ToArray();
            Assert.That(anchors.Length, Is.InRange(1, map == "SciFi" ? 12 : 40));
            var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            var sources = roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToArray();
            foreach (var anchor in anchors)
            {
                Assert.That(catalog.Find(anchor.cue), Is.Not.Null, anchor.cue);
                Assert.That(anchor.gain, Is.InRange(.01f, .3f));
                if (map == "SciFi") continue;
                var expected = catalog.Find(anchor.cue);
                Assert.That(sources.Any(s => s.clip == expected && !s.enabled
                    && (s.transform.position - anchor.transform.position).sqrMagnitude < .01f), Is.True,
                    "Water cue must replace a real authored source, not follow the player.");
            }
            if (map != "SciFi")
                foreach (var source in sources.Where(s => s.clip != null && new[] { "Background", "Wind", "Horn", "DeepRattle", "LeafRustle", "Taps", "WindHowl", "Water" }.Contains(s.clip.name)))
                    Assert.That(source.enabled, Is.False, "Legacy sound must not compete: " + source.name);
            if (map == "SciFi")
            {
                foreach (var source in sources.Where(s => s.clip != null && (s.clip.name == "synthtrack" || s.clip.name == "upbeatfuture")))
                    Assert.That(source.enabled, Is.False, "Demo music must remain off.");
                foreach (var random in roots.SelectMany(r => r.GetComponentsInChildren<PlayRandomSound>(true)))
                    Assert.That(random.enabled, Is.False, "The demo scheduler must not compete with host events.");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
