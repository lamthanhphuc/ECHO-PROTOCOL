using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Authoritative objective source; match phase and final result live in NetworkMatchState.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSectorBox : NetworkInteractable
    {
        [SerializeField, Min(1)] private int _requiredCoreCount = 1;

        [Networked, OnChangedRender(nameof(HandleObjectiveChanged))]
        public int PlacedCoreCount { get; private set; }

        [Networked] public NetworkBool SecurityHoldWasInterrupted { get; private set; }
        [Networked] public uint ObjectiveOrdinal { get; private set; }
        [Networked] public NetworkId MatchStateId { get; private set; }

        public int RequiredCoreCount => _requiredCoreCount;
        public bool IsCoreObjectiveComplete => PlacedCoreCount >= _requiredCoreCount;
        public NetworkMatchPhase Phase => TryGetMatchState(out var matchState)
            ? matchState.CurrentPhase
            : NetworkMatchPhase.CoreObjective;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                PlacedCoreCount = 0;
                SecurityHoldWasInterrupted = false;
                ObjectiveOrdinal = 0;
                MatchStateId = default;
            }
            HandleObjectiveChanged();
        }

        public void InitializeAuthoritative(NetworkId matchStateId)
        {
            if (!Object.HasStateAuthority || !matchStateId.IsValid) return;
            MatchStateId = matchStateId;
            Debug.Log($"[Objective] Sector Box {Object.Id} bound to Match State {matchStateId}.");
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            if (context.PlayerState == null || !context.PlayerState.IsGameplayPlayer)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            if (!TryGetMatchState(out var matchState) || matchState.IsEnded)
            {
                return InteractionValidationResult.InvalidTargetState;
            }

            switch (matchState.CurrentPhase)
            {
                case NetworkMatchPhase.CoreObjective:
                    return !IsCoreObjectiveComplete && context.PlayerState.CarriedCoreId.IsValid
                        ? InteractionValidationResult.Accepted
                        : InteractionValidationResult.InvalidTargetState;
                case NetworkMatchPhase.Puzzle:
                case NetworkMatchPhase.SecurityHold:
                case NetworkMatchPhase.Escape:
                    return InteractionValidationResult.Accepted;
                default:
                    return InteractionValidationResult.InvalidTargetState;
            }
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (!TryGetMatchState(out var matchState) || matchState.IsEnded) return;

            switch (matchState.CurrentPhase)
            {
                case NetworkMatchPhase.CoreObjective:
                    PlaceCarriedCore(context, matchState);
                    break;
                case NetworkMatchPhase.Puzzle:
                    CompletePowerPuzzle(matchState);
                    break;
                case NetworkMatchPhase.SecurityHold:
                    AdvanceSecurityHold(matchState);
                    break;
                case NetworkMatchPhase.Escape:
                    matchState.TryCommitPlayerEscaped(context.Player);
                    break;
            }
        }

        private void PlaceCarriedCore(in InteractionContext context, NetworkMatchState matchState)
        {
            if (!NetworkMatchStateRules.IsObjectiveMutationAllowed(
                    matchState.Status,
                    matchState.CurrentPhase,
                    NetworkMatchPhase.CoreObjective)
                || IsCoreObjectiveComplete)
            {
                return;
            }

            var coreId = context.PlayerState.CarriedCoreId;
            if (!Runner.TryFindObject(coreId, out var coreObject)
                || coreObject == null
                || !coreObject.TryGetComponent<NetworkPickupItem>(out var core)
                || !core.TryPlace(context.Player, transform.position + Vector3.up * 0.75f))
            {
                return;
            }

            PlacedCoreCount = Mathf.Min(PlacedCoreCount + 1, _requiredCoreCount);
            AdvanceObjectiveOrdinal();
            HandleObjectiveChanged();
            if (IsCoreObjectiveComplete)
            {
                matchState.TryCompleteCoreObjective(this);
            }
        }

        private void CompletePowerPuzzle(NetworkMatchState matchState)
        {
            if (!NetworkMatchStateRules.IsObjectiveMutationAllowed(
                    matchState.Status,
                    matchState.CurrentPhase,
                    NetworkMatchPhase.Puzzle))
            {
                return;
            }

            AdvanceObjectiveOrdinal();
            MatchAuthorityRuntime.Instance?.RecordPuzzleCompleted(BuildKey("puzzle-completed"));
            matchState.TryCompletePuzzle(Object.Id);
        }

        private void AdvanceSecurityHold(NetworkMatchState matchState)
        {
            if (!NetworkMatchStateRules.IsObjectiveMutationAllowed(
                    matchState.Status,
                    matchState.CurrentPhase,
                    NetworkMatchPhase.SecurityHold))
            {
                return;
            }

            AdvanceObjectiveOrdinal();
            if (!SecurityHoldWasInterrupted)
            {
                SecurityHoldWasInterrupted = true;
                MatchAuthorityRuntime.Instance?.RecordSecurityHoldInterrupted(
                    BuildKey("security-hold-interrupted"));
                return;
            }

            matchState.TryCompleteSecurityHold(Object.Id);
        }

        private bool TryGetMatchState(out NetworkMatchState matchState)
        {
            matchState = null;
            return MatchStateId.IsValid
                && Runner.TryFindObject(MatchStateId, out var matchObject)
                && matchObject != null
                && matchObject.TryGetComponent(out matchState);
        }

        private void AdvanceObjectiveOrdinal()
        {
            ObjectiveOrdinal++;
            if (ObjectiveOrdinal == 0) ObjectiveOrdinal = 1;
        }

        private string BuildKey(string occurrence)
        {
            return $"objective:{Object.Id}:{occurrence}:{ObjectiveOrdinal}";
        }

        private void HandleObjectiveChanged()
        {
            foreach (var legacyProgress in FindObjectsByType<EnergyCoreObjectiveProgress>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacyProgress.SetNetworkAuthorityPresentationOnly(true);
                legacyProgress.ApplyAuthoritativeSnapshot(PlacedCoreCount, _requiredCoreCount);
            }
        }
    }
}
