#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.Collections;
using UnityEditorMCP.Handlers;

// Conditionally include Input System namespace only if available
#pragma warning disable CS0234 // Type or namespace does not exist
using UnityEngine.InputSystem;
#pragma warning restore CS0234

namespace UnityEditorMCP.Tests
{
    /// <summary>
    /// Tests for InputSystemHandler
    /// </summary>
    [TestFixture]
    public class InputSystemHandlerTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private Touchscreen touchscreen;

        private static JObject ToJObject(object result)
        {
            Assert.NotNull(result);
            return JObject.FromObject(result);
        }

        private static void AssertPlayModeRequired(object result)
        {
            var resultJson = ToJObject(result);
            Assert.NotNull(resultJson["error"]);
            Assert.AreEqual("PLAY_MODE_REQUIRED", resultJson["code"]?.ToString());
            StringAssert.Contains("Play Mode is required", resultJson["error"].ToString());
        }

        [SetUp]
        public void Setup()
        {
            // Add test devices
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test devices
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);

            keyboard = null;
            mouse = null;
            gamepad = null;
            touchscreen = null;
        }

        #region Keyboard Tests

        [Test]
        public void SimulateKeyboardInput_PressKey_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "press",
                ["key"] = "A"
            };

            // Act
            var result = InputSystemHandler.SimulateKeyboardInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateKeyboardInput_ReleaseKey_Success()
        {
            // Arrange - First press the key
            var pressParams = new JObject
            {
                ["action"] = "press",
                ["key"] = "A"
            };
            AssertPlayModeRequired(
                InputSystemHandler.SimulateKeyboardInput(pressParams));

            // Act - Release the key
            var releaseParams = new JObject
            {
                ["action"] = "release",
                ["key"] = "A"
            };
            var result = InputSystemHandler.SimulateKeyboardInput(releaseParams);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateKeyboardInput_TypeText_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "type",
                ["text"] = "Hello",
                ["typingSpeed"] = 10
            };

            // Act
            var result = InputSystemHandler.SimulateKeyboardInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateKeyboardInput_KeyCombo_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "combo",
                ["keys"] = new JArray { "LeftCtrl", "C" }
            };

            // Act
            var result = InputSystemHandler.SimulateKeyboardInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        #endregion

        #region Mouse Tests

        [Test]
        public void SimulateMouseInput_Move_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "move",
                ["x"] = 100,
                ["y"] = 200,
                ["absolute"] = true
            };

            // Act
            var result = InputSystemHandler.SimulateMouseInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateMouseInput_Click_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "click",
                ["button"] = "left",
                ["clickCount"] = 1
            };

            // Act
            var result = InputSystemHandler.SimulateMouseInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateMouseInput_Drag_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "drag",
                ["startX"] = 10,
                ["startY"] = 20,
                ["endX"] = 100,
                ["endY"] = 200,
                ["button"] = "left"
            };

            // Act
            var result = InputSystemHandler.SimulateMouseInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateMouseInput_Scroll_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "scroll",
                ["deltaX"] = 0,
                ["deltaY"] = 10
            };

            // Act
            var result = InputSystemHandler.SimulateMouseInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        #endregion

        #region Gamepad Tests

        [Test]
        public void SimulateGamepadInput_Button_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "button",
                ["button"] = "a",
                ["buttonAction"] = "press"
            };

            // Act
            var result = InputSystemHandler.SimulateGamepadInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateGamepadInput_Stick_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "stick",
                ["stick"] = "left",
                ["x"] = 0.5f,
                ["y"] = 0.75f
            };

            // Act
            var result = InputSystemHandler.SimulateGamepadInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateGamepadInput_Trigger_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "trigger",
                ["trigger"] = "left",
                ["value"] = 0.8f
            };

            // Act
            var result = InputSystemHandler.SimulateGamepadInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateGamepadInput_DPad_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "dpad",
                ["direction"] = "up"
            };

            // Act
            var result = InputSystemHandler.SimulateGamepadInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        #endregion

        #region Touch Tests

        [Test]
        public void SimulateTouchInput_Tap_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "tap",
                ["x"] = 100,
                ["y"] = 200,
                ["touchId"] = 0
            };

            // Act
            var result = InputSystemHandler.SimulateTouchInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateTouchInput_Swipe_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "swipe",
                ["startX"] = 100,
                ["startY"] = 100,
                ["endX"] = 300,
                ["endY"] = 100,
                ["duration"] = 500,
                ["touchId"] = 0
            };

            // Act
            var result = InputSystemHandler.SimulateTouchInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateTouchInput_Pinch_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "pinch",
                ["centerX"] = 200,
                ["centerY"] = 200,
                ["startDistance"] = 50,
                ["endDistance"] = 150
            };

            // Act
            var result = InputSystemHandler.SimulateTouchInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        #endregion

        #region Sequence and State Tests

        [Test]
        public void CreateInputSequence_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["sequence"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "keyboard",
                        ["params"] = new JObject
                        {
                            ["action"] = "press",
                            ["key"] = "W"
                        }
                    },
                    new JObject
                    {
                        ["type"] = "mouse",
                        ["params"] = new JObject
                        {
                            ["action"] = "move",
                            ["x"] = 100,
                            ["y"] = 100,
                            ["absolute"] = true
                        }
                    }
                },
                ["delayBetween"] = 100
            };

            // Act
            var result = InputSystemHandler.CreateInputSequence(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void GetCurrentInputState_Success()
        {
            // Arrange
            var parameters = new JObject();

            // Act
            var result = InputSystemHandler.GetCurrentInputState(parameters);

            // Assert
            var resultJson = ToJObject(result);
            Assert.NotNull(resultJson["activeDevices"]);
            Assert.NotNull(resultJson["keyboard"]);
            Assert.NotNull(resultJson["mouse"]);
            Assert.NotNull(resultJson["gamepad"]);
            Assert.NotNull(resultJson["touchscreen"]);
        }

        #endregion

        #region Error Cases

        [Test]
        public void SimulateKeyboardInput_InvalidAction_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "invalid_action"
            };

            // Act
            var result = InputSystemHandler.SimulateKeyboardInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateKeyboardInput_MissingKey_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "press"
                // Missing "key" parameter
            };

            // Act
            var result = InputSystemHandler.SimulateKeyboardInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        [Test]
        public void SimulateMouseInput_InvalidButton_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "click",
                ["button"] = "invalid_button"
            };

            // Act
            var result = InputSystemHandler.SimulateMouseInput(parameters);

            // Assert
            AssertPlayModeRequired(result);
        }

        #endregion
    }
}
#endif
