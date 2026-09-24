using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public sealed class RoomHeatSystem
    {
        private const int ProcessedNoiseCapacity = 256;

        private readonly StalkerSmartPatrolSettings _settings;
        private readonly Dictionary<ActivityRoomKey, RoomState> _states =
            new Dictionary<ActivityRoomKey, RoomState>();
        private readonly HashSet<string> _processedNoiseEventIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _processedNoiseEventOrder =
            new Queue<string>();

        public RoomHeatSystem(StalkerSmartPatrolSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public int TotalActivePlayers { get; private set; }
        public float EffectivePlayerPresence { get; private set; }

        public int GetVisiblePlayerCount(ActivityRoomKey room) =>
            _states.TryGetValue(room, out var state)
                ? Math.Max(0, state.ActivePlayerCount - state.HiddenPlayerCount)
                : 0;

        public void Reset()
        {
            _states.Clear();
            _processedNoiseEventIds.Clear();
            _processedNoiseEventOrder.Clear();
            TotalActivePlayers = 0;
            EffectivePlayerPresence = 0f;
        }

        public void ApplyOccupancySnapshot(
            IReadOnlyList<ActivityRoomOccupancy> occupancy,
            double nowSeconds)
        {
            var updatedRooms = new HashSet<ActivityRoomKey>();
            TotalActivePlayers = 0;
            EffectivePlayerPresence = 0f;
            for (var i = 0; i < (occupancy?.Count ?? 0); i++)
            {
                var sample = occupancy[i];
                if (!sample.Room.IsValid)
                {
                    continue;
                }

                var state = GetOrCreate(sample.Room, nowSeconds);
                ApplyOccupancy(
                    state,
                    sample.ActivePlayerCount,
                    sample.HiddenPlayerCount,
                    nowSeconds);
                updatedRooms.Add(sample.Room);
                TotalActivePlayers += sample.ActivePlayerCount;

                var visiblePlayers =
                    Math.Max(
                        0,
                        sample.ActivePlayerCount - sample.HiddenPlayerCount);

                EffectivePlayerPresence +=
                    visiblePlayers
                    + (sample.HiddenPlayerCount
                        * _settings.HiddenPlayerInfluenceMultiplier);
            }

            foreach (var pair in _states)
            {
                if (!updatedRooms.Contains(pair.Key))
                {
                    ApplyOccupancy(pair.Value, 0, 0, nowSeconds);
                }
            }
        }

        public void ApplyNoisePulse(StrategicNoisePulse pulse, double nowSeconds)
        {
            if (!pulse.IsValid || !_processedNoiseEventIds.Add(pulse.EventId))
            {
                return;
            }

            _processedNoiseEventOrder.Enqueue(pulse.EventId);
            while (_processedNoiseEventOrder.Count > ProcessedNoiseCapacity)
            {
                _processedNoiseEventIds.Remove(_processedNoiseEventOrder.Dequeue());
            }

            var state = GetOrCreate(pulse.Room, nowSeconds);
            Decay(state, nowSeconds);
            if (pulse.IsObjectiveActivity)
            {
                state.ObjectiveHeat = Mathf.Clamp01(state.ObjectiveHeat + pulse.Strength01);
            }
            else
            {
                state.NoiseHeat = Mathf.Clamp01(state.NoiseHeat + pulse.Strength01);
            }
        }

        public void ApplyLegalLastKnownRoom(ActivityRoomKey room, double nowSeconds)
        {
            if (!room.IsValid)
            {
                return;
            }

            var state = GetOrCreate(room, nowSeconds);
            Decay(state, nowSeconds);
            state.LastKnownHeat = Mathf.Max(state.LastKnownHeat, 1f);
        }

        public float GetLocalInterest(ActivityRoomKey room, double nowSeconds)
        {
            if (!_states.TryGetValue(room, out var state))
            {
                return 0f;
            }

            Decay(state, nowSeconds);
            var visible = Math.Max(0, state.ActivePlayerCount - state.HiddenPlayerCount);
            var effectiveCount = visible
                + (state.HiddenPlayerCount * _settings.HiddenPlayerInfluenceMultiplier);
            var density = 1f - Mathf.Exp(-effectiveCount / _settings.DensitySaturation);
            var linger = Mathf.Clamp01(state.LingerSeconds / _settings.LingerFullSeconds);

            return Mathf.Clamp01(
                (_settings.PlayerDensityWeight * density)
                + (_settings.LingerWeight * linger)
                + (_settings.NoiseWeight * state.NoiseHeat)
                + (_settings.ObjectiveWeight * state.ObjectiveHeat)
                + (_settings.LastKnownWeight * state.LastKnownHeat));
        }

        public float GetStrategicHeat(
            ActivityRoomKey room,
            ActivityRoomIndex index,
            double nowSeconds)
        {
            if (!room.IsValid || index == null)
            {
                return 0f;
            }

            var local = GetLocalInterest(room, nowSeconds);
            var ring1Strongest = 0f;
            var ring2Strongest = 0f;
            for (var i = 0; i < index.AllAreas.Count; i++)
            {
                var other = index.AllAreas[i];
                if (other == room
                    || !index.TryGetMinimumHopDistance(room, other, out var distance))
                {
                    continue;
                }

                var interest = GetLocalInterest(other, nowSeconds);
                if (distance == 1)
                {
                    ring1Strongest = Mathf.Max(ring1Strongest, interest);
                }
                else if (distance == 2)
                {
                    ring2Strongest = Mathf.Max(ring2Strongest, interest);
                }
            }

            return Mathf.Clamp01(
                local
                + (_settings.Ring1Propagation * ring1Strongest)
                + (_settings.Ring2Propagation * ring2Strongest));
        }

        public bool TryGetHotspot(
            ActivityRoomIndex index,
            double nowSeconds,
            out ActivityRoomKey hotspot,
            out float heat)
        {
            hotspot = ActivityRoomKey.Invalid;
            heat = 0f;
            if (index == null)
            {
                return false;
            }

            for (var i = 0; i < index.AllAreas.Count; i++)
            {
                var area = index.AllAreas[i];
                var candidateHeat = GetLocalInterest(area, nowSeconds);
                if (candidateHeat > heat
                    || (Mathf.Approximately(candidateHeat, heat)
                        && area.CompareTo(hotspot) < 0))
                {
                    hotspot = area;
                    heat = candidateHeat;
                }
            }

            return hotspot.IsValid && heat > 0.0001f;
        }

        private RoomState GetOrCreate(ActivityRoomKey room, double nowSeconds)
        {
            if (_states.TryGetValue(room, out var state))
            {
                return state;
            }

            state = new RoomState
            {
                LastDecayTimeSeconds = nowSeconds,
                LastOccupancyUpdateSeconds = nowSeconds
            };
            _states.Add(room, state);
            return state;
        }

        private void ApplyOccupancy(
            RoomState state,
            int activePlayerCount,
            int hiddenPlayerCount,
            double nowSeconds)
        {
            var elapsed =
                Math.Max(
                    0d,
                    nowSeconds - state.LastOccupancyUpdateSeconds);

            var previousVisible =
                Math.Max(
                    0,
                    state.ActivePlayerCount - state.HiddenPlayerCount);

            var elapsedSeconds =
                (float)elapsed;

            if (previousVisible > 0)
            {
                state.LingerSeconds =
                    Mathf.Min(
                        _settings.LingerFullSeconds,
                        state.LingerSeconds + elapsedSeconds);
            }
            else if (state.HiddenPlayerCount > 0)
            {
                var hiddenLingerCap =
                    _settings.LingerFullSeconds
                    * _settings.HiddenPlayerInfluenceMultiplier;

                if (state.LingerSeconds > hiddenLingerCap)
                {
                    state.LingerSeconds =
                        Mathf.Max(
                            hiddenLingerCap,
                            state.LingerSeconds
                            - (elapsedSeconds * 2f));
                }
                else
                {
                    state.LingerSeconds =
                        Mathf.Min(
                            hiddenLingerCap,
                            state.LingerSeconds
                            + (elapsedSeconds
                                * _settings.HiddenPlayerInfluenceMultiplier));
                }
            }
            else
            {
                state.LingerSeconds =
                    Mathf.Max(
                        0f,
                        state.LingerSeconds
                        - (elapsedSeconds * 2f));
            }

            var clampedActive =
                Math.Max(0, activePlayerCount);

            var clampedHidden =
                Math.Min(
                    clampedActive,
                    Math.Max(0, hiddenPlayerCount));

            state.ActivePlayerCount = clampedActive;
            state.HiddenPlayerCount = clampedHidden;
            state.LastOccupancyUpdateSeconds = nowSeconds;
        }

        private void Decay(RoomState state, double nowSeconds)
        {
            var elapsed = Math.Max(0d, nowSeconds - state.LastDecayTimeSeconds);
            if (elapsed <= 0d)
            {
                return;
            }

            state.NoiseHeat *= HalfLifeMultiplier(elapsed, _settings.NoiseHalfLifeSeconds);
            state.ObjectiveHeat *= HalfLifeMultiplier(elapsed, _settings.ObjectiveHalfLifeSeconds);
            state.LastKnownHeat *= HalfLifeMultiplier(elapsed, _settings.LastKnownHalfLifeSeconds);
            state.LastDecayTimeSeconds = nowSeconds;
        }

        private static float HalfLifeMultiplier(double elapsed, float halfLifeSeconds) =>
            (float)Math.Pow(0.5d, elapsed / Math.Max(0.01d, halfLifeSeconds));

        private sealed class RoomState
        {
            public int ActivePlayerCount;
            public int HiddenPlayerCount;
            public float LingerSeconds;
            public float NoiseHeat;
            public float ObjectiveHeat;
            public float LastKnownHeat;
            public double LastDecayTimeSeconds;
            public double LastOccupancyUpdateSeconds;
        }
    }
}
