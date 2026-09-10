using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MotionDecoyHologramPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_motion_decoy_hologram_prefab_builder";
    private const string AnimationFolder = "Assets/Animations/TeamTools/MotionDecoy/Hologram";
    private const string MaterialFolder = "Assets/Materials/TeamTools";
    private const string PrefabFolder = "Assets/Prefabs/Environment/Teamtoools/Animated";
    private const string PlayerVisualSourcePath = "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab";
    private const string PlayerIdlePath = "Assets/Animations/Player/Player_Idle.anim";
    private const string PlayerWalkPath = "Assets/Animations/Player/Player_Walk_Forward.anim";
    private const string HologramMaterialPath = MaterialFolder + "/M_MotionDecoy_Hologram_URP.mat";
    private const string VfxControllerPath = AnimationFolder + "/AC_MotionDecoy_Hologram_VFX.controller";
    private const string BodyControllerPath = AnimationFolder + "/AC_MotionDecoy_Hologram_Body.controller";
    private const string PrefabPath = PrefabFolder + "/PF_MotionDecoy_Hologram_Visual.prefab";

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ToAbsolutePath(RunMarker))) return;
            File.Delete(ToAbsolutePath(RunMarker));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[MotionDecoyHologramBuilder] Exit Play Mode before building the hologram prefab.");
                return;
            }
            Build();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Motion Decoy Hologram Visual")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[MotionDecoyHologramBuilder] Cannot build while Unity is in Play Mode.");
            return;
        }

        EnsureFolder(AnimationFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        Material hologramMaterial = CreateHologramMaterial();
        AnimationClip boot = ConfigureBoot(GetOrCreateClip(AnimationFolder + "/AN_Hologram_Boot.anim"));
        AnimationClip idleVfx = ConfigureIdleVfx(GetOrCreateClip(AnimationFolder + "/AN_Hologram_Idle.anim"));
        AnimationClip dissolve = ConfigureDissolve(GetOrCreateClip(AnimationFolder + "/AN_Hologram_Dissolve.anim"));

        AnimatorController vfxController = GetOrCreateController(VfxControllerPath);
        ConfigureVfxController(vfxController, boot, idleVfx, dissolve);

        AnimatorController bodyController = GetOrCreateController(BodyControllerPath);
        ConfigureBodyController(bodyController);

        BuildPrefab(hologramMaterial, vfxController, bodyController);
        ValidatePrefab(vfxController, bodyController, boot, idleVfx, dissolve);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log("[MotionDecoyHologramBuilder] Built hologram visual prefab with Boot, Idle, Dissolve and body Idle/Walk animation.");
    }

    private static Material CreateHologramMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "M_MotionDecoy_Hologram_URP" };
            AssetDatabase.CreateAsset(material, HologramMaterialPath);
        }

        Color color = new Color(0.08f, 0.78f, 1f, 0.58f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(0.1f, 1.4f, 2.2f, 1f));
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AnimationClip ConfigureBoot(AnimationClip clip)
    {
        ResetClip(clip, false);
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.x",
            SmoothCurve(Key(0f, 0.92f), Key(0.42f, 1.03f), Key(0.55f, 1f)));
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.y",
            SmoothCurve(Key(0f, 0.01f), Key(0.16f, 0.35f), Key(0.42f, 1.08f), Key(0.55f, 1f)));
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.z",
            SmoothCurve(Key(0f, 0.92f), Key(0.42f, 1.03f), Key(0.55f, 1f)));
        SetCurve(clip, "HologramLight", typeof(Light), "m_Intensity",
            SmoothCurve(Key(0f, 0f), Key(0.18f, 0.8f), Key(0.42f, 2.5f), Key(0.55f, 1.1f)));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip ConfigureIdleVfx(AnimationClip clip)
    {
        ResetClip(clip, true);
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.x",
            SmoothCurve(Key(0f, 1f), Key(0.6f, 1.006f), Key(1.2f, 1f)));
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.y",
            SmoothCurve(Key(0f, 1f), Key(0.6f, 1.012f), Key(1.2f, 1f)));
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.z",
            SmoothCurve(Key(0f, 1f), Key(0.6f, 1.006f), Key(1.2f, 1f)));
        SetCurve(clip, "HologramLight", typeof(Light), "m_Intensity",
            SmoothCurve(Key(0f, 0.8f), Key(0.6f, 1.25f), Key(1.2f, 0.8f)));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip ConfigureDissolve(AnimationClip clip)
    {
        ResetClip(clip, false);
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.x",
            SmoothCurve(Key(0f, 1f), Key(0.22f, 1.04f), Key(0.4f, 0.92f)));
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.y",
            SmoothCurve(Key(0f, 1f), Key(0.15f, 0.86f), Key(0.4f, 0.01f)));
        SetCurve(clip, "CharacterVisual", typeof(Transform), "m_LocalScale.z",
            SmoothCurve(Key(0f, 1f), Key(0.22f, 1.04f), Key(0.4f, 0.92f)));
        SetCurve(clip, "HologramLight", typeof(Light), "m_Intensity",
            SmoothCurve(Key(0f, 1.1f), Key(0.12f, 2.2f), Key(0.24f, 0.25f), Key(0.4f, 0f)));
        FinishClip(clip);
        return clip;
    }

    private static void ConfigureVfxController(AnimatorController controller, AnimationClip boot, AnimationClip idle, AnimationClip dissolve)
    {
        ClearController(controller);
        controller.AddParameter("IsVisible", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState hidden = AddState(machine, "Hidden", null, new Vector3(180f, 80f));
        AnimatorState bootState = AddState(machine, "Boot", boot, new Vector3(420f, 80f));
        AnimatorState idleState = AddState(machine, "Idle", idle, new Vector3(660f, 80f));
        AnimatorState dissolveState = AddState(machine, "Dissolve", dissolve, new Vector3(660f, 220f));
        machine.defaultState = hidden;

        AddBoolTransition(hidden, bootState, "IsVisible", true);
        AddExitTransition(bootState, idleState, 0.95f);
        AddBoolTransition(idleState, dissolveState, "IsVisible", false);
        AddExitTransition(dissolveState, hidden, 0.98f);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureBodyController(AnimatorController controller)
    {
        AnimationClip playerIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerIdlePath);
        AnimationClip playerWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerWalkPath);
        if (playerIdle == null || playerWalk == null)
        {
            throw new FileNotFoundException("Missing Player Idle or Walk clip required by the hologram body.");
        }

        ClearController(controller);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = AddState(machine, "Fake Idle", playerIdle, new Vector3(220f, 80f));
        AnimatorState walk = AddState(machine, "Fake Walk", playerWalk, new Vector3(500f, 80f));
        machine.defaultState = idle;
        AddBoolTransition(idle, walk, "IsMoving", true, 0.15f);
        AddBoolTransition(walk, idle, "IsMoving", false, 0.15f);
        EditorUtility.SetDirty(controller);
    }

    private static void BuildPrefab(Material material, RuntimeAnimatorController vfxController, RuntimeAnimatorController bodyController)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualSourcePath);
        if (source == null) throw new FileNotFoundException("Missing Player visual source prefab", PlayerVisualSourcePath);

        var root = new GameObject("PF_MotionDecoy_Hologram_Visual");
        Animator rootAnimator = root.AddComponent<Animator>();
        rootAnimator.runtimeAnimatorController = vfxController;
        rootAnimator.applyRootMotion = false;
        rootAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        GameObject character = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject;
        if (character == null)
        {
            Object.DestroyImmediate(root);
            throw new MissingReferenceException("Could not instantiate the Player visual source.");
        }
        character.name = "CharacterVisual";
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = Vector3.one * 0.01f;

        foreach (PlayerAnimatorDriver driver in character.GetComponentsInChildren<PlayerAnimatorDriver>(true))
        {
            Object.DestroyImmediate(driver);
        }

        Animator bodyAnimator = character.GetComponentInChildren<Animator>(true);
        if (bodyAnimator == null)
        {
            Object.DestroyImmediate(root);
            throw new MissingComponentException("Player visual source has no body Animator.");
        }
        bodyAnimator.runtimeAnimatorController = bodyController;
        bodyAnimator.applyRootMotion = false;
        bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        CreateScanParticles(character.transform, material);

        var lightObject = new GameObject("HologramLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.08f, 0.78f, 1f);
        light.intensity = 0f;
        light.range = 2f;
        light.shadows = LightShadows.None;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        if (prefab == null) throw new IOException("Could not save Motion Decoy hologram prefab.");
    }

    private static void CreateScanParticles(Transform character, Material material)
    {
        var vfxObject = new GameObject("HologramScanParticles");
        vfxObject.transform.SetParent(character, false);
        vfxObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        ParticleSystem particles = vfxObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 0.45f;
        main.startSpeed = 0.025f;
        main.startSize = 0.018f;
        main.startColor = new Color(0.12f, 0.88f, 1f, 0.5f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission;
        emission.rateOverTime = 28f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.7f, 1.8f, 0.45f);
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void ValidatePrefab(RuntimeAnimatorController vfxController, RuntimeAnimatorController bodyController, params AnimationClip[] clips)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new MissingReferenceException("Hologram prefab was not saved.");
        Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 2) throw new MissingComponentException("Hologram prefab must contain exactly two Animators.");
        if (prefab.GetComponent<Animator>()?.runtimeAnimatorController != vfxController)
            throw new MissingReferenceException("Hologram root Animator Controller is invalid.");
        if (prefab.transform.Find("CharacterVisual")?.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController != bodyController)
            throw new MissingReferenceException("Hologram body Animator Controller is invalid.");
        if (prefab.GetComponentsInChildren<PlayerAnimatorDriver>(true).Length != 0)
            throw new MissingComponentException("Hologram visual must not contain PlayerAnimatorDriver.");

        int bindingCount = 0;
        foreach (AnimationClip clip in clips)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0) throw new MissingReferenceException("Hologram clip has no curves: " + clip.name);
            foreach (EditorCurveBinding binding in bindings)
            {
                Transform target = prefab.transform.Find(binding.path);
                if (target == null) throw new MissingReferenceException(clip.name + " has invalid path: " + binding.path);
                if (binding.type != typeof(Transform) && target.GetComponent(binding.type) == null)
                    throw new MissingComponentException(clip.name + " cannot find " + binding.type.Name + " at " + binding.path);
                bindingCount++;
            }
        }
        Debug.Log("[MotionDecoyHologramBuilder] Validated prefab, 2 Animators, 3 clips and " + bindingCount + " bindings.");
    }

    private static void ClearController(AnimatorController controller)
    {
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        foreach (ChildAnimatorStateMachine child in machine.stateMachines) machine.RemoveStateMachine(child.stateMachine);
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value, float duration = 0.05f)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
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
            AnimationUtility.SetEditorCurve(clip, binding, null);
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

    private static void SetCurve(AnimationClip clip, string path, System.Type type, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static Keyframe Key(float time, float value) => new Keyframe(time, value);

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
