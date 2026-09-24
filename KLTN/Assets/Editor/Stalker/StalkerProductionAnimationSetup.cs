using System;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Presentation;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class StalkerProductionAnimationSetup
{
    private const string SourceGeneratedFolder = "Assets/Animations/Stalker/Generated_Quadruped_V33";
    private const string GeneratedFolder = "Assets/Animations/Stalker/Generated_Quadruped_V37_BipedPosture";
    private const string ControllerPath = "Assets/Animations/Stalker/AC_Stalker.controller";
    private const string PrefabPath = "Assets/Prefabs/StalkerNetwork.prefab";
    private const string ModelPath = "Assets/Art/Characters/TheHumanDeer/source/deer_monster_sketchfab_03.fbx";
    private const string HipPath = "C_Hip_J";
    private const string HipHeightProperty = "m_LocalPosition.y";
    private const string SpinePath = "C_Hip_J/C_spine_01_J";
    private const string HeadPath = "C_Hip_J/C_spine_01_J/C_spine_02_J/C_spine_03_J/C_spine_04_J/C_Neck_J/C_Head_J";
    private const string LeftHandPath = "C_Hip_J/C_spine_01_J/C_spine_02_J/C_spine_03_J/C_spine_04_J/L_shoulder_J/L_elbow_J/L_wrist_J/L_hand_J";
    private const string RightHandPath = "C_Hip_J/C_spine_01_J/C_spine_02_J/C_spine_03_J/C_spine_04_J/R_shoulder_J/R_elbow_J/R_wrist_J/R_hand_J";
    private static readonly Vector3 NonRunSpinePose = new Vector3(-3.7f, -17.5f, 0f);
    private static readonly Vector3 RunSpinePose = new Vector3(22.9f, -1.9f, 0f);
    private const float WalkStateSpeed = 1.35f;
    private const float NonRunStateSpeed = 1.08f;

    [MenuItem("Tools/Stalker/Build Production Animator Presentation")]
    public static void BuildProductionAnimatorPresentation()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[StalkerProductionAnim] Cannot modify Stalker animation presentation while in Play Mode.");
            return;
        }

        BakeBipedPostureClips();

        var idle = LoadClip("STK_Idle");
        var walk = LoadClip("STK_Walk");
        var run = LoadClip("STK_Run");
        var alert = LoadClip("STK_Alert");
        var search = LoadClip("STK_Search");
        var attack = LoadClip("STK_Attack_01");
        var recover = LoadClip("STK_Recover");
        var runStop = LoadClip("STK_Run_Stop");
        var turn = LoadClip("STK_Turn");
        var turnLeft = LoadClip("STK_Turn_Left");
        var turnRight = LoadClip("STK_Turn_Right");

        if (idle == null || walk == null || run == null || alert == null
            || search == null || attack == null || recover == null || runStop == null
            || turn == null || turnLeft == null || turnRight == null)
        {
            Debug.LogError($"[StalkerProductionAnim] Missing one or more Stalker clips in {GeneratedFolder}. Aborting setup.");
            return;
        }

        VerifyLoopSetting(idle, true);
        VerifyLoopSetting(walk, true);
        VerifyLoopSetting(run, true);
        VerifyLoopSetting(search, true);
        VerifyLoopSetting(alert, false);
        VerifyLoopSetting(attack, false);
        VerifyLoopSetting(recover, false);
        VerifyLoopSetting(runStop, false);
        VerifyLoopSetting(turn, false);
        VerifyLoopSetting(turnLeft, false);
        VerifyLoopSetting(turnRight, false);

        var controller = CreateController(
            idle,
            walk,
            run,
            alert,
            search,
            attack,
            recover,
            runStop,
            turn,
            turnLeft,
            turnRight);
        AssignPrefab(controller);
        SetupAudioInPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Inject audio animation events into the baked clips
        StalkerAudioEventSetup.AddAudioAnimationEvents();

        Debug.Log("[StalkerProductionAnim] Production Stalker Animator presentation setup complete.");
    }

    private static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{GeneratedFolder}/{name}.anim");
    }

    private static void BakeBipedPostureClips()
    {
        EnsureFolder("Assets/Animations/Stalker", "Generated_Quadruped_V37_BipedPosture");

        BakeClip("STK_Idle", 13.5f);
        BakeClip("STK_Walk", 17.5f, 17.5f, amplifyWalkStride: true);
        BakeClip("STK_Run", 0f);
        BakeClip("STK_Alert", 13.5f);
        BakeClip("STK_Search", 13f);
        BakeClip("STK_Attack_01", 13f);
        BakeClip("STK_Recover", 13f);
        BakeClip("STK_Run_Stop", 0f, 13f);
        BakeClip("STK_Turn", 13f);
        BakeClip("STK_Turn_Left", 13f);
        BakeClip("STK_Turn_Right", 13f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureFolder(string parent, string folder)
    {
        var path = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void BakeClip(string clipName, float hipHeightOffset)
    {
        BakeClip(clipName, hipHeightOffset, hipHeightOffset);
    }

    private static void BakeClip(string clipName, float startHipHeightOffset, float endHipHeightOffset)
    {
        BakeClip(clipName, startHipHeightOffset, endHipHeightOffset, false);
    }

    private static void BakeClip(
        string clipName,
        float startHipHeightOffset,
        float endHipHeightOffset,
        bool amplifyWalkStride)
    {
        var sourcePath = $"{SourceGeneratedFolder}/{clipName}.anim";
        var destinationPath = $"{GeneratedFolder}/{clipName}.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath) != null)
        {
            AssetDatabase.DeleteAsset(destinationPath);
        }

        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            Debug.LogError($"[StalkerProductionAnim] Failed to copy {sourcePath} to {destinationPath}.");
            return;
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
        if (clip == null)
        {
            Debug.LogError($"[StalkerProductionAnim] Failed to load baked clip {destinationPath}.");
            return;
        }

        ApplyHipHeightOffset(clip, startHipHeightOffset, endHipHeightOffset);
        ApplySpinePose(clip, UsesRunLinkedSpinePose(clipName) ? RunSpinePose : NonRunSpinePose);
        if (clipName != "STK_Run")
        {
            ApplySubtleUpperBodyMotion(clip);
        }

        if (amplifyWalkStride)
        {
            ApplyHipForwardStride(clip, 4.2f);
            ApplyWalkStrideOverlay(clip);
        }

        EditorUtility.SetDirty(clip);
        Debug.Log(
            $"[StalkerProductionAnim] Baked {clipName}: hip offset {startHipHeightOffset:0.##}->{endHipHeightOffset:0.##}.");
    }

    private static void ApplyHipHeightOffset(
        AnimationClip clip,
        float startHipHeightOffset,
        float endHipHeightOffset)
    {
        var binding = AnimationUtility.GetCurveBindings(clip)
            .FirstOrDefault(candidate =>
                candidate.path == HipPath
                && candidate.propertyName == HipHeightProperty);

        if (string.IsNullOrEmpty(binding.propertyName))
        {
            Debug.LogWarning($"[StalkerProductionAnim] Clip {clip.name} has no {HipPath}/{HipHeightProperty} curve.");
            return;
        }

        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null || curve.keys.Length == 0)
        {
            Debug.LogWarning($"[StalkerProductionAnim] Clip {clip.name} has empty {HipPath}/{HipHeightProperty} curve.");
            return;
        }

        var length = Mathf.Max(0.0001f, clip.length);
        var keys = curve.keys;
        for (var i = 0; i < keys.Length; i++)
        {
            var t = Mathf.Clamp01(keys[i].time / length);
            var offset = Mathf.Lerp(startHipHeightOffset, endHipHeightOffset, t);
            keys[i].value += offset;
        }

        curve.keys = keys;
        for (var i = 0; i < keys.Length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void ApplySpinePose(AnimationClip clip, Vector3 localPosition)
    {
        SetVectorPositionCurve(clip, SpinePath, localPosition, Vector3.zero, 0f, 0f);
    }

    private static bool UsesRunLinkedSpinePose(string clipName)
    {
        return clipName == "STK_Run"
            || clipName == "STK_Run_Stop"
            || clipName == "STK_Attack_01"
            || clipName == "STK_Recover";
    }

    private static void ApplySubtleUpperBodyMotion(AnimationClip clip)
    {
        SetVectorPositionCurve(
            clip,
            HeadPath,
            Vector3.zero,
            new Vector3(0.35f, 0.18f, 0.12f),
            0f,
            2f);
        SetVectorPositionCurve(
            clip,
            LeftHandPath,
            Vector3.zero,
            new Vector3(0.25f, 0.45f, 0.35f),
            0.25f,
            2f);
        SetVectorPositionCurve(
            clip,
            RightHandPath,
            Vector3.zero,
            new Vector3(0.25f, 0.45f, 0.35f),
            0.75f,
            2f);
    }

    private static void SetVectorPositionCurve(
        AnimationClip clip,
        string path,
        Vector3 baseValue,
        Vector3 amplitude,
        float phaseOffset,
        float cycles)
    {
        SetFloatCurve(clip, path, "m_LocalPosition.x", baseValue.x, amplitude.x, phaseOffset, cycles);
        SetFloatCurve(clip, path, "m_LocalPosition.y", baseValue.y, amplitude.y, phaseOffset + 0.25f, cycles);
        SetFloatCurve(clip, path, "m_LocalPosition.z", baseValue.z, amplitude.z, phaseOffset + 0.5f, cycles);
    }

    private static void SetFloatCurve(
        AnimationClip clip,
        string path,
        string propertyName,
        float baseValue,
        float amplitude,
        float phaseOffset,
        float cycles)
    {
        var length = Mathf.Max(0.0001f, clip.length);
        var curve = new AnimationCurve();
        const int samples = 16;
        for (var i = 0; i <= samples; i++)
        {
            var normalizedTime = i / (float)samples;
            var phase = (normalizedTime * cycles + phaseOffset) * Mathf.PI * 2f;
            var value = baseValue + Mathf.Sin(phase) * amplitude;
            curve.AddKey(normalizedTime * length, value);
        }

        SmoothCurve(curve);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), ToTransformCurveProperty(propertyName)),
            curve);
    }

    private static string ToTransformCurveProperty(string propertyName)
    {
        return propertyName;
    }

    private static void ApplyHipForwardStride(AnimationClip clip, float amplitude)
    {
        var binding = AnimationUtility.GetCurveBindings(clip)
            .FirstOrDefault(candidate =>
                candidate.path == HipPath
                && candidate.propertyName == "m_LocalPosition.z");

        if (string.IsNullOrEmpty(binding.propertyName))
        {
            Debug.LogWarning($"[StalkerProductionAnim] Clip {clip.name} has no {HipPath}/m_LocalPosition.z curve.");
            return;
        }

        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null || curve.keys.Length == 0)
        {
            Debug.LogWarning($"[StalkerProductionAnim] Clip {clip.name} has empty {HipPath}/m_LocalPosition.z curve.");
            return;
        }

        var length = Mathf.Max(0.0001f, clip.length);
        var keys = curve.keys;
        for (var i = 0; i < keys.Length; i++)
        {
            var phase = (keys[i].time / length) * Mathf.PI * 4f;
            keys[i].value += Mathf.Sin(phase) * amplitude;
        }

        curve.keys = keys;
        SmoothCurve(curve);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void ApplyWalkStrideOverlay(AnimationClip clip)
    {
        const float hindStride = 5.5f;
        const float frontStride = 4.5f;
        const float lift = 2.4f;

        ApplyStrideCurve(clip, "C_Hip_J/L_upper_leg_J/L_knee_J/L_ankle_J/L_heel_J/L_feet_J", hindStride, lift, 0f);
        ApplyStrideCurve(clip, "C_Hip_J/R_upper_leg_J/R_knee_J/R_ankle_J/R_heel_J/R_feet_J", hindStride, lift, 0.5f);
        ApplyStrideCurve(clip, "C_Hip_J/C_spine_01_J/C_spine_02_J/C_spine_03_J/C_spine_04_J/L_shoulder_J/L_elbow_J/L_wrist_J/L_hand_J", frontStride, lift * 0.75f, 0.5f);
        ApplyStrideCurve(clip, "C_Hip_J/C_spine_01_J/C_spine_02_J/C_spine_03_J/C_spine_04_J/R_shoulder_J/R_elbow_J/R_wrist_J/R_hand_J", frontStride, lift * 0.75f, 0f);
    }

    private static void ApplyStrideCurve(
        AnimationClip clip,
        string path,
        float forwardAmplitude,
        float liftAmplitude,
        float phaseOffset)
    {
        var length = Mathf.Max(0.0001f, clip.length);
        var forwardCurve = new AnimationCurve();
        var liftCurve = new AnimationCurve();
        const int samples = 16;

        for (var i = 0; i <= samples; i++)
        {
            var normalizedTime = i / (float)samples;
            var phase = (normalizedTime + phaseOffset) * Mathf.PI * 2f;
            var time = normalizedTime * length;
            forwardCurve.AddKey(time, Mathf.Sin(phase) * forwardAmplitude);
            liftCurve.AddKey(time, Mathf.Max(0f, Mathf.Sin(phase)) * liftAmplitude);
        }

        SmoothCurve(forwardCurve);
        SmoothCurve(liftCurve);

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), ToTransformCurveProperty("m_LocalPosition.z")),
            forwardCurve);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), ToTransformCurveProperty("m_LocalPosition.y")),
            liftCurve);
    }

    private static void SmoothCurve(AnimationCurve curve)
    {
        for (var i = 0; i < curve.keys.Length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }
    }

    private static AnimatorController CreateController(
        AnimationClip idle,
        AnimationClip walk,
        AnimationClip run,
        AnimationClip alert,
        AnimationClip search,
        AnimationClip attack,
        AnimationClip recover,
        AnimationClip runStop,
        AnimationClip turn,
        AnimationClip turnLeft,
        AnimationClip turnRight)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("StalkerState", AnimatorControllerParameterType.Int);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("StateProgress", AnimatorControllerParameterType.Float);
        controller.AddParameter("AttackProgress", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

        var stateMachine = controller.layers[0].stateMachine;
        stateMachine.name = "Base Layer";

        var idleState = AddState(stateMachine, "Idle", idle, new Vector2(200f, 0f), NonRunStateSpeed);
        AddState(stateMachine, "Walk", walk, new Vector2(420f, 0f), WalkStateSpeed);
        AddState(stateMachine, "Run", run, new Vector2(640f, 0f));
        AddState(stateMachine, "Alert", alert, new Vector2(860f, 0f), NonRunStateSpeed);
        AddState(stateMachine, "Search", search, new Vector2(420f, 160f), NonRunStateSpeed);
        AddState(stateMachine, "Attack", attack, new Vector2(640f, 160f), NonRunStateSpeed);
        AddState(stateMachine, "Recover", recover, new Vector2(860f, 160f), NonRunStateSpeed);
        AddState(stateMachine, "RunStop", runStop, new Vector2(1080f, 160f), NonRunStateSpeed);
        AddState(stateMachine, "Turn", turn, new Vector2(420f, 320f), NonRunStateSpeed);
        AddState(stateMachine, "TurnLeft", turnLeft, new Vector2(640f, 320f), NonRunStateSpeed);
        AddState(stateMachine, "TurnRight", turnRight, new Vector2(860f, 320f), NonRunStateSpeed);
        stateMachine.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        Debug.Log($"[StalkerProductionAnim] Created AC_Stalker.controller from {GeneratedFolder} with Base Layer default Idle.");
        return controller;
    }

    private static AnimatorState AddState(
        AnimatorStateMachine stateMachine,
        string name,
        Motion motion,
        Vector2 position,
        float speed = 1f)
    {
        var state = stateMachine.AddState(name, position);
        state.motion = motion;
        state.speed = speed;
        state.writeDefaultValues = false;
        return state;
    }

    private static void AssignPrefab(RuntimeAnimatorController controller)
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var animator = prefabRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("[StalkerProductionAnim] StalkerNetwork prefab has no Animator in children.");
                return;
            }

            animator.runtimeAnimatorController = controller;
            animator.avatar = LoadModelAvatar();
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);

            var fusionRuntime = prefabRoot.GetComponent<StalkerFusionRuntime>();
            if (fusionRuntime != null)
            {
                var runtimeObject = new SerializedObject(fusionRuntime);
                runtimeObject.FindProperty("animator").objectReferenceValue = animator;
                runtimeObject.FindProperty("animatorStateParameter").stringValue = "StalkerState";
                runtimeObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fusionRuntime);
            }

            var presenter = prefabRoot.GetComponent<StalkerAnimatorPresenter>();
            if (presenter == null)
            {
                presenter = prefabRoot.AddComponent<StalkerAnimatorPresenter>();
            }

            var presenterObject = new SerializedObject(presenter);
            presenterObject.FindProperty("animator").objectReferenceValue = animator;
            presenterObject.FindProperty("fusionRuntime").objectReferenceValue = fusionRuntime;
            presenterObject.FindProperty("controller").objectReferenceValue =
                prefabRoot.GetComponent<EchoProtocol.AI.Stalker.StalkerController>();
            presenterObject.FindProperty("navMeshAgent").objectReferenceValue =
                prefabRoot.GetComponent<UnityEngine.AI.NavMeshAgent>();
            presenterObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Debug.Log("[StalkerProductionAnim] Assigned AC_Stalker.controller and StalkerAnimatorPresenter to StalkerNetwork prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Avatar LoadModelAvatar()
    {
        var avatar = AssetDatabase
            .LoadAllAssetsAtPath(ModelPath)
            .OfType<Avatar>()
            .FirstOrDefault();

        if (avatar == null)
        {
            Debug.LogError($"[StalkerProductionAnim] No Avatar found in {ModelPath}. Animator will not play model clips.");
        }

        return avatar;
    }

    private static void VerifyLoopSetting(AnimationClip clip, bool expected)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime != expected)
        {
            Debug.LogWarning(
                $"[StalkerProductionAnim] Clip {clip.name} loopTime={settings.loopTime}; expected {expected}. " +
                "Leaving clip asset unchanged.");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Audio prefab wiring
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates (or refreshes) the four AudioSource child GameObjects inside the
    /// StalkerNetwork prefab and wires them into a StalkerAudioController component.
    ///
    /// Child hierarchy produced:
    ///   StalkerNetwork
    ///   └─ Audio_Voice       (AudioSource – one-shot voice)
    ///   └─ Audio_Movement    (AudioSource – one-shot footstep / jump)
    ///   └─ Audio_Breathing   (AudioSource – loop, volume 0.7)
    ///   └─ Audio_Chase       (AudioSource – loop, volume 0)
    [MenuItem("Tools/Stalker/Setup All Audio (Prefab + Events)")]
    public static void SetupAllAudio()
    {
        SetupAudioInPrefab();
        StalkerAudioEventSetup.AddAudioAnimationEvents();
        Debug.Log("[StalkerProductionAnim] ✓ Complete audio setup (Prefab AudioSources + Animation Events) completed!");
    }

    /// <summary>
    /// Creates (or refreshes) the four AudioSource child GameObjects inside the
    /// StalkerNetwork prefab and wires them into a StalkerAudioController component.
    ///
    /// Child hierarchy produced:
    ///   StalkerNetwork
    ///   └─ Audio_Voice       (AudioSource – one-shot voice)
    ///   └─ Audio_Movement    (AudioSource – one-shot footstep / jump)
    ///   └─ Audio_Breathing   (AudioSource – loop, volume 1.0)
    ///   └─ Audio_Chase       (AudioSource – loop, volume 0)
    /// </summary>
    [MenuItem("Tools/Stalker/Setup Audio In Prefab")]
    public static void SetupAudioInPrefab()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // Ensure StalkerAudioController exists on the root
            var audioController =
                prefabRoot.GetComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAudioController>();
            if (audioController == null)
            {
                audioController =
                    prefabRoot.AddComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAudioController>();
            }

            // Remove legacy GameAudioEmitter from StalkerNetwork prefab to avoid redundancy
            var legacyEmitter = prefabRoot.GetComponent<EchoProtocol.Audio.GameAudioEmitter>();
            if (legacyEmitter != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyEmitter, true);
                Debug.Log("[StalkerProductionAnim] Cleaned up legacy GameAudioEmitter from StalkerNetwork prefab.");
            }

            // Helper: find or create a named child with full 3D audio settings
            AudioSource GetOrCreateAudioChild(
                string childName,
                bool loop,
                float volume,
                float minDist,
                float maxDist,
                AudioRolloffMode rolloff)
            {
                var existing = prefabRoot.transform.Find(childName);
                GameObject childGo;
                if (existing != null)
                {
                    childGo = existing.gameObject;
                }
                else
                {
                    childGo = new GameObject(childName);
                    childGo.transform.SetParent(prefabRoot.transform, false);
                }

                var src = childGo.GetComponent<AudioSource>();
                if (src == null)
                {
                    src = childGo.AddComponent<AudioSource>();
                }

                src.loop                  = loop;
                src.playOnAwake           = false;
                src.volume                = volume;
                src.spatialBlend          = 1f;          // full 3D
                src.minDistance           = minDist;
                src.maxDistance           = maxDist;
                src.rolloffMode           = rolloff;
                src.dopplerLevel          = 0f;          // disable doppler on monster
                src.spread                = 60f;         // wider spread = more omni, audible off-axis
                EditorUtility.SetDirty(src);
                return src;
            }

            //
            // 3D settings per source type (tuned for loud, clear audio across large distance):
            //
            //   Voice  (Detect/Search/Bite/Punch/Sniff)
            //     → Linear rolloff so volume stays strong until maxDistance
            //     → minDistance 30 m: full volume within 30 m radius
            //     → maxDistance 150 m: audible up to 150 m across the complex
            //
            //   Chase  (Conveyor loop)
            //     → Same as Voice – wide, persistent dread audio
            //
            //   Breathing (idle_Breathing loop)
            //     → Linear rolloff, wide hearing range
            //     → minDistance 15 m, maxDistance 60 m
            //
            //   Movement (footsteps, jumps)
            //     → Linear rolloff, heavy footsteps heard through hallways
            //     → minDistance 20 m, maxDistance 90 m
            //
            var voiceSrc    = GetOrCreateAudioChild("Audio_Voice",
                loop: false, volume: 1.0f,
                minDist:  30f, maxDist: 150f,
                rolloff: AudioRolloffMode.Linear);

            var movementSrc = GetOrCreateAudioChild("Audio_Movement",
                loop: false, volume: 1.0f,
                minDist:  20f, maxDist: 90f,
                rolloff: AudioRolloffMode.Linear);

            var breathSrc   = GetOrCreateAudioChild("Audio_Breathing",
                loop: true,  volume: 1.00f,
                minDist:  15f, maxDist: 60f,
                rolloff: AudioRolloffMode.Linear);

            var chaseSrc    = GetOrCreateAudioChild("Audio_Chase",
                loop: true,  volume: 0.00f,
                minDist: 30f, maxDist: 150f,
                rolloff: AudioRolloffMode.Linear);

            // Load audio clips from the project
            AudioClip LoadClipAsset(string assetPath) =>
                AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

            var clipBite     = LoadClipAsset("Assets/Audio/stalker/Bite.wav");
            var clipDetect   = LoadClipAsset("Assets/Audio/stalker/Detect.wav");
            var clipSearch   = LoadClipAsset("Assets/Audio/stalker/Search.wav");
            var clipSniff    = LoadClipAsset("Assets/Audio/stalker/sniff.mp3");
            var clipPunch    = LoadClipAsset("Assets/Audio/stalker/punch.mp3");
            var clipWalk     = LoadClipAsset("Assets/Audio/stalker/walk.wav");
            var clipJump     = LoadClipAsset("Assets/Audio/stalker/jumpin.mp3");
            var clipBreath   = LoadClipAsset("Assets/Audio/stalker/idle_Breathing.mp3");
            var clipConveyor = LoadClipAsset("Assets/Audio/stalker/Conveyor LOOPED.wav");

            // Wire AudioSources + AudioClips into StalkerAudioController
            var so = new SerializedObject(audioController);
            so.FindProperty("voiceSource").objectReferenceValue     = voiceSrc;
            so.FindProperty("movementSource").objectReferenceValue  = movementSrc;
            so.FindProperty("breathingSource").objectReferenceValue = breathSrc;
            so.FindProperty("chaseSource").objectReferenceValue     = chaseSrc;

            // Clips – Voice
            so.FindProperty("detectClip").objectReferenceValue       = clipDetect;
            so.FindProperty("searchClip").objectReferenceValue       = clipSearch;
            so.FindProperty("sniffClip").objectReferenceValue        = clipSniff;
            so.FindProperty("biteClip").objectReferenceValue         = clipBite;
            so.FindProperty("punchClip").objectReferenceValue        = clipPunch;

            // Clips – Movement
            so.FindProperty("walkClip").objectReferenceValue         = clipWalk;
            so.FindProperty("jumpClip").objectReferenceValue         = clipJump;

            // Clips – Ambient
            so.FindProperty("idleBreathingClip").objectReferenceValue  = clipBreath;
            so.FindProperty("conveyorLoopedClip").objectReferenceValue = clipConveyor;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioController);

            // Also wire the presenter's audioController reference
            var presenter =
                prefabRoot.GetComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAnimatorPresenter>();
            if (presenter != null)
            {
                var presenterSo = new SerializedObject(presenter);
                presenterSo.FindProperty("audioController").objectReferenceValue = audioController;
                presenterSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Debug.Log(
                "[StalkerProductionAnim] Audio sources created and StalkerAudioController " +
                "wired in StalkerNetwork prefab: Voice / Movement / Breathing / Chase.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StalkerProductionAnim] SetupAudioInPrefab failed: {ex}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
