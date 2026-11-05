using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDV02CabinetPhase { Idle, CuePending, WarningLead, JumpIn, Approach, Inspect, Recovery, Backoff }
    public enum AEDV02CabinetInspection { Pending, Pulled, Empty, Protected, Failed }

    public sealed class AEDV02CabinetEncounter
    {
        private readonly Dictionary<int, int> _zoneCounts = new Dictionary<int, int>();
        private readonly HashSet<Guid> _players = new HashSet<Guid>();
        private readonly HashSet<Guid> _requiredCues = new HashSet<Guid>();
        private readonly HashSet<Guid> _readyCues = new HashSet<Guid>();
        private Guid _match;
        private double _phaseBegan;
        private double _cooldownUntil;
        private double _lastSpecial = double.NegativeInfinity;
        private bool _quotaConsumed;
        public AEDV02CabinetPhase Phase { get; private set; }
        public Guid EncounterId { get; private set; }
        public double LastActivationAt { get; private set; } = double.NegativeInfinity;
        public Guid PlayerId { get; private set; }
        public ulong SpotId { get; private set; }
        public int Zone { get; private set; }
        public string SourceFingerprint { get; private set; } = string.Empty;
        public string Reason { get; private set; } = string.Empty;
        public bool IsActive => Phase is AEDV02CabinetPhase.CuePending or AEDV02CabinetPhase.WarningLead or AEDV02CabinetPhase.JumpIn or
            AEDV02CabinetPhase.Approach or AEDV02CabinetPhase.Inspect or AEDV02CabinetPhase.Recovery;
        public double Elapsed(double now) => Math.Max(0, now - _phaseBegan);
        public void Reset(Guid match)
        {
            _match = match; _zoneCounts.Clear(); _players.Clear(); _requiredCues.Clear(); _readyCues.Clear();
            _cooldownUntil = 0; _lastSpecial = double.NegativeInfinity; LastActivationAt = double.NegativeInfinity; _quotaConsumed = false; EncounterId = Guid.Empty;
            PlayerId = Guid.Empty; SpotId = 0; Zone = 0; SourceFingerprint = string.Empty; Reason = string.Empty; Phase = AEDV02CabinetPhase.Idle;
        }
        public void RecordOtherSpecial(double now) { if (AEDV02PilotPolicy.Finite(now)) _lastSpecial = Math.Max(_lastSpecial, now); }
        public bool TryReserve(Guid match, Guid player, ulong spot, int zone, double now, double clueObservedAt,
            AEDV02PressureSnapshot pressure, string sourceFingerprint, int positiveTokens, IEnumerable<Guid> relevantClients)
        {
            Reason = "HOLD_CABINET_REVALIDATION";
            if (match == Guid.Empty || _match != match || player == Guid.Empty || spot == 0 || zone is < 1 or > 2 || IsActive ||
                !AEDV02PilotPolicy.Finite(now) || !AEDV02PilotPolicy.Finite(clueObservedAt) || clueObservedAt > now ||
                now - clueObservedAt > AEDV02PilotPolicy.AmbushClueAgeSeconds || !pressure.Complete || pressure.Tier is < 0 or > 1 ||
                !AEDV02WorldSample.Hash(sourceFingerprint) || positiveTokens != 0 || now < _cooldownUntil ||
                now - _lastSpecial < AEDV02PilotPolicy.SharedSpecialSeparationSeconds || _players.Count >= 2 || _players.Contains(player) ||
                _zoneCounts.TryGetValue(zone, out int count) && count >= 1 || relevantClients == null) return false;
            var clients = relevantClients.ToArray();
            if (clients.Length is < 1 or > 4 || clients.Any(x => x == Guid.Empty) || clients.Distinct().Count() != clients.Length || !clients.Contains(player)) return false;
            _requiredCues.Clear(); _readyCues.Clear(); foreach (var client in clients) _requiredCues.Add(client);
            EncounterId = Guid.NewGuid(); PlayerId = player; SpotId = spot; Zone = zone; SourceFingerprint = sourceFingerprint;
            _quotaConsumed = false; Reason = string.Empty; SetPhase(AEDV02CabinetPhase.CuePending, now); return true;
        }
        public bool AcknowledgeCue(Guid encounter, Guid client, bool audioAvailable, bool visualPresented, double now)
        {
            if (encounter != EncounterId || Phase != AEDV02CabinetPhase.CuePending || !_requiredCues.Contains(client) ||
                !AEDV02PilotPolicy.Finite(now) || now < _phaseBegan) return false;
            if (!audioAvailable || !visualPresented) { Abort("HOLD_CUE_UNAVAILABLE", now); return false; }
            _readyCues.Add(client);
            if (_readyCues.SetEquals(_requiredCues)) SetPhase(AEDV02CabinetPhase.WarningLead, now);
            return true;
        }
        public void Tick(double now, bool revalidated, bool arrived, AEDV02CabinetInspection inspection)
        {
            if (!AEDV02PilotPolicy.Finite(now) || now < _phaseBegan) { Abort("ABORT_TIME_INVALID", _phaseBegan); return; }
            if (Phase == AEDV02CabinetPhase.Backoff || Phase == AEDV02CabinetPhase.Recovery)
            { if (Elapsed(now) >= (Phase == AEDV02CabinetPhase.Recovery ? AEDV02PilotPolicy.AmbushRecoverySeconds : AEDV02PilotPolicy.AmbushBackoffSeconds)) SetPhase(AEDV02CabinetPhase.Idle, now); return; }
            if (!IsActive) return;
            if (!revalidated) { Abort("ABORT_CABINET_REVALIDATION", now); return; }
            switch (Phase)
            {
                case AEDV02CabinetPhase.CuePending:
                    if (Elapsed(now) > AEDV02PilotPolicy.EvidenceMaxAgeSeconds) Abort("HOLD_CUE_TIMEOUT", now);
                    break;
                case AEDV02CabinetPhase.WarningLead:
                    if (Elapsed(now) >= AEDV02PilotPolicy.AmbushWarningSeconds)
                    {
                        if (!_quotaConsumed) { _players.Add(PlayerId); _zoneCounts[Zone] = 1; _quotaConsumed = true; }
                        _lastSpecial = now; LastActivationAt = now; _cooldownUntil = now + AEDV02PilotPolicy.AmbushCooldownSeconds;
                        SetPhase(AEDV02CabinetPhase.JumpIn, now);
                    }
                    break;
                case AEDV02CabinetPhase.JumpIn:
                    if (Elapsed(now) >= 3) SetPhase(AEDV02CabinetPhase.Approach, now);
                    break;
                case AEDV02CabinetPhase.Approach:
                    if (arrived) SetPhase(AEDV02CabinetPhase.Inspect, now);
                    else if (Elapsed(now) >= AEDV02PilotPolicy.AmbushApproachSeconds) Abort("ABORT_APPROACH_TIMEOUT", now);
                    break;
                case AEDV02CabinetPhase.Inspect:
                    if (inspection == AEDV02CabinetInspection.Pulled) SetPhase(AEDV02CabinetPhase.Recovery, now);
                    else if (inspection != AEDV02CabinetInspection.Pending) Abort("ABORT_INSPECTION_" + inspection.ToString().ToUpperInvariant(), now);
                    break;
            }
        }
        public void Abort(string reason, double now)
        {
            if (!IsActive) return;
            Reason = reason; _cooldownUntil = Math.Max(_cooldownUntil, now + AEDV02PilotPolicy.AmbushBackoffSeconds);
            SetPhase(AEDV02CabinetPhase.Backoff, now);
        }
        private void SetPhase(AEDV02CabinetPhase phase, double now) { Phase = phase; _phaseBegan = now; }
    }
}
