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
          "minion": { "roamSpeed": 5.5, "trackSpeed": 8, "attackDamage": 10 }
        }
        """;

    [Theory]
    [InlineData("0.1.0", 0)]
    [InlineData("0.1.1", 3)]
    public void AdaptiveBasisKeepsZoneThreeCapForTheLockedGameVersion(string gameVersion, int expected)
    {
        Assert.Equal(expected, RemoteFixedBalanceContract.AdaptiveBasis(null, gameVersion).Values[8]);
        Assert.Equal(expected, RemoteFixedBalanceContract.AdaptiveBasis(ValidSnapshot, gameVersion).Values[8]);
    }

    [Fact]
    public void AdaptiveBasisPreservesExplicitWebZoneThreeCap()
    {
        const string snapshot = """
            {"schemaVersion":"RFB-2","stalker":{"patrolSpeed":6,"chaseSpeed":7},
             "gameplay":[{"key":"minionCapZone3","value":0}]}
            """;
        Assert.Equal(0, RemoteFixedBalanceContract.AdaptiveBasis(snapshot, "0.1.1").Values[8]);
    }

    [Fact] public void ValidSnapshot_IsAccepted() => Assert.True(RemoteFixedBalanceContract.IsValidSnapshot(ValidSnapshot));
    [Fact]
    public void OldMinionDamageRemainsReadableButCannotBePublished()
    {
        var legacy = ValidSnapshot.Replace("\"attackDamage\": 10", "\"attackDamage\": 20");
        Assert.True(RemoteFixedBalanceContract.IsValidSnapshot(legacy));
        Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(legacy, allowRetiredFields: false));
        Assert.True(RemoteFixedBalanceContract.IsValidSnapshot(ValidSnapshot, allowRetiredFields: false));
    }

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

    [Theory]
    [InlineData("SciFi", "[{\"key\":\"convoySpeed\",\"value\":9},{\"key\":\"staminaDrain\",\"value\":22}]", true)]
    [InlineData("Scene_A", "[{\"key\":\"convoySpeed\",\"value\":9}]", false)]
    [InlineData("Scene_A", "[{\"key\":\"purgeSpawnCooldown\",\"value\":22}]", true)]
    [InlineData("SciFi", "[{\"key\":\"convoySpeed\",\"value\":20}]", false)]
    [InlineData("SciFi", "[{\"key\":\"convoyFuelCapacity\",\"value\":2.5}]", false)]
    [InlineData("SciFi", "[{\"key\":\"inventorySlots\",\"value\":2},{\"key\":\"inventorySlots\",\"value\":1}]", false)]
    [InlineData("SciFi", "[{\"key\":\"relayRepairWindowSeconds\",\"value\":75},{\"key\":\"terminalHoldSeconds\",\"value\":90}]", false)]
    public void GameplaySchema_ValidatesMapRangesAndCrossFieldSafety(string map, string gameplay, bool valid)
    {
        var json = ValidSnapshot.Replace("RFB-1", "RFB-2").TrimEnd();
        json = json[..^1] + ",\"gameplay\":" + gameplay + "}";
        Assert.Equal(valid, RemoteFixedBalanceContract.IsValidSnapshot(json, map));
    }

    [Theory]
    [InlineData("stalkerHearing", 1)]
    [InlineData("listenerHearing", 1)]
    [InlineData("doorTravelSeconds", 1)]
    [InlineData("toolsPerZone", 5)]
    [InlineData("zone1ToolBudget", 5)]
    [InlineData("zone2ToolBudget", 5)]
    [InlineData("zone3ToolBudget", 5)]
    public void RetiredGameplayField_IsAllowedOnlyForExistingSnapshots(string key, int value)
    {
        var json = ValidSnapshot.Replace("RFB-1", "RFB-2").TrimEnd()[..^1] +
            $",\"gameplay\":[{{\"key\":\"{key}\",\"value\":{value}}}]}}";

        Assert.True(RemoteFixedBalanceContract.IsValidSnapshot(json, "SciFi"));
        Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(json, "SciFi", allowRetiredFields: false));
        Assert.DoesNotContain(RemoteGameplayTuning.EditableFields, field => field.Key == key);
    }

    [Theory]
    [InlineData("Scene_B", "PURGE", "LIZARD", "SCENE_B_AUTHORED", true)]
    [InlineData("Scene_B", "EXPEDITION", "LIZARD", "SCENE_B_AUTHORED", false)]
    [InlineData("SciFi", "EXPEDITION", "LIZARD", "DEFAULT_ROUTE", false)]
    [InlineData("SciFi", "EXPEDITION", "STALKER", "DEFAULT_ROUTE", true)]
    public void ContentVariant_RequiresRegisteredMapModeMonsterAndRoute(string map, string mode, string monster, string route, bool valid)
    {
        var json = ValidSnapshot.Replace("RFB-1", "RFB-2").TrimEnd()[..^1] +
            $",\"gameplay\":[],\"content\":{{\"gameVersion\":\"0.1.0\",\"mode\":\"{mode}\",\"monster\":\"{monster}\",\"route\":\"{route}\"}}}}";
        Assert.Equal(valid, RemoteFixedBalanceContract.IsValidSnapshot(json, map));
        if (valid) Assert.Equal("0.1.0", RemoteFixedBalanceContract.GameVersion(json));
    }

    [Fact]
    public void OversizedSnapshot_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot + new string(' ', 8193)));

    [Fact]
    public void SprintBelowWalk_IsRejected() => Assert.False(RemoteFixedBalanceContract.IsValidSnapshot(
        ValidSnapshot.Replace("\"sprintSpeed\": 7", "\"sprintSpeed\": 3")));
}
