using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkDoorJammerState
    {
        NotDeployed = 0,
        Active = 1,
        Destroyed = 2,
    }

    [DisallowMultipleComponent]
    public sealed class NetworkDoorJammer : NetworkBehaviour, INetworkTraversalBlocker
    {
        [SerializeField, Min(0.01f)] private float _breakDurationSeconds = 3f;
        [SerializeField] private Collider _blockingCollider;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private AudioClip _breakClip;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkDoorJammerState State { get; private set; }

        [Networked] public NetworkId DoorId { get; private set; }

        private NetworkDoorJammerState _offlineState = NetworkDoorJammerState.NotDeployed;

        public bool IsActive => Object != null && Object.IsValid ? State == NetworkDoorJammerState.Active : _offlineState == NetworkDoorJammerState.Active;
        public bool BlocksTraversal => IsActive;
        public float BreakDurationSeconds => _breakDurationSeconds;

        public void InitializeOffline()
        {
            _offlineState = NetworkDoorJammerState.Active;
            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = true;
                _blockingCollider.isTrigger = false;
            }

            if (_visualRoot != null)
            {
                _visualRoot.gameObject.SetActive(true);
            }
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                State = NetworkDoorJammerState.NotDeployed;
                DoorId = default;
            }

            ApplyReplicatedState();
        }

        public bool InitializeAuthoritative(NetworkId doorId)
        {
            if (!Object.HasStateAuthority || !doorId.IsValid)
            {
                return false;
            }

            switch (State)
            {
                case NetworkDoorJammerState.NotDeployed:
                    DoorId = doorId;
                    State = NetworkDoorJammerState.Active;
                    ApplyReplicatedState();
                    return true;
                case NetworkDoorJammerState.Active:
                    return DoorId == doorId;
                case NetworkDoorJammerState.Destroyed:
                default:
                    return false;
            }
        }

        public bool CompleteBreakAuthoritative()
        {
            return TryDestroyAuthoritative();
        }

        public bool TryDestroyAuthoritative()
        {
            if (!Object.HasStateAuthority)
            {
                return false;
            }

            if (State != NetworkDoorJammerState.Destroyed)
            {
                State = NetworkDoorJammerState.Destroyed;
                ApplyReplicatedState();
                RpcPlayBreakAudio();
            }

            TryReconcileDoorRelationAuthoritative();
            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayBreakAudio()
        {
            if (_breakClip != null)
            {
                AudioSource.PlayClipAtPoint(_breakClip, transform.position);
            }
        }

        private void TryReconcileDoorRelationAuthoritative()
        {
            if (!DoorId.IsValid
                || Runner == null
                || !Runner.TryFindObject(DoorId, out var doorObject)
                || doorObject == null
                || !doorObject.TryGetComponent<NetworkSlidingDoor>(out var slidingDoor))
            {
                return;
            }

            slidingDoor.TryClearJammerAuthoritative(this);
        }

        private void ApplyReplicatedState()
        {
            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = BlocksTraversal;
                _blockingCollider.isTrigger = false;
            }

            if (_visualRoot != null)
            {
                _visualRoot.gameObject.SetActive(State == NetworkDoorJammerState.Active);
            }
        }

        private void OnValidate()
        {
            _breakDurationSeconds = Mathf.Max(0.01f, _breakDurationSeconds);
        }
    }
}
