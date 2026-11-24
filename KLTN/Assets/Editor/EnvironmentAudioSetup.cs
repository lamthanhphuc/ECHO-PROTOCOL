using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnvironmentAudioSetup
{
    public static string Status { get; private set; } = "Idle";
    public static string StartBuild()
    {
        Status = "Running";
        EditorApplication.update -= BuildOnce;
        EditorApplication.update += BuildOnce;
        return "Queued";
    }
    private static void BuildOnce()
    {
        EditorApplication.update -= BuildOnce;
        try { Status = Build(); } catch (Exception e) { Status = "FAILED: " + e; }
    }
    private static readonly string[] Scenes = { "Assets/Scenes/SciFi.unity", "Assets/Scenes/Scene_A.unity", "Assets/Scenes/Scene_B.unity" };
    public static string StartFloodedMerge()
    {
        Status = "Running"; EditorApplication.update -= MergeFloodedOnce; EditorApplication.update += MergeFloodedOnce;
        return "Queued";
    }
    private static void MergeFloodedOnce()
    {
        EditorApplication.update -= MergeFloodedOnce;
        try { Status = MergeFlooded(); } catch (Exception e) { Status = "FAILED: " + e; }
    }
    private static string MergeFlooded()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
        string[] names = { "Background", "Wind", "Horn", "DeepRattle", "LeafRustle", "Taps", "WindHowl" };
        var entries = new List<GameAudioCatalog.Entry>(catalog.clips);
        foreach (var name in names)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Flooded_Grounds/Content/Sounds/" + name + ".mp3");
            if (clip == null) throw new InvalidOperationException(name + " missing");
            if (!entries.Any(e => e.key == "flooded/" + name))
                entries.Add(new GameAudioCatalog.Entry { key = "flooded/" + name, clip = clip });
        }
        catalog.clips = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        var report = new List<string>();
        foreach (var path in Scenes.Skip(1))
        {
            var scene = SceneManager.GetSceneByPath(path); bool open = scene.IsValid() && scene.isLoaded;
            if (open && scene.isDirty) throw new InvalidOperationException("Unsaved: " + path);
            if (!open) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(r => r.name == "Flooded Audio Library")) { report.Add(path + " already merged"); continue; }
                var root = new GameObject("Flooded Audio Library"); SceneManager.MoveGameObjectToScene(root, scene);
                var positions = names.ToDictionary(n => n, n => new List<Vector3>());
                int muted = 0, activeBefore = 0, anchors = 0;
                foreach (var source in roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)))
                {
                    if (source.clip == null) continue;
                    string name = source.clip.name;
                    bool water = name == "Water";
                    if (!water && !names.Contains(name)) continue;
                    if (source.isActiveAndEnabled && source.playOnAwake) activeBefore++;
                    source.enabled = false; source.playOnAwake = false; muted++;
                    if (water || name == "Background" || name == "Wind" || name == "Horn") continue;
                    var placed = positions[name];
                    if (placed.Count >= 6 || placed.Any(p => (p - source.transform.position).sqrMagnitude < 225)) continue;
                    float gain = name == "DeepRattle" ? .16f : name == "LeafRustle" ? .20f : .18f;
                    Add(root.transform, source.transform.position, "flooded/" + name, gain, name == "WindHowl" ? 28 : 22, Mathf.Min(source.clip.length, 6));
                    placed.Add(source.transform.position); anchors++;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.Add(path + ": " + muted + " original sources retired (" + activeBefore + " active autoplay), " + anchors + " extra physical anchors; Horn shared 90s / 180–300s");
            }
            finally { if (!open) EditorSceneManager.CloseScene(scene, true); }
        }
        return string.Join("\n", report);
    }
    public static string Inspect()
    {
        var report = new List<string>();
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));
                report.Add(path + "\n" + string.Join("\n", transforms.Where(t => t.GetComponent<AudioSource>() != null
                    || t.name.StartsWith("3D_Water") || t.name.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(35).Select(t => t.name + " | " + t.position + " | " + ClipName(t))));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        return string.Join("\n", report);
    }
    private static string ClipName(Transform t)
    { var source = t.GetComponent<AudioSource>(); return source != null && source.clip != null ? source.clip.name : ""; }

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var report = new List<string>();
        AddCatalog();
        foreach (var path in Scenes)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (alreadyOpen && scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var existing = roots.FirstOrDefault(r => r.name == "Environment Audio Anchors");
                if (existing != null) { report.Add(path + " already authored"); continue; }
                var root = new GameObject("Environment Audio Anchors"); SceneManager.MoveGameObjectToScene(root, scene);
                int count = 0;
                var positions = new List<Vector3>();
                if (path.Contains("SciFi"))
                {
                    foreach (var random in roots.SelectMany(r => r.GetComponentsInChildren<PlayRandomSound>(true)))
                    { random.enabled = false; if (random.randomSound != null) { random.randomSound.enabled = false; random.randomSound.playOnAwake = false; } }
                    foreach (var source in roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)))
                    {
                        if (source.clip == null) continue;
                        string clip = source.clip.name;
                        // Disable demo random playback and music. Keep existing constant fans/rumble intact.
                        var random = source.GetComponent<PlayRandomSound>();
                        if (random != null) random.enabled = false;
                        if (clip == "synthtrack" || clip == "upbeatfuture") { source.playOnAwake = false; source.enabled = false; continue; }
                        string cue = clip.StartsWith("pressure_release") ? "scifi/" + clip : clip == "enginestart" ? "scifi/engine_start" : null;
                        if (cue == null) continue;
                        if (positions.Any(p => (p - source.transform.position).sqrMagnitude < 25)) continue;
                        Add(root.transform, source.transform.position, cue, .18f, 26, Mathf.Min(5f, source.clip.length));
                        positions.Add(source.transform.position); count++;
                        source.playOnAwake = false; source.enabled = false;
                    }
                    // Pipes are physical mesh locations, not listener-relative random positions.
                    foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                    {
                        if (count >= 12) break;
                        if (t.name.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) < 0 || t.GetComponent<Renderer>() == null
                            || positions.Any(p => (p - t.position).sqrMagnitude < 100)) continue;
                        Add(root.transform, t.GetComponent<Renderer>().bounds.center, "scifi/pressure_release_01", .14f, 24, 4);
                        positions.Add(t.position); count++;
                    }
                }
                else
                {
                    foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                    {
                        if (count >= 10) break;
                        if (!t.name.StartsWith("3D_Water")) continue;
                        var renderer = t.GetComponentInChildren<Renderer>(true);
                        var waterSource = t.GetComponent<AudioSource>();
                        if (renderer == null && (waterSource == null || waterSource.clip == null || waterSource.clip.name != "Water")) continue;
                        // Prefer the map author's existing positioned water emitter.
                        var point = waterSource != null ? t.position : renderer.bounds.center;
                        if (positions.Any(p => (p - point).sqrMagnitude < 225)) continue;
                        Add(root.transform, point, "atmosphere/flooded_water", .14f, 30, 5);
                        if (waterSource != null) { waterSource.enabled = false; waterSource.playOnAwake = false; }
                        positions.Add(point); count++;
                    }
                }
                if (count == 0) throw new InvalidOperationException("No justified audio locations found: " + path);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.Add(path + ": " + count + " physical anchors");
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        return string.Join("\n", report);
    }

    private static void Add(Transform parent, Vector3 position, string cue, float gain, float range, float duration)
    {
        var go = new GameObject(cue); go.transform.SetParent(parent); go.transform.position = position;
        var anchor = go.AddComponent<EnvironmentAudioAnchor>(); anchor.cue = cue; anchor.gain = gain; anchor.range = range; anchor.duration = duration;
    }
    private static void AddCatalog()
    {
        var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
        var entries = new List<GameAudioCatalog.Entry>(catalog.clips);
        foreach (var file in new[] { "pressure_release_01", "pressure_release_02", "enginestart" })
        {
            string key = file == "enginestart" ? "scifi/engine_start" : "scifi/" + file;
            if (entries.Any(e => e.key == key)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SciFi Warehouse Kit/Demo/Audio/SFX/" + file + ".wav");
            if (clip == null) throw new InvalidOperationException(file + " missing");
            entries.Add(new GameAudioCatalog.Entry { key = key, clip = clip });
        }
        catalog.clips = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
    }
}
