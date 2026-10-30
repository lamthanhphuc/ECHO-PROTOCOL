using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.Networking.Loading
{
    public enum MatchLoadingPhase
    {
        BackendAuthorization,
        NetworkSynchronization,
        SceneLoading,
        GameplayReadiness,
        Complete,
        Failed
    }

    public readonly struct MatchLoadingSnapshot
    {
        public bool IsVisible { get; }
        public MatchLoadingPhase Phase { get; }
        public string SceneName { get; }
        /// <summary>Raw local scene operation progress, or -1 when unavailable.</summary>
        public float SceneProgress { get; }
        public bool LocalReady { get; }
        public int ReadyPlayers { get; }
        public int RequiredPlayers { get; }
        public string Error { get; }

        public MatchLoadingSnapshot(bool isVisible, MatchLoadingPhase phase, string sceneName,
            float sceneProgress, bool localReady, int readyPlayers, int requiredPlayers, string error)
        {
            IsVisible = isVisible;
            Phase = phase;
            SceneName = sceneName;
            SceneProgress = sceneProgress;
            LocalReady = localReady;
            ReadyPlayers = readyPlayers;
            RequiredPlayers = requiredPlayers;
            Error = error;
        }
    }

    /// <summary>
    /// Tracks one frozen match-loading attempt without Unity or transport dependencies.
    /// The transport must authenticate the source of host announcements and ready aggregates.
    /// Scene progress describes the local scene operation; only a host release completes loading.
    /// </summary>
    public sealed class MatchLoadingBarrier
    {
        private readonly HashSet<int> _requiredPlayers = new HashSet<int>();
        private readonly HashSet<int> _readyPlayers = new HashSet<int>();
        private readonly List<int> _requiredOrder = new List<int>();
        private readonly List<int> _readyOrder = new List<int>();
        private readonly ReadOnlyCollection<int> _requiredView;
        private readonly ReadOnlyCollection<int> _readyView;

        public string AttemptId { get; private set; } = string.Empty;
        public string SceneName { get; private set; } = string.Empty;
        public int HostPlayer { get; private set; }
        public IReadOnlyCollection<int> RequiredPlayers => _requiredView;
        public IReadOnlyCollection<int> ReadyPlayers => _readyView;
        public bool BackendAuthorized { get; private set; }
        public bool SceneStarted { get; private set; }
        public bool LocalSceneLoaded { get; private set; }
        public bool LocalReady { get; private set; }
        public bool ReleaseReceived { get; private set; }
        public bool IsVisible { get; private set; }
        public string Error { get; private set; } = string.Empty;
        public float SceneProgress { get; private set; } = -1f;
        public double StartedAt { get; private set; }

        public MatchLoadingPhase Phase
        {
            get
            {
                if (Error.Length != 0) return MatchLoadingPhase.Failed;
                if (ReleaseReceived) return MatchLoadingPhase.Complete;
                if (LocalSceneLoaded) return MatchLoadingPhase.GameplayReadiness;
                if (SceneStarted) return MatchLoadingPhase.SceneLoading;
                return BackendAuthorized
                    ? MatchLoadingPhase.NetworkSynchronization
                    : MatchLoadingPhase.BackendAuthorization;
            }
        }

        public MatchLoadingSnapshot Snapshot => new MatchLoadingSnapshot(
            IsVisible, Phase, SceneName, SceneProgress, LocalReady,
            _readyPlayers.Count, _requiredPlayers.Count, Error);

        private bool CanAdvance => IsVisible && Error.Length == 0 && !ReleaseReceived;

        public MatchLoadingBarrier()
        {
            _requiredView = _requiredOrder.AsReadOnly();
            _readyView = _readyOrder.AsReadOnly();
        }

        public void Begin(string attemptId, string sceneName, int hostPlayer,
            IEnumerable<int> roster, double now)
        {
            if (string.IsNullOrWhiteSpace(attemptId))
                throw new ArgumentException("A loading attempt ID is required.", nameof(attemptId));
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("A gameplay scene is required.", nameof(sceneName));
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (double.IsNaN(now) || double.IsInfinity(now))
                throw new ArgumentOutOfRangeException(nameof(now));

            var required = new HashSet<int>(roster);
            if (required.Count == 0 || !required.Contains(hostPlayer))
                throw new ArgumentException("The loading roster must include its host.", nameof(roster));

            AttemptId = attemptId;
            SceneName = sceneName;
            HostPlayer = hostPlayer;
            StartedAt = now;
            _requiredPlayers.Clear();
            _requiredOrder.Clear();
            foreach (var player in required)
            {
                _requiredPlayers.Add(player);
                _requiredOrder.Add(player);
            }
            _requiredOrder.Sort();
            _readyPlayers.Clear();
            _readyOrder.Clear();
            BackendAuthorized = false;
            SceneStarted = false;
            LocalSceneLoaded = false;
            LocalReady = false;
            ReleaseReceived = false;
            SceneProgress = -1f;
            Error = string.Empty;
            IsVisible = true;
        }

        public bool Matches(string attempt, string scene) => IsVisible
            && string.Equals(AttemptId, attempt, StringComparison.Ordinal)
            && string.Equals(SceneName, scene, StringComparison.Ordinal);

        public void Authorize()
        {
            if (CanAdvance) BackendAuthorized = true;
        }

        public void StartScene()
        {
            if (CanAdvance) SceneStarted = true;
        }

        public void SetSceneProgress(float progress)
        {
            if (!CanAdvance || !SceneStarted || LocalSceneLoaded
                || float.IsNaN(progress) || float.IsInfinity(progress)) return;
            SceneProgress = progress < 0f ? -1f : Math.Min(1f, progress);
        }

        public void SceneLoaded()
        {
            if (!CanAdvance || !SceneStarted) return;
            LocalSceneLoaded = true;
            SceneProgress = 1f;
        }

        public void SetLocalReady()
        {
            if (CanAdvance && BackendAuthorized && LocalSceneLoaded) LocalReady = true;
        }

        public bool Acknowledge(string attempt, string scene, int sender)
        {
            if (!CanAdvance || !Matches(attempt, scene) || !_requiredPlayers.Contains(sender))
                return false;
            if (_readyPlayers.Add(sender)) _readyOrder.Add(sender);
            return true;
        }

        public bool TryRelease(string attempt, string scene, int sender)
        {
            if (!IsVisible || Error.Length != 0 || !Matches(attempt, scene) || sender != HostPlayer
                || !BackendAuthorized || !SceneStarted || !LocalSceneLoaded || !LocalReady
                || _readyPlayers.Count != _requiredPlayers.Count) return false;
            ReleaseReceived = true;
            return true;
        }

        public void Fail(string error)
        {
            if (!CanAdvance) return;
            Error = string.IsNullOrWhiteSpace(error) ? "Match loading failed." : error;
        }

        public void Dismiss()
        {
            IsVisible = false;
        }

        public bool HasTimedOut(double now, double timeout) => CanAdvance
            && !double.IsNaN(now) && !double.IsInfinity(now)
            && !double.IsNaN(timeout) && !double.IsInfinity(timeout)
            && timeout > 0d && now - StartedAt >= timeout;
    }
}
