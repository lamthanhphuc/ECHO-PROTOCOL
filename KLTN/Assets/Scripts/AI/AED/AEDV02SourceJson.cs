using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Telemetry;

namespace EchoProtocol.AI.AED
{
    public static class AEDV02SourceJson
    {
        public static string World(AEDV02WorldSample sample)
        {
            if (sample == null || !sample.IsValid()) throw new ArgumentException("AED_V02_WORLD_SOURCE_INVALID");
            return new TelemetryJsonObject().AddString("Version", sample.Version).AddString("ResolutionMode", sample.ResolutionMode).AddString("Difficulty", sample.Difficulty)
                .AddInteger("Tick", sample.Tick).AddInteger("TickRate", sample.TickRate)
                .AddInteger("PhaseOrdinal", sample.PhaseOrdinal).AddString("PhaseName", sample.PhaseName).AddInteger("LogicalPhase", sample.LogicalPhase)
                .AddInteger("Zone", sample.Zone).AddInteger("PlanRevision", sample.PlanRevision).AddString("PlanFingerprint", sample.PlanFingerprint)
                .AddString("RosterIdentity", sample.RosterIdentity).AddString("SceneManifestFingerprint", sample.SceneManifestFingerprint)
                .AddString("EvidenceExclusion", sample.EvidenceExclusion)
                .AddArray("Players", sample.Players.Select(x => Position(new TelemetryJsonObject().AddString("UserId", x.UserId.ToString("D"))
                    .AddString("LifeState", x.LifeState).AddBoolean("IsCaught", x.IsCaught).AddBoolean("IsReviving", x.IsReviving)
                    .AddString("HeldToolInstanceId", x.HeldToolInstanceId).AddString("ObjectiveUnitId", x.ObjectiveUnitId)
                    .AddString("ObjectiveState", x.ObjectiveState).AddString("ObjectiveCauseEntityId", x.ObjectiveCauseEntityId)
                    .AddInteger("RequiredToolId", x.RequiredToolId).AddString("HideSpotId", x.HideSpotId), x.X, x.Y, x.Z)))
                .AddArray("Stalkers", sample.Stalkers.Select(Threat)).AddArray("Minions", sample.Minions.Select(Threat))
                .AddArray("Tools", sample.Tools.Select(x =>
                {
                    var item = new TelemetryJsonObject().AddString("InstanceId", x.InstanceId).AddInteger("ToolId", x.ToolId).AddInteger("Zone", x.Zone)
                        .AddString("Location", x.Location).AddString("Origin", x.Origin).AddString("OwnerUserId", x.OwnerUserId?.ToString("D"))
                        .AddString("MarkerId", x.MarkerId).AddStringArray("RouteEdgeIds", x.RouteEdgeIds);
                    Position(item, x.X, x.Y, x.Z);
                    return x.RemainingCharges.HasValue ? item.AddInteger("RemainingCharges", x.RemainingCharges.Value) : item.AddNull("RemainingCharges");
                })).AddArray("Doors", sample.Doors.Select(x => new TelemetryJsonObject().AddString("DoorId", x.DoorId).AddString("State", x.State))).ToJson();
        }
        public static string Effect(AEDV02EffectSource effect)
        {
            if (effect == null || !effect.IsValid()) throw new ArgumentException("AED_V02_EFFECT_INVALID");
            return new TelemetryJsonObject().AddString("DecisionId", effect.DecisionId.ToString("D")).AddInteger("PlanRevision", effect.PlanRevision)
                .AddString("PlanFingerprint", effect.PlanFingerprint).AddString("Consumer", effect.Consumer).AddString("Key", effect.Key)
                .AddNumber("ObservedValue", effect.ObservedValue).AddInteger("ObservedTick", effect.ObservedTick)
                .AddStringArray("NetworkObjectIds", effect.NetworkObjectIds).ToJson();
        }
        private static TelemetryJsonObject Threat(AEDV02ThreatSource x) => Position(new TelemetryJsonObject().AddString("NetworkId", x.NetworkId)
            .AddString("State", x.State).AddString("EpisodeId", x.EpisodeId).AddString("TargetUserId", x.TargetUserId?.ToString("D"))
            .AddInteger("Zone", x.Zone), x.X, x.Y, x.Z);
        private static TelemetryJsonObject Position(TelemetryJsonObject json, double x, double y, double z) => json.AddNumber("X", x).AddNumber("Y", y).AddNumber("Z", z);
    }
}
