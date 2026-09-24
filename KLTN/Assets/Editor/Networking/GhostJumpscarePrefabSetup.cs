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
        private const string ScreamPath = "Assets/Audio/horror_ambience/distant_monster_sound.wav";
        private const string AudioRequest = "Temp/GhostJumpscareAudioSetup.request";
        private const string AudioReport = "Temp/GhostJumpscareAudioSetup.result.txt";

        static GhostJumpscarePrefabSetup() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (File.Exists(AudioRequest)
                && !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling)
            {
                File.Delete(AudioRequest);

                try
                {
                    SetupAudio();
                }
                catch (Exception error)
                {
                    File.WriteAllText(
                        AudioReport,
                        error.ToString());

                    Debug.LogException(error);
                }
            }

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
            var scream =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    ScreamPath);
            if (source == null || scream == null) throw new InvalidOperationException("Missing Stalker model/audio.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(
                PrefabPath);

            AssetDatabase.DeleteAsset(
                Folder + "/JumpscareLunge.anim");

            AssetDatabase.DeleteAsset(
                Folder + "/GhostJumpscare.controller");

            AssetDatabase.Refresh();

            var prefab =
                CreatePresentation(source);

            foreach (var path in PlayerPrefabPaths())
            {
                var player = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var effect = player.GetComponent<PlayerJumpscareController>()
                        ?? player.AddComponent<PlayerJumpscareController>();
                    var settings = new SerializedObject(effect);
                    settings.FindProperty("_ghostJumpscarePrefab").objectReferenceValue = prefab;
                    settings.FindProperty("_scream").objectReferenceValue = scream;
                    settings.FindProperty("_ghostOffset").vector3Value = new Vector3(0f, -1.5f, 1f);
                    settings.FindProperty("_ghostEuler").vector3Value = new Vector3(0f, 180f, 0f);
                    settings.FindProperty(
                            "_stalkerBiteSeconds")
                        .floatValue = 1.5f;
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

        [MenuItem("ECHO Protocol/Setup/Ghost Jumpscare Audio")]
        public static void SetupAudio()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before configuring audio.");
            }

            AssetDatabase.ImportAsset(
                ScreamPath,
                ImportAssetOptions.ForceSynchronousImport);

            var importer =
                AssetImporter.GetAtPath(ScreamPath)
                    as AudioImporter;

            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Missing jumpscare audio.");
            }

            var audioSettings =
                importer.defaultSampleSettings;

            audioSettings.loadType =
                AudioClipLoadType.DecompressOnLoad;

            audioSettings.compressionFormat =
                AudioCompressionFormat.PCM;

            audioSettings.preloadAudioData = true;

            importer.defaultSampleSettings =
                audioSettings;

            importer.SaveAndReimport();

            var scream =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    ScreamPath);

            if (scream == null || scream.length <= 0f)
            {
                throw new InvalidOperationException(
                    "Missing or empty jumpscare audio clip.");
            }

            foreach (var path in PlayerPrefabPaths())
            {
                var player =
                    PrefabUtility.LoadPrefabContents(path);

                try
                {
                    var effect =
                        player.GetComponent<
                            PlayerJumpscareController>();

                    if (effect == null)
                    {
                        throw new InvalidOperationException(
                            "Missing jumpscare controller: "
                            + path);
                    }

                    var settings =
                        new SerializedObject(effect);

                    settings.FindProperty("_scream")
                        .objectReferenceValue =
                        scream;

                    settings.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(
                        player,
                        path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(
                        player);
                }

                var saved =
                    new SerializedObject(
                        AssetDatabase
                            .LoadAssetAtPath<GameObject>(path)
                            .GetComponent<
                                PlayerJumpscareController>());

                if (saved.FindProperty("_scream")
                        .objectReferenceValue
                    != scream)
                {
                    throw new InvalidOperationException(
                        "Audio reference not saved: "
                        + path);
                }
            }

            AssetDatabase.SaveAssets();

            File.WriteAllText(
                AudioReport,
                "PASS: distant_monster_sound.wav, preloaded PCM, existing player prefab audio references saved and verified. PlayMode listening not run.");

            Debug.Log(
                "[GhostJumpscareAudioSetup] "
                + File.ReadAllText(AudioReport));
        }

        private static
            System.Collections.Generic.IEnumerable<string>
                PlayerPrefabPaths()
        {
            yield return
                "Assets/Prefabs/PlayerNetwork.prefab";

            const string legacyTestPlayer =
                "Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab";

            if (File.Exists(legacyTestPlayer))
            {
                yield return legacyTestPlayer;
            }
        }

        private static GameObject CreatePresentation(GameObject source)
        {
            var sourceAnimator =
                source.GetComponentsInChildren<Animator>(true)
                    .FirstOrDefault(
                        animator =>
                            animator.runtimeAnimatorController != null
                            && AssetDatabase.GetAssetPath(
                                animator.runtimeAnimatorController)
                            == "Assets/Animations/Stalker/AC_Stalker.controller");

            if (sourceAnimator == null)
            {
                throw new InvalidOperationException(
                    "Stalker AC_Stalker Animator not found.");
            }

            var root = new GameObject("GhostJumpscare");
            try
            {
                var visual = UnityEngine.Object.Instantiate(sourceAnimator.gameObject, root.transform);
                visual.name = "Visual";

                var visualAnimator =
                    visual.GetComponent<Animator>()
                    ?? visual.GetComponentInChildren<Animator>(true);

                if (visualAnimator == null)
                {
                    throw new InvalidOperationException(
                        "Jumpscare visual Animator missing.");
                }

                visualAnimator.runtimeAnimatorController =
                    sourceAnimator.runtimeAnimatorController;

                visualAnimator.applyRootMotion = false;
                visualAnimator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;

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

            var visualAnimator =
                prefab.GetComponentsInChildren<Animator>(true)
                    .FirstOrDefault(
                        animator =>
                            animator.gameObject != prefab);

            if (visualAnimator == null)
            {
                throw new InvalidOperationException(
                    "Jumpscare visual Animator missing.");
            }

            var controllerPath =
                AssetDatabase.GetAssetPath(
                    visualAnimator.runtimeAnimatorController);

            if (controllerPath
                != "Assets/Animations/Stalker/AC_Stalker.controller")
            {
                throw new InvalidOperationException(
                    $"Wrong visual Animator: {controllerPath}");
            }

            foreach (var path in PlayerPrefabPaths())
            {
                var component = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PlayerJumpscareController>();
                var settings = new SerializedObject(component);
                if (settings.FindProperty("_ghostJumpscarePrefab").objectReferenceValue != prefab
                    || settings.FindProperty("_scream").objectReferenceValue
                    != AssetDatabase.LoadAssetAtPath<AudioClip>(
                        ScreamPath))
                    throw new InvalidOperationException("Player references not saved: " + path);
            }
            File.WriteAllText(Report, "PASS: presentation prefab, Animator trigger/lunge, existing player references and Downed defaults saved. Audio: distant_monster_sound.wav. Multiplayer PlayMode not run.");
            Debug.Log("[GhostJumpscareSetup] " + File.ReadAllText(Report));
        }
    }
}
