using System;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkPowerPuzzleState
    {
        Idle = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Resetting = 4,
    }

    public enum PowerPuzzleInputResult
    {
        AcceptedCorrect = 0,
        AcceptedIncorrect = 1,
        RejectedInvalidState = 2,
        RejectedInvalidInput = 3,
        AlreadyCompleted = 4,
    }

    /// <summary>State-authority source of truth for the cooperative power-puzzle sequence.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPowerPuzzle : NetworkBehaviour
    {
        public static event Action<NetworkPowerPuzzle> StateChanged;

        [SerializeField] private int[] _sequence = { 0, 1, 0, 1 };
        [SerializeField, Min(1)] private int _stationCount = 2;
        [SerializeField, Min(1)] private int _maxFailuresBeforeProgressReset = 3;
        [SerializeField, Min(0.05f)] private float _failureLockoutSeconds = 4f;
        [SerializeField, Min(0.05f)] private float _resettingSeconds = 0.15f;

        [Networked, OnChangedRender(nameof(HandleStateChanged))]
        public NetworkPowerPuzzleState State { get; private set; }

        [Networked, OnChangedRender(nameof(HandleStateChanged))]
        public int CurrentSequenceIndex { get; private set; }

        [Networked, OnChangedRender(nameof(HandleStateChanged))]
        public int FailureCount { get; private set; }

        [Networked, OnChangedRender(nameof(HandleStateChanged))]
        public int LastInputId { get; private set; }

        [Networked, OnChangedRender(nameof(HandleStateChanged))]
        public NetworkBool LastInputWasCorrect { get; private set; }

        [Networked, OnChangedRender(nameof(HandleStateChanged))]
        public PlayerRef LastInteractor { get; private set; }

        [Networked] public NetworkId SectorBoxId { get; private set; }
        [Networked] public uint TransitionOrdinal { get; private set; }
        [Networked] private TickTimer StateTimer { get; set; }
        [Networked] private NetworkBool ResetProgressAfterFailure { get; set; }

        public int SequenceLength => _sequence != null ? _sequence.Length : 0;
        public int StationCount => _stationCount;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                State = NetworkPowerPuzzleState.Idle;
                CurrentSequenceIndex = 0;
                FailureCount = 0;
                LastInputId = -1;
                LastInputWasCorrect = false;
                LastInteractor = PlayerRef.None;
                TransitionOrdinal = 0;
                StateTimer = TickTimer.None;
                ResetProgressAfterFailure = false;
            }

            // OnChangedRender is not guaranteed for a late joiner's initial snapshot.
            HandleStateChanged();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (!TryGetSectorBox(out var sectorBox)) return;

            if (State == NetworkPowerPuzzleState.Idle
                && sectorBox.Phase == NetworkMatchPhase.PowerPuzzle)
            {
                CommitState(NetworkPowerPuzzleState.InProgress);
                return;
            }

            if (State == NetworkPowerPuzzleState.Failed && StateTimer.ExpiredOrNotRunning(Runner))
            {
                StateTimer = TickTimer.CreateFromSeconds(Runner, _resettingSeconds);
                CommitState(NetworkPowerPuzzleState.Resetting);
                return;
            }

            if (State == NetworkPowerPuzzleState.Resetting && StateTimer.ExpiredOrNotRunning(Runner))
            {
                if (ResetProgressAfterFailure)
                {
                    CurrentSequenceIndex = 0;
                    FailureCount = 0;
                }

                ResetProgressAfterFailure = false;
                LastInputWasCorrect = false;
                StateTimer = TickTimer.None;
                CommitState(NetworkPowerPuzzleState.InProgress);
            }
        }

        public void InitializeAuthoritative(NetworkId sectorBoxId)
        {
            if (!Object.HasStateAuthority || !sectorBoxId.IsValid) return;
            SectorBoxId = sectorBoxId;
            Debug.Log($"[PowerPuzzle] Bound puzzle {Object.Id} to Sector Box {sectorBoxId}.");
        }

        public bool CanAcceptInput(int inputId)
        {
            var result = PowerPuzzleAuthorityRules.EvaluateInput(
                State,
                inputId,
                _stationCount,
                ExpectedInput());
            return result == PowerPuzzleInputResult.AcceptedCorrect
                || result == PowerPuzzleInputResult.AcceptedIncorrect;
        }

        public PowerPuzzleInputResult TryApplyInput(PlayerRef requester, int inputId)
        {
            if (!Object.HasStateAuthority
                || !requester.IsValid
                || !Runner.TryGetPlayerObject(requester, out var requesterObject)
                || requesterObject == null
                || requesterObject.InputAuthority != requester)
            {
                return PowerPuzzleInputResult.RejectedInvalidState;
            }

            var result = PowerPuzzleAuthorityRules.EvaluateInput(
                State,
                inputId,
                _stationCount,
                ExpectedInput());
            if (result != PowerPuzzleInputResult.AcceptedCorrect
                && result != PowerPuzzleInputResult.AcceptedIncorrect)
            {
                return result;
            }

            LastInputId = inputId;
            LastInteractor = requester;
            LastInputWasCorrect = result == PowerPuzzleInputResult.AcceptedCorrect;

            if (result == PowerPuzzleInputResult.AcceptedIncorrect)
            {
                FailureCount++;
                ResetProgressAfterFailure = _maxFailuresBeforeProgressReset > 0
                    && FailureCount >= _maxFailuresBeforeProgressReset;
                StateTimer = TickTimer.CreateFromSeconds(Runner, _failureLockoutSeconds);
                CommitState(NetworkPowerPuzzleState.Failed);
                Debug.LogWarning(
                    $"[PowerPuzzle] Incorrect input={inputId}, player={requester}, failures={FailureCount}.");
                return result;
            }

            CurrentSequenceIndex++;
            if (CurrentSequenceIndex >= SequenceLength)
            {
                CommitCompletionOnce();
            }
            else
            {
                AdvanceOrdinal();
                HandleStateChanged();
            }

            Debug.Log(
                $"[PowerPuzzle] Correct input={inputId}, player={requester}, " +
                $"progress={CurrentSequenceIndex}/{SequenceLength}.");
            return result;
        }

        private void CommitCompletionOnce()
        {
            if (State == NetworkPowerPuzzleState.Completed) return;

            StateTimer = TickTimer.None;
            CommitState(NetworkPowerPuzzleState.Completed);
            if (!TryGetSectorBox(out var sectorBox)
                || !sectorBox.TryCommitPowerPuzzleCompletion(Object.Id))
            {
                Debug.LogError($"[PowerPuzzle] Completed puzzle {Object.Id}, but Sector Box commit failed.");
            }
        }

        private int ExpectedInput()
        {
            return _sequence != null
                   && CurrentSequenceIndex >= 0
                   && CurrentSequenceIndex < _sequence.Length
                ? _sequence[CurrentSequenceIndex]
                : -1;
        }

        private bool TryGetSectorBox(out NetworkSectorBox sectorBox)
        {
            sectorBox = null;
            return SectorBoxId.IsValid
                && Runner.TryFindObject(SectorBoxId, out var sectorObject)
                && sectorObject != null
                && sectorObject.TryGetComponent(out sectorBox);
        }

        private void CommitState(NetworkPowerPuzzleState nextState)
        {
            State = nextState;
            AdvanceOrdinal();
            HandleStateChanged();
        }

        private void AdvanceOrdinal()
        {
            TransitionOrdinal++;
            if (TransitionOrdinal == 0) TransitionOrdinal = 1;
        }

        private void HandleStateChanged()
        {
            foreach (var legacy in FindObjectsByType<PowerPuzzleController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacy.ApplyAuthoritativeSnapshot(
                    State == NetworkPowerPuzzleState.InProgress,
                    State == NetworkPowerPuzzleState.Completed,
                    CurrentSequenceIndex,
                    SequenceLength,
                    FailureCount,
                    State == NetworkPowerPuzzleState.Failed
                    || State == NetworkPowerPuzzleState.Resetting);
            }

            StateChanged?.Invoke(this);
        }
    }

    public static class PowerPuzzleAuthorityRules
    {
        public static PowerPuzzleInputResult EvaluateInput(
            NetworkPowerPuzzleState state,
            int inputId,
            int stationCount,
            int expectedInputId)
        {
            if (state == NetworkPowerPuzzleState.Completed)
            {
                return PowerPuzzleInputResult.AlreadyCompleted;
            }

            if (state != NetworkPowerPuzzleState.InProgress)
            {
                return PowerPuzzleInputResult.RejectedInvalidState;
            }

            if (inputId < 0
                || inputId >= stationCount
                || expectedInputId < 0
                || expectedInputId >= stationCount)
            {
                return PowerPuzzleInputResult.RejectedInvalidInput;
            }

            return inputId == expectedInputId
                ? PowerPuzzleInputResult.AcceptedCorrect
                : PowerPuzzleInputResult.AcceptedIncorrect;
        }
    }
}
