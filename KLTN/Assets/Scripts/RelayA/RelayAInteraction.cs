using UnityEngine;

namespace EchoProtocol.RelayA
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RelayAInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private RelayAController controller;
        [SerializeField] private string prompt = "Stabilize Power Relay A";
        [SerializeField] private string onlinePrompt = "Power Relay A - Online";
        [SerializeField] private bool allowInspectWhenOnline = true;

        public string InteractionPrompt => controller != null && controller.IsOnline ? onlinePrompt : prompt;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<RelayAController>();
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
