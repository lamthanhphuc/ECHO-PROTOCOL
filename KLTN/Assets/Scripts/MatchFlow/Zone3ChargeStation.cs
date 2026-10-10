using UnityEngine;

namespace EchoProtocol.Networking
{
    [RequireComponent(typeof(Collider))]
    public sealed class Zone3ChargeStation : MonoBehaviour, IHoldInteractable
    {
        private void Awake() { EchoProtocol.Audio.GameAudioRuntime.RegisterEmitter(this); }

        [SerializeField, Range(20f, 30f)] private float chargeDurationSeconds = 25f;
        [SerializeField, Range(0.05f, 1f)] private float decaySecondsPerSecond = 0.2f;

        private GameObject _offlineInteractor;
        private float _offlineChargeSeconds;
        private bool _offlineCompleted;

        public float ChargeDurationSeconds => chargeDurationSeconds;
        public float DecaySecondsPerSecond => decaySecondsPerSecond;
        public float Progress01
        {
            get
            {
                var match = NetworkMatchState.Instance;
                return match != null && match.Object != null && match.Object.IsValid
                    ? match.Zone3ChargeProgress01
                    : Mathf.Clamp01(_offlineChargeSeconds / chargeDurationSeconds);
            }
        }
        public bool IsCharging
        {
            get
            {
                var match = NetworkMatchState.Instance;
                return match != null && match.Object != null && match.Object.IsValid
                    ? match.IsZone3Charging
                    : _offlineInteractor != null;
            }
        }
        public bool RequiresHold => true;
        public string InteractionPrompt => IsCharging
            ? $"TRANSFERRING EMERGENCY POWER {Mathf.RoundToInt(Progress01 * 100f)}%"
            : $"HOLD E - INITIATE POWER TRANSFER {Mathf.RoundToInt(Progress01 * 100f)}%";

        public bool CanInteract(GameObject interactor)
        {
            if (Zone3MissionDirector.Instance?.CanActivateCharge(interactor) != true) return false;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid
                && match.Zone3ChargeOperator.IsRealPlayer)
            {
                var playerObject = interactor.GetComponentInParent<Fusion.NetworkObject>();
                return playerObject != null && playerObject.InputAuthority == match.Zone3ChargeOperator;
            }
            return _offlineInteractor == null || _offlineInteractor == interactor;
        }

        public void Interact(GameObject interactor) => BeginHoldInteract(interactor);

        public void BeginHoldInteract(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                match.RequestStartZone3Charge();
            else
                _offlineInteractor = interactor;
        }

        public void EndHoldInteract(GameObject interactor)
        {
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                match.RequestCancelZone3Charge();
            else if (_offlineInteractor == interactor)
                _offlineInteractor = null;
        }

        private void Update()
        {
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid) return;
            if (_offlineCompleted) return;
            if (_offlineInteractor != null
                && Zone3MissionDirector.Instance?.CanActivateCharge(_offlineInteractor) != true)
                _offlineInteractor = null;

            _offlineChargeSeconds = Mathf.Clamp(_offlineChargeSeconds + Time.deltaTime
                * (_offlineInteractor != null ? 1f : -decaySecondsPerSecond),
                0f, chargeDurationSeconds);
            if (_offlineChargeSeconds >= chargeDurationSeconds && _offlineInteractor != null)
            {
                var actor = _offlineInteractor;
                _offlineInteractor = null;
                _offlineCompleted = true;
                Zone3MissionDirector.Instance?.ActivateCharge(actor);
            }
        }

        private void OnDisable() => _offlineInteractor = null;
    }
}
