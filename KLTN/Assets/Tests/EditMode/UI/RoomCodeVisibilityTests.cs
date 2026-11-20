using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Tests
{
    public class RoomCodeVisibilityTests
    {
        [Test]
        public void EyePreservesCodeResetsOnReopenAndHonorsDisabledInput()
        {
            var root = new GameObject("Code", typeof(RectTransform), typeof(Image), typeof(InputField));
            try
            {
                var text = new GameObject("Text", typeof(RectTransform), typeof(Text));
                text.transform.SetParent(root.transform, false);
                text.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var field = root.GetComponent<InputField>();
                field.textComponent = text.GetComponent<Text>();
                field.text = "Room1234";
                var type = Type.GetType("EchoProtocol.UI.MainMenu.RoomCodeVisibilityToggle, Assembly-CSharp");
                var toggle = (Component)type.GetMethod("Attach").Invoke(null, new object[] { field });
                var eye = root.transform.Find("RoomCodeVisibility").GetComponent<Button>();
                Assert.That(eye.transform.Find("Eye").GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(field.inputType, Is.EqualTo(InputField.InputType.Password));
                eye.onClick.Invoke();
                Assert.That(field.inputType, Is.EqualTo(InputField.InputType.Standard));
                Assert.That(field.text, Is.EqualTo("Room1234"));
                field.interactable = false;
                eye.onClick.Invoke();
                Assert.That(field.inputType, Is.EqualTo(InputField.InputType.Standard));
                field.interactable = true;
                var controllerType = Type.GetType("EchoProtocol.UI.MainMenu.MainMenuMatchmakingController, Assembly-CSharp");
                var controller = root.AddComponent(controllerType);
                controllerType.GetField("roomCodeInput", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, field);
                controllerType.GetMethod("OpenDialog", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                Assert.That(field.inputType, Is.EqualTo(InputField.InputType.Password));
                Assert.That(field.text, Is.EqualTo("Room1234"));
                Assert.That(field.textComponent.rectTransform.offsetMax.x, Is.LessThanOrEqualTo(-60));
                Assert.That(toggle, Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
