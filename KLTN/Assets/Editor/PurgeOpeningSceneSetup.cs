using System;
using System.IO;
using System.Linq;
using System.Text;
using EchoProtocol.AI.Stalker.Presentation;
using EchoProtocol.Cinematics;
using EchoProtocol.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace EchoProtocol.Editor.Cinematics
{
    public static class PurgeOpeningSceneSetup
    {
        internal const string ScenePath = "Assets/Scenes/Scene_B.unity";
        private const string Folder = "Assets/_Project/Cinematics/Scene_B";
        private const string BossPath = "Assets/Prefabs/StalkerNetwork_Lizard.prefab";
        private const string Report = "Temp/OpeningCutscene.report.txt";

        [MenuItem("ECHO Protocol/Cinematics/Survey Purge Scene")]
        public static void Survey() => WithScene(scene =>
        {
            var objects = SceneComponents<Transform>(scene);
            var mission = SceneComponents<AlienHuntMission>(scene).Single();
            var report = new StringBuilder($"{scene.path} | Play mode: {EditorApplication.isPlaying}\n");
            foreach (var item in objects.Where(t => t.gameObject.activeInHierarchy &&
                         (t.name.Contains("WaterPlane") || t.name.StartsWith("3D_Water") || t.name.Contains("PlayerStart")
                          || t.name == "MonsterSpawn_Stalker_EMPTY" || t.GetComponent<Camera>() != null)))
                Append(report, item);
            foreach (var item in mission.Furnaces) Append(report, item.transform);
            foreach (var item in mission.Actors) Append(report, item.transform);
            report.AppendLine($"Active lights: {SceneComponents<Light>(scene).Count(t => t.isActiveAndEnabled)}");
            foreach (var source in SceneComponents<AudioSource>(scene).Where(t => t.isActiveAndEnabled))
                report.AppendLine($"Audio: {source.name} / {source.clip?.name}");
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
            foreach (var skin in boss.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine($"Boss visual: {skin.name}, mesh={skin.sharedMesh?.name}, active={skin.gameObject.activeInHierarchy}");
            File.WriteAllText(Report, report.ToString());
        });

        [MenuItem("ECHO Protocol/Cinematics/Build Purge Opening")]
        public static void Build() => WithScene(scene =>
        {
            if (SceneComponents<OpeningCutsceneController>(scene).Any())
            {
                File.WriteAllText(Report, "Purge opening already authored. Adjust the anchors and rebuild the Timeline.");
                return;
            }

            var mission = SceneComponents<AlienHuntMission>(scene).Single();
            var spawn = SceneComponents<NetworkPlayerSpawnPoint>(scene).Single(p => p.name == "PlayerStart_EMPTY").transform;
            var bossSpawn = SceneComponents<Transform>(scene).Single(t => t.name == "MonsterSpawn_Stalker_EMPTY");
            var alien = mission.Actors.First(a => a.Kind == HuntKind.Alien);
            var furnace = mission.Furnaces.OrderBy(f => (f.transform.position - alien.transform.position).sqrMagnitude).First();
            var fuel = mission.Actors.Where(a => a.Kind == HuntKind.Jerrycan).OrderBy(a => Distance(a, furnace)).First();
            var bait = mission.Actors.Where(a => a.Kind == HuntKind.Meat).OrderBy(a => Distance(a, furnace)).First();
            var water = SceneComponents<Transform>(scene).Single(t => t.name == "WaterPlane 2" && t.gameObject.activeInHierarchy);

            Physics.SyncTransforms();
            var alienFocus = BoundsOf(alien.transform).center;
            var furnaceFocus = BoundsOf(furnace.transform).center;
            var spawnFocus = spawn.position + spawn.forward * 8f + Vector3.up * 1.7f;
            var outlook = spawn.position + spawn.forward * 48f;
            var overlook = spawn.position - spawn.forward * 17f + spawn.right * 20f + Vector3.up * 30f;
            var alienCamera = FindCamera(alien.transform, alienFocus, alien.transform.forward + alien.transform.right, 2.8f, 0.5f);
            var furnaceCamera = FindCamera(furnace.transform, furnaceFocus, furnace.transform.forward, 6.3f, 1.2f);
            var threatFocus = bossSpawn.position + Vector3.up * 1.25f - bossSpawn.forward * 2.1f;
            var threatCamera = FindCamera(bossSpawn, threatFocus, -bossSpawn.right, 6.0f, 0.65f);
            var spawnCamera = FindCamera(spawn, spawnFocus, -spawn.forward, 10.0f, 1.3f);

            var shots = new[]
            {
                Shot("Shot01_ContaminatedZone", water, overlook, outlook, 6f, Vector3.up * -2f,
                    "Chỉ huy ECHO gọi đội Thanh Trừng. Đã tới khu vực ngập nước.",
                    "ECHO command to Purge team. You have reached the flooded zone.", "horror_ambience/tension_drone_loop"),
                Shot("Shot02_HostileDetection", alien.transform, alienCamera, alienFocus, 5f, Vector3.zero,
                    "Phát hiện Alien non. Dùng thịt làm mồi nhử để tiếp cận và thu giữ.",
                    "Juvenile Alien detected. Use meat as bait to approach and capture it.", "purge_alien/idle_01"),
                Shot("Shot03_PurgeProtocol", furnace.transform, furnaceCamera, furnaceFocus, 7f, Vector3.zero,
                    $"Nạp nhiên liệu vào lò, rồi đưa Alien đã bắt tới. Mục tiêu: thiêu hủy {AlienHuntMission.AlienCount} Alien.",
                    $"Fuel the furnace before loading a captured Alien. Objective: incinerate {AlienHuntMission.AlienCount} Aliens.", "ui/objective_update"),
                Shot("Shot04_UnknownThreat", bossSpawn, threatCamera, threatFocus, 5f, Vector3.zero,
                    "Có chuyển động lớn trong vùng ngập. Giữ đội hình và quan sát.",
                    "Something large is moving through the flooded zone. Stay together and watch your surroundings.", "horror_ambience/distant_monster_sound"),
                Shot("Shot05_MissionStart", spawn, spawnCamera, spawnFocus, 5f, Vector3.zero,
                    "Đội Thanh Trừng, triển khai.", "Purge team, move in.", "ui/match_start")
            };

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            string definitionPath = Folder + "/PurgeOpeningDefinition.asset";
            var definition = AssetDatabase.LoadAssetAtPath<OpeningCutsceneDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<OpeningCutsceneDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            var root = new GameObject("OpeningCutscene_Purge");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                definition.sceneName = AlienHuntMission.SceneName;
                definition.hidePlayers = true;
                definition.fieldOfView = 52f;
                definition.shots = shots;
                var director = root.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.extrapolationMode = DirectorWrapMode.Hold;
                var cameraObject = Child(root.transform, "CinematicCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 700f;
                camera.useOcclusionCulling = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                cameraObject.AddComponent<Animator>();
                var listener = cameraObject.AddComponent<AudioListener>();
                listener.enabled = false;
                var fill = Child(cameraObject.transform, "CinematicFill").AddComponent<Light>();
                fill.type = LightType.Spot;
                fill.range = 20f;
                fill.spotAngle = 63f;
                fill.intensity = 2.3f;
                fill.color = new Color(0.84f, 0.91f, 1f);
                fill.enabled = false;

                var sources = new[] { water, alien.transform, furnace.transform, bossSpawn, spawn };
                var anchors = Child(root.transform, "ShotAnchors");
                for (int i = 0; i < shots.Length; i++) AddAnchor(anchors.transform, shots[i], sources[i], i);

                var props = Child(root.transform, "PurgeProtocol_VisualProps");
                var nearSide = Vector3.ProjectOnPlane(furnaceCamera - furnaceFocus, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, -nearSide);
                CopyProp(fuel.transform, props.transform, furnaceFocus + nearSide * 2.25f + right * 0.95f);
                CopyProp(bait.transform, props.transform, furnaceFocus + nearSide * 2.25f - right * 0.95f);
                props.SetActive(false);

                var audio = Child(root.transform, "CinematicAudio").AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;
                var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
                if (boss != null && boss.TryGetComponent<PurgeBossFeedback>(out var feedback))
                    shots[3].audioOverride = (AudioClip)new SerializedObject(feedback).FindProperty("recoveryGrowl").objectReferenceValue;
                var silhouette = StageBossVisual(boss, root.transform, Vector3.Lerp(threatCamera, threatFocus, 0.58f), bossSpawn.rotation);
                var threatBounds = silhouette.GetComponent<MeshRenderer>().bounds;
                shots[3].startEuler = Quaternion.LookRotation(threatBounds.center - shots[3].startPosition).eulerAngles;
                shots[3].endEuler = Quaternion.LookRotation(threatBounds.center - shots[3].endPosition).eulerAngles;
                shots[3].fillMultiplier = 0.38f;
                var threatAnchor = anchors.GetComponentsInChildren<OpeningCutsceneShotAnchor>().Single(a => a.shotIndex == 3);
                threatAnchor.transform.SetPositionAndRotation(shots[3].startPosition, Quaternion.Euler(shots[3].startEuler));
                threatAnchor.end.SetPositionAndRotation(shots[3].endPosition, Quaternion.Euler(shots[3].endEuler));

                var presentation = root.AddComponent<OpeningCutscenePresentation>();
                presentation.Build(root.transform);
                var controller = root.AddComponent<OpeningCutsceneController>();
                controller.Configure(definition, director, camera, listener, audio, presentation);
                controller.SetFillLight(fill);
                var controllerSettings = new SerializedObject(controller);
                var visuals = controllerSettings.FindProperty("_shotVisuals");
                visuals.arraySize = shots.Length;
                visuals.GetArrayElementAtIndex(2).objectReferenceValue = props;
                visuals.GetArrayElementAtIndex(3).objectReferenceValue = silhouette;
                controllerSettings.ApplyModifiedPropertiesWithoutUndo();

                OpeningCutsceneSceneSetup.Bake(controller);
                if (!controller.IsPlayable) throw new InvalidOperationException("Purge opening has invalid bindings.");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Report, $"Scene_B saved with {shots.Length} camera shots ({definition.Duration:F1}s). Player visuals excluded.");
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        });

        private static float Distance(AlienHuntActor actor, AlienHuntFurnace furnace) =>
            (actor.transform.position - furnace.transform.position).sqrMagnitude;

        private static void WithScene(Action<Scene> action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try { action(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static T[] SceneComponents<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void Append(StringBuilder report, Transform item) => report.AppendLine(
            $"{OpeningCutsceneSceneSetup.HierarchyPath(item)} | {item.position:F3} | rotation={item.eulerAngles:F3}");

        private static Bounds BoundsOf(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Missing visual bounds for " + target.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Vector3 FindCamera(Transform subject, Vector3 focus, Vector3 facing, float distance, float height)
        {
            foreach (float yaw in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f })
            {
                var offset = Quaternion.Euler(0, yaw, 0) * Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
                var candidate = focus + offset * distance + Vector3.up * height;
                if (Physics.CheckSphere(candidate, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!ClearView(candidate, focus, subject)) continue;
                return candidate;
            }
            throw new InvalidOperationException("No clear camera view for " + subject.name);
        }

        private static bool ClearView(Vector3 from, Vector3 to, Transform subject) => !Physics.Linecast(from, to,
            out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(subject);

        private static OpeningCutsceneDefinition.Shot Shot(string name, Transform source, Vector3 start, Vector3 focus,
            float duration, Vector3 drift, string subtitle, string english, string audio)
        {
            var look = Quaternion.LookRotation(focus - start);
            var slide = look * Vector3.right * Mathf.Min(1.6f, Vector3.Distance(start, focus) * 0.2f);
            var end = start;
            foreach (var delta in new[] { drift + slide, drift - slide, slide * 0.4f, -slide * 0.4f, look * Vector3.forward * 0.28f })
            {
                var candidate = start + delta;
                if (Physics.CheckSphere(candidate, 0.3f, ~0, QueryTriggerInteraction.Ignore) || !ClearView(start, candidate, source)) continue;
                if (drift == Vector3.zero && !ClearView(candidate, focus, source)) continue;
                end = candidate;
                break;
            }
            return new OpeningCutsceneDefinition.Shot
            {
                name = name, sourceHierarchy = OpeningCutsceneSceneSetup.HierarchyPath(source),
                startPosition = start, endPosition = end, startEuler = look.eulerAngles,
                endEuler = Quaternion.LookRotation(focus - end).eulerAngles, duration = duration,
                subtitle = subtitle, subtitleEnglish = english, audioKey = audio,
                fillMultiplier = name.Contains("Contaminated") ? 0f : name.Contains("Unknown") ? 0.18f : 1f
            };
        }

        private static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void AddAnchor(Transform parent, OpeningCutsceneDefinition.Shot shot, Transform source, int index)
        {
            var start = Child(parent, shot.name).transform;
            start.SetPositionAndRotation(shot.startPosition, Quaternion.Euler(shot.startEuler));
            var end = Child(start, "End").transform;
            end.SetPositionAndRotation(shot.endPosition, Quaternion.Euler(shot.endEuler));
            var anchor = start.gameObject.AddComponent<OpeningCutsceneShotAnchor>();
            anchor.shotIndex = index;
            anchor.source = source;
            anchor.end = end;
        }

        private static void CopyProp(Transform source, Transform parent, Vector3 position)
        {
            var mesh = source.GetComponentInChildren<MeshFilter>();
            var renderer = mesh.GetComponent<MeshRenderer>();
            var prop = Child(parent, "Visual_" + source.name);
            prop.transform.SetPositionAndRotation(position, mesh.transform.rotation);
            prop.transform.localScale = mesh.transform.lossyScale;
            prop.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            var copy = prop.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            if (!Physics.Raycast(position + Vector3.up * 2, Vector3.down, out var floor, 6f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No surface for cinematic prop " + source.name);
            prop.transform.position += Vector3.up * (floor.point.y - copy.bounds.min.y + 0.025f);
        }

        private static GameObject StageBossVisual(GameObject prefab, Transform parent, Vector3 focus, Quaternion rotation)
        {
            var skin = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.sharedMesh != null);
            var mesh = new Mesh { name = "Purge Lizard Cinematic Pose" };
            skin.BakeMesh(mesh);
            string path = Folder + "/PurgeLizardPose.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                saved = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
            }
            var visual = Child(parent, "Lizard_VisualOnly");
            visual.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = skin.sharedMaterials;
            visual.transform.SetPositionAndRotation(focus, rotation * Quaternion.Inverse(prefab.transform.rotation) * skin.transform.rotation);
            visual.transform.localScale = skin.transform.lossyScale;
            if (!Physics.Raycast(focus + Vector3.up, Vector3.down, out var floor, 6f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No floor beneath cinematic Lizard.");
            visual.transform.position += Vector3.up * (floor.point.y - renderer.bounds.min.y);
            visual.SetActive(false);
            return visual;
        }
    }
}
