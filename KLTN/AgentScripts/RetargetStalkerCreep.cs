using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Editor-only baker: keeps both imported Generic rigs unchanged.
public static class RetargetStalkerCreep
{
    const string Source = "Assets/Creep Horror Creature/Meshes/Creep_mesh_lod1.fbx";
    const string Target = "Assets/Hatogame_new/Lizard/Mesh/Lizard_skin.FBX";
    const string Controller = "Assets/Animations/Stalker/AC_Stalker_Lizard.controller";
    const string Folder = "Assets/Animations/Stalker/LizardRetargeted";
    static readonly Dictionary<string, string> Bones = new Dictionary<string, string>
    {
        { "Torso", "Bip01 Pelvis" }, { "Chest", "Bip01 Spine2" },
        { "Neck", "Bip01 Neck" }, { "Head", "Bip01 Head" }, { "Jaw1", "Bip01_Lip" },
        { "Shoulder.L", "Bip01 L Clavicle" }, { "Shoulder.R", "Bip01 R Clavicle" },
        { "Arm1.L", "Bip01 L UpperArm" }, { "Arm1.R", "Bip01 R UpperArm" },
        { "Arm2.L", "Bip01 L Forearm" }, { "Arm2.R", "Bip01 R Forearm" },
        { "Hand.L", "Bip01 L Hand" }, { "Hand.R", "Bip01 R Hand" },
        { "Leg1.L", "Bip01 L Thigh" }, { "Leg1.R", "Bip01 R Thigh" },
        { "Leg2.L", "Bip01 L Calf" }, { "Leg2.R", "Bip01 R Calf" },
        { "Leg3.L", "Bip01 L HorseLink" }, { "Leg3.R", "Bip01 R HorseLink" },
        { "Foot.L", "Bip01 L Foot" }, { "Foot.R", "Bip01 R Foot" },
        { "StepA1.L", "Bip01 L Toe0" }, { "StepA1.R", "Bip01 R Toe0" },
        { "StepB1.L", "Bip01 L Toe1" }, { "StepB1.R", "Bip01 R Toe1" },
        { "FingA1.L", "Bip01 L Finger0" }, { "FingA1.R", "Bip01 R Finger0" },
        { "FindA2.L", "Bip01 L Finger01" }, { "FindA2.R", "Bip01 R Finger01" },
        { "FingB1.L", "Bip01 L Finger1" }, { "FingB1.R", "Bip01 R Finger1" },
        { "FingB2.L", "Bip01 L Finger11" }, { "FingB2.R", "Bip01 R Finger11" }
    };

    public static string Bake()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before baking.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        var states = controller.layers[0].stateMachine.states.Select(x => x.state).ToArray();
        var protectedStates = states.Where(x => new[] { "Idle1", "Walk1", "Walk2", "Punch" }.Contains(x.name))
            .ToDictionary(x => x, x => x.motion);
        var clips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().ToArray();
        var src = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        var dst = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Target));
        src.hideFlags = dst.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var sourceBones = src.GetComponentsInChildren<Transform>(true).ToDictionary(x => x.name);
            var targetBones = dst.GetComponentsInChildren<Transform>(true).ToDictionary(x => x.name);
            // Calibrate from equivalent idle poses, not the different modelling bind poses.
            clips.Single(x => x.name == "Creep|Idle1_Action").SampleAnimation(src, 0);
            ((AnimationClip)states.Single(x => x.name == "Idle1").motion).SampleAnimation(dst, 0);
            var sourcePose = sourceBones.Values.ToDictionary(x => x, x => (x.localPosition, x.localRotation, x.localScale));
            var targetPose = targetBones.Values.ToDictionary(x => x, x => (x.localPosition, x.localRotation, x.localScale));
            var pairs = Bones.Select(x => (source: sourceBones[x.Key], target: targetBones[x.Value]))
                .OrderBy(x => AnimationUtility.CalculateTransformPath(x.target, dst.transform).Count(c => c == '/')).ToArray();
            var sourceRest = pairs.ToDictionary(x => x.source, x => x.source.rotation);
            var targetRest = pairs.ToDictionary(x => x.target, x => x.target.rotation);
            var hipsRest = sourceBones["Torso"].position;
            var targetHipsRest = targetBones["Bip01 Pelvis"].position;
            float ratio = targetHipsRest.y / hipsRest.y;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations/Stalker", "LizardRetargeted");
            var generated = new Dictionary<AnimatorState, AnimationClip>();
            foreach (var state in states.Where(x => !protectedStates.ContainsKey(x)))
            {
                var input = clips.Single(x => x.name == "Creep|" + state.name + "_Action");
                var output = new AnimationClip { name = "Lizard_Creep_" + state.name, frameRate = 30 };
                var curves = targetBones.Values.Where(x => x != dst.transform).ToDictionary(x => x,
                    x => Enumerable.Range(0, 7).Select(_ => new AnimationCurve()).ToArray());
                int frames = Mathf.CeilToInt(input.length * 30);
                for (int frame = 0; frame <= frames; frame++)
                {
                    float time = Mathf.Min(input.length, frame / 30f);
                    foreach (var pose in sourcePose) { pose.Key.localPosition = pose.Value.Item1; pose.Key.localRotation = pose.Value.Item2; pose.Key.localScale = pose.Value.Item3; }
                    foreach (var pose in targetPose) { pose.Key.localPosition = pose.Value.Item1; pose.Key.localRotation = pose.Value.Item2; pose.Key.localScale = pose.Value.Item3; }
                    input.SampleAnimation(src, time);
                    // Transfer motion in model space relative to each rig's calibrated idle pose.
                    foreach (var pair in pairs)
                        pair.target.rotation = pair.source.rotation * Quaternion.Inverse(sourceRest[pair.source]) * targetRest[pair.target];
                    // Keep horizontal locomotion under the existing NavMesh/Fusion authority.
                    var displacement = sourceBones["Torso"].position - hipsRest;
                    targetBones["Bip01 Pelvis"].position = targetHipsRest + Vector3.up * displacement.y * ratio;
                    foreach (var entry in curves)
                    {
                        var q = entry.Key.localRotation;
                        var p = entry.Key.localPosition;
                        var values = new[] { q.x, q.y, q.z, q.w, p.x, p.y, p.z };
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidOperationException("Invalid pose: " + state.name);
                            entry.Value[i].AddKey(time, values[i]);
                        }
                    }
                }
                string[] properties = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
                foreach (var entry in curves)
                {
                    string path = AnimationUtility.CalculateTransformPath(entry.Key, dst.transform);
                    for (int i = 0; i < properties.Length; i++)
                        AnimationUtility.SetEditorCurve(output, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[i]), Reduce(entry.Value[i], i < 4 ? 0.0005f : 0.0001f));
                }
                output.EnsureQuaternionContinuity();
                AnimationUtility.SetAnimationEvents(output, AnimationUtility.GetAnimationEvents(input));
                var settings = AnimationUtility.GetAnimationClipSettings(input);
                AnimationUtility.SetAnimationClipSettings(output, settings);
                string assetPath = Folder + "/" + output.name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (existing != null) { EditorUtility.CopySerialized(output, existing); UnityEngine.Object.DestroyImmediate(output); output = existing; }
                else AssetDatabase.CreateAsset(output, assetPath);
                generated.Add(state, output);
            }
            foreach (var entry in generated) entry.Key.motion = entry.Value;
            foreach (var entry in protectedStates)
                if (entry.Key.motion != entry.Value) throw new InvalidOperationException("Protected motion changed: " + entry.Key.name);
            EditorUtility.SetDirty(controller);
            foreach (var state in generated.Keys) EditorUtility.SetDirty(state);
            AssetDatabase.SaveAssets();
            return "Baked " + generated.Count + " Lizard clips; protected Idle1/Walk1/Walk2/Punch unchanged.\n" + Validate();
        }
        finally { UnityEngine.Object.DestroyImmediate(src); UnityEngine.Object.DestroyImmediate(dst); }
    }

    static AnimationCurve Reduce(AnimationCurve input, float tolerance)
    {
        var keys = input.keys;
        var retained = new SortedSet<int> { 0, keys.Length - 1 };
        var pending = new Stack<(int first, int last)>();
        pending.Push((0, keys.Length - 1));
        while (pending.Count > 0)
        {
            var range = pending.Pop();
            float error = tolerance;
            int split = -1;
            for (int i = range.first + 1; i < range.last; i++)
            {
                float fraction = (keys[i].time - keys[range.first].time) / (keys[range.last].time - keys[range.first].time);
                float difference = Mathf.Abs(keys[i].value - Mathf.Lerp(keys[range.first].value, keys[range.last].value, fraction));
                if (difference > error) { error = difference; split = i; }
            }
            if (split < 0) continue;
            retained.Add(split);
            pending.Push((range.first, split));
            pending.Push((split, range.last));
        }
        var result = new AnimationCurve(retained.Select(i => keys[i]).ToArray());
        for (int i = 0; i < result.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(result, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(result, i, AnimationUtility.TangentMode.Linear);
        }
        return result;
    }

    public static string Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork_Lizard.prefab");
        var root = prefab.GetComponentsInChildren<Animator>(true).Single(x => x.enabled).gameObject;
        var paths = root.GetComponentsInChildren<Transform>(true).Select(x => AnimationUtility.CalculateTransformPath(x, root.transform)).ToHashSet();
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        var results = new List<string>();
        foreach (var state in c.layers[0].stateMachine.states.Select(x => x.state).Where(x => AssetDatabase.GetAssetPath(x.motion).StartsWith(Folder)))
        {
            var clip = (AnimationClip)state.motion;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Any(x => !paths.Contains(x.path))) throw new InvalidOperationException("Unbound curves in " + state.name);
            if (!bindings.Any(x => { var keys = AnimationUtility.GetEditorCurve(clip, x).keys; return keys.Max(k => k.value) - keys.Min(k => k.value) > 0.01f; }))
                throw new InvalidOperationException("Static clip " + state.name);
            results.Add(state.name + ": " + clip.length.ToString("F2") + "s, " + bindings.Length + " bound curves");
        }
        if (results.Count != 7) throw new InvalidOperationException("Expected seven retargeted clips.");
        return string.Join("\n", results);
    }

    public static string Preview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Target));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, scene);
        var cameraObject = new GameObject("Retarget preview camera", typeof(Camera));
        var lightObject = new GameObject("Retarget preview light", typeof(Light));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
        camera.transform.position = new Vector3(3, 2.2f, 4);
        camera.transform.LookAt(new Vector3(0, 1.1f, 0));
        camera.orthographic = true;
        camera.orthographicSize = 1.65f;
        lightObject.GetComponent<Light>().type = LightType.Directional;
        lightObject.GetComponent<Light>().intensity = 1.7f;
        lightObject.transform.rotation = Quaternion.Euler(35, -35, 0);
        var target = new RenderTexture(256, 320, 24);
        var tile = new Texture2D(256, 320, TextureFormat.RGB24, false);
        var sheet = new Texture2D(1024, 2240, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            var states = controller.layers[0].stateMachine.states.Select(x => x.state)
                .Where(x => AssetDatabase.GetAssetPath(x.motion).StartsWith(Folder)).ToArray();
            for (int row = 0; row < states.Length; row++)
                for (int col = 0; col < 4; col++)
                {
                    var clip = (AnimationClip)states[row].motion;
                    clip.SampleAnimation(model, clip.length * col / 3f);
                    foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
                    camera.Render();
                    RenderTexture.active = target;
                    tile.ReadPixels(new Rect(0, 0, 256, 320), 0, 0);
                    tile.Apply();
                    sheet.SetPixels(col * 256, (6 - row) * 320, 256, 320, tile.GetPixels());
                }
            sheet.Apply();
            System.IO.Directory.CreateDirectory("Temp/StalkerRetarget");
            System.IO.File.WriteAllBytes("Temp/StalkerRetarget/preview.png", sheet.EncodeToPNG());
            return "Temp/StalkerRetarget/preview.png; rows: " + string.Join(",", states.Select(x => x.name));
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
