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
    private const string VisualPrefabPath = "Assets/Stylized3DMonster/Monster08/Prefab/Monster08_04.prefab";
    private const string ModelPath = "Assets/Stylized3DMonster/Monster08/Monster08.fbx";
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
        var names = new[] { "Idle", "Run", "Shoot", "Attack2" };
        var sourcePaths = new[]
        {
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Idle.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Run.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Shoot.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Attack02.anim"
        };
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.layers = Array.Empty<AnimatorControllerLayer>();
        controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < names.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePaths[i]);
            if (clip == null) throw new InvalidOperationException("Missing Monster08 animation clip: " + sourcePaths[i]);

            if (names[i] == "Run")
            {
                MakeLoopingInPlace(clip);
            }

            var state = machine.AddState(names[i]);
            state.motion = clip;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void MakeLoopingInPlace(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path == "root"
                && binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        EditorUtility.SetDirty(clip);
    }

    private static void BuildNetworkPrefab(AnimatorController controller)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(NetworkPrefabPath)
            : new GameObject("PF_CreepMinionNetwork");
        try
        {
            root.transform.localScale = Vector3.one;

            if (root.GetComponent<NetworkObject>() == null)
            {
                root.AddComponent<NetworkObject>();
            }

            if (root.GetComponent<NetworkTransform>() == null)
            {
                root.AddComponent<NetworkTransform>();
            }

            var agent = root.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = root.AddComponent<NavMeshAgent>();
            }

            agent.enabled = false;
            agent.speed = 3.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 24f;
            agent.stoppingDistance = 0.6f;
            agent.radius = 0.35f;
            agent.height = 1.2f;
            agent.baseOffset = 0f;

            var collider = root.GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<CapsuleCollider>();
            }

            collider.radius = 0.4f;
            collider.height = 1.2f;
            collider.center = new Vector3(0f, 1.6f, 0f);

            if (root.GetComponent<CreepMinionRuntime>() == null)
            {
                root.AddComponent<CreepMinionRuntime>();
            }

            var oldVisual = root.transform.Find("Visual");
            if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (visualAsset == null) throw new InvalidOperationException("Missing Creep visual prefab: " + VisualPrefabPath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var existingAnimator in visual.GetComponentsInChildren<Animator>(true))
            {
                UnityEngine.Object.DestroyImmediate(existingAnimator);
            }

            var animatorTarget = visual
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.Find("root") != null);

            if (animatorTarget == null)
                throw new InvalidOperationException("Monster08 animation root was not found.");

            var animator = animatorTarget.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
