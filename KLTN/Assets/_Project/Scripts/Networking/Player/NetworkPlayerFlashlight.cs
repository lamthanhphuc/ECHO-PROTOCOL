using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Networking
{
    /// <summary>
    /// Networked flashlight behaviour for PlayerNetwork.
    /// Synchronizes flashlight state across clients via Fusion.
    /// </summary>
    public sealed class NetworkPlayerFlashlight : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Light _flashlight;
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Settings")]
        [SerializeField] private bool _startOn = true;
        [SerializeField] private float _toggleCooldown = 0.2f;
        [SerializeField, Min(0f)] private float _beamIntensity = 60f;
        [SerializeField, Min(1f)] private float _beamRange = 40f;
        [SerializeField, Range(1f, 179f)] private float _beamSpotAngle = 66f;
        [SerializeField, Range(1f, 179f)] private float _beamInnerSpotAngle = 33f;

        [Networked] public NetworkBool IsFlashlightOn { get; set; }

        private InputAction _flashlightAction;
        private float _cooldownUntil;
        private bool _offlineIsOn;
        private PlayerCamera _playerCamera;
        private NetworkPlayerMovement _networkMovement;
        private NetworkPlayerLifeState _lifeState;

        public bool IsOn
        {
            get
            {
                if (Runner != null && Object != null && Object.IsValid)
                {
                    return IsFlashlightOn;
                }
                return _flashlight != null ? _flashlight.enabled : _offlineIsOn;
            }
        }

        private bool HasLocalControl()
        {
            if (Runner == null || Object == null || !Object.IsValid)
            {
                return true;
            }

            if (Object.HasInputAuthority)
            {
                return true;
            }

            if (Object.HasStateAuthority && Object.InputAuthority == PlayerRef.None)
            {
                return true;
            }

            return false;
        }

        private void Awake()
        {
            ResolveLight();
            ResolveLifeState();

            if (_inputActions != null)
            {
                var map = _inputActions.FindActionMap("Player", false);
                _flashlightAction = map?.FindAction("Flashlight", false);
            }

            if (_flashlightAction == null)
            {
                _flashlightAction = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            }
        }

        private void Start()
        {
            ResolveLight();
            if (Runner == null || Object == null || !Object.IsValid)
            {
                _offlineIsOn = _startOn;
                _flashlightAction?.Enable();
                ApplyVisuals();
            }
        }

        private void OnEnable()
        {
            if (HasLocalControl())
            {
                _flashlightAction?.Enable();
            }
        }

        public override void Spawned()
        {
            ResolveLight();

            if (Object.HasStateAuthority)
            {
                IsFlashlightOn = _startOn;
            }

            if (HasLocalControl())
            {
                _flashlightAction?.Enable();
            }

            ApplyVisuals();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _flashlightAction?.Disable();
        }

        private void OnDisable()
        {
            _flashlightAction?.Disable();
        }

        private void OnDestroy()
        {
            _flashlightAction?.Dispose();
        }

        public override void Render()
        {
            ApplyVisuals();
            UpdateFlashlightRotation();
        }

        private void LateUpdate()
        {
            UpdateFlashlightRotation();
        }

        private void UpdateFlashlightRotation()
        {
            ResolveLight();
            if (_flashlight == null) return;

            if (HasLocalControl())
            {
                if (_playerCamera == null)
                {
                    var mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        _playerCamera = mainCam.GetComponent<PlayerCamera>();
                    }
                }

                if (_playerCamera != null)
                {
                    _flashlight.transform.rotation = _playerCamera.transform.rotation;
                }
                else if (Camera.main != null)
                {
                    _flashlight.transform.rotation = Camera.main.transform.rotation;
                }
            }
            else
            {
                if (_networkMovement == null)
                {
                    _networkMovement = GetComponent<NetworkPlayerMovement>();
                }

                float pitch = _networkMovement != null ? _networkMovement.CurrentPitch : 0f;
                _flashlight.transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
            }
        }

        private void Update()
        {
            if (!HasLocalControl()) return;
            if (!CanUseFlashlight())
            {
                ApplyVisuals();
                return;
            }

            if (Time.time < _cooldownUntil) return;

            bool pressed = false;
            if (_flashlightAction != null && _flashlightAction.enabled && _flashlightAction.WasPressedThisFrame())
            {
                pressed = true;
            }
            else if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                pressed = true;
            }

            if (pressed)
            {
                _cooldownUntil = Time.time + _toggleCooldown;
                ToggleFlashlight();
            }
        }

        private void ToggleFlashlight()
        {
            if (!CanUseFlashlight())
            {
                ApplyVisuals();
                return;
            }

            ResolveLight();

            if (Runner != null && Object != null && Object.IsValid)
            {
                bool nextState = !IsFlashlightOn;
                if (Object.HasStateAuthority)
                {
                    IsFlashlightOn = nextState;
                }
                else
                {
                    RpcToggleFlashlight(nextState);
                }
            }
            else
            {
                _offlineIsOn = _flashlight != null ? !_flashlight.enabled : !_offlineIsOn;
            }

            ApplyVisuals();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcToggleFlashlight(NetworkBool nextState)
        {
            IsFlashlightOn = nextState;
        }

        private void ResolveLight()
        {
            if (_flashlight == null)
            {
                Transform t = transform.Find("Flashlight_Light");
                if (t != null)
                {
                    _flashlight = t.GetComponent<Light>();
                }
            }

            if (_flashlight == null)
            {
                _flashlight = GetComponentInChildren<Light>(true);
            }

            if (_flashlight == null)
            {
                var lightObj = new GameObject("Flashlight_Light");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0.0645f, 0.862f, 0.06f);
                lightObj.transform.localRotation = Quaternion.Euler(12.245f, 0f, 0f);

                _flashlight = lightObj.AddComponent<Light>();
                _flashlight.type = LightType.Spot;
                _flashlight.color = new Color(1f, 0.96f, 0.88f);
            }

            ApplyBeamTuning();
        }

        private void ResolveLifeState()
        {
            if (_lifeState == null)
            {
                _lifeState = GetComponent<NetworkPlayerLifeState>();
            }
        }

        private bool CanUseFlashlight()
        {
            ResolveLifeState();
            return _lifeState == null || !_lifeState.IsEliminated;
        }

        private void ApplyBeamTuning()
        {
            if (_flashlight == null)
            {
                return;
            }

            _flashlight.type = LightType.Spot;
            _flashlight.color = new Color(1f, 0.96f, 0.88f);
            _flashlight.intensity = _beamIntensity;
            _flashlight.range = _beamRange;
            _flashlight.spotAngle = _beamSpotAngle;
            _flashlight.innerSpotAngle = Mathf.Min(_beamInnerSpotAngle, _beamSpotAngle);
            _flashlight.renderMode = LightRenderMode.ForcePixel;
            _flashlight.bounceIntensity = 0.4f;
            _flashlight.shadows = LightShadows.Soft;

            RemoveBeamVisual();
        }

        private void ApplyVisuals()
        {
            ResolveLight();
            if (_flashlight != null)
            {
                bool isOn;
                if (Runner != null && Object != null && Object.IsValid)
                {
                    isOn = IsFlashlightOn && CanUseFlashlight();
                }
                else
                {
                    isOn = _offlineIsOn && CanUseFlashlight();
                }

                _flashlight.enabled = isOn;
                RemoveBeamVisual();
            }
        }

        private void RemoveBeamVisual()
        {
            if (_flashlight == null)
            {
                return;
            }

            var beamVisual = _flashlight.GetComponent<FlashlightBeamVisual>();
            if (beamVisual != null)
            {
                Destroy(beamVisual);
            }

            Transform beam = _flashlight.transform.Find("Flashlight_Beam_Visual");
            if (beam != null)
            {
                Destroy(beam.gameObject);
            }
        }

    }
}

