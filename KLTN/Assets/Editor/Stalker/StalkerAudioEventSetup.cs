using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: Tools > Stalker > Add Audio Animation Events
///
/// Injects AnimationEvent entries for StalkerAudioController into:
///   1. FBX-embedded clips in Creep_mesh.fbx  (via ModelImporter clip settings)
///   2. Baked .anim clips in Generated_Quadruped_V37_BipedPosture  (via AnimationUtility)
///
/// FBX clip frame rates are assumed at 30 fps (as exported).
/// Event times are expressed in seconds: frame / 30f.
///
/// Re-running is safe: existing audio events are replaced, not duplicated.
/// Run AFTER "Build Production Animator Presentation" so generated .anim files exist.
/// </summary>
public static class StalkerAudioEventSetup
{
    private static readonly string[] FbxPaths =
    {
        "Assets/Creep Horror Creature/Meshes/Creep_mesh.fbx",
        "Assets/Creep Horror Creature/Meshes/Creep_mesh_lod1.fbx",
    };

    private const string GeneratedFolder =
        "Assets/Animations/Stalker/Generated_Quadruped_V37_BipedPosture";

    private const float FbxFps = 30f;

    // Audio method names – must match StalkerAudioController public API exactly
    private const string MethodFootstep = "PlayFootstep";
    private const string MethodSniff    = "PlaySniff";
    private const string MethodBite     = "PlayBite";
    private const string MethodPunch    = "PlayPunch";
    private const string MethodDetect   = "PlayDetect";
    private const string MethodJumpOut  = "PlayJumpOut";
    private const string MethodJumpIn   = "PlayJumpIn";

    // All audio method names – used to strip old entries before reinjecting
    private static readonly HashSet<string> AudioMethods =
        new HashSet<string>(StringComparer.Ordinal)
        {
            MethodFootstep, MethodSniff, MethodBite,
            MethodPunch, MethodDetect, MethodJumpOut, MethodJumpIn,
        };

    // ──────────────────────────────────────────────────────────────────────
    // Menu entry
    // ──────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Stalker/Add Audio Animation Events")]
    public static void AddAudioAnimationEvents()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[StalkerAudio] Cannot modify clips while in Play Mode.");
            return;
        }

        InjectFbxEvents();
        if (System.IO.Directory.Exists(GeneratedFolder))
        {
            InjectGeneratedClipEvents();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[StalkerAudio] ✓ Audio Animation Events injection complete.");
    }

    // ──────────────────────────────────────────────────────────────────────
    // 1. FBX clips (Creep_mesh.fbx – embedded take clips)
    // ──────────────────────────────────────────────────────────────────────
    //
    // Clip take names and their audio event definitions.
    // Frame numbers are at 30 fps; converted to seconds for AnimationEvent.time.
    //
    // Bite_Action     (120 frames) – jaw impact at frame 55 (~46 %)
    // Punch_Action    (120 frames) – fist impact at frame 55 (~46 %)
    // Sniff_Action    (121 frames) – inhale at frame 28 and 88
    // Walk1_Action    (121 frames) – L-foot f10, R-foot f70
    // Walk2_Action    (121 frames) – L-foot f10, R-foot f70   (chase walk)
    // JumpOut_Action  ( 32 frames) – push-off at frame 8
    // JumpIn_Action   ( 32 frames) – impact   at frame 26
    // Roar_Action     (121 frames) – roar peak at frame 36
    //
    private static readonly (string takeName, (float frame, string method)[] events)[]
        FbxClipEvents =
        {
            (
                "Creep|Bite_Action",
                new (float, string)[] { (55f, MethodBite) }
            ),
            (
                "Creep|Punch_Action",
                new (float, string)[] { (55f, MethodPunch) }
            ),
            (
                "Creep|Sniff_Action",
                new (float, string)[] { (28f, MethodSniff), (88f, MethodSniff) }
            ),
            (
                "Creep|Walk1_Action",
                new (float, string)[]
                {
                    (0f, MethodFootstep),
                    (15f, MethodFootstep),
                    (30f, MethodFootstep),
                    (45f, MethodFootstep),
                    (60f, MethodFootstep),
                    (75f, MethodFootstep),
                    (90f, MethodFootstep),
                    (105f, MethodFootstep),
                }
            ),
            (
                "Creep|Walk2_Action",
                new (float, string)[]
                {
                    (0f, MethodFootstep),
                    (15f, MethodFootstep),
                    (30f, MethodFootstep),
                    (45f, MethodFootstep),
                    (60f, MethodFootstep),
                    (75f, MethodFootstep),
                    (90f, MethodFootstep),
                    (105f, MethodFootstep),
                }
            ),
            (
                "Creep|JumpOut_Action",
                new (float, string)[] { (8f, MethodJumpOut) }
            ),
            (
                "Creep|JumpIn_Action",
                new (float, string)[] { (26f, MethodJumpIn) }
            ),
            (
                "Creep|Roar_Action",
                new (float, string)[] { (36f, MethodDetect) }
            ),
        };

    private static void InjectFbxEvents()
    {
        foreach (var path in FbxPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[StalkerAudio] ModelImporter not found at: {path}");
                continue;
            }

            var clipAnimations = importer.clipAnimations;

            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                // Fall back to default clip animations if none are overridden yet
                clipAnimations = importer.defaultClipAnimations;
            }

            var changed = false;

            foreach (var (takeName, eventSpecs) in FbxClipEvents)
            {
                var clip = Array.Find(
                    clipAnimations,
                    c => string.Equals(c.name, takeName, StringComparison.Ordinal)
                      || string.Equals(c.takeName, takeName, StringComparison.Ordinal));

                if (clip == null)
                {
                    Debug.LogWarning(
                        $"[StalkerAudio] FBX clip not found: '{takeName}' in {path} – skipped.");
                    continue;
                }

                // Ensure clean clip settings (no empty avatar mask and free root motion)
                clip.maskType = ClipAnimationMaskType.None;
                clip.keepOriginalPositionY = false;

                // Strip old audio events, keep everything else
                var preserved = (clip.events ?? Array.Empty<AnimationEvent>())
                    .Where(e => !AudioMethods.Contains(e.functionName))
                    .ToList();

                var frameSpan = Mathf.Max(1f, clip.lastFrame - clip.firstFrame);
                var clipLength = frameSpan / FbxFps;

                foreach (var (frame, method) in eventSpecs)
                {
                    // Clamp to clip frame range
                    var localFrame = Mathf.Clamp(frame, clip.firstFrame, clip.lastFrame);
                    // In Unity ModelImporterClipAnimation, AnimationEvent.time must be normalized time (0.0 to 1.0)
                    // If seconds (> 1.0) are passed, Unity's importer stretches m_StopTime = time * duration!
                    var t = (localFrame - clip.firstFrame) / frameSpan;

                    preserved.Add(new AnimationEvent
                    {
                        time            = t,
                        functionName    = method,
                        messageOptions  = SendMessageOptions.DontRequireReceiver,
                    });
                }

                preserved.Sort((a, b) => a.time.CompareTo(b.time));
                clip.events = preserved.ToArray();

                var summary = string.Join(", ",
                    eventSpecs.Select(e => $"{e.method}@frame{e.frame}"));

                Debug.Log(
                    $"[StalkerAudio] ✓ FBX '{takeName}' ({clipLength:0.00}s) – {summary}");

                changed = true;
            }

            if (changed)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
                Debug.Log($"[StalkerAudio] FBX reimported with audio events: {path}");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // 2. Generated .anim clips (Walk + Run footsteps from baked clips)
    // ──────────────────────────────────────────────────────────────────────
    //
    // STK_Walk (1.42 s, looping) – L-foot @7 %, R-foot @55 %
    // STK_Run  (0.78 s, looping) – L-foot @7 %, R-foot @55 %
    //
    private static void InjectGeneratedClipEvents()
    {
        InjectIntoGeneratedClip(
            "STK_Walk",
            new (float normalizedTime, string method)[]
            {
                (0.07f, MethodFootstep),
                (0.55f, MethodFootstep),
            });

        InjectIntoGeneratedClip(
            "STK_Run",
            new (float normalizedTime, string method)[]
            {
                (0.07f, MethodFootstep),
                (0.55f, MethodFootstep),
            });
    }

    private static void InjectIntoGeneratedClip(
        string clipName,
        (float normalizedTime, string method)[] eventSpecs)
    {
        var path = $"{GeneratedFolder}/{clipName}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"[StalkerAudio] Generated clip not found: {path} – skipped.");
            return;
        }

        var preserved = AnimationUtility
            .GetAnimationEvents(clip)
            .Where(e => !AudioMethods.Contains(e.functionName))
            .ToList();

        foreach (var (normalizedTime, method) in eventSpecs)
        {
            var t = Mathf.Clamp01(normalizedTime) * clip.length;
            preserved.Add(new AnimationEvent
            {
                time            = t,
                functionName    = method,
                messageOptions  = SendMessageOptions.DontRequireReceiver,
            });
        }

        preserved.Sort((a, b) => a.time.CompareTo(b.time));

        AnimationUtility.SetAnimationEvents(clip, preserved.ToArray());
        EditorUtility.SetDirty(clip);

        var summary = string.Join(", ",
            eventSpecs.Select(e => $"{e.method}@{e.normalizedTime * 100f:0}%"));

        Debug.Log(
            $"[StalkerAudio] ✓ Generated '{clipName}' ({clip.length:0.00}s) – {summary}");
    }
}
