namespace StoryMaker;

/// <summary>Where the journals are, where the website is, which environment variables hold the keys.</summary>
public class SettingsForm : Form
{
    readonly Settings settings;
    readonly string? character;
    readonly TextBox wtf = new() { Width = 520 };
    readonly TextBox site = new() { Width = 520 };
    readonly TextBox tokenVar = new() { Width = 200 };
    readonly TextBox extra = new() { Width = 640, Height = 70, Multiline = true, ScrollBars = ScrollBars.Vertical };
    readonly Dictionary<AiKind, (TextBox key, TextBox url, Label found)> ai = [];

    public SettingsForm(Settings settings, string? character)
    {
        this.settings = settings;
        this.character = character;
        Text = "StoryMaker settings";
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Dock = DockStyle.Fill };
        void Row(string label, Control c, Control? after = null, Control? after2 = null)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            t.Controls.Add(c);
            t.Controls.Add(after ?? new Label());
            t.Controls.Add(after2 ?? new Label());
        }
        void Header(string text)
        {
            var l = new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 12, 0, 2) };
            t.Controls.Add(l);
            t.SetColumnSpan(l, 4);
        }
        void Note(string text)
        {
            var l = new Label { Text = text, AutoSize = true, MaximumSize = new Size(820, 0), ForeColor = SystemColors.GrayText };
            t.Controls.Add(l);
            t.SetColumnSpan(l, 4);
        }

        Header("WoW: Forever");
        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = wtf.Text, Description = "The WTF\\Account folder of WoW: Forever" };
            if (d.ShowDialog(this) == DialogResult.OK) wtf.Text = d.SelectedPath;
        };
        Row("WTF\\Account folder", wtf, browse);

        Header("Website");
        Row("Site address", site);
        var tokenFound = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        Row("Token variable", tokenVar, tokenFound);
        tokenVar.TextChanged += (_, _) => Found(tokenFound, tokenVar.Text, optional: false);
        Note("The token is the site's STORY_TOKEN. It is read from a Windows user environment variable and never stored by this program.");

        Header("AI (keys are read from user environment variables; set them with: setx NAME value)");
        t.Controls.Add(new Label { Text = "", AutoSize = true });
        t.Controls.Add(new Label { Text = "Key variable", AutoSize = true });
        t.Controls.Add(new Label { Text = "Address (only for OpenAI-compatible)", AutoSize = true });
        t.Controls.Add(new Label { Text = "", AutoSize = true });
        foreach (var kind in Enum.GetValues<AiKind>())
        {
            var p = settings.Profile(kind);
            var key = new TextBox { Width = 200, Text = p.KeyVariable };
            var url = new TextBox { Width = 320, Text = p.BaseUrl, Enabled = kind is AiKind.OpenAI or AiKind.OpenAICompatible };
            var found = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            key.TextChanged += (_, _) => Found(found, key.Text, optional: kind == AiKind.OpenAICompatible);
            Found(found, key.Text, optional: kind == AiKind.OpenAICompatible);
            ai[kind] = (key, url, found);
            t.Controls.Add(new Label { Text = AiProfile.Title(kind), AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            t.Controls.Add(key);
            t.Controls.Add(url);
            t.Controls.Add(found);
        }
        Note("A local AI (Ollama: http://localhost:11434/v1, LM Studio: http://localhost:1234/v1) needs no key; leave the variable empty.");

        Header("Wishes for every chapter (optional)");
        t.Controls.Add(extra);
        t.SetColumnSpan(extra, 4);

        if (character is not null && settings.Told.ContainsKey(character))
        {
            Header("Told sessions");
            var forget = new Button { Text = $"Forget which of {character}'s sessions are told", AutoSize = true };
            forget.Click += (_, _) =>
            {
                settings.Told.Remove(character);
                forget.Enabled = false;
                forget.Text = "Forgotten: every session counts as untold again";
            };
            t.Controls.Add(forget);
            t.SetColumnSpan(forget, 4);
        }

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;

        var outer = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
        outer.Controls.Add(t);
        outer.Controls.Add(buttons);
        Controls.Add(outer);

        wtf.Text = settings.WtfFolder;
        site.Text = settings.SiteUrl;
        tokenVar.Text = settings.SiteTokenVariable;
        extra.Text = settings.ExtraInstructions;
        Found(tokenFound, tokenVar.Text, optional: false);

        FormClosing += (_, _) =>
        {
            if (DialogResult != DialogResult.OK) return;
            settings.WtfFolder = wtf.Text.Trim();
            settings.SiteUrl = site.Text.Trim().TrimEnd('/');
            settings.SiteTokenVariable = tokenVar.Text.Trim();
            settings.ExtraInstructions = extra.Text.Trim();
            foreach (var (kind, c) in ai)
            {
                var p = settings.Profile(kind);
                p.KeyVariable = c.key.Text.Trim();
                p.BaseUrl = c.url.Text.Trim();
            }
        };
    }

    static void Found(Label label, string variable, bool optional)
    {
        if (string.IsNullOrWhiteSpace(variable))
        {
            label.Text = optional ? "no key needed" : "name a variable";
            label.ForeColor = optional ? SystemColors.GrayText : Color.Firebrick;
        }
        else if (Settings.Secret(variable) is not null)
        {
            label.Text = "found";
            label.ForeColor = Color.ForestGreen;
        }
        else
        {
            label.Text = "not set";
            label.ForeColor = Color.Firebrick;
        }
    }
}
