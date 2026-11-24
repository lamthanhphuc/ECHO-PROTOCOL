using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.Audio;
using UnityEditor;
using UnityEngine;

public sealed class HorrorAudioPreview : EditorWindow
{
    private static readonly string[] Keys = { "scifi/pressure_release_01", "scifi/pressure_release_02", "scifi/engine_start",
        "atmosphere/flooded_water", "flooded/Horn", "flooded/Background", "flooded/Wind",
        "flooded/DeepRattle", "flooded/LeafRustle", "flooded/Taps", "flooded/WindHowl", "objectives/charge_engage",
        "energy_core/ambient_hum_loop", "first_aid/revive_complete" };

    [MenuItem("ECHO/Audio/Atmosphere Preview")]
    public static void Open() => GetWindow<HorrorAudioPreview>("Atmosphere audio");

    // Idempotent editor setup: reuse the map's existing water recording, no package/scene changes.
    [MenuItem("ECHO/Audio/Ensure Atmosphere Catalog")]
    public static void SetupCatalog() => Debug.Log(EnsureCatalog());
    public static string EnsureCatalog()
    {
        var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
        var water = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Flooded_Grounds/Content/Sounds/Water.mp3");
        if (catalog == null || water == null) throw new System.InvalidOperationException("Audio catalog or Flooded water recording missing.");
        var entries = new List<GameAudioCatalog.Entry>(catalog.clips);
        if (!entries.Exists(entry => entry.key == "atmosphere/flooded_water"))
        {
            Undo.RecordObject(catalog, "Add Flooded atmosphere cue");
            entries.Add(new GameAudioCatalog.Entry { key = "atmosphere/flooded_water", clip = water });
            catalog.clips = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        }
        return "Atmosphere catalog ready";
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("World ambience and device cues", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Preview source clips here. In-game cues have lower gain, distance attenuation and cooldowns. No AI noise or gameplay event is emitted.", MessageType.Info);
        var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
        foreach (var key in Keys)
        {
            var clip = catalog != null ? catalog.Find(key) : null;
            using (new EditorGUI.DisabledScope(clip == null))
                if (GUILayout.Button(key)) Preview(clip);
        }
        if (GUILayout.Button("Stop preview")) StopPreview();
    }

    private static void Preview(AudioClip clip)
    {
        StopPreview();
        var type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        type?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
            null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)?.Invoke(null, new object[] { clip, 0, false });
    }
    private static void StopPreview() => typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil")
        ?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
    private void OnDisable() => StopPreview();
}
