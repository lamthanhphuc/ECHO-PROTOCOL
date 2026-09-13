using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorMCP.Handlers;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnityEditorMCP.Tests
{
    [TestFixture]
    public class SceneHandlerTests
    {
        private string testSceneFolder = "Assets/TestScenes";

        private const string MinimalSceneName =
            "__UnityEditorMCP_MinimalSceneTest";

        private const string MinimalScenePath =
            "Assets/Scenes/__UnityEditorMCP_MinimalSceneTest.unity";

        private static JObject ToJObject(object result)
        {
            Assert.IsNotNull(result);
            return JObject.FromObject(result);
        }

        [SetUp]
        public void Setup()
        {
            AssetDatabase.DeleteAsset(MinimalScenePath);

            // Create test folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(testSceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "TestScenes");
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test scenes
            if (AssetDatabase.IsValidFolder(testSceneFolder))
            {
                AssetDatabase.DeleteAsset(testSceneFolder);
            }

            AssetDatabase.DeleteAsset(MinimalScenePath);

            // Remove any test scenes from build settings
            var buildScenes = EditorBuildSettings.scenes.ToList();
            buildScenes.RemoveAll(s => s.path.Contains("TestScene"));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        [Test]
        public void CreateScene_ShouldWorkWithMinimalParameters()
        {
            var parameters = new JObject
            {
                ["sceneName"] = MinimalSceneName
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.AreEqual(MinimalSceneName, result["sceneName"].ToString());
            Assert.AreEqual(MinimalScenePath, result["path"].ToString());
            Assert.IsTrue(result["isLoaded"].Value<bool>());
            
            // Verify scene was created
            Assert.IsTrue(File.Exists(result["path"].ToString()));
            
            // Clean up
            AssetDatabase.DeleteAsset(result["path"].ToString());
        }

        [Test]
        public void CreateScene_ShouldWorkWithCustomPath()
        {
            var parameters = new JObject
            {
                ["sceneName"] = "CustomScene",
                ["path"] = testSceneFolder + "/"
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.AreEqual("CustomScene", result["sceneName"].ToString());
            Assert.AreEqual(testSceneFolder + "/CustomScene.unity", result["path"].ToString());
            
            // Verify scene was created
            Assert.IsTrue(File.Exists(result["path"].ToString()));
        }

        [Test]
        public void CreateScene_ShouldNotLoadScene_WhenLoadSceneIsFalse()
        {
            var currentScenePath = SceneManager.GetActiveScene().path;
            
            var parameters = new JObject
            {
                ["sceneName"] = "UnloadedScene",
                ["path"] = testSceneFolder + "/",
                ["loadScene"] = false
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.IsFalse(result["isLoaded"].Value<bool>());
            
            // Verify current scene didn't change
            Assert.AreEqual(currentScenePath, SceneManager.GetActiveScene().path);
        }

        [Test]
        public void CreateScene_ShouldAddToBuildSettings_WhenRequested()
        {
            var parameters = new JObject
            {
                ["sceneName"] = "BuildScene",
                ["path"] = testSceneFolder + "/",
                ["addToBuildSettings"] = true
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.IsTrue(result["sceneIndex"].Value<int>() >= 0);
            
            // Verify scene is in build settings
            var buildScenes = EditorBuildSettings.scenes;
            Assert.IsTrue(buildScenes.Any(s => s.path == result["path"].ToString()));
        }

        [Test]
        public void CreateScene_ShouldFailForEmptySceneName()
        {
            var parameters = new JObject
            {
                ["sceneName"] = ""
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Scene name cannot be empty"));
        }

        [Test]
        public void CreateScene_ShouldFailForInvalidSceneName()
        {
            var parameters = new JObject
            {
                ["sceneName"] = "Invalid/Scene/Name"
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("invalid characters"));
        }

        [Test]
        public void CreateScene_ShouldFailForExistingScene()
        {
            // Create a scene first
            var scenePath = testSceneFolder + "/ExistingScene.unity";
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, scenePath);

            var parameters = new JObject
            {
                ["sceneName"] = "ExistingScene",
                ["path"] = testSceneFolder + "/"
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("already exists"));
        }

        [Test]
        public void CreateScene_ShouldFailForInvalidPath()
        {
            var parameters = new JObject
            {
                ["sceneName"] = "TestScene",
                ["path"] = "../InvalidPath/"
            };

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Invalid path"));
        }

        [Test]
        public void CreateScene_ShouldHandleMissingParameters()
        {
            var parameters = new JObject();

            var result = ToJObject(SceneHandler.CreateScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Scene name cannot be empty"));
        }
    }
}
