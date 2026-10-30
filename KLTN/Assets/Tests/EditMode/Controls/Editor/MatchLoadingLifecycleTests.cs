using System;
using System.Linq;
using System.Reflection;
using EchoProtocol.Networking;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace EchoProtocol.Tests.EditMode.Controls
{
    /// <summary>Local lifecycle hooks only: no Photon session, authentication or scene loading.</summary>
    public sealed class MatchLoadingLifecycleTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject _root;
        private NetworkBootstrap _bootstrap;
        private NetworkRunner _runner;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.False);
            _root = new GameObject("MatchLoadingLifecycleTest");
            _root.SetActive(false);
            // Keep Awake/OnEnable dormant: tests must not create persistent services or contact a backend.
            _bootstrap = _root.AddComponent<NetworkBootstrap>();
            _runner = AddRunner("OwnedRunner");
            SetBootstrapProperty(nameof(NetworkBootstrap.Runner), _runner);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SceneFailure_OnlyClearsOwnedCallbackFlagAndPreservesFusionBusyScope()
        {
            var otherRunner = AddRunner("UnrelatedRunner");
            var sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            var loadingScope = typeof(NetworkSceneManagerDefault).GetField("_isLoading", InstanceMembers);
            Assert.That(loadingScope, Is.Not.Null);
            loadingScope.SetValue(sceneManager, true);
            SetBootstrapProperty(nameof(NetworkBootstrap.IsSceneLoading), true);
            SetBootstrapProperty(nameof(NetworkBootstrap.LastError), "previous error");

            try
            {
                _bootstrap.ReportSceneLoadFailure(otherRunner, "unrelated failure");
                Assert.That(_bootstrap.IsSceneLoading, Is.True);
                Assert.That(_bootstrap.LastError, Is.EqualTo("previous error"));

                _bootstrap.ReportSceneLoadFailure(_runner, "scene operation failed");
                Assert.That(_bootstrap.IsSceneLoading, Is.False);
                Assert.That(_bootstrap.LastError, Is.EqualTo("scene operation failed"));
                Assert.That(_bootstrap.Runner, Is.SameAs(_runner), "Failure observation must not tear down the runner.");
                Assert.That(sceneManager.IsBusy, Is.True,
                    "Clearing the callback flag must preserve Fusion's independent scene-idle fence.");
                Assert.That(_bootstrap.State, Is.EqualTo(NetworkSessionState.Disconnected));
            }
            finally { loadingScope.SetValue(sceneManager, false); }
        }

        [Test]
        public void RunnerComponents_ReplaceStockManagerPreserveOptionsAndRemainUnique()
        {
            var stock = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            stock.IsSceneTakeOverEnabled = false;
            stock.LogSceneLoadErrors = false;
            var ensure = typeof(NetworkBootstrap).GetMethod("EnsureRunnerComponents", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ensure, Is.Not.Null);

            ensure.Invoke(null, new object[] { _runner });
            var observed = _runner.GetComponent<MatchLoadingSceneManager>();
            Assert.That(observed, Is.Not.Null);
            Assert.That(stock == null, Is.True, "The uninitialized stock component should be replaced immediately.");
            Assert.That(observed.IsSceneTakeOverEnabled, Is.False);
            Assert.That(observed.LogSceneLoadErrors, Is.False);
            var provider = _runner.GetComponent<INetworkObjectProvider>();
            Assert.That(provider, Is.Not.Null);

            ensure.Invoke(null, new object[] { _runner });
            Assert.That(_runner.GetComponents<INetworkSceneManager>().Length, Is.EqualTo(1));
            Assert.That(_runner.GetComponents<INetworkObjectProvider>().Length, Is.EqualTo(1));
            Assert.That(_runner.GetComponent<MatchLoadingSceneManager>(), Is.SameAs(observed));
            Assert.That(_runner.GetComponent<INetworkObjectProvider>(), Is.SameAs(provider));
            Assert.That(_runner.IsRunning, Is.False, "Installing observation must not start a Photon session.");
        }

        [Test]
        public void FailedLoadingAbort_RejectsUnownedAttemptWithoutChangingSessionOrScene()
        {
            var originalScene = SceneManager.GetActiveScene();
            var originalState = _bootstrap.State;
            SetBootstrapProperty(nameof(NetworkBootstrap.IsSceneLoading), true);

            Assert.Throws<InvalidOperationException>(() =>
            {
                _bootstrap.AbortFailedMatchLoadingAsync(_runner, Guid.NewGuid().ToString("N"));
            });

            Assert.That(_bootstrap.Runner, Is.SameAs(_runner));
            Assert.That(_bootstrap.State, Is.EqualTo(originalState));
            Assert.That(_bootstrap.IsSceneLoading, Is.True);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(originalScene.handle));
            Assert.That(_runner.IsRunning, Is.False);
        }

        [Test]
        public void LoadingStatusRpc_HasFusionGeneratedReceiveInvoker()
        {
            bool woven = typeof(MatchLoadingRpc)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(method => Attribute.IsDefined(method, typeof(NetworkRpcWeavedInvokerAttribute)));

            Assert.That(woven, Is.True,
                "Fusion must weave the static loading RPC before host announcements can reach clients. Run this check in Unity.");
        }

        [Test]
        public void NetworkRecoveryYield_OnlyDismissesOwnedAttemptWithoutCompletingIt()
        {
            var coordinator = _root.AddComponent<MatchLoadingCoordinator>();
            typeof(MatchLoadingCoordinator).GetField("_runner", InstanceMembers).SetValue(coordinator, _runner);
            typeof(MatchLoadingCoordinator).GetField("_bootstrap", InstanceMembers).SetValue(coordinator, _bootstrap);
            var barrier = (EchoProtocol.Networking.Loading.MatchLoadingBarrier)typeof(MatchLoadingCoordinator)
                .GetField("_barrier", InstanceMembers).GetValue(coordinator);
            barrier.Begin(Guid.NewGuid().ToString("N"), "SciFi", 1, new[] { 1, 2 }, 0d);
            barrier.Authorize();
            barrier.StartScene();
            barrier.SetSceneProgress(0.4f);

            coordinator.YieldToNetworkRecovery(AddRunner("UnrelatedRecoveryRunner"));
            Assert.That(coordinator.Snapshot.IsVisible, Is.True);
            Assert.That(barrier.ReleaseReceived, Is.False);

            coordinator.YieldToNetworkRecovery(_runner);
            Assert.That(coordinator.Snapshot.IsVisible, Is.False);
            Assert.That(barrier.ReleaseReceived, Is.False, "Migration handoff must not report session readiness.");
            Assert.That(coordinator.Snapshot.Phase,
                Is.Not.EqualTo(EchoProtocol.Networking.Loading.MatchLoadingPhase.Complete));
            Assert.That(coordinator.Snapshot.SceneProgress, Is.EqualTo(0.4f));
            Assert.That(_bootstrap.Runner, Is.SameAs(_runner));
        }

        [Test]
        public void ReplacedRunner_DismissesObsoleteFailureWithoutChangingRecoveredSession()
        {
            var coordinator = _root.AddComponent<MatchLoadingCoordinator>();
            typeof(MatchLoadingCoordinator).GetField("_runner", InstanceMembers).SetValue(coordinator, _runner);
            typeof(MatchLoadingCoordinator).GetField("_bootstrap", InstanceMembers).SetValue(coordinator, _bootstrap);
            var barrier = (EchoProtocol.Networking.Loading.MatchLoadingBarrier)typeof(MatchLoadingCoordinator)
                .GetField("_barrier", InstanceMembers).GetValue(coordinator);
            barrier.Begin(Guid.NewGuid().ToString("N"), "SciFi", 1, new[] { 1, 2 }, 0d);
            barrier.Fail("The previous runner failed while loading.");
            var recoveredRunner = AddRunner("RecoveredRunner");
            SetBootstrapProperty(nameof(NetworkBootstrap.Runner), recoveredRunner);
            var originalState = _bootstrap.State;

            typeof(MatchLoadingCoordinator).GetMethod("Update", InstanceMembers).Invoke(coordinator, null);

            Assert.That(coordinator.Snapshot.IsVisible, Is.False,
                "An obsolete failure must not cover a session owned by the replacement runner.");
            Assert.That(barrier.ReleaseReceived, Is.False);
            Assert.That(_bootstrap.Runner, Is.SameAs(recoveredRunner));
            Assert.That(_bootstrap.State, Is.EqualTo(originalState));
            Assert.That(recoveredRunner.IsRunning, Is.False, "Retiring old UI must not start or stop the new runner.");
        }

        private NetworkRunner AddRunner(string name)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(_root.transform, false);
            return owner.AddComponent<NetworkRunner>();
        }

        private void SetBootstrapProperty(string name, object value)
        {
            var property = typeof(NetworkBootstrap).GetProperty(name, InstanceMembers);
            Assert.That(property, Is.Not.Null, name);
            var setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, name);
            setter.Invoke(_bootstrap, new[] { value });
        }
    }
}
