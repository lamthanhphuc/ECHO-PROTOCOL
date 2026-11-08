using System;
using System.Reflection;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking.Authority;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.AI.AED.Tests
{
    public sealed class AEDPreMatchCandidateTests
    {
        [Test]
        public void BackendFingerprint_MatchesBackendCanonicalVector()
        {
            var config = new AEDBackendScenarioConfigDto
            {
                scenarioConfigId = "LOCAL_AED_SUPPORT_1",
                scenarioConfigVersion = "LOCAL_AED_SUPPORT_1_V1",
                schemaVersion = "AED_SCENARIO_CONFIG_V1_1",
                policyVersion = "AED_SCENARIO_POLICY_V1_1",
                configSource = "ADAPTIVE",
                mapId = "M2-MAP-1",
                monsterType = "STALKER",
                objectiveSpawnSetId = "BASE",
                supportItemBudget = 1,
                detectionFillRate = 1.25,
                detectionDecayRate = 0.5,
                chaseSpeed = 9,
                searchDuration = 20,
                routeModifier = "NONE",
                escapeDoorTimerSeconds = 60,
                fallbackConfigId = "FIXED_BASELINE_V1",
                fallbackConfigVersion = "FIXED_BASELINE_V1",
                contentWhitelistVersion = "M2-WHITELIST-1",
                unityCompatibilityVersion = "0.1.0"
            };

            Assert.That(Fingerprint(config), Is.EqualTo(
                "095bfcfda97efcb352c0f542d0828e4e29f2ec51a38420db3ba470ed721a87cc"));
            config.supportItemBudget++;
            Assert.That(Fingerprint(config), Is.Not.EqualTo(
                "095bfcfda97efcb352c0f542d0828e4e29f2ec51a38420db3ba470ed721a87cc"));
        }

        [Test]
        public void StagingRejectsCandidateWhoseContentChangedAfterFingerprint()
        {
            var baseline = FixedDirector.CreateFixedBaseline();
            var config = new AEDBackendScenarioConfigDto
            {
                scenarioConfigId = "LOCAL_AED_SUPPORT_1",
                scenarioConfigVersion = "LOCAL_AED_SUPPORT_1_V1",
                schemaVersion = "AED_SCENARIO_CONFIG_V1_1",
                policyVersion = baseline.PolicyVersion,
                configSource = "ADAPTIVE",
                mapId = baseline.MapId,
                monsterType = baseline.MonsterType,
                objectiveSpawnSetId = baseline.ObjectiveSpawnSetId,
                supportItemBudget = baseline.SupportItemBudget + 1,
                detectionFillRate = baseline.MonsterParameters.DetectionFillRate,
                detectionDecayRate = baseline.MonsterParameters.DetectionDecayRate,
                chaseSpeed = baseline.MonsterParameters.ChaseSpeed,
                searchDuration = baseline.MonsterParameters.SearchDuration,
                routeModifier = baseline.RouteModifier,
                escapeDoorTimerSeconds = baseline.FinalHuntParameters.EscapeDoorTimerSeconds,
                fallbackConfigId = baseline.FallbackConfigId,
                fallbackConfigVersion = FixedDirector.FixedBaselineScenarioVersion,
                contentWhitelistVersion = FixedDirector.FixedBaselineContentWhitelistVersion,
                unityCompatibilityVersion = Application.version
            };
            config.contentFingerprint = Fingerprint(config);
            var matchId = Guid.NewGuid();
            var decision = new AEDResolvePreMatchData
            {
                matchId = matchId.ToString("D"),
                decisionId = ScenarioConfigAuthorityRuntime.CreateDecisionId(
                    matchId, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", 0).ToString("D"),
                resolutionResult = "ADAPTIVE_CONFIG_ISSUED",
                candidateValidationStatus = "VALID",
                decisionStatus = "COMMITTED",
                config = config
            };

            var existing = ScenarioConfigAuthorityRuntime.Instance;
            var runtime = ScenarioConfigAuthorityRuntime.EnsureExists();
            var settings = Resources.Load<AEDRuntimeSettings>("AED/AEDRuntimeSettings");
            Assert.That(settings, Is.Not.Null);
            typeof(ScenarioConfigAuthorityRuntime).GetField("runtimeSettings",
                BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(runtime, settings);
            try
            {
                Assert.That(runtime.TryStageBackendPreMatch(matchId, decision), Is.True);
                Assert.That(runtime.TryGetStagedBackendPreMatch(matchId, out _,
                    out var stagedConfig), Is.True);
                config.supportItemBudget++;
                Assert.That(runtime.ConfirmStagedBackendPreMatchAsync(matchId,
                    stagedConfig).GetAwaiter().GetResult(), Is.False,
                    "A receipt cannot confirm a mutated Backend candidate.");
                Assert.That(runtime.TryStageBackendPreMatch(matchId, decision), Is.False);
                Assert.That(runtime.TryGetStagedBackendPreMatch(matchId, out _, out _), Is.False);
                config.contentFingerprint = Fingerprint(config);
                Assert.That(runtime.TryStageBackendPreMatch(matchId, decision), Is.False,
                    "A registered value cannot skip the next approved step.");
            }
            finally
            {
                runtime.ClearStagedBackendPreMatch(matchId);
                if (existing == null)
                    UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            }
        }

        [Test]
        public void HostSessionPropertiesUseExplicitModeAfterBindingReset()
        {
            var gameObject = new GameObject("AED_HostMode_Test");
            try
            {
                var authority = gameObject.AddComponent<MatchAuthorityRuntime>();
                authority.RequestScenarioResolutionMode(ScenarioResolutionMode.Adaptive);
                Assert.That((string)authority.BuildHostSessionProperties()[
                    MatchAuthorityRuntime.ScenarioResolutionModeSessionProperty],
                    Is.EqualTo("ADAPTIVE"));

                authority.ResetBinding();
                Assert.That((string)authority.BuildHostSessionProperties()[
                    MatchAuthorityRuntime.ScenarioResolutionModeSessionProperty],
                    Is.EqualTo("FIXED"));

                authority.RequestScenarioResolutionMode(ScenarioResolutionMode.Adaptive);
                Assert.That((string)authority.BuildHostSessionProperties()[
                    MatchAuthorityRuntime.ScenarioResolutionModeSessionProperty],
                    Is.EqualTo("ADAPTIVE"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase("Assets/Scenes/MainMenu.unity", "EchoProtocol.UI.MainMenu.MainMenuMatchmakingController", "confirmButton")]
        [TestCase("Assets/Scenes/Lobby.unity", "EchoProtocol.UI.LobbyRuntimeBindingsController", "startButton")]
        public void SceneModeSelectionAndStartControlsAreBound(string path, string typeName, string fieldName)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var type = Type.GetType(typeName + ", Assembly-CSharp");
                Assert.That(type, Is.Not.Null);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren(type, true))
                    {
                        var field = type.GetField(fieldName,
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.That(field?.GetValue(component), Is.Not.Null,
                            $"{path} has no bound {fieldName}.");
                        return;
                    }
                Assert.Fail($"{path} has no {typeName}.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string Fingerprint(AEDBackendScenarioConfigDto config)
        {
            var method = typeof(ScenarioConfigAuthorityRuntime).GetMethod(
                "ComputeBackendContentFingerprint", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { config });
        }
    }
}
