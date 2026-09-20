using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public sealed class StalkerStrategicPatrolRuntime
    {
        private readonly StalkerSmartPatrolSettings _settings;
        private readonly RoomHeatSystem _heatSystem;
        private readonly StalkerPatrolDirector _director;
        private readonly SmartRoomSweepTargetStrategy _targetStrategy;
        private readonly Queue<PendingOccupancy> _pendingOccupancy =
            new Queue<PendingOccupancy>();
        private bool _hadLegalLastKnownRoom;
        private ActivityRoomKey _lastLegalLastKnownRoom;
        private Guid _boundMatchId;

        public StalkerStrategicPatrolRuntime(
            RegionGraph regionGraph,
            CoverageMemory coverageMemory,
            RoomSweepCoverageMemory roomSweepCoverageMemory,
            StalkerSmartPatrolSettings settings,
            int variationSeed)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            RoomIndex = new ActivityRoomIndex(
                regionGraph ?? throw new ArgumentNullException(nameof(regionGraph)));
            _heatSystem = new RoomHeatSystem(settings);
            _director = new StalkerPatrolDirector(settings);
            _targetStrategy = new SmartRoomSweepTargetStrategy(
                regionGraph,
                roomSweepCoverageMemory,
                coverageMemory,
                RoomIndex,
                _heatSystem,
                _director,
                settings,
                variationSeed);
        }

        public ActivityRoomIndex RoomIndex { get; }
        public IRoomSweepTargetStrategy TargetStrategy => _targetStrategy;
        public StalkerPatrolPacingMode Mode => _director.Mode;
        public float Pressure01 => _director.Pressure01;
        public bool HasHotspot => _director.HasHotspot;
        public ActivityRoomKey Hotspot => _director.Hotspot;

        public void BeginMatch(Guid matchId)
        {
            if (matchId == Guid.Empty || _boundMatchId == matchId)
            {
                return;
            }

            ResetForMatch();
            _boundMatchId = matchId;
        }

        public void ResetForMatch()
        {
            _pendingOccupancy.Clear();
            _heatSystem.Reset();
            _director.Reset();
            _targetStrategy.ResetForMatch();
            RoomIndex.InvalidateDistanceCache();
            _hadLegalLastKnownRoom = false;
            _lastLegalLastKnownRoom = ActivityRoomKey.Invalid;
            _boundMatchId = Guid.Empty;
        }

        public void ConfigureVariationSeed(int variationSeed) =>
            _targetStrategy.ConfigureVariationSeed(variationSeed);

        public void InvalidateTopology() => RoomIndex.InvalidateDistanceCache();

        public void RecordCompletedRoomSweep(
            RegionId completedRegionId)
        {
            if (!completedRegionId.IsValid
                || !_director.HasHotspot
                || !RoomIndex.TryGetAreaForRegion(
                    completedRegionId,
                    out var completedRoom)
                || !completedRoom.IsRoom
                || !RoomIndex.TryGetMinimumHopDistance(
                    completedRoom,
                    _director.Hotspot,
                    out var hotspotRing))
            {
                return;
            }

            _director.RecordCompletedPeripheralSweep(
                completedRoom,
                hotspotRing);
        }

        public void ApplyFrame(
            StalkerStrategicWorldFrame frame,
            StalkerState currentState)
        {
            if (frame == null)
            {
                return;
            }

            for (var i = 0; i < frame.NoisePulses.Count; i++)
            {
                _heatSystem.ApplyNoisePulse(frame.NoisePulses[i], frame.SampleTimeSeconds);
            }

            if (frame.HasLegalLastKnownRoom)
            {
                if (!_hadLegalLastKnownRoom
                    || frame.LegalLastKnownRoom != _lastLegalLastKnownRoom)
                {
                    _heatSystem.ApplyLegalLastKnownRoom(
                        frame.LegalLastKnownRoom,
                        frame.SampleTimeSeconds);
                }

                _hadLegalLastKnownRoom = true;
                _lastLegalLastKnownRoom = frame.LegalLastKnownRoom;
            }
            else
            {
                _hadLegalLastKnownRoom = false;
                _lastLegalLastKnownRoom = ActivityRoomKey.Invalid;
            }

            if (frame.HasOccupancySample)
            {
                _pendingOccupancy.Enqueue(new PendingOccupancy(
                    frame.SampleTimeSeconds + _settings.OccupancyInformationDelaySeconds,
                    frame.Occupancy));
            }

            while (_pendingOccupancy.Count > 0
                && _pendingOccupancy.Peek().DueTimeSeconds <= frame.SampleTimeSeconds)
            {
                var pending = _pendingOccupancy.Dequeue();
                _heatSystem.ApplyOccupancySnapshot(
                    pending.Occupancy,
                    pending.DueTimeSeconds);
            }

            _director.Update(currentState, _heatSystem, RoomIndex, frame.SampleTimeSeconds);
        }

        private readonly struct PendingOccupancy
        {
            public PendingOccupancy(
                double dueTimeSeconds,
                IReadOnlyList<ActivityRoomOccupancy> occupancy)
            {
                DueTimeSeconds = dueTimeSeconds;
                Occupancy = new ActivityRoomOccupancy[occupancy?.Count ?? 0];
                for (var i = 0; i < Occupancy.Length; i++)
                {
                    Occupancy[i] = occupancy[i];
                }
            }

            public double DueTimeSeconds { get; }
            public ActivityRoomOccupancy[] Occupancy { get; }
        }
    }
}
