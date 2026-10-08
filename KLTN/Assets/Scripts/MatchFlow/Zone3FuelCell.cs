using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace EchoProtocol.MatchFlow
{
    /// <summary>Independent Zone 3 carry item; never enters the Zone 1 core inventory.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent, RequireComponent(typeof(Collider), typeof(NetworkObject))]
    public sealed class Zone3FuelCell : NetworkBehaviour, IInteractable
    {
        private static readonly HashSet<Zone3FuelCell> ActiveCells = new HashSet<Zone3FuelCell>();
        private void OnEnable() => ActiveCells.Add(this);
        private void OnDisable()
        {
            ActiveCells.Remove(this);
            if (_renderers != null)
                foreach (var renderer in _renderers) HeldVisualShadowOverride.SetActive(renderer, false);
        }
        [SerializeField] private Vector3 carryLocalOffset = new Vector3(0.25f, 1.15f, 0.55f);
        [Networked] public NetworkBool Selected { get; private set; }
        [Networked] public NetworkBool Consumed { get; private set; }
        [Networked] public PlayerRef Holder { get; private set; }
        [Networked] public Vector3 WorldPosition { get; private set; }
        private GameObject _offlineCarrier;
        private bool _offlineSelected;
        private bool _offlineConsumed;
        private Collider _collider;
        private Renderer[] _renderers;
        private bool Online => Object != null && Object.IsValid && Runner != null;
        public bool IsAvailable => Online ? Selected && !Consumed : _offlineSelected && !_offlineConsumed;
        public GameObject Carrier
        {
            get
            {
                if (!Online) return _offlineCarrier;
                return Holder.IsRealPlayer && Runner.TryGetPlayerObject(Holder, out var player) ? player.gameObject : null;
            }
        }
        public bool IsCarried => Online ? Holder.IsRealPlayer : _offlineCarrier != null;
        public string InteractionPrompt => "E - PICK UP CONVOY FUEL CELL";

        private Vector3 _originalVisualScale;
        private void Awake()
        {
            _originalVisualScale = transform.localScale;
            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (TryGetComponent<Rigidbody>(out var body)) { body.isKinematic = true; body.useGravity = false; }
        }
        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                Holder = PlayerRef.None;
                WorldPosition = transform.position;
            }
            ApplyVisuals();
        }
        public void SetSelectedAuthoritative(bool selected)
        {
            if (Online)
            {
                if (!Object.HasStateAuthority) return;
                Selected = selected;
            }
            else _offlineSelected = selected;
            ApplyVisuals();
        }
        public static Zone3FuelCell FindCarried(GameObject player)
        {
            if (player == null) return null;
            var root = player.GetComponentInParent<NetworkPlayerLifeState>()?.gameObject
                ?? player.GetComponentInParent<PlayerDownState>()?.gameObject ?? player;
            foreach (var cell in ActiveCells)
                if (cell.IsAvailable && cell.IsCarried && cell.Carrier == root) return cell;
            return null;
        }
        private static bool IsAlive(GameObject player)
        {
            if (player == null) return false;
            var life = player.GetComponentInParent<NetworkPlayerLifeState>();
            if (life != null)
            {
                var lobby = life.GetComponent<LobbyPlayerState>();
                return life.Status == NetworkPlayerLifeStatus.Alive && lobby != null && lobby.IsGameplayPlayer;
            }
            var down = player.GetComponentInParent<PlayerDownState>();
            return down != null && down.IsActive && !down.IsDown;
        }
        public bool CanInteract(GameObject player)
        {
            var match = NetworkMatchState.Instance;
            var core = player != null ? player.GetComponentInParent<PlayerEnergyCoreCarrier>() : null;
            var lobby = player != null ? player.GetComponentInParent<LobbyPlayerState>() : null;
            return IsAvailable && !IsCarried && IsAlive(player) && FindCarried(player) == null
                && (core == null || !core.IsCarrying) && (lobby == null || !lobby.CarriedCoreId.IsValid)
                && Zone3MissionDirector.Instance?.IsPushAvailable == true
                && (match == null || !match.IsEnded);
        }
        public void Interact(GameObject player)
        {
            if (!CanInteract(player)) return;
            if (Online) RpcPickup();
            else { _offlineCarrier = player.GetComponentInParent<PlayerDownState>()?.gameObject ?? player; ApplyVisuals(); }
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RpcPickup(RpcInfo info = default)
        {
            if (!Runner.TryGetPlayerObject(info.Source, out var player) || player.InputAuthority != info.Source
                || !CanInteract(player.gameObject)
                || Vector3.Distance(player.transform.position, transform.position) > 3f) return;
            Holder = info.Source;
            ApplyVisuals();
        }
        public bool ConsumeAuthoritative(GameObject player)
        {
            if (!IsAvailable || Carrier != player) return false;
            if (Online)
            {
                if (!Object.HasStateAuthority) return false;
                Consumed = true;
                Holder = PlayerRef.None;
            }
            else { _offlineConsumed = true; _offlineCarrier = null; }
            ApplyVisuals();
            return true;
        }
        public void RequestDrop()
        {
            if (!IsCarried) return;
            if (Online) RpcDrop();
            else DropOffline();
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RpcDrop(RpcInfo info = default)
        {
            if (Holder != info.Source || !Holder.IsRealPlayer) return;
            DropAuthoritative();
        }
        private Vector3 DropPosition(GameObject player)
        {
            if (player == null) return transform.position;
            ItemDropPlacementUtility.GetFloorSnappedPose(player.transform, 1.1f, 0.3f,
                out var position, out _);
            return position;
        }
        private void DropAuthoritative()
        {
            WorldPosition = DropPosition(Carrier);
            Holder = PlayerRef.None;
            transform.position = WorldPosition;
            ApplyVisuals();
        }
        private void DropOffline()
        {
            transform.position = DropPosition(_offlineCarrier);
            _offlineCarrier = null;
            ApplyVisuals();
        }
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !Holder.IsRealPlayer) return;
            var player = Carrier;
            if (!IsAlive(player)) { DropAuthoritative(); return; }
            WorldPosition = player.transform.TransformPoint(carryLocalOffset);
        }
        public override void Render() => ApplyVisuals();
        private void LateUpdate()
        {
            if (Application.isPlaying && IsCarried) ApplyVisuals();
        }
        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!Online)
            {
                if (_offlineCarrier != null && !IsAlive(_offlineCarrier)) DropOffline();
                ApplyVisuals();
            }
        }
        private void ApplyVisuals()
        {
            if (_renderers == null) return;
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                renderer.enabled = IsAvailable;
                HeldVisualShadowOverride.SetActive(renderer, IsCarried && IsAvailable);
            }
            if (_collider != null) _collider.enabled = IsAvailable && !IsCarried;
            var player = Carrier;
            transform.localScale = _originalVisualScale * (IsCarried && player != null ? player.GetComponent<PlayerCharacterPresenter>()?.HeldItemScale ?? 1f : 1f);
            if (IsCarried && player != null)
            {
                var anchor = PlayerHeldItemAnchor.ResolveCoreCarryAnchor(player);
                transform.SetPositionAndRotation((anchor != null ? anchor.position : player.transform.TransformPoint(carryLocalOffset))
                    + (player.GetComponent<PlayerCharacterPresenter>()?.HeldItemWorldOffset ?? Vector3.zero),
                    anchor != null ? anchor.rotation : player.transform.rotation);
            }
            else if (Online) transform.position = WorldPosition;
        }
    }
}


