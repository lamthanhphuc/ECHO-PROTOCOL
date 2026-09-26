using UnityEngine;

namespace EchoProtocol.Networking
{
    [DisallowMultipleComponent]
    public sealed class StalkerZone2EntryTrigger : MonoBehaviour
    {
        [SerializeField] private PlayerSpawner _spawner;
        [SerializeField] private Transform _spawnMarker;

        private void Awake()
        {
            if (_spawner == null) _spawner = FindAnyObjectByType<PlayerSpawner>();
            if (_spawner == null)
            {
                Debug.LogError("[STK_ZONE2][CONFIG] PlayerSpawner was not found for the Zone 2 entry trigger.", this);
            }

            var trigger = GetComponent<Collider>();
            if (trigger == null || !trigger.enabled || !trigger.isTrigger)
            {
                Debug.LogError("[STK_ZONE2][CONFIG] Zone 2 entry trigger requires an enabled Is Trigger collider.", this);
            }
        }

        public static bool Zone2Triggered { get; private set; }
        public static event System.Action OnZone2Triggered;

        private void OnDestroy()
        {
            Zone2Triggered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            var playerState = other != null ? other.GetComponentInParent<LobbyPlayerState>() : null;
            var playerMovement = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
            if (playerState == null && playerMovement == null)
            {
                return;
            }

            Zone2Triggered = true;
            OnZone2Triggered?.Invoke();

            if (_spawner == null)
            {
                Debug.LogError(
                    $"[STK_ZONE2][TRIGGER] Valid player entered, but PlayerSpawner is missing. " +
                    $"player={playerState.name} collider={other.name}.",
                    this);
                return;
            }

            Debug.Log(
                $"[STK_ZONE2][TRIGGER] Player entered Zone 2 trigger. " +
                $"player={playerState.name} authority={playerState.Object?.InputAuthority} " +
                $"collider={other.name} marker={(_spawnMarker != null ? _spawnMarker.name : "missing")}.",
                this);
            _spawner.TrySpawnZone2Stalker(other, _spawnMarker);
        }
    }
}
