using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact]
    public async Task ProfileTuning_PreviewDoesNotPublishAndVersionsAreImmutable()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new ProfileTuningService(fixture.Db, Options.Create(new PlayerAIProfileSettings()),
            Options.Create(new TeamProfileSettings()), new FixedClock(Now));
        var settings = new ProfileTuning(.5m, .5m, .3m, .3m, 0, 100, 5, ["SPRINT"],
            120, 900, 4, 10, 24, 7);
        var preview = await service.PreviewAsync(new(settings, true, true, 3,
            new() { ["SPRINT"] = 20, ["WALK"] = 5 }, 300, 60), default);
        Assert.True(preview.IsSuccess, preview.Message);
        Assert.Empty(fixture.Db.ProfileTuningVersions);
        var data = JsonSerializer.SerializeToElement(preview.Data);
        var after = data.GetProperty("After");
        Assert.Equal(20, after.GetProperty("CountedNoise").GetInt32());
        Assert.Equal(70m, after.GetProperty("Scores")[1].GetProperty("UpdatedScore").GetDecimal());
        Assert.Equal(0, after.GetProperty("ThirtyDayHistoryWeight").GetDouble());
        var fingerprint = data.GetProperty("PreviewFingerprint").GetString()!;

        var changed = await service.PublishAsync(fixture.HostId, new(null, settings with { NoiseMax = 120 }, fingerprint), default);
        Assert.Equal(ErrorCodes.ValidationError, changed.ErrorCode);
        var published = await service.PublishAsync(fixture.HostId, new(null, settings, fingerprint), default);
        Assert.True(published.IsSuccess, published.Message);
        var version = (await ProfileTuningService.LatestAsync(fixture.Db, default))!;
        Assert.Equal(100m, service.PlayerPolicy(version).ProfileNoiseCountMax);
        Assert.Equal(version.Version, service.TeamPolicy(version).NormalizationConfigVersion);
        Assert.Equal(ErrorCodes.Conflict,
            (await service.PublishAsync(fixture.HostId, new(null, settings, fingerprint), default)).ErrorCode);
    }
}
