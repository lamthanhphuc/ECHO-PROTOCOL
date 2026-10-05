using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    public enum RelayACircuitTile : byte
    {
        Empty, Straight, Corner, Junction, Cross, Source, Target, Fault
    }

    [Serializable]
    public sealed class RelayACircuitCell
    {
        [SerializeField] private RelayACircuitTile type;
        [SerializeField, Range(0, 3)] private int rotation;
        [SerializeField] private bool locked;
        [SerializeField] private string label;

        public RelayACircuitCell(RelayACircuitTile type, int rotation = 0, bool locked = false, string label = "")
        {
            this.type = type;
            this.rotation = rotation & 3;
            this.locked = locked;
            this.label = label;
        }

        public RelayACircuitTile Type => type;
        public int Rotation => rotation;
        public bool Locked => locked || type == RelayACircuitTile.Source || type == RelayACircuitTile.Target
            || type == RelayACircuitTile.Fault || type == RelayACircuitTile.Empty;
        public string Label => label;
    }

    [Serializable]
    public sealed class RelayACircuitScenario
    {
        [SerializeField] private string name;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private RelayACircuitCell[] cells;
        [SerializeField] private int failedCell;
        [SerializeField] private int[] initialSolution;
        [SerializeField] private int[] faultSolution;

        public RelayACircuitScenario(string name, int width, int height, RelayACircuitCell[] cells,
            int failedCell, int[] initialSolution, int[] faultSolution)
        {
            this.name = name;
            this.width = width;
            this.height = height;
            this.cells = cells;
            this.failedCell = failedCell;
            this.initialSolution = initialSolution;
            this.faultSolution = faultSolution;
        }

        public string Name => name;
        public int Width => width;
        public int Height => height;
        public int Count => cells != null ? cells.Length : 0;
        public int FailedCell => failedCell;
        public RelayACircuitCell[] Cells => cells;
        public int[] InitialSolution => initialSolution;
        public int[] FaultSolution => faultSolution;
    }

    public readonly struct RelayACircuitResult
    {
        public RelayACircuitResult(ulong powered, int[] depth, int missingTargets, bool faultPowered)
        {
            Powered = powered;
            Depth = depth;
            MissingTargets = missingTargets;
            FaultPowered = faultPowered;
        }

        public ulong Powered { get; }
        public int[] Depth { get; }
        public int MissingTargets { get; }
        public bool FaultPowered { get; }
        public bool IsValid => MissingTargets == 0 && !FaultPowered;
    }

    public static class RelayACircuitBoard
    {
        private const int North = 1;
        private const int East = 2;
        private const int South = 4;
        private const int West = 8;

        public static int Connections(RelayACircuitTile type, int rotation)
        {
            int mask;
            switch (type)
            {
                case RelayACircuitTile.Straight: mask = North | South; break;
                case RelayACircuitTile.Corner: mask = North | East; break;
                case RelayACircuitTile.Junction: mask = North | East | West; break;
                case RelayACircuitTile.Cross: mask = 15; break;
                case RelayACircuitTile.Source:
                case RelayACircuitTile.Target:
                case RelayACircuitTile.Fault: mask = North; break;
                default: return 0;
            }
            for (int i = 0; i < (rotation & 3); i++) mask = ((mask << 1) & 15) | ((mask >> 3) & 1);
            return mask;
        }

        public static RelayACircuitResult Evaluate(RelayACircuitScenario scenario, int[] rotations, bool faultActive)
        {
            if (scenario == null || scenario.Width <= 0 || scenario.Height <= 0
                || scenario.Count == 0 || scenario.Count > 32 || scenario.Count != scenario.Width * scenario.Height
                || rotations == null || rotations.Length != scenario.Count)
                throw new ArgumentException("Invalid circuit dimensions or rotations.");
            int count = scenario.Count;
            var depth = new int[count];
            Array.Fill(depth, -1);
            int source = -1;
            for (int i = 0; i < count; i++)
                if (scenario.Cells[i].Type == RelayACircuitTile.Source) { source = i; break; }
            var queue = new Queue<int>();
            ulong powered = 0;
            if (source >= 0 && source < 64)
            {
                queue.Enqueue(source);
                depth[source] = 0;
                powered |= 1UL << source;
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % scenario.Width;
                int y = current / scenario.Width;
                int mask = Connections(scenario.Cells[current].Type, rotations[current]);
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + (direction == 1 ? 1 : direction == 3 ? -1 : 0);
                    int ny = y + (direction == 2 ? 1 : direction == 0 ? -1 : 0);
                    if (nx < 0 || nx >= scenario.Width || ny < 0 || ny >= scenario.Height) continue;
                    int next = ny * scenario.Width + nx;
                    if (next == scenario.FailedCell && faultActive || depth[next] >= 0) continue;
                    int outward = 1 << direction;
                    int inward = 1 << ((direction + 2) & 3);
                    if ((mask & outward) == 0 || (Connections(scenario.Cells[next].Type, rotations[next]) & inward) == 0) continue;
                    depth[next] = depth[current] + 1;
                    powered |= 1UL << next;
                    queue.Enqueue(next);
                }
            }

            int missing = 0;
            bool redPowered = false;
            for (int i = 0; i < count; i++)
            {
                if (scenario.Cells[i].Type == RelayACircuitTile.Target && depth[i] < 0) missing++;
                if (scenario.Cells[i].Type == RelayACircuitTile.Fault && depth[i] >= 0) redPowered = true;
            }
            return new RelayACircuitResult(powered, depth, missing, redPowered);
        }

        public static bool HasAuthoredSolutions(RelayACircuitScenario scenario)
        {
            if (scenario == null || scenario.Width <= 0 || scenario.Height <= 0 || scenario.Count == 0
                || scenario.Count != scenario.Width * scenario.Height || scenario.Count > 32
                || scenario.InitialSolution == null || scenario.InitialSolution.Length != scenario.Count
                || scenario.FaultSolution == null || scenario.FaultSolution.Length != scenario.Count
                || scenario.FailedCell < 0 || scenario.FailedCell >= scenario.Count) return false;
            int sources = 0, targets = 0;
            for (int i = 0; i < scenario.Count; i++)
            {
                var cell = scenario.Cells[i];
                if (cell == null) return false;
                if (cell.Type == RelayACircuitTile.Source) sources++;
                if (cell.Type == RelayACircuitTile.Target) targets++;
                if (scenario.InitialSolution[i] < 0 || scenario.InitialSolution[i] > 3
                    || scenario.FaultSolution[i] < 0 || scenario.FaultSolution[i] > 3) return false;
                if (cell.Locked && (scenario.InitialSolution[i] != cell.Rotation
                    || scenario.FaultSolution[i] != cell.Rotation)) return false;
            }
            if (sources != 1 || targets == 0 || scenario.Cells[scenario.FailedCell].Locked) return false;
            var initial = Evaluate(scenario, scenario.InitialSolution, false);
            return initial.IsValid && (initial.Powered & (1UL << scenario.FailedCell)) != 0
                && !Evaluate(scenario, scenario.InitialSolution, true).IsValid
                && Evaluate(scenario, scenario.FaultSolution, true).IsValid;
        }
    }
}
