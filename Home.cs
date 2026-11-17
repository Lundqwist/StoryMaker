using System.Drawing.Drawing2D;

namespace StoryMaker;

/// <summary>Pictures built into the program (the book icon, and lundqwist.se's emblem and logo).</summary>
public static class Assets
{
    static readonly Dictionary<string, Image?> cache = [];

    public static Image? Get(string name)
    {
        if (cache.TryGetValue(name, out var img)) return img;
        try
        {
            using var stream = typeof(Assets).Assembly.GetManifestResourceStream(name);
            img = stream is null ? null : new Bitmap(Image.FromStream(stream));
        }
        catch (Exception) { img = null; }
        return cache[name] = img;
    }
}

/// <summary>One character on the front page: a gold-framed card. Click it to write that character's story.</summary>
public class CharacterCard : Control, IThemed
{
    public JournalFile File { get; }
    readonly string title, line1, line2;
    bool hover;

    public CharacterCard(JournalFile file, string title, string line1, string line2)
    {
        File = file;
        this.title = title;
        this.line1 = line1;
        this.line2 = line2;
        Size = new Size(300, 104);
        Margin = new Padding(10);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
        AccessibleName = $"{title}, {line1}, {line2}";
        AccessibleRole = AccessibleRole.PushButton;
    }

    public void ApplyTheme() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Back);
        var r = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = Rounded(r, 8);
        using (var fill = new SolidBrush(hover ? Theme.Frame : Theme.Band)) g.FillPath(fill, path);
        using (var pen = new Pen(hover ? Theme.AccentSoft : Theme.Accent, hover ? 2f : 1f)) g.DrawPath(pen, path);
        // a little gold book at the left
        if (Theme.AppIcon is { } icon) g.DrawIcon(new Icon(icon, 32, 32), new Rectangle(16, 18, 32, 32));
        using var titleFont = new Font("Georgia", 14f, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 9.5f);
        TextRenderer.DrawText(g, title, titleFont, new Rectangle(60, 14, Width - 72, 28), Theme.Accent, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, line1, smallFont, new Rectangle(60, 46, Width - 72, 20), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, line2, smallFont, new Rectangle(60, 68, Width - 72, 20), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>
/// The front page: lundqwist.se's big emblem, the program's name, and a card for every character that
/// has a journal.
/// </summary>
public class HomeView : Panel, IThemed
{
    // the emblem is drawn by the panel itself, with its edges fading into the page (it has a lighter navy of its own)
    readonly Image? emblem = Assets.Get("lundqwist-emblem.png");
    Rectangle emblemBounds;
    readonly Label title = new() { Text = "StoryMaker", AutoSize = true, Font = new Font("Georgia", 26f, FontStyle.Regular) };
    readonly Label subtitle = new()
    {
        Text = "Your characters' adventures, told as their own memories",
        AutoSize = true, Font = new Font("Segoe UI", 11.5f, FontStyle.Italic),
    };
    readonly Label empty = new() { AutoSize = true, Font = new Font("Segoe UI", 10.5f), Visible = false };
    readonly FlowLayoutPanel cards = new() { AutoSize = true, WrapContents = true, BackColor = Color.Transparent };

    public event Action<JournalFile>? CharacterChosen;

    public HomeView()
    {
        AutoScroll = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Controls.AddRange([title, subtitle, empty, cards]);
        Scroll += (_, _) => { Arrange(); Invalidate(); };
        Resize += (_, _) => Arrange();
    }

    public void SetCharacters(IEnumerable<CharacterCard> list, string emptyText)
    {
        foreach (Control c in cards.Controls) c.Dispose();
        cards.Controls.Clear();
        foreach (var card in list)
        {
            card.Click += (_, _) => CharacterChosen?.Invoke(card.File);
            cards.Controls.Add(card);
        }
        empty.Text = emptyText;
        empty.Visible = cards.Controls.Count == 0;
        ApplyTheme();
        Arrange();
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Back;
        title.ForeColor = Theme.Accent;
        subtitle.ForeColor = Theme.Muted;
        empty.ForeColor = Theme.Text;
        foreach (Control c in cards.Controls) c.Invalidate();
        Invalidate();
    }

    void Arrange()
    {
        int w = ClientSize.Width;
        int ew = Math.Min(Math.Max(w - 80, 300), 880);
        int eh = ew * 572 / 1024;
        int y = 12 - VerticalScroll.Value;
        emblemBounds = new Rectangle((w - ew) / 2, y, ew, eh);
        y += eh - 6;
        title.Location = new Point((w - title.PreferredWidth) / 2, y);
        y += title.PreferredHeight + 2;
        subtitle.Location = new Point((w - subtitle.PreferredWidth) / 2, y);
        y += subtitle.PreferredHeight + 22;
        empty.Location = new Point(Math.Max(10, (w - empty.PreferredWidth) / 2), y);
        // cards centred, as many to a row as fit
        int cardW = 320, perRow = Math.Max(1, Math.Min(cards.Controls.Count, (w - 40) / cardW));
        cards.MaximumSize = new Size(perRow * cardW, 0);
        var size = cards.GetPreferredSize(new Size(perRow * cardW, 0));
        cards.Location = new Point(Math.Max(10, (w - size.Width) / 2), y);
    }

    // the emblem, fading out at its edges; then a soft gold line under the title, like the site's dividers
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (emblem is not null && emblemBounds.Width > 0)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(emblem, emblemBounds);
            var r = emblemBounds;
            int fx = r.Width / 7, fy = r.Height / 6;
            void Fade(Rectangle area, LinearGradientMode mode, bool towardsStart)
            {
                using var brush = new LinearGradientBrush(area, towardsStart ? Theme.Back : Color.FromArgb(0, Theme.Back),
                    towardsStart ? Color.FromArgb(0, Theme.Back) : Theme.Back, mode);
                g.FillRectangle(brush, area);
            }
            Fade(new Rectangle(r.Left - 1, r.Top, fx, r.Height), LinearGradientMode.Horizontal, true);
            Fade(new Rectangle(r.Right - fx + 1, r.Top, fx, r.Height), LinearGradientMode.Horizontal, false);
            Fade(new Rectangle(r.Left, r.Top - 1, r.Width, fy), LinearGradientMode.Vertical, true);
            Fade(new Rectangle(r.Left, r.Bottom - fy + 1, r.Width, fy), LinearGradientMode.Vertical, false);
        }
        int y = subtitle.Bottom + 10;
        using var pen = new Pen(Color.FromArgb(120, Theme.Accent), 1);
        int half = 120;
        e.Graphics.DrawLine(pen, ClientSize.Width / 2 - half, y, ClientSize.Width / 2 + half, y);
    }
}

/// <summary>The band at the top of the window, as on lundqwist.se: the tree-and-moon logo and the name.</summary>
public class HeaderBand : Panel, IThemed
{
    readonly PictureBox logo = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(42, 42), Location = new Point(14, 8) };
    readonly Label name = new() { Text = "StoryMaker", AutoSize = true, Font = new Font("Georgia", 17f), Location = new Point(64, 9) };
    readonly Label sub = new() { Text = "Lunar Journal", AutoSize = true, Font = new Font("Segoe UI", 9f), Location = new Point(66, 37) };
    public readonly Button HomeButton = new() { Text = "Home", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };

    public HeaderBand()
    {
        Dock = DockStyle.Top;
        Height = 58;
        logo.Image = Assets.Get("lundqwist-logo.png");
        Controls.AddRange([logo, name, sub, HomeButton]);
        Resize += (_, _) => HomeButton.Location = new Point(ClientSize.Width - HomeButton.Width - 16, (Height - HomeButton.Height) / 2);
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Band;
        logo.BackColor = Theme.Band;
        name.ForeColor = Theme.Accent;
        sub.ForeColor = Theme.Muted;
        foreach (Control c in Controls) if (c is Button) Theme.PaintControl(c);
        Invalidate();
    }

    // the gold line under the band
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Accent, 1);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}
