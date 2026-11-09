using System;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDV02SupplyLedger
    {
        private readonly bool[] _spawned = new bool[3];
        private int? _optionalMask;
        public AEDV02SupplyLedger(Guid matchId, int optionalBudget, int? optionalMask = null)
        {
            if (matchId == Guid.Empty || optionalBudget < 0 || optionalBudget > 2) throw new ArgumentException("Invalid match supply budget.");
            if (optionalMask is < 0 or > 7) throw new ArgumentException("Invalid optional zone mask.");
            MatchId = matchId;
            _optionalMask = optionalMask;
            InitialOptionalBudget = optionalMask.HasValue ? 3 : optionalBudget;
            OptionalBudget = optionalMask.HasValue ? AEDV02Plan.OptionalCount(optionalMask.Value) : optionalBudget;
        }
        public Guid MatchId { get; }
        public int InitialOptionalBudget { get; }
        public int OptionalBudget { get; private set; }
        public int OptionalMaterialized { get; private set; }
        public int ResourceGrade { get; private set; }
        public bool CanSetResourceGrade(int grade) => grade is >= -1 and <= 3 && (grade == ResourceGrade || !_spawned[0] || !_spawned[1] || !_spawned[2]);
        public bool TrySetResourceGrade(int grade) { if (!CanSetResourceGrade(grade)) return false; ResourceGrade = grade; return true; }
        public bool HasSpawned(int zone) => zone >= 1 && zone <= 3 && _spawned[zone - 1];
        public int OptionalForZone(int zone) => zone is >= 1 and <= 3
            ? _optionalMask.HasValue ? AEDV02Plan.OptionalForZone(_optionalMask.Value, zone)
                : Math.Min(1, Math.Max(0, OptionalBudget - zone + 1))
            : 0;

        public bool CanSetOptionalMask(int mask) => _optionalMask.HasValue && mask is >= 0 and <= 7 &&
            (mask == _optionalMask.Value || ResourceGrade == 0 &&
                (Enumerable.Range(1, 3).All(zone => !HasSpawned(zone) ||
                    OptionalForZone(zone) == AEDV02Plan.OptionalForZone(mask, zone))));

        public bool TrySetOptionalMask(int mask)
        {
            if (!CanSetOptionalMask(mask)) return false;
            _optionalMask = mask;
            OptionalBudget = AEDV02Plan.OptionalCount(mask);
            return true;
        }
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
