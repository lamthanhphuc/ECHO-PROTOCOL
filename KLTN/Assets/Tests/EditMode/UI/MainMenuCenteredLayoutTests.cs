using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.Tests
{
    public sealed class MainMenuCenteredLayoutTests
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [Test]
        public void PrimaryContentSharesTheScreenCenterAxis()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            try
            {
                RectTransform root = RequireRect("MainMenuCanvas/MainMenuPanel");
                AssertCentered(root, "MainMenuPanel");

                AssertCenteredLabel(root, "HeaderText");
                AssertCenteredLabel(root, "SystemLabel");

                AssertCenteredButton(root, "HostGameButton");
                AssertCenteredButton(root, "JoinGameButton");
                AssertCenteredButton(root, "OptionsButton");
                AssertCenteredButton(root, "LogoutButton");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) && previousPath != scene.path)
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void PrimaryButtonsHaveExactlyOneDirectTextLabel()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            try
            {
                RectTransform root = RequireRect("MainMenuCanvas/MainMenuPanel");
                AssertSingleDirectTextLabel(root, "HostGameButton");
                AssertSingleDirectTextLabel(root, "JoinGameButton");
                AssertSingleDirectTextLabel(root, "OptionsButton");
                AssertSingleDirectTextLabel(root, "LogoutButton");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) && previousPath != scene.path)
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void PrimaryTitleHasExactlyOneDirectHeaderLabel()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            try
            {
                RectTransform root = RequireRect("MainMenuCanvas/MainMenuPanel");
                int count = 0;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child.name == "HeaderText" &&
                        (child.GetComponent<Text>() != null || child.GetComponent<TMP_Text>() != null))
                        count++;
                }

                Assert.That(count, Is.EqualTo(1),
                    "MainMenuPanel must not contain overlapping HeaderText labels.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) && previousPath != scene.path)
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void PrimaryButtonsHaveMirroredSignalIndicators()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            try
            {
                RectTransform root = RequireRect("MainMenuCanvas/MainMenuPanel");
                foreach (string name in new[]
                {
                    "HostGameButton", "JoinGameButton", "OptionsButton", "LogoutButton"
                })
                {
                    RectTransform button = root.Find(name) as RectTransform;
                    Assert.That(button, Is.Not.Null, name + " is missing.");
                    RectTransform left = button.Find("HoverSignalIndicator") as RectTransform;
                    RectTransform right = button.Find("RightHoverSignalIndicator") as RectTransform;
                    Assert.That(left, Is.Not.Null, name + " left indicator is missing.");
                    Assert.That(right, Is.Not.Null, name + " right indicator is missing.");
                    Assert.That(left.sizeDelta, Is.EqualTo(right.sizeDelta),
                        name + " indicator sizes differ.");
                    float leftCenter = left.anchoredPosition.x + left.sizeDelta.x * 0.5f;
                    float rightCenter = right.anchoredPosition.x + right.sizeDelta.x * 0.5f;
                    Assert.That(leftCenter + rightCenter,
                        Is.EqualTo(button.rect.width).Within(0.001f),
                        name + " indicators are not mirrored.");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) && previousPath != scene.path)
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        private static RectTransform RequireRect(string path)
        {
            GameObject target = GameObject.Find(path);
            Assert.That(target, Is.Not.Null, path + " is missing.");
            return target.GetComponent<RectTransform>();
        }

        private static void AssertCentered(RectTransform rect, string label)
        {
            Assert.That(rect.anchorMin.x, Is.EqualTo(0.5f).Within(0.001f), label + " anchorMin");
            Assert.That(rect.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f), label + " anchorMax");
            Assert.That(rect.pivot.x, Is.EqualTo(0.5f).Within(0.001f), label + " pivot");
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f), label + " position");
        }

        private static void AssertCenteredLabel(RectTransform root, string name)
        {
            RectTransform rect = root.Find(name) as RectTransform;
            Assert.That(rect, Is.Not.Null, name + " is missing.");
            AssertCentered(rect, name);

            Text legacy = rect.GetComponent<Text>();
            if (legacy != null)
                Assert.That(legacy.alignment, Is.EqualTo(TextAnchor.MiddleCenter));

            TMP_Text tmp = rect.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                Assert.That(tmp.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        private static void AssertCenteredButton(RectTransform root, string name)
        {
            RectTransform rect = root.Find(name) as RectTransform;
            Assert.That(rect, Is.Not.Null, name + " is missing.");
            AssertCentered(rect, name);

            Text legacy = rect.GetComponentInChildren<Text>(true);
            if (legacy != null)
                Assert.That(legacy.alignment, Is.EqualTo(TextAnchor.MiddleCenter));

            TMP_Text tmp = rect.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                Assert.That(tmp.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        private static void AssertSingleDirectTextLabel(RectTransform root, string name)
        {
            Transform button = root.Find(name);
            Assert.That(button, Is.Not.Null, name + " is missing.");

            int count = 0;
            for (int i = 0; i < button.childCount; i++)
            {
                Transform child = button.GetChild(i);
                if (child.name == "Text" &&
                    (child.GetComponent<Text>() != null || child.GetComponent<TMP_Text>() != null))
                    count++;
            }

            Assert.That(count, Is.EqualTo(1), name + " must not contain overlapping Text labels.");
        }
    }
}
