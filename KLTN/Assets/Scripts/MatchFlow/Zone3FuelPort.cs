using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.MatchFlow
{
    /// <summary>
    /// Placeholder fuel port receiver for a future Zone 3 fuel redesign.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Zone3FuelPort : MonoBehaviour, IInteractable
    {
        private Zone3ConvoyController _convoy;

        public string InteractionPrompt => "FUEL PORT OFFLINE";

        private void Awake()
        {
            _convoy = GetComponentInParent<Zone3ConvoyController>();
        }

        private Zone3ConvoyController GetConvoy()
        {
            if (_convoy == null)
            {
                _convoy = GetComponentInParent<Zone3ConvoyController>()
                    ?? Zone3MissionDirector.Instance?.Convoy;
            }
            return _convoy;
        }

        public bool CanInteract(GameObject interactor)
        {
            return false;
        }

        public void Interact(GameObject interactor)
        {
        }
    }
}
