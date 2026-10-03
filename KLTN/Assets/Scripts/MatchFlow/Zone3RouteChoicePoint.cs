using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed class Zone3RouteChoicePoint : MonoBehaviour, IInteractable
    {
        private Zone3ConvoyController _convoy;
        private Zone3ConvoyRoutePoint _point;

        public string InteractionPrompt => $"ROUTE {_point}\nPRESS [E] TO SEND SPACEFRIGATE";

        public void Bind(Zone3ConvoyController convoy, Zone3ConvoyRoutePoint point)
        {
            _convoy = convoy;
            _point = point;
        }

        private void Update()
        {
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        public bool CanInteract(GameObject interactor) =>
            _convoy != null && _convoy.CanSelectRoute(interactor, _point);

        public void Interact(GameObject interactor) =>
            _convoy?.RequestRouteSelection(interactor, _point);
    }
}
