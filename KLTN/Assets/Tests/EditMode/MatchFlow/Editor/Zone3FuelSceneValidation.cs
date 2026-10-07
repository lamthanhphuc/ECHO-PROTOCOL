using System;
using Fusion;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class Zone3FuelSceneValidation
{
    public static string Validate()
    {
        var scene = SceneManager.GetSceneByName("SciFi");
        Assert.That(scene.isLoaded, Is.True);
        var root = GameObject.Find("Zone3FuelSpawnCandidates");
        Assert.That(root, Is.Not.Null);
        var cells = root.GetComponentsInChildren<Zone3FuelCell>(true);
        Assert.That(cells.Length, Is.EqualTo(10));
        GameObject start = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (candidate.gameObject.scene == scene && candidate.name.Trim() == Zone3ConvoyRouteGraph.GetSceneObjectName(Zone3ConvoyRoutePoint.Point01))
            { start = candidate.gameObject; break; }
        Assert.That(start, Is.Not.Null);
        Assert.That(NavMesh.SamplePosition(start.transform.position, out var startHit, 8f, NavMesh.AllAreas), Is.True);
        foreach (var cell in cells)
        {
            Assert.That(cell.GetComponent<NetworkObject>(), Is.Not.Null, cell.name);
            Assert.That(cell.GetComponentInChildren<EnergyCorePickup>(true), Is.Null, cell.name);
            Assert.That(cell.GetComponentInChildren<NetworkPickupItem>(true), Is.Null, cell.name);
            var serialized = new SerializedObject(cell.GetComponent<NetworkObject>());
            var behaviours = serialized.FindProperty("NetworkedBehaviours");
            Assert.That(behaviours.arraySize, Is.EqualTo(1), cell.name);
            Assert.That(behaviours.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(cell), cell.name);
            Assert.That(NavMesh.SamplePosition(cell.transform.position, out var hit, 2f, NavMesh.AllAreas), Is.True, cell.name);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path), Is.True, cell.name);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), cell.name);
        }
        var ship = GameObject.Find("Spacefrigate");
        Assert.That(ship.GetComponentInChildren<Zone3FuelPort>(true), Is.Not.Null);
        Assert.That(ship.GetComponent<Zone3FuelPresentation>(), Is.Not.Null);
        return "10 fuel NetworkObjects have independent behavior and complete NavMesh paths from escort corridor entry; Fuel Port installed.";
    }
}
