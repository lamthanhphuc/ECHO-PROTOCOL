using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAnimatorSetupBuilder
{
    private const string RunMarker = "Assets/Editor/.run_player_animator_setup_builder";
    private const string ControllerPath = "Assets/Animations/Player/AC_PlayerCharacter.controller";
    private const string BodyModelPath = "Assets/import/Character/source/astro-engineer-07d.fbx";
    private const string AnimationFolder = "Assets/Animations/Player";
    private const string IdleFbxPath = AnimationFolder + "/Idle.fbx";
    private const string WalkForwardFbxPath = AnimationFolder + "/Walking.fbx";
    private const string WalkBackwardFbxPath = AnimationFolder + "/Player_Walk_Backward.fbx";
    private const string WalkLeftFbxPath = AnimationFolder + "/Left Strafe Walk.fbx";
    private const string WalkRightFbxPath = AnimationFolder + "/Right Strafe Walk.fbx";
    private const string RunForwardFbxPath = AnimationFolder + "/Fast Run.fbx";
    private const string CrouchIdleFbxPath = AnimationFolder + "/Crouch Idle.fbx";
    private const string CrouchWalkForwardFbxPath = AnimationFolder + "/Crouch Walk Forward.fbx";
    private const string CrouchWalkBackwardFbxPath = AnimationFolder + "/Crouch Walk Back.fbx";
    private const string CrouchWalkLeftFbxPath = AnimationFolder + "/Crouch Walk Left.fbx";
    private const string CrouchWalkRightFbxPath = AnimationFolder + "/Crouch Walk Right.fbx";
    private const string CarryIdleFbxPath = AnimationFolder + "/Player_Carry_Idle.fbx";
    private const string CarryWalkFbxPath = AnimationFolder + "/Player_Carry_Walk.fbx";
    private const string CarryRunFbxPath = AnimationFolder + "/Player_Carry_Run.fbx";
    private const string CrawlIdleFbxPath = AnimationFolder + "/Player_Crawl.FBX";
    private const string CrawlForwardFbxPath = AnimationFolder + "/Crawl_Forward.fbx";
    private const string ReviveFbxPath = AnimationFolder + "/Reviving.FBX";

    private static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab",
        "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P2_Orange.prefab",
        "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P3_Green.prefab",
        "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P4_Purple.prefab",
    };

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ToAbsolutePath(RunMarker)))
            {
                return;
            }

            File.Delete(ToAbsolutePath(RunMarker));
            BuildAnimatorSetup();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Player Animator Setup")]
    public static void BuildAnimatorSetup()
    {
        ClearEditorSelection();

        EnsureHumanoidBodyImport();
        EnsureHumanoidAnimationImport(IdleFbxPath, loop: true);
        EnsureHumanoidAnimationImport(WalkForwardFbxPath, loop: true);
        EnsureHumanoidAnimationImport(WalkBackwardFbxPath, loop: true);
        EnsureHumanoidAnimationImport(WalkLeftFbxPath, loop: true);
        EnsureHumanoidAnimationImport(WalkRightFbxPath, loop: true);
        EnsureHumanoidAnimationImport(RunForwardFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrouchIdleFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrouchWalkForwardFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrouchWalkBackwardFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrouchWalkLeftFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrouchWalkRightFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CarryIdleFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CarryWalkFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CarryRunFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrawlIdleFbxPath, loop: true);
        EnsureHumanoidAnimationImport(CrawlForwardFbxPath, loop: true);
        EnsureHumanoidAnimationImport(ReviveFbxPath, loop: false);

        AnimationClip idle = LoadRequiredClip("Player_Idle", IdleFbxPath, "Idle");
        AnimationClip walkForward = LoadRequiredClip("Player_Walk_Forward", WalkForwardFbxPath, "Walking");
        AnimationClip walkBackward = LoadRequiredClip("Player_Walk_Backward", WalkBackwardFbxPath);
        AnimationClip walkLeft = LoadRequiredClip("Player_Walk_Left", WalkLeftFbxPath, "Left Strafe Walk");
        AnimationClip walkRight = LoadRequiredClip("Player_Walk_Right", WalkRightFbxPath, "Right Strafe Walk");
        AnimationClip runForward = LoadRequiredClip("Player_Run_Forward", RunForwardFbxPath, "Fast Run");
        AnimationClip crouchIdle = LoadRequiredClip("Player_Crouch_Idle", CrouchIdleFbxPath, "Crouch Idle");
        AnimationClip crouchWalkForward = LoadRequiredClip("Player_Crouch_Walk_Forward", CrouchWalkForwardFbxPath, "Crouch Walk Forward");
        AnimationClip crouchWalkBackward = LoadRequiredClip("Player_Crouch_Walk_Backward", CrouchWalkBackwardFbxPath, "Crouch Walk Back");
        AnimationClip crouchWalkLeft = LoadRequiredClip("Player_Crouch_Walk_Left", CrouchWalkLeftFbxPath, "Crouch Walk Left");
        AnimationClip crouchWalkRight = LoadRequiredClip("Player_Crouch_Walk_Right", CrouchWalkRightFbxPath, "Crouch Walk Right");
        AnimationClip carryIdle = LoadRequiredClip("Player_Carry_Idle", CarryIdleFbxPath);
        AnimationClip carryWalk = LoadRequiredClip("Player_Carry_Walk", CarryWalkFbxPath);
        AnimationClip carryRun = LoadRequiredClip("Player_Carry_Run", CarryRunFbxPath);
        AnimationClip downedIdle = LoadRequiredClip("Player_Downed_Idle", CrawlIdleFbxPath, "Player_Crawl");
        AnimationClip crawlForward = LoadRequiredClip("Player_Crawl_Forward", CrawlForwardFbxPath, "Crawl_Forward");
        AnimationClip revive = LoadRequiredClip("Player_Revive", ReviveFbxPath, "Reviving");

        PlayerClips clips = new PlayerClips
        {
            Idle = idle,
            WalkForward = walkForward,
            WalkBackward = walkBackward,
            WalkLeft = walkLeft,
            WalkRight = walkRight,
            RunForward = runForward,
            CrouchIdle = crouchIdle,
            CrouchWalkForward = crouchWalkForward,
            CrouchWalkBackward = crouchWalkBackward,
            CrouchWalkLeft = crouchWalkLeft,
            CrouchWalkRight = crouchWalkRight,
            CarryIdle = carryIdle,
            CarryWalk = carryWalk,
            CarryRun = carryRun,
            DownedIdle = downedIdle,
            CrawlForward = crawlForward,
            Revive = revive,
        };

        if (clips.HasMissingClips)
        {
            Debug.LogError("[PlayerAnimatorSetupBuilder] Missing real player animation clips. Import the clips listed in the console, then run this builder again.");
            return;
        }

        AnimatorController controller = RecreateController();
        ConfigureController(controller, clips);
        ApplyControllerToPrefabs(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ClearEditorSelection();
        Debug.Log("[PlayerAnimatorSetupBuilder] Built gameplay animation clips, AC_PlayerCharacter, and applied it to player prefabs.");
    }

    private static AnimatorController RecreateController()
    {
        ClearEditorSelection();

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
    }

    private static void ConfigureController(AnimatorController controller, PlayerClips clips)
    {
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCarrying", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDowned", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsReviving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Revive", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddState(stateMachine, "Locomotion", CreateLocomotionTree(controller, clips), new Vector3(260f, 80f, 0f));
        AnimatorState runForward = AddState(stateMachine, "Run Forward", clips.RunForward, new Vector3(260f, -90f, 0f));
        AnimatorState crouch = AddState(stateMachine, "Crouch Locomotion", CreateCrouchTree(controller, clips), new Vector3(560f, 80f, 0f));
        AnimatorState carry = AddState(stateMachine, "Carry Locomotion", CreateCarryTree(controller, clips), new Vector3(260f, 260f, 0f));
        AnimatorState downed = AddState(stateMachine, "Downed Crawl", CreateDownedTree(controller, clips), new Vector3(560f, 260f, 0f));
        AnimatorState reviving = AddState(stateMachine, "Reviving", clips.Revive, new Vector3(860f, 260f, 0f));

        stateMachine.defaultState = locomotion;

        AddBoolTransition(locomotion, runForward, true, "IsSprinting");
        AddBoolTransition(runForward, locomotion, false, "IsSprinting");
        AddBoolTransition(runForward, crouch, true, "IsCrouching");
        AddBoolTransition(runForward, carry, true, "IsCarrying");
        AddBoolTransition(locomotion, crouch, true, "IsCrouching");
        AddBoolTransition(crouch, locomotion, false, "IsCrouching");
        AddBoolTransition(locomotion, carry, true, "IsCarrying");
        AddBoolTransition(crouch, carry, true, "IsCarrying");
        AddBoolTransition(carry, locomotion, false, "IsCarrying");
        AddBoolTransition(carry, crouch, true, "IsCrouching", false, "IsCarrying");

        AddAnyBoolTransition(stateMachine, downed, true, "IsDowned");
        AddBoolTransition(downed, locomotion, false, "IsDowned");
        AddBoolTransition(locomotion, reviving, true, "IsReviving");
        AddBoolTransition(runForward, reviving, true, "IsReviving");
        AddBoolTransition(crouch, reviving, true, "IsReviving");
        AddBoolTransition(carry, reviving, true, "IsReviving");
        AddBoolTransition(reviving, locomotion, false, "IsReviving", waitForExit: true, exitTime: 0.92f);

        AddAnyTriggerTransition(stateMachine, reviving, "Revive");
    }

    private static Motion CreateLocomotionTree(AnimatorController controller, PlayerClips clips)
    {
        BlendTree tree = CreateDirectionalTree(controller, "BT_Locomotion");
        AddChild(tree, clips.Idle, Vector2.zero);
        AddChild(tree, clips.WalkForward, new Vector2(0f, 1f));
        AddChild(tree, clips.WalkBackward, new Vector2(0f, -1f));
        AddChild(tree, clips.WalkLeft, new Vector2(-1f, 0f));
        AddChild(tree, clips.WalkRight, new Vector2(1f, 0f));
        return tree;
    }

    private static Motion CreateCrouchTree(AnimatorController controller, PlayerClips clips)
    {
        BlendTree tree = CreateDirectionalTree(controller, "BT_CrouchLocomotion");
        AddChild(tree, clips.CrouchIdle, Vector2.zero);
        AddChild(tree, clips.CrouchWalkForward, new Vector2(0f, 1f));
        AddChild(tree, clips.CrouchWalkBackward, new Vector2(0f, -1f));
        AddChild(tree, clips.CrouchWalkLeft, new Vector2(-1f, 0f));
        AddChild(tree, clips.CrouchWalkRight, new Vector2(1f, 0f));
        return tree;
    }

    private static Motion CreateCarryTree(AnimatorController controller, PlayerClips clips)
    {
        BlendTree tree = CreateDirectionalTree(controller, "BT_CarryLocomotion");
        AddChild(tree, clips.CarryIdle, Vector2.zero);
        AddChild(tree, clips.CarryWalk, new Vector2(0f, 1f));
        AddChild(tree, clips.CarryWalk, new Vector2(0f, -1f));
        AddChild(tree, clips.CarryWalk, new Vector2(-1f, 0f));
        AddChild(tree, clips.CarryWalk, new Vector2(1f, 0f), mirror: true);
        AddChild(tree, clips.CarryRun, new Vector2(0f, 2f));
        return tree;
    }

    private static Motion CreateDownedTree(AnimatorController controller, PlayerClips clips)
    {
        BlendTree tree = CreateDirectionalTree(controller, "BT_DownedCrawl");
        AddChild(tree, clips.DownedIdle, Vector2.zero);
        AddChild(tree, clips.CrawlForward, new Vector2(0f, 1f));
        return tree;
    }

    private static void AddChild(BlendTree tree, Motion motion, Vector2 position, bool mirror = false)
    {
        tree.AddChild(motion, position);
        ChildMotion[] children = tree.children;
        ChildMotion child = children[children.Length - 1];
        child.mirror = mirror;
        children[children.Length - 1] = child;
        tree.children = children;
    }

    private static BlendTree CreateDirectionalTree(AnimatorController controller, string name)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = false,
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool expected, string parameter, bool waitForExit = false, float exitTime = 0f)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureTransition(transition, waitForExit, exitTime);
        transition.AddCondition(expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool expectedA, string parameterA, bool expectedB, string parameterB)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureTransition(transition);
        transition.AddCondition(expectedA ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterA);
        transition.AddCondition(expectedB ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterB);
    }

    private static void AddAnyBoolTransition(AnimatorStateMachine stateMachine, AnimatorState to, bool expected, string parameter)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        ConfigureTransition(transition);
        transition.canTransitionToSelf = false;
        transition.AddCondition(expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddAnyTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState to, string parameter)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        ConfigureTransition(transition);
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
    }

    private static void AddExitTransition(AnimatorState from)
    {
        AnimatorStateTransition transition = from.AddExitTransition();
        transition.hasExitTime = true;
        transition.exitTime = 0.85f;
        transition.duration = 0.1f;
    }

    private static void ConfigureTransition(AnimatorStateTransition transition, bool waitForExit = false, float exitTime = 0f)
    {
        transition.hasExitTime = waitForExit;
        transition.exitTime = waitForExit ? exitTime : 0f;
        transition.duration = 0.18f;
        transition.canTransitionToSelf = false;
    }

    private static void ApplyControllerToPrefabs(RuntimeAnimatorController controller)
    {
        ClearEditorSelection();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(BodyModelPath).OfType<Avatar>().FirstOrDefault(candidate => candidate.isHuman)
            ?? AssetDatabase.LoadAllAssetsAtPath(BodyModelPath).OfType<Avatar>().FirstOrDefault();

        foreach (string prefabPath in PrefabPaths)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning("[PlayerAnimatorSetupBuilder] Missing player prefab: " + prefabPath);
                continue;
            }

            Animator rootAnimator = prefabRoot.GetComponent<Animator>();
            if (rootAnimator != null)
            {
                Object.DestroyImmediate(rootAnimator);
            }

            Transform body = prefabRoot.transform.Find("Body");
            GameObject animatorTarget = body != null ? body.gameObject : prefabRoot;
            Animator animator = animatorTarget.GetComponent<Animator>();
            if (animator == null)
            {
                animator = animatorTarget.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PlayerAnimatorDriver driver = prefabRoot.GetComponent<PlayerAnimatorDriver>();
            if (driver == null)
            {
                driver = prefabRoot.AddComponent<PlayerAnimatorDriver>();
            }

            SerializedObject driverSo = new SerializedObject(driver);
            SetObject(driverSo, "animator", animator);
            SetFloat(driverSo, "walkSpeedReference", 4f);
            SetFloat(driverSo, "runSpeedReference", 7f);
            SetFloat(driverSo, "speedDampTime", 0.16f);
            SetFloat(driverSo, "directionDampTime", 0.12f);
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ClearEditorSelection()
    {
        Selection.activeObject = null;
        Selection.objects = new Object[0];
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void EnsureHumanoidBodyImport()
    {
        ModelImporter importer = AssetImporter.GetAtPath(BodyModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[PlayerAnimatorSetupBuilder] Could not find body model importer: " + BodyModelPath);
            return;
        }

        if (importer.animationType == ModelImporterAnimationType.Human &&
            importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
        {
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();
    }

    private static void EnsureHumanoidAnimationImport(string assetPath, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[PlayerAnimatorSetupBuilder] Could not find animation importer: " + assetPath);
            return;
        }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }

        WrapMode targetWrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        if (importer.animationWrapMode != targetWrapMode)
        {
            importer.animationWrapMode = targetWrapMode;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            bool clipChanged = false;

            if (clip.loopTime != loop)
            {
                clip.loopTime = loop;
                clipChanged = true;
            }

            if (clip.loopPose != loop)
            {
                clip.loopPose = loop;
                clipChanged = true;
            }

            if (!clip.lockRootRotation)
            {
                clip.lockRootRotation = true;
                clipChanged = true;
            }

            if (!clip.lockRootHeightY)
            {
                clip.lockRootHeightY = true;
                clipChanged = true;
            }

            if (!clip.lockRootPositionXZ)
            {
                clip.lockRootPositionXZ = true;
                clipChanged = true;
            }

            if (!clip.keepOriginalOrientation)
            {
                clip.keepOriginalOrientation = true;
                clipChanged = true;
            }

            if (!clip.keepOriginalPositionY)
            {
                clip.keepOriginalPositionY = true;
                clipChanged = true;
            }

            if (!clip.keepOriginalPositionXZ)
            {
                clip.keepOriginalPositionXZ = true;
                clipChanged = true;
            }

            if (clipChanged)
            {
                clips[i] = clip;
                changed = true;
            }
        }

        if (clips.Length > 0)
        {
            importer.clipAnimations = clips;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip LoadRequiredClip(string clipName)
    {
        return LoadRequiredClip(clipName, null);
    }

    private static AnimationClip LoadRequiredClip(string clipName, string fallbackAssetPath, string fallbackClipName = null)
    {
        AnimationClip clip = null;
        if (!string.IsNullOrEmpty(fallbackAssetPath))
        {
            clip = LoadClipFromModel(fallbackAssetPath, fallbackClipName ?? clipName);
        }

        if (clip == null)
        {
            clip = LoadClip(clipName);
        }

        if (clip == null)
        {
            Debug.LogWarning("[PlayerAnimatorSetupBuilder] Missing required animation clip: " + clipName);
        }

        return clip;
    }

    private static AnimationClip LoadOptionalClip(string clipName, AnimationClip fallback)
    {
        AnimationClip clip = LoadClip(clipName);
        if (clip == null)
        {
            Debug.LogWarning("[PlayerAnimatorSetupBuilder] Missing optional animation clip " + clipName + "; using " + fallback.name + " as FPS prototype fallback.");
            return fallback;
        }

        return clip;
    }

    private static AnimationClip LoadClip(string clipName)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationPath(clipName));
    }

    private static AnimationClip LoadClipFromModel(string assetPath, string preferredName)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return clips.FirstOrDefault(clip => clip.name == preferredName)
            ?? clips.FirstOrDefault(clip => clip.name.Contains(preferredName))
            ?? clips.FirstOrDefault();
    }

    private static string AnimationPath(string clipName)
    {
        return AnimationFolder + "/" + clipName + ".anim";
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }

    private sealed class PlayerClips
    {
        public AnimationClip Idle;
        public AnimationClip WalkForward;
        public AnimationClip WalkBackward;
        public AnimationClip WalkLeft;
        public AnimationClip WalkRight;
        public AnimationClip RunForward;
        public AnimationClip CrouchIdle;
        public AnimationClip CrouchWalkForward;
        public AnimationClip CrouchWalkBackward;
        public AnimationClip CrouchWalkLeft;
        public AnimationClip CrouchWalkRight;
        public AnimationClip CarryIdle;
        public AnimationClip CarryWalk;
        public AnimationClip CarryRun;
        public AnimationClip DownedIdle;
        public AnimationClip CrawlForward;
        public AnimationClip Revive;

        public bool HasMissingClips =>
            Idle == null || WalkForward == null || WalkBackward == null || WalkLeft == null || WalkRight == null ||
            RunForward == null ||
            CrouchIdle == null || CrouchWalkForward == null || CrouchWalkBackward == null || CrouchWalkLeft == null || CrouchWalkRight == null ||
            CarryIdle == null || CarryWalk == null || CarryRun == null ||
            DownedIdle == null || CrawlForward == null || Revive == null;
    }
}
