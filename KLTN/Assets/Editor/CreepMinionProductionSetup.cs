#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using EchoProtocol.AI.Minions;
using Fusion;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class CreepMinionProductionSetup
{
    private const string VisualPrefabPath = "Assets/Creep Horror Creature/Prefabs/Creep2.prefab";
    private const string ModelPath = "Assets/Creep Horror Creature/Meshes/Creep_mesh.fbx";
    private const string ControllerDirectory = "Assets/Animations/Minion";
    private const string ControllerPath = "Assets/Animations/Minion/AC_CreepMinion.controller";
    private const string NetworkPrefabPath = "Assets/Resources/PF_CreepMinionNetwork.prefab";

    [MenuItem("Tools/ECHO Protocol/Build Creep Minion Production Setup")]
    public static void Build()
    {
        Directory.CreateDirectory(ControllerDirectory);
        var controller = BuildAnimatorController();
        BuildNetworkPrefab(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreepMinionProductionSetup][PASS] Controller and network prefab ready.");
    }

    private static AnimatorController BuildAnimatorController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        var names = new[] { "Idle", "Walk", "Bite", "Roar" };
        var sourceNames = new[] { "Creep|Idle1_Action", "Creep|Walk1_Action",
            "Creep|Bite_Action", "Creep|Roar_Action" };
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.layers = Array.Empty<AnimatorControllerLayer>();
        controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < names.Length; i++)
        {
            var clip = clips.FirstOrDefault(c => c.name == sourceNames[i]);
            if (clip == null) throw new InvalidOperationException("Missing Creep animation clip: " + sourceNames[i]);
            var state = machine.AddState(names[i]);
            state.motion = clip;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void BuildNetworkPrefab(AnimatorController controller)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(NetworkPrefabPath)
            : new GameObject("PF_CreepMinionNetwork");
        try
        {
            if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
            if (root.GetComponent<NetworkTransform>() == null) root.AddComponent<NetworkTransform>();
            var agent = root.GetComponent<NavMeshAgent>() ?? root.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 24f;
            agent.stoppingDistance = 0.6f;
            agent.radius = 0.35f;
            agent.height = 1.2f;
            agent.baseOffset = 0f;
            var collider = root.GetComponent<CapsuleCollider>() ?? root.AddComponent<CapsuleCollider>();
            collider.radius = 0.35f;
            collider.height = 1.2f;
            collider.center = new Vector3(0f, 0.6f, 0f);
            if (root.GetComponent<CreepMinionRuntime>() == null) root.AddComponent<CreepMinionRuntime>();

            var oldVisual = root.transform.Find("Visual");
            if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (visualAsset == null) throw new InvalidOperationException("Missing Creep visual prefab: " + VisualPrefabPath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            PrefabUtility.SaveAsPrefabAsset(root, NetworkPrefabPath);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NetworkPrefabPath);
            var labels = AssetDatabase.GetLabels(asset);
            if (!labels.Contains("FusionPrefab"))
            {
                ArrayUtility.Add(ref labels, "FusionPrefab");
                AssetDatabase.SetLabels(asset, labels);
            }
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
