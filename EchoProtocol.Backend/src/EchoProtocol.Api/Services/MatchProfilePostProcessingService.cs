using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class MatchProfilePostProcessingService(AppDbContext db, TimeProvider clock)
    : IMatchProfilePostProcessingService
{
    public async Task EnqueueAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (matchId == Guid.Empty) return;
        if (await db.MatchProfileProcessingJobs.AnyAsync(job => job.MatchId == matchId, cancellationToken)) return;
        db.MatchProfileProcessingJobs.Add(new MatchProfileProcessingJob
        {
            MatchId = matchId,
            Status = "PENDING",
            NextAttemptAtUtc = clock.GetUtcNow().UtcDateTime
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            var entry = db.Entry(db.MatchProfileProcessingJobs.Local.Single(x => x.MatchId == matchId));
            entry.State = EntityState.Detached;
            if (!await db.MatchProfileProcessingJobs.AnyAsync(job => job.MatchId == matchId, cancellationToken))
                throw;
        }
    }
}
