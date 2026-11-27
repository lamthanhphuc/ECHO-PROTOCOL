using System;

namespace EchoProtocol.Salvage
{
    public enum SalvagePhase { Base, InDay, DaySummary, CycleSummary, Failed, Ended }
    public enum SalvageCargoState { Empty, World, Held, Stored, Sold, Destroyed, PhysicsHeld }
    public enum SalvageCargoMaterial { Metal, Plastic, Organic }

    [Serializable]
    public struct SalvageCargo
    {
        public int Serial, Definition, BaseValue, Integrity, Owner, Slot;
        public SalvageCargoState State;
        public int Value => Math.Max(0, BaseValue * Math.Clamp(Integrity, 0, 100) / 100);
    }

    public readonly struct SalvageScrapType
    {
        public readonly string Name, Visual;
        public readonly int MinValue, MaxValue;
        public readonly float Weight;
        public readonly bool Heavy, Fragile, Noisy;
        public readonly bool Pocketable;
        public readonly SalvageCargoMaterial Material;
        public SalvageScrapType(string name, string visual, int min, int max, float weight,
            SalvageCargoMaterial material, bool heavy = false, bool fragile = false, bool noisy = false, bool pocketable = true)
        { Name = name; Visual = visual; MinValue = min; MaxValue = max; Weight = weight;
          Material = material; Heavy = heavy; Fragile = fragile; Noisy = noisy; Pocketable = pocketable; }
    }

    // Owns only the new Run rules. Player, tools, input, voice and HUD stay in ECHO.
    public sealed class SalvageLedger
    {
        public const int Capacity = 128, CrewCapacity = 4, ScrapSlots = 4, DaysPerCycle = 3;
        public const float DaySeconds = 600;
        public static readonly SalvageScrapType[] Types =
        {
            new("Power Core", "FuelCell", 160, 240, 18, SalvageCargoMaterial.Metal, heavy:true),
            new("Industrial Battery", "FuelCell", 110, 170, 14, SalvageCargoMaterial.Metal, heavy:true),
            new("Research Scanner", "SurveyScanner", 45, 80, 3, SalvageCargoMaterial.Plastic),
            new("Communication Module", "Resonator", 35, 65, 2, SalvageCargoMaterial.Metal),
            new("Medical Supplies", "UtilityCase", 35, 60, 2, SalvageCargoMaterial.Plastic),
            new("Security Actuator", "UtilityCase", 40, 75, 4, SalvageCargoMaterial.Metal),
            new("Biological Specimen", "AlienSpecimen", 90, 150, 6, SalvageCargoMaterial.Organic, fragile:true),
            new("Industrial Canister", "FuelCanister", 75, 120, 10, SalvageCargoMaterial.Metal, heavy:true),
            new("Tissue Sample", "OrganicSample", 55, 100, 3, SalvageCargoMaterial.Organic, fragile:true),
            new("Unstable Resonator", "Resonator", 100, 160, 5, SalvageCargoMaterial.Metal, noisy:true),
            new("Survey Case", "UtilityCase", 50, 85, 5, SalvageCargoMaterial.Plastic),
            new("Containment Device", "FuelCanister", 120, 190, 12, SalvageCargoMaterial.Metal, heavy:true),
            new("Fire Extinguisher", "FireExtinguisher", 30, 65, 4, SalvageCargoMaterial.Metal),
            new("Security Camera", "SecurityCamera", 45, 85, 2, SalvageCargoMaterial.Metal),
            new("Wall Light", "WallLight", 25, 50, 1, SalvageCargoMaterial.Plastic),
            new("Hanging Light", "HangingLight", 30, 60, 2, SalvageCargoMaterial.Metal),
            new("Utility Cart", "UtilityCart", 100, 160, 18, SalvageCargoMaterial.Metal, pocketable:false),
            new("Waste Container", "WasteContainer", 60, 100, 12, SalvageCargoMaterial.Plastic, pocketable:false),
            new("Antique Clock", "AntiqueClock", 55, 100, 2, SalvageCargoMaterial.Metal),
            new("Table Lamp", "TableLamp", 35, 75, 2, SalvageCargoMaterial.Metal, fragile:true),
            new("Ceramic Vase", "CeramicVase", 60, 110, 2, SalvageCargoMaterial.Plastic, fragile:true),
            new("Decorative Vase", "DecorativeVase", 50, 95, 2, SalvageCargoMaterial.Plastic, fragile:true),
            new("Framed Painting", "FramedPainting", 45, 90, 2, SalvageCargoMaterial.Plastic),
            new("Recovery Chair", "RecoveryChair", 60, 100, 8, SalvageCargoMaterial.Metal, pocketable:false),
            new("Hospital Bed", "HospitalBed", 130, 210, 30, SalvageCargoMaterial.Metal, pocketable:false),
            new("Bunk Bed", "BunkBed", 140, 230, 35, SalvageCargoMaterial.Metal, pocketable:false),
            new("Storage Cabinet", "StorageCabinet", 100, 180, 25, SalvageCargoMaterial.Metal, pocketable:false),
            new("Recovery Sofa", "RecoverySofa", 110, 190, 22, SalvageCargoMaterial.Plastic, pocketable:false),
            new("Utility Table", "UtilityTable", 75, 130, 15, SalvageCargoMaterial.Metal, pocketable:false),
            new("Recovery Bench", "RecoveryBench", 85, 145, 16, SalvageCargoMaterial.Metal, pocketable:false),
            new("Vehicle Relic", "VehicleRelic", 180, 280, 30, SalvageCargoMaterial.Metal, pocketable:false),
            new("Boat Relic", "BoatRelic", 150, 240, 25, SalvageCargoMaterial.Metal, pocketable:false),
            new("Ship Relic", "ShipRelic", 200, 320, 35, SalvageCargoMaterial.Metal, pocketable:false),
            new("Saucer Relic", "SaucerRelic", 220, 350, 35, SalvageCargoMaterial.Metal, pocketable:false),
            new("Xe hàng", "TransportCart", 0, 0, 12, SalvageCargoMaterial.Metal, pocketable:false)
        };

        public readonly SalvageCargo[] Cargo = new SalvageCargo[Capacity];
        public SalvagePhase Phase { get; private set; }
        public int Cycle { get; private set; } = 1;
        public int Day { get; private set; } = 1;
        public int Funds { get; private set; }
        public int Quota { get; private set; }
        public int SoldThisCycle { get; private set; }
        public int TotalSold { get; private set; }
        public int LastDayRecovered { get; private set; }
        public int LastDayLost { get; private set; }
        public int NextSerial { get; private set; } = 1;
        public int CrewAtCycleStart { get; private set; }
        public bool DayCompleted { get; private set; }
        public int Revision { get; private set; }

        public SalvageLedger(int crew)
        { CrewAtCycleStart = Math.Clamp(crew, 2, 4); Quota = QuotaFor(1, CrewAtCycleStart); }

        public static int QuotaFor(int cycle, int crew)
        {
            int baseQuota = cycle <= 1 ? 600 : cycle == 2 ? 900
                : (int)Math.Min(1000000, 1250 * Math.Pow(1.4, Math.Min(30, cycle - 3)));
            return (int)Math.Ceiling(baseQuota * (1 + .2 * (Math.Clamp(crew, 2, 4) - 2)));
        }
        public static int CompletedLevelsFor(int cycle,int day,bool completed,int sold,int quota)=>Math.Max(0,cycle-1)+(day==3&&completed&&sold>=quota?1:0);
        public static int CompletedDaysFor(int cycle,int day,bool completed)=>Math.Max(0,cycle-1)*3+day-1+(completed?1:0);
        public static int CartDefinition => Types.Length-1;
        public static int EquipmentPrice(int tool) => tool switch { 1 => 120, 2 => 40, 3 => 60, 4 => 50, 5 => 110, 6 => 180, _ => -1 };
        public static int EquipmentLimit(int tool) => tool switch {1=>2,2=>2,3=>4,4=>2,5=>2,6=>1,_=>0};
        public bool TryCreateCart(out int index)
        {
            index=-1;
            if(Phase==SalvagePhase.InDay||Phase==SalvagePhase.Failed||Phase==SalvagePhase.Ended||NextSerial==int.MaxValue)return false;
            for(int i=0;i<Capacity;i++)
                if(Cargo[i].State==SalvageCargoState.Empty||Cargo[i].State==SalvageCargoState.Sold||Cargo[i].State==SalvageCargoState.Destroyed)
                {Cargo[i]=new SalvageCargo{Definition=CartDefinition,Serial=NextSerial++,Integrity=100,Owner=-1,Slot=-1,State=SalvageCargoState.Stored};index=i;Revision++;return true;}
            return false;
        }
        public static int MinimumLevel(int definition) => definition>=30?5:definition>=24&&definition<=29?3:1;
        public static float LootScale(int cycle) => (float)Math.Min(800, Math.Pow(1.4, Math.Clamp(cycle - 3, 0, 30)));
        public static float CarrySpeed(float kg) => kg <= 10 ? 1 : kg <= 25
            ? 1 - (kg - 10) / 15 * .15f : Math.Max(.75f, .85f - (kg - 25) / 15 * .1f);

        public bool TryCreate(int definition, int value, out int index)
        {
            index = -1;
            if (Phase != SalvagePhase.InDay || definition < 0 || definition >= Types.Length
                || NextSerial == int.MaxValue) return false;
            for (int i = 0; i < Cargo.Length; i++)
            {
                if (Cargo[i].State != SalvageCargoState.Empty && Cargo[i].State != SalvageCargoState.Sold && Cargo[i].State != SalvageCargoState.Destroyed) continue;
                var type = Types[definition];
                float scale = LootScale(Cycle);
                Cargo[i] = new SalvageCargo { Serial = NextSerial++, Definition = definition,
                    BaseValue = Math.Clamp(value, (int)(type.MinValue * scale), (int)(type.MaxValue * scale)), Integrity = 100,
                    Owner = -1, Slot = -1, State = SalvageCargoState.World };
                index = i; Revision++; return true;
            }
            return false;
        }
        public int FindHeld(int owner, int slot)
        {
            for (int i = 0; i < Cargo.Length; i++)
                if (Cargo[i].State == SalvageCargoState.Held && Cargo[i].Owner == owner
                    && (Cargo[i].Slot == slot || Types[Cargo[i].Definition].Heavy && Cargo[i].Slot + 1 == slot)) return i;
            return -1;
        }
        public int HeavyHeld(int owner)
        {
            for (int i = 0; i < Cargo.Length; i++)
                if (Cargo[i].State == SalvageCargoState.Held && Cargo[i].Owner == owner && Types[Cargo[i].Definition].Heavy) return i;
            return -1;
        }
        public float Weight(int owner)
        {
            float result = 0;
            foreach (var item in Cargo) if (item.State == SalvageCargoState.Held && item.Owner == owner) result += Types[item.Definition].Weight;
            return result;
        }
        public bool TryPickup(int index, int serial, int owner, out int selected)
        {
            selected = -1;
            if (!IsItem(index, serial) || owner < 0 || owner >= CrewCapacity || Phase != SalvagePhase.InDay) return false;
            var item = Cargo[index];
            if (item.State != SalvageCargoState.World || !Types[item.Definition].Pocketable || HeavyHeld(owner) >= 0 || PhysicsHeld(owner)>=0) return false;
            bool heavy = Types[item.Definition].Heavy;
            for (int slot = 0; slot < ScrapSlots; slot++)
            {
                if (FindHeld(owner, slot) >= 0 || heavy && (slot == ScrapSlots - 1 || FindHeld(owner, slot + 1) >= 0)) continue;
                item.State = SalvageCargoState.Held; item.Owner = owner; item.Slot = slot;
                Cargo[index] = item; selected = slot; Revision++; return true;
            }
            return false;
        }
        public bool TryDrop(int index, int serial, int owner)
        {
            if (!IsItem(index, serial) || Cargo[index].State != SalvageCargoState.Held || Cargo[index].Owner != owner) return false;
            var item = Cargo[index]; item.State = SalvageCargoState.World; item.Owner = item.Slot = -1;
            Cargo[index] = item; Revision++; return true;
        }
        public int PhysicsHeld(int owner)
        { for(int i=0;i<Cargo.Length;i++)if(Cargo[i].State==SalvageCargoState.PhysicsHeld&&Cargo[i].Owner==owner)return i;return -1; }
        public bool TryGrab(int index,int serial,int owner)
        {
            if(!IsItem(index,serial)||owner<0||owner>=CrewCapacity||PhysicsHeld(owner)>=0
                ||Phase==SalvagePhase.Failed||Phase==SalvagePhase.Ended)return false;
            var c=Cargo[index];
            if(c.State!=SalvageCargoState.World&&c.State!=SalvageCargoState.Stored)return false;
            if(Phase!=SalvagePhase.InDay&&c.State!=SalvageCargoState.Stored)return false;
            c.State=SalvageCargoState.PhysicsHeld;c.Owner=owner;c.Slot=-1;Cargo[index]=c;Revision++;return true;
        }
        public bool TryRelease(int index,int serial,int owner)
        {
            if(!IsItem(index,serial)||Cargo[index].State!=SalvageCargoState.PhysicsHeld||Cargo[index].Owner!=owner)return false;
            var c=Cargo[index];c.State=SalvageCargoState.World;c.Owner=c.Slot=-1;Cargo[index]=c;Revision++;return true;
        }
        public bool AcceptDelivery(int index,int serial)
        {
            if(!IsItem(index,serial)||Cargo[index].State!=SalvageCargoState.World||Phase==SalvagePhase.Failed||Phase==SalvagePhase.Ended)return false;
            var c=Cargo[index];c.State=SalvageCargoState.Stored;c.Owner=c.Slot=-1;Cargo[index]=c;Revision++;return true;
        }
        public void ReceivingChanged()=>Revision++;
        public bool StoreItem(int index,int owner)
        {
            if(Phase!=SalvagePhase.InDay||index<0||index>=Cargo.Length||Cargo[index].State!=SalvageCargoState.Held||Cargo[index].Owner!=owner)return false;
            var item=Cargo[index];item.State=SalvageCargoState.Stored;item.Owner=item.Slot=-1;Cargo[index]=item;Revision++;return true;
        }
        public int Store(int owner)
        {
            if (Phase != SalvagePhase.InDay) return 0;
            int count = 0;
            for (int i = 0; i < Cargo.Length; i++)
            {
                var item = Cargo[i]; if (item.State != SalvageCargoState.Held || item.Owner != owner) continue;
                item.State = SalvageCargoState.Stored; item.Owner = item.Slot = -1; Cargo[i] = item; count++;
            }
            if (count > 0) Revision++;
            return count;
        }
        public bool TryWithdraw(int index, int serial, int owner, out int selected)
        {
            selected = -1;
            if (!IsItem(index, serial) || Cargo[index].Definition==CartDefinition || Cargo[index].State != SalvageCargoState.Stored || Phase != SalvagePhase.InDay) return false;
            var original = Cargo[index]; var item = original; item.State = SalvageCargoState.World; Cargo[index] = item;
            if (TryPickup(index, serial, owner, out selected)) return true;
            Cargo[index] = original; return false;
        }
        public bool TrySell(int index, int serial)
        {
            if (!IsItem(index, serial) || Cargo[index].Definition==CartDefinition || Cargo[index].State != SalvageCargoState.Stored
                || Phase == SalvagePhase.Failed || Phase == SalvagePhase.Ended) return false;
            var item = Cargo[index];
            int surplus = SurplusFromSale(item.Value);
            if (SoldThisCycle > int.MaxValue - item.Value) return false;
            Funds = (int)Math.Min(int.MaxValue,(long)Funds+surplus); SoldThisCycle += item.Value; TotalSold = (int)Math.Min(int.MaxValue,(long)TotalSold+item.Value);
            item.State = SalvageCargoState.Sold; Cargo[index] = item; Revision++; return true;
        }
        public int SurplusFromSale(int value) => value <= 0 ? 0 : Math.Max(0, value - Math.Max(0, Quota - SoldThisCycle));
        public bool TrySpendPower(int price)
        {
            if (price <= 0 || Funds < price || Phase == SalvagePhase.InDay || Phase == SalvagePhase.Failed || Phase == SalvagePhase.Ended) return false;
            Funds -= price; Revision++; return true;
        }
        public bool TryPurchase(int tool, bool equipmentEmpty)
        {
            int price = EquipmentPrice(tool);
            if (!equipmentEmpty || price < 0 || Funds < price || Phase == SalvagePhase.InDay
                || Phase == SalvagePhase.Failed || Phase == SalvagePhase.Ended) return false;
            Funds -= price; Revision++; return true;
        }
        public int StoredValue()
        { int total = 0; foreach (var item in Cargo) if (item.State == SalvageCargoState.Stored) total += item.Value; return total; }
        public bool TrySellAll(int expectedRevision, int expectedValue)
        {
            if (expectedRevision != Revision || expectedValue <= 0 || StoredValue() != expectedValue
                || Phase == SalvagePhase.Failed || Phase == SalvagePhase.Ended
                || SoldThisCycle > int.MaxValue - expectedValue) return false;
            for (int i = 0; i < Cargo.Length; i++)
            { var item = Cargo[i]; if (item.State != SalvageCargoState.Stored || item.Definition==CartDefinition) continue; item.State = SalvageCargoState.Sold; Cargo[i] = item; }
            Funds = (int)Math.Min(int.MaxValue,(long)Funds+SurplusFromSale(expectedValue)); SoldThisCycle += expectedValue; TotalSold = (int)Math.Min(int.MaxValue,(long)TotalSold+expectedValue); Revision++; return true;
        }
        public bool Damage(int index, int serial, int amount, bool physicsImpact=false)
        {
            if (!IsItem(index, serial) || Cargo[index].Definition==CartDefinition || Phase != SalvagePhase.InDay || (Cargo[index].State != SalvageCargoState.World && Cargo[index].State != SalvageCargoState.PhysicsHeld)
                || (!Types[Cargo[index].Definition].Fragile&&!physicsImpact) || amount <= 0) return false;
            var item = Cargo[index]; item.Integrity = Math.Max(0, item.Integrity - Math.Min(100, amount));
            if (item.Integrity == 0 || item.Value==0) item.State = SalvageCargoState.Destroyed;
            Cargo[index] = item; Revision++; return true;
        }
        public bool LoseWorldCargo(int index, int serial)
        {
            if (!IsItem(index, serial) || Cargo[index].Definition==CartDefinition || Phase != SalvagePhase.InDay || Cargo[index].State != SalvageCargoState.World) return false;
            var item = Cargo[index]; LastDayLost += item.Value; item.State = SalvageCargoState.Destroyed;
            Cargo[index] = item; Revision++; return true;
        }
        public bool Depart(int connectedCrew)
        {
            if (Phase != SalvagePhase.Base && Phase != SalvagePhase.DaySummary && Phase != SalvagePhase.CycleSummary) return false;
            if (DayCompleted)
            {
                if (Day == DaysPerCycle)
                {
                    if (SoldThisCycle < Quota) { Phase = SalvagePhase.Failed; Revision++; return false; }
                    Phase = SalvagePhase.CycleSummary; Cycle++; Day = 1; SoldThisCycle = 0;
                    CrewAtCycleStart = Math.Clamp(connectedCrew, 2, 4); Quota = QuotaFor(Cycle, CrewAtCycleStart);
                    DayCompleted = false; Revision++; return false;
                }
                Day++; DayCompleted = false;
            }
            Phase = SalvagePhase.InDay; LastDayRecovered = LastDayLost = 0; Revision++; return true;
        }
        public bool Return(bool[] survivors)
        {
            if (Phase != SalvagePhase.InDay || survivors == null || survivors.Length != CrewCapacity) return false;
            for (int i = 0; i < Cargo.Length; i++)
            {
                var item = Cargo[i];
                if (item.State == SalvageCargoState.Held && survivors[item.Owner])
                { LastDayRecovered += item.Value; item.State = SalvageCargoState.Stored; item.Owner = item.Slot = -1; }
                else if (item.State == SalvageCargoState.World || item.State == SalvageCargoState.Held || item.State == SalvageCargoState.PhysicsHeld)
                { LastDayLost += item.Value; item.State = SalvageCargoState.Destroyed; item.Owner = item.Slot = -1; }
                Cargo[i] = item;
            }
            DayCompleted = true; Phase = SalvagePhase.DaySummary; Revision++; return true;
        }
        public bool End()
        {
            if (Phase == SalvagePhase.InDay || Phase == SalvagePhase.Ended || Phase == SalvagePhase.Failed) return false;
            Phase = SalvagePhase.Ended; Revision++; return true;
        }
        public bool IsItem(int index, int serial) => index >= 0 && index < Capacity && serial > 0 && Cargo[index].Serial == serial;

        // Replicated snapshot restores the same ledger after reconnect/host takeover.
        public void Restore(SalvagePhase phase, int cycle, int day, int funds, int quota, int sold, int total,
            int nextSerial, int crew, bool completed, int revision, int recovered, int lost)
        {
            if (!Enum.IsDefined(typeof(SalvagePhase), phase) || cycle < 1 || day < 1 || day > DaysPerCycle
                || funds < 0 || quota < 1 || sold < 0 || total < 0 || nextSerial < 1 || crew < 2 || crew > 4)
                throw new InvalidOperationException("Invalid Salvage snapshot.");
            Phase = phase; Cycle = cycle; Day = day; Funds = funds; Quota = quota; SoldThisCycle = sold;
            TotalSold = total; NextSerial = nextSerial; CrewAtCycleStart = crew; DayCompleted = completed;
            Revision = revision; LastDayRecovered = recovered; LastDayLost = lost;
        }
    }
}
