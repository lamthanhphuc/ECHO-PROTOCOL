using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.AED;
using Fusion;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Special
{
    public sealed partial class StalkerSpecialEncounterRuntime
    {
        private readonly AEDV02CabinetEncounter _cabinet = new AEDV02CabinetEncounter();
        private double _cabinetNow;
        private Guid _cabinetMatch;
        private double _cabinetLastOtherSpecial = double.NegativeInfinity;
        private Vector3 _cabinetEntry;
        private Vector3 _cabinetInspectPoint;
        private Func<bool> _cabinetRevalidate;
        private Func<AEDV02CabinetInspection> _cabinetInspect;
        public AEDV02CabinetEncounter Cabinet => _cabinet;
        private StalkerSpecialEncounterPhase CabinetPresentationPhase => _cabinet.Phase == AEDV02CabinetPhase.JumpIn
            ? StalkerSpecialEncounterPhase.JumpIn : StalkerSpecialEncounterPhase.None;
        private void ResetCabinet()
        {
            _cabinet.Reset(Guid.Empty); _cabinetMatch = Guid.Empty; _cabinetNow = 0;
            _cabinetLastOtherSpecial = double.NegativeInfinity;
            _cabinetRevalidate = null; _cabinetInspect = null;
        }
        public bool TryBeginCabinet(Guid match, Guid player, ulong spot, int zone, double now, double clueAt,
            AEDV02PressureSnapshot pressure, string sourceFingerprint, int positiveTokens, IEnumerable<Guid> clients,
            Vector3 entry, Vector3 inspectPoint, IReadOnlyList<Transform> aliveRoots, Func<bool> revalidate, Func<AEDV02CabinetInspection> inspect)
        {
            ResolveDependencies();
            if (_phase != StalkerSpecialEncounterPhase.None || controller == null || settings == null || !settings.Enabled ||
                controller.CurrentState is not (StalkerState.PATROL or StalkerState.SEARCH) || controller.SpecialEncounterOverrideActive ||
                controller.HasActiveHideSpotInvestigation || IsCoolingDown(new AiSimulationTime(0, now)) || revalidate == null || inspect == null ||
                !revalidate() || aliveRoots == null || !controller.CanStartSpecialEncounter(settings.MaxDirectorPressureForStart)) return false;
            if (_cabinetMatch != match)
            {
                _cabinet.Reset(match); _cabinetMatch = match;
                _cabinet.RecordOtherSpecial(_cabinetLastOtherSpecial);
            }
            if (!_cabinet.TryReserve(match, player, spot, zone, now, clueAt, pressure, sourceFingerprint, positiveTokens, clients)) return false;
            _cabinetEntry = entry; _cabinetInspectPoint = inspectPoint; _cabinetRevalidate = revalidate; _cabinetInspect = inspect;
            _eligibleAlivePlayerRoots.Clear(); foreach (var root in aliveRoots) _eligibleAlivePlayerRoots.Add(root);
            _sequenceOrdinal++; _cabinetNow = now; _ownsControllerOverride = true; controller.BeginSpecialEncounterOverride();
            Debug.Log($"[AED_V02][CABINET_RESERVED] match={match:D} encounter={_cabinet.EncounterId:D} source={sourceFingerprint} spot={spot} zone={zone}");
            return true;
        }
        public bool AcknowledgeCabinetCue(Guid encounter, Guid client, bool audio, bool visual, double now) =>
            _cabinet.AcknowledgeCue(encounter, client, audio, visual, now);
        private void RecordCabinetSharedSpecial()
        {
            _cabinetLastOtherSpecial = Math.Max(_cabinetLastOtherSpecial, _cabinetNow);
            _cabinet.RecordOtherSpecial(_cabinetNow);
        }
        private void TickCabinet(NetworkRunner runner, AiSimulationStep step)
        {
            if (!step.IsValid) return;
            var previous = _cabinet.Phase;
            bool valid = runner != null && runner.IsRunning && runner.IsServer && _cabinetRevalidate?.Invoke() == true;
            bool arrived = _cabinet.Phase == AEDV02CabinetPhase.Approach && controller.HasArrivedAtSpecialEncounterDestination(settings.StagingArrivalTolerance);
            var inspected = _cabinet.Phase == AEDV02CabinetPhase.Inspect && valid ? _cabinetInspect() : AEDV02CabinetInspection.Pending;
            _cabinet.Tick(step.Time.Seconds, valid, arrived, inspected);
            if (_cabinet.Phase != previous)
            {
                Debug.Log($"[AED_V02][CABINET_PHASE] encounter={_cabinet.EncounterId:D} phase={_cabinet.Phase} reason={_cabinet.Reason}");
                if (_cabinet.Phase == AEDV02CabinetPhase.JumpIn)
                {
                    _jumpInPosition = _cabinetEntry;
                    if (_cabinetRevalidate?.Invoke() != true || !TryTeleportToJumpIn()) _cabinet.Abort("ABORT_ENTRY_TELEPORT", step.Time.Seconds);
                }
                else if (_cabinet.Phase == AEDV02CabinetPhase.Approach)
                {
                    if (!controller.TrySetSpecialEncounterDestination(_cabinetInspectPoint)) _cabinet.Abort("ABORT_APPROACH_PATH", step.Time.Seconds);
                }
                else if (_cabinet.Phase == AEDV02CabinetPhase.Recovery) controller.StopSpecialEncounterNavigation();
            }
            if (_cabinet.Phase is AEDV02CabinetPhase.Backoff or AEDV02CabinetPhase.Idle && _ownsControllerOverride)
            { _ownsControllerOverride = false; controller.EndSpecialEncounterOverrideToPatrol(); _cabinetRevalidate = null; _cabinetInspect = null; }
        }
    }
}
