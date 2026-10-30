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
            WalletBalance = profile.WalletBalance,
            RecentMatch = await LatestAsync(activity),
            LatestWin = await LatestAsync(activity.Where(player => player.Survived
                && player.MatchResult.Outcome == MatchOutcome.WIN)),
            LatestNonSurvival = await LatestAsync(activity.Where(player => !player.Survived))
        }, "Player profile retrieved");
    }
}
