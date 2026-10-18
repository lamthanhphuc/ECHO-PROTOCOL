using UnityEngine;

namespace EchoProtocol.Networking
{
    [RequireComponent(typeof(Collider))]
    public sealed class FloodedChargeInteraction : MonoBehaviour, IHoldInteractable
    {
        private GameObject _interactor;
        private float _refreshAt;
        public bool RequiresHold => true;
        public float Progress01
        {
            get
            {
                var match = NetworkMatchState.Instance;
                return match && match.Object && match.Object.IsValid ? match.Zone3ChargeProgress01 : 0f;
            }
        }
        public string InteractionPrompt => "GIỮ [E] - NẠP NĂNG LƯỢNG CHO ĐĨA BAY";
        public bool CanInteract(GameObject interactor)
        {
            var match = NetworkMatchState.Instance;
            return interactor && match && match.Object && match.Object.IsValid && !match.IsEnded
                && match.CurrentPhase == NetworkMatchPhase.Zone3FindFrigate;
        }
        public void Interact(GameObject interactor) => BeginHoldInteract(interactor);
        public void BeginHoldInteract(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            _interactor = interactor;
            _refreshAt = 0f;
        }
        public void EndHoldInteract(GameObject interactor)
        {
            if (_interactor != interactor) return;
            _interactor = null;
            NetworkMatchState.Instance?.RequestFloodedCharge(false);
        }
        private void Update()
        {
            if (!_interactor) return;
            if (!CanInteract(_interactor)) { EndHoldInteract(_interactor); return; }
            if (Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + .2f;
            NetworkMatchState.Instance.RequestFloodedCharge(true);
        }
        private void OnDisable()
        {
            if (_interactor) EndHoldInteract(_interactor);
        }
    }
}
