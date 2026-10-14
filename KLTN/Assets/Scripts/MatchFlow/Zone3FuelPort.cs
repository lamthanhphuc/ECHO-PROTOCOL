using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.MatchFlow
{
    [DisallowMultipleComponent, RequireComponent(typeof(Collider))]
    public sealed class Zone3FuelPort : MonoBehaviour, IHoldInteractable
    {
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f;
        private GameObject _offlineInteractor;
        private float _offlineProgress;
        public bool RequiresHold => true;
        private Zone3ConvoyController Convoy => GetComponentInParent<Zone3ConvoyController>()
            ?? Zone3MissionDirector.Instance?.Convoy;
        public float Progress01
        {
            get
            {
                var match = NetworkMatchState.Instance;
                return match != null && match.Object != null && match.Object.IsValid
                    ? match.Zone3RefuelProgress01 : _offlineProgress / Zone3FuelRules.InsertDurationSeconds;
            }
        }
        public string InteractionPrompt => Convoy == null || !Convoy.IsFuelEmpty
            ? EchoProtocol.Settings.GameLanguage.Choose("Chưa cần nạp nhiên liệu", "No refueling needed")
            : Progress01 > 0f ? EchoProtocol.Settings.GameLanguage.Choose($"Nạp nhiên liệu · {Mathf.RoundToInt(Progress01 * 100f)}%", $"Refueling · {Mathf.RoundToInt(Progress01 * 100f)}%")
            : EchoProtocol.Settings.GameLanguage.Choose("Nạp nhiên liệu", "Refuel");
        public bool IsInRange(Vector3 position) =>
            Vector3.Distance(position, GetComponent<Collider>().ClosestPoint(position)) <= interactionDistance;
        public bool CanInteract(GameObject player)
        {
            if (player == null || Convoy == null || !Convoy.IsInitialized || !Convoy.IsFuelEmpty
                || Convoy.CurrentPoint == Zone3ConvoyRoutePoint.Final
                || Zone3MissionDirector.Instance?.IsPushAvailable != true
                || !Zone3FuelRules.CanRefuel(Convoy.FuelPointsRemaining, Zone3FuelCell.FindCarried(player) != null,
                    true, IsInRange(player.transform.position))) return false;
            var life = player.GetComponentInParent<NetworkPlayerLifeState>();
            if (life != null && life.Status != NetworkPlayerLifeStatus.Alive) return false;
            var down = player.GetComponentInParent<PlayerDownState>();
            if (down != null && (!down.IsActive || down.IsDown)) return false;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid && match.Zone3RefuelOperator.IsRealPlayer)
                return player.GetComponentInParent<Fusion.NetworkObject>()?.InputAuthority == match.Zone3RefuelOperator;
            return _offlineInteractor == null || _offlineInteractor == player;
        }
        public void Interact(GameObject player) => BeginHoldInteract(player);
        public void BeginHoldInteract(GameObject player)
        {
            if (!CanInteract(player)) return;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid) match.RequestZone3ConvoyRefuel();
            else { _offlineInteractor = player; _offlineProgress = 0f; }
        }
        public void EndHoldInteract(GameObject player)
        {
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid) match.RequestCancelZone3Refuel();
            if (_offlineInteractor == player) { _offlineInteractor = null; _offlineProgress = 0f; }
        }
        private void Update()
        {
            if (_offlineInteractor == null) return;
            if (!CanInteract(_offlineInteractor)) { _offlineInteractor = null; _offlineProgress = 0f; return; }
            _offlineProgress += Time.deltaTime;
            if (_offlineProgress < Zone3FuelRules.InsertDurationSeconds) return;
            var cell = Zone3FuelCell.FindCarried(_offlineInteractor);
            var root = _offlineInteractor.GetComponentInParent<PlayerDownState>()?.gameObject ?? _offlineInteractor;
            if (cell != null && cell.ConsumeAuthoritative(root)) Convoy.Refuel();
            _offlineInteractor = null;
            _offlineProgress = 0f;
        }
        private void OnDisable() { _offlineInteractor = null; _offlineProgress = 0f; }
    }
}
