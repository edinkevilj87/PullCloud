using System.Globalization;
using System.Text;

namespace PullCloud;

public static class Util
{
    /// <summary>Normalizuje naziv grada za poređenje (mala slova, bez dijakritike, tačaka, apostrofa).</summary>
    public static string Norm(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c == '.' || c == '\'' || c == '’' || c == ',') continue;
            sb.Append(c == '-' ? ' ' : c);
        }
        var r = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (r.StartsWith("saint ")) r = "st " + r[6..];
        if (r.StartsWith("ft ")) r = "fort " + r[3..];
        if (r.StartsWith("mt ")) r = "mount " + r[3..];
        return r;
    }

    public static string FormatSpan(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = t.Negate();
        var h = (int)t.TotalHours;
        return h > 0 ? $"{h}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }
}

public static class Tz
{
    private static readonly Dictionary<string, TimeZoneInfo> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>IANA zones that were renamed – current name → previous name.</summary>
    private static readonly Dictionary<string, string> Renamed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Europe/Kyiv"] = "Europe/Kiev",
        ["America/Nuuk"] = "America/Godthab",
        ["Asia/Yangon"] = "Asia/Rangoon",
        ["Asia/Kolkata"] = "Asia/Calcutta",
        ["Asia/Kathmandu"] = "Asia/Katmandu",
        ["Asia/Ho_Chi_Minh"] = "Asia/Saigon",
        ["Atlantic/Faroe"] = "Atlantic/Faeroe",
        ["Pacific/Pohnpei"] = "Pacific/Ponape",
        ["Pacific/Chuuk"] = "Pacific/Truk",
        ["Pacific/Kanton"] = "Pacific/Enderbury",
        ["America/Ciudad_Juarez"] = "America/Ojinaga",
    };

    /// <summary>Vraća TimeZoneInfo za IANA (npr. America/Chicago) ili Windows ID.</summary>
    public static TimeZoneInfo Get(string id)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(id, out var tz)) return tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win))
                    tz = TimeZoneInfo.FindSystemTimeZoneById(win!);
                else if (Renamed.TryGetValue(id, out var old))
                    tz = Get(old);   // older Windows/ICU data only knows the previous IANA name
                else throw;
            }
            Cache[id] = tz;
            return tz;
        }
    }

    public static bool TryGet(string id, out TimeZoneInfo? tz)
    {
        try { tz = Get(id); return true; } catch { tz = null; return false; }
    }

    /// <summary>
    /// Pretvara lokalno vrijeme u datoj zoni u apsolutni trenutak.
    /// Kod dvosmislenog sata (jesenje vraćanje sata) i nepostojećeg sata (proljetno pomjeranje)
    /// uzima se kasniji trenutak = sigurnija (konzervativna) varijanta za pull.
    /// </summary>
    public static (DateTimeOffset instant, string? warning) ToInstant(DateTime local, TimeZoneInfo tz)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(local))
        {
            var o1 = tz.GetUtcOffset(local.AddHours(-3));
            var o2 = tz.GetUtcOffset(local.AddHours(3));
            var off = o1 < o2 ? o1 : o2;
            return (new DateTimeOffset(local, off),
                "The entered time does not exist in this zone (spring-forward night) – the safer (later) interpretation was used.");
        }
        if (tz.IsAmbiguousTime(local))
        {
            var offs = tz.GetAmbiguousTimeOffsets(local);
            var off = offs.Min();
            return (new DateTimeOffset(local, off),
                "The entered time occurs twice (fall-back night) – the later, safer interpretation was used.");
        }
        return (new DateTimeOffset(local, tz.GetUtcOffset(local)), null);
    }

    public static string Offset(TimeSpan o) => (o < TimeSpan.Zero ? "UTC-" : "UTC+") + o.Duration().ToString(@"hh\:mm");

    /// <summary>Skraćenica za američke zone (EST/EDT, CST/CDT...).</summary>
    public static string UsAbbrev(TimeZoneInfo tz, DateTimeOffset instant)
    {
        var dst = tz.IsDaylightSavingTime(instant);
        var b = tz.BaseUtcOffset.TotalHours;
        var p = b switch { -4 => "A", -5 => "E", -6 => "C", -7 => "M", -8 => "P", -9 => "AK", -10 => "H", _ => "" };
        return p == "" ? Offset(tz.GetUtcOffset(instant)) : p + (dst ? "DT" : "ST");
    }

    public static string UsZoneName(string ianaId)
    {
        if (ianaId == "America/Phoenix") return "Mountain (Arizona – no DST)";
        if (ianaId == "Pacific/Honolulu") return "Hawaii (no DST)";
        if (!TryGet(ianaId, out var tz)) return ianaId;
        return tz!.BaseUtcOffset.TotalHours switch
        {
            -4 => "Atlantic",
            -5 => "Eastern",
            -6 => "Central",
            -7 => "Mountain",
            -8 => "Pacific",
            -9 => "Alaska",
            -10 => "Hawaii-Aleutian",
            _ => ianaId
        };
    }
}
