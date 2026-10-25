using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDV02ResourceManagementOpportunityTests
{
    [Fact, Trait("Category", "AEDV02")]
    public void HistoricalResourceIncreaseRequiresDedicatedAxis()
    {
        Assert.Contains(AEDV02PilotOpportunities.ResourceManagementAxis, AEDV02PilotOpportunities.HistoricalIncreaseAxes);
        Assert.False(AEDV02PilotOpportunities.HasResourceManagementHistory(["Tool"]));
        Assert.True(AEDV02PilotOpportunities.HasResourceManagementHistory([AEDV02PilotOpportunities.ResourceManagementAxis]));
        Assert.False(AEDV02PilotOpportunities.HasResourceManagementHistory(null!));
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("Success")]
    [InlineData("Failure")]
    [InlineData("Censored")]
    public void ResourceManagementOutcomeUsesDedicatedAxisAndNoToolId(string resolution)
    {
        var outcome = new AEDV02ResolvedOpportunity
        {
            Id = "ResourceManagement:" + Guid.NewGuid().ToString("N"), UserId = Guid.NewGuid(),
            Axis = "ResourceManagement", ToolId = 0, Resolution = resolution, StartTick = 10, EndTick = 11
        };

        Assert.True(outcome.IsValid());
        outcome.ToolId = 3;
        Assert.False(outcome.IsValid());
    }

    [Fact, Trait("Category", "AEDV02")]
    public void ResourceActionRejectsForgedCauseAndUnsupportedTool()
    {
        var action = new AEDV02ResourceManagementAction
        {
            CauseEventId = Guid.NewGuid(), ActorUserId = Guid.NewGuid(), Action = "PICKUP",
            ToolInstanceId = "tool:1", LifecycleOccurrenceKey = "resource:1", ToolId = 1, Tick = 10
        };

        Assert.True(action.IsValid());
        action.CauseEventId = Guid.Empty;
        Assert.False(action.IsValid());
        action.CauseEventId = Guid.NewGuid(); action.ToolId = 9;
        Assert.False(action.IsValid());
    }

    [Fact, Trait("Category", "AEDV02")]
    public void ResourceVerifierFailsClosedWithoutCanonicalCauseOrFrames()
    {
        var method = typeof(AEDV02SourceVerifier).GetMethod(
            "TryResolveResourceManagementOpportunities",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var manifest = new AEDV02SceneManifest { SceneVersion = "resource-contract", Markers = [
            new AEDV02SceneMarker { Id = AEDV02Plan.Hash("entry"), Kind = "ENTRY", Zone = 1 }
        ] };
        // The exact telemetry tuple is intentionally not forged here; the verifier must reject absent authoritative frames.
        Assert.False((bool)method!.Invoke(null, new object?[] { Array.Empty<EchoProtocol.Api.Data.Telemetry.TelemetryEventDocument>(),
            new List<(EchoProtocol.Api.Data.Telemetry.TelemetryEventDocument Event, AEDV02WorldSample Sample)>(), manifest, null })!);
    }

    [Fact, Trait("Category", "AEDV02")]
    public void SafeRoomRegistryRejectsMissingBoundsAndIntersectsSegments()
    {
        var manifest = new AEDV02SceneManifest
        {
            SceneVersion = "safe-room-contract",
            Markers = [new AEDV02SceneMarker { Id = AEDV02Plan.Hash("safe-room"), Kind = "SAFE_ROOM", Zone = 1,
                InteractionBounds = [-2, -1, -2, 2, 3, 2] }]
        };

        Assert.True(manifest.TryIsInsideSafeRoom(0, 0, 0, out var inside));
        Assert.True(inside);
        Assert.True(manifest.TryPathIntersectsSafeRoom([[-4, 0, 0], [4, 0, 0]], out var intersects));
        Assert.True(intersects);
        manifest.Markers[0].InteractionBounds = null!;
        Assert.False(manifest.TryIsInsideSafeRoom(0, 0, 0, out _));
    }
}
