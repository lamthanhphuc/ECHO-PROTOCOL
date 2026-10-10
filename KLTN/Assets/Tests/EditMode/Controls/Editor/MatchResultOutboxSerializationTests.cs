using NUnit.Framework;
using UnityEngine;
using EchoProtocol.Networking.Authority;

public sealed class MatchResultOutboxSerializationTests
{
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
