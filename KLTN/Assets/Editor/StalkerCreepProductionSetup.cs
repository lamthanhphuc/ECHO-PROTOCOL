#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Presentation;
using EchoProtocol.AI.Stalker.Special;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class StalkerCreepProductionSetup
{
    private const string PrefabPath =
        "Assets/Prefabs/StalkerNetwork.prefab";

    private const string CreepModelPath =
        "Assets/Creep Horror Creature/Meshes/Creep_mesh.fbx";

    private const string ControllerPath =
        "Assets/Animations/Stalker/AC_Stalker.controller";

    private static readonly StateDefinition[] States =
    {
        new StateDefinition(
            "Idle1",
            "Creep|Idle1_Action"),

        new StateDefinition(
            "Walk1",
            "Creep|Walk1_Action"),

        new StateDefinition(
            "Walk2",
            "Creep|Walk2_Action"),

        new StateDefinition(
            "Crouch",
            "Creep|Crouch_Action"),

        new StateDefinition(
            "Roar",
            "Creep|Roar_Action"),

        new StateDefinition(
            "Bite",
            "Creep|Bite_Action"),

        new StateDefinition(
            "Sniff",
            "Creep|Sniff_Action"),

        new StateDefinition(
            "Punch",
            "Creep|Punch_Action"),

        new StateDefinition(
            "JumpOut",
            "Creep|JumpOut_Action"),

        new StateDefinition(
            "JumpIn",
            "Creep|JumpIn_Action"),

        new StateDefinition(
            "Death",
            "Creep|Death_Action")
    };

    private static readonly HashSet<string>
        LoopingClipNames =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "Creep|Idle1_Action",
                "Creep|Walk1_Action",
                "Creep|Walk2_Action",
                "Creep|Crouch_Action"
            };

    [MenuItem(
        "Tools/ECHO Protocol/Build Stalker Creep Production Setup")]
    public static void Build()
    {
        try
        {
            ConfigureCreepImporter();

            var clips =
                LoadRequiredClips();

            var controller =
                BuildProductionController(
                    clips);

            WireStalkerPrefab(
                controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[STK_CREEP_SETUP][PASS] " +
                "Production controller and prefab wiring completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            throw;
        }
    }

    private static void ConfigureCreepImporter()
    {
        var importer =
            AssetImporter.GetAtPath(
                CreepModelPath)
            as ModelImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Cannot load ModelImporter: "
                + CreepModelPath);
        }

        var clips =
            importer.clipAnimations;

        if (clips == null
            || clips.Length == 0)
        {
            clips =
                importer.defaultClipAnimations;
        }

        if (clips == null
            || clips.Length == 0)
        {
            throw new InvalidOperationException(
                "Creep FBX contains no importable animation clips.");
        }

        for (var i = 0;
             i < clips.Length;
             i++)
        {
            var clip =
                clips[i];

            clip.loopTime =
                LoopingClipNames.Contains(
                    clip.name);

            clip.loopPose =
                LoopingClipNames.Contains(
                    clip.name);
        }

        importer.clipAnimations =
            clips;

        importer.SaveAndReimport();
    }

    private static Dictionary<string, AnimationClip>
        LoadRequiredClips()
    {
        var importedClips =
            AssetDatabase
                .LoadAllAssetsAtPath(
                    CreepModelPath)
                .OfType<AnimationClip>()
                .Where(
                    clip =>
                        clip != null
                        && !clip.name.StartsWith(
                            "__preview__",
                            StringComparison.Ordinal))
                .ToDictionary(
                    clip => clip.name,
                    clip => clip,
                    StringComparer.Ordinal);

        var result =
            new Dictionary<string, AnimationClip>(
                StringComparer.Ordinal);

        for (var i = 0;
             i < States.Length;
             i++)
        {
            var definition =
                States[i];

            if (!importedClips.TryGetValue(
                    definition.ClipName,
                    out var clip))
            {
                throw new InvalidOperationException(
                    "Missing Creep animation clip: "
                    + definition.ClipName);
            }

            result.Add(
                definition.ClipName,
                clip);
        }

        return result;
    }

    private static AnimatorController
        BuildProductionController(
            IReadOnlyDictionary<string, AnimationClip> clips)
    {
        var controller =
            AssetDatabase.LoadAssetAtPath<
                AnimatorController>(
                    ControllerPath);

        if (controller == null)
        {
            controller =
                AnimatorController
                    .CreateAnimatorControllerAtPath(
                        ControllerPath);
        }

        controller.parameters =
            Array.Empty<AnimatorControllerParameter>();

        //
        // Replace the legacy state-machine reference while
        // keeping the AC_Stalker asset itself and therefore
        // keeping its existing .meta GUID.
        //
        controller.layers =
            Array.Empty<AnimatorControllerLayer>();

        controller.AddLayer(
            "Base Layer");

        var layers =
            controller.layers;

        if (layers == null
            || layers.Length != 1
            || layers[0].stateMachine == null)
        {
            throw new InvalidOperationException(
                "Failed to create Base Layer.");
        }

        var stateMachine =
            layers[0].stateMachine;

        stateMachine.name =
            "Base Layer";

        AnimatorState idleState =
            null;

        for (var i = 0;
             i < States.Length;
             i++)
        {
            var definition =
                States[i];

            var state =
                stateMachine.AddState(
                    definition.StateName);

            state.motion =
                clips[definition.ClipName];

            state.speed = 1f;
            state.cycleOffset = 0f;
            state.mirror = false;
            state.writeDefaultValues = true;

            if (definition.StateName
                == "Idle1")
            {
                idleState =
                    state;
            }
        }

        if (idleState == null)
        {
            throw new InvalidOperationException(
                "Idle1 state was not created.");
        }

        stateMachine.defaultState =
            idleState;

        EditorUtility.SetDirty(
            controller);

        AssetDatabase.SaveAssets();

        return controller;
    }

    private static void WireStalkerPrefab(
        AnimatorController controllerAsset)
    {
        var root =
            PrefabUtility.LoadPrefabContents(
                PrefabPath);

        if (root == null)
        {
            throw new InvalidOperationException(
                "Cannot load Stalker prefab: "
                + PrefabPath);
        }

        try
        {
            var presenter =
                root.GetComponent<
                    StalkerAnimatorPresenter>();

            if (presenter == null)
            {
                presenter =
                    root.AddComponent<
                        StalkerAnimatorPresenter>();
            }

            var presenterSerialized =
                new SerializedObject(
                    presenter);

            var animatorProperty =
                presenterSerialized.FindProperty(
                    "animator");

            var oldAnimator =
                animatorProperty != null
                    ? animatorProperty
                        .objectReferenceValue
                        as Animator
                    : null;

            var oldVisualRoot =
                FindTopLevelVisualRoot(
                    root.transform,
                    oldAnimator);

            var localPosition =
                Vector3.zero;

            var localRotation =
                Quaternion.identity;

            var localScale =
                Vector3.one;

            if (oldVisualRoot != null)
            {
                localPosition =
                    oldVisualRoot.localPosition;

                localRotation =
                    oldVisualRoot.localRotation;

                localScale =
                    oldVisualRoot.localScale;

                UnityEngine.Object.DestroyImmediate(
                    oldVisualRoot.gameObject);
            }

            //
            // Remove a prior migration result if this menu item
            // is deliberately run again.
            //
            var previousCreepVisual =
                root.transform.Find(
                    "CreepVisual");

            if (previousCreepVisual != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    previousCreepVisual.gameObject);
            }

            var creepAsset =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                        CreepModelPath);

            if (creepAsset == null)
            {
                throw new InvalidOperationException(
                    "Cannot load Creep FBX GameObject.");
            }

            var creepVisual =
                PrefabUtility.InstantiatePrefab(
                    creepAsset,
                    root.transform)
                as GameObject;

            if (creepVisual == null)
            {
                throw new InvalidOperationException(
                    "Failed to instantiate Creep visual.");
            }

            creepVisual.name =
                "CreepVisual";

            creepVisual.transform.localPosition =
                localPosition;

            creepVisual.transform.localRotation =
                localRotation;

            creepVisual.transform.localScale =
                localScale;

            var animator =
                creepVisual.GetComponent<Animator>();

            if (animator == null)
            {
                animator =
                    creepVisual.GetComponentInChildren<
                        Animator>(
                            true);
            }

            if (animator == null)
            {
                animator =
                    creepVisual.AddComponent<
                        Animator>();

                var avatar =
                    AssetDatabase
                        .LoadAllAssetsAtPath(
                            CreepModelPath)
                        .OfType<Avatar>()
                        .FirstOrDefault();

                if (avatar != null)
                {
                    animator.avatar =
                        avatar;
                }
            }

            animator.runtimeAnimatorController =
                controllerAsset;

            animator.applyRootMotion =
                false;

            animator.updateMode =
                AnimatorUpdateMode.Normal;

            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;

            var fusionRuntime =
                root.GetComponent<
                    StalkerFusionRuntime>();

            var stalkerController =
                root.GetComponent<
                    StalkerController>();

            var navMeshAgent =
                root.GetComponent<
                    NavMeshAgent>();

            if (fusionRuntime == null
                || stalkerController == null
                || navMeshAgent == null)
            {
                throw new InvalidOperationException(
                    "Stalker root is missing required gameplay components.");
            }

            var specialRuntime =
                root.GetComponent<
                    StalkerSpecialEncounterRuntime>();

            if (specialRuntime == null)
            {
                specialRuntime =
                    root.AddComponent<
                        StalkerSpecialEncounterRuntime>();
            }

            presenterSerialized =
                new SerializedObject(
                    presenter);

            SetObjectReference(
                presenterSerialized,
                "animator",
                animator);

            SetObjectReference(
                presenterSerialized,
                "fusionRuntime",
                fusionRuntime);

            SetObjectReference(
                presenterSerialized,
                "controller",
                stalkerController);

            SetObjectReference(
                presenterSerialized,
                "navMeshAgent",
                navMeshAgent);

            SetRendererArray(
                presenterSerialized,
                "controlledRenderers",
                creepVisual.GetComponentsInChildren<
                    Renderer>(
                        true));

            presenterSerialized
                .ApplyModifiedPropertiesWithoutUndo();

            var fusionSerialized =
                new SerializedObject(
                    fusionRuntime);

            var specialRuntimeProperty =
                fusionSerialized.FindProperty(
                    "specialEncounterRuntime");

            if (specialRuntimeProperty != null)
            {
                specialRuntimeProperty
                    .objectReferenceValue =
                        specialRuntime;

                fusionSerialized
                    .ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(
                animator);

            EditorUtility.SetDirty(
                presenter);

            EditorUtility.SetDirty(
                specialRuntime);

            EditorUtility.SetDirty(
                fusionRuntime);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(
                root);
        }
    }

    private static Transform FindTopLevelVisualRoot(
        Transform prefabRoot,
        Animator animator)
    {
        if (prefabRoot == null
            || animator == null)
        {
            return null;
        }

        var current =
            animator.transform;

        if (current == prefabRoot)
        {
            return null;
        }

        while (current.parent != null
               && current.parent != prefabRoot)
        {
            current =
                current.parent;
        }

        return current.parent == prefabRoot
            ? current
            : null;
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        var property =
            serializedObject.FindProperty(
                propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "Missing serialized property: "
                + propertyName);
        }

        property.objectReferenceValue =
            value;
    }

    private static void SetRendererArray(
        SerializedObject serializedObject,
        string propertyName,
        Renderer[] renderers)
    {
        var property =
            serializedObject.FindProperty(
                propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "Missing serialized property: "
                + propertyName);
        }

        property.arraySize =
            renderers != null
                ? renderers.Length
                : 0;

        for (var i = 0;
             i < property.arraySize;
             i++)
        {
            property
                .GetArrayElementAtIndex(i)
                .objectReferenceValue =
                    renderers[i];
        }
    }

    private readonly struct StateDefinition
    {
        public StateDefinition(
            string stateName,
            string clipName)
        {
            StateName =
                stateName;

            ClipName =
                clipName;
        }

        public string StateName { get; }

        public string ClipName { get; }
    }
}

#endif
