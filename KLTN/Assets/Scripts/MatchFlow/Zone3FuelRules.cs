using System;
using System.Collections.Generic;
using EchoProtocol.Networking;

namespace EchoProtocol.MatchFlow
{
    public static class Zone3FuelRules
    {
        public const int Capacity = 2;
        public const float InsertDurationSeconds = 1.75f;

        public static int ConsumeArrival(int remaining) => Math.Max(0, remaining - 1);
        public static bool CanRefuel(int remaining, bool carryingCell, bool alive, bool atPort) =>
            remaining == 0 && carryingCell && alive && atPort;

        // Reverse edges exist: budget a complete route without revisiting a waypoint.
        public static int LongestSimpleRouteSegments() =>
            Longest(Zone3ConvoyRoutePoint.Point00, new HashSet<Zone3ConvoyRoutePoint>());

        private static int Longest(Zone3ConvoyRoutePoint point, HashSet<Zone3ConvoyRoutePoint> visited)
        {
            if (point == Zone3ConvoyRoutePoint.Final) return 0;
            if (!visited.Add(point)) return -1;
            int longest = -1;
            foreach (var next in Zone3ConvoyRouteGraph.GetNextPoints(point))
            {
                if (visited.Contains(next)) continue;
                int tail = Longest(next, visited);
                if (tail >= 0) longest = Math.Max(longest, tail + 1);
            }
            visited.Remove(point);
            return longest;
        }

        public static int RequiredCells(int segments) =>
            Math.Max(0, (segments - Capacity + Capacity - 1) / Capacity) + 1;
    }
}
