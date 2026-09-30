using System;
using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class GameplayInputSettingsTests
    {
        private const string Prefix = "EchoProtocol.Controls.";
        private readonly Dictionary<string, int?> _savedInts = new Dictionary<string, int?>();
        private bool _hadSensitivity;
        private float _savedSensitivity;
        private bool _hadVoiceKey;
        private string _savedVoiceKey;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.False, "Run these preference tests in Edit mode.");
            foreach (GameplayAction action in Enum.GetValues(typeof(GameplayAction))) SaveInt(Prefix + action);
            SaveInt(Prefix + "InvertY");
            SaveInt(Prefix + "Acceleration");
            _hadVoiceKey = PlayerPrefs.HasKey("Echo.Voice.ToggleKey");
            _savedVoiceKey = PlayerPrefs.GetString("Echo.Voice.ToggleKey", "V");
            PlayerPrefs.SetString("Echo.Voice.ToggleKey", "V");
            _hadSensitivity = PlayerPrefs.HasKey(Prefix + "Sensitivity");
            _savedSensitivity = PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
            PlayerPrefs.DeleteKey(Prefix + "Sensitivity");
            ReloadPreferences();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var saved in _savedInts)
            {
                if (saved.Value.HasValue) PlayerPrefs.SetInt(saved.Key, saved.Value.Value);
                else PlayerPrefs.DeleteKey(saved.Key);
            }
            _savedInts.Clear();
            if (_hadVoiceKey) PlayerPrefs.SetString("Echo.Voice.ToggleKey", _savedVoiceKey);
            else PlayerPrefs.DeleteKey("Echo.Voice.ToggleKey");
            if (_hadSensitivity) PlayerPrefs.SetFloat(Prefix + "Sensitivity", _savedSensitivity);
            else PlayerPrefs.DeleteKey(Prefix + "Sensitivity");
            ReloadPreferences();
        }

        private void SaveInt(string key)
        {
            _savedInts[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            PlayerPrefs.DeleteKey(key);
        }

        private static void ReloadPreferences()
        {
            typeof(GameplayInputSettings).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, false);
        }

        [Test]
        public void RebindResolvesKeyboardControlsAndPreservesGamepadBinding()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            try
            {
                GameplayInputSettings.RegisterAction(move);
                move.Enable();
                Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.MoveForward, Key.I, out string error), Is.True, error);
                // Edit-mode updates go to Editor input buffers in this Input System version.
                // Check the enabled action's actual resolved controls without simulating a player loop.
                CollectionAssert.Contains(move.controls, keyboard.iKey);
                CollectionAssert.DoesNotContain(move.controls, keyboard.wKey);
                CollectionAssert.Contains(move.controls, gamepad.leftStick);
                Assert.That(move.bindings[5].effectivePath, Is.EqualTo("<Gamepad>/leftStick"));
            }
            finally
            {
                GameplayInputSettings.UnregisterAction(move);
                move.Dispose();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void RejectsExistingBindingsAndReservedGameplayKeys()
        {
            foreach (Key key in new[] { Key.W, Key.Escape, Key.V, Key.G, Key.H, Key.Q, Key.F1, Key.Backquote, Key.Digit1, Key.LeftCtrl })
            {
                Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.Interact, key, out string error), Is.False, key.ToString());
                Assert.That(error, Is.Not.Empty);
            }
            Assert.That(GameplayInputSettings.GetKey(GameplayAction.Interact), Is.EqualTo(Key.E));
        }

        [Test]
        public void RestoringDefaultDoesNotStealAnAlternateKeyFromAnotherAction()
        {
            Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.MoveForward, Key.I, out _), Is.True);
            Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.Inventory, Key.UpArrow, out _), Is.True);
            Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.MoveForward, Key.W, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameplayInputSettings.GetKey(GameplayAction.MoveForward), Is.EqualTo(Key.I));
        }

        [Test]
        public void RestoringDefaultDoesNotStealAnAlternateKeyFromMicrophone()
        {
            Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.MoveForward, Key.I, out _), Is.True);
            PlayerPrefs.SetString("Echo.Voice.ToggleKey", "UpArrow");
            Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.MoveForward, Key.W, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameplayInputSettings.GetKey(GameplayAction.MoveForward), Is.EqualTo(Key.I));
        }

        [Test]
        public void PreferencesReloadAndApplyToNewActions()
        {
            Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.Interact, Key.I, out _), Is.True);
            GameplayInputSettings.MouseSensitivity = 1.7f;
            GameplayInputSettings.InvertY = true;
            GameplayInputSettings.MouseAcceleration = true;
            ReloadPreferences();
            Assert.That(GameplayInputSettings.MouseSensitivity, Is.EqualTo(1.7f).Within(0.001f));
            Assert.That(GameplayInputSettings.InvertY, Is.True);
            Assert.That(GameplayInputSettings.MouseAcceleration, Is.True);
            var interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            try
            {
                GameplayInputSettings.RegisterAction(interact);
                Assert.That(interact.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/i"));
                Assert.That(GameplayInputSettings.TrySetKey(GameplayAction.Interact, Key.E, out _), Is.True);
                Assert.That(interact.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
                Assert.That(interact.bindings[0].overridePath, Is.Null);
            }
            finally
            {
                GameplayInputSettings.UnregisterAction(interact);
                interact.Dispose();
            }
        }

        [Test]
        public void DefaultLookIsUnchangedAndInvertYChangesOnlyVerticalAxis()
        {
            var input = new Vector2(3f, 5f);
            Assert.That(GameplayInputSettings.ApplyLookSettings(input, 1f / 60f), Is.EqualTo(input));
            GameplayInputSettings.MouseSensitivity = 2f;
            GameplayInputSettings.InvertY = true;
            Assert.That(GameplayInputSettings.ApplyLookSettings(input, 1f / 60f), Is.EqualTo(new Vector2(6f, -10f)));
        }
    }
}
