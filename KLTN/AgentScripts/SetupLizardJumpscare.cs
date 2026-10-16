using System;
using System.Linq;
using EchoProtocol.AI.Stalker.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupLizardJumpscare
{
    const string Folder = "Assets/Prefabs/Player/Jumpscare";
    const string PrefabPath = Folder + "/LizardJumpscare.prefab";
    const string StalkerPath = "Assets/Prefabs/StalkerNetwork_Lizard.prefab";

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPath);
        var sourceAnimator = source.GetComponentsInChildren<Animator>(true).Single(x => x.enabled);
        var root = new GameObject("LizardJumpscare");
        try
        {
            var visual = UnityEngine.Object.Instantiate(sourceAnimator.gameObject, root.transform);
            visual.name = "Visual";
            foreach (var component in visual.GetComponentsInChildren<Component>(true).Reverse())
                if (!(component is Transform) && !(component is Renderer) && !(component is MeshFilter) && !(component is Animator))
                    UnityEngine.Object.DestroyImmediate(component);
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            var animator = visual.GetComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var bite = (AnimationClip)controller.layers[0].stateMachine.states.Single(x => x.state.name == "Bite").state.motion;
            bite.SampleAnimation(visual, 0);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var head = visual.GetComponentsInChildren<Transform>(true).Single(x => x.name == "Bip01 Head");
            // PlayerJumpscareController mounts the root at (0,-1.5,1), facing the camera.
            var headPosition = visual.transform.InverseTransformPoint(head.position);
            visual.transform.localPosition = new Vector3(-headPosition.x, 1.5f - headPosition.y, 0.15f - headPosition.z);
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;

            string clipPath = Folder + "/LizardJumpscareLunge.anim";
            var lunge = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (lunge == null) { lunge = new AnimationClip { name = "LizardJumpscareLunge", frameRate = 60 }; AssetDatabase.CreateAsset(lunge, clipPath); }
            float z = visual.transform.localPosition.z;
            lunge.SetCurve("Visual", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1.9f, visual.transform.localPosition.x));
            lunge.SetCurve("Visual", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0, 1.9f, visual.transform.localPosition.y));
            lunge.SetCurve("Visual", typeof(Transform), "localPosition.z", new AnimationCurve(
                new Keyframe(0, z), new Keyframe(0.12f, z + 0.18f), new Keyframe(0.3f, z + 0.12f), new Keyframe(1.9f, z + 0.12f)));
            var lungeController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/LizardJumpscare.controller");
            if (lungeController == null)
            {
                lungeController = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/LizardJumpscare.controller");
                lungeController.AddParameter("Jumpscare", AnimatorControllerParameterType.Trigger);
                var machine = lungeController.layers[0].stateMachine;
                machine.defaultState = machine.AddState("Idle");
                var scare = machine.AddState("Jumpscare");
                scare.motion = lunge;
                var transition = machine.defaultState.AddTransition(scare);
                transition.hasExitTime = false;
                transition.duration = 0;
                transition.AddCondition(AnimatorConditionMode.If, 0, "Jumpscare");
            }
            var rootAnimator = root.AddComponent<Animator>();
            rootAnimator.runtimeAnimatorController = lungeController;
            rootAnimator.applyRootMotion = false;
            rootAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rootAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var stalker = PrefabUtility.LoadPrefabContents(StalkerPath);
            try
            {
                var so = new SerializedObject(stalker.GetComponent<StalkerAnimatorPresenter>());
                so.FindProperty("biteJumpscarePrefab").objectReferenceValue = saved;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(stalker, StalkerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(stalker); }
            AssetDatabase.SaveAssets();
            return Validate();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.GetComponentsInChildren<Fusion.NetworkObject>(true).Length != 0 ||
            prefab.GetComponentsInChildren<Collider>(true).Length != 0 || prefab.GetComponentsInChildren<AudioSource>(true).Length != 0 ||
            prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("Jumpscare must contain presentation components only.");
        var animators = prefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 2 || animators.Any(x => !x.enabled || x.applyRootMotion || x.updateMode != AnimatorUpdateMode.UnscaledTime))
            throw new InvalidOperationException("Incorrect jumpscare Animator configuration.");
        if (!animators[0].runtimeAnimatorController.animationClips.Any(x => x.name == "LizardJumpscareLunge") ||
            !((AnimatorController)animators[1].runtimeAnimatorController).layers[0].stateMachine.states.Any(x => x.state.name == "Bite"))
            throw new InvalidOperationException("Missing lunge or Bite state.");
        var lizard = AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPath).GetComponent<StalkerAnimatorPresenter>();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork.prefab").GetComponent<StalkerAnimatorPresenter>();
        if (lizard.BiteJumpscarePrefab != prefab || original.BiteJumpscarePrefab != null)
            throw new InvalidOperationException("Model-specific prefab routing failed.");
        return "PASS: Lizard prefab assigned; Original retains player fallback; two unscaled Animators, Bite/lunge states, no network/gameplay/audio components.";
    }

    public static string Preview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, scene);
        model.transform.position = new Vector3(0, -1.5f, 1);
        model.transform.rotation = Quaternion.Euler(0, 180, 0);
        var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
        var lightObject = new GameObject("PreviewLight", typeof(Light));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.fieldOfView = 70;
        camera.nearClipPlane = 0.03f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(20, 0, 0);
        var target = new RenderTexture(640, 360, 24);
        var tile = new Texture2D(640, 360, TextureFormat.RGB24, false);
        var sheet = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            var visual = model.transform.Find("Visual").gameObject;
            var controller = (AnimatorController)visual.GetComponent<Animator>().runtimeAnimatorController;
            var bite = (AnimationClip)controller.layers[0].stateMachine.states.Single(x => x.state.name == "Bite").state.motion;
            var lunge = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/LizardJumpscareLunge.anim");
            float[] times = { 0, 0.12f, 0.5f, 1.2f };
            var positions = new System.Collections.Generic.List<string>();
            for (int i = 0; i < times.Length; i++)
            {
                bite.SampleAnimation(visual, times[i]);
                lunge.SampleAnimation(model, times[i]);
                positions.Add(times[i] + ":head=" + visual.GetComponentsInChildren<Transform>(true).First(x => x.name == "Bip01 Head").position + "; visual=" + visual.transform.localPosition);
                camera.Render();
                RenderTexture.active = target;
                tile.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                tile.Apply();
                sheet.SetPixels((i % 2) * 640, (1 - i / 2) * 360, 640, 360, tile.GetPixels());
            }
            sheet.Apply();
            System.IO.Directory.CreateDirectory("Temp/StalkerRetarget");
            System.IO.File.WriteAllBytes("Temp/StalkerRetarget/jumpscare.png", sheet.EncodeToPNG());
            return "Temp/StalkerRetarget/jumpscare.png: 0, 0.12, 0.5, 1.2 seconds\n" + string.Join("\n", positions);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
