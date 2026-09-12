using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public enum StalkerWorldInteractionKind
    {
        None = 0,
        OpeningDoor = 1,
        BreakingDoor = 2,
        BreakingJammer = 3
    }

    public enum StalkerWorldInteractionStartResult
    {
        NotHandled = 0,
        Started = 1,
        Completed = 2,
        AuthorityRejected = 3
    }

    public sealed class StalkerWorldInteractionDriver
    {
        private StalkerWorldInteractionKind _kind;
        private NetworkSlidingDoor _door;
        private NetworkDoorJammer _jammer;
        private StalkerNavigationObjectiveKey _objectiveKey;
        private StalkerState _state;
        private float _elapsedSeconds;
        private float _durationSeconds;

        public bool HasActiveInteraction => _kind != StalkerWorldInteractionKind.None;
        public StalkerWorldInteractionKind CurrentKind => _kind;
        public Component CurrentBlocker => _kind == StalkerWorldInteractionKind.BreakingJammer
            ? _jammer
            : _door;
        public StalkerNavigationObjectiveKey ObjectiveKey => _objectiveKey;
        public StalkerState State => _state;
        public float InteractionProgress01 => _durationSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(_elapsedSeconds / _durationSeconds);

        public StalkerWorldInteractionStartResult TryBegin(
            Component blocker,
            StalkerNavigationObjectiveKey objectiveKey,
            StalkerState state,
            bool hasAuthority,
            float doorBreakDurationSeconds)
        {
            if (blocker == null || !objectiveKey.IsValid || !hasAuthority)
            {
                return StalkerWorldInteractionStartResult.AuthorityRejected;
            }

            var jammer = blocker.GetComponentInParent<NetworkDoorJammer>();
            if (jammer != null)
            {
                if (!jammer.IsActive)
                {
                    return StalkerWorldInteractionStartResult.Completed;
                }

                Begin(
                    StalkerWorldInteractionKind.BreakingJammer,
                    null,
                    jammer,
                    objectiveKey,
                    state,
                    jammer.BreakDurationSeconds);
                return StalkerWorldInteractionStartResult.Started;
            }

            var door = blocker.GetComponentInParent<NetworkSlidingDoor>();
            if (door == null)
            {
                return StalkerWorldInteractionStartResult.NotHandled;
            }

            if (!door.BlocksTraversal)
            {
                return StalkerWorldInteractionStartResult.Completed;
            }

            if (!door.IsBroken && door.CanMonsterOpen)
            {
                return door.TryOpenForMonsterAuthoritative()
                    ? StalkerWorldInteractionStartResult.Completed
                    : StalkerWorldInteractionStartResult.AuthorityRejected;
            }

            if (!door.IsBroken)
            {
                Begin(
                    StalkerWorldInteractionKind.BreakingDoor,
                    door,
                    null,
                    objectiveKey,
                    state,
                    doorBreakDurationSeconds);
                return StalkerWorldInteractionStartResult.Started;
            }

            return StalkerWorldInteractionStartResult.NotHandled;
        }

        public bool Tick(float deltaSeconds, bool hasAuthority, out bool completed)
        {
            completed = false;
            if (!HasActiveInteraction)
            {
                return false;
            }

            if (!hasAuthority)
            {
                Cancel();
                return false;
            }

            if (_kind == StalkerWorldInteractionKind.BreakingDoor)
            {
                if (_door == null)
                {
                    Cancel();
                    return false;
                }

                if (_door.IsBroken || !_door.BlocksTraversal)
                {
                    Complete();
                    completed = true;
                    return true;
                }

                _elapsedSeconds += Mathf.Max(0f, deltaSeconds);
                if (_elapsedSeconds < _durationSeconds)
                {
                    return true;
                }

                if (_door.TryBreakAuthoritative())
                {
                    Complete();
                    completed = true;
                    return true;
                }

                Cancel();
                return false;
            }

            if (_kind == StalkerWorldInteractionKind.BreakingJammer)
            {
                if (_jammer == null)
                {
                    Cancel();
                    return false;
                }

                if (!_jammer.IsActive)
                {
                    Complete();
                    completed = true;
                    return true;
                }

                _elapsedSeconds += Mathf.Max(0f, deltaSeconds);
                if (_elapsedSeconds < _durationSeconds)
                {
                    return true;
                }

                if (_jammer.CompleteBreakAuthoritative())
                {
                    Complete();
                    completed = true;
                    return true;
                }

                Cancel();
                return false;
            }

            Cancel();
            return false;
        }

        public void Cancel()
        {
            Clear();
        }

        private void Begin(
            StalkerWorldInteractionKind kind,
            NetworkSlidingDoor door,
            NetworkDoorJammer jammer,
            StalkerNavigationObjectiveKey objectiveKey,
            StalkerState state,
            float durationSeconds)
        {
            _kind = kind;
            _door = door;
            _jammer = jammer;
            _objectiveKey = objectiveKey;
            _state = state;
            _elapsedSeconds = 0f;
            _durationSeconds = Mathf.Max(0.01f, durationSeconds);
        }

        private void Complete()
        {
            Clear();
        }

        private void Clear()
        {
            _kind = StalkerWorldInteractionKind.None;
            _door = null;
            _jammer = null;
            _objectiveKey = StalkerNavigationObjectiveKey.None;
            _state = default;
            _elapsedSeconds = 0f;
            _durationSeconds = 0f;
        }
    }
}
