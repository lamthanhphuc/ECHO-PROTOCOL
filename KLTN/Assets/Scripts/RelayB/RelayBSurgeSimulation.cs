using System;
using System.Collections.Generic;

namespace EchoProtocol.RelayB
{
    public enum RelayBSurgePhase : byte { Ready, Running, Contained, CoreLost, SafetyLost, TimedOut }

    // Two words cover the largest 10 x 8 board without allocations per network snapshot.
    public struct RelayBSurgeMask
    {
        public ulong Low, High;
        public bool Has(int cell) => cell >= 0 && cell < 80 && (cell < 64
            ? (Low & (1UL << cell)) != 0 : (High & (1UL << (cell - 64))) != 0);
        public void Add(int cell) { if (cell < 64) Low |= 1UL << cell; else High |= 1UL << (cell - 64); }
        public void Remove(int cell) { if (cell < 64) Low &= ~(1UL << cell); else High &= ~(1UL << (cell - 64)); }
        public int Count { get { int n = 0; ulong bits = Low; while (bits != 0) { bits &= bits - 1; n++; }
            bits = High; while (bits != 0) { bits &= bits - 1; n++; } return n; } }
    }

    public sealed class RelayBSurgeBoard
    {
        public int Width, Height, Budget, Difficulty, Variant, Seed, MinimumSeals;
        public float PulseSeconds, TimeLimit, MinimumSafety = 0.38f;
        public RelayBSurgeMask Walls, Sources, Cores, ProtectedRegion;
        // A timed, verified witness is retained for validation, never sent to the UI.
        public int[] Witness;
        public float WitnessSeconds;
        public int Count => Width * Height;
        public IEnumerable<int> Neighbors(int cell)
        {
            int x = cell % Width, y = cell / Width;
            if (x > 0) yield return cell - 1;
            if (x + 1 < Width) yield return cell + 1;
            if (y > 0) yield return cell - Width;
            if (y + 1 < Height) yield return cell + Width;
        }
    }

    public sealed class RelayBSurgeSimulation
    {
        public const float PlacementCooldown = 1.2f;
        public RelayBSurgeBoard Board { get; private set; }
        public RelayBSurgePhase Phase { get; private set; }
        public RelayBSurgeMask Infected { get; private set; }
        public RelayBSurgeMask Insulated { get; private set; }
        public float Elapsed { get; private set; }
        public float PulseRemaining { get; private set; }
        public float Cooldown { get; private set; }
        public int PulseSequence { get; private set; }
        public int Attempt { get; private set; }
        public event Action Changed, Pulsed, CriticalOverload, Failed, Completed;
        private bool _criticalNotified;
        public int Remaining => Board == null ? 0 : Board.Budget - Insulated.Count;
        public float Safety => Board == null ? 1 : 1f - (float)Infected.Count / Math.Max(1, Board.Count - Board.Walls.Count);
        public bool IsFailed => Phase >= RelayBSurgePhase.CoreLost;
        public RelayBSurgeMask Frontier
        {
            get
            {
                var frontier = new RelayBSurgeMask();
                if (Board == null) return frontier;
                for (int cell = 0; cell < Board.Count; cell++) if (Infected.Has(cell))
                    foreach (int next in Board.Neighbors(cell))
                        if (!Board.Walls.Has(next) && !Infected.Has(next) && !Insulated.Has(next)) frontier.Add(next);
                return frontier;
            }
        }
        public void Initialize(int seed, int difficulty, int variant, int attempt = 0)
        {
            Reset(RelayBSurgeGenerator.Generate(seed, difficulty, variant, attempt), attempt);
        }
        internal void Reset(RelayBSurgeBoard board, int attempt)
        {
            Board = board; Attempt = attempt; Infected = board.Sources; Insulated = default;
            Elapsed = Cooldown = 0; PulseRemaining = board.PulseSeconds; PulseSequence = 0;
            Phase = RelayBSurgePhase.Ready; Changed?.Invoke();
            _criticalNotified=false;
        }
        public bool Start()
        {
            if (Board == null || Phase != RelayBSurgePhase.Ready) return false;
            Phase = RelayBSurgePhase.Running; Changed?.Invoke(); return true;
        }
        public bool CanPlace(int cell) => Board != null && Phase == RelayBSurgePhase.Running
            && Cooldown <= 0 && Remaining > 0 && cell >= 0 && cell < Board.Count
            && !Board.Walls.Has(cell) && !Board.Cores.Has(cell) && !Infected.Has(cell) && !Insulated.Has(cell);
        public bool Place(int cell)
        {
            if (!CanPlace(cell)) return false;
            var mask = Insulated; mask.Add(cell); Insulated = mask; Cooldown = PlacementCooldown;
            CheckContained(); Changed?.Invoke(); return true;
        }
        public void Tick(float dt)
        {
            if (Phase != RelayBSurgePhase.Running || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            // Process cooldown, deadline and every pulse in chronological order, including frame stalls.
            while (dt > 0 && Phase == RelayBSurgePhase.Running)
            {
                float step = Math.Min(dt, Math.Min(PulseRemaining, Math.Max(0, Board.TimeLimit - Elapsed)));
                Elapsed += step; Cooldown = Math.Max(0, Cooldown - step); PulseRemaining -= step; dt -= step;
                if (Elapsed >= Board.TimeLimit) { Lose(RelayBSurgePhase.TimedOut); break; }
                if (PulseRemaining <= 0.00001f)
                {
                    var next = Frontier;
                    Infected = new RelayBSurgeMask { Low = Infected.Low | next.Low, High = Infected.High | next.High };
                    PulseSequence++; PulseRemaining = Board.PulseSeconds; Pulsed?.Invoke();
                    if ((Infected.Low & Board.Cores.Low) != 0 || (Infected.High & Board.Cores.High) != 0) Lose(RelayBSurgePhase.CoreLost);
                    else if (Safety < Board.MinimumSafety) Lose(RelayBSurgePhase.SafetyLost);
                    else CheckContained();
                    if(Phase==RelayBSurgePhase.Running && Safety<0.55f && !_criticalNotified) {
                        _criticalNotified=true;CriticalOverload?.Invoke();
                    }
                }
            }
            Changed?.Invoke();
        }
        private void CheckContained()
        {
            if (Phase != RelayBSurgePhase.Running || Frontier.Count != 0) return;
            Phase = RelayBSurgePhase.Contained; Completed?.Invoke();
        }
        private void Lose(RelayBSurgePhase phase) { Phase = phase; Failed?.Invoke(); }
        public void ApplyRemote(RelayBSurgePhase phase, RelayBSurgeMask infected, RelayBSurgeMask insulated,
            float elapsed, float pulseRemaining, float cooldown, int pulseSequence)
        {
            Phase = phase; Infected = infected; Insulated = insulated; Elapsed = elapsed;
            PulseRemaining = pulseRemaining; Cooldown = cooldown; PulseSequence = pulseSequence;
            Changed?.Invoke();
        }
    }

    public static class RelayBSurgeGenerator
    {
        public static RelayBSurgeBoard Generate(int seed, int difficulty, int variant, int attempt)
        {
            difficulty = Math.Max(0, Math.Min(2, difficulty));
            var random = new Random(unchecked(seed * 397 ^ attempt * 7919 ^ variant * 101 ^ difficulty));
            for (int trial = 0; trial < 16384; trial++)
            {
                var board = Build(random, difficulty, variant, seed, trial % 1024 < 960);
                var cut = MinimumCut(board);
                int minimum = difficulty == 0 ? 2 : difficulty == 1 ? 4 : 5;
                if (cut.Length < minimum || cut.Length > board.Budget || board.Sources.Count == 0) continue;
                // A single straight row/column of obvious gates should not solve normal/hard.
                if (difficulty > 0 && !HasDistributedCut(board, cut)) continue;
                // Exclude the universal 'seal all neighboring source cells' shortcut.
                var preview = new RelayBSurgeSimulation(); preview.Reset(board, attempt);
                if (difficulty > 0 && preview.Frontier.Count <= board.Budget) continue;
                if (!VerifyTimedWitness(board, cut, out float seconds)) continue;
                if(seconds < (difficulty==0 ? 10f : difficulty==1 ? 22.4f : trial % 1024 < 960 ? 19.9f : 15.9f))continue;
                board.MinimumSeals = cut.Length; board.Witness = cut; board.WitnessSeconds = seconds;
                return board;
            }
            // Continue the same deterministic PRNG stream instead of replaying seed 1.
            // Every candidate, including simplified batches, must pass the same witness checks.
            throw new InvalidOperationException("No validated surge board could be generated.");
        }
        private static RelayBSurgeBoard Build(Random random, int difficulty, int variant, int seed, bool obstacles)
        {
            var b = new RelayBSurgeBoard { Width = difficulty == 0 ? 7 : difficulty == 1 ? 9 : 10,
                Height = difficulty == 2 ? 8 : 7, Budget = difficulty == 0 ? 3 : difficulty == 1 ? 4 : 5,
                PulseSeconds = difficulty == 0 ? 5 : difficulty == 1 ? 4.5f : 4,
                TimeLimit = difficulty == 0 ? 70 : difficulty == 1 ? 60 : 55,
                Difficulty = difficulty, Variant = variant, Seed = seed };
            int separator = difficulty == 0 ? 3 : difficulty == 1 ? 4 : 5;
            int[] separators = new int[b.Height];
            int protectedStart = separator + 1;
            for (int y = 0; y < b.Height; y++)
            {
                separators[y] = difficulty == 0 ? separator : separator + random.Next(-1, 2);
                protectedStart = Math.Max(protectedStart, separators[y] + 1);
            }
            int gates = difficulty == 0 ? random.Next(2, 4) : difficulty == 1 ? random.Next(4, 6) : random.Next(5, 7);
            var rows = new int[b.Height]; for (int y = 0; y < b.Height; y++) rows[y] = y;
            for (int y = rows.Length - 1; y > 0; y--) { int j = random.Next(y + 1); int t = rows[y]; rows[y] = rows[j]; rows[j] = t; }
            var openRows = new HashSet<int>(); for (int y = 0; y < gates; y++) openRows.Add(rows[y]);
            for (int y = 0; y < b.Height; y++)
            {
                bool sourceRow = difficulty == 0 ? y >= 1 && y <= 5 : difficulty == 1 ? y<=1 || y>=5 : y != 2 && y != 5;
                if (sourceRow) b.Sources.Add(y * b.Width);
                else if(difficulty!=1 || y==3)b.Walls.Add(y * b.Width);
                if (!openRows.Contains(y)) b.Walls.Add(y * b.Width + separators[y]);
                for (int x = 1; x < b.Width - 1; x++)
                    if (x != separators[y] && obstacles && random.NextDouble() < (variant == 0 ? 0.10 : 0.15)) b.Walls.Add(y * b.Width + x);
            }
            b.Cores.Add((b.Height / 2) * b.Width + b.Width - 1);
            if(difficulty==2) foreach(int row in new[]{1,4}) { int cell=row*b.Width+1;b.Walls.Remove(cell);b.Sources.Add(cell); }
            if (difficulty == 2) b.Cores.Add(b.Width * (b.Height - 2) + b.Width - 1);
            for(int y=0;y<b.Height;y++) for(int x=protectedStart;x<b.Width;x++)
                if(!b.Walls.Has(y*b.Width+x))b.ProtectedRegion.Add(y*b.Width+x);
            // Rotations/reflections change the approach and location of the protected side.
            bool mirrorX = random.Next(2) == 0, mirrorY = random.Next(2) == 0;
            b.Walls = Reflect(b, b.Walls, mirrorX, mirrorY);
            b.Sources = Reflect(b, b.Sources, mirrorX, mirrorY);
            b.Cores = Reflect(b, b.Cores, mirrorX, mirrorY);
            b.ProtectedRegion = Reflect(b, b.ProtectedRegion, mirrorX, mirrorY);
            return b;
        }
        public static bool HasDistributedCut(RelayBSurgeBoard board, int[] cut)
        {
            if (cut == null || cut.Length < 2) return false;
            int x = cut[0] % board.Width, y = cut[0] / board.Width;
            bool differentX = false, differentY = false;
            foreach (int cell in cut)
            {
                differentX |= cell % board.Width != x;
                differentY |= cell / board.Width != y;
            }
            return differentX && differentY;
        }
        private static RelayBSurgeMask Reflect(RelayBSurgeBoard b, RelayBSurgeMask mask, bool x, bool y)
        {
            var result = new RelayBSurgeMask();
            for (int c = 0; c < b.Count; c++) if (mask.Has(c))
                result.Add((y ? b.Height - 1 - c / b.Width : c / b.Width) * b.Width + (x ? b.Width - 1 - c % b.Width : c % b.Width));
            return result;
        }
        // Vertex splitting + Edmonds-Karp max-flow finds the minimum number of gray cells
        // separating every source from the protected region, not just the core's neighbors.
        // The resulting cut is accepted only after the timed containment simulation succeeds.
        public static int[] MinimumCut(RelayBSurgeBoard b)
        {
            int source = b.Count * 2, sink = source + 1, n = sink + 1;
            const int infinite = 1000;
            var residual = new int[n, n];
            for (int c = 0; c < b.Count; c++) if (!b.Walls.Has(c))
            {
                residual[c * 2, c * 2 + 1] = b.Sources.Has(c) || b.ProtectedRegion.Has(c) ? infinite : 1;
                if (b.Sources.Has(c)) residual[source, c * 2] = infinite;
                if (b.ProtectedRegion.Has(c)) residual[c * 2 + 1, sink] = infinite;
                foreach (int next in b.Neighbors(c)) if (!b.Walls.Has(next)) residual[c * 2 + 1, next * 2] = infinite;
            }
            var parent = new int[n]; var seen = new bool[n]; var queue = new Queue<int>();
            while (true)
            {
                Array.Clear(seen, 0, n); queue.Clear(); seen[source] = true; queue.Enqueue(source);
                while (queue.Count > 0 && !seen[sink])
                {
                    int u = queue.Dequeue();
                    for (int v = 0; v < n; v++) if (!seen[v] && residual[u, v] > 0) { parent[v] = u; seen[v] = true; queue.Enqueue(v); }
                }
                if (!seen[sink]) break;
                int flow = infinite;
                for (int v = sink; v != source; v = parent[v]) flow = Math.Min(flow, residual[parent[v], v]);
                for (int v = sink; v != source; v = parent[v]) { residual[parent[v], v] -= flow; residual[v, parent[v]] += flow; }
            }
            var cut = new List<int>();
            for (int c = 0; c < b.Count; c++) if (seen[c * 2] && !seen[c * 2 + 1] && !b.Walls.Has(c)) cut.Add(c);
            return cut.ToArray();
        }
        public static bool VerifyTimedWitness(RelayBSurgeBoard board, int[] placements, out float seconds)
        {
            var sim = new RelayBSurgeSimulation(); sim.Reset(board, 0); sim.Start();
            int next = 0; float nextPlacement = 1.5f;
            while (sim.Phase == RelayBSurgePhase.Running && sim.Elapsed < board.TimeLimit)
            {
                // Include reading time and 200 ms command latency between placements.
                if (next < placements.Length && sim.Elapsed >= nextPlacement)
                {
                    if (!sim.Place(placements[next++])) { seconds = sim.Elapsed; return false; }
                    nextPlacement = sim.Elapsed + RelayBSurgeSimulation.PlacementCooldown + 0.2f;
                }
                sim.Tick(0.05f);
            }
            seconds = sim.Elapsed; return sim.Phase == RelayBSurgePhase.Contained;
        }
    }
}
