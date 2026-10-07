using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class JammoCharacterSetup
{
    private const string Model = "Assets/Jammo-Character/Models/Jammo_LowPoly.fbx";
    private const string Folder = "Assets/Resources/Characters";
    [MenuItem("ECHO PROTOCOL/Player/Build Jammo Character")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Characters");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var avatar = model.GetComponent<Animator>().avatar;
        if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new Exception("Jammo needs a valid Humanoid avatar.");
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string name in new[] { "a_Idle", "a_Walking", "a_Running" })
            clips[name] = BakeHumanoid(model, avatar, name);
        var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/AC_PlayerCharacter.controller");
        string overridePath = Folder + "/AOC_JammoPlayer.overrideController";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if (controller == null) { controller = new AnimatorOverrideController(baseController); AssetDatabase.CreateAsset(controller, overridePath); }
        controller.runtimeAnimatorController = baseController;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        int replaced = 0;
        for (int i = 0; i < overrides.Count; i++)
        {
            string path = AssetDatabase.GetAssetPath(overrides[i].Key);
            AnimationClip replacement = null;
            if (path == "Assets/Animations/Player/Idle.fbx" || path == Folder + "/a_Idle_Jammo_Humanoid.anim") replacement = clips["a_Idle"];
            else if (path == "Assets/Animations/Player/Walking.fbx" || path == Folder + "/a_Walking_Jammo_Humanoid.anim") replacement = clips["a_Walking"];
            else if (path == "Assets/Animations/Player/Fast Run.fbx" || path == Folder + "/a_Running_Jammo_Humanoid.anim") replacement = clips["a_Running"];
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, replacement);
            if (replacement != null) replaced++;
        }
        if (replaced != 3) throw new Exception("Expected exactly three normal locomotion overrides, got " + replaced);
        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            instance.name = "PF_JammoVisual";
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    string path = Folder + "/M_Jammo_" + shared[i].name.Replace(".", "_") + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        var source = shared[i];
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.name = "M_Jammo_" + source.name;
                        material.SetTexture("_BaseMap", source.mainTexture);
                        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.color : Color.white);
                        if (source.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                        if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
                        if (source.HasProperty("_MetallicGlossMap"))
                        {
                            material.SetTexture("_MetallicGlossMap", source.GetTexture("_MetallicGlossMap"));
                            if (material.GetTexture("_MetallicGlossMap") != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        }
                        AssetDatabase.CreateAsset(material, path);
                    }
                    shared[i] = material;
                }
                renderer.sharedMaterials = shared;
            }
            PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/PF_JammoVisual.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        foreach (string path in new[] { "Assets/Prefabs/Player.prefab", "Assets/Prefabs/PlayerNetwork.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var presenter = root.GetComponent<PlayerCharacterPresenter>() ?? root.AddComponent<PlayerCharacterPresenter>();
                var so = new SerializedObject(presenter);
                so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        ApplySharedLocomotion();
        RefreshAnimationOverride();
        AssetDatabase.SaveAssets();
        Debug.Log("[Jammo] Built Humanoid visual and exactly three locomotion overrides; other clips inherit the existing controller.");
    }

    public static void RefreshAnimationOverride()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Folder + "/AOC_JammoPlayer.overrideController");
        if (controller == null) return;
        ApplySharedLocomotion();
        controller.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/AC_PlayerCharacter.controller");
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            string path = AssetDatabase.GetAssetPath(overrides[i].Key);
            string name = path == "Assets/Animations/Player/Idle.fbx" ? "a_Idle" :
                path == "Assets/Animations/Player/Walking.fbx" ? "a_Walking" :
                path == "Assets/Animations/Player/Fast Run.fbx" ? "a_Running" : null;
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key,
                name == null ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + "_Jammo_Humanoid.anim"));
        }
        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
    }

    private static string MuscleBindingName(string name)
    {
        foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string prefix = side + " " + finger + " ";
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return side + "Hand." + finger + "." + name.Substring(prefix.Length);
            }
        return name;
    }

    private static AnimationClip SharedReplacement(AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (path == Folder + "/a_Idle_Jammo_Humanoid.anim")
            return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Idle.fbx");
        string name = path == "Assets/Animations/Player/Walking.fbx" ? "a_Walking" :
            path == "Assets/Animations/Player/Fast Run.fbx" ? "a_Running" : null;
        return name == null ? clip : AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + "_Jammo_Humanoid.anim") ?? clip;
    }

    private static void UpdateSharedTree(UnityEngine.Motion motion)
    {
        var tree = motion as UnityEditor.Animations.BlendTree;
        if (tree == null) return;
        var children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            var clip = children[i].motion as AnimationClip;
            if (clip != null) children[i].motion = SharedReplacement(clip);
            else UpdateSharedTree(children[i].motion);
        }
        tree.children = children;
        EditorUtility.SetDirty(tree);
    }

    private static void UpdateSharedStates(UnityEditor.Animations.AnimatorStateMachine machine)
    {
        foreach (var child in machine.states)
        {
            var clip = child.state.motion as AnimationClip;
            if (clip != null) child.state.motion = SharedReplacement(clip);
            else UpdateSharedTree(child.state.motion);
            EditorUtility.SetDirty(child.state);
        }
        foreach (var child in machine.stateMachines) UpdateSharedStates(child.stateMachine);
    }

    private static void ApplySharedLocomotion()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animations/Player/AC_PlayerCharacter.controller");
        foreach (var layer in controller.layers) UpdateSharedStates(layer.stateMachine);
        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip BakeHumanoid(GameObject model, Avatar avatar, string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Jammo-Character/Animations/Default/" + name + ".anim");
        var instance = UnityEngine.Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        var sourceAnimator = instance.GetComponent<Animator>();
        float humanScale = sourceAnimator.humanScale;
        var leftFoot = sourceAnimator.GetBoneTransform(UnityEngine.HumanBodyBones.LeftFoot);
        var rightFoot = sourceAnimator.GetBoneTransform(UnityEngine.HumanBodyBones.RightFoot);
        // A bound Animator exposes its cached avatar pose instead of the sampled Generic transforms.
        UnityEngine.Object.DestroyImmediate(sourceAnimator);
        var handler = new HumanPoseHandler(avatar, instance.transform);
        try
        {
            int frames = Mathf.CeilToInt(source.length * 30f);
            var muscles = new AnimationCurve[HumanTrait.MuscleCount];
            for (int i = 0; i < muscles.Length; i++) muscles[i] = new AnimationCurve();
            var root = new AnimationCurve[7];
            for (int i = 0; i < root.Length; i++) root[i] = new AnimationCurve();
            float footHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            float minimumFootHeight = float.PositiveInfinity;
            var pose = new HumanPose();
            Vector3 initial = Vector3.zero;
            Quaternion lastRotation = Quaternion.identity;
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = Mathf.Min(frame / 30f, source.length);
                source.SampleAnimation(instance, time);
                handler.GetHumanPose(ref pose);
                minimumFootHeight = Mathf.Min(minimumFootHeight, leftFoot.position.y, rightFoot.position.y);
                if (frame == 0) initial = pose.bodyPosition;
                for (int i = 0; i < muscles.Length; i++) muscles[i].AddKey(time, pose.muscles[i]);
                Vector3 position = pose.bodyPosition;
                position.x = initial.x; position.z = initial.z;
                Quaternion rotation = pose.bodyRotation;
                if (frame > 0 && Quaternion.Dot(lastRotation, rotation) < 0f)
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                lastRotation = rotation;
                float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w };
                for (int i = 0; i < root.Length; i++) root[i].AddKey(time, values[i]);
            }
            float heightOffset = (footHeight - minimumFootHeight) / humanScale;
            for (int i = 0; i < root[1].length; i++) { var key = root[1].keys[i]; key.value += heightOffset; root[1].MoveKey(i, key); }
            var generated = new AnimationClip { name = name + "_Jammo_Humanoid", frameRate = 30f };
            for (int i = 0; i < muscles.Length; i++) generated.SetCurve("", typeof(Animator), MuscleBindingName(HumanTrait.MuscleName[i]), muscles[i]);
            string[] channels = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < root.Length; i++) generated.SetCurve("", typeof(Animator), channels[i], root[i]);
            generated.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(generated);
            settings.loopTime = true;
            settings.keepOriginalPositionY = true;
            AnimationUtility.SetAnimationClipSettings(generated, settings);
            string path = Folder + "/" + generated.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        finally { handler.Dispose(); UnityEngine.Object.DestroyImmediate(instance); }
    }
}

