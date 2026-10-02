using System;
using System.Collections.Generic;

namespace EchoProtocol.Networking
{
    public enum Zone3ConvoyRoutePoint
    {
        Point00,
        Point01,
        Point02,
        Point03,
        Point04,
        Point05,
        Point06,
        Point07,
        Point08,
        Point09,
        Point10,
        Final
    }

    public static class Zone3ConvoyRouteGraph
    {
        public const string RouteRootName = "Zone3ConvoyRoute";

        private static readonly Zone3ConvoyRoutePoint[] Point00Next = { Zone3ConvoyRoutePoint.Point01 };
        private static readonly Zone3ConvoyRoutePoint[] Point01Next = { Zone3ConvoyRoutePoint.Point02, Zone3ConvoyRoutePoint.Point03 };
        private static readonly Zone3ConvoyRoutePoint[] Point02Next = { Zone3ConvoyRoutePoint.Point04, Zone3ConvoyRoutePoint.Point09 };
        private static readonly Zone3ConvoyRoutePoint[] Point03Next = { Zone3ConvoyRoutePoint.Point04, Zone3ConvoyRoutePoint.Point05 };
        private static readonly Zone3ConvoyRoutePoint[] Point04Next = { Zone3ConvoyRoutePoint.Point02, Zone3ConvoyRoutePoint.Point03, Zone3ConvoyRoutePoint.Point08, Zone3ConvoyRoutePoint.Point06 };
        private static readonly Zone3ConvoyRoutePoint[] Point05Next = { Zone3ConvoyRoutePoint.Point06 };
        private static readonly Zone3ConvoyRoutePoint[] Point06Next = { Zone3ConvoyRoutePoint.Point07, Zone3ConvoyRoutePoint.Point04 };
        private static readonly Zone3ConvoyRoutePoint[] Point07Next = { Zone3ConvoyRoutePoint.Point08, Zone3ConvoyRoutePoint.Final };
        private static readonly Zone3ConvoyRoutePoint[] Point08Next = { Zone3ConvoyRoutePoint.Point09, Zone3ConvoyRoutePoint.Point04, Zone3ConvoyRoutePoint.Point10 };
        private static readonly Zone3ConvoyRoutePoint[] Point09Next = { Zone3ConvoyRoutePoint.Point02, Zone3ConvoyRoutePoint.Point08, Zone3ConvoyRoutePoint.Point10 };
        private static readonly Zone3ConvoyRoutePoint[] Point10Next = { Zone3ConvoyRoutePoint.Point08, Zone3ConvoyRoutePoint.Point09 };
        private static readonly Zone3ConvoyRoutePoint[] EmptyNext = Array.Empty<Zone3ConvoyRoutePoint>();

        public static readonly Zone3ConvoyRoutePoint[] OrderedPoints =
        {
            Zone3ConvoyRoutePoint.Point00,
            Zone3ConvoyRoutePoint.Point01,
            Zone3ConvoyRoutePoint.Point02,
            Zone3ConvoyRoutePoint.Point03,
            Zone3ConvoyRoutePoint.Point04,
            Zone3ConvoyRoutePoint.Point05,
            Zone3ConvoyRoutePoint.Point06,
            Zone3ConvoyRoutePoint.Point07,
            Zone3ConvoyRoutePoint.Point08,
            Zone3ConvoyRoutePoint.Point09,
            Zone3ConvoyRoutePoint.Point10,
            Zone3ConvoyRoutePoint.Final
        };

        public static string GetSceneObjectName(Zone3ConvoyRoutePoint point) => point switch
        {
            Zone3ConvoyRoutePoint.Point00 => "Z3_Route_00_Frigate_Start",
            Zone3ConvoyRoutePoint.Point01 => "Z3_Route_01_Room_Exit",
            Zone3ConvoyRoutePoint.Point02 => "Z3_Route_02_First_Corridor",
            Zone3ConvoyRoutePoint.Point03 => "Z3_Route_03_Checkpoint_A",
            Zone3ConvoyRoutePoint.Point04 => "Z3_Route_04_Junction_A",
            Zone3ConvoyRoutePoint.Point05 => "Z3_Route_05_Route_A_Mid",
            Zone3ConvoyRoutePoint.Point06 => "Z3_Route_06_Fuel_Stop",
            Zone3ConvoyRoutePoint.Point07 => "Z3_Route_07_Post_Fuel_Restart",
            Zone3ConvoyRoutePoint.Point08 => "Z3_Route_08_Junction_B",
            Zone3ConvoyRoutePoint.Point09 => "Z3_Route_09_Dock_Approach",
            Zone3ConvoyRoutePoint.Point10 => "Z3_Route_10_Power_Dock_Final",
            Zone3ConvoyRoutePoint.Final => "Z3_Route_Final",
            _ => throw new ArgumentOutOfRangeException(nameof(point), point, null)
        };

        public static IReadOnlyList<Zone3ConvoyRoutePoint> GetNextPoints(Zone3ConvoyRoutePoint point) => point switch
        {
            Zone3ConvoyRoutePoint.Point00 => Point00Next,
            Zone3ConvoyRoutePoint.Point01 => Point01Next,
            Zone3ConvoyRoutePoint.Point02 => Point02Next,
            Zone3ConvoyRoutePoint.Point03 => Point03Next,
            Zone3ConvoyRoutePoint.Point04 => Point04Next,
            Zone3ConvoyRoutePoint.Point05 => Point05Next,
            Zone3ConvoyRoutePoint.Point06 => Point06Next,
            Zone3ConvoyRoutePoint.Point07 => Point07Next,
            Zone3ConvoyRoutePoint.Point08 => Point08Next,
            Zone3ConvoyRoutePoint.Point09 => Point09Next,
            Zone3ConvoyRoutePoint.Point10 => Point10Next,
            Zone3ConvoyRoutePoint.Final => EmptyNext,
            _ => EmptyNext
        };

        public static bool CanTravel(
            Zone3ConvoyRoutePoint from,
            Zone3ConvoyRoutePoint to,
            bool allowReverse = false)
        {
            return HasDirectPath(from, to)
                || (allowReverse && HasDirectPath(to, from));
        }

        private static bool HasDirectPath(Zone3ConvoyRoutePoint from, Zone3ConvoyRoutePoint to)
        {
            var nextPoints = GetNextPoints(from);
            for (int i = 0; i < nextPoints.Count; i++)
            {
                if (nextPoints[i] == to) return true;
            }

            return false;
        }
    }
}
