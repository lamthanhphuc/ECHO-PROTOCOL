using System;
using System.Collections.Generic;
using EchoProtocol.AI.Stalker;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public enum StalkerPatrolPacingMode
    {
        Calm = 0,
        Build = 1,
        Pressure = 2,
        Cooldown = 3
    }

    public sealed class StalkerPatrolDirector
    {
        private readonly StalkerSmartPatrolSettings _settings;
        private readonly Dictionary<ActivityRoomKey, double> _lastPressureAtByRoom =
            new Dictionary<ActivityRoomKey, double>();
        private readonly HashSet<ActivityRoomKey> _peripheralRoomsForCurrentHotspot =
            new HashSet<ActivityRoomKey>();
        private StalkerState _previousState;
        private bool _hasPreviousState;
        private double _lastUpdateSeconds;
        private double _cooldownUntilSeconds;
        private double _hotspotApproachSeconds;
        private ActivityRoomKey _pendingStrategicSweepRoom;
        private ActivityRoomKey _pendingStrategicSweepHotspot;

        public StalkerPatrolDirector(StalkerSmartPatrolSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Reset();
        }

        public StalkerPatrolPacingMode Mode { get; private set; }
        public float Pressure01 { get; private set; }
        public ActivityRoomKey Hotspot { get; private set; }
        public bool HasHotspot { get; private set; }
        public double CurrentTimeSeconds { get; private set; }
        public int PeripheralSweepsForCurrentHotspot { get; private set; }

        public void Reset()
        {
            Mode = StalkerPatrolPacingMode.Calm;
            Pressure01 = 0f;
            Hotspot = ActivityRoomKey.Invalid;
            HasHotspot = false;
            CurrentTimeSeconds = 0d;
            PeripheralSweepsForCurrentHotspot = 0;
            _previousState = StalkerState.PATROL;
            _hasPreviousState = false;
            _lastUpdateSeconds = 0d;
            _cooldownUntilSeconds = 0d;
            _hotspotApproachSeconds = 0d;
            _pendingStrategicSweepRoom = ActivityRoomKey.Invalid;
            _pendingStrategicSweepHotspot = ActivityRoomKey.Invalid;
            _lastPressureAtByRoom.Clear();
            _peripheralRoomsForCurrentHotspot.Clear();
        }

        public void Update(
            StalkerState currentState,
            RoomHeatSystem heat,
            ActivityRoomIndex index,
            double nowSeconds)
        {
            CurrentTimeSeconds = nowSeconds;
            var delta = _hasPreviousState
                ? Math.Max(0d, nowSeconds - _lastUpdateSeconds)
                : 0d;

            if (_hasPreviousState
                && _previousState == StalkerState.CHASE
                && currentState != StalkerState.CHASE
                && currentState != StalkerState.ATTACK)
            {
                _cooldownUntilSeconds = Math.Max(
                    _cooldownUntilSeconds,
                    nowSeconds + _settings.PostChaseCooldownSeconds);
            }

            if (_hasPreviousState
                && _previousState == StalkerState.ATTACK
                && currentState != StalkerState.ATTACK)
            {
                _cooldownUntilSeconds = Math.Max(
                    _cooldownUntilSeconds,
                    nowSeconds + _settings.PostAttackCooldownSeconds);
            }

            var hotspot = ActivityRoomKey.Invalid;
            var hasHotspot = heat != null
                && heat.TryGetHotspot(index, nowSeconds, out hotspot, out _);
            var hotspotChanged =
                hasHotspot != HasHotspot
                || (hasHotspot && hotspot != Hotspot);

            if (hotspotChanged)
            {
                Hotspot = hasHotspot
                    ? hotspot
                    : ActivityRoomKey.Invalid;

                HasHotspot = hasHotspot;
                PeripheralSweepsForCurrentHotspot = 0;
                _hotspotApproachSeconds = 0d;
                _peripheralRoomsForCurrentHotspot.Clear();
                _pendingStrategicSweepRoom =
                    ActivityRoomKey.Invalid;

                _pendingStrategicSweepHotspot =
                    ActivityRoomKey.Invalid;
            }

            if (currentState == StalkerState.CHASE || currentState == StalkerState.ATTACK)
            {
                Pressure01 = Mathf.Max(Pressure01, _settings.PressureModeThreshold);
                Mode = StalkerPatrolPacingMode.Pressure;
            }
            else if (nowSeconds < _cooldownUntilSeconds)
            {
                Pressure01 = DecayPressure(Pressure01, delta);
                Mode = StalkerPatrolPacingMode.Cooldown;
            }
            else if (heat == null
                || heat.EffectivePlayerPresence <= 0f)
            {
                Pressure01 =
                    DecayPressure(
                        Pressure01,
                        delta);

                Mode =
                    StalkerPatrolPacingMode.Calm;
            }
            else
            {
                var pressureBuildScale =
                    Mathf.Clamp01(
                        heat.EffectivePlayerPresence);

                Pressure01 =
                    Mathf.Clamp01(
                        Pressure01
                        + (((float)delta
                            / _settings.PressureBuildSeconds)
                            * pressureBuildScale));

                Mode = Pressure01 < _settings.PressureBuildThreshold
                    ? StalkerPatrolPacingMode.Calm
                    : Pressure01 < _settings.PressureModeThreshold
                        ? StalkerPatrolPacingMode.Build
                        : StalkerPatrolPacingMode.Pressure;
            }

            if (!hotspotChanged
                && HasHotspot
                && heat != null
                && heat.EffectivePlayerPresence > 0f
                && currentState == StalkerState.PATROL
                && Mode != StalkerPatrolPacingMode.Cooldown)
            {
                _hotspotApproachSeconds += delta;
            }

            _previousState = currentState;
            _hasPreviousState = true;
            _lastUpdateSeconds = nowSeconds;
        }

        public float GetRingAffinity(int ring)
        {
            switch (Mode)
            {
                case StalkerPatrolPacingMode.Build:
                    return ring <= 0 ? 0.10f : ring == 1 ? 0.75f : ring == 2 ? 1f : 0.50f;
                case StalkerPatrolPacingMode.Pressure:
                    return ring <= 0
                        ? (IsDirectHotspotRoomEligible() ? 0.75f : 0.35f)
                        : ring == 1 ? 1f : ring == 2 ? 0.55f : 0.20f;
                case StalkerPatrolPacingMode.Cooldown:
                    return ring <= 0 ? 0f : ring == 1 ? 0.20f : ring == 2 ? 0.70f : 1f;
                default:
                    return ring <= 0 ? 0f : ring == 1 ? 0.30f : ring == 2 ? 0.80f : 1f;
            }
        }

        public bool IsDirectHotspotRoomEligible() =>
            Mode == StalkerPatrolPacingMode.Pressure
            && (PeripheralSweepsForCurrentHotspot
                    >= _settings.MinimumPeripheralSweepsBeforeHotRoom
                || _hotspotApproachSeconds
                    >= _settings.MaxPeripheralApproachSeconds);

        public float GetRecentRoomPressure01(ActivityRoomKey room)
        {
            if (!_lastPressureAtByRoom.TryGetValue(room, out var lastPressureAt))
            {
                return 0f;
            }

            var elapsed = Math.Max(0d, CurrentTimeSeconds - lastPressureAt);
            return elapsed >= _settings.SameRoomPressureCooldownSeconds
                ? 0f
                : 1f - ((float)elapsed / _settings.SameRoomPressureCooldownSeconds);
        }

        public void RecordStrategicSelection(
            ActivityRoomKey selectedRoom,
            int hotspotRing)
        {
            if (HasHotspot
                && selectedRoom.IsValid
                && hotspotRing > 0)
            {
                _pendingStrategicSweepRoom =
                    selectedRoom;

                _pendingStrategicSweepHotspot =
                    Hotspot;
            }
            else
            {
                CancelPendingStrategicSweep();
            }

            if (Mode == StalkerPatrolPacingMode.Build
                || Mode == StalkerPatrolPacingMode.Pressure)
            {
                _lastPressureAtByRoom[selectedRoom] =
                    CurrentTimeSeconds;
            }
        }

        public void CancelPendingStrategicSweep()
        {
            _pendingStrategicSweepRoom =
                ActivityRoomKey.Invalid;

            _pendingStrategicSweepHotspot =
                ActivityRoomKey.Invalid;
        }

        public void RecordCompletedPeripheralSweep(
            ActivityRoomKey completedRoom,
            int hotspotRing)
        {
            if (!completedRoom.IsValid
                || !_pendingStrategicSweepRoom.IsValid
                || completedRoom
                    != _pendingStrategicSweepRoom)
            {
                return;
            }

            var sameHotspot =
                HasHotspot
                && _pendingStrategicSweepHotspot.IsValid
                && _pendingStrategicSweepHotspot == Hotspot;

            _pendingStrategicSweepRoom =
                ActivityRoomKey.Invalid;

            _pendingStrategicSweepHotspot =
                ActivityRoomKey.Invalid;

            if (!sameHotspot
                || hotspotRing <= 0)
            {
                return;
            }

            if (_peripheralRoomsForCurrentHotspot.Add(
                    completedRoom))
            {
                PeripheralSweepsForCurrentHotspot++;
            }
        }

        private float DecayPressure(float pressure, double delta) =>
            Mathf.Max(0f, pressure - ((float)delta / _settings.PressureDecaySeconds));
    }
}
