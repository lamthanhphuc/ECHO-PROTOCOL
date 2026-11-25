using System.Linq;
using EchoProtocol.Cinematics;
using EchoProtocol.Networking.Loading;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked] public OpeningCutsceneState OpeningState { get; private set; }
        [Networked] public NetworkBool HasOpeningCutscene { get; private set; }
        [Networked] public int OpeningStartTick { get; private set; }
        [Networked] public float OpeningDuration { get; private set; }
        [Networked] public NetworkBool OpeningSkipScheduled { get; private set; }
        [Networked] public TickTimer OpeningEndTimer { get; private set; }
        [Networked, Capacity(4)] private NetworkArray<PlayerRef> OpeningVotes => default;

        public bool IsGameplayReleased => OpeningCutsceneRules.GameplayReleased(HasOpeningCutscene, OpeningState);
        public double OpeningElapsed => Runner == null ? 0d
            : OpeningCutsceneRules.Elapsed(Runner.Tick.Raw, OpeningStartTick, Runner.TickRate);
        public float OpeningRemaining => OpeningEndTimer.RemainingTime(Runner) ?? 0f;
        public int OpeningConnectedPlayers => ConnectedOpeningPlayers().Count();
        public int OpeningSkipVotes => OpeningCutsceneRules.CountVotes(
            ConnectedOpeningPlayers().Select(p => p.RawEncoded), OpeningVotes.Select(p => p.RawEncoded));

        private System.Collections.Generic.IEnumerable<PlayerRef> ConnectedOpeningPlayers() => Runner.ActivePlayers.Where(p => p.IsRealPlayer);

        private bool IsOpeningParticipant(PlayerRef player) => player.IsRealPlayer
            && ConnectedOpeningPlayers().Contains(player) && Runner.TryGetPlayerObject(player, out var obj)
            && obj != null && obj.IsValid && obj.InputAuthority == player
            && obj.TryGetComponent<LobbyPlayerState>(out var lobby) && lobby.IsGameplayPlayer && lobby.GameplaySpawnReady;

        private void InitializeOpeningAuthoritative()
        {
            var controller = OpeningCutsceneController.FindForScene(gameObject.scene);
            HasOpeningCutscene = controller != null && controller.IsPlayable;
            OpeningState = HasOpeningCutscene ? OpeningCutsceneState.NotStarted : OpeningCutsceneState.Completed;
            OpeningDuration = HasOpeningCutscene ? controller.Definition.Duration : 0f;
            OpeningStartTick = 0;
            OpeningEndTimer = TickTimer.None;
            OpeningSkipScheduled = false;
            for (int i = 0; i < OpeningVotes.Length; i++) OpeningVotes.Set(i, PlayerRef.None);
        }

        // Invoked on the existing match object's simulation; never creates another Runner or network object.
        private bool TickOpeningAuthoritative()
        {
            if (!HasOpeningCutscene || OpeningState == OpeningCutsceneState.Completed) return false;
            if (OpeningState == OpeningCutsceneState.NotStarted)
            {
                var loading = MatchLoadingCoordinator.Instance;
                if (loading != null && loading.IsTrackingRunner(Runner) && !loading.GameplayReadinessReleased) return true;
                var players = Runner.ActivePlayers.Where(p => p.IsRealPlayer).ToArray();
                if (players.Length == 0 || players.Any(p => !Runner.TryGetPlayerObject(p, out var obj)
                    || obj == null || !obj.IsValid || !obj.TryGetComponent<LobbyPlayerState>(out var lobby)
                    || !lobby.IsGameplayPlayer || !lobby.GameplaySpawnReady)) return true;
                var controller = OpeningCutsceneController.FindForScene(gameObject.scene);
                if (controller == null || !controller.IsPlayable) { CompleteOpeningAuthoritative(); return false; }
                OpeningStartTick = Runner.Tick.Raw + Mathf.CeilToInt(OpeningCutsceneRules.StartLeadSeconds * Runner.TickRate);
                OpeningEndTimer = TickTimer.CreateFromSeconds(Runner, OpeningCutsceneRules.StartLeadSeconds + OpeningDuration);
                OpeningState = OpeningCutsceneState.Playing;
                Debug.Log($"[OpeningCutscene] start={OpeningStartTick} duration={OpeningDuration} object={Object.Id}");
            }
            if (!OpeningSkipScheduled && OpeningCutsceneRules.Unanimous(
                    ConnectedOpeningPlayers().Select(p => p.RawEncoded), OpeningVotes.Select(p => p.RawEncoded)))
            {
                OpeningSkipScheduled = true;
                OpeningEndTimer = TickTimer.CreateFromSeconds(Runner, OpeningCutsceneRules.FadeSeconds);
            }
            // Replicated timer is also the watchdog. It survives snapshots and cannot depend on a local stopped event.
            if (OpeningEndTimer.ExpiredOrNotRunning(Runner))
            {
                CompleteOpeningAuthoritative();
                return false;
            }
            return true;
        }

        private void CompleteOpeningAuthoritative()
        {
            if (!Object.HasStateAuthority || OpeningState == OpeningCutsceneState.Completed) return;
            OpeningState = OpeningCutsceneState.Completed;
            OpeningEndTimer = TickTimer.None;
            MatchTimer = TickTimer.CreateFromSeconds(Runner, EffectiveMatchDurationSeconds);
            Debug.Log("[OpeningCutscene] Gameplay released; mission timer started.");
        }

        public bool HasOpeningVote(PlayerRef player) => OpeningVotes.Any(vote => vote == player);

        public void RequestOpeningSkip() => RpcOpeningSkip();
        public void ReportOpeningUnavailable() => RpcOpeningUnavailable();

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RpcOpeningSkip(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || !HasOpeningCutscene || !IsOpeningParticipant(info.Source) || !OpeningCutsceneRules.CanVote(OpeningState,
                info.Source.RawEncoded, ConnectedOpeningPlayers().Select(p => p.RawEncoded), OpeningVotes.Select(p => p.RawEncoded))) return;
            for (int i = 0; i < OpeningVotes.Length; i++)
                if (OpeningVotes[i] == PlayerRef.None) { OpeningVotes.Set(i, info.Source); return; }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RpcOpeningUnavailable(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || !HasOpeningCutscene || OpeningState != OpeningCutsceneState.Playing
                || !IsOpeningParticipant(info.Source)) return;
            // A peer with a broken/missing asset cannot release itself: the authority schedules everyone together.
            if (!OpeningSkipScheduled)
            {
                OpeningSkipScheduled = true;
                OpeningEndTimer = TickTimer.CreateFromSeconds(Runner, OpeningCutsceneRules.FadeSeconds);
                Debug.LogWarning("[OpeningCutscene] Peer reported unavailable presentation; synchronized fallback.");
            }
        }
    }
}
