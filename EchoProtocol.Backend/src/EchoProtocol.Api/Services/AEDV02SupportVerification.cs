using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    internal async Task<HashSet<string>> AcceptedSupportInstances(Guid match, IReadOnlyList<TelemetryEventDocument> events, CancellationToken ct)
    {
        var rows = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == match && x.ApprovalStatus == "APPROVED" && x.ApplyStatus != "ABORTED" && x.ActionFingerprint != "")
            .ToArrayAsync(ct);
        var ids = new HashSet<string>();
        foreach (var row in rows)
        {
            AEDV02DecisionRequest? request;
            try { request = JsonSerializer.Deserialize<AEDV02DecisionRequest>(row.ProposalJson); }
            catch (JsonException) { continue; }
            var support = request?.Support;
            if (support?.IsValid() != true || support.Fingerprint(row.DecisionId) != row.ActionFingerprint) continue;
            foreach (var item in events.Where(x => x.EventType == "AED_V02_ACTION_EFFECT" && x.SchemaVersion == "1.3"))
            {
                var effect = JsonSerializer.Deserialize<AEDV02EffectSource>(item.ValueJson["data"]["effect"].ToJson());
                if (effect?.IsValid() != true || effect.DecisionId != row.DecisionId || effect.PlanRevision != row.PlanRevision ||
                    effect.PlanFingerprint != row.PlanFingerprint || effect.Consumer != "TeamToolWorldSpawn" || effect.Key != "SupportTool" ||
                    effect.ObservedValue != support.ToolId || effect.NetworkObjectIds.Length != 1) continue;
                if (events.Any(x => x.EventType == "TEAM_TOOL_LIFECYCLE" && x.EventSequence < item.EventSequence && x.Ts >= row.CreatedAtUtc &&
                    x.ValueJson["data"]["action"] == "SPAWN" && x.ValueJson["data"]["instanceId"] == effect.NetworkObjectIds[0] &&
                    x.ValueJson["data"]["toolId"].ToInt32() == support.ToolId && x.ValueJson["data"]["zone"].ToInt32() == support.Zone))
                    ids.Add(effect.NetworkObjectIds[0]);
            }
        }
        return ids;
    }
}
