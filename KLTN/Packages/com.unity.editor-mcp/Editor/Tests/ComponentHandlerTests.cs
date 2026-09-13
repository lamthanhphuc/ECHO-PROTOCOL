using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using UnityEditorMCP.Handlers;

namespace UnityEditorMCP.Tests
{
    /// <summary>
    /// Unit tests for ComponentHandler functionality
    /// </summary>
    [TestFixture]
    public class ComponentHandlerTests
    {
        private GameObject testGameObject;

        private static JObject ToJObject(object result)
        {
            Assert.IsNotNull(result);
            return JObject.FromObject(result);
        }

        [SetUp]
        public void Setup()
        {
            // Create a test GameObject for each test
            testGameObject = new GameObject("TestObject");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test GameObject
            if (testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(testGameObject);
            }
        }

        #region AddComponent Tests

        [Test]
        public void AddComponent_WithValidType_ShouldSucceed()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody",
                ["properties"] = new JObject
                {
                    ["mass"] = 2.0f,
                    ["useGravity"] = true
                }
            };

            // Act
            var result = ComponentHandler.AddComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNull(dict["error"]);
            Assert.IsNotNull(dict["success"]);
            Assert.IsTrue(dict["success"].Value<bool>());
            
            // Verify component was added
            var rb = testGameObject.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb);
            Assert.AreEqual(2.0f, rb.mass);
            Assert.IsTrue(rb.useGravity);
        }

        [Test]
        public void AddComponent_WithInvalidType_ShouldFail()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "NonExistentComponent"
            };

            // Act
            var result = ComponentHandler.AddComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("Component type not found"));
        }

        [Test]
        public void AddComponent_ToNonExistentGameObject_ShouldFail()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/NonExistentObject",
                ["componentType"] = "Rigidbody"
            };

            // Act
            var result = ComponentHandler.AddComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("GameObject not found"));
        }

        [Test]
        public void AddComponent_DuplicateUniqueComponent_ShouldFail()
        {
            // Arrange
            testGameObject.AddComponent<Rigidbody>(); // Add first
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody"
            };

            // Act
            var result = ComponentHandler.AddComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("already has component"));
        }

        #endregion

        #region RemoveComponent Tests

        [Test]
        public void RemoveComponent_ExistingComponent_ShouldSucceed()
        {
            // Arrange
            testGameObject.AddComponent<Rigidbody>();
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody"
            };

            // Act
            var result = ComponentHandler.RemoveComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNull(dict["error"]);
            Assert.IsTrue(dict["removed"].Value<bool>());
            Assert.IsNull(testGameObject.GetComponent<Rigidbody>());
        }

        [Test]
        public void RemoveComponent_NonExistentComponent_ShouldReturnFalse()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody"
            };

            // Act
            var result = ComponentHandler.RemoveComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNull(dict["error"]);
            Assert.IsFalse(dict["removed"].Value<bool>());
        }

        [Test]
        public void RemoveComponent_Transform_ShouldFail()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Transform"
            };

            // Act
            var result = ComponentHandler.RemoveComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("Cannot remove Transform"));
        }

        [Test]
        public void RemoveComponent_WithIndex_ShouldRemoveSpecificInstance()
        {
            // Arrange
            testGameObject.AddComponent<BoxCollider>();
            testGameObject.AddComponent<BoxCollider>();
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "BoxCollider",
                ["componentIndex"] = 1
            };

            // Act
            var result = ComponentHandler.RemoveComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsTrue(dict["removed"].Value<bool>());
            var remainingColliders = testGameObject.GetComponents<BoxCollider>();
            Assert.AreEqual(1, remainingColliders.Length);
        }

        #endregion

        #region ModifyComponent Tests

        [Test]
        public void ModifyComponent_ValidProperties_ShouldSucceed()
        {
            // Arrange
            var rb = testGameObject.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody",
                ["properties"] = new JObject
                {
                    ["mass"] = 5.0f,
                    ["linearDamping"] = 0.5f
                }
            };

            // Act
            var result = ComponentHandler.ModifyComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNull(dict["error"]);
            Assert.AreEqual(5.0f, rb.mass);
            Assert.AreEqual(0.5f, rb.linearDamping);
            
            var modifiedProps =
                dict["modifiedProperties"]?.ToObject<string[]>();
            Assert.Contains("mass", modifiedProps);
            Assert.Contains("linearDamping", modifiedProps);
        }

        [Test]
        public void ModifyComponent_InvalidPropertyType_ShouldFail()
        {
            // Arrange
            testGameObject.AddComponent<Rigidbody>();
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody",
                ["properties"] = new JObject
                {
                    ["mass"] = "not a number"
                }
            };

            // Act
            var result = ComponentHandler.ModifyComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(
                dict["error"].ToString().Contains("Property not found or invalid")
                || dict["error"].ToString().Contains("mass"));
        }

        [Test]
        public void ModifyComponent_NonExistentProperty_ShouldFail()
        {
            // Arrange
            testGameObject.AddComponent<Rigidbody>();
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["componentType"] = "Rigidbody",
                ["properties"] = new JObject
                {
                    ["nonExistentProperty"] = 123
                }
            };

            // Act
            var result = ComponentHandler.ModifyComponent(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("Property not found"));
        }

        #endregion

        #region ListComponents Tests

        [Test]
        public void ListComponents_ShouldReturnAllComponents()
        {
            // Arrange
            testGameObject.AddComponent<Rigidbody>();
            testGameObject.AddComponent<BoxCollider>();
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject"
            };

            // Act
            var result = ComponentHandler.ListComponents(parameters);

            // Assert
            var dict = ToJObject(result);
            Assert.IsNull(dict["error"]);
            
            var components = dict["components"] as JArray;
            Assert.IsNotNull(components);
            Assert.AreEqual(3, components.Count); // Transform + Rigidbody + BoxCollider
        }

        [Test]
        public void ListComponents_WithProperties_ShouldIncludeValues()
        {
            // Arrange
            var rb = testGameObject.AddComponent<Rigidbody>();
            rb.mass = 2.5f;
            rb.useGravity = false;
            
            var parameters = new JObject
            {
                ["gameObjectPath"] = "/TestObject",
                ["includeProperties"] = true
            };

            // Act
            var result = ComponentHandler.ListComponents(parameters);

            // Assert
            var dict = ToJObject(result);
            var components = dict["components"] as JArray;
            
            // Find Rigidbody in results
            JObject rbComponent = null;
            foreach (var comp in components)
            {
                var compDict = comp as JObject;
                if (compDict?["type"]?.ToString() == "Rigidbody")
                {
                    rbComponent = compDict;
                    break;
                }
            }
            
            Assert.IsNotNull(rbComponent);
            Assert.IsNotNull(rbComponent["properties"]);
            
            var props = rbComponent["properties"] as JObject;
            Assert.AreEqual(2.5f, props["mass"].Value<float>());
            Assert.AreEqual(false, props["useGravity"].Value<bool>());
        }

        #endregion

        #region Helper Method Tests

        [Test]
        public void ResolveComponentType_ShortName_ShouldResolve()
        {
            // Act
            var type = ComponentHandler.ResolveComponentType("Rigidbody");
            
            // Assert
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(Rigidbody), type);
        }

        [Test]
        public void ResolveComponentType_FullName_ShouldResolve()
        {
            // Act
            var type = ComponentHandler.ResolveComponentType("UnityEngine.Rigidbody");
            
            // Assert
            Assert.IsNotNull(type);
            Assert.AreEqual(typeof(Rigidbody), type);
        }

        [Test]
        public void ConvertValue_SimpleTypes_ShouldConvert()
        {
            // Act & Assert
            Assert.AreEqual(5.0f, ComponentHandler.ConvertValue("5.0", typeof(float)));
            Assert.AreEqual(10, ComponentHandler.ConvertValue("10", typeof(int)));
            Assert.AreEqual(true, ComponentHandler.ConvertValue("true", typeof(bool)));
            Assert.AreEqual("test", ComponentHandler.ConvertValue("test", typeof(string)));
        }

        [Test]
        public void ConvertValue_UnityTypes_ShouldConvert()
        {
            // Arrange
            var vector3Json = new JObject { ["x"] = 1, ["y"] = 2, ["z"] = 3 };
            var colorJson = new JObject { ["r"] = 1, ["g"] = 0, ["b"] = 0, ["a"] = 1 };

            // Act
            var vector = ComponentHandler.ConvertValue(vector3Json, typeof(Vector3));
            var color = ComponentHandler.ConvertValue(colorJson, typeof(Color));

            // Assert
            Assert.AreEqual(new Vector3(1, 2, 3), vector);
            Assert.AreEqual(Color.red, color);
        }

        #endregion
    }
}
