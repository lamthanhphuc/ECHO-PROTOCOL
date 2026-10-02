using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.MatchFlow
{
    /// <summary>
    /// World pickup for Fuel Cells in Zone 3. Reuses the standard IInteractable interface
    /// and carrier-parenting pattern so players can pick up and transport fuel to Spacefrigate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Zone3FuelCell : MonoBehaviour, IInteractable
    {
        [SerializeField] private string pickupPrompt = "Pick up Fuel Cell";
        [SerializeField] private Vector3 carryLocalOffset = new Vector3(0.2f, -0.2f, 0.4f);

        private GameObject _carrier;
        private Collider _collider;
        private Rigidbody _rigidbody;

        public bool IsCarried => _carrier != null;
        public GameObject Carrier => _carrier;
        public string InteractionPrompt => pickupPrompt;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _rigidbody = GetComponent<Rigidbody>();
            if (GetComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>() == null)
            {
                gameObject.AddComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>();
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (interactor == null || IsCarried) return false;
            // Check if interactor is an alive gameplay player
            var lifeState = interactor.GetComponentInParent<NetworkPlayerLifeState>();
            if (lifeState != null && lifeState.Status != NetworkPlayerLifeStatus.Alive) return false;
            var downState = interactor.GetComponentInParent<PlayerDownState>();
            if (downState != null && (!downState.IsActive || downState.IsDown)) return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            AttachToCarrier(interactor);
        }

        public void AttachToCarrier(GameObject interactor)
        {
            _carrier = interactor;
            if (_collider != null) _collider.enabled = false;
            if (_rigidbody != null) _rigidbody.isKinematic = true;

            // Parent to player's camera or hands if available, else root
            Transform mount = interactor.transform;
            var cam = interactor.GetComponentInChildren<Camera>();
            if (cam != null) mount = cam.transform;

            transform.SetParent(mount, false);
            transform.localPosition = carryLocalOffset;
            transform.localRotation = Quaternion.identity;
        }

        public void Consume()
        {
            _carrier = null;
            Destroy(gameObject);
        }

        public void Drop(Vector3 position, Quaternion rotation)
        {
            _carrier = null;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            if (_collider != null) _collider.enabled = true;
            if (_rigidbody != null) _rigidbody.isKinematic = false;
        }
    }
}
