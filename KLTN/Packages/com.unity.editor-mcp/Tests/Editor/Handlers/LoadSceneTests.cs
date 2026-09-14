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
    public class LoadSceneTests
    {
        private string testSceneFolder = "Assets/TestScenes";
        private string testScenePath;
        private Scene originalScene;

        private static JObject ToJObject(object result)
        {
            Assert.IsNotNull(result);
            return JObject.FromObject(result);
        }

        [SetUp]
        public void Setup()
        {
            // Save current scene state
            originalScene = SceneManager.GetActiveScene();
            
            // Create test folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(testSceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "TestScenes");
            }

            // Create a test scene
            testScenePath = testSceneFolder + "/LoadTestScene.unity";
            var testScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(testScene, testScenePath);
            
            // Add test scene to build settings
            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == testScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(testScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Remove test scene from build settings
            var buildScenes = EditorBuildSettings.scenes.ToList();
            buildScenes.RemoveAll(s => s.path.Contains("LoadTestScene"));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            
            // Clean up test scenes
            if (AssetDatabase.IsValidFolder(testSceneFolder))
            {
                AssetDatabase.DeleteAsset(testSceneFolder);
            }
        }

        [Test]
        public void LoadScene_ShouldLoadByPath()
        {
            var parameters = new JObject
            {
                ["scenePath"] = testScenePath
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.AreEqual("LoadTestScene", result["sceneName"].ToString());
            Assert.AreEqual(testScenePath, result["scenePath"].ToString());
            Assert.AreEqual("Single", result["loadMode"].ToString());
            Assert.IsTrue(result["isLoaded"].Value<bool>());
            
            // Verify scene is actually loaded
            Assert.AreEqual("LoadTestScene", SceneManager.GetActiveScene().name);
        }

        [Test]
        public void LoadScene_ShouldLoadByName()
        {
            // Ensure scene is in build settings
            var parameters = new JObject
            {
                ["sceneName"] = "LoadTestScene"
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.AreEqual("LoadTestScene", result["sceneName"].ToString());
            Assert.IsTrue(result["isLoaded"].Value<bool>());
        }

        [Test]
        public void LoadScene_ShouldLoadAdditively()
        {
            // First create another scene to have multiple scenes
            var additiveScenePath = testSceneFolder + "/AdditiveTestScene.unity";
            var additiveScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(additiveScene, additiveScenePath);
            
            var parameters = new JObject
            {
                ["scenePath"] = additiveScenePath,
                ["loadMode"] = "Additive"
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.AreEqual("AdditiveTestScene", result["sceneName"].ToString());
            Assert.AreEqual("Additive", result["loadMode"].ToString());
            Assert.IsTrue(result["isLoaded"].Value<bool>());
            Assert.IsTrue(result["activeSceneCount"].Value<int>() > 1);
            
            // Verify multiple scenes are loaded
            Assert.AreEqual(2, SceneManager.sceneCount);
        }

        [Test]
        public void LoadScene_ShouldFailForMissingParameters()
        {
            var parameters = new JObject();

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Either scenePath or sceneName must be provided"));
        }

        [Test]
        public void LoadScene_ShouldFailForBothParameters()
        {
            var parameters = new JObject
            {
                ["scenePath"] = testScenePath,
                ["sceneName"] = "LoadTestScene"
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Provide either scenePath or sceneName, not both"));
        }

        [Test]
        public void LoadScene_ShouldFailForInvalidLoadMode()
        {
            var parameters = new JObject
            {
                ["scenePath"] = testScenePath,
                ["loadMode"] = "InvalidMode"
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Invalid load mode"));
        }

        [Test]
        public void LoadScene_ShouldFailForNonExistentScenePath()
        {
            var parameters = new JObject
            {
                ["scenePath"] = "Assets/NonExistent/Scene.unity"
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("Scene file not found"));
        }

        [Test]
        public void LoadScene_ShouldFailForSceneNotInBuildSettings()
        {
            // Create a scene not in build settings
            var notInBuildPath = testSceneFolder + "/NotInBuild.unity";
            var notInBuildScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(notInBuildScene, notInBuildPath);

            var parameters = new JObject
            {
                ["sceneName"] = "NotInBuild"
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["error"]);
            Assert.IsTrue(result["error"].ToString().Contains("not in build settings"));
        }

        [Test]
        public void LoadScene_ShouldReturnPreviousSceneInfo()
        {
            // Create the target scene first.
            var newScenePath = testSceneFolder + "/NewTestScene.unity";
            var newScene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            EditorSceneManager.SaveScene(newScene, newScenePath);

            // Restore the scene that must be active immediately before LoadScene().
            EditorSceneManager.OpenScene(testScenePath, OpenSceneMode.Single);
            var previousSceneName = SceneManager.GetActiveScene().name;

            var parameters = new JObject
            {
                ["scenePath"] = newScenePath
            };

            var result = ToJObject(SceneHandler.LoadScene(parameters));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            Assert.AreEqual(previousSceneName, result["previousScene"].ToString());
        }
    }
}
