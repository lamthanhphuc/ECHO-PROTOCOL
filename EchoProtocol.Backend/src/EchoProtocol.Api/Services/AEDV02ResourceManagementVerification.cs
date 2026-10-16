using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    internal static bool TryResolveResourceManagementOpportunities(
        IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames,
        AEDV02SceneManifest manifest,
        out AEDV02ResolvedOpportunity[] outcomes)
    {
        outcomes = [];
        if (accepted == null || frames == null || frames.Count < 2 || manifest?.IsValid() != true ||
            !TryReadImpacts(accepted, frames, out var impacts)) return false;

        var first = frames[0];
        var last = frames[^1];
        var roster = last.Sample.Players.Select(x => x.UserId).ToHashSet();
        var impactsByEventId = new Dictionary<Guid, (TelemetryEventDocument Event, AEDV02CausalImpact Impact)>();
        foreach (var impact in impacts)
        {
            var source = accepted.Where(e => e.Id == impact.Key && e.EventType == "AED_V02_CAUSAL_IMPACT").ToArray();
            if (source.Length != 1) return false;
            impactsByEventId.Add(impact.Key, (source[0], impact.Value));
        }

        var actions = new List<(TelemetryEventDocument Event, AEDV02ResourceManagementAction Action,
            TelemetryEventDocument Lifecycle, AEDV02WorldSample? Before, AEDV02WorldSample? After)>();
        foreach (var item in accepted.Where(x => x.MatchId == last.Event.MatchId &&
            x.EventType == "AED_V02_RESOURCE_ACTION" && x.EventSequence >= first.Event.EventSequence &&
            x.EventSequence <= last.Event.EventSequence))
        {
            AEDV02ResourceManagementAction? action;
            if (!item.ValueJson.TryGetValue("data", out var rawData) || !rawData.IsBsonDocument ||
                !rawData.AsBsonDocument.TryGetValue("action", out var rawAction))
                return false;
            try { action = JsonSerializer.Deserialize<AEDV02ResourceManagementAction>(rawAction.ToJson()); }
            catch (JsonException) { return false; }
            if (item.SchemaVersion != "1.3" || item.UserId.HasValue || action?.IsValid() != true ||
                !roster.Contains(action.ActorUserId) ||
                !TryReadActionContext(item, last.Sample.PhaseName, action.Tick) ||
                !impactsByEventId.TryGetValue(action.CauseEventId, out var cause) ||
                cause.Impact.Kind != "MINION_TOOL_LOSS" || cause.Impact.ToolId != action.ToolId ||
                cause.Impact.Tick > action.Tick || cause.Impact.UntilTick < action.Tick)
                return false;

            var lifecycleEvents = accepted.Where(x => x.MatchId == item.MatchId && x.EventType == "TEAM_TOOL_LIFECYCLE" &&
                x.SchemaVersion == "1.2" && HasContextOccurrence(x, action.LifecycleOccurrenceKey) &&
                x.EventSequence < item.EventSequence).ToArray();
            if (lifecycleEvents.Length != 1 || !MatchesLifecycle(lifecycleEvents[0], action)) return false;
            if (!AEDPhase8LifecycleReconcilerV1.TryReconstruct(accepted, item.MatchId, lifecycleEvents[0].EventSequence,
                    roster, false, out var toolState, out _, out _) ||
                !toolState.TryGetValue(action.ToolInstanceId, out var lifecycleTool) ||
                lifecycleTool.ToolId != action.ToolId || !MatchesLifecycleState(lifecycleTool, action))
                return false;

            var before = frames.LastOrDefault(x => x.Event.EventSequence < lifecycleEvents[0].EventSequence &&
                x.Sample.Tick <= action.Tick).Sample;
            var after = frames.FirstOrDefault(x => x.Event.EventSequence > item.EventSequence &&
                x.Sample.Tick >= action.Tick).Sample;
            if (action.Action == "PICKUP" && (before == null || after == null ||
                !before.Tools.Any(x => x.InstanceId == action.ToolInstanceId && x.ToolId == action.ToolId &&
                    x.Location == "World" && x.CarrierMinionId.Length == 0 && x.RemainingCharges != 0) ||
                !after.Tools.Any(x => x.InstanceId == action.ToolInstanceId && x.ToolId == action.ToolId &&
                    x.Location == "Player" && x.OwnerUserId == action.ActorUserId &&
                    x.CarrierMinionId.Length == 0 && x.RemainingCharges != 0) ||
                after.Players.SingleOrDefault(x => x.UserId == action.ActorUserId)?.HeldToolInstanceId != action.ToolInstanceId))
                before = after = null;
            actions.Add((item, action, lifecycleEvents[0], before, after));
        }

        var results = new Dictionary<(Guid Cause, Guid Actor), AEDV02ResolvedOpportunity>();
        var lossImpacts = impactsByEventId.Where(x => x.Value.Impact.Kind == "MINION_TOOL_LOSS" &&
            x.Value.Impact.Tick <= last.Sample.Tick && x.Value.Impact.UntilTick >= first.Sample.Tick).ToArray();
        foreach (var entry in lossImpacts)
        {
            Guid causeId = entry.Key;
            var impact = entry.Value.Impact;
            var causeEvent = entry.Value.Event!;
            var needFrames = frames.Where(x => x.Sample.Tick >= impact.Tick && x.Sample.Tick <= impact.UntilTick)
                .Select(x => (x.Sample, Player: x.Sample.Players.SingleOrDefault(p => p.UserId == impact.UserId)))
                .Where(x => x.Player?.RequiredToolId == impact.ToolId &&
                    x.Player.ResourceCauseEventId == causeId.ToString("D")).ToArray();
            var matchingActions = actions.Where(x => x.Action.CauseEventId == causeId)
                .GroupBy(x => (x.Action.CauseEventId, x.Action.ActorUserId))
                .Select(x => x.OrderBy(y => y.Event.EventSequence).First()).ToArray();
            if (matchingActions.Length == 0)
            {
                Add(causeId, impact.UserId, "Censored", impact.Tick, last.Sample.Tick);
                continue;
            }

            foreach (var observed in matchingActions)
            {
                if (IsReviveToolUse(accepted, observed.Event, observed.Action, impact.UserId)) continue;
                string resolution = "Censored";
                long endTick = last.Sample.Tick;
                bool completeNeed = first.Sample.Tick <= impact.Tick && last.Sample.Tick >= impact.UntilTick &&
                    impact.UntilTick - impact.Tick <= last.Sample.TickRate * AEDV02PilotOpportunities.ResourceLossNeedSeconds;
                bool disconnected = accepted.Any(x => x.MatchId == last.Event.MatchId &&
                    x.EventType == "PLAYER_DISCONNECTED" && x.UserId == impact.UserId &&
                    x.EventSequence >= causeEvent.EventSequence && x.EventSequence <= last.Event.EventSequence);
                bool needObservedAtAction = observed.Before?.Players.Any(x => x.UserId == impact.UserId &&
                    x.RequiredToolId == impact.ToolId && x.ResourceCauseEventId == causeId.ToString("D")) == true;
                if (completeNeed && !disconnected && needFrames.Length != 0 && needObservedAtAction &&
                    observed.Before != null && observed.After != null)
                {
                    var need = observed.After.Players.SingleOrDefault(x => x.UserId == impact.UserId);
                    if (observed.Action.Action == "PICKUP" && need != null &&
                        AEDV02ResourcePlacement.UsableSupply(manifest, observed.After, need, impact.ToolId) == true &&
                        observed.After.Tick > observed.Action.Tick)
                    {
                        resolution = "Success";
                        endTick = observed.After.Tick;
                    }
                    else if (observed.Action.Action is "DROP" or "USE" or "CONSUME" && need != null)
                    {
                        var observations = frames.Where(x => x.Sample.Tick >= observed.Action.Tick &&
                            x.Sample.Tick <= impact.UntilTick).ToArray();
                        var supply = observations.Select(x =>
                        {
                            var player = x.Sample.Players.SingleOrDefault(p => p.UserId == impact.UserId);
                            return player?.RequiredToolId == impact.ToolId && player.ResourceCauseEventId == causeId.ToString("D")
                                ? AEDV02ResourcePlacement.UsableSupply(manifest, x.Sample, player, impact.ToolId)
                                : (bool?)null;
                        }).ToArray();
                        if (supply.Length != 0 && supply.All(x => x == false))
                        {
                            resolution = "Failure";
                            endTick = impact.UntilTick;
                        }
                    }
                }
                Add(causeId, observed.Action.ActorUserId, resolution, observed.Action.Tick, endTick);
            }
        }

        outcomes = results.Values.ToArray();
        return true;

        void Add(Guid cause, Guid actor, string resolution, long start, long end)
        {
            if (actor == Guid.Empty) return;
            var key = (cause, actor);
            if (results.ContainsKey(key)) return;
            results.Add(key, new AEDV02ResolvedOpportunity
            {
                Id = "ResourceManagement:" + cause.ToString("D") + ":" + actor.ToString("D"),
                Axis = "ResourceManagement",
                ToolId = 0,
                UserId = actor,
                StartTick = Math.Max(0, start),
                EndTick = Math.Max(start + 1, end),
                Resolution = resolution
            });
        }
    }

    private static bool MatchesLifecycle(TelemetryEventDocument item, AEDV02ResourceManagementAction action)
    {
        if (!item.ValueJson.TryGetValue("data", out var rawData) || !rawData.IsBsonDocument ||
            !item.ValueJson.TryGetValue("context", out var rawContext) || !rawContext.IsBsonDocument)
            return false;
        var data = rawData.AsBsonDocument;
        var context = rawContext.AsBsonDocument;
        return context.TryGetValue("authorityTick", out var tick) && tick.IsNumeric && tick.ToInt64() == action.Tick &&
            data.TryGetValue("action", out var operation) && operation.IsString && operation.AsString == action.Action &&
            data.TryGetValue("toolId", out var toolId) && toolId.IsNumeric && toolId.ToInt32() == action.ToolId &&
            data.TryGetValue("instanceId", out var instance) && instance.IsString && instance.AsString == action.ToolInstanceId &&
            data.TryGetValue("ownerUserId", out var owner) && owner.IsString && owner.AsString == action.ActorUserId.ToString("D");
    }

    private static bool MatchesLifecycleState(AEDPhase8LifecycleReconcilerV1.Tool tool,
        AEDV02ResourceManagementAction action) => action.Action switch
    {
        "PICKUP" => tool.Location == "Player" && tool.Owner == action.ActorUserId && tool.Charges != 0,
        "USE" => tool.Location == "Player" && tool.Owner == action.ActorUserId && tool.Charges != 0,
        "CONSUME" => tool.Location == "Consumed" && tool.Owner == action.ActorUserId && tool.Charges == 0,
        "DROP" => tool.Location == "World" && tool.Owner == null,
        _ => false
    };

    private static bool IsReviveToolUse(IReadOnlyList<TelemetryEventDocument> accepted,
        TelemetryEventDocument actionEvent, AEDV02ResourceManagementAction action, Guid needUserId) =>
        action.ToolId == 3 && action.Action == "CONSUME" &&
        accepted.Any(x => x.MatchId == actionEvent.MatchId && x.EventType == "PLAYER_REVIVED" &&
            x.UserId == needUserId && x.EventSequence > actionEvent.EventSequence &&
            HasReviveActor(x, action.ActorUserId, action.Tick));

    private static bool TryReadActionContext(TelemetryEventDocument item, string phase, long tick) =>
        item.ValueJson.TryGetValue("context", out var raw) && raw.IsBsonDocument &&
        raw.AsBsonDocument.TryGetValue("authorityTick", out var rawTick) && rawTick.IsNumeric && rawTick.ToInt64() == tick &&
        raw.AsBsonDocument.TryGetValue("phase", out var rawPhase) && rawPhase.IsString && rawPhase.AsString == phase;

    private static bool HasContextOccurrence(TelemetryEventDocument item, string occurrence) =>
        item.ValueJson.TryGetValue("context", out var raw) && raw.IsBsonDocument &&
        raw.AsBsonDocument.TryGetValue("occurrenceKey", out var value) && value.IsString && value.AsString == occurrence;

    private static bool HasReviveActor(TelemetryEventDocument item, Guid actor, long tick)
    {
        if (!item.ValueJson.TryGetValue("context", out var rawContext) || !rawContext.IsBsonDocument ||
            !rawContext.AsBsonDocument.TryGetValue("authorityTick", out var rawTick) || !rawTick.IsNumeric ||
            rawTick.ToInt64() != tick || !item.ValueJson.TryGetValue("data", out var rawData) || !rawData.IsBsonDocument)
            return false;
        var data = rawData.AsBsonDocument;
        return data.TryGetValue("usedFirstAidKit", out var used) && used.IsBoolean && used.AsBoolean &&
            data.TryGetValue("reviverPlayerId", out var reviver) && reviver.IsString && reviver.AsString == actor.ToString("D");
    }
}
