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
        private static readonly HttpClient client = new HttpClient();
        private const string PricesUrl = "https://prices.runescape.wiki/api/v1/osrs/latest";

        // Identify your tool + contact per OSRS Wiki API rules
        private const string UserAgent = "HerbloreCalculator/1.0 (contact: Discord bottleo)";

        static PriceFetcher()
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        }

        public static async Task<Dictionary<int, PriceData>> GetLatestPricesAsync()
        {
            // simple retry/backoff for 429/5xx
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using var resp = await client.GetAsync(PricesUrl);
                if ((int)resp.StatusCode == 429) // rate limited
                {
                    await Task.Delay(2000 * (attempt + 1));
                    continue;
                }

                resp.EnsureSuccessStatusCode(); // throws on 403/5xx

                await using var stream = await resp.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                var data = json.RootElement.GetProperty("data");
                var prices = new Dictionary<int, PriceData>(capacity: 2048);

                foreach (var prop in data.EnumerateObject())
                {
                    // Some items occasionally have null high/low; guard with TryGetProperty
                    int id = int.Parse(prop.Name);

                    double high = prop.Value.TryGetProperty("high", out var highEl) && highEl.ValueKind == JsonValueKind.Number
                        ? highEl.GetDouble() : 0;

                    double low = prop.Value.TryGetProperty("low", out var lowEl) && lowEl.ValueKind == JsonValueKind.Number
                        ? lowEl.GetDouble() : 0;

                    long ts = prop.Value.TryGetProperty("highTime", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number
                        ? tsEl.GetInt64()
                        : (prop.Value.TryGetProperty("lowTime", out var tsEl2) && tsEl2.ValueKind == JsonValueKind.Number
                            ? tsEl2.GetInt64()
                            : 0L);

                    prices[id] = new PriceData
                    {
                        High = high,
                        Low = low,
                        LastUpdate = ts > 0 ? TimeHelper.UnixTimeToLocal(ts) : DateTime.Now
                    };
                }

                return prices;
            }

            throw new HttpRequestException("Failed to fetch prices after retries.");
        }
    }
}
