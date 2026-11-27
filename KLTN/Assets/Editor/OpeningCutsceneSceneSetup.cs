using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoProtocol.Cinematics;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EchoProtocol.Editor.Cinematics
{
    /// <summary>Explicit request bridge for Editors without the Unity MCP connection.</summary>
    [InitializeOnLoad]
    public static class OpeningCutsceneSceneSetup
    {
        private const string RequestPath = "Temp/OpeningCutscene.v2.request";
        private const string ReportPath = "Temp/OpeningCutscene.report.txt";
        private const string PlayTestsPending = "EchoProtocol.OpeningCutscene.PlayTestsPending";

        private static PreviewSession _preview;
        private static double _nextRequestPollAt;

        static OpeningCutsceneSceneSetup()
        {
            EditorApplication.update += ProcessRequest;
            AssemblyReloadEvents.beforeAssemblyReload += () => _preview?.Dispose();
            if (SessionState.GetBool(PlayTestsPending, false)) RegisterTestCallbacks(true);
        }

        private static void ProcessRequest()
        {
            if (_preview != null)
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating) _preview.Tick();
                return;
            }
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup < _nextRequestPollAt) return;
            _nextRequestPollAt = EditorApplication.timeSinceStartup + 0.2d;
            if (!TryConsumeRequest(RequestPath, out string command)) return;
            try
            {
                if (command == "survey") Survey();
                else if (command == "purge-survey") PurgeOpeningSceneSetup.Survey();
                else if (command == "purge-build") PurgeOpeningSceneSetup.Build();
                else if (command == "purge-preview") PreviewPurge();
                else if (command == "purge-play-tests") RunPurgePlayTests();
                else if (command == "build") BuildSciFi();
                else if (command == "preview") Preview();
                else if (command == "tests") RunTests();
                else if (command == "refresh") AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                else if (command == "polish")
                {
                    var definition = AssetDatabase.LoadAssetAtPath<OpeningCutsceneDefinition>(DefinitionPath);
                    foreach (var shot in definition.shots) shot.fillMultiplier = 1f;
                    definition.shots[2].fillMultiplier = 0.35f;
                    EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                    File.WriteAllText(ReportPath, "SciFi mission shot fill adjusted; camera paths preserved.");
                }
                else if (command == "frame-threat") FrameThreatCorridor();
                else throw new ArgumentException("Unknown opening cutscene request: " + command);
            }
            catch (Exception error)
            {
                File.WriteAllText(ReportPath, error.ToString());
                Debug.LogException(error);
            }
        }

        internal static bool TryConsumeRequest(string path, out string command)
        {
            command = null;
            string claimedPath = path + "." + Guid.NewGuid().ToString("N") + ".claimed";
            bool consumed = false;
            try
            {
                // Producers publish a complete same-directory temp file by rename.
                // Claim by rename so only one consumer can dispatch the request.
                if (!File.Exists(path)) return false;
                File.Move(path, claimedPath);
                string pending;
                using (var stream = new FileStream(claimedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    pending = reader.ReadToEnd().Trim();
                }
                File.Delete(claimedPath);
                consumed = true;
                if (pending.Length == 0) return false;
                command = pending;
                return true;
            }
            catch (IOException)
            {
                // Includes concurrent claims and a producer removing/replacing the file.
                command = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                command = null;
                return false;
            }
            finally
            {
                try
                {
                    if (!consumed && File.Exists(claimedPath) && !File.Exists(path))
                        File.Move(claimedPath, path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        [MenuItem("ECHO Protocol/Cinematics/Survey Gameplay Scenes")]
        public static void Survey()
        {
            var report = new StringBuilder();
            report.AppendLine("Project: " + Application.dataPath);
            report.AppendLine("Play mode: " + EditorApplication.isPlaying);
            var active = SceneManager.GetActiveScene();
            foreach (string name in new[] { "SciFi", "Scene_A", "Scene_B" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    report.AppendLine("SCENE " + path);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        string hierarchy = HierarchyPath(t);
                        if (t.parent != null && !System.Text.RegularExpressions.Regex.IsMatch(hierarchy,
                            "spawn|terminal|corridor|door|junction|power|sector|core|stalker|monster|player|hall|research|security|light|zone|server",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                        report.Append(hierarchy).Append(" | position=").Append(t.position.ToString("F3"))
                            .Append(" rotation=").Append(t.eulerAngles.ToString("F3"))
                            .Append(" active=").Append(t.gameObject.activeSelf).Append(" | ")
                            .AppendLine(string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("[OpeningCutscene] Scene survey written to " + ReportPath);
        }

        internal static string HierarchyPath(Transform target) => target.parent == null
            ? target.name : HierarchyPath(target.parent) + "/" + target.name;

        private const string AssetFolder = "Assets/_Project/Cinematics";
        private const string DefinitionPath = AssetFolder + "/SciFiOpeningDefinition.asset";
        private const string TimelinePath = AssetFolder + "/SciFiOpening.playable";
        private const string Zone1 = "GameObject/Zone01_ResearchStorage/";

        private static Transform Require(Scene scene, string path)
        {
            var parts = path.Split('/');
            var root = scene.GetRootGameObjects().SingleOrDefault(obj => obj.name == parts[0]);
            var value = root == null ? null : (parts.Length == 1 ? root.transform
                : root.transform.Find(string.Join("/", parts.Skip(1))));
            if (value == null || !value.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Missing active cinematic source: " + path);
            return value;
        }

        [MenuItem("ECHO Protocol/Cinematics/Build SciFi Opening")]
        public static void BuildSciFi()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/SciFi.unity");
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Additive);
            try
            {
                var existing = scene.GetRootGameObjects().FirstOrDefault(obj => obj.GetComponent<OpeningCutsceneController>() != null);
                if (existing != null)
                {
                    EnsureFill(existing.GetComponent<OpeningCutsceneController>());
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    File.WriteAllText(ReportPath, "Opening already exists; preserving authored poses. Use Rebuild Timeline From Anchors to bake edits.");
                    return;
                }
                // Resolve EVERY source before creating assets. Disabled sample geometry is never eligible.
                var entry = Require(scene, Zone1 + "AED_Z1_RESOURCE_ENTRY");
                var room = Require(scene, Zone1 + "01_Start_Area_EMPTY/Generated_StartRoom_FromPrefab");
                var server = Require(scene, Zone1 + "04_Server_Room_C2_EMPTY/route/Corridor Wide");
                var shelf = Require(scene, Zone1 + "04_Server_Room_C2_EMPTY/Props/GameObject/Shelf Variation 01");
                var hub = Require(scene, "NetworkSectorBox");
                var threat = Require(scene, Zone1 + "03_Central_Junction/route/Corridor Passthrough (1)");
                var threatLook = Require(scene, Zone1 + "03_Central_Junction/route/Corridor Passthrough (2)");
                var spawn = Require(scene, "GameObject/SpawnPoints/PlayerStart_EMPTY");

                Directory.CreateDirectory(AssetFolder);
                AssetDatabase.Refresh();
                var definition = AssetDatabase.LoadAssetAtPath<OpeningCutsceneDefinition>(DefinitionPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<OpeningCutsceneDefinition>();
                    AssetDatabase.CreateAsset(definition, DefinitionPath);
                }
                definition.shots = new[]
                {
                    MakeShot("Shot01_Establishing", entry, room, 6f,
                        "Đội phản ứng ECHO. Khu vực nghiên cứu đang bị phong tỏa.",
                        "ECHO response team. The research facility is under lockdown.", ""),
                    MakeShot("Shot02_Incident", server, shelf, 6f,
                        "Sau sự cố, nhiệm vụ của đội là khôi phục hệ thống và tìm đường thoát.",
                        "Following the incident, restore the systems and find a way out.", "security_terminal/terminal_boot"),
                    MakeShot("Shot03_Mission", hub, hub, 7f,
                        "Tìm các Lõi Năng Lượng và đưa chúng về bộ tiếp nhận.",
                        "Locate the Energy Cores and bring them to the power hub.", "ui/objective_update"),
                    MakeShot("Shot04_Threat", threat, threatLook, 5f,
                        "", "", ""),
                    MakeShot("Shot05_PlayerSpawn", spawn, entry, 6f,
                        "Phối hợp cùng đồng đội. Hoàn thành nhiệm vụ và thoát khỏi khu phong tỏa.",
                        "Work together. Complete the mission and escape the lockdown.", "ui/match_start")
                };
                definition.sceneName = "SciFi";
                // The hub close-up uses its actual visual bounds, without exposing core spawn locations.
                var hubVisual = hub.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.enabled);
                var focus = hubVisual != null ? hubVisual.bounds.center : hub.position + Vector3.up;
                var hubShot = definition.shots[2];
                hubShot.fillMultiplier = 0.35f;
                hubShot.startPosition = FreePose(hub.position - hub.forward * 3.5f);
                hubShot.endPosition = SafeEnd(hubShot.startPosition, focus);
                hubShot.startEuler = Quaternion.LookRotation(focus - hubShot.startPosition).eulerAngles;
                hubShot.endEuler = Quaternion.LookRotation(focus - hubShot.endPosition).eulerAngles;

                var root = new GameObject("OpeningCutscene");
                SceneManager.MoveGameObjectToScene(root, scene);
                var director = root.AddComponent<PlayableDirector>();
                director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.Hold;
                var cameraObject = new GameObject("CinematicCamera", typeof(Camera), typeof(AudioListener), typeof(Animator));
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.fieldOfView = definition.fieldOfView; camera.nearClipPlane = 0.05f; camera.farClipPlane = 500;
                camera.enabled = false;
                var listener = cameraObject.GetComponent<AudioListener>(); listener.enabled = false;
                var anchors = new GameObject("ShotAnchors").transform; anchors.SetParent(root.transform, false);
                for (int i = 0; i < definition.shots.Length; i++)
                {
                    var shot = definition.shots[i];
                    var start = new GameObject(shot.name).transform; start.SetParent(anchors, false);
                    start.SetPositionAndRotation(shot.startPosition, Quaternion.Euler(shot.startEuler));
                    var end = new GameObject("End").transform; end.SetParent(start, false);
                    end.SetPositionAndRotation(shot.endPosition, Quaternion.Euler(shot.endEuler));
                    var anchor = start.gameObject.AddComponent<OpeningCutsceneShotAnchor>();
                    anchor.shotIndex = i; anchor.end = end; anchor.source = Require(scene, shot.sourceHierarchy);
                }
                var audioObject = new GameObject("CinematicAudio", typeof(AudioSource)); audioObject.transform.SetParent(root.transform, false);
                var audio = audioObject.GetComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0f;
                var presentation = root.AddComponent<OpeningCutscenePresentation>(); presentation.Build(root.transform);
                var controller = root.AddComponent<OpeningCutsceneController>();
                controller.Configure(definition, director, camera, listener, audio, presentation);
                EnsureFill(controller);
                Bake(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(ReportPath, "SciFi opening built and saved. Duration: " + definition.Duration);
                Debug.Log("[OpeningCutscene] SciFi opening built and saved.");
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static OpeningCutsceneDefinition.Shot MakeShot(string name, Transform source, Transform target,
            float duration, string subtitle, string english, string audio)
        {
            var start = FreePose(source.position);
            var lookAt = target.position + Vector3.up * 1.6f;
            if ((lookAt - start).sqrMagnitude < 0.5f) lookAt = start + source.forward * 5f;
            var end = SafeEnd(start, lookAt);
            return new OpeningCutsceneDefinition.Shot { name = name, sourceHierarchy = HierarchyPath(source),
                startPosition = start, endPosition = end, startEuler = Quaternion.LookRotation(lookAt - start).eulerAngles,
                endEuler = Quaternion.LookRotation(lookAt - end).eulerAngles, duration = duration,
                subtitle = subtitle, subtitleEnglish = english, audioKey = audio };
        }

        private static Vector3 FreePose(Vector3 seed)
        {
            // NavMesh belongs to the inspected gameplay scene. All offsets are camera composition, never guessed map coordinates.
            foreach (var offset in new[] { Vector3.zero, Vector3.forward * 2, Vector3.back * 2, Vector3.right * 2, Vector3.left * 2 })
            {
                if (!NavMesh.SamplePosition(seed + offset, out var hit, 6f, NavMesh.AllAreas)) continue;
                var pose = hit.position + Vector3.up * 1.65f;
                if (!Physics.CheckSphere(pose, 0.14f, ~0, QueryTriggerInteraction.Ignore)) return pose;
            }
            throw new InvalidOperationException("No clear camera pose on the scene NavMesh near " + seed);
        }

        private static Vector3 SafeEnd(Vector3 start, Vector3 focus)
        {
            var motion = Vector3.ProjectOnPlane(focus - start, Vector3.up).normalized * 0.8f;
            return Physics.SphereCast(start, 0.15f, motion.normalized, out _, motion.magnitude, ~0, QueryTriggerInteraction.Ignore)
                ? start : start + motion;
        }

        private static void EnsureFill(OpeningCutsceneController controller)
        {
            var camera = controller.CinematicCamera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.012f, 0.016f);
            camera.useOcclusionCulling = false;
            if (controller.FillLight != null) return;
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
            var source = player != null ? player.GetComponentInChildren<Light>(true) : null;
            if (source == null) throw new InvalidOperationException("Player flashlight asset is missing.");
            var obj = new GameObject("CinematicFill", typeof(Light)); obj.transform.SetParent(camera.transform, false);
            var fill = obj.GetComponent<Light>();
            EditorUtility.CopySerialized(source, fill);
            var driver = player.GetComponent<EchoProtocol.Networking.NetworkPlayerFlashlight>();
            if (driver != null)
            {
                // Match the beam the existing runtime actually configures, at half intensity for the cinematic.
                var settings = new SerializedObject(driver);
                fill.intensity = settings.FindProperty("_beamIntensity").floatValue * 0.5f;
                fill.range = settings.FindProperty("_beamRange").floatValue;
                fill.spotAngle = settings.FindProperty("_beamSpotAngle").floatValue;
                fill.innerSpotAngle = settings.FindProperty("_beamInnerSpotAngle").floatValue;
            }
            fill.enabled = false;
            controller.SetFillLight(fill);
            EditorUtility.SetDirty(controller);
        }

        private static void FrameThreatCorridor()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/SciFi.unity");
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().Select(root => root.GetComponent<OpeningCutsceneController>()).First(c => c != null);
                var floor = Require(scene, Zone1 + "03_Central_Junction/route/Corridor Passthrough (1)/Tunnel Floor");
                var start = FreePose(floor.GetComponent<Renderer>().bounds.center);
                var direction = floor.forward;
                float longest = -1;
                // Choose the open corridor using real collision geometry, rather than facing a nearby door post.
                for (int yaw = 0; yaw < 360; yaw += 30)
                {
                    var candidate = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    float distance = Physics.Raycast(start, candidate, out var hit, 25f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 25f;
                    if (distance > longest) { longest = distance; direction = candidate; }
                }
                var anchor = controller.GetComponentsInChildren<OpeningCutsceneShotAnchor>().Single(a => a.shotIndex == 3);
                var rotation = Quaternion.LookRotation(direction);
                anchor.transform.SetPositionAndRotation(start, rotation);
                anchor.end.SetPositionAndRotation(SafeEnd(start, start + direction * 10), rotation);
                anchor.source = floor;
                controller.Definition.shots[3].sourceHierarchy = HierarchyPath(floor);
                controller.Definition.shots[3].fillMultiplier = 0.5f;
                Bake(controller);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                File.WriteAllText(ReportPath, $"Threat shot reframed from real floor bounds. Clear view: {longest:F2}m.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem("ECHO Protocol/Cinematics/Rebuild Timeline From Anchors")]
        public static void RebuildFromAnchors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var controller = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<OpeningCutsceneController>() : null;
            if (controller == null) controller = UnityEngine.Object.FindAnyObjectByType<OpeningCutsceneController>();
            if (controller == null) throw new InvalidOperationException("Open SciFi and select OpeningCutscene.");
            Bake(controller); EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        internal static void Bake(OpeningCutsceneController controller)
        {
            var definition = controller.Definition;
            string clipFolder = definition.sceneName == "SciFi" ? AssetFolder : AssetFolder + "/" + definition.sceneName;
            Directory.CreateDirectory(clipFolder);
            string timelinePath = definition.timeline != null ? AssetDatabase.GetAssetPath(definition.timeline)
                : definition.sceneName == "SciFi" ? TimelinePath
                : clipFolder + (definition.sceneName == AlienHuntSceneName ? "/PurgeOpeningTimeline.playable" : "/" + definition.sceneName + "Opening.playable");
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, timelinePath); }
            foreach (var track in timeline.GetOutputTracks().Where(track => track is AnimationTrack && track.name == "Cinematic Camera").ToArray())
                timeline.DeleteTrack(track);
            var animation = timeline.CreateTrack<AnimationTrack>(null, "Cinematic Camera");
            animation.trackOffset = TrackOffset.ApplyTransformOffsets;
            double time = 0;
            foreach (var anchor in controller.GetComponentsInChildren<OpeningCutsceneShotAnchor>().OrderBy(a => a.shotIndex))
            {
                var shot = definition.shots[anchor.shotIndex];
                shot.startPosition = anchor.transform.position; shot.endPosition = anchor.end.position;
                shot.startEuler = anchor.transform.eulerAngles; shot.endEuler = anchor.end.eulerAngles;
                string clipPath = clipFolder + "/" + shot.name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) { clip = new AnimationClip { name = shot.name }; AssetDatabase.CreateAsset(clip, clipPath); }
                clip.ClearCurves(); clip.frameRate = 30;
                for (int axis = 0; axis < 3; axis++)
                {
                    string suffix = "." + "xyz"[axis];
                    var parent = controller.CinematicCamera.transform.parent;
                    var startPosition = parent != null ? parent.InverseTransformPoint(shot.startPosition) : shot.startPosition;
                    var endPosition = parent != null ? parent.InverseTransformPoint(shot.endPosition) : shot.endPosition;
                    var startEuler = parent != null ? (Quaternion.Inverse(parent.rotation) * Quaternion.Euler(shot.startEuler)).eulerAngles : shot.startEuler;
                    var endEuler = parent != null ? (Quaternion.Inverse(parent.rotation) * Quaternion.Euler(shot.endEuler)).eulerAngles : shot.endEuler;
                    clip.SetCurve("", typeof(Transform), "localPosition" + suffix,
                        AnimationCurve.EaseInOut(0, startPosition[axis], shot.duration, endPosition[axis]));
                    clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw" + suffix,
                        AnimationCurve.EaseInOut(0, startEuler[axis], shot.duration,
                            startEuler[axis] + Mathf.DeltaAngle(startEuler[axis], endEuler[axis])));
                }
                var timelineClip = animation.CreateClip(clip);
                timelineClip.displayName = shot.name; timelineClip.start = time; timelineClip.duration = shot.duration;
                var playable = (AnimationPlayableAsset)timelineClip.asset;
                playable.removeStartOffset = false; playable.applyFootIK = false;
                time += shot.duration;
                EditorUtility.SetDirty(clip);
            }
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = time;
            definition.timeline = timeline;
            controller.Director.playableAsset = timeline;
            controller.Director.SetGenericBinding(animation, controller.CinematicCamera.GetComponent<Animator>());
            controller.CinematicCamera.fieldOfView = definition.fieldOfView;
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(definition); EditorUtility.SetDirty(controller.Director);
            AssetDatabase.SaveAssets();
            ExportShots(definition);
        }

        private static void ExportShots(OpeningCutsceneDefinition definition)
        {
            var report = new StringBuilder("\n| Shot | Reference | Start position / Euler | End position / Euler | Seconds | Audio key |\n| --- | --- | --- | --- | --- | --- |\n");
            foreach (var shot in definition.shots)
                report.AppendLine($"| {shot.name} | `{shot.sourceHierarchy}` | `{shot.startPosition:F3}` / `{shot.startEuler:F3}` | `{shot.endPosition:F3}` / `{shot.endEuler:F3}` | {shot.duration} | `{shot.audioKey}` |");
            string path = definition.sceneName == AlienHuntSceneName
                ? "../docs/PURGE_OPENING_CUTSCENE_STORYBOARD.md" : "../docs/OPENING_CUTSCENE_STORYBOARD.md";
            if (File.Exists(path))
            {
                var doc = File.ReadAllText(path);
                const string start = "<!-- SHOT_POSES_START -->", end = "<!-- SHOT_POSES_END -->";
                int first = doc.IndexOf(start, StringComparison.Ordinal), last = doc.IndexOf(end, StringComparison.Ordinal);
                if (first >= 0 && last > first)
                    File.WriteAllText(path, doc.Substring(0, first + start.Length) + "\n" + report + "\n" + doc.Substring(last));
            }
            File.WriteAllText("Temp/OpeningCutscene.poses.md", report.ToString());
        }

        [MenuItem("ECHO Protocol/Cinematics/Render SciFi Shot Previews")]
        public static void Preview() => StartPreview("SciFi", "Temp/OpeningCutscene");

        private const string AlienHuntSceneName = EchoProtocol.Networking.AlienHuntMission.SceneName;

        [MenuItem("ECHO Protocol/Cinematics/Render Purge Shot Previews")]
        public static void PreviewPurge() => StartPreview(AlienHuntSceneName, "Temp/PurgeOpening");

        private static void StartPreview(string sceneName, string folder)
        {
            if (_preview != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _preview = new PreviewSession(sceneName, folder);
        }

        // One URP request per Editor update, with asynchronous readback. Never nest Camera.Render calls.
        private sealed class PreviewSession : IDisposable
        {
            private readonly Scene _active, _scene;
            private readonly bool _opened, _lightEnabled;
            private readonly OpeningCutsceneController _controller;
            private readonly RenderTexture _texture;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly string _folder;
            private AsyncGPUReadbackRequest _readback;
            private double _progressAt;
            private readonly float _fillIntensity;
            private int _shot, _settle = 4;
            private bool _pending, _disposed;
            private string _error;

            public PreviewSession(string sceneName, string folder)
            {
                _folder = folder;
                _active = SceneManager.GetActiveScene();
                string path = "Assets/Scenes/" + sceneName + ".unity";
                _scene = SceneManager.GetSceneByPath(path);
                _opened = !_scene.IsValid() || !_scene.isLoaded;
                if (_opened) _scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(_scene);
                _controller = _scene.GetRootGameObjects().Select(obj => obj.GetComponent<OpeningCutsceneController>()).First(c => c != null);
                _position = _controller.CinematicCamera.transform.position;
                _rotation = _controller.CinematicCamera.transform.rotation;
                _lightEnabled = _controller.FillLight != null && _controller.FillLight.enabled;
                _fillIntensity = _controller.FillLight != null ? _controller.FillLight.intensity : 0f;
                if (_controller.FillLight != null) _controller.FillLight.enabled = true;
                _texture = new RenderTexture(960, 540, 24);
                Directory.CreateDirectory(_folder);
                _progressAt = EditorApplication.timeSinceStartup;
            }

            public void Tick()
            {
                if (_disposed) return;
                if (_error != null || _shot >= _controller.Definition.shots.Length || EditorApplication.timeSinceStartup - _progressAt > 90)
                {
                    File.WriteAllText(ReportPath, _error ?? (_shot >= _controller.Definition.shots.Length
                        ? "Rendered shot previews in " + _folder : "Preview timed out; gameplay assets were preserved."));
                    Dispose(); _preview = null; return;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                if (_pending) { _readback.Update(); return; }
                if (_settle-- > 0)
                {
                    _controller.SetShotVisuals(_shot);
                    return;
                }
                try
                {
                    var camera = _controller.CinematicCamera;
                    var shot = _controller.Definition.shots[_shot];
                    _controller.SetShotVisuals(_shot);
                    if (_controller.FillLight != null) _controller.FillLight.intensity = _fillIntensity * shot.fillMultiplier;
                    camera.transform.SetPositionAndRotation(shot.startPosition, Quaternion.Euler(shot.startEuler));
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _texture };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP camera render requests unavailable.");
                    _pending = true;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    _readback = AsyncGPUReadback.Request(_texture, 0, TextureFormat.RGB24, result =>
                    {
                        if (_disposed) return;
                        try
                        {
                            if (result.hasError) throw new InvalidOperationException("Preview GPU readback failed.");
                            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                            try
                            {
                                pixels.LoadRawTextureData(result.GetData<byte>()); pixels.Apply();
                                File.WriteAllBytes($"{_folder}/shot{_shot + 1:00}.png", pixels.EncodeToPNG());
                            }
                            finally { UnityEngine.Object.DestroyImmediate(pixels); }
                            _shot++; _settle = 3; _progressAt = EditorApplication.timeSinceStartup;
                        }
                        catch (Exception error) { _error = error.ToString(); }
                        finally { _pending = false; }
                    });
                    _readback.forcePlayerLoopUpdate = true;
                }
                catch (Exception error) { _pending = false; _error = error.ToString(); }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_controller != null)
                {
                    _controller.SetShotVisuals(-1);
                    if (_controller.FillLight != null) _controller.FillLight.enabled = _lightEnabled;
                    if (_controller.FillLight != null) _controller.FillLight.intensity = _fillIntensity;
                    _controller.CinematicCamera.transform.SetPositionAndRotation(_position, _rotation);
                }
                if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture);
                if (_active.IsValid() && _active.isLoaded) SceneManager.SetActiveScene(_active);
                if (_opened && _scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [MenuItem("ECHO Protocol/Cinematics/Run Opening And Loading Tests")]
        public static void RunTests()
        {
            File.WriteAllText("Temp/OpeningCutscene.test-failures.txt", string.Empty);
            var api = RegisterTestCallbacks(false);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                groupNames = new[] { "OpeningCutsceneRulesTests", "OpeningCutsceneLifecycleTests", "MatchLoadingBarrierTests",
                    "OpeningCutsceneRequestFileTests",
                    "MatchLoadingLifecycleTests", "CinematicMatchLoadingScreenTests", "NetworkMatchStateTests",
                    "PlayerSpawnerContractTests", "FusionPlayerLifecycleContractTests", "AlienHuntSceneTests",
                    "PurgeDirectorTests", "PurgeGameplayFixTests", "PurgePresentationRulesTests", "PurgeHUDPresentationTests",
                    "MatchResultOutboxSerializationTests", "CreditsTests" } }) { runSynchronously = true });
        }

        [MenuItem("ECHO Protocol/Cinematics/Run Purge Play Mode Tests")]
        public static void RunPurgePlayTests()
        {
            File.WriteAllText("Temp/OpeningCutscene.play-test-failures.txt", string.Empty);
            SessionState.SetBool(PlayTestsPending, true);
            var api = RegisterTestCallbacks(true);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                testNames = new[] {
                    "EchoProtocol.Networking.Tests.MapGameplayPlayModeTests.BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien",
                    "EchoProtocol.Networking.Tests.MapGameplayPlayModeTests.DirectorRageUvSupplyAndCatchRunOnStateAuthority"
                } }));
        }

        private static TestRunnerApi RegisterTestCallbacks(bool playMode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestResults(api, playMode));
            return api;
        }

        private sealed class TestResults : ICallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly bool _playMode;
            public TestResults(TestRunnerApi api, bool playMode) { _api = api; _playMode = playMode; }
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.FailCount > 0)
                    File.AppendAllText(_playMode ? "Temp/OpeningCutscene.play-test-failures.txt" : "Temp/OpeningCutscene.test-failures.txt",
                        result.FullName + "\n" + result.Message + "\n");
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                File.WriteAllText(_playMode ? "Temp/OpeningCutscene.play-tests.xml" : "Temp/OpeningCutscene.tests.xml", result.ToXml().OuterXml);
                if (_playMode) SessionState.SetBool(PlayTestsPending, false);
                File.WriteAllText(ReportPath, $"Tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
                Debug.Log($"[OpeningCutscene] {(_playMode ? "PlayMode" : "EditMode")} tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
                _api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(_api);
            }
        }
    }
}
