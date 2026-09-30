using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyPlayerSpotlightController : MonoBehaviour
    {
        [SerializeField] private Light[] spotlights;
        [SerializeField, Min(0.1f)] private float playerMatchRadius = 0.55f;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

        private float _nextRefreshAt;

        private void Awake()
        {
            DisableAll();
        }

        private void OnEnable()
        {
            DisableAll();
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshAt) return;

            _nextRefreshAt = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        private void OnDisable()
        {
            DisableAll();
        }

        private void Refresh()
        {
            if (SceneManager.GetActiveScene().name != "Lobby")
            {
                DisableAll();
                return;
            }

            LobbyPlayerState[] players = FindObjectsByType<LobbyPlayerState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < spotlights.Length; i++)
            {
                Light spotlight = spotlights[i];
                if (spotlight == null) continue;

                spotlight.enabled = HasLobbyPlayerUnderLight(spotlight, players);
            }
        }

        private bool HasLobbyPlayerUnderLight(Light spotlight, LobbyPlayerState[] players)
        {
            Vector3 lightPosition = spotlight.transform.position;
            float sqrRadius = playerMatchRadius * playerMatchRadius;

            for (int i = 0; i < players.Length; i++)
            {
                LobbyPlayerState player = players[i];
                if (player == null
                    || player.Object == null
                    || !player.Object.IsValid
                    || player.IsGameplayPlayer)
                {
                    continue;
                }

                Vector3 playerPosition = player.transform.position;
                float deltaX = playerPosition.x - lightPosition.x;
                float deltaZ = playerPosition.z - lightPosition.z;

                if (deltaX * deltaX + deltaZ * deltaZ <= sqrRadius) return true;
            }

            return false;
        }

        private void DisableAll()
        {
            if (spotlights == null) return;

            for (int i = 0; i < spotlights.Length; i++)
            {
                if (spotlights[i] != null) spotlights[i].enabled = false;
            }
        }
    }
}
