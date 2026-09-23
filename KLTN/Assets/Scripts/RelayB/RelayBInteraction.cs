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

            return allowInspectWhenOnline || !controller.IsOnline;
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

