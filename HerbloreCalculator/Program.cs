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
            InitConsole(); // <- set window/buffer + initial clear

            // Potions driven by their item IDs and XP each
            var potions = new List<Potion>
            {
                new Potion("Saradomin brew(3)", 3002, 6693, 6687, 6685, 180.0),
                new Potion("Super restore(3)",  3004,  223, 3026, 3024, 142.5)
            };

            while (true)
            {
                ClearScreen(); // <- robust clear (screen + scrollback)

                Console.WriteLine($"Bottleos Herb Calculator - {DateTime.Now}");
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

                // Worst = buy inputs at high; sell outputs at low (impatient insta-buy/sell)
                // Average = midpoint of high/low on both sides
                // Best = buy inputs at low; sell outputs at high (patient flipping / tight margins)
                Console.WriteLine(" Worst = Buy high, sell low (impatient) \n Average = Average.... \n Best = Buy inputs low, sell high. \n");

                // 2) Pull all latest prices in a single API call (OSRS Wiki)
                var prices = await PriceFetcher.GetLatestPricesAsync();

                // 3) Print tracked items (simple dashboard at the top)
                // (You moved the tracker to the bottom—leaving this placeholder to match your structure)

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

        // --- Helpers to keep the console tidy ---

        private static void InitConsole()
        {
            try
            {
                // Use max possible height so you don't have to drag it every run
                int targetWidth = Math.Min(140, Console.LargestWindowWidth);
                int targetHeight = Console.LargestWindowHeight; // max height allowed

                // Set window size first, then buffer
                Console.SetWindowSize(targetWidth, targetHeight);
                Console.SetBufferSize(targetWidth, targetHeight);
            }
            catch
            {
                // Ignore if console host doesn't allow resizing
            }

            ClearScreen();
        }


        private static void ClearScreen()
        {
            try
            {
                // ANSI: clear screen, clear scrollback, move cursor to home
                Console.Write("\x1b[2J\x1b[3J\x1b[H");
            }
            catch
            {
                // Fallback if ANSI not supported
                Console.Clear();
            }
        }
    }
}
