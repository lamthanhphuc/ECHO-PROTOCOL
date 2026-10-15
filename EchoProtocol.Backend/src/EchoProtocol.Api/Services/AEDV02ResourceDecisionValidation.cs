using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.DTOs.Scenarios;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02DecisionService
{
    private async Task<string> ValidateSupport(Guid match, AEDV02SupportAction support, AEDV02SourceResult source, string json, CancellationToken ct)
    {
        if (support.Kind != "EMERGENCY" && !source.PressureComplete || source.OpportunityContractVersion != AEDV02PilotOpportunities.Version ||
            source.ResourceNeeds?.Any(x => x.UserId == support.UserId && x.CauseEventId == support.CauseEventId && x.ToolId == support.ToolId &&
                (support.Kind == "EMERGENCY" ? x.Downed : !x.Downed)) != true) return "HOLD_SUPPORT_NEED_UNVERIFIED";
        if (support.Zone != source.Zone || !(support.Kind == "EMERGENCY" ? source.SupportSafeWindow : source.SafeWindow)) return "HOLD_SAFE_WINDOW_CLOSED";
        var previous = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == match && x.ApprovalStatus == "APPROVED" && x.ApplyStatus != "ABORTED" && x.ChangedKey == "SupportTool").ToArrayAsync(ct);
        var requests = previous.Select(x => JsonSerializer.Deserialize<AEDV02DecisionRequest>(x.ProposalJson)).ToArray();
        int count = requests.Count(x => x?.Support?.Kind == support.Kind);
        if (count >= (support.Kind == "EMERGENCY" ? AEDV02PilotOpportunities.EmergencySupportQuota : AEDV02PilotOpportunities.OrdinarySupportQuota) ||
            requests.Any(x => x?.Support?.CauseEventId == support.CauseEventId && x.Support.UserId == support.UserId)) return "HOLD_SUPPORT_QUOTA";
        if (support.Kind == "ORDINARY" && previous.Any(x => clock.GetUtcNow().UtcDateTime - x.CreatedAtUtc < TimeSpan.FromSeconds(AEDV02PilotOpportunities.SupportSeparationSeconds)))
            return "HOLD_SUPPORT_COOLDOWN";
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(json)!;
        var marker = manifest.Markers.SingleOrDefault(x => x.Id == support.MarkerId && x.Zone == support.Zone && x.Kind == "TOOL" &&
            x.AllowedToolIds.Contains(support.ToolId) && AEDV02ResourcePlacement.AllowsRole(support.Kind == "EMERGENCY" ? 3 : 2, x.ResourceRole));
        if (marker == null) return "HOLD_SUPPORT_MARKER_INVALID";
        var world = await ReadSourceWorld(match, source, ct);
        if (world == null || world.Tools.Any(x => x.MarkerId == marker.Id)) return "HOLD_SUPPORT_MARKER_OCCUPIED";
        if (!AEDV02ResourcePlacement.ClearOfSupply(marker, world, manifest.MinimumToolSpacing)) return "HOLD_SUPPORT_PLACEMENT_SOURCE_REQUIRED";
        var victim = world.Players.Single(x => x.UserId == support.UserId);
        var target = new[] { marker.X, marker.Y, marker.Z };
        if (!world.Players.Where(x => x.LifeState == "ALIVE" && !x.IsCaught).Any(helper =>
            manifest.HasReachableGeometry(new[] { helper.X, helper.Y, helper.Z }, target, world.Doors) &&
            manifest.HasReachableGeometry(target, new[] { victim.X, victim.Y, victim.Z }, world.Doors))) return "HOLD_SUPPORT_ROUTE_INVALID";
        return string.Empty;
    }

    private async Task<bool> ValidatePlacement(Guid match, AEDV02ProposedReservation[]? proposed, AEDV02SourceResult source, int grade, string json, CancellationToken ct)
    {
        if (proposed == null) return false;
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(json)!;
        var world = await ReadSourceWorld(match, source, ct);
        return world != null && AEDV02ResourcePlacement.ValidateProposed(manifest, world, proposed, grade);
    }

    private async Task<AEDV02WorldSample?> ReadSourceWorld(Guid match, AEDV02SourceResult source, CancellationToken ct)
    {
        var events = await telemetry.LoadAcceptedMatchEventsAsync(match, ct);
        var worlds = events.Where(x => x.MatchId == match && x.EventType == "AED_V02_WORLD_SAMPLE" && x.SchemaVersion == "1.3")
            .Select(x => JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson()))
            .Where(x => x?.IsValid() == true && x.Tick == source.EndTick && x.PhaseOrdinal == source.PhaseOrdinal &&
                x.RosterIdentity == source.RosterIdentity && x.PlanRevision == source.PlanRevision && x.PlanFingerprint == source.PlanFingerprint &&
                x.SceneManifestFingerprint == source.SceneManifestFingerprint).Take(2).ToArray();
        return worlds.Length == 1 ? worlds[0] : null;
    }
}
