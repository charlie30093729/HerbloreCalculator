using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HerbloreCalculator.Models;
using HerbloreCalculator.Services;

namespace HerbloreCalculator
{
    class Program
    {
        private const string Rsn = "bottleo";
        private const double TargetXp = 200_000_000;
        private const int RefreshSeconds = 60;

        private const int ChemAmuletId = 21163; // Amulet of chemistry
        private static readonly List<(int Id, string Name)> TrackedItems = new()
        {
            (24777, "Blood shard"),
        };

        static async Task Main(string[] args)
        {
            InitConsole();

            // Potions to show
            var potions = new List<Potion>
            {
                new Potion("Saradomin brew(3)", 3002, 6693, 6687, 6685, 180.0),
                new Potion("Super restore(3)",  3004,  223, 3026, 3024, 142.5)
            };

            while (true)
            {
                ClearScreen();

                Console.WriteLine($"Bottleos Herb Calc V1.3 - {DateTime.Now}");
                Console.WriteLine($"Mode: {Calculator.Mode}   (1 = Flow / 2 = Normal / 3 = Fast | R = Refresh now | Q = Quit)");
                Console.WriteLine();

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

                Console.WriteLine(" Worst = Buy high, sell low (impatient)");
                Console.WriteLine(" Average = Average....");
                Console.WriteLine(" Best = Buy inputs low, sell high.\n");

                var prices = await PriceFetcher.GetLatestPricesAsync();
                prices.TryGetValue(ChemAmuletId, out var chemAmmyPrice);

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

                // Wait with hotkeys
                Console.WriteLine($"Next update in {RefreshSeconds} seconds...  (press 1/2/3/R/Q)");
                if (!await WaitWithHotkeysAsync(TimeSpan.FromSeconds(RefreshSeconds)))
                    break; // Q pressed -> exit
            }
        }

        // ----- Hotkeys -----
        private static async Task<bool> WaitWithHotkeysAsync(TimeSpan duration)
        {
            var end = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < end)
            {
                // Poll every 100ms for a key
                for (int i = 0; i < 10; i++)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true).Key;
                        switch (key)
                        {
                            case ConsoleKey.D1:
                            case ConsoleKey.NumPad1:
                                Calculator.Mode = FlowMode.Flow;
                                return true; // trigger immediate refresh
                            case ConsoleKey.D2:
                            case ConsoleKey.NumPad2:
                                Calculator.Mode = FlowMode.Normal;
                                return true;
                            case ConsoleKey.D3:
                            case ConsoleKey.NumPad3:
                                Calculator.Mode = FlowMode.Fast;
                                return true;
                            case ConsoleKey.R:
                                return true; // manual refresh
                            case ConsoleKey.Q:
                                return false; // quit
                        }
                    }
                    await Task.Delay(100);
                }
            }
            return true; // time elapsed -> refresh
        }

        // ----- Console helpers -----
        private static void InitConsole()
        {
            try
            {
                int targetWidth = Math.Min(140, Console.LargestWindowWidth);
                int targetHeight = Console.LargestWindowHeight;
                Console.SetWindowSize(targetWidth, targetHeight);
                Console.SetBufferSize(targetWidth, targetHeight);
            }
            catch { /* ignore if host doesn't allow resizing */ }

            ClearScreen();
        }

        private static void ClearScreen()
        {
            try { Console.Write("\x1b[2J\x1b[3J\x1b[H"); }
            catch { Console.Clear(); }
        }
    }
}
