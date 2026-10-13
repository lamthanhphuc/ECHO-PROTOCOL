using System.Collections.Generic;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyPlayerSpotlightController : MonoBehaviour
    {
        [Header("Scene-authored lights")]
        [SerializeField] private Light[] spotlights;
        [SerializeField] private Light[] bodyFillLights;

        [Header("Player matching")]
        [SerializeField, Min(0.1f)]
        private float playerMatchRadius = 0.55f;

        [SerializeField, Min(0.05f)]
        private float refreshInterval = 0.15f;

        [Header("Light isolation")]
        [SerializeField]
        private string visualLayerName = "LocalPlayerVisual";

        private readonly Dictionary<GameObject, int> _originalLayers =
            new Dictionary<GameObject, int>();

        private float _nextRefreshAt;
        private int _visualLayer = -1;
        private bool _layerErrorLogged;


        private void Awake()
        {
            ResolveVisualLayer();
            DisableAll();
        }


        private void OnEnable()
        {
            ResolveVisualLayer();

            DisableAll();
            Refresh();
        }


        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt =
                Time.unscaledTime + refreshInterval;

            Refresh();
        }


        private void OnDisable()
        {
            DisableAll();
            RestoreVisualLayers();
        }


        private void OnDestroy()
        {
            RestoreVisualLayers();
        }


        private bool ResolveVisualLayer()
        {
            if (_visualLayer >= 0)
                return true;

            _visualLayer =
                LayerMask.NameToLayer(
                    visualLayerName);

            if (_visualLayer >= 0)
            {
                _layerErrorLogged = false;
                return true;
            }

            if (!_layerErrorLogged)
            {
                Debug.LogError(
                    "[LobbyLighting] Layer '"
                    + visualLayerName
                    + "' does not exist.",
                    this);

                _layerErrorLogged = true;
            }

            return false;
        }


        private void Refresh()
        {
            if (!ResolveVisualLayer())
            {
                DisableAll();
                return;
            }

            if (spotlights == null)
                return;

            LobbyPlayerState[] players =
                FindObjectsByType<LobbyPlayerState>(
                    FindObjectsInactive.Exclude);

            for (int index = 0;
                 index < spotlights.Length;
                 index++)
            {
                Light spotlight =
                    spotlights[index];

                if (spotlight == null)
                    continue;

                LobbyPlayerState matchedPlayer;

                bool occupied =
                    HasLobbyPlayerUnderLight(
                        spotlight,
                        players,
                        out matchedPlayer);

                if (occupied
                    && matchedPlayer != null)
                {
                    ApplyVisualLayer(
                        matchedPlayer);
                }

                spotlight.enabled =
                    occupied;

                if (bodyFillLights != null
                    && index < bodyFillLights.Length
                    && bodyFillLights[index] != null)
                {
                    bodyFillLights[index].enabled =
                        occupied;
                }
            }
        }


        private void ApplyVisualLayer(
            LobbyPlayerState player)
        {
            if (player == null
                || _visualLayer < 0)
            {
                return;
            }

            Renderer[] renderers =
                player.GetComponentsInChildren<Renderer>(
                    true);

            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer =
                    renderers[index];

                if (renderer == null)
                    continue;

                GameObject owner =
                    renderer.gameObject;

                if (!_originalLayers.ContainsKey(
                        owner))
                {
                    _originalLayers.Add(
                        owner,
                        owner.layer);
                }

                owner.layer =
                    _visualLayer;
            }
        }


        private void RestoreVisualLayers()
        {
            foreach (
                KeyValuePair<GameObject, int> entry
                in _originalLayers)
            {
                if (entry.Key != null)
                {
                    entry.Key.layer =
                        entry.Value;
                }
            }

            _originalLayers.Clear();
        }


        private bool HasLobbyPlayerUnderLight(
            Light spotlight,
            LobbyPlayerState[] players,
            out LobbyPlayerState matchedPlayer)
        {
            matchedPlayer = null;

            Vector3 lightPosition =
                spotlight.transform.position;

            float sqrRadius =
                playerMatchRadius
                * playerMatchRadius;

            for (int index = 0;
                 index < players.Length;
                 index++)
            {
                LobbyPlayerState player =
                    players[index];

                if (player == null
                    || player.Object == null
                    || !player.Object.IsValid
                    || player.IsGameplayPlayer)
                {
                    continue;
                }

                Vector3 playerPosition =
                    player.transform.position;

                float deltaX =
                    playerPosition.x
                    - lightPosition.x;

                float deltaZ =
                    playerPosition.z
                    - lightPosition.z;

                if (deltaX * deltaX
                    + deltaZ * deltaZ
                    <= sqrRadius)
                {
                    matchedPlayer =
                        player;

                    return true;
                }
            }

            return false;
        }


        private void DisableAll()
        {
            if (spotlights != null)
            {
                for (int index = 0;
                     index < spotlights.Length;
                     index++)
                {
                    if (spotlights[index] != null)
                    {
                        spotlights[index].enabled =
                            false;
                    }
                }
            }

            if (bodyFillLights != null)
            {
                for (int index = 0;
                     index < bodyFillLights.Length;
                     index++)
                {
                    if (bodyFillLights[index] != null)
                    {
                        bodyFillLights[index].enabled =
                            false;
                    }
                }
            }
        }
    }
}