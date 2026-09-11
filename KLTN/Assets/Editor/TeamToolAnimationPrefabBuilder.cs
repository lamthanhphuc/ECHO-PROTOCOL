using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class TeamToolAnimationPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_team_tool_animation_prefab_builder";
    private const string AnimationRoot = "Assets/Animations/TeamTools";
    private const string CoreAnimationFolder = AnimationRoot + "/CoreStabilizer";
    private const string AnimatedPrefabFolder = "Assets/Prefabs/Environment/Teamtoools/Animated";

    private const string CoreVisualPath = "Assets/Prefabs/Environment/Teamtoools/PF_CoreStabilizer_Device_Visual.prefab";
    private const string CorePrefabPath = AnimatedPrefabFolder + "/PF_CoreStabilizer_Device_Animated.prefab";
    private const string ParticleMaterialPath = "Assets/Materials/TeamTools/M_CoreStabilizer_Particles.mat";
    private const string FieldMaterialPath = "Assets/Materials/TeamTools/M_CoreStabilizer_Field_URP.mat";

    private const string CoreControllerPath = CoreAnimationFolder + "/AC_CoreStabilizer_Device.controller";

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ToAbsolutePath(RunMarker))) return;
            File.Delete(ToAbsolutePath(RunMarker));

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[TeamToolAnimationBuilder] Exit Play Mode, recreate the marker, then rebuild.");
                return;
            }

            BuildAll();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Team Tool Device Animations")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[TeamToolAnimationBuilder] Cannot build while Unity is in Play Mode.");
            return;
        }

        EnsureFolder(CoreAnimationFolder);
        EnsureFolder(AnimatedPrefabFolder);
        PreserveAndMoveExistingCoreClips();
        Material fieldMaterial = CreateFieldMaterial();

        AnimationClip coreIdle = ConfigureCoreIdle(GetOrCreateClip(CoreAnimationFolder + "/AN_CoreStabilizer_Idle.anim"));
        AnimationClip coreActivate = ConfigureCoreActivate(GetOrCreateClip(CoreAnimationFolder + "/AN_CoreStabilizer_Activate.anim"));
        AnimationClip coreActive = ConfigureCoreActive(GetOrCreateClip(CoreAnimationFolder + "/AN_CoreStabilizer_Active.anim"));
        AnimationClip coreDeactivate = ConfigureCoreDeactivate(GetOrCreateClip(CoreAnimationFolder + "/AN_CoreStabilizer_Deactivate.anim"));
        AnimatorController coreController = GetOrCreateController(CoreControllerPath);
        ConfigureBoolController(coreController, "IsActive", coreIdle, coreActivate, coreActive, coreDeactivate,
            "Idle", "Activate", "Active", "Deactivate");
        BuildCoreWrapper(coreController, fieldMaterial);
        ValidateAnimationBindings(CorePrefabPath, coreController, coreIdle, coreActivate, coreActive, coreDeactivate);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TeamToolAnimationBuilder] Built Core Stabilizer device animation prefab.");
    }

    private static AnimationClip ConfigureCoreIdle(AnimationClip clip)
    {
        ResetClip(clip, true);
        SetScale(clip, "Visual/GripPivot/DeviceModel", new[]
        {
            Key(0f, 1f), Key(1f, 1.01f), Key(2f, 1f),
        });
        SetCurve(clip, "Visual/GripPivot/DeviceModel", typeof(Transform), "localEulerAnglesRaw.z",
            SmoothCurve(Key(0f, -0.5f), Key(1f, 0.5f), Key(2f, -0.5f)));
        SetLightIntensity(clip, "Visual/DeviceVFXOrigin/StatusLight",
            Key(0f, 0.4f), Key(1f, 0.8f), Key(2f, 0.4f));
        SetUniformScale(clip, "SupportFieldVFX", Key(0f, 0.01f), Key(2f, 0.01f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip ConfigureCoreActivate(AnimationClip clip)
    {
        ResetClip(clip, false);
        SetScale(clip, "Visual/GripPivot/DeviceModel", new[]
        {
            Key(0f, 1f), Key(0.15f, 0.97f), Key(0.4f, 1.05f), Key(0.6f, 1f),
        });
        SetCurve(clip, "Visual/GripPivot/DeviceModel", typeof(Transform), "localEulerAnglesRaw.z",
            LinearCurve(Key(0f, 0f), Key(0.2f, -2f), Key(0.3f, 2f), Key(0.4f, -1f), Key(0.6f, 0f)));
        SetLightIntensity(clip, "Visual/DeviceVFXOrigin/StatusLight",
            Key(0f, 0.5f), Key(0.2f, 1f), Key(0.45f, 3f), Key(0.6f, 1.5f));
        SetUniformScale(clip, "SupportFieldVFX",
            Key(0f, 0.01f), Key(0.18f, 0.15f), Key(0.45f, 1.08f), Key(0.6f, 1f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip ConfigureCoreActive(AnimationClip clip)
    {
        ResetClip(clip, true);
        SetScale(clip, "Visual/GripPivot/DeviceModel", new[]
        {
            Key(0f, 1f), Key(0.5f, 1.025f), Key(1f, 1f),
        });
        SetCurve(clip, "Visual/GripPivot/DeviceModel", typeof(Transform), "localEulerAnglesRaw.y",
            SmoothCurve(Key(0f, 0f), Key(0.5f, 3f), Key(1f, 0f)));
        SetLightIntensity(clip, "Visual/DeviceVFXOrigin/StatusLight",
            Key(0f, 1.4f), Key(0.5f, 2.2f), Key(1f, 1.4f));
        SetUniformScale(clip, "SupportFieldVFX", Key(0f, 1f), Key(0.5f, 1.04f), Key(1f, 1f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip ConfigureCoreDeactivate(AnimationClip clip)
    {
        ResetClip(clip, false);
        SetScale(clip, "Visual/GripPivot/DeviceModel", new[]
        {
            Key(0f, 1f), Key(0.15f, 0.96f), Key(0.4f, 1f),
        });
        SetCurve(clip, "Visual/GripPivot/DeviceModel", typeof(Transform), "localEulerAnglesRaw.z",
            SmoothCurve(Key(0f, 0f), Key(0.2f, -1f), Key(0.4f, 0f)));
        SetLightIntensity(clip, "Visual/DeviceVFXOrigin/StatusLight",
            Key(0f, 1.5f), Key(0.2f, 0.2f), Key(0.4f, 0.4f));
        SetUniformScale(clip, "SupportFieldVFX",
            Key(0f, 1f), Key(0.2f, 0.65f), Key(0.4f, 0.01f));
        FinishClip(clip);
        return clip;
    }

    private static void BuildCoreWrapper(RuntimeAnimatorController controller, Material fieldMaterial)
    {
        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoreVisualPath);
        if (visualPrefab == null) throw new FileNotFoundException("Missing Core Stabilizer visual prefab", CoreVisualPath);

        GameObject root = CreateAnimatedRoot("PF_CoreStabilizer_Device_Animated", controller);
        InstantiateVisual(visualPrefab, root.transform);
        CreateSupportFieldVfx(root.transform, fieldMaterial);
        SaveWrapper(root, CorePrefabPath);
    }

    private static Material CreateFieldMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FieldMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "M_CoreStabilizer_Field_URP" };
            AssetDatabase.CreateAsset(material, FieldMaterialPath);
        }

        Color color = new Color(0.08f, 0.8f, 1f, 0.24f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateSupportFieldVfx(Transform parent, Material material)
    {
        const float radius = 2.5f;
        const int segments = 96;

        var field = new GameObject("SupportFieldVFX");
        field.transform.SetParent(parent, false);
        field.transform.localPosition = new Vector3(0f, 0.025f, 0f);
        field.transform.localScale = Vector3.one * 0.01f;

        var ringObject = new GameObject("GroundRing_2_5m");
        ringObject.transform.SetParent(field.transform, false);
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = material;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.startWidth = 0.035f;
        ring.endWidth = 0.035f;
        ring.startColor = new Color(0.08f, 0.8f, 1f, 0.24f);
        ring.endColor = ring.startColor;
        ring.numCornerVertices = 2;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        var particlesObject = new GameObject("FieldBoundaryParticles");
        particlesObject.transform.SetParent(field.transform, false);
        particlesObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 1.5f;
        main.loop = true;
        main.startLifetime = 1.1f;
        main.startSpeed = 0.025f;
        main.startSize = 0.035f;
        main.startColor = new Color(0.1f, 0.85f, 1f, 0.35f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission;
        emission.rateOverTime = 14f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.03f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static GameObject CreateAnimatedRoot(string name, RuntimeAnimatorController controller)
    {
        var root = new GameObject(name);
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return root;
    }

    private static GameObject InstantiateVisual(GameObject source, Transform parent)
    {
        GameObject visual = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
        if (visual == null) throw new MissingReferenceException("Could not instantiate visual prefab: " + source.name);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        return visual;
    }

    private static void SaveWrapper(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (prefab == null) throw new IOException("Could not save animated Team Tool prefab: " + path);
    }

    private static void ValidateAnimationBindings(string prefabPath, RuntimeAnimatorController controller, params AnimationClip[] clips)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Animator animator = prefab != null ? prefab.GetComponent<Animator>() : null;
        if (animator == null || animator.runtimeAnimatorController != controller)
        {
            throw new MissingReferenceException("Animated prefab is missing its expected Animator Controller: " + prefabPath);
        }

        int bindingCount = 0;
        foreach (AnimationClip clip in clips)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0) throw new MissingReferenceException("Animation clip has no curves: " + clip.name);

            foreach (EditorCurveBinding binding in bindings)
            {
                Transform target = prefab.transform.Find(binding.path);
                if (target == null)
                {
                    throw new MissingReferenceException(clip.name + " has an invalid path: " + binding.path);
                }
                if (binding.type != typeof(Transform) && target.GetComponent(binding.type) == null)
                {
                    throw new MissingComponentException(clip.name + " cannot find " + binding.type.Name + " at " + binding.path);
                }
                bindingCount++;
            }
        }

        Debug.Log("[TeamToolAnimationBuilder] Validated " + clips.Length + " clips and " + bindingCount + " bindings for " + prefab.name + ".");
    }

    private static void ConfigureBoolController(
        AnimatorController controller,
        string parameter,
        AnimationClip idleClip,
        AnimationClip startClip,
        AnimationClip activeClip,
        AnimationClip stopClip,
        string idleName,
        string startName,
        string activeName,
        string stopName)
    {
        while (controller.parameters.Length > 0)
        {
            controller.RemoveParameter(0);
        }
        controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            stateMachine.RemoveState(child.state);
        }
        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(child.stateMachine);
        }

        AnimatorState idle = AddState(stateMachine, idleName, idleClip, new Vector3(220f, 80f));
        AnimatorState start = AddState(stateMachine, startName, startClip, new Vector3(460f, 80f));
        AnimatorState active = AddState(stateMachine, activeName, activeClip, new Vector3(700f, 80f));
        AnimatorState stop = AddState(stateMachine, stopName, stopClip, new Vector3(700f, 230f));
        stateMachine.defaultState = idle;

        AddBoolTransition(idle, start, parameter, true);
        AddExitTransition(start, active, 0.95f);
        AddBoolTransition(active, stop, parameter, false);
        AddExitTransition(stop, idle, 0.95f);
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
    }

    private static AnimatorController GetOrCreateController(string path)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        return controller != null ? controller : AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    private static AnimationClip GetOrCreateClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;
        clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path), frameRate = 60f };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void ResetClip(AnimationClip clip, bool loop)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationUtility.SetEditorCurve(clip, binding, null);
        }
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }

        clip.frameRate = 60f;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static void FinishClip(AnimationClip clip)
    {
        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
    }

    private static void SetScale(AnimationClip clip, string path, Keyframe[] keys)
    {
        SetUniformScale(clip, path, keys);
    }

    private static void SetUniformScale(AnimationClip clip, string path, params Keyframe[] keys)
    {
        SetCurve(clip, path, typeof(Transform), "m_LocalScale.x", SmoothCurve(keys));
        SetCurve(clip, path, typeof(Transform), "m_LocalScale.y", SmoothCurve(keys));
        SetCurve(clip, path, typeof(Transform), "m_LocalScale.z", SmoothCurve(keys));
    }

    private static void SetLightIntensity(AnimationClip clip, string path, params Keyframe[] keys)
    {
        SetCurve(clip, path, typeof(Light), "m_Intensity", SmoothCurve(keys));
    }

    private static void SetCurve(AnimationClip clip, string path, System.Type type, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static Keyframe Key(float time, float value)
    {
        return new Keyframe(time, value);
    }

    private static AnimationCurve SmoothCurve(params Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    private static AnimationCurve LinearCurve(params Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static void PreserveAndMoveExistingCoreClips()
    {
        MoveIfDestinationMissing(
            AnimatedPrefabFolder + "/AN_CoreStabilizer_Idle.anim",
            CoreAnimationFolder + "/AN_CoreStabilizer_Idle.anim");
        MoveIfDestinationMissing(
            AnimatedPrefabFolder + "/AN_CoreStabilizer_Activate.anim",
            CoreAnimationFolder + "/AN_CoreStabilizer_Activate.anim");
    }

    private static void MoveIfDestinationMissing(string source, string destination)
    {
        if (AssetDatabase.LoadMainAssetAtPath(source) == null || AssetDatabase.LoadMainAssetAtPath(destination) != null) return;
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[TeamToolAnimationBuilder] Could not move " + source + ": " + error);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
    }
}
