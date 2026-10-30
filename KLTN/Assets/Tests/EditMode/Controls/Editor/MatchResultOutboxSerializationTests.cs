using NUnit.Framework;
using UnityEngine;
using EchoProtocol.Networking.Authority;

public sealed class MatchResultOutboxSerializationTests
{
    [Test]
    public void RecoveryCannotExportOrRetryAnotherAccountsResult()
    {
        var record = new PendingMatchResultRecord { ownerId = "a", rejected = true,
            request = new SubmitMatchResultRequestDto() };
        Assert.That(MatchAuthorityRuntime.CanRecoverResult(record, "a"), Is.True);
        Assert.That(MatchAuthorityRuntime.CanRecoverResult(record, "b"), Is.False);
        Assert.That(MatchAuthorityRuntime.CanRecoverResult(record, ""), Is.False);
        record.saved = true;
        Assert.That(MatchAuthorityRuntime.CanRecoverResult(record, "a"), Is.False);
    }
    [TestCase(401, "TOKEN_EXPIRED", true, false)]
    [TestCase(429, "RATE_LIMITED", true, false)]
    [TestCase(503, "UNAVAILABLE", true, false)]
    [TestCase(410, "MATCH_LEASE_EXPIRED", true, true)]
    [TestCase(400, "MATCH_RESULT_INVALID_DURATION", true, false)]
    [TestCase(400, "MATCH_RESULT_INVALID_DURATION", false, true)]
    public void TransientFailuresRemainRetryable(long status, string code, bool durationCanRetry, bool permanent)
    {
        Assert.That(MatchAuthorityRuntime.IsPermanentResultFailure(status, code, durationCanRetry), Is.EqualTo(permanent));
    }

    [Test]
    public void RestartRetainsRetryScheduleAndSavedReceiptWithoutPayload()
    {
        var queue = new PendingMatchResultQueue();
        queue.items.Add(new PendingMatchResultRecord { matchId = "saved", ownerId = "host-a", saved = true });
        queue.items.Add(new PendingMatchResultRecord { matchId = "pending", ownerId = "host-b", nextAttemptUtc = 1234,
            attempts = 3, lastErrorCode = "UNAVAILABLE" });
        var restored = JsonUtility.FromJson<PendingMatchResultQueue>(JsonUtility.ToJson(queue));
        Assert.That(restored.items[0].saved, Is.True);
        Assert.That(restored.items[0].request, Is.Null);
        Assert.That(restored.items[1].ownerId, Is.EqualTo("host-b"));
        Assert.That(restored.items[1].nextAttemptUtc, Is.EqualTo(1234));
        Assert.That(restored.items[1].attempts, Is.EqualTo(3));
    }
    [Test]
    public void RestartPayload_PreservesHostRosterAndResult()
    {
        var queue = new PendingMatchResultQueue();
        queue.items.Add(new PendingMatchResultRecord { matchId = "match-a", ownerId = "host-a",
            notBeforeUtc = 123456, nextLeaseUtc = 123450,
            request = new SubmitMatchResultRequestDto { outcome = "WIN", objectiveCompletion = 0.75f,
                players = new[] { new SubmitMatchResultPlayerDto { userId = "player-a", survived = true,
                    downedCount = 2, reviveCount = 3, objectiveContribution = 1 } } } });
        var restored = JsonUtility.FromJson<PendingMatchResultQueue>(JsonUtility.ToJson(queue));
        Assert.That(restored.items.Count, Is.EqualTo(1));
        var record = restored.items[0];
        Assert.That(record.ownerId, Is.EqualTo("host-a"));
        Assert.That(record.matchId, Is.EqualTo("match-a"));
        Assert.That(record.notBeforeUtc, Is.EqualTo(123456));
        Assert.That(record.request.outcome, Is.EqualTo("WIN"));
        Assert.That(record.request.objectiveCompletion, Is.EqualTo(0.75f));
        Assert.That(record.request.players[0].survived, Is.True);
        Assert.That(record.request.players[0].downedCount, Is.EqualTo(2));
        Assert.That(record.request.players[0].reviveCount, Is.EqualTo(3));
    }

    [Test]
    public void RejectedRecord_IsRetainedAlongsideAnotherMatch()
    {
        var queue = new PendingMatchResultQueue();
        queue.items.Add(new PendingMatchResultRecord { matchId = "old", ownerId = "host-a", rejected = true });
        queue.items.Add(new PendingMatchResultRecord { matchId = "new", ownerId = "host-b" });
        var restored = JsonUtility.FromJson<PendingMatchResultQueue>(JsonUtility.ToJson(queue));
        Assert.That(restored.items.Count, Is.EqualTo(2));
        Assert.That(restored.items[0].rejected, Is.True);
        Assert.That(restored.items[1].ownerId, Is.EqualTo("host-b"));
    }
}
