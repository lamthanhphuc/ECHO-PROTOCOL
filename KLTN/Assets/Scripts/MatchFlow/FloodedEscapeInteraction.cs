using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed class FloodedEscapeInteraction : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => EchoProtocol.Settings.GameLanguage.Choose("[E] LÊN TÀU SƠ TÁN", "[E] BOARD TO EVACUATE");
        public bool CanInteract(GameObject interactor)
        {
            var match = NetworkMatchState.Instance;
            return interactor && match && match.Object && match.Object.IsValid
                && !match.IsEnded && match.FloodedEscortArrived;
        }
        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor)) NetworkMatchState.Instance.RequestFloodedExit();
        }
    }
}
