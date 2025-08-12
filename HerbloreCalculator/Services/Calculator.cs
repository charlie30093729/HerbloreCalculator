using System;
using System.Collections.Generic;
using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;

namespace HerbloreCalculator.Services
{
    public static class Calculator
    {
        // These mechanics are fixed per your spec
        private const double FourDoseChance = 0.15;        // 15% chance craft yields an extra dose (effectively 4-dose)
        private const double SecondarySaveChance = 0.10;   // 10% chance to save secondary
        private const double ChargesPerChemAmulet = 10.0;  // 10 charges per Amulet of chemistry
        private const double PotionsPerHour = 2600.0;      // Throughput for XP/hr & GP/hr

        /// <summary>
        /// Renders a full report for a potion using three price scenarios.
        /// </summary>
        public static void DisplayPotionReport(
            Potion potion,
            Dictionary<int, PriceData> prices,
            double remainingXp,
            PriceData chemAmmyPrice
        )
        {
            // Guard against missing price data (rare but happens during API hiccups)
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

            // Worst = buy inputs at high; sell outputs at low (impatient insta-buy/sell)
            DisplayScenario(
                "Worst",
                basePrice.High,
                secondaryPrice.High,
                output3Price.Low,
                output4Price.Low,
                potion.Xp,
                remainingXp,
                chemAmmyPrice?.High ?? 0
            );

            // Average = midpoint of high/low on both sides
            DisplayScenario(
                "Average",
                Avg(basePrice.High, basePrice.Low),
                Avg(secondaryPrice.High, secondaryPrice.Low),
                Avg(output3Price.High, output3Price.Low),
                Avg(output4Price.High, output4Price.Low),
                potion.Xp,
                remainingXp,
                Avg(chemAmmyPrice?.High ?? 0, chemAmmyPrice?.Low ?? 0)
            );

            // Best = buy inputs at low; sell outputs at high (patient flipping / tight margins)
            DisplayScenario(
                "Best",
                basePrice.Low,
                secondaryPrice.Low,
                output3Price.High,
                output4Price.High,
                potion.Xp,
                remainingXp,
                chemAmmyPrice?.Low ?? 0
            );

            Console.WriteLine($"Last updated: {output3Price.LastUpdate:dd MMM yyyy HH:mm:ss}");
        }

        /// <summary>
        /// One scenario row with automatic “sell-as” decision.
        /// EV math notes:
        /// - Expected doses per craft = 3 + 0.15 (chance of extra dose) = 3.15
        /// - Auto-decant: sell in whichever size has higher gp/dose (no wasted value)
        /// - Add expected amulet charge cost: 0.15 * (chemAmmyPrice / 10)
        /// - Add expected secondary saving: + 0.10 * secondaryCost
        /// </summary>
        private static void DisplayScenario(
            string label,
            double baseCost, double secondaryCost,
            double output3, double output4,
            double xp, double remainingXp,
            double chemAmmyBuyPrice
        )
        {
            // Compare per-dose value: which sells better right now?
            double perDose3 = output3 / 3.0;
            double perDose4 = output4 / 4.0;
            double bestPerDose = Math.Max(perDose3, perDose4);
            string sellingAs = (bestPerDose == perDose3) ? "(3-dose)" : "(4-dose)";

            // Expected total doses from one craft (3 base + 15% chance of a bonus dose)
            double expectedDoses = 3.0 + FourDoseChance;

            // Revenue = sell all produced doses at the better gp/dose
            double revenue = bestPerDose * expectedDoses;

            // Expected amulet charge cost per craft:
            // proc consumes 1 charge; chem amulet provides 10 charges; we only pay on proc
            double chargeCostPerProc = chemAmmyBuyPrice > 0 ? chemAmmyBuyPrice / ChargesPerChemAmulet : 0.0;
            double expectedChargeCost = FourDoseChance * chargeCostPerProc;

            // Expected profit = revenue - inputs - expected charge cost + expected secondary saving
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

            Console.Write($"  |  {sellingAs}");

            Console.WriteLine();
        }

        private static double Avg(double a, double b) => (a + b) / 2.0;
    }
}
