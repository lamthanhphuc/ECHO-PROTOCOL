using System.Text.Json;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed partial class EconomyAdminService
{
    private static decimal? Percentile(IEnumerable<decimal> values, decimal percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? null : sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * percentile) - 1)];
    }

    private async Task<object> WalletTelemetryAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        const int playerLimit = 2000, eventLimit = 20000;
        var wallets = await db.Wallets.AsNoTracking().OrderByDescending(w => w.UpdatedAt).ThenBy(w => w.Id)
            .Take(playerLimit + 1).Select(w => new { w.Id, w.UserId, w.Balance, CreatedAt = w.User.CreatedAt }).ToListAsync(ct);
        bool playersTruncated = wallets.Count > playerLimit;
        if (playersTruncated) wallets.RemoveAt(playerLimit);
        var walletIds = wallets.Select(w => w.Id).ToArray();
        var users = wallets.Select(w => w.UserId).ToArray();
        var ledger = await db.WalletTransactions.AsNoTracking().Where(t => walletIds.Contains(t.WalletId) && t.CreatedAtUtc >= cutoff)
            .OrderByDescending(t => t.CreatedAtUtc).ThenBy(t => t.Id).Take(eventLimit + 1)
            .Select(t => new { t.WalletId, t.Type, t.Amount, t.BalanceBefore, t.BalanceAfter, t.CreatedAtUtc, t.ReferenceId,
                Category = t.PurchaseTransaction == null ? null : t.PurchaseTransaction.ShopItem.Category }).ToListAsync(ct);
        bool ledgerTruncated = ledger.Count > eventLimit;
        if (ledgerTruncated) ledger.RemoveAt(eventLimit);
        var grants = await db.MatchRewardGrants.AsNoTracking().Where(g => users.Contains(g.UserId) &&
                g.ProcessedAtUtc >= cutoff && g.ResultPlayer.MatchResult.Match.MatchDataPolicy == RemoteFixedBalanceContract.Standard)
            .OrderByDescending(g => g.ProcessedAtUtc).ThenBy(g => g.Id).Take(eventLimit + 1)
            .Select(g => new { g.UserId, g.WalletId, g.MatchId, g.CurrencyAmount, g.ExperiencePointsAwarded,
                g.ProcessedAtUtc, g.UnlockedItemsJson, g.ResultPlayer.MatchResult.DurationSeconds }).ToListAsync(ct);
        bool grantsTruncated = grants.Count > eventLimit;
        if (grantsTruncated) grants.RemoveAt(eventLimit);
        var adminXp = await db.AdminExperienceGrants.AsNoTracking().Where(g => users.Contains(g.UserId) && g.CreatedAtUtc >= cutoff)
            .OrderByDescending(g => g.CreatedAtUtc).Take(eventLimit + 1)
            .Select(g => new { g.UserId, g.Amount, g.CreatedAtUtc }).ToListAsync(ct);
        bool adminXpTruncated = adminXp.Count > eventLimit;
        if (adminXpTruncated) adminXp.RemoveAt(eventLimit);
        var adminHistory = adminXp.ToLookup(g => g.UserId);
        var rewardLedger = ledger.Where(t => t.Type == WalletTransactionType.MATCH_REWARD)
            .ToLookup(t => (t.WalletId, t.ReferenceId));
        var prices = await db.ShopItems.AsNoTracking().ToDictionaryAsync(i => i.ItemId, i => i.Price, ct);
        var ledgerHistory = ledger.ToLookup(t => t.WalletId);
        var grantHistory = grants.ToLookup(g => g.UserId);
        var unlocks = new List<object>();
        var unlockEvents = grants.Where(g => g.UnlockedItemsJson != "[]").ToArray();
        foreach (var grant in unlockEvents.Take(200))
        {
            var reward = rewardLedger[(grant.WalletId, grant.MatchId)].SingleOrDefault();
            if (reward is null) continue;
            var wallet = wallets.Single(w => w.UserId == grant.UserId);
            var ownLedger = ledgerHistory[wallet.Id].Where(t => t.CreatedAtUtc <= grant.ProcessedAtUtc).ToArray();
            bool adminExperience = adminHistory[grant.UserId].Any(g => g.CreatedAtUtc <= grant.ProcessedAtUtc);
            var firstLedger = ownLedger.OrderBy(t => t.CreatedAtUtc).FirstOrDefault();
            var historyComplete = wallet.CreatedAt >= cutoff && firstLedger?.BalanceBefore == 0 && !ledgerTruncated && !grantsTruncated;
            foreach (var item in JsonSerializer.Deserialize<LevelUnlockResponse[]>(grant.UnlockedItemsJson) ?? [])
            {
                int price = item.Price > 0 ? item.Price : prices.GetValueOrDefault(item.ItemId);
                var affordAt = ownLedger.Where(t => t.BalanceAfter >= price).Select(t => (DateTime?)t.CreatedAtUtc).Min();
                unlocks.Add(new { grant.UserId, item.ItemId, item.ItemName, item.RequiredLevel,
                    UnlockedAtUtc = grant.ProcessedAtUtc, Price = price, PriceWasSnapshotted = item.Price > 0,
                    WalletAtUnlock = reward.BalanceAfter,
                    AffordabilityRatio = price > 0 ? (decimal?)reward.BalanceAfter / price : null,
                    ExternalFundsBeforeUnlock = ownLedger.Any(t => t.Type is WalletTransactionType.ADMIN_GRANT or WalletTransactionType.PAYMENT_FULFILLMENT),
                    AdminExperienceBeforeUnlock = adminExperience,
                    HistoryComplete = historyComplete,
                    MinutesToUnlock = historyComplete && !adminExperience && !adminXpTruncated ? (decimal?)grantHistory[grant.UserId].Where(g =>
                        g.ProcessedAtUtc <= grant.ProcessedAtUtc).Sum(g => (decimal)g.DurationSeconds) / 60m : null,
                    UnlockGapWallMinutes = historyComplete && affordAt.HasValue
                        ? (decimal?)(grant.ProcessedAtUtc - affordAt.Value).TotalMinutes : null });
            }
        }
        var activity = grants.ToLookup(g => g.UserId);
        var cohorts = wallets.GroupBy(w => activity[w.UserId].Count() switch { < 5 => "0-4", < 20 => "5-19", _ => "20+" })
            .Select(group => new { MatchesInWindow = group.Key, Players = group.Count(),
                WalletP50 = Percentile(group.Select(w => (decimal)w.Balance), .5m),
                WalletP90 = Percentile(group.Select(w => (decimal)w.Balance), .9m),
                GameplayCredits = group.Sum(w => activity[w.UserId].Sum(g => (long)g.CurrencyAmount)),
                Purchases = ledger.Count(t => group.Any(w => w.Id == t.WalletId) && t.Type == WalletTransactionType.PURCHASE) }).ToArray();
        return new { Days = 30, PlayerLimit = playerLimit, EventLimit = eventLimit, Players = wallets.Count,
            PlayersTruncated = playersTruncated, LedgerTruncated = ledgerTruncated, GrantsTruncated = grantsTruncated,
            ExperienceSources = new { Gameplay = grants.Sum(g => g.ExperiencePointsAwarded),
                Admin = adminXp.Sum(g => (long)g.Amount), AdminGrants = adminXp.Count, AdminTruncated = adminXpTruncated },
            WalletP50 = Percentile(wallets.Select(w => (decimal)w.Balance), .5m),
            WalletP90 = Percentile(wallets.Select(w => (decimal)w.Balance), .9m),
            Flows = ledger.GroupBy(t => t.Type).Select(group => new { Source = group.Key.ToString(),
                Credits = group.Sum(t => (long)t.Amount), Transactions = group.Count() }).ToArray(),
            PurchasesByCategory = ledger.Where(t => t.Type == WalletTransactionType.PURCHASE)
                .GroupBy(t => t.Category ?? "UNKNOWN").Select(group => new { Category = group.Key,
                    CreditsSpent = -group.Sum(t => (long)t.Amount), Purchases = group.Count() }).ToArray(),
            Cohorts = cohorts, Unlocks = unlocks, UnlockEventsTruncated = unlockEvents.Length > 200,
            TimeBasis = "MinutesToUnlock sums match durations, including downed time, excluding lobby. UnlockGapWallMinutes includes offline/lobby time. Wallets are current, flows cover the sampled 30-day window." };
    }
}
