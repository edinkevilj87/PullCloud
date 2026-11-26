using System.Drawing.Drawing2D;

namespace PullCloud;

public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(241, 245, 249);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Header = Color.FromArgb(15, 23, 42);
    public static readonly Color HeaderSub = Color.FromArgb(148, 163, 184);
    public static readonly Color Green = Color.FromArgb(22, 163, 74);
    public static readonly Color Amber = Color.FromArgb(217, 119, 6);
    public static readonly Color Red = Color.FromArgb(220, 38, 38);
    public static readonly Color Neutral = Color.FromArgb(148, 163, 184);
    public static readonly Color OkText = Color.FromArgb(21, 128, 61);
    public static readonly Color ErrText = Color.FromArgb(185, 28, 28);
    public static readonly Color WarnBg = Color.FromArgb(254, 243, 199);
    public static readonly Color WarnText = Color.FromArgb(146, 64, 14);

    public static Font Font(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Font Semi(float size) => new("Segoe UI Semibold", size);

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Button Button(string text, bool primary)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = Semi(9.75f), Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0, 0, 8, 0),
            BackColor = primary ? Accent : Color.FromArgb(241, 245, 249),
            ForeColor = primary ? Color.White : Text, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Color.FromArgb(226, 232, 240);
        return b;
    }

    /// <summary>Compact button sized to line up with text boxes and combo boxes.</summary>
    public static Button SmallButton(string text, bool primary = false)
    {
        var b = Button(text, primary);
        b.AutoSize = false;
        b.Padding = new Padding(0);
        b.Height = 27;
        b.Width = Math.Max(72, TextRenderer.MeasureText(text + "  ✓", b.Font).Width + 20);
        return b;
    }

    /// <summary>A labelled field: small caption above the input, optional hint below.</summary>
    public static FlowLayoutPanel Field(string caption, Control input, Control? hint = null)
    {
        var f = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false,
            Margin = new Padding(0, 0, 28, 10), BackColor = Card
        };
        f.Controls.Add(new Label
        {
            Text = caption.ToUpperInvariant(), AutoSize = true, ForeColor = Muted, Font = Semi(8.25f),
            Margin = new Padding(1, 0, 0, 4)
        });
        f.Controls.Add(input);
        if (hint != null) f.Controls.Add(hint);
        return f;
    }

    public static FlowLayoutPanel Row(params Control[] cs)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = Card };
        f.Controls.AddRange(cs);
        return f;
    }
}

/// <summary>White rounded card with a thin border and a small title.</summary>
public sealed class Card : Panel
{
    private readonly string _title;

    public Card(string title)
    {
        _title = title.ToUpperInvariant();
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Card;
        Padding = new Padding(20, 40, 20, 10);
        Margin = new Padding(0, 0, 0, 14);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 12);
        using var fill = new SolidBrush(Theme.Card);
        g.FillPath(fill, path);
        using var pen = new Pen(Theme.Border);
        g.DrawPath(pen, path);
        using var accent = new SolidBrush(Theme.Accent);
        g.FillRectangle(accent, 20, 18, 4, 14);
        using var f = Theme.Semi(9f);
        TextRenderer.DrawText(g, _title, f, new Point(30, 16), Theme.Text);
    }
}

/// <summary>Large rounded status pill (PULL AVAILABLE / WAIT / NOT YET).</summary>
public sealed class StatusBadge : Control
{
    private Color _color = Theme.Neutral;

    public StatusBadge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = Theme.Font(26f, FontStyle.Bold);
        BackColor = Theme.Card;
        ForeColor = Color.White;
    }

    public Color BadgeColor
    {
        get => _color;
        set { if (_color != value) { _color = value; Invalidate(); } }
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 14);
        using var b = new SolidBrush(_color);
        g.FillPath(b, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>NumericUpDown that always shows two digits (e.g. 05 for minutes).</summary>
public sealed class TwoDigitUpDown : NumericUpDown
{
    protected override void UpdateEditText() => Text = ((int)Value).ToString("00");
}
