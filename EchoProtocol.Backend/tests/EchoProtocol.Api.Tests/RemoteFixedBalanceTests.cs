using EchoProtocol.Api.Services;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RemoteFixedBalanceTests
{
    public const string ValidSnapshot =
        """
        {
          "schemaVersion": "RFB-1",
          "player": { "maxHealth": 100, "walkSpeed": 4, "sprintSpeed": 7, "maxStamina": 100 },
          "stalker": { "patrolSpeed": 6, "chaseSpeed": 7 },
          "minion": { "roamSpeed": 5.5, "trackSpeed": 8, "attackDamage": 20 }
        }
        """;

    [Fact] public void ValidSnapshot_IsAccepted() => Assert.True(RemoteFixedBalanceContract.IsValidSnapshot(ValidSnapshot));
    [Fact] public void InvalidJson_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot("{invalid}"));

    [Fact]
    public void UnknownField_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace("\"maxHealth\": 100", "\"maxHealth\": 100, \"unknown\": 1")));

    [Fact]
    public void OutOfRangeValue_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace("\"maxHealth\": 100", "\"maxHealth\": -1")));

    [Fact]
    public void HashChangesWhenPayloadChanges() => Assert.NotEqual(
        RemoteFixedBalanceContract.ComputeHash(ValidSnapshot),
        RemoteFixedBalanceContract.ComputeHash(ValidSnapshot.Replace("\"maxHealth\": 100", "\"maxHealth\": 101")));

    [Fact]
    public void HashIsLowercaseSha256()
    {
        var hash = RemoteFixedBalanceContract.ComputeHash(ValidSnapshot);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.All(hash, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void DuplicateProperty_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace("\"maxHealth\": 100,", "\"maxHealth\": 100, \"maxHealth\": 100,")));

    [Fact]
    public void MissingProperty_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace(", \"maxStamina\": 100", "")));

    [Fact]
    public void WrongSchema_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace("RFB-1", "RFB-2")));

    [Fact]
    public void OversizedSnapshot_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot + new string(' ', 8193)));

    [Fact]
    public void SprintBelowWalk_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace("\"sprintSpeed\": 7", "\"sprintSpeed\": 3")));
}
