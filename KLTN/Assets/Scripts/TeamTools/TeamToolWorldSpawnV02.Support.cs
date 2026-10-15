using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.TeamTools
{
    public static partial class TeamToolWorldSpawn
    {
        private static readonly Dictionary<Guid, (string Fingerprint, NetworkId Instance)> V02SupportActions = new Dictionary<Guid, (string, NetworkId)>();
        public static bool PreviewV02Support(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, AEDV02ResourceNeedDto need,
            int zone, float spacing, out AEDV02SupportActionDto action)
        {
            action = null;
            if (need == null || !Guid.TryParse(need.userId, out var user) || !Guid.TryParse(need.causeEventId, out var cause)) return false;
            var proposal = new AEDV02SupportAction { Kind = need.downed ? "EMERGENCY" : "ORDINARY", UserId = user,
                CauseEventId = cause, ToolId = need.toolId, Zone = zone };
            if (!TryPrepareV02Support(runner, catalog, match, proposal, spacing, out var plan)) return false;
            action = new AEDV02SupportActionDto { kind = proposal.Kind, userId = user.ToString("D"), causeEventId = cause.ToString("D"),
                toolId = proposal.ToolId, zone = zone, markerId = AEDV02SceneSource.MarkerId(plan.Point.transform) };
            return true;
        }

        public static bool TrySpawnV02Support(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, Guid decision,
            AEDV02SupportAction action, float spacing, out NetworkId instanceId)
        {
            instanceId = default;
            var authority = MatchAuthorityRuntime.Instance; var state = NetworkMatchState.Instance;
            if (runner == null || !runner.IsServer || action?.IsValid() != true || authority == null || !authority.HasStateAuthority ||
                authority.MatchId != match || state == null || state.IsEnded || state.AEDV02DecisionId.ToString() != decision.ToString("D") ||
                state.AEDV02ActionFingerprint.ToString() != action.Fingerprint(decision) ||
                authority.RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || authority.Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.PacingV02GameplayEnabled != true || _v02Supply?.MatchId != match) return false;
            if (V02SupportActions.TryGetValue(decision, out var prior))
            { instanceId = prior.Instance; return prior.Fingerprint == action.Fingerprint(decision); }
            if (!TryPrepareV02Support(runner, catalog, match, action, spacing, out var plan)) return false;
            var instance = SpawnPlannedTool(runner, plan);
            if (instance == null) return false;
            instanceId = instance.Id; V02SupportActions.Add(decision, (action.Fingerprint(decision), instanceId));
            authority.RecordPhase8ToolSpawn(instanceId, action.ToolId, action.Zone, runner.Tick.Raw);
            Debug.Log($"[AED_V02][SUPPORT_SPAWN] match={match:D} decision={decision:D} kind={action.Kind} user={action.UserId:D} cause={action.CauseEventId:D} tool={action.ToolId} instance={instanceId} marker={action.MarkerId}");
            return true;
        }

        private static bool TryPrepareV02Support(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, AEDV02SupportAction action,
            float spacing, out SpawnPlan plan)
        {
            plan = default;
            var authority = MatchAuthorityRuntime.Instance; var state = NetworkMatchState.Instance;
            if (runner == null || !runner.IsServer || catalog == null || authority == null || authority.MatchId != match || !authority.HasStateAuthority || state == null ||
                !(action.Kind == "EMERGENCY" ? state.IsV02SupportWindow() : state.IsV02SafeWindow()) ||
                !authority.TryReadCurrentV02World(out var world) || !AEDV02SceneSource.TryLoad(out var manifest, out _) ||
                !authority.IsV02NeedCurrent(action)) return false;
            var victim = world.Players.SingleOrDefault(x => x.UserId == action.UserId);
            if (victim == null || AEDV02ResourcePlacement.UsableSupply(manifest, world, victim, action.ToolId) != false ||
                !IsCorrectTool(catalog.GetPrefab(action.ToolId), action.ToolId)) return false;
            var entry = manifest.Markers.SingleOrDefault(x => x.Id == "Z" + action.Zone + "_ENTRY");
            if (entry == null) return false;
            var entryPosition = new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z);
            var actors = UnityEngine.Object.FindObjectsByType<NetworkBehaviour>().Where(x => x.Runner == runner &&
                (x is NetworkPlayerLifeState || x is EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime || x is EchoProtocol.AI.Minions.CreepMinionRuntime ||
                    x is NetworkInteractable && x is not NetworkTeamToolPickup && x is not EchoProtocol.Tools.Scanner.NetworkToolPickup)).Select(x => x.transform).ToArray();
            var options = new List<(TeamToolSpawnPoint Point, Vector3 Position, float Distance)>();
            foreach (var point in UnityEngine.Object.FindObjectsByType<TeamToolSpawnPoint>(FindObjectsInactive.Exclude))
            {
                if (point.gameObject.scene.path != manifest.ScenePath || (int)point.Zone != action.Zone || !point.Allows(action.ToolId)) continue;
                var marker = manifest.Markers.SingleOrDefault(x => x.Id == AEDV02SceneSource.MarkerId(point.transform));
                if (marker == null || action.MarkerId.Length != 0 && action.MarkerId != marker.Id ||
                    !AEDV02ResourcePlacement.AllowsRole(action.Kind == "EMERGENCY" ? 3 : 2, marker.ResourceRole) ||
                    !point.TryGetReachablePosition(entryPosition, out var position, out var meters) || HasNearbyWorldTeamTool(position, spacing) ||
                    IsV02Occupied(position, actors) || !TryV02EntryPath(entryPosition, position, out var path) || !manifest.TryValidatePlacementPath(path, world.Doors, out _)) continue;
                if (!world.Players.Where(x => x.LifeState == "ALIVE" && !x.IsCaught).Any(helper =>
                    TryV02EntryPath(new Vector3((float)helper.X, (float)helper.Y, (float)helper.Z), position, out var approach) &&
                    manifest.TryValidatePlacementPath(approach, world.Doors, out _) &&
                    TryV02EntryPath(position, new Vector3((float)victim.X, (float)victim.Y, (float)victim.Z), out var delivery) &&
                    manifest.TryValidatePlacementPath(delivery, world.Doors, out _))) continue;
                options.Add((point, position, meters));
            }
            if (options.Count == 0) return false;
            var chosen = options.OrderBy(x => x.Distance).ThenBy(x => AEDV02SceneSource.MarkerId(x.Point.transform), StringComparer.Ordinal).First();
            plan = new SpawnPlan(chosen.Point, chosen.Position, catalog.GetPrefab(action.ToolId), action.ToolId); return true;
        }
    }
}
