using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDV02SupplyLedger
    {
        private readonly bool[] _spawned = new bool[3];
        public AEDV02SupplyLedger(Guid matchId, int optionalBudget)
        {
            if (matchId == Guid.Empty || optionalBudget < 0 || optionalBudget > 2) throw new ArgumentException("Invalid match supply budget.");
            MatchId = matchId; InitialOptionalBudget = OptionalBudget = optionalBudget;
        }
        public Guid MatchId { get; }
        public int InitialOptionalBudget { get; }
        public int OptionalBudget { get; private set; }
        public int OptionalMaterialized { get; private set; }
        public bool HasSpawned(int zone) => zone >= 1 && zone <= 3 && _spawned[zone - 1];
        public int OptionalForZone(int zone) => zone >= 1 && zone <= 3 ? Math.Min(1, Math.Max(0, OptionalBudget - zone + 1)) : 0;
        public bool TrySetOptionalBudget(int budget)
        {
            if (!CanSetOptionalBudget(budget)) return false;
            OptionalBudget = budget; return true;
        }
        public bool CanSetOptionalBudget(int budget)
        {
            if (budget < OptionalMaterialized || budget < 0 || budget > InitialOptionalBudget) return false;
            if (budget == OptionalBudget) return true;
            for (int zone = 1; zone <= 3; zone++)
                if (!HasSpawned(zone) && OptionalForZone(zone) != Math.Min(1, Math.Max(0, budget - zone + 1))) return true;
            return false;
        }
        public bool TryRecordZoneSpawn(int zone, int required, int optional)
        {
            if (zone < 1 || zone > 3 || HasSpawned(zone) || required != 5 || optional < 0 || optional > OptionalForZone(zone) ||
                optional + OptionalMaterialized > OptionalBudget) return false;
            _spawned[zone - 1] = true; OptionalMaterialized += optional; return true;
        }
    }
}
