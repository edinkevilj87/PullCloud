using System.Globalization;
using System.Text.RegularExpressions;

namespace PullCloud;

public enum PasteResult { Ok, Invalid, BadYear }

/// <summary>Reads a US date/time copied from the incident system, e.g. "11/26/2026 3:45 PM EST".</summary>
public static class PasteParser
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static PasteResult TryParse(string? text, out DateTime value)
    {
        value = default;
        var s = (text ?? "").Trim();
        if (s == "") return PasteResult.Invalid;
        s = Regex.Replace(s, @"\b(AKST|AKDT|EST|EDT|CST|CDT|MST|MDT|PST|PDT|HST|HDT|ET|CT|MT|PT)\b", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bat\b", " ", RegexOptions.IgnoreCase).Trim();
        // "11/26/2" or "11/26/20" would be read as 2002 / 2020 – require a full 4-digit year while typing.
        var slash = Regex.Match(s, @"^\s*\d{1,2}/\d{1,2}/(\d*)");
        if (slash.Success && slash.Groups[1].Value.Length != 4)
            return DateTime.TryParse(s, Us, DateTimeStyles.AllowWhiteSpaces, out _) || slash.Groups[1].Value.Length < 4
                ? PasteResult.BadYear : PasteResult.Invalid;
        if (!DateTime.TryParse(s, Us, DateTimeStyles.AllowWhiteSpaces, out var dt)) return PasteResult.Invalid;
        if (dt.Year < 2000 || dt.Year > 2100) return PasteResult.BadYear;
        value = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        return PasteResult.Ok;
    }
}
