using System.Collections.Generic;
using EchoProtocol.UI.Loading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace EchoProtocol.Tests.EditMode.Loading
{
    public sealed class LoadingUiNavigationLockTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private LoadingUiNavigationLock _cinematic, _feedback;
        private EventSystem _events;
        private GameObject _selection;

        [SetUp]
        public void SetUp()
        {
            _cinematic = new LoadingUiNavigationLock();
            _feedback = new LoadingUiNavigationLock();
            _events = CreateEventSystem("LoadingNavigationTestEvents");
            _selection = CreateObject("OriginalLobbySelection");
            _events.SetSelectedGameObject(_selection);
        }

        [TearDown]
        public void TearDown()
        {
            _cinematic.Release();
            _feedback.Release();
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OverlappingPresentersRestoreOriginalStateOnlyAfterLastOwnerLeaves(bool cinematicLeavesFirst)
        {
            _cinematic.Acquire(_events);
            _feedback.Acquire(_events);
            // Reasserting an existing owner must not add another reference or capture false as the baseline.
            _cinematic.Acquire(_events);
            AssertBlocked(_events);

            (cinematicLeavesFirst ? _cinematic : _feedback).Release();
            AssertBlocked(_events);
            (cinematicLeavesFirst ? _feedback : _cinematic).Release();

            Assert.That(_events.sendNavigationEvents, Is.True);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(_selection));
        }

        [Test]
        public void HandoffWithoutOverlapRestoresAndCapturesTheSameOriginalFocus()
        {
            _cinematic.Acquire(_events);
            _cinematic.Release();
            _feedback.Acquire(_events);
            AssertBlocked(_events);
            _feedback.Release();
            Assert.That(_events.sendNavigationEvents, Is.True);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(_selection));
        }

        [Test]
        public void InitiallyDisabledNavigationRemainsDisabledAfterBothOwnersLeave()
        {
            _events.sendNavigationEvents = false;
            _feedback.Acquire(_events);
            _cinematic.Acquire(_events);
            _feedback.Release();
            _cinematic.Release();
            Assert.That(_events.sendNavigationEvents, Is.False);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(_selection));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FinalReleaseSkipsDestroyedOrInactiveOriginalSelection(bool destroy)
        {
            _cinematic.Acquire(_events);
            _feedback.Acquire(_events);
            if (destroy) Object.DestroyImmediate(_selection);
            else _selection.SetActive(false);
            _cinematic.Release();
            Assert.DoesNotThrow(() => _feedback.Release());
            Assert.That(_events.sendNavigationEvents, Is.True);
            Assert.That(_events.currentSelectedGameObject == null, Is.True);
        }

        [Test]
        public void ActiveOwnerReassertsBlockingWithoutReplacingTheOriginalState()
        {
            _cinematic.Acquire(_events);
            _events.sendNavigationEvents = true;
            _events.SetSelectedGameObject(CreateObject("AccidentalUnderlyingSelection"));
            _cinematic.Acquire(_events);
            AssertBlocked(_events);
            _cinematic.Release();
            _cinematic.Release();
            Assert.That(_events.sendNavigationEvents, Is.True);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(_selection));
        }

        [Test]
        public void EventSystemReplacementKeepsEachOriginalStateIndependent()
        {
            _cinematic.Acquire(_events);
            _feedback.Acquire(_events);
            var replacement = CreateEventSystem("ReplacementSceneEvents");
            var replacementSelection = CreateObject("ReplacementSceneSelection");
            replacement.SetSelectedGameObject(replacementSelection);
            _cinematic.Acquire(replacement);
            AssertBlocked(_events);
            AssertBlocked(replacement);
            _feedback.Release();
            Assert.That(_events.sendNavigationEvents, Is.True);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(_selection));
            AssertBlocked(replacement);
            _cinematic.Release();
            Assert.That(replacement.sendNavigationEvents, Is.True);
            Assert.That(replacement.currentSelectedGameObject, Is.SameAs(replacementSelection));
        }

        [Test]
        public void DestroyedEventSystemCanBeReleasedBeforeAcquiringTheNextScene()
        {
            _cinematic.Acquire(_events);
            Object.DestroyImmediate(_events.gameObject);
            Assert.DoesNotThrow(() => _cinematic.Release());
            var replacement = CreateEventSystem("NextSceneEvents");
            _cinematic.Acquire(replacement);
            AssertBlocked(replacement);
            _cinematic.Release();
            Assert.That(replacement.sendNavigationEvents, Is.True);
        }

        private GameObject CreateObject(string name)
        {
            var owner = new GameObject(name);
            _objects.Add(owner);
            return owner;
        }

        private EventSystem CreateEventSystem(string name) => CreateObject(name).AddComponent<EventSystem>();

        private static void AssertBlocked(EventSystem events)
        {
            Assert.That(events.sendNavigationEvents, Is.False);
            Assert.That(events.currentSelectedGameObject == null, Is.True);
        }
    }
}
