using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class MatchProfilePostProcessingWorker(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<MatchProfilePostProcessingWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOneAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Match profile post-processing poll failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOneAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;

        var missing = await db.MatchResults.AsNoTracking()
            .Where(result => !db.MatchProfileProcessingJobs.Any(job => job.MatchId == result.MatchId))
            .OrderBy(result => result.SubmittedAtUtc).Select(result => result.MatchId)
            .Take(10).ToListAsync(ct);
        foreach (var matchId in missing)
            db.MatchProfileProcessingJobs.Add(new MatchProfileProcessingJob { MatchId = matchId, NextAttemptAtUtc = now });
        if (missing.Count > 0) await db.SaveChangesAsync(ct);

        var job = await db.MatchProfileProcessingJobs.AsNoTracking()
            .Where(item => item.Status == "PENDING" && item.NextAttemptAtUtc <= now
                || item.Status == "PROCESSING" && item.LeaseExpiresAtUtc < now)
            .OrderBy(item => item.NextAttemptAtUtc).FirstOrDefaultAsync(ct);
        if (job is null) return;
        var leaseUntil = now.AddMinutes(2);
        var claimed = await db.MatchProfileProcessingJobs
            .Where(item => item.MatchId == job.MatchId
                && (item.Status == "PENDING" && item.NextAttemptAtUtc <= now
                    || item.Status == "PROCESSING" && item.LeaseExpiresAtUtc < now))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "PROCESSING")
                .SetProperty(item => item.Attempts, item => item.Attempts + 1)
                .SetProperty(item => item.LeaseExpiresAtUtc, leaseUntil), ct);
        if (claimed == 0) return;

        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITelemetryEventRepository>();
            var result = await db.MatchResults.AsNoTracking().SingleOrDefaultAsync(item => item.MatchId == job.MatchId, ct);
            var match = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(item => item.MatchId == job.MatchId, ct);
            var events = await repository.LoadAcceptedMatchEventsAsync(job.MatchId, ct);
            if (result is null || match?.Status != MatchAuthorityStatus.Ended
                || events.Count(item => item.EventType == "MATCH_ENDED") != 1)
            {
                await RetryAsync(db, job.MatchId, job.Attempts + 1, "MATCH_OR_TELEMETRY_PENDING", ct);
                return;
            }

            var updater = scope.ServiceProvider.GetRequiredService<IPlayerAIProfileUpdater>();
            var users = await db.MatchPlayerBindings.AsNoTracking().Where(item => item.MatchId == job.MatchId)
                .Select(item => item.UserId).Distinct().OrderBy(userId => userId).ToListAsync(ct);
            foreach (var userId in users)
            {
                var updated = await updater.ProcessAsync(job.MatchId, userId, ct);
                if (!updated.IsSuccess)
                {
                    await RetryAsync(db, job.MatchId, job.Attempts + 1, updated.ErrorCode ?? "PLAYER_PROFILE_PENDING", ct);
                    return;
                }
            }
            var team = await scope.ServiceProvider.GetRequiredService<ITeamProfileService>().ProcessAsync(job.MatchId, ct);
            if (!team.IsSuccess || team.Data?.ProcessingStatus == TeamProfileProcessingStatus.Pending)
            {
                await RetryAsync(db, job.MatchId, job.Attempts + 1, team.ErrorCode ?? "TEAM_PROFILE_PENDING", ct);
                return;
            }
            await db.MatchProfileProcessingJobs.Where(item => item.MatchId == job.MatchId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "COMPLETED")
                    .SetProperty(item => item.LeaseExpiresAtUtc, (DateTime?)null)
                    .SetProperty(item => item.LastError, string.Empty), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Profile post-processing failed for match {MatchId}", job.MatchId);
            await RetryAsync(db, job.MatchId, job.Attempts + 1, exception.GetType().Name, ct);
        }
    }

    private async Task RetryAsync(AppDbContext db, Guid matchId, int attempts, string error, CancellationToken ct)
    {
        // shortcut: retry ends after 20 attempts, add an operator replay path before raising this ceiling.
        var dead = attempts >= 20;
        await db.MatchProfileProcessingJobs.Where(item => item.MatchId == matchId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, dead ? "FAILED" : "PENDING")
                .SetProperty(item => item.NextAttemptAtUtc, clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(300, attempts * 5)))
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(item => item.LastError, error), ct);
    }
}
