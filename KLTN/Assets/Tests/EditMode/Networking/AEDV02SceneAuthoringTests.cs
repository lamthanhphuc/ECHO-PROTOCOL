using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDV02SceneAuthoringTests
    {
        [Test]
        public void AEDV02_SceneEntriesAndZoneGatesMatchExportAndProductionReferences()
        {
            const string path = "Assets/Scenes/SciFi.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var source = Type.GetType("EchoProtocol.AI.AED.AEDV02SceneSource, Assembly-CSharp", true);
                var args = new object[] { null, null };
                Assert.That(source.GetMethod("TryLoad").Invoke(null, args), Is.True);
                var manifest = (AEDV02SceneManifest)args[0];
                var markers = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(x => x != null && x.GetType().FullName == "EchoProtocol.AI.AED.AEDZoneEntryMarker").ToArray();
                Assert.That(markers.Length, Is.EqualTo(3));
                foreach (var marker in markers)
                {
                    int zone = (int)marker.GetType().GetProperty("Zone").GetValue(marker);
                    var entry = manifest.Markers.Single(x => x.Id == "Z" + zone + "_ENTRY");
                    Assert.That(new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z), Is.EqualTo(marker.transform.position));
                    Assert.That(manifest.NavCells.Any(x => x.Contains(new[] { entry.X, entry.Y, entry.Z })), Is.True, "Entry outside real mesh: " + zone);
                }
                var gates = (Transform[])source.GetMethod("Zone2ExitGates").Invoke(null, new object[] { scene });
                Assert.That(gates.Length, Is.EqualTo(2));
                var directorType = Type.GetType("EchoProtocol.MatchFlow.Zone2MissionDirector, Assembly-CSharp", true);
                var director = directorType.GetProperty("Instance").GetValue(null);
                Assert.That(director, Is.Not.Null);
                // EditMode does not run the scene director's Awake initialization.
                directorType.GetMethod("ResolveReferences").Invoke(director, null);
                for (int i = 0; i < gates.Length; i++)
                {
                    Assert.That(directorType.GetProperty("DoorBlocker" + (i + 1)).GetValue(director), Is.EqualTo(gates[i].gameObject));
                    string id = (string)source.GetMethod("MarkerId").Invoke(null, new object[] { gates[i] });
                    Assert.That(manifest.DoorIds.Contains(id), Is.True, "Gate source absent: " + id);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
