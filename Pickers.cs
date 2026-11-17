namespace StoryMaker;

/// <summary>
/// Pick a picture from the game's Screenshots folder (newest first, as thumbnails), or any other picture,
/// and give it a caption.
/// </summary>
public class ScreenshotPicker : Form
{
    const int ThumbW = 192, ThumbH = 108, MaxShown = 400;

    readonly ListView list = new() { Dock = DockStyle.Fill, View = View.LargeIcon, MultiSelect = false, HideSelection = false };
    readonly ImageList thumbs = new() { ImageSize = new Size(ThumbW, ThumbH), ColorDepth = ColorDepth.Depth32Bit };
    readonly TextBox caption = new() { Width = 520, PlaceholderText = "Caption, shown under the picture (optional)" };
    readonly Label folderLabel = new() { AutoSize = true, Tag = "muted", Padding = new Padding(0, 6, 0, 0) };
    readonly Button insert = new() { Text = "Insert", Width = 100, Enabled = false };
    readonly CancellationTokenSource loading = new();

    public string? ChosenPath { get; private set; }
    public string Caption => caption.Text.Trim();

    public ScreenshotPicker(string folder)
    {
        Text = "Insert a picture";
        if (Theme.AppIcon is { } icon) Icon = icon;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 720);
        MinimumSize = new Size(600, 400);
        list.LargeImageList = thumbs;

        var other = new Button { Text = "Other picture...", AutoSize = true };
        var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), WrapContents = false };
        bottom.Controls.AddRange([new Label { Text = "Caption", AutoSize = true, Padding = new Padding(0, 6, 4, 0) }, caption, insert, cancel, other]);
        var top = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 4, 8, 0) };
        top.Controls.Add(folderLabel);
        Controls.Add(list);
        Controls.Add(bottom);
        Controls.Add(top);
        AcceptButton = insert;
        CancelButton = cancel;

        list.SelectedIndexChanged += (_, _) => insert.Enabled = list.SelectedItems.Count > 0;
        list.ItemActivate += (_, _) => Choose();
        insert.Click += (_, _) => Choose();
        other.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Title = "Choose a picture", Filter = "Pictures|*.jpg;*.jpeg;*.png;*.webp;*.gif|All files|*.*" };
            if (Directory.Exists(folder)) d.InitialDirectory = folder;
            if (d.ShowDialog(this) != DialogResult.OK) return;
            ChosenPath = d.FileName;
            DialogResult = DialogResult.OK;
        };
        FormClosed += (_, _) => loading.Cancel();
        Theme.Apply(this);
        Load += (_, _) => Fill(folder);
    }

    void Choose()
    {
        if (list.SelectedItems.Count == 0) return;
        ChosenPath = (string)list.SelectedItems[0].Tag!;
        DialogResult = DialogResult.OK;
    }

    async void Fill(string folder)
    {
        if (!Directory.Exists(folder))
        {
            folderLabel.Text = $"No screenshots folder at {folder}. Take screenshots in the game (Print Screen), or use Other picture.";
            return;
        }
        var files = new DirectoryInfo(folder).EnumerateFiles()
            .Where(f => Media.PictureExtensions.Contains(f.Extension.ToLowerInvariant()))
            .OrderByDescending(f => f.LastWriteTime).Take(MaxShown).ToList();
        folderLabel.Text = files.Count == 0
            ? $"No screenshots in {folder} yet. Take some in the game with Print Screen."
            : $"{folder}   ·   newest first, double-click to insert";
        foreach (var f in files)
        {
            list.Items.Add(new ListViewItem($"{f.LastWriteTime:yyyy-MM-dd HH:mm}") { Tag = f.FullName, ImageKey = f.FullName });
        }
        if (list.Items.Count > 0) list.Items[0].Selected = true;
        // thumbnails a few at a time, without holding up the window
        foreach (var f in files)
        {
            if (loading.IsCancellationRequested) return;
            Bitmap? thumb = await Task.Run(() => Thumbnail(f.FullName));
            if (loading.IsCancellationRequested || IsDisposed) { thumb?.Dispose(); return; }
            if (thumb is not null) thumbs.Images.Add(f.FullName, thumb);
        }
    }

    static Bitmap? Thumbnail(string path)
    {
        try
        {
            using var img = Image.FromFile(path);
            var bmp = new Bitmap(ThumbW, ThumbH);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Black);
            var scale = Math.Min((double)ThumbW / img.Width, (double)ThumbH / img.Height);
            int w = (int)(img.Width * scale), h = (int)(img.Height * scale);
            g.DrawImage(img, (ThumbW - w) / 2, (ThumbH - h) / 2, w, h);
            return bmp;
        }
        catch (Exception) { return null; }
    }
}

/// <summary>A small window that asks for one or two lines of text.</summary>
public class Prompt : Form
{
    readonly TextBox first = new() { Width = 460 };
    readonly TextBox? second;

    Prompt(string title, string label, string value, string? label2, string value2)
    {
        Text = title;
        if (Theme.AppIcon is { } icon) Icon = icon;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
        first.Text = value;
        t.Controls.Add(first);
        if (label2 is not null)
        {
            second = new TextBox { Width = 460, Text = value2 };
            t.Controls.Add(new Label { Text = label2, AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            t.Controls.Add(second);
        }
        var ok = new Button { Text = "OK", Width = 90, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);
        t.Controls.Add(buttons);
        t.SetColumnSpan(buttons, 2);
        Controls.Add(t);
        AcceptButton = ok;
        CancelButton = cancel;
        Theme.Apply(this);
    }

    public static string? Ask(IWin32Window owner, string title, string label, string value = "")
    {
        using var p = new Prompt(title, label, value, null, "");
        return p.ShowDialog(owner) == DialogResult.OK ? p.first.Text : null;
    }

    public static (string, string)? Ask2(IWin32Window owner, string title, string label, string value, string label2, string value2)
    {
        using var p = new Prompt(title, label, value, label2, value2);
        return p.ShowDialog(owner) == DialogResult.OK ? (p.first.Text, p.second!.Text) : null;
    }
}
