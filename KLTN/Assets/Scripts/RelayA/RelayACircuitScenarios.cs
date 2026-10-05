using System;

namespace EchoProtocol.RelayA
{
    public static class RelayACircuitScenarios
    {
        public static RelayACircuitScenario[] CreateDefaults()
        {
            return new[]
            {
                CreateBoard("A1 / Split Feed", false, false),
                Mirror(CreateBoard("A1 / Return Feed", false, true), "A1 / Return Feed"),
                CreateBoard("A2 / Triple Feed", true, false),
                Mirror(CreateBoard("A2 / Return Feed", true, true), "A2 / Return Feed")
            };
        }

        private static RelayACircuitScenario CreateBoard(string name, bool harder, bool upperTerminal)
        {
            const int width = 6;
            const int height = 5;
            var cells = new RelayACircuitCell[width * height];
            var solved = new int[cells.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = new RelayACircuitCell(RelayACircuitTile.Empty);

            void Put(int x, int y, RelayACircuitTile type, int rotation, bool locked = false, string label = "")
            {
                int index = y * width + x;
                cells[index] = new RelayACircuitCell(type, rotation, locked, label);
                solved[index] = rotation;
            }

            // Two winding supply trees share an interior bypass, not a direct row feed.
            Put(0, 2, RelayACircuitTile.Source, 1);
            Put(1, 2, RelayACircuitTile.Junction, 3);
            Put(1, 1, upperTerminal ? RelayACircuitTile.Junction : RelayACircuitTile.Corner, 1, harder);
            Put(2, 1, RelayACircuitTile.Corner, 3);
            Put(2, 0, RelayACircuitTile.Corner, 1);
            Put(3, 0, RelayACircuitTile.Straight, 1);
            Put(4, 0, RelayACircuitTile.Corner, 2);
            Put(4, 1, RelayACircuitTile.Junction, 1);
            Put(5, 1, RelayACircuitTile.Target, 3, true, "COOLING");

            Put(1, 3, RelayACircuitTile.Corner, 0);
            Put(2, 3, RelayACircuitTile.Corner, 2);
            Put(2, 4, RelayACircuitTile.Junction, upperTerminal ? 1 : 0, harder);
            Put(1, 4, RelayACircuitTile.Straight, 1);
            Put(3, 4, RelayACircuitTile.Straight, 1);
            Put(4, 4, harder ? RelayACircuitTile.Junction : RelayACircuitTile.Corner, harder ? 0 : 3);
            Put(4, 3, RelayACircuitTile.Junction, 1, harder);
            Put(5, 3, RelayACircuitTile.Target, 3, true, "SECURITY");
            if (harder) Put(5, 4, RelayACircuitTile.Target, 3, true, "AIR FILTER");
            else Put(5, 4, RelayACircuitTile.Corner, 2);
            Put(0, upperTerminal ? 0 : 4, RelayACircuitTile.Target, 1, true, "LIFE SUPPORT");
            if (upperTerminal) Put(1, 0, RelayACircuitTile.Corner, 2);
            else
            {
                Put(0, 0, RelayACircuitTile.Corner, 1);
                Put(1, 0, RelayACircuitTile.Corner, 2);
            }

            Put(4, 2, RelayACircuitTile.Corner, 3);
            Put(3, 2, RelayACircuitTile.Corner, 2);
            Put(3, 1, RelayACircuitTile.Corner, 3);
            Put(0, 3, RelayACircuitTile.Corner, 1);
            Put(0, 1, RelayACircuitTile.Fault, 1);
            Put(2, 2, RelayACircuitTile.Fault, 2);
            Put(3, 3, RelayACircuitTile.Fault, 0);
            Put(5, 2, RelayACircuitTile.Fault, 3);

            int failedCell = 3;
            int[] initial = (int[])solved.Clone();
            int[] fault = (int[])solved.Clone();
            fault[2 * width + 4] = 2;
            fault[2 * width + 3] = 0;
            fault[width + 3] = 1;
            fault[width + 4] = 2;
            for (int i = 0; i < cells.Length; i++)
                if (!cells[i].Locked && cells[i].Type != RelayACircuitTile.Cross)
                    initial[i] = (solved[i] + (cells[i].Type == RelayACircuitTile.Straight
                        ? (i % 2 == 0 ? 1 : 3) : 1 + (i * 7 + i / width) % 3)) & 3;
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new RelayACircuitCell(cells[i].Type, initial[i], cells[i].Locked, cells[i].Label);
            return new RelayACircuitScenario(name, width, height, cells, failedCell, solved, fault);
        }

        private static RelayACircuitScenario Mirror(RelayACircuitScenario original, string name)
        {
            int width = original.Width;
            int count = original.Count;
            var cells = new RelayACircuitCell[count];
            var initial = new int[count];
            var fault = new int[count];
            for (int i = 0; i < count; i++)
            {
                int x = i % width;
                int y = i / width;
                int target = y * width + (width - 1 - x);
                RelayACircuitCell source = original.Cells[i];
                int rotation = MirrorRotation(source.Type, source.Rotation);
                cells[target] = new RelayACircuitCell(source.Type, rotation, source.Locked, source.Label);
                initial[target] = MirrorRotation(source.Type, original.InitialSolution[i]);
                fault[target] = MirrorRotation(source.Type, original.FaultSolution[i]);
            }
            int failed = original.FailedCell / width * width + width - 1 - original.FailedCell % width;
            return new RelayACircuitScenario(name, width, original.Height, cells, failed, initial, fault);
        }

        private static int MirrorRotation(RelayACircuitTile type, int rotation)
        {
            int mask = RelayACircuitBoard.Connections(type, rotation);
            int mirrored = (mask & 5) | ((mask & 2) << 2) | ((mask & 8) >> 2);
            for (int i = 0; i < 4; i++)
                if (RelayACircuitBoard.Connections(type, i) == mirrored) return i;
            return rotation;
        }
    }
}
