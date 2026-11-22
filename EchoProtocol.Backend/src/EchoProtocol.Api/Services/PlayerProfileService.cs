using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class PlayerProfileService : IPlayerProfileService
{
    private readonly AppDbContext _db;

    public PlayerProfileService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<PlayerProfileResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.PlayerProfiles.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new PlayerProfileResponse
            {
                UserId = item.UserId,
                DisplayName = item.DisplayName,
                TotalMatches = item.TotalMatches,
                TotalWins = item.TotalWins,
                ExperiencePoints = item.ExperiencePoints,
                Level = item.Level,
                LifetimeExperiencePoints = item.LifetimeExperiencePoints,
                LevelCurveVersion = item.LevelCurveVersion,
                WalletBalance = item.User.Wallet!.Balance
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
            return ServiceResult<PlayerProfileResponse>.Failure(
                "Player profile not found", ErrorCodes.PlayerProfileNotFound);

        var activity = _db.MatchResultPlayers.AsNoTracking()
            .Where(player => player.UserId == userId);
        async Task<PlayerActivityResponse?> LatestAsync(
            IQueryable<MatchResultPlayer> source) =>
            await source.OrderByDescending(player => player.MatchResult.EndedAtUtc)
                .ThenByDescending(player => player.MatchId)
                .Select(player => new PlayerActivityResponse
                {
                    MatchId = player.MatchId,
                    EndedAtUtc = player.MatchResult.EndedAtUtc,
                    MapId = player.MatchResult.Match.MapId,
                    Outcome = player.MatchResult.Outcome.ToString(),
                    DurationSeconds = player.MatchResult.DurationSeconds
                })
                .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<PlayerProfileResponse>.Success(new PlayerProfileResponse
        {
            UserId = profile.UserId,
            DisplayName = profile.DisplayName,
            TotalMatches = profile.TotalMatches,
            TotalWins = profile.TotalWins,
            ExperiencePoints = profile.ExperiencePoints,
            Level = profile.Level,
            LifetimeExperiencePoints = profile.LifetimeExperiencePoints,
            LevelCurveVersion = profile.LevelCurveVersion,
            LevelCap = profile.LevelCurveVersion == ProgressionCurveV3.Version ? ProgressionCurveV3.LevelCap : 0,
            CosmeticRewards = await ProgressionCosmeticService.ReadAsync(_db, userId, cancellationToken),
            LevelUnlocks = await LevelUnlockService.ReadAsync(_db, userId, profile.Level,
                profile.ExperiencePoints, profile.LevelCurveVersion, cancellationToken),
            ExperienceInLevel = profile.LevelCurveVersion == ProgressionCurveV3.Version
                ? (profile.Level >= ProgressionCurveV3.LevelCap ? 0 : profile.ExperiencePoints - ProgressionCurveV3.Threshold(profile.Level))
                : profile.ExperiencePoints % 500,
            ExperienceRequiredForNextLevel = profile.LevelCurveVersion == ProgressionCurveV3.Version
                ? (profile.Level >= ProgressionCurveV3.LevelCap ? 0 : ProgressionCurveV3.RequiredForNextLevel(profile.Level)) : 500,
            WalletBalance = profile.WalletBalance,
            RecentMatch = await LatestAsync(activity),
            LatestWin = await LatestAsync(activity.Where(player =>
                player.MatchResult.Outcome == MatchOutcome.WIN)),
            LatestNonSurvival = await LatestAsync(activity.Where(player => !player.Survived))
        }, "Player profile retrieved");
    }
}
