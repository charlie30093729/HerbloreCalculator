using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HerbloreCalculator.Models;
using HerbloreCalculator.Utils;

namespace HerbloreCalculator.Services
{
    public static class PriceFetcher
    {
        // Shared HttpClient for all requests (avoid creating a new one each time)
        private static readonly HttpClient client = new HttpClient();

        // OSRS Wiki prices API endpoint
        private const string PricesUrl = "https://prices.runescape.wiki/api/v1/osrs/latest";

        // Required by OSRS Wiki API rules to identify your app + contact info
        private const string UserAgent = "HerbloreCalculator/1.0 (contact: Discord bottleo)";

        // Static constructor — runs once when class is first used
        static PriceFetcher()
        {
            // Set required headers for the API
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            // Timeout after 15 seconds if API is unresponsive
            client.Timeout = TimeSpan.FromSeconds(15);
        }

        /// <summary>
        /// Fetches the latest item prices from the OSRS Wiki API.
        /// Includes basic retry/backoff logic for 429 (rate limit) and transient 5xx errors.
        /// </summary>
        public static async Task<Dictionary<int, PriceData>> GetLatestPricesAsync()
        {
            // Try up to 3 attempts if request fails
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using var resp = await client.GetAsync(PricesUrl);

                // If rate limited (429), wait longer for each retry
                if ((int)resp.StatusCode == 429)
                {
                    await Task.Delay(2000 * (attempt + 1));
                    continue;
                }

                // Throw if request failed (403, 500, etc.)
                resp.EnsureSuccessStatusCode();

                // Read and parse JSON response
                await using var stream = await resp.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                // Extract "data" object containing all item prices
                var data = json.RootElement.GetProperty("data");

                // Pre-size dictionary to avoid resizing during insert
                var prices = new Dictionary<int, PriceData>(capacity: 2048);

                // Loop over all items in the API response
                foreach (var prop in data.EnumerateObject())
                {
                    int id = int.Parse(prop.Name); // Item ID as integer

                    // High price (0 if missing)
                    double high = prop.Value.TryGetProperty("high", out var highEl) && highEl.ValueKind == JsonValueKind.Number
                        ? highEl.GetDouble() : 0;

                    // Low price (0 if missing)
                    double low = prop.Value.TryGetProperty("low", out var lowEl) && lowEl.ValueKind == JsonValueKind.Number
                        ? lowEl.GetDouble() : 0;

                    // Last update time — prefer highTime, fallback to lowTime
                    long ts = prop.Value.TryGetProperty("highTime", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number
                        ? tsEl.GetInt64()
                        : (prop.Value.TryGetProperty("lowTime", out var tsEl2) && tsEl2.ValueKind == JsonValueKind.Number
                            ? tsEl2.GetInt64()
                            : 0L);

                    // Store in dictionary with formatted DateTime
                    prices[id] = new PriceData
                    {
                        High = high,
                        Low = low,
                        LastUpdate = ts > 0 ? TimeHelper.UnixTimeToLocal(ts) : DateTime.Now
                    };
                }

                // Return all parsed prices
                return prices;
            }

            // If all retries fail, throw error
            throw new HttpRequestException("Failed to fetch prices after retries.");
        }
    }
}
