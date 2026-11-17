using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Markdig;

namespace StoryMaker;

/// <summary>
/// Pictures and sounds in a chapter. While you edit, a chapter points at files on this PC:
///   ![The road to Razor Hill](&lt;D:/Blizzard/World of Warcraft/_forever_/Screenshots/WoWScrnShot_111726_085709.jpg&gt;)
///   ![Wind at the docks](&lt;C:/Users/me/Music/wind.mp3&gt;)      (a "picture" that is a sound file is a sound)
/// Paths use forward slashes: in Markdown "\_" would turn into "_" and break the path.
/// When the chapter is posted the files are uploaded and the chapter points at the website instead;
/// when it is saved on this PC they are copied into a "media" folder next to it.
/// </summary>
public static partial class Media
{
    public static readonly string[] PictureExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    public static readonly string[] SoundExtensions = [".mp3", ".ogg", ".oga", ".wav", ".m4a"];
    const int MaxWidth = 1600;   // screenshots are made smaller than this before they leave the PC

    public static bool IsSound(string path) => SoundExtensions.Contains(Path.GetExtension(path.Split('?')[0]).ToLowerInvariant());

    /// <summary>The Markdown for a picture or a sound on its own line.</summary>
    public static string Markdown(string path, string caption)
    {
        caption = caption.Replace("[", "(").Replace("]", ")").Replace("\r", " ").Replace("\n", " ").Trim();
        return $"![{caption}](<{path.Replace('\\', '/')}>)";
    }

    // ![caption](<D:/...>) or ![caption](D:/...)
    [GeneratedRegex(@"!\[(?<caption>[^\]]*)\]\((?:<(?<path>[A-Za-z]:/[^>]+)>|(?<path>[A-Za-z]:/[^)\s]+))\)")]
    private static partial Regex LocalReference();

    /// <summary>The files on this PC the chapter points at (each once), that exist.</summary>
    public static List<string> LocalFiles(string markdown) =>
        LocalReference().Matches(markdown).Select(m => m.Groups["path"].Value).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => File.Exists(p)).ToList();

    /// <summary>Files on this PC the chapter points at that aren't there (any more).</summary>
    public static List<string> MissingFiles(string markdown) =>
        LocalReference().Matches(markdown).Select(m => m.Groups["path"].Value).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => !File.Exists(p)).ToList();

    /// <summary>Points every reference to <paramref name="path"/> at <paramref name="address"/> instead.</summary>
    public static string Replace(string markdown, string path, string address) =>
        LocalReference().Replace(markdown, m => string.Equals(m.Groups["path"].Value, path, StringComparison.OrdinalIgnoreCase)
            ? $"![{m.Groups["caption"].Value}]({(address.Contains(' ') ? "<" + address + ">" : address)})"
            : m.Value);

    /// <summary>
    /// The file as it should be sent or saved: big pictures are made at most 1600 pixels wide (JPEG
    /// pictures stay JPEG, others become PNG); sounds and small pictures are sent as they are.
    /// </summary>
    public static (string Name, byte[] Data) Prepare(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!PictureExtensions.Contains(ext) || ext == ".gif") return (name, File.ReadAllBytes(path));
        using var original = Image.FromFile(path);
        if (original.Width <= MaxWidth) return (name, File.ReadAllBytes(path));

        var height = (int)Math.Round(original.Height * (double)MaxWidth / original.Width);
        using var small = new Bitmap(MaxWidth, height);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, 0, 0, MaxWidth, height);
        }
        using var ms = new MemoryStream();
        if (ext is ".jpg" or ".jpeg")
        {
            var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(Encoder.Quality, 86L);
            small.Save(ms, jpeg, p);
        }
        else
        {
            small.Save(ms, ImageFormat.Png);
            name = Path.ChangeExtension(name, ".png");
        }
        return (name, ms.ToArray());
    }

    /// <summary>
    /// For saving on this PC: copies each file into &lt;folder&gt;\media and points the chapter at
    /// "media/&lt;file&gt;". A file already there with other content gets a new name.
    /// </summary>
    public static string CopyNextTo(string markdown, string folder)
    {
        foreach (var path in LocalFiles(markdown))
        {
            var (name, data) = Prepare(path);
            var mediaDir = Path.Combine(folder, "media");
            Directory.CreateDirectory(mediaDir);
            var target = Path.Combine(mediaDir, name);
            for (int i = 2; File.Exists(target) && !File.ReadAllBytes(target).AsSpan().SequenceEqual(data); i++)
                target = Path.Combine(mediaDir, $"{Path.GetFileNameWithoutExtension(name)}-{i}{Path.GetExtension(name)}");
            if (!File.Exists(target)) File.WriteAllBytes(target, data);
            markdown = Replace(markdown, path, "media/" + Path.GetFileName(target));
        }
        return markdown;
    }

    // ---------------------------------------------------------------- preview

    // the same as the website: Markdig's own media links off, sounds get a player with their caption
    static readonly MarkdownPipeline Pipeline = BuildPipeline();

    static MarkdownPipeline BuildPipeline()
    {
        var b = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml();
        b.Extensions.RemoveAll(e => e is Markdig.Extensions.MediaLinks.MediaLinkExtension);
        return b.Build();
    }

    [GeneratedRegex("<p><img src=\"([^\"]+)\" alt=\"([^\"]*)\" /></p>")]
    private static partial Regex LoneImage();

    [GeneratedRegex("src=\"([A-Za-z]:/[^\"]*)\"")]
    private static partial Regex LocalSrc();

    /// <summary>
    /// The chapter as a web page, drawn like the website draws it: pictures with captions, sounds as
    /// players. <paramref name="baseFolder"/> is where relative addresses (media/...) are looked for.
    /// </summary>
    public static string PreviewHtml(string markdown, bool dark, string? baseFolder)
    {
        var html = Markdig.Markdown.ToHtml(markdown, Pipeline);
        html = LoneImage().Replace(html, m =>
        {
            var src = m.Groups[1].Value;
            var caption = m.Groups[2].Value;
            var sound = IsSound(src);
            var media = sound
                ? $"<audio controls preload=\"none\" src=\"{src}\"></audio>"
                : $"<img src=\"{src}\" alt=\"{caption}\" />";
            return $"<figure class=\"{(sound ? "sound" : "picture")}\">{media}{(caption.Length > 0 ? $"<figcaption>{caption}</figcaption>" : "")}</figure>";
        });
        html = LocalSrc().Replace(html, m => $"src=\"file:///{m.Groups[1].Value}\"");

        var (bg, panel, text, muted, accent, line) = dark
            ? ("#081428", "#0e2147", "#d5dceb", "#94a2bd", "#c9a36b", "#17366b")     // lundqwist.se
            : ("#f6f1e7", "#e9e0cd", "#1c2438", "#6b6f7d", "#8a6428", "#c8b896");
        var baseTag = baseFolder is null ? "" : $"<base href=\"{new Uri(baseFolder.TrimEnd('\\') + "\\").AbsoluteUri}\">";
        return $$"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"><meta http-equiv="X-UA-Compatible" content="IE=edge">{{baseTag}}
            <style>
              body { background: {{bg}}; color: {{text}}; font: 17px/1.75 Georgia, "Palatino Linotype", serif; margin: 0; padding: 18px 26px 40px; }
              main { max-width: 46em; }
              h1, h2, h3 { color: {{accent}}; font-weight: normal; letter-spacing: .04em; margin: 0 0 .3em; }
              h3 { font-size: 1.6em; text-transform: uppercase; }
              h3 + p em:only-child { color: {{muted}}; font-size: .9em; }
              a { color: {{accent}}; }
              blockquote { margin: 1em 0; padding-left: 1em; border-left: 3px solid {{accent}}; color: {{muted}}; font-style: italic; }
              hr { border: 0; text-align: center; margin: 1.6em 0; height: 1em; }
              hr:after { content: "\2726   \2726   \2726"; color: {{accent}}; }
              figure { margin: 1.4em 0; }
              figure img { max-width: 100%; border: 1px solid {{line}}; border-radius: 4px; display: block; }
              figcaption { color: {{muted}}; font-style: italic; font-size: .9em; text-align: center; margin-top: .35em; }
              figure.sound { background: {{panel}}; border: 1px solid {{line}}; border-left: 3px solid {{accent}}; padding: .6em .9em; border-radius: 4px; }
              figure.sound figcaption { text-align: left; }
              audio { width: 100%; }
              img { max-width: 100%; }
            </style></head>
            <body><main>{{html}}</main></body></html>
            """;
    }
}
