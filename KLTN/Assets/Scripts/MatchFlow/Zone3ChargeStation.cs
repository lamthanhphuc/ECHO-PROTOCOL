using UnityEngine;

namespace EchoProtocol.Networking
{
    [RequireComponent(typeof(Collider))]
    public sealed class Zone3ChargeStation : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "Activate Scifi Charge";

        public bool CanInteract(GameObject interactor) =>
            Zone3MissionDirector.Instance?.CanActivateCharge(interactor) == true;

        public void Interact(GameObject interactor) =>
            Zone3MissionDirector.Instance?.ActivateCharge(interactor);
    }
}
