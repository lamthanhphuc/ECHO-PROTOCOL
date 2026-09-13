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

        [SerializeField, Min(1)] private int _requiredCoreCount = 2;
        [SerializeField] private Transform[] _corePlacementPoints = Array.Empty<Transform>();
        [SerializeField] private GameObject[] _coreVisuals = Array.Empty<GameObject>();
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
                Debug.LogWarning($"[NetworkSectorBox] Reject interact: invalid requester. box={Object.Id}, player={context.Player}");
                return InteractionValidationResult.InvalidRequester;
            }

            if (!TryGetMatchState(out var matchState) || matchState.IsEnded)
            {
                Debug.LogWarning($"[NetworkSectorBox] Reject interact: missing/ended match state. box={Object.Id}, matchStateId={MatchStateId}");
                return InteractionValidationResult.InvalidTargetState;
            }

            switch (matchState.CurrentPhase)
            {
                case NetworkMatchPhase.CoreObjective:
                    if (!EnergyCoreObjectiveRules.CanRegisterPlacement(PlacedCoreCount, _requiredCoreCount))
                    {
                        Debug.LogWarning($"[NetworkSectorBox] Reject placement: capacity full/invalid. box={Object.Id}, placed={PlacedCoreCount}, required={_requiredCoreCount}");
                        return InteractionValidationResult.InvalidTargetState;
                    }

                    if (!context.PlayerState.CarriedCoreId.IsValid)
                    {
                        Debug.LogWarning($"[NetworkSectorBox] Reject placement: player is not carrying a network core. box={Object.Id}, player={context.Player}");
                        return InteractionValidationResult.InvalidTargetState;
                    }

                    if (!Runner.TryFindObject(context.PlayerState.CarriedCoreId, out var coreObject)
                        || coreObject == null
                        || !coreObject.TryGetComponent<NetworkPickupItem>(out var core))
                    {
                        Debug.LogWarning($"[NetworkSectorBox] Reject placement: carried core object not found. box={Object.Id}, player={context.Player}, core={context.PlayerState.CarriedCoreId}");
                        return InteractionValidationResult.InvalidTargetState;
                    }

                    if (!core.CanBePlacedBy(context.Player, context.PlayerState))
                    {
                        Debug.LogWarning($"[NetworkSectorBox] Reject placement: carried core cannot be placed. box={Object.Id}, player={context.Player}, core={core.Object.Id}, state={core.State}, holder={core.Holder}");
                        return InteractionValidationResult.InvalidTargetState;
                    }

                    return InteractionValidationResult.Accepted;
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
                || !core.TryPlace(context.Player, Object.Id, slotIndex, position, rotation, context.PlayerState))
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

        private SectorBox _boundSceneSectorBox;

        private void ResolveSceneSectorBox()
        {
            if (_boundSceneSectorBox != null) return;
            var boxes = FindObjectsByType<SectorBox>(FindObjectsInactive.Include);
            SectorBox closest = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (box == null) continue;
                float dist = Vector3.Distance(transform.position, box.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = box;
                }
            }

            if (closest != null && closestDist < 3.0f)
            {
                _boundSceneSectorBox = closest;
                if ((_corePlacementPoints == null || _corePlacementPoints.Length == 0)
                    && closest.CoreSockets != null && closest.CoreSockets.Length > 0)
                {
                    _corePlacementPoints = closest.CoreSockets;
                }

                if ((_coreVisuals == null || _coreVisuals.Length == 0)
                    && closest.CoreVisuals != null && closest.CoreVisuals.Length > 0)
                {
                    _coreVisuals = closest.CoreVisuals;
                }
            }
        }

        private void GetPlacementPose(int slotIndex, out Vector3 position, out Quaternion rotation)
        {
            ResolveSceneSectorBox();
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
            ResolveSceneSectorBox();
            if (_boundSceneSectorBox != null)
            {
                _boundSceneSectorBox.UpdateCoreVisuals(PlacedCoreCount);
            }
            UpdateCoreVisuals(PlacedCoreCount);

            int totalPlaced = 0;
            int totalRequired = 0;
            var allBoxes = FindObjectsByType<NetworkSectorBox>(FindObjectsInactive.Include);
            for (int i = 0; i < allBoxes.Length; i++)
            {
                var box = allBoxes[i];
                if (box == null) continue;
                totalPlaced += box.PlacedCoreCount;
                totalRequired += box.RequiredCoreCount;
            }

            if (totalRequired == 0)
            {
                totalPlaced = PlacedCoreCount;
                totalRequired = _requiredCoreCount;
            }

            foreach (var legacyProgress in FindObjectsByType<EnergyCoreObjectiveProgress>(FindObjectsInactive.Include))
            {
                legacyProgress.SetNetworkAuthorityPresentationOnly(true);
                legacyProgress.ApplyAuthoritativeSnapshot(PlacedCoreCount, _requiredCoreCount);
                legacyProgress.ApplyAuthoritativeSnapshot(totalPlaced, totalRequired);
            }

            ObjectiveStateChanged?.Invoke(this);
        }

        private void UpdateCoreVisuals(int placedCount)
        {
            int clampedPlaced = Mathf.Clamp(placedCount, 0, _requiredCoreCount);
            if (_coreVisuals == null) return;

            for (int i = 0; i < _coreVisuals.Length; i++)
            {
                var coreVisual = _coreVisuals[i];
                if (coreVisual == null) continue;

                bool active = i < clampedPlaced;
                coreVisual.SetActive(active);
                if (!active) continue;

                foreach (Transform child in coreVisual.transform)
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        private void ConfigurePresentation()
        {
            ResolveSceneSectorBox();
            if (_embeddedFallbackRenderer == null) return;

            var legacyBoxes = FindObjectsByType<SectorBox>(FindObjectsInactive.Include);
            _embeddedFallbackRenderer.enabled = legacyBoxes.Length == 0;
        }
    }

}
