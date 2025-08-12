using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HerbloreCalculator.Services
{
    public static class HiscoreFetcher
    {
        private static readonly HttpClient client = new HttpClient();
        private const string UA = "HerbloreCalculator/1.0 (contact: Discord bottleo)";

        // CSV endpoint (stable):
        // https://secure.runescape.com/m=hiscore_oldschool/index_lite.ws?player=<name>
        // Each line: rank,level,xp  (Overall first, then skills in fixed order)
        // Herblore line index = 16 (0-based): Overall(0), Attack(1), ..., Mining(15), Herblore(16)
        private const int HerbloreLineIndex = 16;

        static HiscoreFetcher()
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UA);
            client.Timeout = TimeSpan.FromSeconds(12);
        }

        public static async Task<long?> GetHerbloreXpAsync(string rsn)
        {
            if (string.IsNullOrWhiteSpace(rsn)) return null;

            var url = "https://secure.runescape.com/m=hiscore_oldschool/index_lite.ws?player="
                      + Uri.EscapeDataString(rsn.Trim());

            try
            {
                using var resp = await client.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;

                var body = await resp.Content.ReadAsStringAsync();
                // Guard: sometimes Windows \r\n
                var lines = body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length <= HerbloreLineIndex) return null;

                var parts = lines[HerbloreLineIndex].Split(',');
                if (parts.Length < 3) return null;

                // parts[2] = xp
                if (long.TryParse(parts[2], out var xp)) return xp;

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
