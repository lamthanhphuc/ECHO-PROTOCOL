using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Entities;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02DecisionService
{
    private static bool HasAcceptedCommit(IReadOnlyList<TelemetryEventDocument> events, AEDV02Decision decision, long? tick)
    {
        var commits = new List<AEDV02PlanCommitSource>();
        var terminal = events.Where(x => x.MatchId == decision.MatchId && x.EventType is "MATCH_ENDED" or "MATCH_ABORTED")
            .Select(x => x.EventSequence).DefaultIfEmpty(long.MaxValue).Min();
        foreach (var item in events.Where(x => x.MatchId == decision.MatchId && x.EventType == "AED_V02_PLAN_COMMIT" && x.SchemaVersion == "1.3" &&
            x.EventSequence < terminal && x.Ts >= decision.CreatedAtUtc && x.Ts <= decision.ExpiresAtUtc))
        {
            AEDV02PlanCommitSource? commit;
            try { commit = JsonSerializer.Deserialize<AEDV02PlanCommitSource>(item.ValueJson["data"]["commit"].ToJson()); }
            catch (JsonException) { continue; }
            if (commit?.IsValid() == true && commit.DecisionId == decision.DecisionId && commit.PlanRevision == decision.PlanRevision &&
                commit.PlanFingerprint == decision.PlanFingerprint && commit.ActionFingerprint == decision.ActionFingerprint &&
                commit.AppliedTick <= item.ValueJson["context"]["authorityTick"].ToInt64() && (!tick.HasValue || commit.AppliedTick == tick.Value)) commits.Add(commit);
        }
        return commits.Count == 1;
    }
}
