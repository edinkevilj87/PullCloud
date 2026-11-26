using System.Reflection;
using System.Text;

namespace PullCloud.Tests;

/// <summary>
/// Self-contained test runner (no NuGet needed).
/// Usage: dotnet run --project tests -- [screenshotFolder]
/// </summary>
internal static class Program
{
    private static int _pass, _fail;
    private static readonly List<string> Failures = new();

    private static void Check(bool ok, string name, string? detail = null)
    {
        if (ok) _pass++;
        else { _fail++; Failures.Add(name + (detail != null ? "  →  " + detail : "")); }
    }

    private static void Eq<T>(T expected, T actual, string name) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), name, $"expected '{expected}', got '{actual}'");

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var shots = args.Length > 0 ? args[0] : null;

        Run("Data integrity", DataIntegrity);
        Run("UTC offsets & DST", Offsets);
        Run("DST transition days", Transitions);
        Run("Gap / repeated hour", GapAndAmbiguous);
        Run("Status logic", StatusLogic);
        Run("Real scenarios", Scenarios);
        Run("City name matching", Cities);
        Run("Paste parser", Paste);
        Run("Online city lookup", OnlineLookup);
        Run("UI (main window)", () => UiTests(shots));

        Console.WriteLine();
        Console.WriteLine($"PASSED: {_pass}   FAILED: {_fail}");
        foreach (var f in Failures) Console.WriteLine("  ✗ " + f);
        return _fail == 0 ? 0 : 1;
    }

    private static void Run(string name, Action a)
    {
        var before = (_pass, _fail);
        try { a(); }
        catch (Exception ex) { Check(false, name + " threw", ex.ToString()); }
        Console.WriteLine($"{name,-24} {_pass - before._pass,4} passed {(_fail - before._fail == 0 ? "" : $", {_fail - before._fail} FAILED")}");
    }

    // ------------------------------------------------------------------ data

    private static void DataIntegrity()
    {
        Eq(52, UsStates.All.Count, "50 states + DC + PR");
        Eq(UsStates.All.Count, UsStates.All.Select(s => s.Abbr).Distinct().Count(), "state abbreviations unique");
        foreach (var s in UsStates.All)
            foreach (var z in s.AllTz)
                Check(Tz.TryGet(z, out _), $"zone resolves: {s.Abbr} {z}");
        foreach (var s in UsStates.All)
            foreach (var (city, z) in s.Cities)
                Check(s.AllTz.Contains(z), $"{s.Abbr} city '{city}' zone {z} belongs to state");
        Check(UsStates.Conflicts.Count == 0, "no city listed in two zones of one state", string.Join("; ", UsStates.Conflicts));
        foreach (var abbr in new[] { "IN", "TN", "FL", "KY", "TX", "MI", "KS", "NE", "ND", "SD", "OR", "ID", "NV", "AZ", "AK" })
            Check(UsStates.All.Single(s => s.Abbr == abbr).IsSplit, $"{abbr} flagged as multi-zone");
        foreach (var abbr in new[] { "GA", "CA", "NY", "IL", "OH", "WA", "HI", "CO" })
            Check(!UsStates.All.Single(s => s.Abbr == abbr).IsSplit, $"{abbr} single zone");
        Check(WorldCities.All.Count > 60, "world city list populated", WorldCities.All.Count.ToString());
        Check(WorldCities.All.Any(p => p.Name.StartsWith("Sarajevo") && p.TzId == "Europe/Sarajevo"), "Sarajevo in my-location list");
        foreach (var p in WorldCities.All) Check(Tz.TryGet(p.TzId, out _), $"world zone resolves: {p.Name}");
    }

    private static TimeSpan Off(string tz, int y, int m, int d, int h = 12) =>
        Tz.Get(tz).GetUtcOffset(new DateTime(y, m, d, h, 0, 0, DateTimeKind.Unspecified));

    private static void Offsets()
    {
        var table = new (string tz, int summer, int winter)[]
        {
            ("America/New_York", -4, -5), ("America/Chicago", -5, -6), ("America/Denver", -6, -7),
            ("America/Phoenix", -7, -7), ("America/Los_Angeles", -7, -8), ("America/Anchorage", -8, -9),
            ("America/Adak", -9, -10), ("Pacific/Honolulu", -10, -10), ("America/Indiana/Indianapolis", -4, -5),
            ("America/Kentucky/Louisville", -4, -5), ("America/Detroit", -4, -5), ("America/Menominee", -5, -6),
            ("America/Boise", -6, -7), ("America/Puerto_Rico", -4, -4), ("America/Indiana/Vevay", -4, -5),
            ("America/Indiana/Knox", -5, -6), ("America/Indiana/Tell_City", -5, -6), ("America/North_Dakota/Center", -5, -6),
            ("America/Kentucky/Monticello", -4, -5), ("Europe/Sarajevo", 2, 1), ("Europe/London", 1, 0),
        };
        foreach (var (tz, su, wi) in table)
        {
            Eq(TimeSpan.FromHours(su), Off(tz, 2026, 7, 1), $"{tz} July 2026");
            Eq(TimeSpan.FromHours(wi), Off(tz, 2026, 12, 1), $"{tz} Dec 2026");
            Eq(TimeSpan.FromHours(su), Off(tz, 2027, 7, 1), $"{tz} July 2027");
        }
        Eq(TimeSpan.FromHours(5.5), Off("Asia/Kolkata", 2026, 7, 1), "Kolkata +5:30");
    }

    private static void Transitions()
    {
        // US 2026: DST Mar 8 – Nov 1; EU 2026: Mar 29 – Oct 25. US 2027: Mar 14 – Nov 7; EU 2027: Mar 28 – Oct 31.
        Eq(TimeSpan.FromHours(-5), Off("America/New_York", 2026, 3, 8, 1), "NY before spring-forward 2026");
        Eq(TimeSpan.FromHours(-4), Off("America/New_York", 2026, 3, 8, 3), "NY after spring-forward 2026");
        Eq(TimeSpan.FromHours(-4), Off("America/New_York", 2026, 10, 31, 23), "NY before fall-back 2026");
        Eq(TimeSpan.FromHours(-5), Off("America/New_York", 2026, 11, 1, 3), "NY after fall-back 2026");
        Eq(TimeSpan.FromHours(-5), Off("America/Chicago", 2027, 3, 14, 3), "Chicago spring-forward 2027");
        Eq(TimeSpan.FromHours(-6), Off("America/Chicago", 2027, 11, 7, 3), "Chicago fall-back 2027");
        Eq(TimeSpan.FromHours(1), Off("Europe/Sarajevo", 2026, 3, 29, 1), "Sarajevo before EU spring 2026");
        Eq(TimeSpan.FromHours(2), Off("Europe/Sarajevo", 2026, 3, 29, 4), "Sarajevo after EU spring 2026");
        Eq(TimeSpan.FromHours(1), Off("Europe/Sarajevo", 2026, 10, 25, 4), "Sarajevo after EU fall 2026");
        Eq(TimeSpan.FromHours(2), Off("Europe/Sarajevo", 2027, 3, 28, 4), "Sarajevo EU spring 2027");

        // Hour difference New York -> Sarajevo across the year (the tricky "mismatch" weeks)
        int Diff(int m, int d) => (int)(Off("Europe/Sarajevo", 2026, m, d) - Off("America/New_York", 2026, m, d)).TotalHours;
        Eq(6, Diff(3, 1), "NY→Sarajevo +6h (early March)");
        Eq(5, Diff(3, 15), "NY→Sarajevo +5h (US DST, EU not yet)");
        Eq(6, Diff(4, 5), "NY→Sarajevo +6h (both DST)");
        Eq(5, Diff(10, 28), "NY→Sarajevo +5h (EU back, US not yet)");
        Eq(6, Diff(11, 3), "NY→Sarajevo +6h (both standard)");
    }

    private static void GapAndAmbiguous()
    {
        var ny = Tz.Get("America/New_York");
        var (gap, w1) = Tz.ToInstant(new DateTime(2026, 3, 8, 2, 30, 0), ny);
        Eq(new DateTime(2026, 3, 8, 7, 30, 0), gap.UtcDateTime, "non-existent 2:30 AM → later instant");
        Check(w1 != null, "warning for non-existent time");
        var (amb, w2) = Tz.ToInstant(new DateTime(2026, 11, 1, 1, 30, 0), ny);
        Eq(new DateTime(2026, 11, 1, 6, 30, 0), amb.UtcDateTime, "repeated 1:30 AM → later (EST) instant");
        Check(w2 != null, "warning for repeated time");
        var (_, w3) = Tz.ToInstant(new DateTime(2026, 11, 26, 15, 45, 0), ny);
        Check(w3 == null, "no warning for a normal time");
    }

    // ------------------------------------------------------------------ status

    private static void StatusLogic()
    {
        // Created 10:00 AM CST on Dec 1 2026 = 16:00 UTC → pull at 22:00 UTC
        var created = new DateTime(2026, 12, 1, 10, 0, 0);
        var chi = new[] { "America/Chicago" };
        DateTimeOffset At(int h, int m, int s = 0) => new(2026, 12, 1, h, m, s, TimeSpan.Zero);
        CalcResult C(DateTimeOffset now, int thr = 60) => PullCalc.Compute(created, chi, now, thr);

        Eq(new DateTime(2026, 12, 1, 22, 0, 0), C(At(12, 0)).Governing.PullAt.UtcDateTime, "pull time = created + 6h");
        Eq(PullStatus.PullAvailable, C(At(22, 0)).Status, "exactly 6h → PULL AVAILABLE");
        Eq(PullStatus.PullAvailable, C(At(23, 30)).Status, "7.5h → PULL AVAILABLE");
        Eq(PullStatus.Wait, C(At(21, 59, 59)).Status, "1 second left → WAIT");
        Eq(PullStatus.Wait, C(At(21, 0)).Status, "60 min left, threshold 60 → WAIT");
        Eq(PullStatus.NotYet, C(At(20, 59)).Status, "61 min left, threshold 60 → NOT YET");
        Eq(PullStatus.Wait, C(At(20, 30), 120).Status, "90 min left, threshold 120 → WAIT");
        Eq(PullStatus.NotYet, C(At(16, 5)).Status, "just created → NOT YET");
        Eq(TimeSpan.FromHours(3), C(At(19, 0)).Elapsed, "elapsed 3h");
        Eq(TimeSpan.FromHours(3), C(At(19, 0)).Remaining, "remaining 3h");
        var fut = C(At(15, 0));
        Check(fut.CreatedInFuture && fut.Status == PullStatus.NotYet, "creation in the future flagged");

        // Unknown city: latest pull wins
        var fl = PullCalc.Compute(created, new[] { "America/New_York", "America/Chicago" }, At(12, 0), 60);
        Eq("America/Chicago", fl.Governing.TzId, "FL without city → Central (later) governs");
        Eq(2, fl.All.Count, "FL without city → 2 candidates");
        var az = PullCalc.Compute(new DateTime(2026, 7, 1, 15, 0, 0), new[] { "America/Phoenix", "America/Denver" }, At(12, 0), 60);
        Eq("America/Phoenix", az.Governing.TzId, "AZ summer without city → Phoenix (later) governs");
        // Duplicate zones collapse
        Eq(1, PullCalc.Compute(created, new[] { "America/Chicago", "America/Chicago" }, At(12, 0), 60).All.Count, "duplicate zones collapse");
    }

    private static DateTimeOffset InTz(DateTimeOffset i, string tz) => TimeZoneInfo.ConvertTime(i, Tz.Get(tz));

    private static void Scenarios()
    {
        // Indianapolis 11/26/2026 3:45 PM EST → Sarajevo 21:45, pull Sarajevo Nov 27 03:45
        var r = PullCalc.Compute(new DateTime(2026, 11, 26, 15, 45, 0), new[] { "America/Indiana/Indianapolis" }, DateTimeOffset.UtcNow, 60);
        Eq(new DateTime(2026, 11, 26, 21, 45, 0), InTz(r.Governing.Created, "Europe/Sarajevo").DateTime, "IN 3:45 PM → 21:45 Sarajevo");
        Eq(new DateTime(2026, 11, 27, 3, 45, 0), InTz(r.Governing.PullAt, "Europe/Sarajevo").DateTime, "IN pull → 03:45 next day Sarajevo");

        // Pensacola (Central) summer 9:00 AM CDT → Sarajevo 16:00, pull 22:00
        r = PullCalc.Compute(new DateTime(2026, 7, 10, 9, 0, 0), new[] { "America/Chicago" }, DateTimeOffset.UtcNow, 60);
        Eq(new DateTime(2026, 7, 10, 22, 0, 0), InTz(r.Governing.PullAt, "Europe/Sarajevo").DateTime, "Pensacola summer pull 22:00 Sarajevo");

        // LA 11:30 PM PST → pull crosses midnight in both places
        r = PullCalc.Compute(new DateTime(2026, 12, 1, 23, 30, 0), new[] { "America/Los_Angeles" }, DateTimeOffset.UtcNow, 60);
        Eq(new DateTime(2026, 12, 2, 5, 30, 0), InTz(r.Governing.PullAt, "America/Los_Angeles").DateTime, "LA pull 05:30 next day local");
        Eq(new DateTime(2026, 12, 2, 14, 30, 0), InTz(r.Governing.PullAt, "Europe/Sarajevo").DateTime, "LA pull 14:30 Sarajevo");

        // Phoenix in summer (no DST): 3:00 PM MST = 22:00 UTC → Sarajevo 00:00 next day
        r = PullCalc.Compute(new DateTime(2026, 7, 1, 15, 0, 0), new[] { "America/Phoenix" }, DateTimeOffset.UtcNow, 60);
        Eq(new DateTime(2026, 7, 2, 0, 0, 0), InTz(r.Governing.Created, "Europe/Sarajevo").DateTime, "Phoenix summer → midnight Sarajevo");

        // Mismatch week: NY Mar 20 10:00 AM EDT → Sarajevo 15:00 (only 5h difference)
        r = PullCalc.Compute(new DateTime(2026, 3, 20, 10, 0, 0), new[] { "America/New_York" }, DateTimeOffset.UtcNow, 60);
        Eq(new DateTime(2026, 3, 20, 15, 0, 0), InTz(r.Governing.Created, "Europe/Sarajevo").DateTime, "NY Mar 20 → 15:00 Sarajevo");

        // Pull window spanning the US fall-back night: NY Oct 31 11:00 PM EDT (03:00Z) → pull 09:00Z = 4:00 AM EST
        r = PullCalc.Compute(new DateTime(2026, 10, 31, 23, 0, 0), new[] { "America/New_York" }, DateTimeOffset.UtcNow, 60);
        Eq(new DateTime(2026, 11, 1, 4, 0, 0), InTz(r.Governing.PullAt, "America/New_York").DateTime, "6 real hours across fall-back → 4:00 AM EST");
    }

    private static void Cities()
    {
        Eq(Util.Norm("Coeur d'Alene"), Util.Norm("coeur dalene"), "apostrophe ignored");
        Eq(Util.Norm("St. Petersburg"), Util.Norm("Saint Petersburg"), "St./Saint");
        Eq(Util.Norm("Ft. Wayne"), Util.Norm("Fort Wayne"), "Ft./Fort");
        Eq(Util.Norm("  EL   PASO "), Util.Norm("el paso"), "case and spaces");
        Eq(Util.Norm("Bihać"), Util.Norm("Bihac"), "diacritics");

        var cases = new (string st, string city, string tz)[]
        {
            ("FL", "Pensacola", "America/Chicago"), ("FL", "Panama City", "America/Chicago"), ("FL", "Destin", "America/Chicago"),
            ("FL", "Miami", "America/New_York"), ("FL", "Tallahassee", "America/New_York"), ("FL", "St. Petersburg", "America/New_York"),
            ("IN", "Gary", "America/Chicago"), ("IN", "Evansville", "America/Chicago"), ("IN", "Indianapolis", "America/Indiana/Indianapolis"),
            ("IN", "Ft. Wayne", "America/Indiana/Indianapolis"),
            ("TN", "Knoxville", "America/New_York"), ("TN", "Chattanooga", "America/New_York"), ("TN", "Nashville", "America/Chicago"),
            ("TN", "Memphis", "America/Chicago"),
            ("KY", "Louisville", "America/Kentucky/Louisville"), ("KY", "Bowling Green", "America/Chicago"), ("KY", "Paducah", "America/Chicago"),
            ("TX", "El Paso", "America/Denver"), ("TX", "Houston", "America/Chicago"),
            ("MI", "Iron Mountain", "America/Menominee"), ("MI", "Detroit", "America/Detroit"),
            ("AZ", "Window Rock", "America/Denver"), ("AZ", "Phoenix", "America/Phoenix"),
            ("ID", "Coeur d'Alene", "America/Los_Angeles"), ("ID", "Boise", "America/Boise"),
            ("OR", "Ontario", "America/Boise"), ("SD", "Rapid City", "America/Denver"), ("SD", "Sioux Falls", "America/Chicago"),
            ("ND", "Dickinson", "America/Denver"), ("NE", "Scottsbluff", "America/Denver"), ("KS", "Goodland", "America/Denver"),
            ("NV", "West Wendover", "America/Denver"), ("NV", "Las Vegas", "America/Los_Angeles"), ("AK", "Adak", "America/Adak"),
        };
        foreach (var (st, city, tz) in cases)
        {
            var s = UsStates.All.Single(x => x.Abbr == st);
            Eq(tz, s.Cities.TryGetValue(Util.Norm(city), out var z) ? z : "(not found)", $"{city}, {st}");
        }
    }

    private static void Paste()
    {
        void Ok(string input, DateTime expected)
        {
            var r = PasteParser.TryParse(input, out var dt);
            Check(r == PasteResult.Ok && dt == expected, $"paste '{input}'", $"{r} {dt:yyyy-MM-dd HH:mm}");
        }
        void Bad(string input, PasteResult expected)
        {
            var r = PasteParser.TryParse(input, out _);
            Eq(expected, r, $"paste '{input}' rejected");
        }
        var d = new DateTime(2026, 11, 26, 15, 45, 0);
        Ok("11/26/2026 3:45 PM", d);
        Ok("11/26/2026 03:45 PM EST", d);
        Ok("11/26/2026 3:45:00 PM", d);
        Ok("11/26/2026 15:45", d);
        Ok("  11/26/2026   3:45 pm  ", d);
        Ok("Nov 26, 2026 at 3:45 PM CST", d);
        Ok("November 26, 2026 3:45 PM", d);
        Ok("2026-11-26 15:45", d);
        Ok("11/26/2026 12:05 AM", new DateTime(2026, 11, 26, 0, 5, 0));
        Ok("11/26/2026 12:05 PM", new DateTime(2026, 11, 26, 12, 5, 0));
        Ok("11/26/2026", new DateTime(2026, 11, 26));
        Bad("11/26/202", PasteResult.BadYear);
        Bad("11/26/2", PasteResult.BadYear);
        Bad("11/26/20", PasteResult.BadYear);
        Bad("11/26/26 3:45 PM", PasteResult.BadYear);
        Bad("11/26/", PasteResult.BadYear);
        Bad("11/26/20261", PasteResult.Invalid);
        Bad("26/11/2026 15:45", PasteResult.Invalid);
        Bad("hello", PasteResult.Invalid);
        Bad("", PasteResult.Invalid);
        // typing character by character must never crash
        var full = "11/26/2026 3:45 PM EST";
        for (var i = 1; i <= full.Length; i++)
        {
            try { PasteParser.TryParse(full[..i], out _); _pass++; }
            catch (Exception ex) { Check(false, $"typing prefix '{full[..i]}'", ex.Message); }
        }
    }

    private static void OnlineLookup()
    {
        try
        {
            var vevay = Geo.SearchAsync("Vevay", "US").GetAwaiter().GetResult();
            Check(vevay.Any(h => h.Admin1 == "Indiana" && h.Timezone == "America/Indiana/Vevay"), "Vevay, IN online → America/Indiana/Vevay");
            Check(Tz.TryGet("America/Indiana/Vevay", out _), "Vevay zone usable");
            var tell = Geo.SearchAsync("Tell City", "US").GetAwaiter().GetResult();
            Check(tell.Any(h => h.Admin1 == "Indiana" && Tz.Get(h.Timezone).BaseUtcOffset == TimeSpan.FromHours(-6)), "Tell City, IN online → Central");
            var lb = Geo.SearchAsync("Lewisburg", "US").GetAwaiter().GetResult();
            Check(lb.Any(h => h.Admin1 == "Tennessee"), "Lewisburg, TN found online");
            var none = Geo.SearchAsync("Xqzzyplorf", "US").GetAwaiter().GetResult();
            Eq(0, none.Count, "nonsense city → no results");
        }
        catch (HttpRequestException ex) { Console.WriteLine("   (skipped – no internet: " + ex.Message + ")"); }
    }

    // ------------------------------------------------------------------ UI

    private static T F<T>(object o, string name) =>
        (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;

    private static object? M(object o, string name, params object[] a) =>
        o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, a);

    private static T P<T>(object o, string name) =>
        (T)o.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;

    private static void Pump(Task? t = null)
    {
        var until = DateTime.Now.AddSeconds(15);
        do { Application.DoEvents(); Thread.Sleep(20); } while (t != null && !t.IsCompleted && DateTime.Now < until);
    }

    private static void Shot(Form f, string? folder, string name)
    {
        if (folder == null) return;
        Pump();
        using var bmp = new Bitmap(f.Width, f.Height);
        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
        bmp.Save(Path.Combine(folder, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void UiTests(string? shots)
    {
        // Protect the user's real settings file.
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PullCloud", "settings.json");
        var backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
        try { UiTestsCore(shots, settingsPath); }
        finally
        {
            if (backup != null) File.WriteAllText(settingsPath, backup);
            else if (File.Exists(settingsPath)) File.Delete(settingsPath);
        }
    }

    private static void UiTestsCore(string? shots, string settingsPath)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var f = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false };
        f.Show();
        Pump();

        var cmbState = F<ComboBox>(f, "cmbState");
        var txtCity = F<TextBox>(f, "txtCity");
        var txtPaste = F<TextBox>(f, "txtPaste");
        var lblCity = F<Label>(f, "lblCity");
        var lblPaste = F<Label>(f, "lblPaste");
        var lblNote = F<Label>(f, "lblStateNote");
        var lblNotes = F<Label>(f, "lblNotes");
        var lblSub = F<Label>(f, "lblSub");
        var badge = F<StatusBadge>(f, "badge");
        var cmbMe = F<ComboBox>(f, "cmbMe");
        var btnSave = F<Button>(f, "btnMeSave");
        var numHour = F<NumericUpDown>(f, "numHour");
        var cmbAmPm = F<ComboBox>(f, "cmbAmPm");

        Check(f.Text == "Pull from the Cloud calculator", "window title");
        Check(cmbMe.SelectedItem != null, "my location preselected");

        void State(string abbr) { cmbState.SelectedItem = UsStates.All.Single(s => s.Abbr == abbr); Pump(); }
        void City(string c) { txtCity.Text = c; Pump((Task)M(f, "ResolveCityAsync")!); Pump(); }
        void CreatedAgo(TimeSpan ago, string tz)
        {
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow - ago, Tz.Get(tz)).DateTime;
            M(f, "SetPickers", local);
        }

        // Single-zone state
        State("GA"); City("");
        Check(!lblNote.Visible, "GA: no multi-zone warning");
        Check(!f.VerticalScroll.Visible, "default window size → no page scrollbar");
        CreatedAgo(TimeSpan.FromHours(7), "America/New_York");
        Eq("PULL AVAILABLE", badge.Text, "GA created 7h ago");
        CreatedAgo(TimeSpan.FromHours(5.5), "America/New_York");
        Eq("WAIT", badge.Text, "GA created 5.5h ago");
        Check(lblSub.Text.Contains("you can pull"), "WAIT message says when");
        Shot(f, shots, "1_wait");
        CreatedAgo(TimeSpan.FromHours(2), "America/New_York");
        Eq("NOT YET", badge.Text, "GA created 2h ago");
        CreatedAgo(TimeSpan.FromHours(-1), "America/New_York");
        Check(lblSub.Text.Contains("future"), "future creation warning shown");
        City("Atlanta");
        Check(lblCity.Text.Contains("Not needed"), "city not needed for single-zone state");

        // Multi-zone state without city → conservative + note
        State("FL"); City("");
        Check(lblNote.Visible && lblNote.Text.Contains("Florida"), "FL: multi-zone warning visible");
        Check(lblNotes.Text.Contains("City unknown"), "FL without city lists possible zones");
        // created 6.5h ago in Eastern → only 5.5h ago if Central → must NOT be available yet
        CreatedAgo(TimeSpan.FromHours(6.5), "America/New_York");
        Check(badge.Text != "PULL AVAILABLE", "FL no city: Eastern 6.5h ago is NOT available (Central could apply)", badge.Text);
        Shot(f, shots, "2_florida_no_city");

        City("Miami");
        Check(lblCity.Text.Contains("Eastern"), "Miami → Eastern", lblCity.Text);
        Eq("PULL AVAILABLE", badge.Text, "FL Miami 6.5h ago → available");
        City("Pensacola");
        Check(lblCity.Text.Contains("Central"), "Pensacola → Central", lblCity.Text);
        Check(badge.Text != "PULL AVAILABLE", "FL Pensacola: Eastern-6.5h is only 5.5h Central");
        Check(!lblNote.Visible, "multi-zone warning hidden once the city is known");
        Shot(f, shots, "3_pensacola");
        City("");
        Check(lblNote.Visible, "warning back when the city is cleared");

        // Small screen (e.g. 1080p laptop at 150% scaling): page must scroll, not cut off
        var oldSize = f.ClientSize;
        f.ClientSize = new Size(900, 600); Pump();
        Check(f.VerticalScroll.Visible, "small window → page scrolls");
        Check(!f.HorizontalScroll.Visible, "small window → no horizontal scroll");
        Shot(f, shots, "4_small_window");
        f.ClientSize = oldSize; Pump();

        // Online lookup through the UI
        State("IN"); City("Vevay");
        Check(lblCity.Text.Contains("Eastern") || lblCity.Text.Contains("failed"), "Vevay, IN via online lookup", lblCity.Text);
        City("Xqzzyplorf");
        Check(lblCity.Text.StartsWith("⚠"), "unknown city → warning, safe fallback", lblCity.Text);
        Check(lblNotes.Text.Contains("City unknown"), "unknown city → both zones considered");
        City("Gary");
        Check(lblCity.Text.Contains("Central"), "Gary, IN → Central");

        // Paste field: typing char by char never crashes, final value applied
        State("GA"); City("");
        var full = "11/26/2026 3:45 PM EST";
        for (var i = 1; i <= full.Length; i++)
        {
            txtPaste.Text = full[..i];
            try { M(f, "ParsePaste"); _pass++; }
            catch (Exception ex) { Check(false, $"UI paste prefix '{full[..i]}'", ex.InnerException?.Message ?? ex.Message); }
        }
        Eq(new DateTime(2026, 11, 26, 15, 45, 0), P<DateTime>(f, "CreatedLocal"), "pasted value applied to pickers");
        Check(lblPaste.Text.StartsWith("✓"), "paste confirmation shown");
        txtPaste.Text = "11/26/202"; M(f, "ParsePaste");
        Check(lblPaste.Text.Contains("year"), "partial year message");
        Eq(new DateTime(2026, 11, 26, 15, 45, 0), P<DateTime>(f, "CreatedLocal"), "bad paste leaves pickers unchanged");
        txtPaste.Text = "";

        // 12 AM / 12 PM handling
        M(f, "SetPickers", new DateTime(2026, 11, 26, 0, 15, 0));
        Check(numHour.Value == 12 && (string)cmbAmPm.SelectedItem! == "AM", "00:15 shows 12 AM");
        Eq(new DateTime(2026, 11, 26, 0, 15, 0), P<DateTime>(f, "CreatedLocal"), "12:15 AM round-trips");
        M(f, "SetPickers", new DateTime(2026, 11, 26, 12, 15, 0));
        Check(numHour.Value == 12 && (string)cmbAmPm.SelectedItem! == "PM", "12:15 shows 12 PM");
        Eq(new DateTime(2026, 11, 26, 12, 15, 0), P<DateTime>(f, "CreatedLocal"), "12:15 PM round-trips");

        // My location: change → not saved; Save → persisted
        var other = cmbMe.Items.Cast<WorldPlace>().First(p => p.Name.StartsWith("Tokyo"));
        cmbMe.SelectedItem = other; Pump();
        Eq("Save", btnSave.Text, "changed location shows Save");
        Check(F<Label>(f, "lblMe").Text.Contains("not saved"), "unsaved hint");
        Check(F<Label>(f, "lblMe").Text.Contains("UTC+09:00"), "Tokyo offset shown");
        btnSave.PerformClick(); Pump();
        Eq("Saved ✓", btnSave.Text, "Save button confirms");
        Check(File.ReadAllText(settingsPath).Contains("Asia/Tokyo"), "location written to settings file");

        // Copy text
        var copy = (string)M(f, "BuildCopyText")!;
        Check(copy.Contains("Pull available (my time)") && copy.Contains(badge.Text), "copy text contains result");

        // A fresh window picks up the saved location
        using (var f2 = new MainForm())
        {
            var me2 = F<ComboBox>(f2, "cmbMe");
            Check(me2.SelectedItem is WorldPlace wp && wp.TzId == "Asia/Tokyo", "saved location preselected on next start");
        }
        f.Close();
    }
}
