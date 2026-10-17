using System;
using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.Result
{
    /// <summary>Immutable data copied from the replicated match before the gameplay scene unloads.</summary>
    public sealed class ResultPlayerSnapshot
    {
        public string Name { get; }
        public string Status { get; }
        public bool Disconnected { get; }
        public int DownedCount { get; }
        public int ReviveCount { get; }

        public ResultPlayerSnapshot(string name, string status, bool disconnected,
            int downedCount, int reviveCount)
        {
            Name = name ?? string.Empty;
            Status = status ?? string.Empty;
            Disconnected = disconnected;
            DownedCount = downedCount;
            ReviveCount = reviveCount;
        }
    }

    public sealed class ResultSnapshot
    {
        public NetworkMatchResult Outcome { get; }
        public NetworkMatchEndReason EndReason { get; }
        public float DurationSeconds { get; }
        public float ObjectiveCompletion { get; }
        public int Survivors { get; }
        public int TeamSize { get; }
        public string MatchId { get; }
        public bool IsHost { get; }
        public IReadOnlyList<ResultPlayerSnapshot> Players { get; }

        public ResultSnapshot(NetworkMatchResult outcome, NetworkMatchEndReason endReason,
            float durationSeconds, float objectiveCompletion, int survivors, int teamSize,
            string matchId, bool isHost, IReadOnlyList<ResultPlayerSnapshot> players)
        {
            Outcome = outcome;
            EndReason = endReason;
            DurationSeconds = durationSeconds;
            ObjectiveCompletion = objectiveCompletion;
            Survivors = survivors;
            TeamSize = teamSize;
            MatchId = matchId ?? string.Empty;
            IsHost = isHost;
            Players = players ?? Array.Empty<ResultPlayerSnapshot>();
        }
    }

    /// <summary>Scene-independent result handoff. Only the replicated terminal match can populate it.</summary>
    public static class ResultSession
    {
        private static uint _capturedEndOrdinal;
        private static string _capturedMatchId;

        public static ResultSnapshot Current { get; private set; }

        public static void Clear()
        {
            Current = null;
            _capturedEndOrdinal = 0;
            _capturedMatchId = null;
            ResultTransitionFade.Clear();
        }

        public static void Capture(NetworkMatchState state)
        {
            if (state == null || !state.IsEnded || state.EndOrdinal == 0
                || state.Result == NetworkMatchResult.None)
                return;

            var authority = MatchAuthorityRuntime.Instance;
            string matchId = authority != null && authority.HasBinding
                ? authority.MatchId.ToString("D") : string.Empty;
            if (Current != null && _capturedEndOrdinal == state.EndOrdinal
                && string.Equals(_capturedMatchId, matchId, StringComparison.Ordinal))
                return;

            var players = new List<ResultPlayerSnapshot>(4);
            var states = UnityEngine.Object.FindObjectsByType<LobbyPlayerState>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var player in states)
            {
                if (player == null || !player.IsGameplayPlayer)
                    continue;

                string name = player.OperatorName.ToString().Trim();
                if (name.Length == 0) name = "UNKNOWN OPERATOR";
                bool disconnected = player.Disconnected;
                var life = player.GetComponent<NetworkPlayerLifeState>();
                int downs = life != null ? Mathf.Max(0, life.DownCount) : -1;
                int revives = life != null ? Mathf.Max(0, life.ReviveCount) : -1;
                string status = disconnected ? "DISCONNECTED" : life == null ? "UNKNOWN" : life.Status switch
                {
                    NetworkPlayerLifeStatus.Escaped => "SURVIVED",
                    NetworkPlayerLifeStatus.Eliminated => "DECEASED",
                    _ => "MISSING"
                };
                players.Add(new ResultPlayerSnapshot(name, status, disconnected, downs, revives));
            }

            players.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var runner = state.Runner;
            Current = new ResultSnapshot(state.Result, state.EndReason,
                state.FinalDurationSeconds, state.FinalObjectiveCompletion,
                state.FinalSurvivorCount, players.Count, matchId,
                runner != null && runner.IsServer, players.AsReadOnly());
            _capturedEndOrdinal = state.EndOrdinal;
            _capturedMatchId = matchId;
            ResultTransitionFade.Begin();
        }
    }

    /// <summary>Local fade for the short network scene handoff; removed when Result loads.</summary>
    internal sealed class ResultTransitionFade : MonoBehaviour
    {
        private static ResultTransitionFade _instance;
        private CanvasGroup _group;

        internal static void Begin()
        {
            if (_instance != null) return;
            var root = new GameObject("Result Transition Fade", typeof(RectTransform));
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<ResultTransitionFade>();
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            root.AddComponent<GraphicRaycaster>();
            _instance._group = root.AddComponent<CanvasGroup>();
            _instance._group.alpha = 0f;
            _instance._group.blocksRaycasts = false;
            var imageRoot = new GameObject("Black", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)imageRoot.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageRoot.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        internal static void Clear()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Update()
        {
            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Clear();
        }
    }
}
