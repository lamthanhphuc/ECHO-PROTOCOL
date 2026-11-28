using System;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

public sealed class LobbyEntitlementTests
{
    [Test]
    public void MatchEntitlementsResponseDeserializesPurchasedJammo()
    {
        var userId = Guid.NewGuid().ToString("D");
        var json = "{\"success\":true,\"data\":{\"userId\":\"" + userId
                   + "\",\"shopItemIds\":[\"" + CharacterShopAssets.JammoItemId + "\"]}}";

        var response = JsonUtility.FromJson<ApiResponse<MatchPlayerEntitlementsDto>>(json);

        Assert.That(response.success, Is.True);
        Assert.That(response.data.userId, Is.EqualTo(userId));
        Assert.That(response.data.shopItemIds, Does.Contain(CharacterShopAssets.JammoItemId));
    }

    [TearDown]
    public void TearDown()
    {
        AuthSession.Clear();
        TeamToolOwnershipSession.Clear();
    }

    [Test]
    public void PaidSelectionsRequireBackendInventoryIds()
    {
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Character, 0, 0, out var freeCharacter), Is.True);
        Assert.That(freeCharacter, Is.Empty);
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Character, 0, 1, out var paidCharacter), Is.True);
        Assert.That(paidCharacter, Is.EqualTo(new[] { CharacterShopAssets.JammoItemId }));
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.CharacterOutfit, 1, 1, out var paidOutfit), Is.True);
        Assert.That(paidOutfit.Length, Is.EqualTo(2));
        Assert.That(paidOutfit[0], Is.EqualTo(CharacterShopAssets.JammoItemId));
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Tool, 0, LobbyPlayerState.NoiseMakerToolId, out var paidTool), Is.True);
        Assert.That(paidTool, Is.EqualTo(new[] { TeamToolShopAssets.Items[1].ItemId }));
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Tool, 0, 0, out var emptyTool), Is.True);
        Assert.That(emptyTool, Is.Empty);
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Tool, 0, 99, out _), Is.False);
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Pet, 0, 0, out var unequippedPet), Is.True);
        Assert.That(unequippedPet, Is.Empty);
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.Pet, 0, 1, out var paidPet), Is.True);
        Assert.That(paidPet, Is.EqualTo(new[] { PetShopAssets.Pets[0].ItemId }));
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.PetSkin, 1, 0, out var defaultSkin), Is.True);
        Assert.That(defaultSkin, Is.EqualTo(new[] { PetShopAssets.Pets[0].ItemId }));
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.PetSkin, 1, 1, out var paidSkin), Is.True);
        Assert.That(paidSkin, Is.EqualTo(new[]
        {
            PetShopAssets.Pets[0].ItemId,
            PetShopAssets.Skins[1].ItemId
        }));
        Assert.That(LobbySelectionEntitlements.TryGetRequiredItems(
            LobbySelectionKind.PetSkin, 1, 99, out _), Is.False);
    }

    [Test]
    public void PetSelectionCompletionGuardRejectsNewerRequestsChangedIdentityAndLobbyStart()
    {
        Assert.That(IsCurrentPetRequest(), Is.True);
        Assert.That(IsCurrentPetRequest(requestSerial: 4), Is.False,
            "A newer selection of the same kind invalidates an older completion.");
        Assert.That(IsCurrentPetRequest(relatedSerial: 8), Is.False,
            "A newer selection of the related pet/skin kind invalidates the completion.");
        Assert.That(IsCurrentPetRequest(currentUserId: "different-user"), Is.False);
        Assert.That(IsCurrentPetRequest(currentMatchId: "different-match"), Is.False);
        Assert.That(IsCurrentPetRequest(matchStartPending: true), Is.False);

        Assert.That(IsCurrentPetRequest(requiredParentPetId: 2, currentPetId: 2), Is.True);
        Assert.That(IsCurrentPetRequest(requiredParentPetId: 2, currentPetId: 3), Is.False,
            "A skin completion cannot apply after its parent pet changes.");
    }

    private static bool IsCurrentPetRequest(
        int requestSerial = 3,
        int currentRequestSerial = 3,
        int relatedSerial = 7,
        int currentRelatedSerial = 7,
        int? requiredParentPetId = null,
        int currentPetId = 2,
        string requestUserId = "user-a",
        string currentUserId = "user-a",
        string requestMatchId = "match-a",
        string currentMatchId = "match-a",
        bool matchStartPending = false) =>
        LobbyPetSelectionRequestGuard.IsCurrent(
            requestSerial, currentRequestSerial,
            relatedSerial, currentRelatedSerial,
            requiredParentPetId, currentPetId,
            requestUserId, currentUserId,
            requestMatchId, currentMatchId,
            matchStartPending);

    [Test]
    public void SwitchingAccountInvalidatesPreviousInventory()
    {
        AuthSession.ApplyFromMe(new MeDto { id = Guid.NewGuid().ToString("D") });
        TeamToolOwnershipSession.Clear();
        TeamToolOwnershipSession.MarkOwned(CharacterShopAssets.JammoItemId);
        Assert.That(TeamToolOwnershipSession.OwnsItem(CharacterShopAssets.JammoItemId), Is.True);
        typeof(TeamToolOwnershipSession).GetField("_isLoaded",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, true);

        AuthSession.ApplyFromMe(new MeDto { id = Guid.NewGuid().ToString("D") });
        Assert.That(TeamToolOwnershipSession.IsLoaded, Is.False,
            "Lobby Bind must see the new account as unloaded and refresh it.");
        Assert.That(TeamToolOwnershipSession.OwnsItem(CharacterShopAssets.JammoItemId), Is.False);
    }

    [Test]
    public void LogoutLocalClearsOwnershipImmediately()
    {
        AuthSession.ApplyFromMe(new MeDto { id = Guid.NewGuid().ToString("D") });
        TeamToolOwnershipSession.Clear();
        TeamToolOwnershipSession.MarkOwned(CharacterShopAssets.JammoItemId);
        var owner = new GameObject("AuthLogoutTest");
        try
        {
            owner.AddComponent<AuthApiService>().LogoutLocal();
            Assert.That(AuthSession.IsAuthenticated, Is.False);
            Assert.That(TeamToolOwnershipSession.OwnsItem(CharacterShopAssets.JammoItemId), Is.False);
            Assert.That(TeamToolOwnershipSession.IsLoaded, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
}
