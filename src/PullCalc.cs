namespace PullCloud;

public enum PullStatus { PullAvailable, Wait, NotYet }

public sealed record ZoneCalc(string TzId, TimeZoneInfo Tz, DateTimeOffset Created, DateTimeOffset PullAt, string? Warning);

public sealed record CalcResult(
    PullStatus Status,
    ZoneCalc Governing,
    List<ZoneCalc> All,
    TimeSpan Elapsed,
    TimeSpan Remaining,
    bool CreatedInFuture);

public static class PullCalc
{
    public static readonly TimeSpan PullDelay = TimeSpan.FromHours(6);

    /// <summary>
    /// createdLocal = "Incident created on" kako piše u USA sistemu (lokalno vrijeme te države/grada).
    /// Ako je moguće više zona (grad nije poznat), računa za svaku i uzima NAJKASNIJI pull = sigurno.
    /// </summary>
    public static CalcResult Compute(DateTime createdLocal, IEnumerable<string> tzIds, DateTimeOffset now, int waitThresholdMinutes)
    {
        var all = new List<ZoneCalc>();
        foreach (var id in tzIds.Distinct())
        {
            var tz = Tz.Get(id);
            var (instant, warn) = Tz.ToInstant(createdLocal, tz);
            all.Add(new ZoneCalc(id, tz, instant, instant + PullDelay, warn));
        }
        var gov = all.OrderByDescending(z => z.PullAt).First();
        var elapsed = now - gov.Created;
        var remaining = gov.PullAt - now;

        PullStatus st;
        if (remaining <= TimeSpan.Zero) st = PullStatus.PullAvailable;
        else if (remaining <= TimeSpan.FromMinutes(waitThresholdMinutes)) st = PullStatus.Wait;
        else st = PullStatus.NotYet;

        return new CalcResult(st, gov, all, elapsed, remaining, elapsed < TimeSpan.Zero);
    }
}
