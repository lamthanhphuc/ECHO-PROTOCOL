using System;
using System.IO;
using System.Linq;
using EchoProtocol.AI.Stalker.Networking;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EchoProtocol.Editor.Networking
{
    // Explicit menu operation; a Temp request also allows the current Editor to run after import.
    [InitializeOnLoad]
    internal static class GhostJumpscarePrefabSetup
    {
        private const string Folder = "Assets/Prefabs/Player/Jumpscare";
        private const string PrefabPath = Folder + "/GhostJumpscare.prefab";
        private const string Request = "Temp/GhostJumpscareSetup.request";
        private const string Report = "Temp/GhostJumpscareSetup.result.txt";

        static GhostJumpscarePrefabSetup() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                File.WriteAllText(Report, "BLOCKED: Editor must be outside Play Mode and compilation.");
                return;
            }
            File.Delete(Request);
            try { Setup(); }
            catch (Exception error) { File.WriteAllText(Report, error.ToString()); Debug.LogException(error); }
        }

        [MenuItem("ECHO Protocol/Setup/Ghost Jumpscare")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring prefabs.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork.prefab");
            var scream = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/stalker/chase_start.wav");
            if (source == null || scream == null) throw new InvalidOperationException("Missing Stalker model/audio.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePresentation(source);

            foreach (var path in new[] { "Assets/Prefabs/PlayerNetwork.prefab",
                         "Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab" })
            {
                var player = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var effect = player.GetComponent<PlayerJumpscareController>()
                        ?? player.AddComponent<PlayerJumpscareController>();
                    var settings = new SerializedObject(effect);
                    settings.FindProperty("_ghostJumpscarePrefab").objectReferenceValue = prefab;
                    settings.FindProperty("_scream").objectReferenceValue = scream;
                    settings.FindProperty("_ghostOffset").vector3Value = new Vector3(0f, 0f, 1f);
                    settings.FindProperty("_ghostEuler").vector3Value = new Vector3(0f, 180f, 0f);
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(player, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(player); }
            }

            var ghost = PrefabUtility.LoadPrefabContents("Assets/Prefabs/StalkerNetwork.prefab");
            try
            {
                var settings = new SerializedObject(ghost.GetComponent<StalkerFusionRuntime>());
                settings.FindProperty("maximumDamageDistance").floatValue = 2f;
                settings.FindProperty("jumpscareSeconds").floatValue = 2f;
                settings.FindProperty("catchCooldownSeconds").floatValue = 2f;
                settings.FindProperty("catchEndsInDeath").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(ghost, "Assets/Prefabs/StalkerNetwork.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(ghost); }
            AssetDatabase.SaveAssets();
            Validate();
        }

        private static GameObject CreatePresentation(GameObject source)
        {
            var sourceAnimator = source.GetComponentInChildren<Animator>(true);
            if (sourceAnimator == null) throw new InvalidOperationException("Stalker has no visual Animator.");
            var root = new GameObject("GhostJumpscare");
            try
            {
                var visual = UnityEngine.Object.Instantiate(sourceAnimator.gameObject, root.transform);
                visual.name = "Visual";
                // Keep model/rig/materials only. The root Animator owns the local lunge timeline.
                foreach (var component in visual.GetComponentsInChildren<Component>(true).Reverse())
                    if (!(component is Transform) && !(component is Renderer)
                        && !(component is MeshFilter) && !(component is Animator))
                        UnityEngine.Object.DestroyImmediate(component);
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                visual.transform.localPosition = Vector3.zero;
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var scale = 2.2f / Mathf.Max(0.01f, bounds.size.y);
                visual.transform.localScale *= scale;
                visual.transform.localPosition = new Vector3(-bounds.center.x * scale,
                    0.35f - bounds.max.y * scale, -bounds.center.z * scale);

                var clip = new AnimationClip { name = "JumpscareLunge", frameRate = 60f };
                var pos = visual.transform.localPosition;
                // Root faces camera (180 yaw), so increasing local Z lunges toward it.
                clip.SetCurve("Visual", typeof(Transform), "localPosition.z",
                    new AnimationCurve(new Keyframe(0f, pos.z), new Keyframe(.12f, pos.z + .32f),
                        new Keyframe(.3f, pos.z + .2f), new Keyframe(1.9f, pos.z + .2f)));
                AssetDatabase.CreateAsset(clip, Folder + "/JumpscareLunge.anim");
                var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/GhostJumpscare.controller");
                controller.AddParameter("Jumpscare", AnimatorControllerParameterType.Trigger);
                var machine = controller.layers[0].stateMachine;
                machine.defaultState = machine.AddState("Idle");
                var scare = machine.AddState("Jumpscare");
                scare.motion = clip;
                var transition = machine.defaultState.AddTransition(scare);
                transition.hasExitTime = false;
                transition.duration = 0f;
                transition.AddCondition(AnimatorConditionMode.If, 0f, "Jumpscare");
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var inner in visual.GetComponentsInChildren<Animator>(true))
                {
                    inner.applyRootMotion = false;
                    inner.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponentsInChildren<Fusion.NetworkObject>(true).Length != 0
                || prefab.GetComponentsInChildren<Collider>(true).Length != 0
                || prefab.GetComponentsInChildren<AudioSource>(true).Length != 0)
                throw new InvalidOperationException("Presentation prefab isolation failed.");
            foreach (var path in new[] { "Assets/Prefabs/PlayerNetwork.prefab",
                         "Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab" })
            {
                var component = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PlayerJumpscareController>();
                var settings = new SerializedObject(component);
                if (settings.FindProperty("_ghostJumpscarePrefab").objectReferenceValue != prefab
                    || settings.FindProperty("_scream").objectReferenceValue == null)
                    throw new InvalidOperationException("Player references not saved: " + path);
            }
            File.WriteAllText(Report, "PASS: presentation prefab, Animator trigger/lunge, both player references and Downed defaults saved. Audio: chase_start.wav (existing Stalker vocal placeholder). Multiplayer PlayMode not run.");
            Debug.Log("[GhostJumpscareSetup] " + File.ReadAllText(Report));
        }
    }
}
