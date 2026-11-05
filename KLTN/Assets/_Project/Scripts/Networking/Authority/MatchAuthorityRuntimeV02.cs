using System;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Gameplay;

namespace EchoProtocol.Networking.Authority
{
    public sealed partial class MatchAuthorityRuntime
    {
        public uint V02EvidencePhaseOrdinal => _aedv2Evidence.CurrentPhaseOrdinal;
        public string V02EvidencePhaseName => _currentTelemetryPhase;
        public bool TryProtectAEDV02Hiding(NetworkPlayerMovement movement, out AEDV02HideProtectionToken token)
        {
            token = null;
            if (!HasStateAuthority || movement == null || movement.Object == null || !movement.Object.IsValid ||
                !movement.Object.HasStateAuthority || movement.Runner != _bootstrap.Runner || !movement.IsHidden || movement.CurrentHideSpotId == 0 ||
                !TryResolveVerifiedBackendUserId(movement.Object.InputAuthority, out var identity) || !Guid.TryParse(identity, out var user)) return false;
            double now = _bootstrap.Runner.Tick.Raw / (double)_bootstrap.Runner.TickRate;
            if (AEDv2Authority.TryGetExistingV02Hide(MatchId, user, movement.CurrentHideSpotId, now, out token)) return true;
            var life = movement.GetComponent<NetworkPlayerLifeState>();
            var health = movement.GetComponent<NetworkPlayerHealth>();
            if (life == null || life.IsDowned || life.IsCaught || life.IsEliminated || health != null && health.IsDowned) return false;
            if (!_telemetryMatchActive || RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || Difficulty != MatchDifficulty.Normal ||
                ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.PacingV02GameplayEnabled != true) return false;
            return AEDv2Authority.TryGrantV02Hide(MatchId, user, movement.CurrentHideSpotId, now, DateTime.UtcNow, out token);
        }
    }
}
