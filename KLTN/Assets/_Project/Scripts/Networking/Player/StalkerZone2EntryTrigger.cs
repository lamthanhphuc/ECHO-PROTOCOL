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
        }

        private void OnTriggerEnter(Collider other)
        {
            _spawner?.TrySpawnZone2Stalker(other, _spawnMarker);
        }
    }
}
