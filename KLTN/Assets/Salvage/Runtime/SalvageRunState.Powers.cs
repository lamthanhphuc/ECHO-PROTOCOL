using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.Salvage
{
    public enum SalvagePower { Speed, Health, Stamina, Recovery, Strength }

    public sealed partial class SalvageRunState
    {
        public const int PowerRankLimit = 3, PowerDailyLimit = 2;
        public static readonly string[] PowerNames = { "Tốc độ di chuyển", "Máu tối đa", "Thể lực tối đa", "Hồi thể lực", "Sức nâng" };
        internal static string PowerName(int power) => EchoProtocol.Settings.GameLanguage.Choose(PowerNames[power], power switch
        {
            0 => "Movement Speed",
            1 => "Max Health",
            2 => "Max Stamina",
            3 => "Stamina Recovery",
            _ => "Lifting Strength"
        });
        public static readonly float[] PowerSteps = { .05f, 0f, 0f, .1f, .25f };
        public static bool UnlimitedPower(int power) => power == (int)SalvagePower.Health || power == (int)SalvagePower.Stamina;
        public static bool CanUpgradePower(int power, int rank) => power >= 0 && power < PowerNames.Length
            && rank >= 0 && rank < int.MaxValue && (UnlimitedPower(power) || rank < PowerRankLimit);
        public static int PowerPrice(int power, int rank)
        {
            if (!CanUpgradePower(power, rank)) return -1;
            if (!UnlimitedPower(power)) return 75 << rank;
            // Decimal keeps the price positive even for malformed or extremely deep-run ranks.
            decimal n = rank;
            decimal price = power == (int)SalvagePower.Health ? 75 + 25*n + 5*n*n : 50 + 15*n + 3*n*n;
            return (int)System.Math.Min(int.MaxValue, price);
        }
        public static float FlatPowerBonus(SalvagePower power, int rank) => System.Math.Max(0, rank)
            * (power == SalvagePower.Health ? 20f : power == SalvagePower.Stamina ? 10f : 0f);
        public int PowerRank(int seat, int power)
        {
            if (seat < 0 || seat >= 4) return 0;
            var c = Crew[seat];
            return power switch { 0 => c.SpeedRank, 1 => c.HealthRank, 2 => c.StaminaRank, 3 => c.RecoveryRank, 4 => c.StrengthRank, _ => 0 };
        }
        public int PowerPurchasesToday(int seat) => seat >= 0 && seat < 4 && Crew[seat].PowerDay == Cycle * 3 + Day ? Crew[seat].PowerPurchases : 0;
        private static int PlayerPowerRank(GameObject player, SalvagePower power)
        {
            var run = Instance;
            if (!SalvageSite.IsActive || run == null || run.Object == null || !run.Object.IsValid || player == null) return 0;
            var state = player.GetComponentInParent<LobbyPlayerState>();
            if (state == null || state.Object == null || !state.Object.IsValid) return 0;
            return run.PowerRank(run.Seat(state.Object.InputAuthority), (int)power);
        }
        public static float PowerBonus(GameObject player, SalvagePower power) => FlatPowerBonus(power, PlayerPowerRank(player,power));
        public static float PowerMultiplier(GameObject player, SalvagePower power) =>
            1 + PlayerPowerRank(player,power) * PowerSteps[(int)power];
        private bool BuyPower(int seat, int power, int expectedRank)
        {
            if (seat < 0 || !CanUpgradePower(power,expectedRank) || PowerRank(seat, power) != expectedRank
                || PowerPurchasesToday(seat) >= PowerDailyLimit || !AtTerminal(Player(seat))
                || !_ledger.TrySpendPower(PowerPrice(power,expectedRank))) return false;
            var c = Crew[seat];
            switch (power) { case 0: c.SpeedRank++; break; case 1: c.HealthRank++; break;
                case 2: c.StaminaRank++; break; case 3: c.RecoveryRank++; break; case 4: c.StrengthRank++; break; }
            c.PowerPurchases = PowerPurchasesToday(seat) + 1; c.PowerDay = Cycle * 3 + Day;
            Crew.Set(seat, c); Publish(); return true;
        }
    }
}
