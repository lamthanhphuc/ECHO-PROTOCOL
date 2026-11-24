using EchoProtocol.Audio;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked] private TickTimer EnvironmentCueTimer { get; set; }
        [Networked] public uint EnvironmentCueOrdinal { get; private set; }
        [Networked] private Vector3 LastEnvironmentCuePosition { get; set; }
        [Networked] private TickTimer HornTimer { get; set; }
        [Networked] public uint HornOrdinal { get; private set; }

        // Authority simulation owns the timer; anchors are scanned only when it expires.
        public void TickEnvironmentAudio()
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || IsEnded) return;
            TickHorn();
            if (!EnvironmentCueTimer.IsRunning)
            { EnvironmentCueTimer = TickTimer.CreateFromSeconds(Runner, 60); return; }
            if (!EnvironmentCueTimer.Expired(Runner)) return;
            if (CurrentPhase == NetworkMatchPhase.FinalHunt || CurrentPhase == NetworkMatchPhase.Escape
                || CurrentPhase == NetworkMatchPhase.SecurityHold || PurgeMood != PurgeBossMood.Calm)
            { EnvironmentCueTimer = TickTimer.CreateFromSeconds(Runner, 20); return; }
            EnvironmentAudioAnchor selected = null; int candidates = 0;
            foreach (var anchor in EnvironmentAudioAnchor.Active)
            {
                if (anchor == null || !anchor.isActiveAndEnabled || anchor.gameObject.scene.name != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    || (EnvironmentCueOrdinal > 0 && (anchor.transform.position - LastEnvironmentCuePosition).sqrMagnitude < 4)) continue;
                bool nearby = false;
                foreach (var player in Runner.ActivePlayers)
                {
                    if (!Runner.TryGetPlayerObject(player, out var obj) || obj == null || !obj.IsValid) continue;
                    if (obj.TryGetComponent<NetworkPlayerLifeState>(out var life) && life.Status != NetworkPlayerLifeStatus.Alive) continue;
                    if (obj.TryGetComponent<NetworkPlayerInteractor>(out var interaction) && interaction.CurrentReviveTarget != null) continue;
                    float d = (obj.transform.position - anchor.transform.position).sqrMagnitude;
                    if (d > 16 && d < anchor.range * anchor.range) { nearby = true; break; }
                }
                if (nearby && Random.Range(0, ++candidates) == 0) selected = anchor;
            }
            EnvironmentCueTimer = TickTimer.CreateFromSeconds(Runner, selected != null ? Random.Range(65, 101) : 15);
            if (selected == null) return;
            LastEnvironmentCuePosition = selected.transform.position;
            EnvironmentCueOrdinal++;
            RpcEnvironmentAudio(EnvironmentCueOrdinal, selected.cue, selected.transform.position, selected.gain, selected.range, selected.duration);
        }

        private void TickHorn()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene != "Scene_A" && scene != "Scene_B") return;
            if (!HornTimer.IsRunning) { HornTimer = TickTimer.CreateFromSeconds(Runner, 90); return; }
            if (!HornTimer.Expired(Runner)) return;
            // No audience-distance condition: a distant foghorn belongs to the whole outdoor map.
            foreach (var player in Runner.ActivePlayers)
                if (Runner.TryGetPlayerObject(player, out var obj) && obj != null
                    && obj.TryGetComponent<NetworkPlayerLifeState>(out var life) && life.IsReviveInProgress)
                { HornTimer = TickTimer.CreateFromSeconds(Runner, 10); return; }
            HornTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(180, 301));
            HornOrdinal++;
            RpcMapHorn(HornOrdinal);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcMapHorn(uint ordinal) => GameAudioRuntime.PresentMapHorn(this, ordinal);

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcEnvironmentAudio(uint ordinal, string cue, Vector3 position, float gain, float range, float duration)
            => GameAudioRuntime.PresentEnvironmentCue(this, ordinal, cue, position, gain, range, duration);
    }
}
