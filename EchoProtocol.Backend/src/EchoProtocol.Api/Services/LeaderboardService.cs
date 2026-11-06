using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Leaderboards;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _db;

    public LeaderboardService(AppDbContext db) => _db = db;

    public async Task<LeaderboardPageResponse> GetAsync(
        LeaderboardMode mode,
        string mapId,
        string difficulty,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // A result is written only by the bound Host after server-side roster, lease,
        // state, and duration validation. Legacy results lack start context and cannot
        // be assigned to a map/difficulty ranking safely.
        var eligible = _db.MatchResults.AsNoTracking().Where(result =>
            result.Outcome == MatchOutcome.WIN
            && result.ObjectiveCompletion == 1m
            && result.Match.MapId == mapId
            && result.Match.Difficulty == difficulty
            && result.Match.MatchDataPolicy == RemoteFixedBalanceContract.Standard);

        var candidates = eligible.Where(result =>
            result.RosterKey != null
            && result.Players.Any(player => player.Survived && !player.Disconnected));
        candidates = mode == LeaderboardMode.SOLO
            ? candidates.Where(result => result.PlayerCount == 1)
            : candidates.Where(result => result.PlayerCount >= 2);

        // RosterKey is built by the server from the validated result roster. For a
        // solo run it contains the single user ID; for a team it contains all IDs.
        var bestMatchIds = candidates
            .GroupBy(result => result.RosterKey)
            .Select(group => group
                .OrderBy(result => result.DurationSeconds)
                .ThenBy(result => result.EndedAtUtc)
                .ThenBy(result => result.MatchId)
                .Select(result => result.MatchId)
                .First());
        var ranked = eligible.Where(result => bestMatchIds.Contains(result.MatchId));

        var totalCount = await ranked.CountAsync(cancellationToken);
        var offset = (page - 1) * pageSize;
        var rows = await ranked
            .OrderBy(result => result.DurationSeconds)
            .ThenBy(result => result.EndedAtUtc)
            .ThenBy(result => result.MatchId)
            .Skip(offset)
            .Take(pageSize)
            .Include(result => result.Players)
            .ToListAsync(cancellationToken);

        var userIds = rows.SelectMany(result => result.Players)
            .Select(player => player.UserId).Distinct().ToArray();
        var names = await _db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.Username,
                ProfileName = user.PlayerProfile == null
                    ? null
                    : user.PlayerProfile.DisplayName
            })
            .ToDictionaryAsync(
                user => user.Id,
                user => string.IsNullOrWhiteSpace(user.ProfileName)
                    ? user.Username
                    : user.ProfileName,
                cancellationToken);

        var items = rows.Select((result, index) =>
        {
            var players = result.Players
                .Select(player => new LeaderboardPlayerResponse
                {
                    UserId = player.UserId,
                    DisplayName = names.GetValueOrDefault(player.UserId, "UNKNOWN")
                })
                .OrderBy(player => player.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(player => player.UserId)
                .ToArray();
            var squadLabel = mode == LeaderboardMode.TEAM
                ? $"SQUAD {Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(result.RosterKey!)))[..8]}"
                : null;
            return new LeaderboardEntryResponse
            {
                Rank = offset + index + 1,
                MatchId = result.MatchId,
                DisplayName = squadLabel ?? players[0].DisplayName,
                SquadLabel = squadLabel,
                DurationSeconds = result.DurationSeconds,
                DurationCentiseconds = result.DurationSeconds * 100,
                Players = players
            };
        }).ToArray();

        return new LeaderboardPageResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Maps = LeaderboardCatalog.Maps,
            Difficulties = LeaderboardCatalog.Difficulties
        };
    }
}
