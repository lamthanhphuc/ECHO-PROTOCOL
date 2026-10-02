using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class PlayerMenuControlLockTests
    {
        private GameObject _player;
        private PlayerDownState _downState;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.False);
            PlayerInteractionControlLock.ReleaseAll();
            _player = new GameObject("MenuLockTestPlayer");
            _downState = _player.AddComponent<PlayerDownState>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInteractionControlLock.ReleaseAll();
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [Test]
        public void SettingsCanOpenWhenAlreadyDownedAndRemainOpenAfterRevive()
        {
            SetState(PlayerLifeState.Downed);
            var parent = new PlayerInteractionControlLock();
            var settings = new PlayerInteractionControlLock();
            parent.Acquire(_player, allowWhileDowned: true);
            settings.Acquire(_player, allowWhileDowned: true);

            ReleaseInvalidLocks();
            Assert.That(parent.IsLocked, Is.True);
            Assert.That(settings.IsTopmost, Is.True);
            Assert.That(PlayerInteractionControlLock.ShouldUnlockCursor, Is.True);
            Assert.That(PlayerInteractionControlLock.IsGameplayInputBlocked(_player), Is.True);

            SetState(PlayerLifeState.Active);
            ReleaseInvalidLocks();
            Assert.That(settings.IsTopmost, Is.True);
            settings.Release();
            Assert.That(parent.IsTopmost, Is.True);
            parent.Release();
            Assert.That(PlayerInteractionControlLock.HasModal, Is.False);
        }

        [Test]
        public void DowningPlayerInterruptsInteractionButKeepsSettingsLocks()
        {
            int interrupted = 0;
            var interaction = new PlayerInteractionControlLock();
            var parent = new PlayerInteractionControlLock();
            var settings = new PlayerInteractionControlLock();
            interaction.Acquire(_player, () => interrupted++);
            parent.Acquire(_player, allowWhileDowned: true);
            settings.Acquire(_player, allowWhileDowned: true);

            SetState(PlayerLifeState.Downed);
            ReleaseInvalidLocks();

            Assert.That(interrupted, Is.EqualTo(1));
            Assert.That(interaction.IsLocked, Is.False);
            Assert.That(parent.IsLocked, Is.True);
            Assert.That(settings.IsTopmost, Is.True);
        }

        [Test]
        public void SwitchingSettingsLockBackToInventoryRestoresDefaultPolicy()
        {
            var menu = new PlayerInteractionControlLock();
            menu.Acquire(_player, allowWhileDowned: true);
            menu.Acquire(_player);
            SetState(PlayerLifeState.Downed);
            ReleaseInvalidLocks();
            Assert.That(menu.IsLocked, Is.False);
        }

        [Test]
        public void InventoryAvailabilityTracksDownAndRevive()
        {
            Assert.That(PlayerInteractionControlLock.IsPlayerStateValid(_player), Is.True);
            SetState(PlayerLifeState.Downed);
            Assert.That(PlayerInteractionControlLock.IsPlayerStateValid(_player), Is.False);
            Assert.That(PlayerInteractionControlLock.IsPlayerStateValid(_player, allowWhileDowned: true), Is.True);
            SetState(PlayerLifeState.Active);
            Assert.That(PlayerInteractionControlLock.IsPlayerStateValid(_player), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SettingsStillReleaseWhenPlayerIsDisabledOrDestroyed(bool destroy)
        {
            var menu = new PlayerInteractionControlLock();
            menu.Acquire(_player, allowWhileDowned: true);
            SetState(PlayerLifeState.Downed);
            if (destroy) Object.DestroyImmediate(_player);
            else _player.SetActive(false);
            ReleaseInvalidLocks();
            Assert.That(menu.IsLocked, Is.False);
        }

        [Test]
        public void DownedAllowanceDoesNotKeepSettingsOpenAfterElimination()
        {
            var menu = new PlayerInteractionControlLock();
            menu.Acquire(_player, allowWhileDowned: true);
            SetState(PlayerLifeState.Eliminated);
            ReleaseInvalidLocks();
            Assert.That(menu.IsLocked, Is.False);
        }

        private void SetState(PlayerLifeState state)
        {
            _downState.ApplyAuthoritativeSnapshot(state, state == PlayerLifeState.Active ? 100f : 0f,
                45f, 0f, applyLocalControls: false);
        }

        private static void ReleaseInvalidLocks()
        {
            typeof(PlayerInteractionControlLock).GetMethod("ReleaseInvalidLocks", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
        }
    }
}
