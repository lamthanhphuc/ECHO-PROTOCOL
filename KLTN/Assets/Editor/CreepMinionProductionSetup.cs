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
    private const string DisruptorControllerPath = "Assets/Animations/Minion/AC_DisruptorMinion.controller";
    private const string CatfishControllerPath = "Assets/Animations/Minion/AC_CatfishMinion.controller";
    private const string CatfishDirectory = "Assets/Hatogame_new/Catfish";
    private const string Monster10PrefabDirectory = "Assets/Stylized3DMonster/Monster10/Prefab";
    private const string NetworkPrefabPath = "Assets/Resources/PF_CreepMinionNetwork.prefab";

    [MenuItem("Tools/ECHO Protocol/Build Creep Minion Production Setup")]
    public static void Build()
    {
        Directory.CreateDirectory(ControllerDirectory);
        var controller = BuildAnimatorController();
        var disruptorController = BuildDisruptorAnimatorController();
        FixCatfishMaterials();
        var catfishController = BuildCatfishAnimatorController();
        BuildNetworkPrefab(controller, disruptorController, catfishController);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreepMinionProductionSetup][PASS] Controller and network prefab ready.");
    }

    private static AnimatorController BuildAnimatorController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
        {
            EnsureState(existing, "GetHit", "Assets/Stylized3DMonster/Monster08/Anim/Monster08_GetHit.anim");
            EnsureState(existing, "Die", "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Die.anim");
            return existing;
        }
        var names = new[] { "Idle", "Run", "Shoot", "Attack2", "GetHit", "Die" };
        var sourcePaths = new[]
        {
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Idle.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Run.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Shoot.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Attack02.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_GetHit.anim",
            "Assets/Stylized3DMonster/Monster08/Anim/Monster08_Die.anim"
        };
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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
            else if (names[i] == "GetHit")
            {
                MakeLooping(clip);
            }

            var state = machine.AddState(names[i]);
            state.motion = clip;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorController BuildDisruptorAnimatorController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(DisruptorControllerPath);
        if (existing != null)
        {
            EnsureState(existing, "Walk", "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Walk_InPlace.anim");
            EnsureState(existing, "GetHit", "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_GetHit.anim");
            EnsureState(existing, "Die", "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Die.anim");
            return existing;
        }
        var names = new[] { "Idle", "Walk", "Run", "Shoot", "Attack2", "Attack01", "GetHit", "Die" };
        var sourcePaths = new[]
        {
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Idle.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Walk_InPlace.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Run_InPlace.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Shoot.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Attack02_InPlace.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Attack01_InPlace.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_GetHit.anim",
            "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_Die.anim"
        };
        var controller = AnimatorController.CreateAnimatorControllerAtPath(DisruptorControllerPath);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.layers = Array.Empty<AnimatorControllerLayer>();
        controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < names.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePaths[i]);
            if (clip == null) throw new InvalidOperationException("Missing Monster10 animation clip: " + sourcePaths[i]);
            if (names[i] == "Walk" || names[i] == "Run") MakeLoopingInPlace(clip);
            else if (names[i] == "GetHit") MakeLooping(clip);
            var state = machine.AddState(names[i]);
            state.motion = clip;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorController BuildCatfishAnimatorController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CatfishControllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(CatfishControllerPath);
        var machine = controller.layers[0].stateMachine;
        var names = new[] { "Idle", "Run", "Attack01", "GetHit", "Die", "Walk" };
        var sources = new[] { "idle", "run", "attack1", "hit", "die", "walk" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = $"{CatfishDirectory}/Animation/Catfish@{sources[i]}.FBX";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing Catfish animation: " + path);
            if (names[i] == "Idle" || names[i] == "Run" || names[i] == "Walk" || names[i] == "GetHit")
            {
                var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                if (clips.Any(clip => !clip.loopTime))
                {
                    foreach (var clip in clips) clip.loopTime = true;
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }
            var motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == sources[i]);
            if (motion == null) throw new InvalidOperationException("Missing Catfish clip: " + path);
            var state = machine.states.FirstOrDefault(child => child.state.name == names[i]).state
                ?? machine.AddState(names[i]);
            state.motion = motion;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void FixCatfishMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
        foreach (string variant in new[] { "A", "B" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{CatfishDirectory}/Material/Catfish{variant}_Mat.mat");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CatfishDirectory}/Textures/Catfish{(variant == "A" ? "" : "B")}_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CatfishDirectory}/Textures/Catfish_Normal.png");
            if (material == null || albedo == null || normal == null)
                throw new InvalidOperationException("Missing Catfish material or texture: " + variant);
            if (material.shader != shader) material.shader = shader;
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }
    }

    private static void EnsureState(AnimatorController controller, string stateName, string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) throw new InvalidOperationException("Missing animation clip: " + clipPath);
        if (stateName == "GetHit") MakeLooping(clip);
        else if (stateName == "Walk") MakeLoopingInPlace(clip);

        var machine = controller.layers[0].stateMachine;
        if (machine.states.Any(child => child.state.name == stateName)) return;
        machine.AddState(stateName).motion = clip;
        EditorUtility.SetDirty(controller);
    }

    private static void MakeLooping(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
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

    private static void BuildNetworkPrefab(AnimatorController controller, AnimatorController disruptorController,
        AnimatorController catfishController)
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
            agent.height = 1f;
            agent.baseOffset = 0f;

            var collider = root.GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<CapsuleCollider>();
            }

            collider.radius = 0.4f;
            collider.height = 1f;
            collider.center = new Vector3(0f, 0.5f, 0f);

            if (root.GetComponent<CreepMinionRuntime>() == null)
            {
                root.AddComponent<CreepMinionRuntime>();
            }

            var oldVisual = root.transform.Find("Visual");
            if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
            var oldVariants = root.transform.Find("VisualVariants");
            if (oldVariants != null) UnityEngine.Object.DestroyImmediate(oldVariants.gameObject);
            var variants = new GameObject("VisualVariants").transform;
            variants.SetParent(root.transform, false);
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (visualAsset == null) throw new InvalidOperationException("Missing Creep visual prefab: " + VisualPrefabPath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, variants);
            visual.name = "Original";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            ConfigureAnimator(visual, controller, "Monster08");

            for (int i = 1; i <= 15; i++)
            {
                string path = $"{Monster10PrefabDirectory}/Monster10_{i:00}.prefab";
                var variantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (variantAsset == null) throw new InvalidOperationException("Missing Monster10 visual prefab: " + path);
                var variant = (GameObject)PrefabUtility.InstantiatePrefab(variantAsset, variants);
                variant.name = $"Monster10_{i:00}";
                variant.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                variant.transform.localScale = Vector3.one;
                ConfigureAnimator(variant, disruptorController, "Monster10");
                variant.SetActive(false);
            }

            foreach (string name in new[] { "CatfishA", "CatfishB" })
            {
                var catfishAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{CatfishDirectory}/Prefabs/{name}.prefab");
                if (catfishAsset == null) throw new InvalidOperationException("Missing Catfish prefab: " + name);
                var variant = (GameObject)PrefabUtility.InstantiatePrefab(catfishAsset, variants);
                variant.name = name;
                variant.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                variant.transform.localScale = Vector3.one;
                var animator = variant.GetComponent<Animator>();
                if (animator == null) throw new InvalidOperationException("Missing Catfish Animator: " + name);
                animator.runtimeAnimatorController = catfishController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                variant.SetActive(false);
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

    private static void ConfigureAnimator(GameObject visual, AnimatorController controller, string modelName)
    {
        foreach (var existingAnimator in visual.GetComponentsInChildren<Animator>(true))
            UnityEngine.Object.DestroyImmediate(existingAnimator);
        var animatorTarget = visual.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.Find("root") != null);
        if (animatorTarget == null) throw new InvalidOperationException(modelName + " animation root was not found.");
        var animator = animatorTarget.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }
}
#endif
