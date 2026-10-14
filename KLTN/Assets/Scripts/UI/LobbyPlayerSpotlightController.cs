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

        [SerializeField, Min(0f)] private float bodyFillIntensity = 3.5f;
        [SerializeField, Min(0.1f)] private float bodyFillRange = 4f;
        private Light[] _bodyFillLights;
        private float _nextRefreshAt;
        private Camera _presentationCamera;
        private Vector3 _cameraPosition;
        private float _cameraFov;
        private NetworkLobbyUI _lobbyUI;
        private void FitNarrowPresentation()
        {
            if(SceneManager.GetActiveScene().name!="Lobby")return;
            var camera=Camera.main;
            if(camera==null || camera.orthographic)return;
            if(_presentationCamera!=camera) {
                _presentationCamera=camera;_cameraPosition=camera.transform.position;_cameraFov=camera.fieldOfView;
            }
            const float referenceAspect=16f/9f;
            if(camera.aspect>=referenceAspect-0.01f) {
                camera.fieldOfView=_cameraFov;camera.transform.position=_cameraPosition;return;
            }
            if(_lobbyUI==null)_lobbyUI=FindAnyObjectByType<NetworkLobbyUI>();
            float right=_lobbyUI!=null ? _lobbyUI.PresentationRightEdge : 0.74f;
            const float left=0.04f;
            float fit=Mathf.Max(1f,(0.74f-left)/Mathf.Max(0.1f,right-left));
            float tangent=Mathf.Tan(_cameraFov*Mathf.Deg2Rad*0.5f);
            camera.fieldOfView=Mathf.Atan(tangent*referenceAspect/camera.aspect*fit)*2f*Mathf.Rad2Deg;
            float depth=Mathf.Abs(Vector3.Dot(LobbyLineupLayout.Position(0)-_cameraPosition,camera.transform.forward));
            float extraHalfWidth=depth*tangent*referenceAspect*(fit-1f);
            camera.transform.position=_cameraPosition+camera.transform.right*(extraHalfWidth*(1f-2f*left));
        }
        private void RestorePresentationCamera()
        {
            if(_presentationCamera!=null) {
                _presentationCamera.fieldOfView=_cameraFov;
                _presentationCamera.transform.position=_cameraPosition;
            }
        }

        private void EnsureBodyFillLights()
        {
            if (!Application.isPlaying || spotlights == null || _bodyFillLights != null) return;
            _bodyFillLights = new Light[spotlights.Length];
            for (int i = 0; i < spotlights.Length; i++)
            {
                var overhead = spotlights[i];
                if (overhead == null) continue;
                var obj = new GameObject("Lobby Body Fill");
                obj.transform.SetParent(overhead.transform, false);
                obj.transform.position = overhead.transform.position + Vector3.down * 1.2f;
                var fill = obj.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.color = overhead.color;
                fill.intensity = bodyFillIntensity;
                fill.range = bodyFillRange;
                fill.shadows = LightShadows.None;
                fill.cullingMask = overhead.cullingMask;
                fill.enabled = false;
                _bodyFillLights[i] = fill;
            }
        }

        private void OnDestroy()
        {
            if (_bodyFillLights == null) return;
            foreach (var fill in _bodyFillLights) if (fill != null) Destroy(fill.gameObject);
        }

        private void Awake()
        {
            EnsureBodyFillLights();
            DisableAll();
        }

        private void OnEnable()
        {
            EnsureBodyFillLights();
            DisableAll();
            Refresh();
        }

        private void Update()
        {
            FitNarrowPresentation();
            if (Time.unscaledTime < _nextRefreshAt) return;

            _nextRefreshAt = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        private void OnDisable()
        {
            RestorePresentationCamera();
            DisableAll();
        }

        private void Refresh()
        {
            if (SceneManager.GetActiveScene().name != "Lobby")
            {
                DisableAll();
                return;
            }

            if (spotlights == null) return;
            EnsureBodyFillLights();
            LobbyPlayerState[] players = FindObjectsByType<LobbyPlayerState>(
                FindObjectsInactive.Exclude);

            for (int i = 0; i < spotlights.Length; i++)
            {
                Light spotlight = spotlights[i];
                if (spotlight == null) continue;

                // Move the overhead fixture with the same fixed slot layout used by the host.
                var slotPosition=LobbyLineupLayout.Position(i);
                var lightPosition=spotlight.transform.position;
                spotlight.transform.position=new Vector3(slotPosition.x,lightPosition.y,slotPosition.z);
                spotlight.enabled = HasLobbyPlayerUnderLight(spotlight, players, out var matchedPlayer);
                if (_bodyFillLights != null && _bodyFillLights[i] != null)
                {
                    var fill = _bodyFillLights[i];
                    fill.enabled = spotlight.enabled;
                    if (matchedPlayer != null)
                        fill.transform.position = matchedPlayer.transform.position
                            + matchedPlayer.transform.forward * 1.4f + Vector3.up * 1f;
                }
            }
        }

        private bool HasLobbyPlayerUnderLight(Light spotlight, LobbyPlayerState[] players, out LobbyPlayerState matchedPlayer)
        {
            matchedPlayer = null;
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

                if (deltaX * deltaX + deltaZ * deltaZ <= sqrRadius)
                {
                    matchedPlayer = player;
                    return true;
                }
            }

            return false;
        }

        private void DisableAll()
        {
            if (spotlights == null) return;

            for (int i = 0; i < spotlights.Length; i++)
            {
                if (spotlights[i] != null) spotlights[i].enabled = false;
                if (_bodyFillLights != null && _bodyFillLights[i] != null) _bodyFillLights[i].enabled = false;
            }
        }
    }
}

