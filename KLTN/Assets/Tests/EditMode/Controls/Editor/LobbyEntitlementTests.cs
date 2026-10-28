using System;
using EchoProtocol.Auth;
using EchoProtocol.Networking;
using EchoProtocol.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

public sealed class LobbyEntitlementTests
{
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
    }

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
