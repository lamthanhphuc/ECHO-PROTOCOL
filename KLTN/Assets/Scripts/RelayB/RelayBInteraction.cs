using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RelayBInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private RelayBController controller;
        [SerializeField] private string prompt = "Synchronize Data Relay B";
        [SerializeField] private string onlinePrompt = "Data Relay B - Online";
        [SerializeField] private bool allowInspectWhenOnline = true;

        public string InteractionPrompt => controller != null && controller.IsOnline ? onlinePrompt : prompt;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<RelayBController>();
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (controller == null || interactor == null)
            {
                return false;
            }

            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            bool networked = matchState != null && matchState.Object != null && matchState.Object.IsValid;
            if (!networked)
            {
                return allowInspectWhenOnline || !controller.IsOnline;
            }

            if (controller.IsOnline)
            {
                return allowInspectWhenOnline;
            }

            if (matchState.CurrentPhase != NetworkMatchPhase.Zone2Objective
                || matchState.Zone2Stage != Zone2MissionStage.RepairRelays)
            {
                return false;
            }

            var director = Zone2MissionDirector.Instance;
            if (director == null || !director.TryGetRelaySlot(controller, out var slot))
            {
                return false;
            }

            int bit = 1 << (int)slot;
            return (matchState.RelayCompletionMask & bit) == 0
                && director.CanLocalPlayerOperateRelay(controller);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            controller.OpenUI(interactor);
        }
    }
}

