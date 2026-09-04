using System;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Authoritative objective source; match phase and final result live in NetworkMatchState.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSectorBox : NetworkInteractable
    {
        public static event Action<NetworkSectorBox> ObjectiveStateChanged;

        [SerializeField, Min(1)] private int _requiredCoreCount = 3;
        [SerializeField] private Transform[] _corePlacementPoints = Array.Empty<Transform>();
        [SerializeField, Min(0.1f)] private float _fallbackSlotSpacing = 0.65f;
        [SerializeField] private Renderer _embeddedFallbackRenderer;

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
            ConfigurePresentation();
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
                    return EnergyCoreObjectiveRules.CanRegisterPlacement(PlacedCoreCount, _requiredCoreCount)
                           && context.PlayerState.CarriedCoreId.IsValid
                           && Runner.TryFindObject(context.PlayerState.CarriedCoreId, out var coreObject)
                           && coreObject != null
                           && coreObject.TryGetComponent<NetworkPickupItem>(out var core)
                           && core.CanBePlacedBy(context.Player)
                        ? InteractionValidationResult.Accepted
                        : InteractionValidationResult.InvalidTargetState;
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
                || !EnergyCoreObjectiveRules.CanRegisterPlacement(PlacedCoreCount, _requiredCoreCount))
            {
                return;
            }

            var coreId = context.PlayerState.CarriedCoreId;
            var slotIndex = PlacedCoreCount;
            GetPlacementPose(slotIndex, out var position, out var rotation);
            if (!Runner.TryFindObject(coreId, out var coreObject)
                || coreObject == null
                || !coreObject.TryGetComponent<NetworkPickupItem>(out var core)
                || !core.TryPlace(context.Player, Object.Id, slotIndex, position, rotation))
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

        public bool TryCommitPowerPuzzleCompletion(NetworkId puzzleId)
        {
            if (!Object.HasStateAuthority
                || !puzzleId.IsValid
                || !TryGetMatchState(out var matchState)
                || !NetworkMatchStateRules.IsObjectiveMutationAllowed(
                    matchState.Status,
                    matchState.CurrentPhase,
                    NetworkMatchPhase.Puzzle)
                || !Runner.TryFindObject(puzzleId, out var puzzleObject)
                || puzzleObject == null
                || !puzzleObject.TryGetComponent<NetworkPowerPuzzle>(out var puzzle)
                || puzzle.State != NetworkPowerPuzzleState.Completed)
            {
                return false;
            }

            AdvanceObjectiveOrdinal();
            MatchAuthorityRuntime.Instance?.RecordPuzzleCompleted(BuildKey("puzzle-completed"));
            return matchState.TryCompletePuzzle(Object.Id);
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

        private void GetPlacementPose(int slotIndex, out Vector3 position, out Quaternion rotation)
        {
            if (slotIndex >= 0
                && slotIndex < _corePlacementPoints.Length
                && _corePlacementPoints[slotIndex] != null)
            {
                position = _corePlacementPoints[slotIndex].position;
                rotation = _corePlacementPoints[slotIndex].rotation;
                return;
            }

            var centeredIndex = slotIndex - (_requiredCoreCount - 1) * 0.5f;
            position = transform.TransformPoint(new Vector3(centeredIndex * _fallbackSlotSpacing, 0.75f, 0f));
            rotation = transform.rotation;
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

            ObjectiveStateChanged?.Invoke(this);
        }

        private void ConfigurePresentation()
        {
            if (_embeddedFallbackRenderer == null) return;

            var legacyBoxes = FindObjectsByType<SectorBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _embeddedFallbackRenderer.enabled = legacyBoxes.Length == 0;
        }
    }

    public static class EnergyCoreObjectiveRules
    {
        public static bool CanRegisterPlacement(int placedCoreCount, int requiredCoreCount)
        {
            return requiredCoreCount > 0
                && placedCoreCount >= 0
                && placedCoreCount < requiredCoreCount;
        }
    }
}
