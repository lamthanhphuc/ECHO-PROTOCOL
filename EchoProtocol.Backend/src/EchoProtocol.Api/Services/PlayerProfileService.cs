using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Player;
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

        return profile is null
            ? ServiceResult<PlayerProfileResponse>.Failure(
                "Player profile not found",
                ErrorCodes.PlayerProfileNotFound)
            : ServiceResult<PlayerProfileResponse>.Success(profile, "Player profile retrieved");
    }
}
