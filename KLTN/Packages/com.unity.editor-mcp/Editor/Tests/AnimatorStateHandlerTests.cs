using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorMCP.Handlers;
using Newtonsoft.Json.Linq;

namespace UnityEditorMCP.Tests
{
    public class AnimatorStateHandlerTests
    {
        private const string TestControllerPath =
            "Assets/__UnityEditorMCP_AnimatorStateHandlerTests.controller";

        private GameObject testGameObject;
        private Animator testAnimator;
        private AnimatorController testController;

        private static JObject ToJObject(object result)
        {
            Assert.IsNotNull(result);
            return JObject.FromObject(result);
        }
        
        [SetUp]
        public void Setup()
        {
            AssetDatabase.DeleteAsset(TestControllerPath);

            testGameObject = new GameObject("TestAnimatorObject");
            testAnimator = testGameObject.AddComponent<Animator>();

            testController =
                AnimatorController.CreateAnimatorControllerAtPath(TestControllerPath);

            Assert.IsNotNull(testController);
            testAnimator.runtimeAnimatorController = testController;
        }
        
        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }

            testAnimator = null;
            testController = null;

            AssetDatabase.DeleteAsset(TestControllerPath);
        }
        
        [Test]
        public void GetAnimatorState_WithValidGameObject_ReturnsSuccess()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectName"] = testGameObject.name,
                ["includeParameters"] = true,
                ["includeStates"] = true
            };
            
            // Act
            var result = AnimatorStateHandler.GetAnimatorState(parameters);
            
            // Assert
            var dict = ToJObject(result);
            Assert.IsNull(dict["error"]);
            Assert.AreEqual(testGameObject.name, dict["gameObject"].ToString());
            Assert.AreEqual(testAnimator.enabled, dict["enabled"].Value<bool>());
        }
        
        [Test]
        public void GetAnimatorState_WithInvalidGameObject_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectName"] = "NonExistentObject"
            };
            
            // Act
            var result = AnimatorStateHandler.GetAnimatorState(parameters);
            
            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("GameObject not found"));
        }
        
        [Test]
        public void GetAnimatorState_WithoutGameObjectName_ReturnsError()
        {
            // Arrange
            var parameters = new JObject();
            
            // Act
            var result = AnimatorStateHandler.GetAnimatorState(parameters);
            
            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("gameObjectName is required"));
        }
        
        [Test]
        public void GetAnimatorRuntimeInfo_NotInPlayMode_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["gameObjectName"] = testGameObject.name
            };
            
            // Act
            var result = AnimatorStateHandler.GetAnimatorRuntimeInfo(parameters);
            
            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("only available in Play mode"));
        }
        
        [Test]
        public void GetAnimatorState_WithoutAnimatorComponent_ReturnsError()
        {
            // Arrange
            Object.DestroyImmediate(testAnimator);
            var parameters = new JObject
            {
                ["gameObjectName"] = testGameObject.name
            };
            
            // Act
            var result = AnimatorStateHandler.GetAnimatorState(parameters);
            
            // Assert
            var dict = ToJObject(result);
            Assert.IsNotNull(dict["error"]);
            Assert.IsTrue(dict["error"].ToString().Contains("Animator component not found"));
        }
    }
}
