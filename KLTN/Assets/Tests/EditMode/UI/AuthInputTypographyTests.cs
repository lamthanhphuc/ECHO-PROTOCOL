using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Tests
{
    public sealed class AuthInputTypographyTests
    {
        [Test]
        public void SmallAuthFieldsUseReadableTextAndPreservePasswordMasking()
        {
            var root = new GameObject("Password", typeof(RectTransform), typeof(Image), typeof(InputField));
            try
            {
                var input = root.GetComponent<InputField>();
                ((RectTransform)root.transform).sizeDelta = new Vector2(444,56);
                var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(root.transform, false);
                var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
                placeholderObject.transform.SetParent(root.transform, false);
                input.textComponent = textObject.GetComponent<Text>();
                input.placeholder = placeholderObject.GetComponent<Text>();
                ((Text)input.placeholder).text = "Password";
                input.contentType = InputField.ContentType.Password;
                var type = Type.GetType("EchoProtocol.UI.Auth.AuthScreenController, Assembly-CSharp", true);
                type.GetMethod("ConfigureInputField", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { input });
                Assert.That(input.textComponent.font, Is.Not.Null);
                Assert.That(input.textComponent.fontSize, Is.EqualTo(20));
                Assert.That(input.textComponent.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
                Assert.That(((Text)input.placeholder).font, Is.EqualTo(input.textComponent.font));
                Assert.That(((Text)input.placeholder).text, Is.EqualTo("Password"));
                Assert.That(input.contentType, Is.EqualTo(InputField.ContentType.Password));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
