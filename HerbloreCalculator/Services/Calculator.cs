using System;
using System.Collections.Generic;
using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;

namespace HerbloreCalculator.Services
{
    public enum FlowMode { Normal = 0, Fast = 1, Flow = 2 } // Flow = turbo / instant-ish

    public static class Calculator
    {
        // ***** Public mode switch (set by Program via hotkeys) *****
        public static FlowMode Mode { get; set; } = FlowMode.Normal;

        // ----- Fixed mechanics (your spec) -----
        private const double FourDoseChance = 0.15;        // 15% chance for +1 dose (4-dose proc)
        private const double SecondarySaveChance = 0.10;   // 10% chance to save secondary
        private const double ChargesPerChemAmulet = 10.0;  // 10 charges per chemistry amulet
        private const double PotionsPerHour = 2525.0;      // throughput for XP/hr & GP/hr

        // ----- Tight market thresholds -----
        private const double TightSpreadAbs = 20.0;        // <= 20 gp spread considered tight
        private const double TightSpreadPctOfMid = 0.005;  // <= 0.5% of mid considered tight

        // ----- Adaptive aggression (used in Normal mode) -----
        private const double BasePctIntoSpread = 0.15;     // baseline depth into the spread
        private const double MaxPctIntoSpread = 0.45;     // cap depth into the spread
        private const double WideSpreadPct = 0.012;    // >= 1.2% of mid => "wide" market
        private const int StaleMinutes = 10;       // quotes older than this => be more aggressive

        public static void DisplayPotionReport(
            Potion potion,
            Dictionary<int, PriceData> prices,
            double remainingXp,
            PriceData chemAmmyPrice
        )
        {
            if (!prices.ContainsKey(potion.BaseId) ||
                !prices.ContainsKey(potion.SecondaryId) ||
                !prices.ContainsKey(potion.Output3Id) ||
                !prices.ContainsKey(potion.Output4Id))
            {
                Console.WriteLine($"Missing price data for {potion.Name}");
                return;
            }

            var basePrice = prices[potion.BaseId];
            var secondaryPrice = prices[potion.SecondaryId];
            var output3Price = prices[potion.Output3Id];
            var output4Price = prices[potion.Output4Id];

            Console.WriteLine($"=== {potion.Name} ===");
            Console.WriteLine($"XP per potion: {potion.Xp:N1}");
            Console.WriteLine($"XP per hour: {(potion.Xp * PotionsPerHour):N0}");

            // Crafts required (ceil so we reach/overhit target XP)
            long craftsNeeded = (long)Math.Ceiling(remainingXp / potion.Xp);
            long basesNeeded = craftsNeeded; // 1 base per craft
            long secondariesNeeded = (long)Math.Ceiling(craftsNeeded * (1.0 - SecondarySaveChance)); // ~10% saved
            Console.WriteLine($"Need to target: {basesNeeded:N0} base potions, {secondariesNeeded:N0} secondaries");

            // Worst = buy inputs at high; sell outputs at low
            DisplayScenario(
                "Worst",
                basePrice.High, secondaryPrice.High,
                output3Price.Low, output4Price.Low,
                potion.Xp, remainingXp,
                chemAmmyPrice?.High ?? 0
            );

            // Average = midpoints
            DisplayScenario(
                "Average",
                Avg(basePrice.High, basePrice.Low), Avg(secondaryPrice.High, secondaryPrice.Low),
                Avg(output3Price.High, output3Price.Low), Avg(output4Price.High, output4Price.Low),
                potion.Xp, remainingXp,
                Avg(chemAmmyPrice?.High ?? 0, chemAmmyPrice?.Low ?? 0)
            );

            // Best = buy low; sell high
            DisplayScenario(
                "Best",
                basePrice.Low, secondaryPrice.Low,
                output3Price.High, output4Price.High,
                potion.Xp, remainingXp,
                chemAmmyPrice?.Low ?? 0
            );

            // GE offer helper (mode-aware suggested offers, EV-based gp/xp in header)
            string sellingAs = DecideSellingFormByAverage(output3Price, output4Price);
            DisplayOfferGuide(sellingAs, basePrice, secondaryPrice, output3Price, output4Price, chemAmmyPrice, potion.Xp);

            Console.WriteLine($"Last updated: {output3Price.LastUpdate:dd MMM yyyy HH:mm:ss}");
        }

        private static void DisplayScenario(
            string label,
            double baseCost, double secondaryCost,
            double output3, double output4,
            double xp, double remainingXp,
            double chemAmmyBuyPrice
        )
        {
            // Auto-decant: compare gp/dose and sell in the better form
            double perDose3 = output3 / 3.0;
            double perDose4 = output4 / 4.0;
            double bestPerDose = Math.Max(perDose3, perDose4);

            // Expected doses from one craft (3 base + 15% EV)
            double expectedDoses = 3.0 + FourDoseChance;

            // Revenue (after 2% GE tax)
            double revenue = (bestPerDose * expectedDoses) * 0.98;

            // Expected chemistry charge cost per craft (only on proc)
            double chargeCostPerProc = chemAmmyBuyPrice > 0 ? chemAmmyBuyPrice / ChargesPerChemAmulet : 0.0;
            double expectedChargeCost = FourDoseChance * chargeCostPerProc;

            // Expected profit incl. 10% secondary saving
            double expectedProfit = revenue - (baseCost + secondaryCost + expectedChargeCost)
                                    + (SecondarySaveChance * secondaryCost);

            double gpPerXp = expectedProfit / xp;
            double totalGp = (remainingXp / xp) * expectedProfit;
            double gpPerHr = expectedProfit * PotionsPerHour;

            Console.Write($"{label,-8}");

            ConsoleHelper.SetColor(expectedProfit);
            Console.Write($"{expectedProfit,12:N2} gp");
            Console.ResetColor();

            Console.Write("  |  ");

            ConsoleHelper.SetColor(gpPerXp);
            Console.Write($"{gpPerXp,8:N2} gp/xp");
            Console.ResetColor();

            Console.Write("  |  ");

            ConsoleHelper.SetColor(totalGp);
            Console.Write($"{totalGp,15:N0} gp (to target)");
            Console.ResetColor();

            Console.Write("  |  ");

            ConsoleHelper.SetColor(gpPerHr);
            Console.Write($"{gpPerHr,12:N0} gp/hr");
            Console.ResetColor();

            Console.WriteLine();
        }

        // ---------- GE offer helper (mode-aware suggested prices + EV gp/xp) ----------
        private static void DisplayOfferGuide(
            string sellingAs,
            PriceData baseP, PriceData secP, PriceData out3P, PriceData out4P,
            PriceData chemP, double xp)
        {
            // Mode-aware suggested offers
            double buyBase = SuggestBuy(baseP);
            double buySec = SuggestBuy(secP);
            double sellOut = sellingAs.Contains("3") ? SuggestSell(out3P)
                                                     : SuggestSell(out4P);

            // EV gp/xp using the SAME formula as the table, but with our suggested prices
            double perDoseSell = sellOut / (sellingAs.Contains("3") ? 3.0 : 4.0);
            double expectedDoses = 3.0 + FourDoseChance;
            double revenue = (perDoseSell * expectedDoses) * 0.98; // apply 2% GE tax

            double chemAvg = chemP != null ? Avg(chemP.High, chemP.Low) : 0.0;
            double chargeCostPerProc = chemAvg > 0 ? chemAvg / ChargesPerChemAmulet : 0.0;
            double expectedChargeCost = FourDoseChance * chargeCostPerProc;

            double expectedProfit = revenue - (buyBase + buySec + expectedChargeCost)
                                    + (SecondarySaveChance * buySec);
            double gpPerXpEV = expectedProfit / xp;

            Console.WriteLine();
            Console.WriteLine($"GE offer helper (mode: {Mode}) (gp / xp = {gpPerXpEV:N2})");
            Console.WriteLine($"Buy base:       {buyBase:N0}");
            Console.WriteLine($"Buy secondary:  {buySec:N0}");
            Console.WriteLine($"Sell {sellingAs}: {sellOut:N0}");
            Console.WriteLine();
        }

        // ---------- Mode-aware suggestors ----------
        private static double SuggestBuy(PriceData p)
        {
            double high = p.High, low = p.Low;
            double mid = (high + low) / 2.0;
            double spread = Math.Max(0, high - low);

            if (Mode == FlowMode.Flow) return Math.Max(low, high - 1); // near-ask (instant)
            if (Mode == FlowMode.Fast)
            {
                double aggressive = low + 0.45 * spread + 2;
                if (spread <= 10 || (mid > 0 && spread <= 0.003 * mid)) return low + 2;
                return aggressive;
            }

            // Normal (adaptive)
            bool tight = spread <= TightSpreadAbs || (mid > 0 && spread <= TightSpreadPctOfMid * mid);
            if (tight) return low + 1;

            double pct = BasePctIntoSpread;
            double spreadPct = (mid > 0) ? (spread / mid) : 0.0;
            double ageMin = Math.Max(0, (DateTime.Now - p.LastUpdate).TotalMinutes);

            if (spreadPct >= WideSpreadPct) pct += 0.10;
            if (ageMin >= StaleMinutes) pct += 0.10;
            pct = Math.Min(pct, MaxPctIntoSpread);

            return low + pct * spread + 1; // small overbid to beat the floor
        }

        private static double SuggestSell(PriceData p)
        {
            double high = p.High, low = p.Low;
            double mid = (high + low) / 2.0;
            double spread = Math.Max(0, high - low);

            if (Mode == FlowMode.Flow) return Math.Min(high, low + 1); // near-bid (instant)
            if (Mode == FlowMode.Fast)
            {
                double aggressive = high - 0.45 * spread - 2;
                if (spread <= 10 || (mid > 0 && spread <= 0.003 * mid)) return high - 2;
                return aggressive;
            }

            // Normal (adaptive)
            bool tight = spread <= TightSpreadAbs || (mid > 0 && spread <= TightSpreadPctOfMid * mid);
            if (tight) return high - 1;

            double pct = BasePctIntoSpread;
            double spreadPct = (mid > 0) ? (spread / mid) : 0.0;
            double ageMin = Math.Max(0, (DateTime.Now - p.LastUpdate).TotalMinutes);

            if (spreadPct >= WideSpreadPct) pct += 0.10;
            if (ageMin >= StaleMinutes) pct += 0.10;
            pct = Math.Min(pct, MaxPctIntoSpread);

            return high - pct * spread - 1; // small undercut to beat the ask
        }

        // ---------- Helpers ----------
        private static string DecideSellingFormByAverage(PriceData out3P, PriceData out4P)
        {
            double perDose3Avg = Avg(out3P.High, out3P.Low) / 3.0;
            double perDose4Avg = Avg(out4P.High, out4P.Low) / 4.0;
            return (perDose3Avg >= perDose4Avg) ? "(3-dose)" : "(4-dose)";
        }

        private static double Avg(double a, double b) => (a + b) / 2.0;
    }
}
