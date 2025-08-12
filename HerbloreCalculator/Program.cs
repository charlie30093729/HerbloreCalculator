using HerbloreCalculator.Models;
using HerbloreCalculator.Services;
using HerbloreCalculator.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HerbloreCalculator
{
    class Program
    {
        static readonly List<Potion> potions = new List<Potion>
        {
            new Potion
            {
                Name = "Saradomin brew(3)",
                Xp = 180,
                BaseId = 3002,    // Toadflax potion (unf)
                SecondaryId = 6693, // Crushed nest
                Output3Id = 6687, // Saradomin brew(3)
                Output4Id = 6685  // Saradomin brew(4)
            },
            new Potion
            {
                Name = "Super restore(3)",
                Xp = 142.5,
                BaseId = 3004,    // Snapdragon potion (unf)
                SecondaryId = 223,  // Red spiders' eggs
                Output3Id = 3026, // Super restore(3)
                Output4Id = 3024  // Super restore(4)
            }
        };

        static async Task Main()
        {
            while (true)
            {
                Console.Clear();
                try
                {
                    var prices = await PriceFetcher.GetLatestPricesAsync();

                    foreach (var potion in potions)
                    {
                        Calculator.DisplayPotionReport(potion, prices);
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching data: {ex.Message}");
                }

                await Task.Delay(60000); // 60s refresh
            }
        }
    }
}
