using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Telemetry;

namespace EchoProtocol.AI.AED
{
    public static class AEDV02SourceJson
    {
        public static string Decision(AEDV02DecisionRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var json = new TelemetryJsonObject().AddString("decisionId", request.decisionId).AddInteger("phaseOrdinal", request.phaseOrdinal)
                .AddString("decisionPoint", request.decisionPoint).AddInteger("windowSequence", request.windowSequence)
                .AddString("windowId", request.windowId).AddInteger("expectedPlanRevision", request.expectedPlanRevision)
                .AddString("previousPlanFingerprint", request.previousPlanFingerprint).AddNumberArray("planValues", request.planValues)
                .AddString("snapshotId", request.snapshotId).AddString("snapshotFingerprint", request.snapshotFingerprint)
                .AddString("rosterIdentity", request.rosterIdentity).AddString("sourceFingerprint", request.sourceFingerprint)
                .AddInteger("startTick", request.startTick).AddInteger("endTick", request.endTick)
                .AddString("sceneManifestFingerprint", request.sceneManifestFingerprint).AddString("policyVersion", request.policyVersion)
                .AddString("catalogVersion", request.catalogVersion).AddString("resolutionMode", request.resolutionMode)
                .AddString("difficulty", request.difficulty).AddString("targetPhaseName", request.targetPhaseName);
            if (request.supportsRelativePreMatchRelief) json.AddBoolean("supportsRelativePreMatchRelief", true);
            if (request.proposedReservations == null) json.AddNull("proposedReservations");
            else json.AddArray("proposedReservations", request.proposedReservations.Select(x => new TelemetryJsonObject()
                .AddInteger("zone", x.zone).AddInteger("toolId", x.toolId).AddString("markerId", x.markerId)));
            if (request.support == null) json.AddNull("support");
            else json.AddObject("support", new TelemetryJsonObject().AddString("kind", request.support.kind)
                .AddString("userId", request.support.userId).AddString("causeEventId", request.support.causeEventId)
                .AddInteger("toolId", request.support.toolId).AddInteger("zone", request.support.zone).AddString("markerId", request.support.markerId));
            return json.ToJson();
        }
        public static string World(AEDV02WorldSample sample)
        {
            if (sample == null || !sample.IsValid()) throw new ArgumentException("AED_V02_WORLD_SOURCE_INVALID");
            var json = new TelemetryJsonObject().AddString("Version", sample.Version).AddString("OpportunityContractVersion", sample.OpportunityContractVersion)
                .AddString("ResolutionMode", sample.ResolutionMode).AddString("Difficulty", sample.Difficulty)
                .AddInteger("Tick", sample.Tick).AddInteger("TickRate", sample.TickRate)
                .AddInteger("PhaseOrdinal", sample.PhaseOrdinal).AddString("PhaseName", sample.PhaseName).AddInteger("LogicalPhase", sample.LogicalPhase)
                .AddInteger("Zone", sample.Zone).AddInteger("PlanRevision", sample.PlanRevision).AddString("PlanFingerprint", sample.PlanFingerprint)
                .AddString("RosterIdentity", sample.RosterIdentity).AddString("SceneManifestFingerprint", sample.SceneManifestFingerprint)
                .AddString("EvidenceExclusion", sample.EvidenceExclusion)
                .AddArray("Scannables", sample.Scannables.Select(ScanTarget))
                .AddArray("Players", sample.Players.Select(x => Position(new TelemetryJsonObject().AddString("UserId", x.UserId.ToString("D"))
                    .AddString("LifeState", x.LifeState).AddBoolean("IsCaught", x.IsCaught).AddBoolean("IsReviving", x.IsReviving)
                    .AddString("ReviverUserId", x.ReviverUserId?.ToString("D"))
                    .AddString("HeldToolInstanceId", x.HeldToolInstanceId).AddString("ObjectiveUnitId", x.ObjectiveUnitId)
                    .AddString("ObjectiveState", x.ObjectiveState).AddString("ObjectiveCauseEntityId", x.ObjectiveCauseEntityId)
                    .AddString("ObjectiveCauseEventId", x.ObjectiveCauseEventId).AddString("ResourceCauseEventId", x.ResourceCauseEventId).AddInteger("SlowUntilTick", x.SlowUntilTick)
                    .AddInteger("StabilizerUntilTick", x.StabilizerUntilTick).AddInteger("StabilizerActivationOrdinal", x.StabilizerActivationOrdinal)
                    .AddBoolean("StabilizerBuffed", x.StabilizerBuffed)
                    .AddInteger("RequiredToolId", x.RequiredToolId).AddString("HideSpotId", x.HideSpotId), x.X, x.Y, x.Z)))
                .AddArray("Stalkers", sample.Stalkers.Select(Threat)).AddArray("Minions", sample.Minions.Select(Threat))
                .AddArray("Tools", sample.Tools.Select(x =>
                {
                    var item = new TelemetryJsonObject().AddString("InstanceId", x.InstanceId).AddInteger("ToolId", x.ToolId).AddInteger("Zone", x.Zone)
                        .AddString("Location", x.Location).AddString("Origin", x.Origin).AddString("OwnerUserId", x.OwnerUserId?.ToString("D"))
                        .AddString("CarrierMinionId", x.CarrierMinionId)
                        .AddString("MarkerId", x.MarkerId).AddStringArray("RouteEdgeIds", x.RouteEdgeIds);
                    Position(item, x.X, x.Y, x.Z);
                    return x.RemainingCharges.HasValue ? item.AddInteger("RemainingCharges", x.RemainingCharges.Value) : item.AddNull("RemainingCharges");
                })).AddArray("Doors", sample.Doors.Select(x => new TelemetryJsonObject().AddString("DoorId", x.DoorId).AddString("State", x.State)))
                .AddArray("Jammers", sample.Jammers.Select(x => Position(new TelemetryJsonObject().AddString("NetworkId", x.NetworkId)
                    .AddString("MatchId", x.MatchId.ToString("D")).AddString("PhaseName", x.PhaseName)
                    .AddString("UserId", x.UserId.ToString("D")).AddString("Occurrence", x.Occurrence).AddInteger("DeploymentTick", x.DeploymentTick)
                    .AddString("DoorId", x.DoorId).AddBoolean("Active", x.Active).AddBoolean("ColliderBlocking", x.ColliderBlocking)
                    .AddNumber("BreakSeconds", x.BreakSeconds), x.X, x.Y, x.Z)))
                .AddArray("Scans", sample.Scans.Select(x => Position(new TelemetryJsonObject().AddString("MatchId", x.MatchId.ToString("D"))
                    .AddString("UserId", x.UserId.ToString("D")).AddInteger("Sequence", x.Sequence).AddString("Occurrence", x.Occurrence)
                    .AddInteger("Tick", x.Tick).AddString("PhaseName", x.PhaseName).AddString("Mode", x.Mode).AddBoolean("Presented", x.Presented).AddInteger("PresentedTick", x.PresentedTick)
                    .AddNumber("Range", x.Range).AddNumber("MovingThreshold", x.MovingThreshold).AddNumber("OcclusionMultiplier", x.OcclusionMultiplier)
                    .AddInteger("MaxTargets", x.MaxTargets).AddIntegerArray("ResultIds", x.ResultIds)
                    .AddArray("Candidates", x.Candidates.Select(ScanTarget)), x.X, x.Y, x.Z)));
            return (sample.Supply == null ? json.AddNull("Supply") : json.AddObject("Supply", Supply(sample.Supply))).ToJson();
        }
        public static TelemetryJsonObject Supply(AEDV02SupplySource source) => new TelemetryJsonObject()
            .AddInteger("InitialOptionalBudget", source.InitialOptionalBudget).AddInteger("OptionalBudget", source.OptionalBudget)
            .AddInteger("OptionalMaterialized", source.OptionalMaterialized).AddInteger("ReservationGrade", source.ReservationGrade)
            .AddIntegerArray("SpawnedZones", source.SpawnedZones)
            .AddArray("Reservations", source.Reservations.Select(x => new TelemetryJsonObject().AddInteger("Zone", x.Zone)
                .AddInteger("ToolId", x.ToolId).AddString("MarkerId", x.MarkerId).AddNumberMatrix("EntryPath", x.EntryPath)));
        public static string ResourceManagementAction(AEDV02ResourceManagementAction action)
        {
            if (action == null || !action.IsValid()) throw new ArgumentException("AED_V02_RESOURCE_ACTION_INVALID");
            return new TelemetryJsonObject().AddString("Version", action.Version)
                .AddString("CauseEventId", action.CauseEventId.ToString("D"))
                .AddString("ActorUserId", action.ActorUserId.ToString("D")).AddString("Action", action.Action)
                .AddString("ToolInstanceId", action.ToolInstanceId).AddString("LifecycleOccurrenceKey", action.LifecycleOccurrenceKey)
                .AddInteger("ToolId", action.ToolId).AddInteger("Tick", action.Tick).ToJson();
        }
        public static string Commit(AEDV02PlanCommitSource commit)
        {
            if (commit == null || !commit.IsValid()) throw new ArgumentException("AED_V02_COMMIT_INVALID");
            return new TelemetryJsonObject().AddString("DecisionId", commit.DecisionId.ToString("D")).AddInteger("PlanRevision", commit.PlanRevision)
                .AddString("PlanFingerprint", commit.PlanFingerprint).AddString("ActionFingerprint", commit.ActionFingerprint)
                .AddInteger("AppliedTick", commit.AppliedTick).ToJson();
        }
        public static string Impact(AEDV02CausalImpact impact)
        {
            if (impact == null || !impact.IsValid()) throw new ArgumentException("AED_V02_CAUSAL_IMPACT_INVALID");
            return new TelemetryJsonObject().AddString("Version", impact.Version).AddString("UserId", impact.UserId.ToString("D"))
                .AddString("Kind", impact.Kind).AddString("EntityId", impact.EntityId).AddString("ObjectiveUnitId", impact.ObjectiveUnitId)
                .AddString("ToolInstanceId", impact.ToolInstanceId).AddInteger("ToolId", impact.ToolId)
                .AddInteger("Tick", impact.Tick).AddInteger("UntilTick", impact.UntilTick).ToJson();
        }
        public static string Effect(AEDV02EffectSource effect)
        {
            if (effect == null || !effect.IsValid()) throw new ArgumentException("AED_V02_EFFECT_INVALID");
            return new TelemetryJsonObject().AddString("DecisionId", effect.DecisionId.ToString("D")).AddInteger("PlanRevision", effect.PlanRevision)
                .AddString("PlanFingerprint", effect.PlanFingerprint).AddString("Consumer", effect.Consumer).AddString("Key", effect.Key)
                .AddNumber("ObservedValue", effect.ObservedValue).AddInteger("ObservedTick", effect.ObservedTick)
                .AddStringArray("NetworkObjectIds", effect.NetworkObjectIds).ToJson();
        }
        public static string Carrier(AEDV02ToolCarrierSource carrier)
        {
            if (carrier == null || !carrier.IsValid()) throw new ArgumentException("AED_V02_TOOL_CARRIER_INVALID");
            return new TelemetryJsonObject().AddString("ToolInstanceId", carrier.ToolInstanceId).AddString("MinionNetworkId", carrier.MinionNetworkId)
                .AddString("Action", carrier.Action).AddInteger("Tick", carrier.Tick).ToJson();
        }
        public static string CabinetClue(AEDV02CabinetClueSource clue)
        {
            if (clue == null || !clue.IsValid()) throw new ArgumentException("AED_V02_CABINET_CLUE_INVALID");
            return new TelemetryJsonObject().AddString("UserId", clue.UserId.ToString("D")).AddString("StalkerNetworkId", clue.StalkerNetworkId)
                .AddString("HideSpotId", clue.HideSpotId).AddString("SceneMarkerId", clue.SceneMarkerId).AddString("SceneManifestFingerprint", clue.SceneManifestFingerprint)
                .AddInteger("Tick", clue.Tick).AddNumber("OriginX", clue.OriginX).AddNumber("OriginY", clue.OriginY).AddNumber("OriginZ", clue.OriginZ)
                .AddNumber("TargetX", clue.TargetX).AddNumber("TargetY", clue.TargetY).AddNumber("TargetZ", clue.TargetZ).AddNumber("ObservedDistance", clue.ObservedDistance)
                .AddNumber("PlayerX", clue.PlayerX).AddNumber("PlayerY", clue.PlayerY).AddNumber("PlayerZ", clue.PlayerZ).ToJson();
        }
        private static TelemetryJsonObject Threat(AEDV02ThreatSource x) => Position(new TelemetryJsonObject().AddString("NetworkId", x.NetworkId)
            .AddString("State", x.State).AddString("EpisodeId", x.EpisodeId).AddString("TargetUserId", x.TargetUserId?.ToString("D"))
            .AddNumberArray("ConsumerValues", x.ConsumerValues)
            .AddString("JammerBlockerId", x.JammerBlockerId).AddNumber("JammerProgress", x.JammerProgress)
            .AddString("InvestigationNoiseId", x.InvestigationNoiseId).AddNumber("DestinationX", x.DestinationX)
            .AddNumber("DestinationY", x.DestinationY).AddNumber("DestinationZ", x.DestinationZ)
            .AddInteger("Zone", x.Zone), x.X, x.Y, x.Z);
        private static TelemetryJsonObject ScanTarget(AEDV02ScanCandidate c) => Position(new TelemetryJsonObject().AddInteger("LocalId", c.LocalId)
            .AddString("Identity", c.Identity).AddString("Kind", c.Kind).AddBoolean("Available", c.Available).AddBoolean("Moving", c.Moving)
            .AddBoolean("Occluded", c.Occluded).AddNumber("Speed", c.Speed), c.X, c.Y, c.Z);
        private static TelemetryJsonObject Position(TelemetryJsonObject json, double x, double y, double z) => json.AddNumber("X", x).AddNumber("Y", y).AddNumber("Z", z);
    }
}
