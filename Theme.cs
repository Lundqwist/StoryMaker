using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace StoryMaker;

/// <summary>
/// Light and dark look. .NET 8's WinForms has no dark mode of its own, so the colours are set on each
/// control, and Windows is asked for a dark title bar and dark scroll bars.
/// </summary>
public static class Theme
{
    public const string System = "System", Light = "Light", Dark = "Dark";

    public static bool IsDark { get; private set; }

    // Dark colours, close to Windows' own dark mode
    static readonly Color DarkBack = Color.FromArgb(32, 32, 32);
    static readonly Color DarkInput = Color.FromArgb(43, 43, 43);
    static readonly Color DarkButton = Color.FromArgb(55, 55, 55);
    static readonly Color DarkBorder = Color.FromArgb(85, 85, 85);
    static readonly Color DarkText = Color.FromArgb(230, 230, 230);
    static readonly Color DarkMuted = Color.FromArgb(160, 160, 160);

    public static Color Muted => IsDark ? DarkMuted : SystemColors.GrayText;
    public static Color Good => IsDark ? Color.FromArgb(110, 200, 120) : Color.ForestGreen;
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

    static void Paint(Control c)
    {
        switch (c)
        {
            case TextBox or ListBox or ComboBox:
                c.BackColor = IsDark ? DarkInput : SystemColors.Window;
                c.ForeColor = IsDark ? DarkText : SystemColors.WindowText;
                if (c is TextBox t) t.BorderStyle = IsDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                if (c is ListBox l) l.BorderStyle = IsDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                if (c is ComboBox cb) StyleComboBox(cb);
                DarkScrollBars(c);
                break;
            case Button b:
                b.FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                b.BackColor = IsDark ? DarkButton : SystemColors.Control;
                b.ForeColor = IsDark ? DarkText : SystemColors.ControlText;
                b.FlatAppearance.BorderColor = DarkBorder;
                b.UseVisualStyleBackColor = !IsDark;
                break;
            case Label or CheckBox:
                c.BackColor = Color.Transparent;
                if (Equals(c.Tag, "muted")) c.ForeColor = Muted;
                else if (!Equals(c.Tag, "keep")) c.ForeColor = IsDark ? DarkText : SystemColors.ControlText;
                break;
            default:
                c.BackColor = IsDark ? DarkBack : SystemColors.Control;
                c.ForeColor = IsDark ? DarkText : SystemColors.ControlText;
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
        cb.DrawMode = IsDark ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
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
        var back = selected ? (IsDark ? Color.FromArgb(62, 92, 140) : SystemColors.Highlight) : cb.BackColor;
        var fore = selected ? Color.White : cb.ForeColor;
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
