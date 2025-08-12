using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HerbloreCalculator.Models;
using HerbloreCalculator.Services;

namespace HerbloreCalculator
{
    class Program
    {
        // Your RSN used to fetch current Herblore XP from hiscores.
        private const string Rsn = "bottleo";

        // Change this if you want to aim for 99, 200m, etc.
        private const double TargetXp = 200_000_000;

        // API is fine with ~1 req/min.
        private const int RefreshSeconds = 60;

        // Items we fetch alongside potions
        private const int ChemAmuletId = 21163; // Amulet of chemistry (for proc charges)

        // Quick “watch list” that prints current prices each refresh
        private static readonly List<(int Id, string Name)> TrackedItems = new()
        {
            (24777, "Blood shard"),
            // Add anything else: (12934, "Zulrah's scales"), ...
        };

        static async Task Main(string[] args)
        {
            // Potions driven by their item IDs and XP each
            var potions = new List<Potion>
            {
                new Potion("Saradomin brew(3)", 3002, 6693, 6687, 6685, 180.0),
                new Potion("Super restore(3)",  3004,  223, 3026, 3024, 142.5)
            };

            while (true)
            {
                Console.Clear();
                Console.WriteLine($"Bottleos Herb Calc V1.3 - {DateTime.Now}");
                Console.WriteLine();

                // 1) Pull your current Herblore XP (falls back to 0 if unranked)
                var currentXp = await HiscoreFetcher.GetHerbloreXpAsync(Rsn);
                double remainingXp = TargetXp;
                if (currentXp.HasValue)
                {
                    remainingXp = Math.Max(0, TargetXp - currentXp.Value);
                    Console.WriteLine($"{Rsn} current Herblore XP: {currentXp:N0}, remaining: {remainingXp:N0}\n");
                }
                else
                {
                    Console.WriteLine($"Could not fetch XP for {Rsn}, assuming target from 0 XP.");
                }
                
                // 2) Pull all latest prices in a single API call (OSRS Wiki)
                var prices = await PriceFetcher.GetLatestPricesAsync();

                // 3) Print tracked items (simple dashboard at the top)


                // Chemistry ammy price used for proc charge cost (passed to calculator)
                prices.TryGetValue(ChemAmuletId, out var chemAmmyPrice);

                // 4) Show each potion’s report (worst/avg/best)
                foreach (var potion in potions)
                {
                    Calculator.DisplayPotionReport(
                        potion,
                        prices,
                        remainingXp > 0 ? remainingXp : TargetXp, // If we couldn’t fetch XP, use full target
                        chemAmmyPrice
                    );
                    Console.WriteLine();
                }

                Console.WriteLine();
                Console.WriteLine("Item tracker.... \n");
                foreach (var (Id, Name) in TrackedItems)
                {
                    if (prices.TryGetValue(Id, out var p))
                        Console.WriteLine($"{Name}: High {p.High:N0} gp | Low {p.Low:N0} gp");
                    else
                        Console.WriteLine($"{Name}: Price not found.");
                }
                Console.WriteLine();

                Console.WriteLine($"Next update in {RefreshSeconds} seconds...");
                await Task.Delay(RefreshSeconds * 1000);
            }
        }
    }
}
