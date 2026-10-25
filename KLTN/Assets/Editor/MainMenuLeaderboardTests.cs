using EchoProtocol.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Tests
{
    public sealed class MainMenuLeaderboardTests
    {
        private GameObject _canvasObject;
        private MainMenuLeaderboardController _archive;
        private Button _profile;

        [SetUp]
        public void SetUp()
        {
            _canvasObject = new GameObject("MainMenuCanvas",
                typeof(RectTransform), typeof(Canvas));
            var store = new GameObject("StorePanel", typeof(RectTransform));
            store.transform.SetParent(_canvasObject.transform, false);
            var profileObject = new GameObject("ProfileButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            profileObject.transform.SetParent(store.transform, false);
            var rect = (RectTransform)profileObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(-54f, -4f);
            rect.sizeDelta = new Vector2(44f, 44f);
            _profile = profileObject.GetComponent<Button>();

            var menu = new GameObject("MainMenuPanel",
                typeof(RectTransform), typeof(CanvasGroup));
            menu.transform.SetParent(_canvasObject.transform, false);
            _archive = MainMenuLeaderboardController.Install(
                _canvasObject.GetComponent<Canvas>(), loadRecords: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);
        }

        [Test]
        public void TrophyAlignsLeftOfProfileWithoutReplacingProfileButton()
        {
            Assert.That(_archive, Is.Not.Null);
            var trophy = (RectTransform)_archive.TrophyButton.transform;
            var profile = (RectTransform)_profile.transform;
            Assert.That(trophy.parent, Is.EqualTo(profile.parent));
            Assert.That(trophy.anchoredPosition.y,
                Is.EqualTo(profile.anchoredPosition.y));
            Assert.That(profile.anchoredPosition.x
                - (trophy.anchoredPosition.x + trophy.rect.width),
                Is.EqualTo(11f).Within(0.01f));
            Assert.That(profile.GetComponent<Button>(), Is.SameAs(_profile));
        }

        [Test]
        public void TrophyOpensOverlayAndBackdropRestoresMenuInput()
        {
            var menu = _canvasObject.transform.Find("MainMenuPanel")
                .GetComponent<CanvasGroup>();
            _archive.TrophyButton.onClick.Invoke();

            Assert.That(_archive.IsOpen, Is.True);
            Assert.That(_archive.OverlayRoot.GetSiblingIndex(),
                Is.EqualTo(_archive.OverlayRoot.parent.childCount - 1));
            Assert.That(_archive.OverlayGroup.blocksRaycasts, Is.True);
            Assert.That(menu.interactable, Is.False);
            Assert.That(menu.blocksRaycasts, Is.False);
            Assert.That(_archive.OverlayRoot.Find("DimBackground")
                .GetComponent<Image>().raycastTarget, Is.True);

            _archive.BackdropButton.onClick.Invoke();

            Assert.That(_archive.IsOpen, Is.False);
            Assert.That(menu.interactable, Is.True);
            Assert.That(menu.blocksRaycasts, Is.True);
        }

        [Test]
        public void CloseButtonAndEscapeBothDismissOverlay()
        {
            _archive.Open();
            _archive.CloseButton.onClick.Invoke();
            Assert.That(_archive.IsOpen, Is.False);

            _archive.Open();
            _archive.ProcessEscapePressed(true);
            Assert.That(_archive.IsOpen, Is.False);
        }

        [Test]
        public void TabsAndFiltersRemainIndependent()
        {
            _archive.Open();
            Assert.That(_archive.CurrentMode, Is.EqualTo(LeaderboardMode.Solo));
            Assert.That(_archive.SelectedMap, Is.EqualTo("SciFi"));
            Assert.That(_archive.SelectedDifficulty, Is.EqualTo("NORMAL"));

            _archive.OverlayRoot.Find("ArchivePanel/TeamTab")
                .GetComponent<Button>().onClick.Invoke();
            _archive.OverlayRoot.Find("ArchivePanel/MapFilter")
                .GetComponent<Button>().onClick.Invoke();
            _archive.OverlayRoot.Find("ArchivePanel/DifficultyFilter")
                .GetComponent<Button>().onClick.Invoke();

            Assert.That(_archive.CurrentMode, Is.EqualTo(LeaderboardMode.Team));
            Assert.That(_archive.SelectedMap, Is.EqualTo("Scene_A"));
            Assert.That(_archive.SelectedDifficulty, Is.EqualTo("HARD"));
        }

        [Test]
        public void EntriesSortAscendingAndFormatAsMinutesDotSeconds()
        {
            var entries = new[]
            {
                new LeaderboardEntryDto { rank = 3, durationCentiseconds = 32700 },
                new LeaderboardEntryDto { rank = 1, durationCentiseconds = 18100 },
                new LeaderboardEntryDto { rank = 2, durationCentiseconds = 23500 }
            };
            var sorted = LeaderboardPresentation.OrderEntries(entries);

            Assert.That(sorted[0].rank, Is.EqualTo(1));
            Assert.That(sorted[1].rank, Is.EqualTo(2));
            Assert.That(sorted[2].rank, Is.EqualTo(3));
            Assert.That(LeaderboardPresentation.FormatDuration(32700),
                Is.EqualTo("05.27"));
            Assert.That(entries[0].rank, Is.EqualTo(3));
        }

        [Test]
        public void RequestKeepsModeMapDifficultyAndPageSeparate()
        {
            string solo = LeaderboardApiService.BuildEndpoint(
                LeaderboardMode.Solo, "SciFi", "NORMAL", 1, 10);
            string team = LeaderboardApiService.BuildEndpoint(
                LeaderboardMode.Team, "Scene_A", "HARD", 2, 10);

            Assert.That(solo, Does.Contain("mode=SOLO"));
            Assert.That(solo, Does.Contain("mapId=SciFi"));
            Assert.That(solo, Does.Contain("difficulty=NORMAL"));
            Assert.That(team, Does.Contain("mode=TEAM"));
            Assert.That(team, Does.Contain("mapId=Scene_A"));
            Assert.That(team, Does.Contain("difficulty=HARD"));
            Assert.That(team, Does.Contain("page=2"));
        }

        [Test]
        public void ArchiveResourcesContainArtAndBindableRowPrefab()
        {
            var visuals = Resources.Load<LeaderboardArchiveVisuals>(
                "LeaderboardArchive/Visuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.dossierMetal, Is.Not.Null);
            Assert.That(visuals.surveillanceStill, Is.Not.Null);
            Assert.That(visuals.tornNote, Is.Not.Null);
            Assert.That(visuals.agedTape, Is.Not.Null);
            Assert.That(visuals.fingerprint, Is.Not.Null);
            Assert.That(visuals.operatorPortrait, Is.Not.Null);
            Assert.That(visuals.spectralFont, Is.Not.Null);
            Assert.That(visuals.hoverClip, Is.Not.Null);

            var row = Resources.Load<GameObject>(
                "LeaderboardArchive/LeaderboardRow");
            Assert.That(row, Is.Not.Null);
            Assert.That(row.GetComponent<RectTransform>(), Is.Not.Null);
            Assert.That(row.GetComponent<Image>(), Is.Not.Null);
            Assert.That(row.GetComponent<LeaderboardRowView>(), Is.Not.Null);
            AssertChildComponent<Image>(row, "MedalRule");
            AssertChildComponent<Text>(row, "Rank");
            AssertChildComponent<Image>(row, "Avatar");
            AssertChildComponent<Text>(row, "Avatar/AvatarInitials");
            AssertChildComponent<Text>(row, "Name");
            AssertChildComponent<Text>(row, "Duration");
        }

        [Test]
        public void RecordsUseAMaskedVerticalScrollViewport()
        {
            var viewport = _archive.OverlayRoot.Find(
                "ArchivePanel/RecordsViewport");
            Assert.That(viewport, Is.Not.Null);
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            var scroll = viewport.GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport, Is.SameAs(viewport));
            Assert.That(scroll.content, Is.SameAs(viewport.Find("Entries")));
        }

        [Test]
        public void EmptyAndConnectionErrorHaveDistinctArchiveStates()
        {
            var viewport = _archive.OverlayRoot.Find(
                "ArchivePanel/RecordsViewport");
            var message = viewport.Find("BodyMessage").GetComponent<Text>();
            var retry = viewport.Find("RetryButton").gameObject;
            var radio = viewport.Find("RadioSignal").gameObject;
            var entries = viewport.Find("Entries").gameObject;

            _archive.PresentState(
                MainMenuLeaderboardController.ArchiveState.Empty);
            Assert.That(message.text, Does.Contain("NO EXTRACTIONS"));
            Assert.That(retry.activeSelf, Is.False);
            Assert.That(radio.activeSelf, Is.False);
            Assert.That(entries.activeSelf, Is.False);

            _archive.PresentState(
                MainMenuLeaderboardController.ArchiveState.Error);
            Assert.That(message.text, Does.Contain("UNABLE TO REACH"));
            Assert.That(retry.activeSelf, Is.True);
            Assert.That(radio.activeSelf, Is.True);
            Assert.That(entries.activeSelf, Is.False);

            _archive.PresentState(
                MainMenuLeaderboardController.ArchiveState.Records, 3);
            Assert.That(entries.activeSelf, Is.True);
            Assert.That(retry.activeSelf, Is.False);
        }

        [Test]
        public void LoadingAndConnectionErrorDisablePagination()
        {
            var panel = _archive.OverlayRoot.Find("ArchivePanel");
            var previous = panel.Find("PreviousPage").GetComponent<Button>();
            var next = panel.Find("NextPage").GetComponent<Button>();

            previous.interactable = true;
            next.interactable = true;
            _archive.PresentState(
                MainMenuLeaderboardController.ArchiveState.Loading);
            Assert.That(previous.interactable, Is.False);
            Assert.That(next.interactable, Is.False);

            previous.interactable = true;
            next.interactable = true;
            _archive.PresentState(
                MainMenuLeaderboardController.ArchiveState.Error);
            Assert.That(previous.interactable, Is.False);
            Assert.That(next.interactable, Is.False);
        }

        [Test]
        public void SafeAreaMovesBothUtilityIconsTogetherAndRestoresFullScreenPosition()
        {
            var profile = (RectTransform)_profile.transform;
            var trophy = (RectTransform)_archive.TrophyButton.transform;
            var originalPosition = profile.anchoredPosition;
            var safe = new Rect(90f, 40f, 1650f, 920f);

            _archive.ApplySafeArea(safe, new Vector2(1920f, 1080f));

            Assert.That(profile.anchoredPosition.x,
                Is.LessThan(originalPosition.x));
            Assert.That(profile.anchoredPosition.y,
                Is.LessThan(originalPosition.y));
            Assert.That(trophy.anchoredPosition.y,
                Is.EqualTo(profile.anchoredPosition.y));
            Assert.That(profile.anchoredPosition.x
                - (trophy.anchoredPosition.x + trophy.rect.width),
                Is.EqualTo(11f).Within(0.01f));
            var panel = (RectTransform)_archive.OverlayRoot.Find("ArchivePanel");
            Assert.That(panel.anchorMin.x,
                Is.EqualTo(safe.center.x / 1920f).Within(0.001f));
            Assert.That(panel.anchorMin.y,
                Is.EqualTo(safe.center.y / 1080f).Within(0.001f));

            _archive.ApplySafeArea(new Rect(0f, 0f, 1920f, 1080f),
                new Vector2(1920f, 1080f));
            Assert.That(profile.anchoredPosition,
                Is.EqualTo(originalPosition));
        }

        [Test]
        public void PanelScaleFitsInsideSmallAvailableArea()
        {
            float scale = MainMenuLeaderboardController.ComputePanelScale(
                900f, 600f);
            var panel = (RectTransform)_archive.OverlayRoot.Find("ArchivePanel");

            Assert.That(scale, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(panel.rect.width * scale,
                Is.LessThanOrEqualTo(860.01f));
            Assert.That(panel.rect.height * scale,
                Is.LessThanOrEqualTo(560.01f));
        }

        private static void AssertChildComponent<T>(GameObject root, string path)
            where T : Component
        {
            var child = root.transform.Find(path);
            Assert.That(child, Is.Not.Null, path + " is missing from the row prefab");
            Assert.That(child.GetComponent<T>(), Is.Not.Null,
                path + " lacks " + typeof(T).Name);
        }
    }
}
