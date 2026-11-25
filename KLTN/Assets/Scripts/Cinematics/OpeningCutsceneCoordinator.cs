using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Fusion;

namespace EchoProtocol.Cinematics
{
    /// <summary>Local observation only. All lifecycle transitions and votes live in NetworkMatchState.</summary>
    public sealed class OpeningCutsceneCoordinator : MonoBehaviour
    {
        public static bool IsGameplayBlocked
        {
            get
            {
                var match = NetworkMatchState.Instance;
                return match != null && match.Object != null && match.Object.IsValid && !match.IsGameplayReleased;
            }
        }

        private NetworkMatchState _reportedMatch;
        private readonly Dictionary<NavMeshAgent, bool> _stoppedAgents = new Dictionary<NavMeshAgent, bool>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OpeningCutsceneCoordinator>() != null) return;
            DontDestroyOnLoad(new GameObject("OpeningCutsceneCoordinator", typeof(OpeningCutsceneCoordinator)));
        }

        private void Update()
        {
            if (IsGameplayBlocked)
            {
                foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude))
                {
                    var obj = agent.GetComponent<NetworkObject>();
                    if (obj == null || !obj.IsValid || !obj.HasStateAuthority || !agent.isOnNavMesh) continue;
                    if (!_stoppedAgents.ContainsKey(agent)) _stoppedAgents.Add(agent, agent.isStopped);
                    agent.isStopped = true;
                }
            }
            else RestoreNavigation();
            var match = NetworkMatchState.Instance;
            if (match == null || match.Object == null || !match.Object.IsValid || !match.HasOpeningCutscene
                || match.OpeningState != Networking.Loading.OpeningCutsceneState.Playing
                || NetworkBootstrap.IsHostMigrationReconciliationPending) return;
            var controller = OpeningCutsceneController.FindForScene(match.gameObject.scene);
            if (controller != null && controller.IsPlayable && controller.isActiveAndEnabled) return;
            if (_reportedMatch == match) return;
            _reportedMatch = match;
            match.ReportOpeningUnavailable();
        }

        private void RestoreNavigation()
        {
            foreach (var entry in _stoppedAgents)
                if (entry.Key != null && entry.Key.isActiveAndEnabled && entry.Key.isOnNavMesh) entry.Key.isStopped = entry.Value;
            _stoppedAgents.Clear();
        }

        private void OnDisable() => RestoreNavigation();
    }
}
