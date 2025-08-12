using System;
using System.Collections.Generic;
using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;

namespace HerbloreCalculator.Services
{
    public static class Calculator
    {
        private const double FourDoseChance = 0.15;        // 15% chance for 4-dose proc
        private const double SecondarySaveChance = 0.10;   // 10% chance to save secondary
        private const double ChargesPerChemAmulet = 10.0;  // charges per amulet

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
            Console.WriteLine($"XP per potion: {potion.Xp}");

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

        private static void DisplayScenario(
            string label,
            double baseCost, double secondaryCost,
            double output3, double output4,
            double xp, double remainingXp,
            double chemAmmyBuyPrice
        )
        {
            double perDose3 = output3 / 3.0;
            double perDose4 = output4 / 4.0;
            double bestPerDose = Math.Max(perDose3, perDose4);
            string sellingAs = (bestPerDose == perDose3) ? "(3-dose)" : "(4-dose)";

            double expectedDoses = 3.0 + FourDoseChance;
            double revenue = bestPerDose * expectedDoses;

            double chargeCostPerProc = chemAmmyBuyPrice > 0 ? chemAmmyBuyPrice / ChargesPerChemAmulet : 0.0;
            double expectedChargeCost = FourDoseChance * chargeCostPerProc;

            double expectedProfit = revenue - (baseCost + secondaryCost + expectedChargeCost)
                                    + (SecondarySaveChance * secondaryCost);

            double gpPerXp = expectedProfit / xp;
            double totalGp = (remainingXp / xp) * expectedProfit;

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

            Console.Write($"  |  Selling as {sellingAs}");
            Console.WriteLine();
        }

        private static double Avg(double a, double b) => (a + b) / 2.0;
    }
}
