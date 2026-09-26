using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Visuals
{
    [DisallowMultipleComponent]
    public class ObjectiveGlowHighlight : MonoBehaviour
    {
        [Header("Glow Settings")]
        [ColorUsage(true, true)]
        [SerializeField] private Color outlineColor = new Color(0f, 0.85f, 1f, 1f);
        [SerializeField, Range(0.002f, 0.05f)] private float outlineWidth = 0.012f;
        [SerializeField, Range(0f, 10f)] private float pulseSpeed = 2.5f;

        [Header("Auto Target Tracking")]
        [SerializeField] private bool autoDetectType = true;

        private readonly List<GameObject> _proxies = new List<GameObject>();
        private Material _outlineMaterial;
        private Shader _outlineShader;
        private bool _isHighlighted = true;

        // Cached references for auto-checking completion / carry
        private RelayA.RelayAController _relayA;
        private RelayB.RelayBController _relayB;
        private EnergyCorePickup _energyCore;
        private Networking.NetworkPickupItem _networkPickup;

        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthProp = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PulseSpeedProp = Shader.PropertyToID("_PulseSpeed");

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                if (_outlineMaterial != null)
                {
                    _outlineMaterial.SetColor(OutlineColorProp, outlineColor);
                }
            }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                if (_outlineMaterial != null)
                {
                    _outlineMaterial.SetFloat(OutlineWidthProp, outlineWidth);
                }
            }
        }

        private void Awake()
        {
            if (autoDetectType)
            {
                DetectObjectiveContext();
            }

            SetupOutlineVisuals();
        }

        private void DetectObjectiveContext()
        {
            _relayA = GetComponentInParent<RelayA.RelayAController>();
            if (_relayA != null)
            {
                outlineColor = new Color(1f, 0.65f, 0.1f, 1f); // Warm Amber
                outlineWidth = 0.015f;
                return;
            }

            _relayB = GetComponentInParent<RelayB.RelayBController>();
            if (_relayB != null)
            {
                outlineColor = new Color(0.1f, 0.8f, 1f, 1f); // Neon Cyan
                outlineWidth = 0.015f;
                return;
            }

            _energyCore = GetComponentInParent<EnergyCorePickup>();
            _networkPickup = GetComponentInParent<Networking.NetworkPickupItem>();
            if (_energyCore != null || _networkPickup != null)
            {
                outlineColor = new Color(0f, 0.9f, 1f, 1f); // Electric Blue Core
                outlineWidth = 0.012f;
            }
        }

        private void SetupOutlineVisuals()
        {
            if (_proxies.Count > 0) return;

            _outlineShader = Shader.Find("EchoProtocol/ObjectiveOutline");
            if (_outlineShader == null)
            {
                Debug.LogWarning("[ObjectiveGlowHighlight] Outline shader EchoProtocol/ObjectiveOutline not found.");
                return;
            }

            _outlineMaterial = new Material(_outlineShader)
            {
                hideFlags = HideFlags.DontSave
            };
            _outlineMaterial.SetColor(OutlineColorProp, outlineColor);
            _outlineMaterial.SetFloat(OutlineWidthProp, outlineWidth);
            _outlineMaterial.SetFloat(PulseSpeedProp, pulseSpeed);

            var meshFilters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.name.EndsWith("_Outline", System.StringComparison.OrdinalIgnoreCase)) continue;

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;

                // Create outline proxy directly as child of the mesh's transform
                var proxyGo = new GameObject(mf.name + "_Outline");
                proxyGo.transform.SetParent(mf.transform, false);
                proxyGo.transform.localPosition = Vector3.zero;
                proxyGo.transform.localRotation = Quaternion.identity;
                proxyGo.transform.localScale = Vector3.one;

                var proxyMf = proxyGo.AddComponent<MeshFilter>();
                proxyMf.sharedMesh = mf.sharedMesh;

                var proxyMr = proxyGo.AddComponent<MeshRenderer>();
                proxyMr.sharedMaterial = _outlineMaterial;
                proxyMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                proxyMr.receiveShadows = false;
                proxyMr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                proxyMr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                _proxies.Add(proxyGo);
            }

            UpdateHighlightState();
        }

        private void Update()
        {
            CheckState();
        }

        private void CheckState()
        {
            // 1. Relay A completed?
            if (_relayA != null && _relayA.IsOnline)
            {
                if (_isHighlighted) SetHighlightActive(false);
                return;
            }

            // 2. Relay B completed?
            if (_relayB != null && _relayB.IsOnline)
            {
                if (_isHighlighted) SetHighlightActive(false);
                return;
            }

            // 3. Energy Core picked up / carried?
            if (_networkPickup != null)
            {
                bool carried = _networkPickup.IsCarried;
                if (_isHighlighted == carried)
                {
                    SetHighlightActive(!carried);
                }
                return;
            }

            if (_energyCore != null)
            {
                bool active = _energyCore.gameObject.activeInHierarchy && _energyCore.transform.parent == null;
                if (_isHighlighted != active)
                {
                    SetHighlightActive(active);
                }
            }
        }

        public void SetHighlightActive(bool active)
        {
            _isHighlighted = active;
            UpdateHighlightState();
        }

        private void UpdateHighlightState()
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                if (_proxies[i] != null)
                {
                    _proxies[i].SetActive(_isHighlighted);
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                if (_proxies[i] != null)
                {
                    Destroy(_proxies[i]);
                }
            }
            _proxies.Clear();

            if (_outlineMaterial != null)
            {
                Destroy(_outlineMaterial);
            }
        }
    }
}
