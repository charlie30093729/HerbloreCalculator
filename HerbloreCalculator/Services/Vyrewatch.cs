using System;
using System.Collections.Generic;
using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;

namespace HerbloreCalculator.Services
{
    /// <summary>
    /// Vyrewatch Sentinels EV tracker:
    /// - Uses selected drops only (blood shard, addy plate, rune kite, runite bar, rune full helm,
    ///   runite ore, ranarr seed, onyx bolt tips)
    /// - Live prices via PriceFetcher (pass price map in)
    /// - Subtracts hourly supplies (2x Prayer(4) + 2x Super combat(4) by default)
    /// - KPH and number of accounts are adjustable below
    /// </summary>
    public static class Vyrewatch
    {
        // ---------- Config ----------
        public static int KillsPerHour = 100;
        public static int AccountsRun = 5;

        // Supplies per hour (change to taste)
        // If PrayerPot4Id (30125 "Prayer Regen(4)") is not a real/priced item, price will read as 0.
        // Swap to 2434 for Prayer potion(4) if that's what you want to use.
        private const int PrayerPot4Id = 30125; // "Prayer Regen(4)" (use 2434 for Prayer potion(4))
        private const int SuperCombat4Id = 12695; // Super combat potion(4)
        private const int PrayerPotionsPerHr = 2;
        private const int SuperCombatsPerHr = 2;

        // Tracked drop item IDs
        private const int BloodShardId = 24777;
        private const int AdamantPlateId = 1123;
        private const int RuneKiteId = 1201;
        private const int RuniteBarId = 2363;
        private const int RuneFullHelmId = 1163;
        private const int RuniteOreId = 451;
        private const int RanarrSeedId = 5295;
        private const int OnyxBoltTipsId = 9194;

        // ---------- Drop rates (p per kill, avg qty) ----------
        // Tweak as desired to match your preferred source.
        private static readonly Dictionary<int, Drop> Drops = new()
        {
            { BloodShardId,   new Drop("Blood shard",        1.0/1500.0, 1) },
            { AdamantPlateId, new Drop("Adamant platebody",  1.0/128.0,  1) },
            { RuneKiteId,     new Drop("Rune kiteshield",    1.0/128.0,  1) },
            { RuniteBarId,    new Drop("Runite bar",         1.0/128.0,  1) },
            { RuneFullHelmId, new Drop("Rune full helm",     1.0/128.0,  1) },
            { RuniteOreId,    new Drop("Runite ore",         1.0/100.0,  1) },
            { RanarrSeedId,   new Drop("Ranarr seed",        1.0/106.0,  1) },
            { OnyxBoltTipsId, new Drop("Onyx bolt tips",     1.0/128.0, 12) }, // stack drop; 12 is a common qty
        };

        private record Drop(string Name, double ProbabilityPerKill, int AvgQuantity);

        public static void DisplayReport(Dictionary<int, PriceData> prices)
        {
            // ---------- 1) EV from drops ----------
            double evPerKill = 0;
            foreach (var (id, d) in Drops)
            {
                double price = TryMidPrice(prices, id);
                if (price <= 0) continue;

                evPerKill += d.ProbabilityPerKill * d.AvgQuantity * price;
            }
            double dropsGpPerHourGross = evPerKill * KillsPerHour;

            // ---------- 2) Supplies per hour ----------
            double prayerPrice = TryMidPrice(prices, PrayerPot4Id);
            double scPrice = TryMidPrice(prices, SuperCombat4Id);
            double suppliesPerHour =
                (prayerPrice * PrayerPotionsPerHr) +
                (scPrice * SuperCombatsPerHr);

            // ---------- 3) Profit ----------
            const double geTax = 0.02; // apply to drop revenue only
            double dropsAfterTaxPerAcct = dropsGpPerHourGross * (1 - geTax);
            double profitPerAccount = dropsAfterTaxPerAcct - suppliesPerHour;
            double profitAllAccounts = profitPerAccount * AccountsRun;

            // ---------- 4) Neat aligned printout (label | op | value) ----------
            const int labelW = 28; // left column width
            const int opW = 3;  // " - " / spaces
            const int valueW = 15; // right-aligned number width

            string L(string s) => s.PadRight(labelW);
            string OP(bool minus) => minus ? " - " : new string(' ', opW);
            string GP(double v) => $"{v,valueW:N0} gp";
            string NUM(double v) => $"{v,valueW:N0}";

            Console.WriteLine("=== Vyrewatch Sentinels ===");
            Console.WriteLine($"{L("Kills per hour:")}{OP(false)}{NUM(KillsPerHour)}");
            Console.WriteLine($"{L("Accounts played:")}{OP(false)}{NUM(AccountsRun)}");
            Console.WriteLine();

            Console.WriteLine($"{L("Supplies/hour:")}{OP(true)}{GP(suppliesPerHour)}");
            Console.WriteLine(new string('-', labelW + opW + valueW + 3));

            ConsoleHelper.SetColor(profitPerAccount);
            Console.WriteLine($"{L("Profit/hour per account:")}{OP(false)}{GP(profitPerAccount)}");
            Console.ResetColor();

            ConsoleHelper.SetColor(profitAllAccounts);
            Console.WriteLine($"{L("Estimated profit/hour (all):")}{OP(false)}{GP(profitAllAccounts)}");
            Console.ResetColor();

            Console.WriteLine();
        }

        // Mid-price helper (avg of high & low). Returns 0 if not found.
        private static double TryMidPrice(Dictionary<int, PriceData> prices, int id)
        {
            if (id <= 0) return 0;
            if (!prices.TryGetValue(id, out var p)) return 0;
            return (p.High + p.Low) / 2.0;
        }
    }
}
