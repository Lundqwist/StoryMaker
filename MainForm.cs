using System.Diagnostics;
using System.Text.Json.Nodes;

namespace StoryMaker;

/// <summary>
/// Pick a character, tick the play sessions the chapter should cover, pick an AI, write, read and edit
/// the chapter, post it to the character's story page.
/// </summary>
public class MainForm : Form
{
    const string Game = "forever";   // Lunar Journal is a WoW: Forever addon

    readonly Settings settings = Settings.Load();
    Journal? journal;
    string? lastPosted;     // the page of the chapter just posted, for the Open button
    CancellationTokenSource? running;

    readonly ComboBox characterBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    readonly ComboBox aiBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly ComboBox modelBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 190 };
    readonly Button settingsButton = new() { Text = "Settings...", AutoSize = true };
    readonly Button reloadButton = new() { Text = "Read journals again", AutoSize = true };

    readonly CheckedListBox sessionList = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    readonly CheckBox showTold = new() { Text = "Show sessions already in the story", AutoSize = true, Dock = DockStyle.Bottom };
    readonly Label journalInfo = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 56, Tag = "muted" };

    readonly TextBox wishes = new() { Dock = DockStyle.Top, Multiline = true, Height = 46, ScrollBars = ScrollBars.Vertical, PlaceholderText = "Wishes for this chapter (optional), e.g. \"make it about the night at the Crossroads\"" };
    readonly TextBox chapter = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AcceptsTab = false,
        Font = new Font("Georgia", 11f), PlaceholderText = "The chapter appears here. You can edit it before you post it.",
    };

    readonly Button writeButton = new() { Text = "Write chapter", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
    readonly Button postButton = new() { Text = "Post to lundqwist.se", AutoSize = true, Padding = new Padding(8, 2, 8, 2), Enabled = false };
    readonly Button saveButton = new() { Text = "Save as Markdown...", AutoSize = true, Padding = new Padding(8, 2, 8, 2), Enabled = false };
    readonly Button openButton = new() { Text = "Open the story page", AutoSize = true };
    readonly Label status = new() { AutoSize = true, Padding = new Padding(10, 7, 0, 0) };

    public MainForm()
    {
        Text = "Lunar Journal - StoryMaker";
        Width = 1250;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        if (Theme.AppIcon is { } icon) Icon = icon;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), WrapContents = false };
        top.Controls.AddRange([Lbl("Character"), characterBox, Lbl("AI"), aiBox, Lbl("Model"), modelBox, settingsButton, reloadButton]);

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        left.Controls.Add(sessionList);
        left.Controls.Add(new Label { Text = "Play sessions for this chapter (ticked = used)", Dock = DockStyle.Top, Height = 22 });
        left.Controls.Add(showTold);
        left.Controls.Add(journalInfo);

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        right.Controls.Add(chapter);
        right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6 });
        right.Controls.Add(wishes);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 430, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6), WrapContents = false };
        bottom.Controls.AddRange([writeButton, postButton, saveButton, openButton, status]);

        Controls.Add(split);
        Controls.Add(bottom);
        Controls.Add(top);
        Shown += (_, _) => split.SplitterDistance = 430;

        foreach (var kind in Enum.GetValues<AiKind>()) aiBox.Items.Add(AiProfile.Title(kind));
        aiBox.SelectedIndex = (int)settings.Ai;
        FillModels();

        aiBox.SelectedIndexChanged += (_, _) => { settings.Ai = (AiKind)aiBox.SelectedIndex; FillModels(); settings.Save(); };
        modelBox.TextChanged += (_, _) => { settings.Profile(settings.Ai).Model = modelBox.Text.Trim(); };
        modelBox.Leave += (_, _) => settings.Save();
        characterBox.SelectedIndexChanged += (_, _) => LoadCharacter();
        showTold.CheckedChanged += (_, _) => FillSessions();
        reloadButton.Click += (_, _) => FindJournals();
        settingsButton.Click += (_, _) => OpenSettings();
        writeButton.Click += async (_, _) => await WriteChapter();
        postButton.Click += async (_, _) => await PostChapter();
        saveButton.Click += (_, _) => SaveChapter();
        openButton.Click += (_, _) => OpenPage();
        chapter.TextChanged += (_, _) => postButton.Enabled = saveButton.Enabled = running is null && chapter.Text.Trim().Length > 0 && journal is not null;
        FormClosing += (_, _) => { running?.Cancel(); settings.Save(); };

        Theme.Choose(settings.Theme);
        Theme.Apply(this);
        FindJournals();
    }

    static Label Lbl(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(8, 6, 0, 0) };

    async void FillModels()
    {
        var kind = settings.Ai;
        var p = settings.Profile(kind);
        modelBox.Items.Clear();
        modelBox.Items.AddRange(AiProfile.SuggestedModels(kind));
        modelBox.Text = p.Model;
        if (kind != AiKind.Ollama) return;
        // Offer the models Ollama really has
        var installed = await OllamaWriter.Installed(string.IsNullOrWhiteSpace(p.BaseUrl) ? OllamaWriter.DefaultUrl : p.BaseUrl);
        if (settings.Ai != kind) return;
        foreach (var m in installed) if (!modelBox.Items.Contains(m)) modelBox.Items.Add(m);
        if (installed.Count == 0) status.Text = "Couldn't reach Ollama, or it has no models yet. Start Ollama from the Start menu, or download a model: ollama pull gemma3:12b";
    }

    void FindJournals()
    {
        var files = Journal.Find(settings.WtfFolder);
        characterBox.Items.Clear();
        foreach (var f in files) characterBox.Items.Add(f);
        if (files.Count == 0)
        {
            journal = null;
            sessionList.Items.Clear();
            journalInfo.Text = $"No Lunar Journal saves found under {settings.WtfFolder}. Play with the addon, log out or /reload, then press \"Read journals again\".";
            return;
        }
        var last = files.FindIndex(f => f.Slug == settings.LastCharacter);
        characterBox.SelectedIndex = last >= 0 ? last : 0;
    }

    void LoadCharacter()
    {
        if (characterBox.SelectedItem is not JournalFile file) return;
        try
        {
            journal = Journal.Load(file);
        }
        catch (Exception e)
        {
            journal = null;
            sessionList.Items.Clear();
            journalInfo.Text = "Couldn't read the journal: " + e.Message;
            return;
        }
        lastPosted = null;
        openButton.Text = "Open the story page";
        settings.LastCharacter = file.Slug;
        settings.Save();
        FillSessions();
    }

    long Told => journal is not null && settings.Told.TryGetValue(journal.File.Slug, out var t) ? t : 0;

    record SessionItem(JsonObject Session, bool IsTold)
    {
        public override string ToString() => Journal.Describe(Session) + (IsTold ? "   (in the story)" : "");
    }

    void FillSessions()
    {
        sessionList.Items.Clear();
        if (journal is null) return;
        var told = Told;
        int untold = 0;
        foreach (var s in journal.Sessions)
        {
            var isTold = Journal.Id(s) <= told;
            if (isTold && !showTold.Checked) continue;
            sessionList.Items.Add(new SessionItem(s, isTold), !isTold);
            if (!isTold) untold++;
        }
        var info = journal.Character;
        journalInfo.Text =
            $"{info["name"]}, level {info["level"]} {info["race"]} {info["class"]}{(info["guild"] is { } g ? $" of <{g}>" : "")}. " +
            $"{journal.Sessions.Count} {(journal.Sessions.Count == 1 ? "session" : "sessions")} in the journal, {untold} not in the story yet.\n" +
            $"Saved by the game {journal.SavedAt:yyyy-MM-dd HH:mm} (it saves at logout and /reload).";
    }

    List<JsonObject> Ticked() => sessionList.CheckedItems.OfType<SessionItem>().Select(i => i.Session).ToList();

    void Busy(string? message)
    {
        bool busy = message is not null;
        writeButton.Text = busy ? "Stop" : "Write chapter";
        postButton.Enabled = saveButton.Enabled = !busy && chapter.Text.Trim().Length > 0 && journal is not null;
        characterBox.Enabled = aiBox.Enabled = modelBox.Enabled = settingsButton.Enabled = reloadButton.Enabled = !busy;
        UseWaitCursor = busy;
        if (busy) status.Text = message;
    }

    SiteClient SiteApi() => new(settings.SiteUrl, Settings.Secret(settings.SiteTokenVariable));

    async Task WriteChapter()
    {
        if (running is not null) { running.Cancel(); return; }
        if (journal is null) return;
        var sessions = Ticked();
        if (sessions.Count == 0) { status.Text = "Tick at least one play session."; return; }
        if (chapter.Text.Trim().Length > 0 &&
            MessageBox.Show(this, "Write a new chapter? The text in the box will be replaced.", Text, MessageBoxButtons.OKCancel) != DialogResult.OK) return;

        var profile = settings.Profile(settings.Ai);
        profile.Model = modelBox.Text.Trim();
        settings.Save();
        running = new CancellationTokenSource();
        try
        {
            var writer = Writers.For(profile);

            Busy("Reading the story so far...");
            string previous;
            var saved = SavedChapters(journal.File);
            if (Settings.Secret(settings.SiteTokenVariable) is null && saved.Count > 0)
            {
                // No website set up: the chapters saved on this PC are the story so far
                previous = string.Join("\n\n", saved.Select(File.ReadAllText));
            }
            else
            {
                try
                {
                    previous = await SiteApi().Story(Game, journal.File.Slug, running.Token);
                }
                catch (AiException e)
                {
                    var fallback = saved.Count > 0
                        ? $"\n\nWrite the chapter from the {saved.Count} chapter(s) saved on this PC instead?"
                        : "\n\nWrite the chapter without knowing the story so far?";
                    if (MessageBox.Show(this, e.Message + fallback, Text,
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) { status.Text = ""; return; }
                    previous = string.Join("\n\n", saved.Select(File.ReadAllText));
                }
            }

            Busy($"Writing with {AiProfile.Title(profile.Kind).Split(' ')[0]} ({profile.Model})... this can take a minute or two.");
            var user = StoryPrompt.User(journal.Digest(sessions), previous, (settings.ExtraInstructions + "\n" + wishes.Text).Trim());
            var started = DateTime.Now;
            var text = await writer.Write(StoryPrompt.System, user, running.Token);
            chapter.Text = StoryPrompt.Clean(text);
            status.Text = $"Written in {(DateTime.Now - started).TotalSeconds:0} s by {profile.Model}. Read it, edit it if you like, then post or save it.";
        }
        catch (OperationCanceledException)
        {
            status.Text = "Stopped.";
        }
        catch (AiException e)
        {
            status.Text = "";
            MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception e)
        {
            status.Text = "";
            MessageBox.Show(this, "Something went wrong: " + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            running.Dispose();
            running = null;
            Busy(null);
        }
    }

    async Task PostChapter()
    {
        if (journal is null || chapter.Text.Trim().Length == 0) return;
        var site = SiteApi();
        var ticked = Ticked();
        var page = site.PageUrl(Game, journal.File.Slug);
        var question = $"Post this as the next chapter of {journal.File.Character}'s story?\n\n{page}\n\n" +
            (ticked.Count > 0 ? $"The {ticked.Count} ticked session(s) will then count as told." : "No sessions are ticked, so none will count as told.");
        if (MessageBox.Show(this, question, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

        running = new CancellationTokenSource();
        try
        {
            Busy("Posting...");
            var number = await site.Post(Game, journal.File.Slug, chapter.Text, running.Token);
            if (ticked.Count > 0)
            {
                settings.Told[journal.File.Slug] = Math.Max(Told, ticked.Max(Journal.Id));
                settings.Save();
            }
            chapter.Text = "";
            wishes.Text = "";
            FillSessions();
            lastPosted = number > 0 ? site.ChapterUrl(Game, journal.File.Slug, number) : page;
            status.Text = number > 0 ? $"Posted as chapter {number}: {lastPosted}" : $"Posted to {page}";
            openButton.Text = "Open the new chapter";
        }
        catch (AiException e)
        {
            status.Text = "";
            MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            running.Dispose();
            running = null;
            Busy(null);
        }
    }

    // ---------------------------------------------------------------- saving on this PC

    static readonly string DefaultSaveRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lunar Journal");

    /// <summary>The folder a character's chapters are saved in: the last one used, or Documents\Lunar Journal\&lt;Character&gt;.</summary>
    string SaveFolder(JournalFile file) =>
        settings.SaveFolders.TryGetValue(file.Slug, out var dir) && Directory.Exists(dir) ? dir : Path.Combine(DefaultSaveRoot, file.Character);

    /// <summary>Chapters already saved for the character, in order (file names start with their number).</summary>
    List<string> SavedChapters(JournalFile file)
    {
        var dir = SaveFolder(file);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.md").Order(StringComparer.OrdinalIgnoreCase).ToList() : [];
    }

    /// <summary>"002 The Road to Razor Hill.md": the chapter's number and its title (the first # line).</summary>
    public static string SuggestedName(int number, string text)
    {
        var title = text.Replace("\r\n", "\n").Split('\n').FirstOrDefault(l => l.StartsWith('#'))?.TrimStart('#').Trim();
        if (string.IsNullOrWhiteSpace(title)) title = "Chapter";
        var safe = new string(title.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? ' ' : ch).ToArray()).Trim();
        safe = System.Text.RegularExpressions.Regex.Replace(safe, @"\s+", " ");
        if (safe.Length > 60) safe = safe[..60].Trim();
        return $"{number:000} {safe}.md";
    }

    /// <summary>Markdown as it is on the website: UTF-8 without BOM, plain line ends.</summary>
    public static void WriteMarkdown(string path, string text) =>
        File.WriteAllText(path, text.Replace("\r\n", "\n").Trim() + "\n", new System.Text.UTF8Encoding(false));

    void SaveChapter()
    {
        if (journal is null || chapter.Text.Trim().Length == 0) return;
        var dir = SaveFolder(journal.File);
        Directory.CreateDirectory(dir);

        using var dialog = new SaveFileDialog
        {
            Title = $"Save {journal.File.Character}'s chapter",
            InitialDirectory = dir,
            FileName = SuggestedName(SavedChapters(journal.File).Count + 1, chapter.Text),
            Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files|*.*",
            DefaultExt = "md",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        WriteMarkdown(dialog.FileName, chapter.Text);
        settings.SaveFolders[journal.File.Slug] = Path.GetDirectoryName(dialog.FileName)!;

        var ticked = Ticked();
        if (ticked.Count > 0 && MessageBox.Show(this,
                $"Saved.\n\nCount the {ticked.Count} ticked session(s) as told, so they aren't ticked for the next chapter?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            settings.Told[journal.File.Slug] = Math.Max(Told, ticked.Max(Journal.Id));
            FillSessions();
        }
        settings.Save();
        status.Text = $"Saved to {dialog.FileName}";
    }

    void OpenPage()
    {
        if (journal is null) return;
        Process.Start(new ProcessStartInfo(lastPosted ?? SiteApi().PageUrl(Game, journal.File.Slug)) { UseShellExecute = true });
    }

    void OpenSettings()
    {
        using var form = new SettingsForm(settings, journal?.File.Slug);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        settings.Save();
        FillModels();
        Theme.Choose(settings.Theme);
        Theme.Apply(this);
        FindJournals();
    }
}
