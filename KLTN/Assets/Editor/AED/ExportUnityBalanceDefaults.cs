using System;
using System.IO;
using System.Linq;
using EchoProtocol.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ExportUnityBalanceDefaults : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result != BuildResult.Succeeded ||
            !EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/SciFi.unity"))
            return;

        var manifest = new DefaultsManifest
        {
            gameVersion = PlayerSettings.bundleVersion,
            profiles = Enum.GetValues(typeof(MatchDifficulty)).Cast<MatchDifficulty>().Select(BuildProfile).ToArray()
        };
        File.WriteAllText(report.summary.outputPath.TrimEnd('/', '\\') + ".balance-defaults.json",
            JsonUtility.ToJson(manifest, true));
    }

    private static DefaultsProfile BuildProfile(MatchDifficulty difficulty)
    {
        var profile = MatchDifficultyProfiles.Get(difficulty);
        return new DefaultsProfile
        {
            difficulty = difficulty.ToString().ToUpperInvariant(),
            patrolSpeed = profile.PatrolSpeed,
            chaseSpeed = profile.ChaseSpeed,
            detectionSeconds = profile.DetectionDurationSeconds,
            searchSeconds = profile.SearchDurationSeconds,
            hearingMultiplier = profile.HearingRangeMultiplier,
            specialCooldownSeconds = profile.SpecialEncounterCooldownSeconds,
            zone1Minions = profile.Zone1MinionCap,
            zone2Minions = profile.Zone2MinionCap,
            zone3Minions = profile.Zone3MinionCap,
            toolsPerZone = profile.TeamToolsPerZone,
            revivesPerZone = profile.MaximumRevivesPerZone
        };
    }

    [Serializable] private sealed class DefaultsManifest
    {
        public string schema = "UNITY_BALANCE_DEFAULTS_V1";
        public string gameVersion;
        public DefaultsProfile[] profiles;
    }

    [Serializable] private sealed class DefaultsProfile
    {
        public string mapId = "SciFi";
        public string difficulty;
        public float patrolSpeed, chaseSpeed, detectionSeconds, searchSeconds, hearingMultiplier, specialCooldownSeconds;
        public int zone1Minions, zone2Minions, zone3Minions, toolsPerZone, revivesPerZone;
    }
}
