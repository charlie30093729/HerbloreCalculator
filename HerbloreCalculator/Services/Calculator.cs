using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;
using System;
using System.Collections.Generic;

namespace HerbloreCalculator.Services
{
    public static class Calculator
    {
        private const double FourDoseChance = 0.15;
        private const double SecondarySaveChance = 0.10;
        private const double TargetXp = 200_000_000;

        public static void DisplayPotionReport(Potion potion, Dictionary<int, PriceData> prices)
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

            DisplayScenario("Worst",
                basePrice.High, secondaryPrice.High,
                output3Price.Low, output4Price.Low,
                potion.Xp);

            DisplayScenario("Average",
                Avg(basePrice.High, basePrice.Low), Avg(secondaryPrice.High, secondaryPrice.Low),
                Avg(output3Price.High, output3Price.Low), Avg(output4Price.High, output4Price.Low),
                potion.Xp);

            DisplayScenario("Best",
                basePrice.Low, secondaryPrice.Low,
                output3Price.High, output4Price.High,
                potion.Xp);

            Console.WriteLine($"Last updated: {output3Price.LastUpdate:dd MMM yyyy HH:mm:ss}");
        }

        private static void DisplayScenario(string label, double baseCost, double secondaryCost, double output3, double output4, double xp)
        {
            double totalCost = baseCost + secondaryCost;
            double baseProfit = output3 - totalCost;
            double bonusHigh = (output4 - output3) * FourDoseChance;
            double bonusSave = secondaryCost * SecondarySaveChance;
            double expectedProfit = baseProfit + bonusHigh + bonusSave;
            double gpPerXp = expectedProfit / xp;
            double totalGp = (TargetXp / xp) * expectedProfit;

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
            Console.Write($"{totalGp,15:N0} gp (200m XP)");
            Console.ResetColor();

            Console.WriteLine();
        }

        private static double Avg(double a, double b) => (a + b) / 2;
    }
}
