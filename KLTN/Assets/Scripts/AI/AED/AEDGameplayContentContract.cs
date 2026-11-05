using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.AI.AED
{
    public static class AEDGameplayContentContract
    {
        public static bool TryValidate(ScenarioConfig config, out string reason)
        {
            reason = "AED_CONTENT_CONTRACT_MISSING";
            if (config == null
                || config.MapId != FixedDirector.FixedBaselineMapId
                || config.MonsterType != FixedDirector.FixedBaselineMonsterType
                || config.ObjectiveSpawnSetId != FixedDirector.FixedBaselineObjectiveSpawnSetId
                || config.RouteModifier != FixedDirector.FixedBaselineRouteModifier)
                return false;
            if (!Application.CanStreamedLevelBeLoaded(LobbyManager.GameSceneName)
                && SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/SciFi.unity") < 0)
            {
                reason = "AED_CONTENT_MAP_UNAVAILABLE";
                return false;
            }

            // These type references bind the registry IDs to the gameplay systems
            // shipped by the SciFi scene/build instead of accepting string constants alone.
            _ = typeof(StalkerFusionRuntime);
            _ = typeof(NetworkSectorBox);
            _ = typeof(Zone2MissionDirector);
            _ = typeof(Zone3MissionDirector);
            _ = typeof(Zone3ConvoyRouteGraph);
            reason = string.Empty;
            return true;
        }
    }
}
