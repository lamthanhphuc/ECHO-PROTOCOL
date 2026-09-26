using UnityEngine;
using QuickOutline;
using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
using EchoProtocol.Networking;
using EchoProtocol.MatchFlow;

namespace EchoProtocol.Visuals
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Outline))]
    public class ObjectiveGlowHighlight : MonoBehaviour
    {
        public enum ObjectiveType
        {
            AutoDetect,
            Relay,
            EnergyCore,
            SectorBox,
            Terminal
        }

        [Header("Objective Configuration")]
        [SerializeField] private ObjectiveType objectiveType = ObjectiveType.AutoDetect;

        [Header("Outline Component Settings")]
        [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineVisible;
        
        [ColorUsage(true, true)]
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 1f);

        [SerializeField, Range(0.5f, 10.0f)] private float outlineWidth = 2.0f;

        private Outline _outline;
        private bool _isHighlighted = true;

        // Cached references for auto-checking completion / carry / zone
        private RelayAController _relayA;
        private RelayBController _relayB;
        private EnergyCorePickup _energyCore;
        private NetworkPickupItem _networkPickup;
        private SectorBox _sectorBox;
        private NetworkSectorBox _networkSectorBox;
        private SecurityTerminalDownload _terminal;

        private static PlayerEnergyCoreCarrier _cachedLocalCarrier;
        private static LobbyPlayerState _cachedLocalPlayerState;

        public ObjectiveType CurrentObjectiveType
        {
            get => objectiveType;
            set
            {
                objectiveType = value;
                DetectObjectiveContext();
            }
        }

        public Outline.Mode OutlineMode
        {
            get => outlineMode;
            set
            {
                outlineMode = value;
                SyncOutlineProperties();
            }
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                SyncOutlineProperties();
            }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                SyncOutlineProperties();
            }
        }

        private void Awake()
        {
            EnsureOutlineReference();
            DetectObjectiveContext();
        }

        private void OnEnable()
        {
            EnsureOutlineReference();
            SyncOutlineProperties();
        }

        private void OnValidate()
        {
            EnsureOutlineReference();
            SyncOutlineProperties();
        }

        private void EnsureOutlineReference()
        {
            if (_outline == null)
            {
                _outline = GetComponent<Outline>();
                if (_outline == null)
                {
                    _outline = gameObject.AddComponent<Outline>();
                }
            }
        }

        private void SyncOutlineProperties()
        {
            if (_outline == null) return;
            _outline.OutlineMode = outlineMode;
            _outline.OutlineColor = outlineColor;
            _outline.OutlineWidth = outlineWidth;
            _outline.UpdateMaterialProperties();
            _outline.enabled = Application.isPlaying ? _isHighlighted : true;
        }

        private void DetectObjectiveContext()
        {
            _relayA = GetComponentInParent<RelayAController>() ?? GetComponentInChildren<RelayAController>();
            _relayB = GetComponentInParent<RelayBController>() ?? GetComponentInChildren<RelayBController>();
            _energyCore = GetComponentInParent<EnergyCorePickup>() ?? GetComponentInChildren<EnergyCorePickup>();
            _networkPickup = GetComponentInParent<NetworkPickupItem>() ?? GetComponentInChildren<NetworkPickupItem>();
            _sectorBox = GetComponentInParent<SectorBox>() ?? GetComponentInChildren<SectorBox>();
            _networkSectorBox = GetComponentInParent<NetworkSectorBox>() ?? GetComponentInChildren<NetworkSectorBox>();
            _terminal = GetComponentInParent<SecurityTerminalDownload>() ?? GetComponentInChildren<SecurityTerminalDownload>();

            if (objectiveType == ObjectiveType.AutoDetect)
            {
                if (_sectorBox != null || _networkSectorBox != null)
                {
                    objectiveType = ObjectiveType.SectorBox;
                    outlineMode = Outline.Mode.OutlineAll; // Visible through walls (X-Ray)
                    _isHighlighted = false;
                }
                else if (_terminal != null)
                {
                    objectiveType = ObjectiveType.Terminal;
                    outlineMode = Outline.Mode.OutlineAll; // Visible through walls (X-Ray)
                    _isHighlighted = false;
                }
                else if (_relayA != null || _relayB != null)
                {
                    objectiveType = ObjectiveType.Relay;
                    outlineMode = Outline.Mode.OutlineVisible; // Cannot see through walls
                    _isHighlighted = true;
                }
                else if (_energyCore != null || _networkPickup != null)
                {
                    objectiveType = ObjectiveType.EnergyCore;
                    outlineMode = Outline.Mode.OutlineVisible; // Cannot see through walls
                    _isHighlighted = true;
                }
            }
            else
            {
                if (objectiveType == ObjectiveType.SectorBox || objectiveType == ObjectiveType.Terminal)
                {
                    outlineMode = Outline.Mode.OutlineAll;
                    if (Application.isPlaying) _isHighlighted = false;
                }
                else
                {
                    outlineMode = Outline.Mode.OutlineVisible;
                    if (Application.isPlaying) _isHighlighted = true;
                }
            }

            SyncOutlineProperties();
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                CheckState();
            }
        }

        private void CheckState()
        {
            switch (objectiveType)
            {
                case ObjectiveType.Relay:
                    CheckRelayState();
                    break;
                case ObjectiveType.EnergyCore:
                    CheckEnergyCoreState();
                    break;
                case ObjectiveType.SectorBox:
                    CheckSectorBoxState();
                    break;
                case ObjectiveType.Terminal:
                    CheckTerminalState();
                    break;
                default:
                    if (_relayA != null || _relayB != null) CheckRelayState();
                    else if (_energyCore != null || _networkPickup != null) CheckEnergyCoreState();
                    else if (_sectorBox != null || _networkSectorBox != null) CheckSectorBoxState();
                    else if (_terminal != null) CheckTerminalState();
                    break;
            }
        }

        private void CheckRelayState()
        {
            bool isOnline = (_relayA != null && _relayA.IsOnline) || (_relayB != null && _relayB.IsOnline);
            SetHighlightActive(!isOnline);
        }

        private void CheckEnergyCoreState()
        {
            if (_networkPickup != null)
            {
                bool carried = _networkPickup.IsCarried;
                bool active = _networkPickup.gameObject.activeInHierarchy;
                SetHighlightActive(!carried && active);
                return;
            }

            if (_energyCore != null)
            {
                bool active = _energyCore.gameObject.activeInHierarchy && _energyCore.transform.parent == null;
                SetHighlightActive(active);
            }
        }

        private void CheckSectorBoxState()
        {
            bool isComplete = false;
            if (_networkSectorBox != null)
            {
                isComplete = _networkSectorBox.IsCoreObjectiveComplete;
            }
            else if (_sectorBox != null)
            {
                isComplete = _sectorBox.PlacedCoreCount >= _sectorBox.MaxCoreCapacity;
            }

            bool carrying = IsLocalPlayerCarryingCore();
            SetHighlightActive(!isComplete && carrying);
        }

        private void CheckTerminalState()
        {
            bool isComplete = _terminal != null && _terminal.IsComplete;

            bool zone2Active = StalkerZone2EntryTrigger.Zone2Triggered;
            var director = Zone2MissionDirector.Instance;
            if (director != null && director.CurrentStage >= Zone2MissionStage.FindSecurityTerminal)
            {
                zone2Active = true;
            }

            // Before entering Zone 2, or once terminal download is complete: outline is OFF
            if (!zone2Active || isComplete)
            {
                SetHighlightActive(false);
                return;
            }

            bool alreadyTouched = false;

            if (_terminal != null)
            {
                if (_terminal.HasBeenTouched
                    || _terminal.IsDownloading
                    || _terminal.IsPaused
                    || _terminal.Progress01 > 0f)
                {
                    alreadyTouched = true;
                }
                else
                {
                    var playerTransform = GetLocalPlayerTransform();
                    if (playerTransform != null)
                    {
                        float dist = Vector3.Distance(playerTransform.position, _terminal.transform.position);
                        if (dist <= _terminal.MaxInteractorDistance)
                        {
                            _terminal.MarkTouched();
                            alreadyTouched = true;
                        }
                    }
                }
            }

            if (director != null && director.CurrentStage > Zone2MissionStage.FindSecurityTerminal)
            {
                alreadyTouched = true;
            }

            // Before touch: OutlineAll (xuyên tường để dẫn đường).
            // After first touch: OutlineVisible (không cho nhìn xuyên tường, chỉ thấy khi nhìn trực tiếp).
            Outline.Mode targetMode = alreadyTouched ? Outline.Mode.OutlineVisible : Outline.Mode.OutlineAll;
            if (outlineMode != targetMode)
            {
                OutlineMode = targetMode;
            }

            SetHighlightActive(true);
        }

        private static Transform _cachedLocalPlayerTransform;

        public static Transform GetLocalPlayerTransform()
        {
            if (_cachedLocalPlayerTransform != null && _cachedLocalPlayerTransform.gameObject.activeInHierarchy)
            {
                return _cachedLocalPlayerTransform;
            }

            if (_cachedLocalCarrier != null && _cachedLocalCarrier.gameObject.activeInHierarchy)
            {
                _cachedLocalPlayerTransform = _cachedLocalCarrier.transform;
                return _cachedLocalPlayerTransform;
            }

            if (_cachedLocalPlayerState != null && _cachedLocalPlayerState.gameObject.activeInHierarchy)
            {
                _cachedLocalPlayerTransform = _cachedLocalPlayerState.transform;
                return _cachedLocalPlayerTransform;
            }

            var pm = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude);
            for (int i = 0; i < pm.Length; i++)
            {
                if (pm[i] != null && pm[i].enabled)
                {
                    _cachedLocalPlayerTransform = pm[i].transform;
                    return _cachedLocalPlayerTransform;
                }
            }

            if (Camera.main != null)
            {
                return Camera.main.transform;
            }

            return null;
        }

        public static bool IsLocalPlayerCarryingCore()
        {
            if (_cachedLocalCarrier == null || !_cachedLocalCarrier.gameObject.activeInHierarchy)
            {
                var carriers = FindObjectsByType<PlayerEnergyCoreCarrier>(FindObjectsInactive.Exclude);
                for (int i = 0; i < carriers.Length; i++)
                {
                    var pm = carriers[i].GetComponent<PlayerMovement>();
                    if (pm != null && pm.enabled)
                    {
                        _cachedLocalCarrier = carriers[i];
                        break;
                    }
                }
            }

            if (_cachedLocalPlayerState == null || !_cachedLocalPlayerState.gameObject.activeInHierarchy)
            {
                var states = FindObjectsByType<LobbyPlayerState>(FindObjectsInactive.Exclude);
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].Object != null && states[i].Object.IsValid && states[i].Object.HasInputAuthority)
                    {
                        _cachedLocalPlayerState = states[i];
                        break;
                    }
                }
            }

            return (_cachedLocalCarrier != null && _cachedLocalCarrier.IsCarrying)
                || (_cachedLocalPlayerState != null && _cachedLocalPlayerState.CarriedCoreId.IsValid);
        }

        public void SetHighlightActive(bool active)
        {
            if (_isHighlighted == active && _outline != null && _outline.enabled == active) return;
            _isHighlighted = active;
            if (_outline != null)
            {
                _outline.enabled = active;
            }
        }
    }
}
