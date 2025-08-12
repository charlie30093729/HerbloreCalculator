using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HerbloreCalculator.Models;
using HerbloreCalculator.Services;

namespace HerbloreCalculator
{
    class Program
    {
        private const string Rsn = "bottleo";  // your RSN here
        private const double TargetXp = 200_000_000;
        private const int RefreshSeconds = 60;

        private const int ChemAmuletId = 21163; // Amulet of chemistry

        static async Task Main(string[] args)
        {
            var potions = new List<Potion>
                {
                    new Potion("Saradomin brew(3)", 3002, 6693, 6687, 6685, 180.0),
                    new Potion("Super restore(3)", 3004, 223, 3026, 3024, 142.5)
                };


            while (true)
            {
                Console.Clear();
                Console.WriteLine($"OSRS Herblore Profit Calculator - {DateTime.Now}");
                Console.WriteLine();

                double remainingXp = TargetXp;
                var currentXp = await HiscoreFetcher.GetHerbloreXpAsync(Rsn);
                if (currentXp.HasValue)
                {
                    remainingXp = Math.Max(0, TargetXp - currentXp.Value);
                    Console.WriteLine($"{Rsn} current Herblore XP: {currentXp:N0}, remaining: {remainingXp:N0}");
                }
                else
                {
                    Console.WriteLine($"Could not fetch XP for {Rsn}, assuming target from 0 XP.");
                }

                var prices = await PriceFetcher.GetLatestPricesAsync();

                prices.TryGetValue(ChemAmuletId, out var chemAmmyPrice);

                Console.WriteLine();
                foreach (var potion in potions)
                {
                    Calculator.DisplayPotionReport(
                        potion,
                        prices,
                        remainingXp > 0 ? remainingXp : TargetXp,
                        chemAmmyPrice
                    );
                    Console.WriteLine();
                }

                Console.WriteLine($"Next update in {RefreshSeconds} seconds...");
                await Task.Delay(RefreshSeconds * 1000);
            }
        }
    }
}
