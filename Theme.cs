using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace StoryMaker;

/// <summary>
/// The look. Dark is lundqwist.se's own: deep navy, navy bands, gold frames and headings. .NET 8's
/// WinForms has no dark mode of its own, so the colours are set on each control, and Windows is asked
/// for a dark title bar and dark scroll bars.
/// </summary>
public static class Theme
{
    public const string System = "System", Light = "Light", Dark = "Dark";

    public static bool IsDark { get; private set; }

    // lundqwist.se's colours (Website2.0 styles.css)
    public static readonly Color NavyDeep = Color.FromArgb(0x08, 0x14, 0x28);   // page background
    public static readonly Color Navy = Color.FromArgb(0x0e, 0x21, 0x47);       // header and footer bands
    public static readonly Color NavyLight = Color.FromArgb(0x17, 0x36, 0x6b);  // frames and borders
    public static readonly Color Gold = Color.FromArgb(0xc9, 0xa3, 0x6b);       // headings, labels, lines
    public static readonly Color GoldSoft = Color.FromArgb(0xe0, 0xc7, 0x9d);   // hover, highlights
    static readonly Color SiteText = Color.FromArgb(0xd5, 0xdc, 0xeb);
    static readonly Color SiteMuted = Color.FromArgb(0x94, 0xa2, 0xbd);

    static readonly Color DarkBack = NavyDeep;
    static readonly Color DarkInput = Color.FromArgb(0x0b, 0x1b, 0x38);
    static readonly Color DarkButton = Navy;
    static readonly Color DarkBorder = NavyLight;
    static readonly Color DarkText = SiteText;
    static readonly Color DarkMuted = SiteMuted;

    // Light: parchment and ink, with the same gold
    static readonly Color LightBack = Color.FromArgb(0xf6, 0xf1, 0xe7);
    static readonly Color LightBand = Color.FromArgb(0xe9, 0xe0, 0xcd);
    static readonly Color LightInk = Color.FromArgb(0x1c, 0x24, 0x38);
    static readonly Color LightGold = Color.FromArgb(0x8a, 0x64, 0x28);

    public static Color Back => IsDark ? DarkBack : LightBack;
    public static Color Band => IsDark ? Navy : LightBand;
    public static Color Frame => IsDark ? NavyLight : Color.FromArgb(0xc8, 0xb8, 0x96);
    public static Color Text => IsDark ? DarkText : LightInk;
    public static Color Accent => IsDark ? Gold : LightGold;
    public static Color AccentSoft => IsDark ? GoldSoft : Color.FromArgb(0x6e, 0x4c, 0x18);
    public static Color Muted => IsDark ? DarkMuted : Color.FromArgb(0x6b, 0x6f, 0x7d);
    public static Color Good => IsDark ? Color.FromArgb(120, 200, 140) : Color.ForestGreen;
    public static Color Bad => IsDark ? Color.FromArgb(240, 110, 100) : Color.Firebrick;

    /// <summary>Works out light or dark from the setting ("System" follows Windows' app mode).</summary>
    public static void Choose(string? setting) => IsDark = setting switch
    {
        Dark => true,
        Light => false,
        _ => WindowsIsDark(),
    };

    static bool WindowsIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Colours a window and everything in it. Labels marked with Tag = "muted" get the muted text colour.</summary>
    public static void Apply(Form form)
    {
        if (form.IsHandleCreated) TitleBar(form);
        else form.HandleCreated += (_, _) => TitleBar(form);
        Paint(form);
    }

    /// <summary>Colours one control and what is in it.</summary>
    public static void PaintControl(Control c) => Paint(c);

    static void Paint(Control c)
    {
        switch (c)
        {
            case TextBox or ListBox or ComboBox or ListView:
                c.BackColor = IsDark ? DarkInput : Color.FromArgb(0xfd, 0xfb, 0xf6);
                c.ForeColor = IsDark ? DarkText : LightInk;
                if (c is TextBox t) t.BorderStyle = IsDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                if (c is ListBox l) l.BorderStyle = IsDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                if (c is ComboBox cb) StyleComboBox(cb);
                DarkScrollBars(c);
                break;
            case Button b:
                // like the site's buttons: a gold outline; the main ones (Tag "primary") filled with gold
                bool primary = Equals(b.Tag, "primary");
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Accent;
                b.BackColor = primary ? Accent : Band;
                b.ForeColor = primary ? (IsDark ? NavyDeep : Color.White) : (IsDark ? GoldSoft : LightInk);
                b.FlatAppearance.MouseOverBackColor = primary ? AccentSoft : (IsDark ? NavyLight : Color.FromArgb(0xde, 0xd2, 0xb8));
                b.FlatAppearance.MouseDownBackColor = primary ? AccentSoft : Frame;
                b.Cursor = Cursors.Hand;
                break;
            case Label or CheckBox:
                c.BackColor = Color.Transparent;
                if (Equals(c.Tag, "muted")) c.ForeColor = Muted;
                else if (Equals(c.Tag, "gold")) c.ForeColor = Accent;
                else if (!Equals(c.Tag, "keep")) c.ForeColor = Text;
                break;
            case IThemed themed:
                themed.ApplyTheme();
                return;   // draws its own children
            default:
                c.BackColor = Equals(c.Tag, "band") ? Band : Back;
                c.ForeColor = Text;
                break;
        }
        foreach (Control child in c.Controls) Paint(child);
    }

    // Windows draws themed drop-down boxes itself, light whatever their colour. In dark mode the
    // theme is switched off for them and the program draws the items (and the closed box) itself.
    static readonly HashSet<ComboBox> ownDrawn = [];

    static void StyleComboBox(ComboBox cb)
    {
        if (ownDrawn.Add(cb))
        {
            cb.DrawItem += DrawComboItem;
            // Changing DrawMode makes Windows create the box anew, with its theme back: set it every time
            cb.HandleCreated += (_, _) => ComboTheme(cb);
        }
        // Not FlatStyle.Flat: WinForms' flat drawing paints the box white whatever its colour
        cb.FlatStyle = FlatStyle.Standard;
        cb.DrawMode = DrawMode.OwnerDrawFixed;
        if (cb.IsHandleCreated) ComboTheme(cb);
        cb.Invalidate();
    }

    static void ComboTheme(ComboBox cb)
    {
        // Windows' own dark drop-down look (dark box and arrow), or back to the normal one
        SetWindowTheme(cb.Handle, IsDark ? "DarkMode_CFD" : null, null);
    }

    static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
        bool selected = inList && (e.State & DrawItemState.Selected) != 0;
        var back = selected ? (IsDark ? NavyLight : Color.FromArgb(0xe4, 0xd3, 0xae)) : cb.BackColor;
        var fore = selected ? (IsDark ? GoldSoft : LightInk) : cb.ForeColor;
        if (!cb.Enabled) fore = Muted;
        using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
        if (e.Index >= 0)
        {
            var text = cb.GetItemText(cb.Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // ------------------------------------------------------------ Windows' own parts

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    static void TitleBar(Form form)
    {
        int on = IsDark ? 1 : 0;
        // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 20H1 and later; 19 before that)
        if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
        // the title bar is drawn again when the window is next activated; nudge it now
        if (form.Visible) { form.Width++; form.Width--; }
    }

    static void DarkScrollBars(Control c)
    {
        // Not for drop-down boxes: Windows' themed drawing would paint over their dark colour
        if (c is ComboBox) return;
        void Set() => SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null);
        if (c.IsHandleCreated) Set();
        else c.HandleCreated += (_, _) => Set();
    }

    /// <summary>The program's icon (the book), built into the program with every size, for every window.</summary>
    public static Icon? AppIcon { get; } = LoadIcon();

    static Icon? LoadIcon()
    {
        try
        {
            using var stream = typeof(Theme).Assembly.GetManifestResourceStream("StoryMaker.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch (Exception) { return null; }
    }
}

/// <summary>A control that colours itself (and its children) when the look changes.</summary>
public interface IThemed
{
    void ApplyTheme();
}