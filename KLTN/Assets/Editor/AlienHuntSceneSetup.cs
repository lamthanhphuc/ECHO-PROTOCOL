#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.Networking;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class AlienHuntSceneSetup
{
    private const string AssetsRoot = "Assets/AlienHunt";
    private const string Monster = "Assets/Stylized3DMonster/Monster01/Prefab/Monster01_01.prefab";
    private const string Imported = "Assets/import/Gamemode 2/";

    [MenuItem("Tools/ECHO Protocol/Prepare Scene_B Alien Hunt")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Scene_B.unity")
            throw new InvalidOperationException("Open Scene_B in Edit mode first.");
        // Persist authored furnace placements before generating gameplay children.
        EditorSceneManager.SaveScene(scene);
        Folder(AssetsRoot); Folder(AssetsRoot + "/Animations"); Folder(AssetsRoot + "/Materials"); Folder(AssetsRoot + "/Prefabs");
        var controller = BuildController();
        var monsterMaterial = Lit("Monster01_OneColor", AssetDatabase.LoadAssetAtPath<GameObject>(Monster)
            .GetComponentInChildren<Renderer>().sharedMaterial.mainTexture as Texture2D);
        var fuelMaterial = Lit("Jerrycan", Texture(Imported + "jerrycan/textures/DefaultMaterial_albedo.jpg"));
        var meatMaterial = Lit("Meat", Texture(Imported + "meat/textures/Iceage_Meat_D.png"));
        var eggMaterial = Lit("AlienEgg", Texture(Imported + "Allien_egg/textures/DefaultMaterial_albedo.jpeg"));

        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var candidates = all.Single(t => t.name == "EnergyCoreSpawnCandidates").Cast<Transform>()
            .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
        if (candidates.Length != 18) throw new InvalidOperationException("Expected the original 18 fuel spawn candidates.");
        var furnaceRoots = all.Where(t => t.name.StartsWith("Furnace_Export", StringComparison.Ordinal)).ToArray();
        if (furnaceRoots.Length != 3) throw new InvalidOperationException("Expected three authored Furnace_Export models.");
        var previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "AlienHunt_Gameplay");
        var savedNests = previous ? previous.GetComponentsInChildren<Transform>()
            .Where(t => t.name.StartsWith("AlienNest_", StringComparison.Ordinal)).OrderBy(t => t.name)
            .Select(t => t.position).ToArray() : Array.Empty<Vector3>();
        if (previous) Object.DestroyImmediate(previous);
        var nests = savedNests.Length == 9 ? savedNests : FindNests(candidates, furnaceRoots, all);
        var meatPositions = FindMeatPositions(nests);
        var root = new GameObject("AlienHunt_Gameplay");
        var mission = root.AddComponent<AlienHuntMission>();
        var entities = new AlienHuntActor[AlienHuntMission.EntityCount];
        var nestRoot = Child("AlienNests", root.transform);
        var fuelRoot = Child("JerrycanSpawns", root.transform);
        var meatRoot = Child("MeatSpawns", root.transform);
        var monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Monster);
        for (int i = 0; i < AlienHuntMission.AlienCount; i++)
        {
            var nest = Child("AlienNest_" + (i + 1).ToString("00"), nestRoot); nest.position = nests[i];
            var egg = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                Imported + "Allien_egg/source/model/model.dae"), scene);
            egg.name = "AlienEgg_Decoration"; egg.transform.SetParent(nest, false); egg.transform.localScale = Vector3.one * 2.2f;
            Materials(egg, eggMaterial); GroundVisual(egg.transform, nests[i]);
            var agentPosition = Nav(nests[i] + Vector3.right * 1.8f, 3f);
            var actor = Child("AlienYoung_" + (i + 1).ToString("00"), nest);
            actor.position = agentPosition;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(monsterPrefab, scene);
            visual.transform.SetParent(actor, false); visual.transform.localScale = Vector3.one * .65f;
            Materials(visual, monsterMaterial); GroundVisual(visual.transform, actor.position);
            var rig = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Monster01_AllAnim");
            var animator = rig.gameObject.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var capsule = actor.gameObject.AddComponent<CapsuleCollider>(); capsule.radius = .38f;
            capsule.height = 1.25f; capsule.center = Vector3.up * .625f;
            var agent = actor.gameObject.AddComponent<NavMeshAgent>(); agent.radius = .35f; agent.height = 1.3f;
            agent.speed = 1f; agent.angularSpeed = 200; agent.acceleration = 10; agent.enabled = false;
            entities[i] = actor.gameObject.AddComponent<AlienHuntActor>(); entities[i].Configure(i, HuntKind.Alien, animator);
        }
        PrefabUtility.SaveAsPrefabAsset(entities[0].gameObject, AssetsRoot + "/Prefabs/PF_AlienYoung.prefab");
        for (int i = 0; i < candidates.Length; i++)
        {
            // The original markers stay untouched; snap their pickup to a nearby reachable floor.
            var position = Nav(candidates[i].position, 3f);
            entities[9 + i] = Item("Jerrycan_" + (i + 1).ToString("00"), 9 + i, HuntKind.Jerrycan,
                Imported + "jerrycan/source/jerrycan.fbx", position, fuelRoot, fuelMaterial,
                AlienHuntActor.JerrycanVisualScale);
        }
        for (int i = 0; i < AlienHuntMission.MeatCount; i++)
        {
            var position = meatPositions[i];
            entities[27 + i] = Item("Meat_" + (i + 1).ToString("00"), 27 + i, HuntKind.Meat,
                Imported + "meat/source/Meat.fbx", position, meatRoot, meatMaterial, 720f);
        }

        var stations = new AlienHuntFurnace[3];
        for (int i = 0; i < furnaceRoots.Length; i++)
        {
            var furnace = furnaceRoots[i];
            foreach (var filter in furnace.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var collider = filter.GetComponent<MeshCollider>();
                if (!collider) collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            foreach (var renderer in furnace.GetComponentsInChildren<Renderer>())
            {
                string texture = renderer.name.Contains("coal") ? "Coal" :
                    renderer.name.Contains("chain") || renderer.name.Contains("lever") ? "Metal" : "Furnace";
                renderer.sharedMaterial = Lit(texture, Texture(Imported + "Furnace/textures/" + texture + "_BaseColor.png"));
            }
            var panRenderer = furnace.GetComponentsInChildren<Renderer>().Single(r => r.name == "LP_Cotl");
            var obstacle = furnace.GetComponent<NavMeshObstacle>();
            if (!obstacle) obstacle = furnace.gameObject.AddComponent<NavMeshObstacle>();
            var bodyBounds = furnace.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "LP_body").sharedMesh.bounds;
            var bodyTransform = furnace.GetComponentsInChildren<Transform>().Single(t => t.name == "LP_body");
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = furnace.InverseTransformPoint(bodyTransform.TransformPoint(bodyBounds.center));
            obstacle.size = Vector3.Scale(bodyBounds.size, bodyTransform.localScale);
            // Local avoidance keeps the authored narrow paths connected on the existing bake.
            obstacle.carving = false;
            var pan = furnace.Find("AlienPanAnchor"); if (!pan) pan = Child("AlienPanAnchor", furnace);
            pan.rotation = Quaternion.Euler(0, furnace.eulerAngles.y, 90f);
            pan.position = panRenderer.bounds.center + Vector3.up * .25f - pan.rotation * new Vector3(0, .6f, .1f);
            stations[i] = furnace.GetComponent<AlienHuntFurnace>();
            if (!stations[i]) stations[i] = furnace.gameObject.AddComponent<AlienHuntFurnace>();
            stations[i].Configure(i, pan);
            stations[i].ConfigureEffects(FlameMaterial());
            EditorUtility.SetDirty(stations[i]);
        }
        mission.Configure(entities, stations); EditorUtility.SetDirty(mission);
        if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scene.path, true) }).ToArray();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[AlienHunt] Prepared 9 single-color aliens, 9 egg nests, 18 jerrycans, 17 meat pickups and 3 furnaces.");
    }

    public static void UpdateFurnaceEffects()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Scene_B.unity")
            throw new InvalidOperationException("Open Scene_B outside Play mode.");
        foreach (var furnace in Object.FindObjectsByType<AlienHuntFurnace>(FindObjectsSortMode.None))
        { furnace.ConfigureEffects(FlameMaterial()); EditorUtility.SetDirty(furnace); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    private static Material FlameMaterial()
    {
        const string path = AssetsRoot + "/Materials/AlienHuntFlame.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        { material = new Material(Shader.Find("EchoProtocol/AlienHuntFlame")); AssetDatabase.CreateAsset(material,path); }
        return material;
    }

    private static AlienHuntActor Item(string name, int index, HuntKind kind, string model, Vector3 position,
        Transform parent, Material material, float scale)
    {
        var root = Child(name, parent); root.position = position;
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model), root.gameObject.scene);
        visual.transform.SetParent(root, false); visual.transform.localScale = Vector3.one * scale;
        Materials(visual, material); GroundVisual(visual.transform, position);
        var bounds = new Bounds(position, Vector3.zero);
        foreach (var r in visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var collider = root.gameObject.AddComponent<BoxCollider>(); collider.center = root.InverseTransformPoint(bounds.center);
        collider.size = Vector3.Max(bounds.size, Vector3.one * .15f);
        var actor = root.gameObject.AddComponent<AlienHuntActor>(); actor.Configure(index, kind, null); return actor;
    }

    private static Vector3[] FindNests(Transform[] candidates, Transform[] furnaces, Transform[] all)
    {
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0) throw new InvalidOperationException("Scene_B requires its baked NavMesh.");
        var spawn = Nav(all.Single(t => t && t.name == "MonsterSpawn_Stalker_EMPTY").position, 3f);
        var pool = new List<Vector3>(); var path = new NavMeshPath();
        for (int i = 0; i < tri.indices.Length; i += 9)
        {
            if (i + 2 >= tri.indices.Length) break;
            var p = (tri.vertices[tri.indices[i]] + tri.vertices[tri.indices[i + 1]] + tri.vertices[tri.indices[i + 2]]) / 3;
            if (p.x < 430 || p.x > 660 || p.z < 300 || p.z > 650 || p.y < 16.15f) continue;
            if (furnaces.Any(f => Vector3.Distance(f.position, p) < 12)) continue;
            if (!NavMesh.CalculatePath(spawn, p, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            bool blocked = Physics.OverlapCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.3f, .6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider) && c.excludeLayers.value != -1);
            if (!blocked) pool.Add(p);
        }
        var selected = new List<Vector3>();
        for (int i = 0; i < 9; i++)
        {
            var options = pool.Where(p => selected.All(q => Vector3.Distance(p, q) >= 18f)).ToArray();
            if (options.Length == 0) throw new InvalidOperationException("Not enough separated, reachable nest locations.");
            var anchor = candidates[i * 2].position;
            var chosen = options.OrderBy(p => (p - anchor).sqrMagnitude).First(); selected.Add(chosen);
        }
        return selected.ToArray();
    }

    private static Vector3[] FindMeatPositions(Vector3[] nests)
    {
        var tri = NavMesh.CalculateTriangulation();
        var selected = new List<Vector3>();
        var path = new NavMeshPath();
        for (int i = 0; i + 2 < tri.indices.Length && selected.Count < AlienHuntMission.MeatCount; i += 3)
        {
            var p = (tri.vertices[tri.indices[i]] + tri.vertices[tri.indices[i + 1]] + tri.vertices[tri.indices[i + 2]]) / 3f;
            if (p.x < 430 || p.x > 660 || p.z < 300 || p.z > 650 || p.y < 16.15f) continue;
            if (nests.Any(n => Vector2.Distance(new Vector2(n.x,n.z),new Vector2(p.x,p.z)) < 26f)
                || selected.Any(n => Vector3.Distance(n,p) < 10f)) continue;
            if (!NavMesh.CalculatePath(nests[0], p, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            if (Physics.CheckCapsule(p + Vector3.up * .3f, p + Vector3.up * .7f, .2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (!NavMesh.SamplePosition(p, out var snapped, 2f, NavMesh.AllAreas)) continue;
            if (nests.Any(n => Vector2.Distance(new Vector2(n.x,n.z),new Vector2(snapped.position.x,snapped.position.z)) < 26f)) continue;
            selected.Add(snapped.position);
        }
        if (selected.Count != AlienHuntMission.MeatCount)
            throw new InvalidOperationException($"Could not find {AlienHuntMission.MeatCount} safe bait positions away from nests.");
        return selected.ToArray();
    }

    private static AnimatorController BuildController()
    {
        string path = AssetsRoot + "/Animations/AC_AlienYoung.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
            ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var state in controller.layers[0].stateMachine.states) controller.layers[0].stateMachine.RemoveState(state.state);
        foreach (var name in new[] { "Idle", "Walk", "Run", "Stunned" })
        {
            string source = "Assets/Stylized3DMonster/Monster01/Anim/InPlace_Anim/Monster01_" + name
                + (name == "Walk" || name == "Run" ? "_InPlace" : "") + ".anim";
            var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(source);
            if (!original) throw new InvalidOperationException("Missing clip " + source);
            string clipPath = AssetsRoot + "/Animations/AlienYoung_" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip) { clip = Object.Instantiate(original); AssetDatabase.CreateAsset(clip, clipPath); }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var state = controller.layers[0].stateMachine.AddState(name); state.motion = clip;
            if (name == "Idle") controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(clip);
        }
        var eating = BuildEating();
        controller.layers[0].stateMachine.AddState("Eating").motion = eating;
        EditorUtility.SetDirty(controller); return controller;
    }

    private static AnimationClip BuildEating()
    {
        const string output = AssetsRoot + "/Animations/AlienYoung_Eating.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(output); if (existing) return existing;
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Creep Horror Creature/Meshes/Creep_mesh.fbx"));
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Monster));
        source.hideFlags = target.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animation = AssetDatabase.LoadAllAssetsAtPath("Assets/Creep Horror Creature/Meshes/Creep_mesh.fbx")
                .OfType<AnimationClip>().Single(c => c.name == "Creep|Eating_Action");
            var rig = target.GetComponentsInChildren<Transform>().Single(t => t.name == "Monster01_AllAnim");
            var sourceBones = source.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            var targetBones = target.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            var names = new Dictionary<string, string> {
                {"Torso","spine_01.x"},{"Chest","spine_02.x"},{"Neck","neck.x"},{"Head","head.x"},
                {"Shoulder.L","shoulder.l"},{"Arm1.L","arm_stretch.l"},{"Arm2.L","forearm_stretch.l"},{"Hand.L","hand.l"},
                {"Shoulder.R","shoulder.r"},{"Arm1.R","arm_stretch.r"},{"Arm2.R","forearm_stretch.r"},{"Hand.R","hand.r"},
                {"Leg1.L","thigh_stretch.l"},{"Leg2.L","leg_stretch.l"},{"Foot.L","foot.l"},
                {"Leg1.R","thigh_stretch.r"},{"Leg2.R","leg_stretch.r"},{"Foot.R","foot.r"}
            };
            var keys = names.Keys.ToArray();
            var restSource = keys.Select(k => sourceBones[k].rotation).ToArray();
            var restTarget = keys.Select(k => targetBones[names[k]].rotation).ToArray();
            var baseRotations = target.GetComponentsInChildren<Transform>().ToDictionary(t => t, t => t.localRotation);
            var curves = keys.Select(k => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() }).ToArray();
            int frames = Mathf.CeilToInt(animation.length * 30);
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = Mathf.Min(animation.length, frame / 30f);
                animation.SampleAnimation(source, time);
                foreach (var pair in baseRotations) pair.Key.localRotation = pair.Value;
                for (int i = 0; i < keys.Length; i++)
                {
                    var bone = targetBones[names[keys[i]]];
                    bone.rotation = sourceBones[keys[i]].rotation * Quaternion.Inverse(restSource[i]) * restTarget[i];
                    var q = bone.localRotation;
                    curves[i][0].AddKey(time,q.x); curves[i][1].AddKey(time,q.y);
                    curves[i][2].AddKey(time,q.z); curves[i][3].AddKey(time,q.w);
                }
            }
            var result = new AnimationClip { name = "AlienYoung_Eating", frameRate = 30 };
            string[] components = { "x", "y", "z", "w" };
            for (int i = 0; i < keys.Length; i++) for (int axis = 0; axis < 4; axis++)
                AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve(
                    AnimationUtility.CalculateTransformPath(targetBones[names[keys[i]]], rig), typeof(Transform), "m_LocalRotation." + components[axis]), curves[i][axis]);
            result.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(result); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(result, settings); AssetDatabase.CreateAsset(result, output); return result;
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(target); }
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    private static Texture2D Texture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path)
        ?? throw new InvalidOperationException("Missing texture " + path);
    private static Material Lit(string name, Texture2D texture)
    {
        string path = AssetsRoot + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", .3f); EditorUtility.SetDirty(material); return material;
    }
    private static void Materials(GameObject go, Material material)
    { foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(m => material).ToArray(); }
    private static Transform Child(string name, Transform parent)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    private static void GroundVisual(Transform visual, Vector3 position)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0) visual.position += Vector3.up * (position.y - renderers.Min(r => r.bounds.min.y));
    }
    private static Vector3 Nav(Vector3 position, float radius)
    {
        if (NavMesh.SamplePosition(position, out var hit, radius, NavMesh.AllAreas)) return hit.position;
        // Some authored room markers are above/below their floor.
        if (Physics.Raycast(position + Vector3.up * 8, Vector3.down, out var floor, 20,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && NavMesh.SamplePosition(floor.point, out hit, radius, NavMesh.AllAreas)) return hit.position;
        throw new InvalidOperationException("No safe NavMesh location near " + position);
    }
}
#endif
