using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MatchJoinProofServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_ValidPayload_RoundTrips()
    {
        var service = CreateService("proof-key-that-is-at-least-32-bytes-long", Now);
        var expected = new MatchJoinProofPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "room-a", Now.AddMinutes(1).UtcDateTime);

        var proof = service.Issue(expected);

        Assert.True(service.TryValidate(proof, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryValidate_TamperedPayload_IsRejected()
    {
        var service = CreateService("proof-key-that-is-at-least-32-bytes-long", Now);
        var proof = service.Issue(new MatchJoinProofPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "room-a", Now.AddMinutes(1).UtcDateTime));
        var tampered = (proof[0] == 'A' ? "B" : "A") + proof[1..];

        Assert.False(service.TryValidate(tampered, out _));
    }

    [Fact]
    public void TryValidate_ExpiredProof_IsRejected()
    {
        var service = CreateService("proof-key-that-is-at-least-32-bytes-long", Now);
        var proof = service.Issue(new MatchJoinProofPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "room-a", Now.AddSeconds(-1).UtcDateTime));

        Assert.False(service.TryValidate(proof, out _));
    }

    [Fact]
    public void TryValidate_ProofSignedByDifferentKey_IsRejected()
    {
        var issuer = CreateService("first-proof-key-that-is-over-32-bytes", Now);
        var validator = CreateService("second-proof-key-that-is-over-32-bytes", Now);
        var proof = issuer.Issue(new MatchJoinProofPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "room-a", Now.AddMinutes(1).UtcDateTime));

        Assert.False(validator.TryValidate(proof, out _));
    }

    private static MatchJoinProofService CreateService(string key, DateTimeOffset now) => new(
        Options.Create(new MatchAuthoritySettings { ProofSigningKey = key }),
        new FixedTimeProvider(now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
