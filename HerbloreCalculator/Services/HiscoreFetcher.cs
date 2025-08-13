using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HerbloreCalculator.Services
{
    public static class HiscoreFetcher
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        private const string UA = "HerbloreCalculator/1.0 (contact: Discord bottleo)";
        private const string Endpoint = "https://api.wiseoldman.net/v2/players/";

        static HiscoreFetcher()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(UA);
            Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        // Back-compat for existing calls
        public static Task<long?> GetHerbloreXpAsync(string rsn) => GetSkillXpAsync(rsn, "herblore");

        /// <summary>
        /// Fetch XP for any skill from Wise Old Man:
        /// latestSnapshot.data.skills.{skill}.experience
        /// Skill name is case/space/underscore/hyphen-insensitive (e.g., "hit points", "RC", "herb").
        /// </summary>
        public static async Task<long?> GetSkillXpAsync(string rsn, string skill)
        {
            if (string.IsNullOrWhiteSpace(rsn) || string.IsNullOrWhiteSpace(skill)) return null;

            var key = Normalize(skill);
            if (key is null) return null;

            try
            {
                using var resp = await Client.GetAsync(Endpoint + Uri.EscapeDataString(rsn.Trim()));
                if (!resp.IsSuccessStatusCode) return null;

                await using var s = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(s);

                if (!doc.RootElement.TryGetProperty("latestSnapshot", out var snap)) return null;
                if (!snap.TryGetProperty("data", out var data)) return null;
                if (!data.TryGetProperty("skills", out var skills)) return null;
                if (!skills.TryGetProperty(key, out var skillObj)) return null;
                if (!skillObj.TryGetProperty("experience", out var exp)) return null;

                if (exp.ValueKind == JsonValueKind.Number && exp.TryGetInt64(out var xp)) return xp;
                if (exp.ValueKind == JsonValueKind.Number) return (long)exp.GetDouble();
                return null;
            }
            catch
            {
                return null;
            }
        }

        // Canonical WOM keys with common aliases.
        private static string? Normalize(string raw)
        {
            var s = raw.Trim().ToLowerInvariant()
                       .Replace(" ", "").Replace("_", "").Replace("-", "");

            return s switch
            {
                "overall" => "overall",
                "attack" => "attack",
                "defence" or "defense" => "defence",
                "strength" => "strength",
                "hitpoints" or "hp" => "hitpoints",
                "ranged" or "range" => "ranged",
                "prayer" => "prayer",
                "magic" => "magic",
                "cooking" => "cooking",
                "woodcutting" or "wc" => "woodcutting",
                "fletching" => "fletching",
                "fishing" => "fishing",
                "firemaking" or "fm" => "firemaking",
                "crafting" => "crafting",
                "smithing" => "smithing",
                "mining" => "mining",
                "herblore" or "herb" => "herblore",
                "agility" => "agility",
                "thieving" => "thieving",
                "slayer" => "slayer",
                "farming" => "farming",
                "runecrafting" or "runecraft" or "rc" => "runecrafting",
                "hunter" => "hunter",
                "construction" or "con" => "construction",
                _ => null
            };
        }
    }
}
