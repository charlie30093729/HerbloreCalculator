using System;
using System.Collections.Generic;
using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;

namespace HerbloreCalculator.Services
{
    public static class Calculator
    {
        // ----- Fixed mechanics (your spec) -----
        private const double FourDoseChance = 0.15;        // 15% chance for +1 dose (4-dose proc)
        private const double SecondarySaveChance = 0.10;   // 10% chance to save secondary
        private const double ChargesPerChemAmulet = 10.0;  // 10 charges per chemistry amulet
        private const double PotionsPerHour = 2600.0;      // throughput for XP/hr & GP/hr

        // ----- Offer suggestion tuning -----
        // If the market spread is "wide", we'll place offers this % into the spread from the better side.
        // e.g., Buy = low + 20% * (high - low); Sell = high - 20% * (high - low).
        // Tight markets (small spread) default to low (buy) / high (sell).
        private const double OfferPctIntoSpread = 0.20;
        private const double TightSpreadAbs = 50.0;        // <= 50 gp => treat as tight
        private const double TightSpreadPctOfMid = 0.01;   // <= 1% of mid price => treat as tight

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

            // ----- GE offer helper (suggested offers) -----
            // Decide which form sells better *on average* right now (per-dose).
            string sellingAs = DecideSellingFormByAverage(output3Price, output4Price);
            DisplayOfferGuide(sellingAs, basePrice, secondaryPrice, output3Price, output4Price);

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

            // Expected total doses from one craft (3 base + 15% EV)
            double expectedDoses = 3.0 + FourDoseChance;

            // Revenue if we always sell in the better form
            double revenue = bestPerDose * expectedDoses;

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

        // ---------- GE offer helper (suggested prices) ----------

        /// <summary>
        /// Suggests actionable GE prices to type based on current spread,
        /// instead of just printing raw averages.
        /// - Tight markets (small spread): buy at Low, sell at High.
        /// - Wide markets: buy = Low + p% of spread, sell = High - p% of spread.
        /// </summary>
        private static void DisplayOfferGuide(
            string sellingAs, PriceData baseP, PriceData secP, PriceData out3P, PriceData out4P)
        {
            Console.WriteLine();
            Console.WriteLine("GE offer helper (suggested offers):");

            // Buy suggestions for inputs (base + secondary)
            double buyBase = SuggestBuy(baseP.High, baseP.Low);
            double buySec = SuggestBuy(secP.High, secP.Low);

            // Sell suggestion based on chosen output form
            double sellPrice = sellingAs.Contains("3")
                ? SuggestSell(out3P.High, out3P.Low)
                : SuggestSell(out4P.High, out4P.Low);

            Console.WriteLine($"Buy base:       {buyBase:N0}");
            Console.WriteLine($"Buy secondary:  {buySec:N0}");
            Console.WriteLine($"Sell {sellingAs}: {sellPrice:N0}");
            Console.WriteLine();
        }

        /// <summary>
        /// Suggest a buy price: if spread is tight, use Low; else move OfferPctIntoSpread into the spread.
        /// </summary>
        private static double SuggestBuy(double high, double low)
        {
            double mid = (high + low) / 2.0;
            double spread = Math.Max(0, high - low);
            bool tight = spread <= TightSpreadAbs || (mid > 0 && spread <= TightSpreadPctOfMid * mid);

            if (tight) return low; // tight market → low is fine (fills quickly)
            return low + OfferPctIntoSpread * spread;
        }

        /// <summary>
        /// Suggest a sell price: if spread is tight, use High; else move OfferPctIntoSpread into the spread (from the top).
        /// </summary>
        private static double SuggestSell(double high, double low)
        {
            double mid = (high + low) / 2.0;
            double spread = Math.Max(0, high - low);
            bool tight = spread <= TightSpreadAbs || (mid > 0 && spread <= TightSpreadPctOfMid * mid);

            if (tight) return high; // tight market → high is fine (fills quickly)
            return high - OfferPctIntoSpread * spread;
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
