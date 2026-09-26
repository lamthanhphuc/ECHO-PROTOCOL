using EchoProtocol.Networking;
using EchoProtocol.UI.HUD;
using Fusion;
using UnityEngine;

namespace EchoProtocol.MatchFlow
{
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public sealed class ObjectiveZone1ExitTrigger : MonoBehaviour
    {
        private bool _triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || other == null) return;

            var localMovement = other.GetComponentInParent<PlayerMovement>();
            var networkMovement = other.GetComponentInParent<NetworkPlayerMovement>();
            if (localMovement == null && networkMovement == null) return;

            var networkObject = other.GetComponentInParent<NetworkObject>();
            if (networkObject != null && networkObject.IsValid && !networkObject.HasInputAuthority) return;

            var objectiveTrackers = FindObjectsByType<HUDObjectiveTracker>(FindObjectsInactive.Exclude);
            if (objectiveTrackers.Length == 0)
            {
                Debug.LogWarning("[ObjectiveZone1ExitTrigger] No active HUDObjectiveTracker was found.", this);
                return;
            }

            foreach (var objectiveTracker in objectiveTrackers)
            {
                objectiveTracker.HideZone1Objective();
            }

            _triggered = true;
            Debug.Log("[ObjectiveZone1ExitTrigger] Local player entered the doorway; Zone 1 objective hidden.", this);
        }
    }
}
