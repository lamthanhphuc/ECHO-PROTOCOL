using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public readonly struct ActivityRoomOccupancy
    {
        public ActivityRoomOccupancy(
            ActivityRoomKey room,
            int activePlayerCount,
            int hiddenPlayerCount)
        {
            Room = room;
            ActivePlayerCount = Math.Max(0, activePlayerCount);
            HiddenPlayerCount = Math.Min(
                ActivePlayerCount,
                Math.Max(0, hiddenPlayerCount));
        }

        public ActivityRoomKey Room { get; }
        public int ActivePlayerCount { get; }
        public int HiddenPlayerCount { get; }
    }

    public readonly struct StrategicNoisePulse
    {
        public StrategicNoisePulse(
            string eventId,
            ActivityRoomKey room,
            float strength01,
            bool isObjectiveActivity)
        {
            EventId = eventId ?? string.Empty;
            Room = room;
            Strength01 = (float)Math.Max(0d, Math.Min(1d, strength01));
            IsObjectiveActivity = isObjectiveActivity;
        }

        public string EventId { get; }
        public ActivityRoomKey Room { get; }
        public float Strength01 { get; }
        public bool IsObjectiveActivity { get; }
        public bool IsValid => !string.IsNullOrEmpty(EventId) && Room.IsValid;
    }

    public sealed class StalkerStrategicWorldFrame
    {
        private readonly ActivityRoomOccupancy[] _occupancy;
        private readonly StrategicNoisePulse[] _noisePulses;

        public StalkerStrategicWorldFrame(
            double sampleTimeSeconds,
            bool hasOccupancySample,
            IReadOnlyList<ActivityRoomOccupancy> occupancy,
            IReadOnlyList<StrategicNoisePulse> noisePulses,
            bool hasLegalLastKnownRoom,
            ActivityRoomKey legalLastKnownRoom)
        {
            SampleTimeSeconds = sampleTimeSeconds;
            HasOccupancySample = hasOccupancySample;
            _occupancy = Copy(occupancy);
            _noisePulses = Copy(noisePulses);
            HasLegalLastKnownRoom = hasLegalLastKnownRoom && legalLastKnownRoom.IsValid;
            LegalLastKnownRoom = HasLegalLastKnownRoom
                ? legalLastKnownRoom
                : ActivityRoomKey.Invalid;
        }

        public double SampleTimeSeconds { get; }
        public bool HasOccupancySample { get; }
        public IReadOnlyList<ActivityRoomOccupancy> Occupancy => _occupancy;
        public IReadOnlyList<StrategicNoisePulse> NoisePulses => _noisePulses;
        public bool HasLegalLastKnownRoom { get; }
        public ActivityRoomKey LegalLastKnownRoom { get; }

        private static ActivityRoomOccupancy[] Copy(IReadOnlyList<ActivityRoomOccupancy> source)
        {
            var result = new ActivityRoomOccupancy[source?.Count ?? 0];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = source[i];
            }

            return result;
        }

        private static StrategicNoisePulse[] Copy(IReadOnlyList<StrategicNoisePulse> source)
        {
            var result = new StrategicNoisePulse[source?.Count ?? 0];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = source[i];
            }

            return result;
        }
    }
}
