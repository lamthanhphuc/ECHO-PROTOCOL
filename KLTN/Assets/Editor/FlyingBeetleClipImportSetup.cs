using System;
using System.Linq;
using UnityEditor;

[InitializeOnLoad]
internal static class FlyingBeetleClipImportSetup
{
    static FlyingBeetleClipImportSetup()
    {
        EditorApplication.delayCall += Configure;
    }

    [MenuItem("Tools/Flying Beetle/Configure Clip Loops")]
    private static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        foreach (var name in new[] { "flying", "idle 1", "idle 2", "atack 1", "atack 2", "roar", "gethit", "death" })
        {
            var path = "Assets/Flying Beetle/animation/flyingbeetle@" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing Beetle FBX: " + path);

            var clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
                throw new InvalidOperationException("No imported clips: " + path);

            var loop = name == "flying" || name == "idle 1" || name == "idle 2";
            if (clips.All(clip => clip.loopTime == loop))
                continue;

            foreach (var clip in clips)
                clip.loopTime = loop;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
