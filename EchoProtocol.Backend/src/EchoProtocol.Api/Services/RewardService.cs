using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Rewards;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class RewardService : IRewardService
{
    private const int MaximumPolicyVersionLength = 50;

    private readonly AppDbContext _db;
    private readonly IRewardPolicy _policy;
    private readonly IProgressionService _progressionService;
    private readonly TimeProvider _timeProvider;

    public RewardService(
        AppDbContext db,
        IRewardPolicy policy,
        IProgressionService progressionService,
        TimeProvider timeProvider)
    {
        _db = db;
        _policy = policy;
        _progressionService = progressionService;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<RewardProcessingResponse>> ProcessAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        if (!_policy.IsConfigured)
        {
            return Failure(
                "No approved reward policy is configured",
                ErrorCodes.RewardPolicyNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(_policy.Version) ||
            _policy.Version.Length > MaximumPolicyVersionLength)
        {
            return Failure("Reward policy version is invalid", ErrorCodes.RewardPolicyInvalid);
        }

        if (!_progressionService.IsConfigured)
        {
            return Failure(
                "No approved progression policy is configured",
                ErrorCodes.ProgressionPolicyNotConfigured);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await LoadResultForUpdateAsync(matchId, cancellationToken);
            if (result is null)
            {
                return Failure("Finalized match result not found", ErrorCodes.RewardResultNotFound);
            }

            await _db.Entry(result).Collection(item => item.Players).LoadAsync(cancellationToken);
            await _db.Entry(result).Reference(item => item.Match).LoadAsync(cancellationToken);

            if (result.RewardStatus == MatchRewardStatus.Completed)
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                return await LoadCompletedResponseAsync(matchId, isReplay: true, cancellationToken);
            }

            if (result.RewardStatus != MatchRewardStatus.Pending ||
                result.Match.Status != MatchAuthorityStatus.Ended ||
                result.PlayerCount != result.Players.Count ||
                result.Players.Count == 0)
            {
                return Failure(
                    "Match result is not valid for reward processing",
                    ErrorCodes.RewardInvalidResult);
            }

            IReadOnlyList<RewardAllocation> allocations;
            try
            {
                allocations = _policy.Calculate(CreateSnapshot(result));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Failure(
                    $"Reward policy rejected the stored result: {exception.Message}",
                    ErrorCodes.RewardPolicyInvalid);
            }

            var allocationFailure = ValidateAllocations(result.Players, allocations);
            if (allocationFailure is not null)
            {
                return Failure(allocationFailure, ErrorCodes.RewardPolicyInvalid);
            }

            var allocationsByUser = allocations.ToDictionary(item => item.UserId);
            var wallets = new Dictionary<Guid, Wallet>();
            foreach (var userId in result.Players.Select(item => item.UserId).Order())
            {
                var wallet = await LoadWalletForUpdateAsync(userId, cancellationToken);
                if (wallet is null)
                {
                    return Failure(
                        $"Wallet not found for result player {userId:D}",
                        ErrorCodes.RewardWalletNotFound);
                }

                wallets.Add(userId, wallet);
            }

            var profiles = new Dictionary<Guid, PlayerProfile>();
            foreach (var userId in result.Players.Select(item => item.UserId).Order())
            {
                var profile = await LoadProfileForUpdateAsync(userId, cancellationToken);
                if (profile is null)
                {
                    return Failure(
                        $"Player profile not found for result player {userId:D}",
                        ErrorCodes.ProgressionProfileNotFound);
                }

                profiles.Add(userId, profile);
            }

            var snapshot = CreateSnapshot(result);
            var progressionResult = _progressionService.Calculate(snapshot, profiles);
            if (!progressionResult.IsSuccess)
            {
                return Failure(progressionResult.Message, progressionResult.ErrorCode!);
            }

            var progressionByUser = progressionResult.Data!.ToDictionary(item => item.UserId);

            var balancesAfter = new Dictionary<Guid, int>();
            try
            {
                foreach (var (userId, wallet) in wallets)
                {
                    balancesAfter[userId] = checked(
                        wallet.Balance + allocationsByUser[userId].CurrencyAmount);
                }
            }
            catch (OverflowException)
            {
                return Failure("Reward would overflow a wallet balance", ErrorCodes.RewardPolicyInvalid);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var responseGrants = new List<RewardGrantResponse>(result.Players.Count);
            foreach (var userId in wallets.Keys.Order())
            {
                var wallet = wallets[userId];
                var amount = allocationsByUser[userId].CurrencyAmount;
                var balanceBefore = wallet.Balance;
                var balanceAfter = balancesAfter[userId];
                var progression = progressionByUser[userId];

                _db.MatchRewardGrants.Add(new MatchRewardGrant
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    UserId = userId,
                    WalletId = wallet.Id,
                    CurrencyAmount = amount,
                    PolicyVersion = _policy.Version,
                    ExperiencePointsAwarded = progression.ExperiencePointsAwarded,
                    ProgressionPolicyVersion = _progressionService.PolicyVersion,
                    ProcessedAtUtc = now
                });
                _db.WalletTransactions.Add(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    Type = WalletTransactionType.MATCH_REWARD,
                    Amount = amount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ReferenceId = matchId,
                    Description = $"Match reward ({_policy.Version})",
                    CreatedAtUtc = now
                });

                wallet.Balance = balanceAfter;
                wallet.UpdatedAt = now;
                var profile = profiles[userId];
                profile.TotalMatches = progression.TotalMatches;
                profile.TotalWins = progression.TotalWins;
                profile.ExperiencePoints = progression.ExperiencePoints;
                profile.Level = progression.Level;
                profile.UpdatedAt = now;
                responseGrants.Add(new RewardGrantResponse
                {
                    UserId = userId,
                    CurrencyAmount = amount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter
                });
            }

            result.RewardStatus = MatchRewardStatus.Completed;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<RewardProcessingResponse>.Success(new RewardProcessingResponse
            {
                MatchId = matchId,
                RewardStatus = MatchRewardStatus.Completed,
                PolicyVersion = _policy.Version,
                IsReplay = false,
                ProcessedAtUtc = now,
                Grants = responseGrants
            }, "Match rewards processed");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            var replay = await LoadCompletedResponseAsync(matchId, isReplay: true, cancellationToken);
            return replay.IsSuccess
                ? replay
                : Failure(
                    "A conflicting reward ledger entry already exists",
                    ErrorCodes.RewardConflict);
        }
    }

    private async Task<MatchResult?> LoadResultForUpdateAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.MatchResults
                .FromSqlInterpolated(
                    $"SELECT * FROM \"MatchResults\" WHERE \"MatchId\" = {matchId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.MatchResults.SingleOrDefaultAsync(
            item => item.MatchId == matchId,
            cancellationToken);
    }

    private async Task<Wallet?> LoadWalletForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.Wallets
                .FromSqlInterpolated(
                    $"SELECT * FROM \"Wallets\" WHERE \"UserId\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.Wallets.SingleOrDefaultAsync(
            item => item.UserId == userId,
            cancellationToken);
    }

    private async Task<PlayerProfile?> LoadProfileForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.PlayerProfiles
                .FromSqlInterpolated(
                    $"SELECT * FROM \"PlayerProfiles\" WHERE \"UserId\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.PlayerProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId,
            cancellationToken);
    }

    private async Task<ServiceResult<RewardProcessingResponse>> LoadCompletedResponseAsync(
        Guid matchId,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var result = await _db.MatchResults.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (result is null)
        {
            return Failure("Finalized match result not found", ErrorCodes.RewardResultNotFound);
        }

        if (result.RewardStatus != MatchRewardStatus.Completed)
        {
            return Failure(
                "Reward processing did not complete",
                ErrorCodes.RewardConflict);
        }

        var grants = await _db.MatchRewardGrants.AsNoTracking()
            .Where(item => item.MatchId == matchId)
            .OrderBy(item => item.UserId)
            .ToListAsync(cancellationToken);
        var walletIds = grants.Select(item => item.WalletId).ToArray();
        var ledger = await _db.WalletTransactions.AsNoTracking()
            .Where(item => walletIds.Contains(item.WalletId) &&
                           item.Type == WalletTransactionType.MATCH_REWARD &&
                           item.ReferenceId == matchId)
            .ToDictionaryAsync(item => item.WalletId, cancellationToken);

        if (grants.Count != result.PlayerCount ||
            grants.Any(item => !ledger.ContainsKey(item.WalletId)))
        {
            return Failure(
                "Completed reward data is incomplete",
                ErrorCodes.RewardConflict);
        }

        return ServiceResult<RewardProcessingResponse>.Success(new RewardProcessingResponse
        {
            MatchId = matchId,
            RewardStatus = MatchRewardStatus.Completed,
            PolicyVersion = grants.Select(item => item.PolicyVersion).Distinct().Single(),
            IsReplay = isReplay,
            ProcessedAtUtc = grants.Max(item => item.ProcessedAtUtc),
            Grants = grants.Select(grant =>
            {
                var entry = ledger[grant.WalletId];
                return new RewardGrantResponse
                {
                    UserId = grant.UserId,
                    CurrencyAmount = grant.CurrencyAmount,
                    BalanceBefore = entry.BalanceBefore,
                    BalanceAfter = entry.BalanceAfter
                };
            }).ToArray()
        }, "Match rewards already processed");
    }

    private static RewardMatchSnapshot CreateSnapshot(MatchResult result) => new(
        result.MatchId,
        result.Outcome,
        result.ObjectiveCompletion,
        result.DurationSeconds,
        result.Players.OrderBy(item => item.UserId).Select(item => new RewardPlayerSnapshot(
            item.UserId,
            item.Survived,
            item.Disconnected,
            item.DetectionCount,
            item.DownedCount,
            item.ReviveCount,
            item.ObjectiveContribution)).ToArray());

    private static string? ValidateAllocations(
        ICollection<MatchResultPlayer> players,
        IReadOnlyList<RewardAllocation>? allocations)
    {
        if (allocations is null || allocations.Count != players.Count)
        {
            return "Reward policy must return exactly one allocation per result player";
        }

        if (allocations.Any(item => item.UserId == Guid.Empty || item.CurrencyAmount < 0) ||
            allocations.Select(item => item.UserId).Distinct().Count() != allocations.Count)
        {
            return "Reward policy returned an invalid or duplicate allocation";
        }

        var playerIds = players.Select(item => item.UserId).ToHashSet();
        return allocations.Any(item => !playerIds.Contains(item.UserId))
            ? "Reward policy returned an allocation for a user outside the stored result"
            : null;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static ServiceResult<RewardProcessingResponse> Failure(string message, string code) =>
        ServiceResult<RewardProcessingResponse>.Failure(message, code);
}
