using System.Text.Json;

namespace PullCloud;

public sealed record GeoHit(string Name, string? Admin1, string? Country, string? CountryCode, string Timezone, long Population)
{
    public string Display => string.Join(", ", new[] { Name, Admin1, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public override string ToString() => $"{Display}  —  {Timezone}";
}

/// <summary>Online pretraga gradova (Open-Meteo geocoding, besplatno, bez API ključa). Vraća i IANA vremensku zonu.</summary>
public static class Geo
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PullCloud/1.0");
        return c;
    }

    public static async Task<List<GeoHit>> SearchAsync(string name, string? countryCode, CancellationToken ct = default)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(name.Trim()) +
                  "&count=20&language=en&format=json" + (countryCode != null ? "&countryCode=" + countryCode : "");
        using var resp = await Http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<GeoHit>();
        if (!doc.RootElement.TryGetProperty("results", out var results)) return list;
        foreach (var r in results.EnumerateArray())
        {
            string? S(string p) => r.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var tz = S("timezone");
            if (string.IsNullOrEmpty(tz)) continue;
            long pop = r.TryGetProperty("population", out var pv) && pv.ValueKind == JsonValueKind.Number ? pv.GetInt64() : 0;
            list.Add(new GeoHit(S("name") ?? name, S("admin1"), S("country"), S("country_code"), tz!, pop));
        }
        return list;
    }
}
