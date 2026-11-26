using System.Globalization;
using System.Text;

namespace PullCloud;

public sealed class MainForm : Form
{
    private const string AppTitle = "Pull from the Cloud calculator";
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    private readonly AppSettings _settings = AppSettings.Load();
    private bool _ready;

    // Incident
    private readonly DateTimePicker dtpDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy", Width = 130 };
    private readonly NumericUpDown numHour = new() { Minimum = 1, Maximum = 12, Width = 52, Value = 12, BorderStyle = BorderStyle.FixedSingle };
    private readonly TwoDigitUpDown numMin = new() { Minimum = 0, Maximum = 59, Width = 52, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox cmbAmPm = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 62 };
    private readonly Button btnNowUs = Theme.SmallButton("Now");
    private readonly TextBox txtPaste = new() { Width = 250, PlaceholderText = "e.g. 11/26/2026 3:45 PM EST", BorderStyle = BorderStyle.FixedSingle };
    private readonly Label lblPaste = Hint();
    private readonly ComboBox cmbState = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, MaxDropDownItems = 20 };
    private readonly TextBox txtCity = new() { Width = 250, PlaceholderText = "optional – more precise", BorderStyle = BorderStyle.FixedSingle };
    private readonly Label lblCity = Hint();
    private readonly Label lblStateNote = new()
    {
        AutoSize = true, MaximumSize = new Size(760, 0), BackColor = Theme.WarnBg, ForeColor = Theme.WarnText,
        Padding = new Padding(10, 7, 10, 7), Margin = new Padding(0, 0, 0, 4), Visible = false
    };

    // My location
    private readonly ComboBox cmbMe = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, MaxDropDownItems = 20 };
    private readonly Button btnMeSave = Theme.SmallButton("Save", primary: true);
    private readonly Label lblMe = Hint();
    private readonly NumericUpDown numWait = new() { Minimum = 1, Maximum = 360, Width = 64, BorderStyle = BorderStyle.FixedSingle };

    // Result
    private readonly StatusBadge badge = new() { Dock = DockStyle.Top, Height = 64, Text = "—" };
    private readonly Label lblSub = new()
    {
        Dock = DockStyle.Top, Height = 46, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Font(11f), ForeColor = Theme.Text
    };
    private readonly TableLayoutPanel details = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, BackColor = Theme.Card };
    private readonly Dictionary<string, Label> _values = new();
    private readonly Label lblNotes = new() { AutoSize = true, ForeColor = Theme.Muted, MaximumSize = new Size(700, 0), Margin = new Padding(0, 6, 0, 0) };
    private readonly Button btnCopy = Theme.SmallButton("Copy result");

    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _cityDebounce = new() { Interval = 700 };
    private readonly System.Windows.Forms.Timer _pasteDebounce = new() { Interval = 600 };

    // US city lookup state
    private readonly Dictionary<string, (string? tz, string info)> _cityCache = new();
    private string _cityKey = "";
    private string? _cityTz;
    private string _cityInfo = "";
    private CancellationTokenSource? _cityCts;

    private static readonly string[] DetailRows =
    {
        "Incident location", "Created (US time)", "Created (my time)", "Pull available (US time)",
        "Pull available (my time)", "Elapsed", "Remaining", "Now in incident zone", "Now at my location"
    };

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppTitle;
        Font = Theme.Font(9.75f);
        ForeColor = Theme.Text;
        BackColor = Theme.Bg;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 940);
        MinimumSize = new Size(820, 560);

        cmbAmPm.Items.AddRange(new object[] { "AM", "PM" });
        cmbAmPm.SelectedIndex = 0;
        foreach (var s in UsStates.All) cmbState.Items.Add(s);
        var last = UsStates.All.FindIndex(s => s.Abbr == _settings.LastState);
        cmbState.SelectedIndex = last >= 0 ? last : -1;
        numWait.Value = Math.Clamp(_settings.WaitThresholdMinutes, 1, 360);
        FillMyLocations();

        BuildLayout();
        WireEvents();

        _ready = true;
        SetUsNow();
        UpdateMeInfo();
        UpdateStateNote();
        _ = ResolveCityAsync();
        _tick.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Never open taller/wider than the screen (laptops with 125–150% Windows scaling).
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (Height > wa.Height) Height = wa.Height;
        if (Width > wa.Width) Width = wa.Width;
        if (StartPosition == FormStartPosition.CenterScreen)
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
    }

    private static Label Hint() => new() { AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(1, 4, 0, 0), MaximumSize = new Size(420, 0) };

    // ------------------------------------------------------------------ layout

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20, 18, 20, 16), BackColor = Theme.Bg
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Incident card
        var c1 = new Card("Incident (USA)") { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var b1 = Stack();
        b1.Controls.Add(Theme.Row(
            Theme.Field("Incident created on (US local time) *",
                Theme.Row(dtpDate, Gap(), numHour, new Label { Text = ":", AutoSize = true, Margin = new Padding(2, 5, 2, 0) }, numMin, Gap(), cmbAmPm, Gap(), btnNowUs)),
            Theme.Field("…or paste from the system", txtPaste, lblPaste)));
        b1.Controls.Add(Theme.Row(
            Theme.Field("State *", cmbState),
            Theme.Field("City", txtCity, lblCity)));
        b1.Controls.Add(lblStateNote);
        c1.Controls.Add(b1);
        root.Controls.Add(c1, 0, 0);

        // My location card
        var c2 = new Card("My location") { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var b2 = Stack();
        btnMeSave.Margin = new Padding(8, 0, 0, 0);
        b2.Controls.Add(Theme.Row(
            Theme.Field("Current location", Theme.Row(cmbMe, btnMeSave), lblMe),
            Theme.Field("WAIT threshold",
                Theme.Row(numWait, new Label { Text = "minutes", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(6, 5, 0, 0) }),
                new Label { Text = "Time left ≤ threshold → WAIT, more → NOT YET", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(1, 4, 0, 0) })));
        c2.Controls.Add(b2);
        root.Controls.Add(c2, 0, 1);

        // Result card (docking: Fill first, then Bottom, then Top items in reverse order)
        var c3 = new Card("Result") { Dock = DockStyle.Fill, Margin = new Padding(0) };
        // vertical scrolling only for the details table
        details.AutoScroll = false;
        details.HorizontalScroll.Maximum = 0;
        details.HorizontalScroll.Visible = false;
        details.AutoScroll = true;
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        details.Padding = new Padding(0, 10, 0, 0);
        foreach (var key in DetailRows)
        {
            var r = details.RowCount++;
            details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            details.Controls.Add(new Label { Text = key, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 3, 0, 2) }, 0, r);
            var v = new Label { AutoSize = true, Font = Theme.Semi(10f), ForeColor = Theme.Text, Margin = new Padding(0, 2, 0, 2) };
            if (key.Contains("my time")) v.ForeColor = Theme.Accent;
            _values[key] = v;
            details.Controls.Add(v, 1, r);
        }
        var nr = details.RowCount++;
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.Controls.Add(lblNotes, 0, nr);
        details.SetColumnSpan(lblNotes, 2);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Card, Padding = new Padding(0, 6, 0, 0)
        };
        btnCopy.Margin = new Padding(0);
        bottom.Controls.Add(btnCopy);

        c3.Controls.Add(details);
        c3.Controls.Add(bottom);
        c3.Controls.Add(lblSub);
        c3.Controls.Add(badge);
        root.Controls.Add(c3, 0, 2);

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Header, Padding = new Padding(24, 12, 24, 10) };
        var sub = new Label
        {
            Text = "Checks whether an incident report can be pulled from the cloud – at least 6 hours must pass since it was created.",
            Dock = DockStyle.Top, AutoSize = false, Height = 22, ForeColor = Theme.HeaderSub, Font = Theme.Font(9.5f)
        };
        var title = new Label { Text = "☁  " + AppTitle, Dock = DockStyle.Top, Height = 34, ForeColor = Color.White, Font = Theme.Semi(17f) };
        header.Controls.Add(sub);
        header.Controls.Add(title);

        Controls.Add(root);
        Controls.Add(header);

        // Small screens / high Windows scaling: the whole page scrolls instead of the result being cut off.
        AutoScroll = true;
        void UpdateMinHeight()
        {
            var min = header.Height + root.Padding.Vertical + c1.Height + c1.Margin.Vertical
                      + c2.Height + c2.Margin.Vertical + LogicalToDeviceUnits(360);
            if (AutoScrollMinSize.Height != min) AutoScrollMinSize = new Size(0, min);
        }
        c1.SizeChanged += (_, _) => UpdateMinHeight();
        c2.SizeChanged += (_, _) => UpdateMinHeight();
        header.SizeChanged += (_, _) => UpdateMinHeight();
    }

    private static FlowLayoutPanel Stack() => new()
    {
        Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Card
    };

    private static Control Gap() => new Panel { Width = 4, Height = 1, Margin = new Padding(0), BackColor = Theme.Card };

    // ------------------------------------------------------------------ events

    private void WireEvents()
    {
        EventHandler recalc = (_, _) => Recalc();
        dtpDate.ValueChanged += recalc;
        numHour.ValueChanged += recalc;
        numMin.ValueChanged += recalc;
        cmbAmPm.SelectedIndexChanged += recalc;
        btnNowUs.Click += (_, _) => SetUsNow();
        _tick.Tick += recalc;

        // Parse only after the user pauses typing, so partial input like "11/26/202" isn't applied.
        txtPaste.TextChanged += (_, _) => { _pasteDebounce.Stop(); _pasteDebounce.Start(); };
        txtPaste.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _pasteDebounce.Stop(); ParsePaste(); } };
        txtPaste.Leave += (_, _) => { if (_pasteDebounce.Enabled) { _pasteDebounce.Stop(); ParsePaste(); } };
        _pasteDebounce.Tick += (_, _) => { _pasteDebounce.Stop(); ParsePaste(); };

        cmbState.SelectedIndexChanged += (_, _) =>
        {
            if (SelectedState is { } s) { _settings.LastState = s.Abbr; _settings.Save(); }
            UpdateStateNote();
            _ = ResolveCityAsync();
        };
        txtCity.TextChanged += (_, _) => { _cityDebounce.Stop(); _cityDebounce.Start(); };
        txtCity.Leave += (_, _) => { _cityDebounce.Stop(); _ = ResolveCityAsync(); };
        txtCity.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _cityDebounce.Stop(); _ = ResolveCityAsync(); } };
        _cityDebounce.Tick += (_, _) => { _cityDebounce.Stop(); _ = ResolveCityAsync(); };

        cmbMe.SelectedIndexChanged += (_, _) => { UpdateMeInfo(); Recalc(); };
        btnMeSave.Click += (_, _) => SaveMe();
        numWait.ValueChanged += (_, _) => { _settings.WaitThresholdMinutes = (int)numWait.Value; _settings.Save(); Recalc(); };

        btnCopy.Click += (_, _) =>
        {
            try { Clipboard.SetText(BuildCopyText()); btnCopy.Text = "Copied ✓"; }
            catch { }
        };
        btnCopy.MouseLeave += (_, _) => btnCopy.Text = "Copy result";
    }

    private UsState? SelectedState => cmbState.SelectedItem as UsState;
    private WorldPlace? SelectedPlace => cmbMe.SelectedItem as WorldPlace;

    private TimeZoneInfo MyTz =>
        SelectedPlace is { } p && Tz.TryGet(p.TzId, out var tz) ? tz! : TimeZoneInfo.Local;

    private string MyPlaceName => SelectedPlace?.Name ?? "This PC";

    private DateTime CreatedLocal
    {
        get
        {
            var h = (int)numHour.Value % 12 + (cmbAmPm.SelectedIndex == 1 ? 12 : 0);
            return dtpDate.Value.Date.AddHours(h).AddMinutes((int)numMin.Value);
        }
    }

    private void SetPickers(DateTime dt)
    {
        if (dt < dtpDate.MinDate || dt > dtpDate.MaxDate) return;
        _ready = false;
        try
        {
            dtpDate.Value = dt.Date;
            var h12 = dt.Hour % 12;
            numHour.Value = h12 == 0 ? 12 : h12;
            numMin.Value = dt.Minute;
            cmbAmPm.SelectedIndex = dt.Hour < 12 ? 0 : 1;
        }
        catch (ArgumentOutOfRangeException) { }
        finally { _ready = true; }
        Recalc();
    }

    /// <summary>Set "created on" to the current time in the selected state's zone.</summary>
    private void SetUsNow()
    {
        var tzId = SelectedState is { } s ? Candidates(s).First() : "America/New_York";
        SetPickers(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz.Get(tzId)).DateTime);
    }

    private void ParsePaste()
    {
        if (txtPaste.Text.Trim() == "") { lblPaste.Text = ""; return; }
        switch (PasteParser.TryParse(txtPaste.Text, out var dt))
        {
            case PasteResult.Ok:
                SetPickers(dt);
                lblPaste.ForeColor = Theme.OkText;
                lblPaste.Text = "✓ Read as " + dt.ToString("MM/dd/yyyy hh:mm tt", Us);
                break;
            case PasteResult.BadYear:
                lblPaste.ForeColor = Theme.ErrText;
                lblPaste.Text = "✗ Incomplete or invalid year (use MM/dd/yyyy h:mm AM/PM)";
                break;
            default:
                lblPaste.ForeColor = Theme.ErrText;
                lblPaste.Text = "✗ Can't read this (use MM/dd/yyyy h:mm AM/PM)";
                break;
        }
    }

    private void UpdateStateNote()
    {
        var s = SelectedState;
        var cityResolved = _cityTz != null && _cityKey == CityKey(s);
        if (s == null || !s.IsSplit || cityResolved) { lblStateNote.Visible = false; return; }
        lblStateNote.Text = $"⚠  {s.Name} spans more than one time zone. {s.Note}\n     Enter the city for an exact result – without it, the safest (latest) pull time is used.";
        lblStateNote.Visible = true;
    }

    // ------------------------------------------------------------------ US city lookup

    private string CityKey(UsState? s) => (s?.Abbr ?? "") + "|" + Util.Norm(txtCity.Text);

    private IEnumerable<string> Candidates(UsState s)
    {
        if (_cityTz != null && _cityKey == CityKey(s)) return new[] { _cityTz };
        return s.IsSplit ? s.AllTz : new[] { s.DefaultTz };
    }

    private async Task ResolveCityAsync()
    {
        var st = SelectedState;
        var city = txtCity.Text.Trim();
        var key = CityKey(st);
        if (key == _cityKey && !_cityInfo.StartsWith("Searching")) { Recalc(); return; }

        _cityCts?.Cancel();
        _cityKey = key;
        _cityTz = null;

        if (st == null || city == "") { SetCityInfo("", Theme.Muted); return; }
        if (!st.IsSplit)
        {
            _cityTz = st.DefaultTz;
            SetCityInfo("✓ Not needed – the whole state is in one time zone", Theme.Muted);
            return;
        }
        if (_cityCache.TryGetValue(key, out var cached))
        {
            _cityTz = cached.tz;
            SetCityInfo(cached.info, cached.tz != null ? Theme.OkText : Theme.ErrText);
            return;
        }
        if (st.Cities.TryGetValue(Util.Norm(city), out var localTz))
        {
            _cityTz = localTz;
            var info = $"✓ {Tz.UsZoneName(localTz)} time ({localTz})";
            _cityCache[key] = (localTz, info);
            SetCityInfo(info, Theme.OkText);
            return;
        }

        SetCityInfo("Searching online…", Theme.Muted);
        var cts = _cityCts = new CancellationTokenSource();
        (string? tz, string info) result;
        try
        {
            var hits = await Geo.SearchAsync(city, "US", cts.Token);
            var hit = hits.FirstOrDefault(h => string.Equals(h.Admin1, st.Name, StringComparison.OrdinalIgnoreCase) && Tz.TryGet(h.Timezone, out _));
            result = hit != null
                ? (hit.Timezone, $"✓ {hit.Name}, {st.Abbr}: {Tz.UsZoneName(hit.Timezone)} time ({hit.Timezone})")
                : (null, $"⚠ '{city}' not found in {st.Name} – using the safest pull time");
            _cityCache[key] = result;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { return; }
        catch
        {
            result = (null, "⚠ Online lookup failed (no internet?) – using the safest pull time");
        }
        if (_cityKey != key) return; // input changed while searching
        _cityTz = result.tz;
        SetCityInfo(result.info, result.tz != null ? Theme.OkText : Theme.ErrText);
    }

    private void SetCityInfo(string text, Color color)
    {
        _cityInfo = text;
        UpdateStateNote();
        lblCity.Text = text;
        lblCity.ForeColor = color;
        Recalc();
    }

    // ------------------------------------------------------------------ my location

    private void FillMyLocations()
    {
        var items = WorldCities.All.ToList();
        // Exact name match; otherwise same city (first word) in the same zone; otherwise keep the saved place as its own entry.
        var firstWord = Util.Norm(_settings.MyLocationName).Split(' ').FirstOrDefault() ?? "";
        var saved = items.FirstOrDefault(p => p.Name == _settings.MyLocationName)
                    ?? items.FirstOrDefault(p => p.TzId == _settings.MyTzId && Util.Norm(p.Name).Split(' ')[0] == firstWord);
        if (saved == null && Tz.TryGet(_settings.MyTzId, out _))
        {
            saved = new WorldPlace(_settings.MyLocationName, _settings.MyTzId);
            items.Insert(0, saved);
        }
        foreach (var p in items) cmbMe.Items.Add(p);
        cmbMe.SelectedItem = saved ?? items.FirstOrDefault(p => p.TzId == "Europe/Sarajevo") ?? items.First();
    }

    private bool IsSaved => SelectedPlace is { } p && p.Name == _settings.MyLocationName && p.TzId == _settings.MyTzId;

    private void UpdateMeInfo()
    {
        var tz = MyTz;
        var now = DateTimeOffset.UtcNow;
        var dst = tz.SupportsDaylightSavingTime ? (tz.IsDaylightSavingTime(now) ? " · summer time (DST)" : " · standard (winter) time") : " · no DST";
        lblMe.Text = $"{SelectedPlace?.TzId} · {Tz.Offset(tz.GetUtcOffset(now))}{dst}" + (IsSaved ? "" : "   •  not saved");
        btnMeSave.Text = IsSaved ? "Saved ✓" : "Save";
        btnMeSave.BackColor = IsSaved ? Theme.Green : Theme.Accent;
    }

    private void SaveMe()
    {
        if (SelectedPlace is not { } p) return;
        _settings.MyLocationName = p.Name;
        _settings.MyTzId = p.TzId;
        _settings.Save();
        UpdateMeInfo();
        Recalc();
    }

    // ------------------------------------------------------------------ calculation

    private void Recalc()
    {
        if (!_ready) return;
        var st = SelectedState;
        if (st == null)
        {
            ShowStatus("SELECT A STATE", Theme.Neutral, "Enter the incident creation time and the state.");
            ClearDetails();
            return;
        }

        CalcResult r;
        try
        {
            r = PullCalc.Compute(CreatedLocal, Candidates(st), DateTimeOffset.UtcNow, (int)numWait.Value);
        }
        catch (Exception ex)
        {
            ShowStatus("ERROR", Theme.Neutral, ex.Message);
            ClearDetails();
            return;
        }

        var me = MyTz;
        var g = r.Governing;
        var pullMe = TimeZoneInfo.ConvertTime(g.PullAt, me);
        var nowMe = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, me);
        string when = pullMe.Date == nowMe.Date ? $"today at {pullMe:HH:mm}"
            : pullMe.Date == nowMe.Date.AddDays(1) ? $"tomorrow at {pullMe:HH:mm}"
            : "on " + pullMe.ToString("ddd dd MMM yyyy 'at' HH:mm", En);

        string sub;
        switch (r.Status)
        {
            case PullStatus.PullAvailable:
                sub = $"More than 6 hours have passed ({Util.FormatSpan(r.Elapsed)} elapsed). The report can be pulled.";
                ShowStatus("PULL AVAILABLE", Theme.Green, sub);
                break;
            case PullStatus.Wait:
                sub = $"6 hours have not passed yet – you can pull {when} (your time), in {Util.FormatSpan(r.Remaining)}.";
                ShowStatus("WAIT", Theme.Amber, sub);
                break;
            default:
                sub = $"Still a lot of time left – pull available {when} (your time), in {Util.FormatSpan(r.Remaining)}.";
                ShowStatus("NOT YET", Theme.Red, sub);
                break;
        }
        if (r.CreatedInFuture)
            lblSub.Text = "⚠ The creation time is in the future for this zone – check the date, AM/PM and state!\n" + lblSub.Text;

        FillDetails(st, r, me);
    }

    private static string UsFmt(DateTimeOffset i, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTime(i, tz).ToString("ddd MM/dd/yyyy hh:mm tt", Us) + "  " + Tz.UsAbbrev(tz, i) + " (" + Tz.Offset(tz.GetUtcOffset(i)) + ")";

    private static string MeFmt(DateTimeOffset i, TimeZoneInfo me)
    {
        var t = TimeZoneInfo.ConvertTime(i, me);
        return t.ToString("ddd dd MMM yyyy HH:mm", En) + "  (" + Tz.Offset(t.Offset) + ")";
    }

    private void FillDetails(UsState st, CalcResult r, TimeZoneInfo me)
    {
        var g = r.Governing;
        var now = DateTimeOffset.UtcNow;
        var place = string.IsNullOrWhiteSpace(txtCity.Text) ? st.Name : $"{txtCity.Text.Trim()}, {st.Abbr}";
        Set("Incident location", $"{place}  →  {Tz.UsZoneName(g.TzId)} ({g.TzId})");
        Set("Created (US time)", UsFmt(g.Created, g.Tz));
        Set("Created (my time)", MeFmt(g.Created, me));
        Set("Pull available (US time)", UsFmt(g.PullAt, g.Tz));
        Set("Pull available (my time)", MeFmt(g.PullAt, me));
        Set("Elapsed", (r.Elapsed < TimeSpan.Zero ? "-" : "") + Util.FormatSpan(r.Elapsed));
        Set("Remaining", r.Remaining <= TimeSpan.Zero ? "0 – ready to pull" : Util.FormatSpan(r.Remaining));
        Set("Now in incident zone", UsFmt(now, g.Tz));
        Set("Now at my location", $"{MeFmt(now, me)}  – {MyPlaceName}");

        var notes = new StringBuilder();
        if (r.All.Count > 1)
        {
            notes.AppendLine("City unknown → possible time zones (the latest pull time is used to be safe):");
            foreach (var z in r.All.OrderBy(z => z.PullAt))
                notes.AppendLine($"   • {Tz.UsZoneName(z.TzId)}: pull from {MeFmt(z.PullAt, me)} my time");
        }
        foreach (var w in r.All.Select(z => z.Warning).Where(w => w != null).Distinct())
            notes.AppendLine("⚠ " + w);
        var text = notes.ToString().TrimEnd();
        if (lblNotes.Text != text) lblNotes.Text = text;
    }

    private void Set(string key, string value)
    {
        var l = _values[key];
        if (l.Text != value) l.Text = value;
    }

    private void ClearDetails()
    {
        foreach (var l in _values.Values) l.Text = "";
        lblNotes.Text = "";
    }

    private void ShowStatus(string title, Color color, string sub)
    {
        badge.Text = title;
        badge.BadgeColor = color;
        if (lblSub.Text != sub) lblSub.Text = sub;
    }

    private string BuildCopyText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(badge.Text);
        sb.AppendLine(lblSub.Text);
        sb.AppendLine();
        foreach (var k in DetailRows) sb.AppendLine($"{k,-26}: {_values[k].Text}");
        if (lblNotes.Text != "") { sb.AppendLine(); sb.AppendLine(lblNotes.Text); }
        return sb.ToString();
    }
}
