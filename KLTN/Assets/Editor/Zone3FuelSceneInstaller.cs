using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using Fusion;

public static class Zone3FuelSceneInstaller
{
    [MenuItem("ECHO PROTOCOL/Zone 3/Install Fuel Gameplay")]
    public static void Install()
    {
        var scene = SceneManager.GetSceneByName("SciFi");
        if (!scene.isLoaded) throw new InvalidOperationException("Open SciFi in Edit mode.");
        var ship = GameObject.Find("Spacefrigate");
        if (ship == null || ship.scene != scene) throw new InvalidOperationException("Spacefrigate missing.");
        Undo.RecordObject(ship, "Install Zone 3 fuel");
        if (ship.GetComponent<Zone3ConvoyController>() == null) Undo.AddComponent<Zone3ConvoyController>(ship);
        const string path = "Assets/Resources/Network/PF_Zone3FuelCell.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_EnergyCore_Imported.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(core, scene);
            instance.name = "PF_Zone3FuelCell";
            foreach (var old in instance.GetComponentsInChildren<EnergyCorePickup>(true)) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in instance.GetComponentsInChildren<NetworkPickupItem>(true)) UnityEngine.Object.DestroyImmediate(old);
            var cell = instance.AddComponent<Zone3FuelCell>();
            var net = instance.GetComponent<NetworkObject>();
            var serialized = new SerializedObject(net);
            var behaviours = serialized.FindProperty("NetworkedBehaviours");
            if (behaviours == null) throw new InvalidOperationException("NetworkedBehaviours missing.");
            behaviours.arraySize = 1;
            behaviours.GetArrayElementAtIndex(0).objectReferenceValue = cell;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            int index = 0;
            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    if (!materials.TryGetValue(shared[i], out var fuelMaterial))
                    {
                        string materialPath = "Assets/Materials/Zone3/M_Zone3FuelCell_" + index + ".mat";
                        fuelMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (fuelMaterial == null)
                        {
                            fuelMaterial = new Material(shared[i]);
                            fuelMaterial.name = "M_Zone3FuelCell_" + index;
                            if (fuelMaterial.HasProperty("_BaseColor")) fuelMaterial.SetColor("_BaseColor", new Color(1f, 0.7f, 0.25f));
                            fuelMaterial.EnableKeyword("_EMISSION");
                            if (fuelMaterial.HasProperty("_EmissionColor")) fuelMaterial.SetColor("_EmissionColor", new Color(2.5f, 0.8f, 0.08f));
                            AssetDatabase.CreateAsset(fuelMaterial, materialPath);
                        }
                        materials.Add(shared[i], fuelMaterial);
                        index++;
                    }
                    shared[i] = fuelMaterial;
                }
                renderer.sharedMaterials = shared;
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
        }
        var supplyRoot = GameObject.Find("Zone3FuelSpawnCandidates");
        if (supplyRoot == null)
        {
            supplyRoot = new GameObject("Zone3FuelSpawnCandidates");
            SceneManager.MoveGameObjectToScene(supplyRoot, scene);
            Undo.RegisterCreatedObjectUndo(supplyRoot, "Install fuel candidates");
            supplyRoot.AddComponent<Zone3FuelSupply>();
            Vector3[] candidates = {
                new Vector3(86,1.4f,-449),new Vector3(65,1.4f,-440),
                new Vector3(128,1.4f,-466),new Vector3(118,1.4f,-481),
                new Vector3(33,1.4f,-477),new Vector3(70,1.4f,-478),
                new Vector3(70,1.4f,-518),new Vector3(118,1.4f,-532),
                new Vector3(32,1.4f,-539),new Vector3(17,1.4f,-486)};
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!NavMesh.SamplePosition(candidates[i], out var hit, 2f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Fuel candidate is off NavMesh: " + i);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Z3_FuelCandidate_" + i.ToString("00");
                instance.transform.SetParent(supplyRoot.transform, true);
                instance.transform.position = hit.position + Vector3.up * 0.35f;
                Undo.RegisterCreatedObjectUndo(instance, "Install fuel candidate");
            }
        }
        var port = ship.GetComponentInChildren<Zone3FuelPort>(true);
        if (port == null)
        {
            var portObject = new GameObject("Zone3_FuelPort");
            SceneManager.MoveGameObjectToScene(portObject, scene);
            portObject.transform.SetParent(ship.transform, true);
            var bounds = ship.GetComponent<Collider>().bounds;
            portObject.transform.position = new Vector3(bounds.max.x + 0.4f, bounds.min.y + 1.3f, bounds.center.z);
            var box = portObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.6f, 0.7f, 0.6f);
            box.isTrigger = true;
            port = portObject.AddComponent<Zone3FuelPort>();
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "FuelStatusIndicator";
            indicator.transform.SetParent(portObject.transform, false);
            indicator.transform.localScale = new Vector3(0.15f, 0.45f, 0.35f);
            UnityEngine.Object.DestroyImmediate(indicator.GetComponent<Collider>());
            const string indicatorMaterialPath = "Assets/Materials/Zone3/M_Zone3FuelPort.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(indicatorMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "M_Zone3FuelPort";
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.15f, 1f, 0.4f) * 2f);
                AssetDatabase.CreateAsset(material, indicatorMaterialPath);
            }
            indicator.GetComponent<Renderer>().sharedMaterial = material;
            var lamp = new GameObject("FuelStatusLight");
            lamp.transform.SetParent(portObject.transform, false);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            light.intensity = 1f;
            light.color = Color.green;
            Undo.RegisterCreatedObjectUndo(portObject, "Install fuel port");
        }
        if (ship.GetComponent<Zone3FuelPresentation>() == null) Undo.AddComponent<Zone3FuelPresentation>(ship);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Zone3Fuel] Installed independent prefab, 10 candidate NetworkObjects and hold Fuel Port.");
    }
}
