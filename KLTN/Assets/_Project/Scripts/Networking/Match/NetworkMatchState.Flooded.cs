using Fusion;
using UnityEngine;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked] public NetworkBool FloodedEscortPoseReady { get; private set; }
        [Networked] public NetworkBool FloodedEscortArrived { get; private set; }
        [Networked] public int FloodedRouteIndex { get; private set; }
        [Networked] public NetworkBool FloodedEscortMoving { get; private set; }
        private TickTimer _floodedChargeLease;

        public void RequestFloodedExit()
        {
            if (!HasValidNetworkObject()) return;
            if (Object.HasStateAuthority)
            {
                if (TryGetLocalRequester(out var actor)) TryFloodedExit(actor);
            }
            else RpcFloodedExit();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcFloodedExit(RpcInfo info = default) => TryFloodedExit(info.Source);

        private void TryFloodedExit(PlayerRef actor)
        {
            var map = FloodedMapMission.Instance;
            if (!Object.HasStateAuthority || IsEnded || !map || !map.ExitTarget || !FloodedEscortArrived
                || (CurrentPhase != NetworkMatchPhase.Zone3PushFrigate && CurrentPhase != NetworkMatchPhase.Escape)
                || !TryResolveActivePlayer(actor, out var life)
                || !TryValidateZone2Requester(actor, map.ExitTarget, 3f)) return;
            if (CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                && !TryAdvancePhase(NetworkMatchPhase.Zone3PushFrigate, NetworkMatchPhase.Escape, "MAP2_ESCORT")) return;
            StartEscapeDeadlineIfNeededAuthoritative("MAP2_EXIT");
            LastActor = actor;
            if (!life.TryEscape()) return;
            MatchAuthorityRuntime.Instance?.RecordObjectiveContribution(actor);
            CountFinalPlayers(out var escaped, out _, out _, out _, out var remaining);
            if (escaped > 0 && remaining == 0)
                TryEndMatch(NetworkMatchResult.Win, NetworkMatchEndReason.PlayerEscaped, actor);
        }

        public void RequestFloodedCharge(bool holding)
        {
            if (Object.HasStateAuthority)
            {
                if (TryGetLocalRequester(out var actor)) SetFloodedCharge(actor, holding);
            }
            else RpcFloodedCharge(holding);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcFloodedCharge(bool holding, RpcInfo info = default) => SetFloodedCharge(info.Source, holding);

        private void SetFloodedCharge(PlayerRef actor, bool holding)
        {
            var map = FloodedMapMission.Instance;
            if (!Object.HasStateAuthority || IsEnded || !map || CurrentPhase != NetworkMatchPhase.Zone3FindFrigate) return;
            if (!holding)
            {
                if (Zone3ChargeOperator == actor) Zone3ChargeOperator = PlayerRef.None;
                return;
            }
            if ((!Zone3ChargeOperator.IsNone && Zone3ChargeOperator != actor)
                || !TryResolveActivePlayer(actor, out var life)
                || !map.ChargeTarget
                || (map.ChargeTarget.ClosestPoint(life.transform.position) - life.transform.position).sqrMagnitude > 16f) return;
            Zone3ChargeOperator = actor;
            Zone3ChargeDurationSeconds = map.ChargeSeconds;
            _floodedChargeLease = TickTimer.CreateFromSeconds(Runner, .75f);
        }

        private void TickFloodedMission()
        {
            var map = FloodedMapMission.Instance;
            if (!map || !map.Escort || !map.Destination) return;
            FloodedEscortMoving = false;
            if (!FloodedEscortPoseReady)
            {
                Zone3FrigatePosition = map.Escort.position;
                Zone3FrigateRotation = map.Escort.rotation;
                FloodedEscortPoseReady = true;
                FloodedRouteIndex = 0;
            }
            if (CurrentPhase == NetworkMatchPhase.Zone3FindFrigate)
            {
                if (Zone3ChargeOperator.IsRealPlayer
                    && (_floodedChargeLease.ExpiredOrNotRunning(Runner)
                        || !TryResolveActivePlayer(Zone3ChargeOperator, out var charger)
                        || !map.ChargeTarget
                        || (map.ChargeTarget.ClosestPoint(charger.transform.position) - charger.transform.position).sqrMagnitude > 16f))
                    Zone3ChargeOperator = PlayerRef.None;
                Zone3ChargeAccumulatedSeconds = Mathf.Clamp(Zone3ChargeAccumulatedSeconds
                    + Runner.DeltaTime * (Zone3ChargeOperator.IsRealPlayer ? 1f : -.2f), 0f, map.ChargeSeconds);
                if (Zone3ChargeAccumulatedSeconds >= map.ChargeSeconds)
                {
                    Zone3ChargeOperator = PlayerRef.None;
                    TryAdvancePhase(NetworkMatchPhase.Zone3FindFrigate, NetworkMatchPhase.Zone3PushFrigate, "MAP2_ESCORT_CHARGED");
                }
                return;
            }
            if (CurrentPhase != NetworkMatchPhase.Zone3PushFrigate) return;
            if (FloodedEscortArrived) return;
            PlayerRef nearby = PlayerRef.None;
            foreach (var player in Runner.ActivePlayers)
                if (TryResolveActivePlayer(player, out var life)
                    && new Vector2(life.transform.position.x - Zone3FrigatePosition.x,
                        life.transform.position.z - Zone3FrigatePosition.z).sqrMagnitude <= map.EscortRadius * map.EscortRadius)
                { nearby = player; break; }
            if (nearby.IsNone) return;
            var route = map.Route;
            if (route == null || route.Length == 0) return;
            if (FloodedRouteIndex < route.Length)
            {
                var point = route[FloodedRouteIndex];
                if (!point) return;
                var delta = point.position - Zone3FrigatePosition;
                FloodedEscortMoving = delta.sqrMagnitude > .0001f;
                Zone3FrigatePosition = Vector3.MoveTowards(Zone3FrigatePosition, point.position, map.Speed * Runner.DeltaTime);
                var horizontal = new Vector3(delta.x, 0f, delta.z);
                if (horizontal.sqrMagnitude > .001f)
                    Zone3FrigateRotation = Quaternion.RotateTowards(Zone3FrigateRotation, Quaternion.LookRotation(horizontal), 45f * Runner.DeltaTime);
                if ((Zone3FrigatePosition - point.position).sqrMagnitude <= .04f) FloodedRouteIndex++;
            }
            if (FloodedRouteIndex < route.Length) return;
            FloodedEscortArrived = map.IsEscortInDock(Zone3FrigatePosition);
            FloodedEscortMoving = false;
        }
    }
}
