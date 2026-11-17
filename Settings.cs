using System.Text.Json;

namespace StoryMaker;

// The numbers are stored in the settings file: add new ones at the end
public enum AiKind { Claude, OpenAI, Gemini, OpenAICompatible, Ollama }

/// <summary>One AI the program can use. Keys are never stored: only the name of the environment variable that holds it.</summary>
public class AiProfile
{
    public AiKind Kind { get; set; }
    public string Model { get; set; } = "";
    public string KeyVariable { get; set; } = "";
    /// <summary>For OpenAI, OpenAI-compatible servers and Ollama.</summary>
    public string BaseUrl { get; set; } = "";

    public static string Title(AiKind kind) => kind switch
    {
        AiKind.Claude => "Claude (Anthropic)",
        AiKind.OpenAI => "OpenAI",
        AiKind.Gemini => "Gemini (Google)",
        AiKind.Ollama => "Ollama (free, runs on this PC)",
        _ => "OpenAI-compatible (LM Studio, OpenRouter...)",
    };

    public static string[] SuggestedModels(AiKind kind) => kind switch
    {
        AiKind.Claude => ["claude-opus-5-5", "claude-sonnet-5-5", "claude-fable-5-2", "claude-haiku-5-5"],
        AiKind.OpenAI => ["gpt-5"],
        AiKind.Gemini => ["gemini-2.5-pro", "gemini-2.5-flash"],
        AiKind.Ollama => ["gemma3:12b"],
        _ => ["llama3.1"],
    };

    public static AiProfile Default(AiKind kind) => kind switch
    {
        AiKind.Claude => new() { Kind = kind, Model = "claude-opus-5-5", KeyVariable = "ANTHROPIC_API_KEY" },
        AiKind.OpenAI => new() { Kind = kind, Model = "gpt-5", KeyVariable = "OPENAI_API_KEY", BaseUrl = "https://api.openai.com/v1" },
        AiKind.Gemini => new() { Kind = kind, Model = "gemini-2.5-pro", KeyVariable = "GEMINI_API_KEY" },
        AiKind.Ollama => new() { Kind = kind, Model = "gemma3:12b", KeyVariable = "", BaseUrl = "http://localhost:11434" },
        _ => new() { Kind = kind, Model = "llama3.1", KeyVariable = "", BaseUrl = "http://localhost:1234/v1" },
    };
}

public class Settings
{
    public string WtfFolder { get; set; } = @"D:\Blizzard\World of Warcraft\_forever_\WTF\Account";
    /// <summary>Where pictures are picked from; empty = the game's Screenshots folder next to WTF.</summary>
    public string ScreenshotFolder { get; set; } = "";
    public string SiteUrl { get; set; } = "https://lundqwist.se/wowcharacters";
    public string SiteTokenVariable { get; set; } = "STORY_TOKEN";
    public AiKind Ai { get; set; } = AiKind.Ollama;
    public Dictionary<AiKind, AiProfile> Profiles { get; set; } = [];
    /// <summary>Character slug -> id of the last session that is in a story.</summary>
    public Dictionary<string, long> Told { get; set; } = [];
    public string LastCharacter { get; set; } = "";
    /// <summary>Character slug -> the folder its chapters were last saved in on this PC.</summary>
    public Dictionary<string, string> SaveFolders { get; set; } = [];
    /// <summary>"System" (follow Windows), "Light" or "Dark".</summary>
    public string Theme { get; set; } = "System";
    /// <summary>Extra wishes for the AI, added to every request (optional).</summary>
    public string ExtraInstructions { get; set; } = "";

    public AiProfile Profile(AiKind kind)
    {
        if (!Profiles.TryGetValue(kind, out var p)) Profiles[kind] = p = AiProfile.Default(kind);
        return p;
    }

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LunarJournal");
    static string FilePath => Path.Combine(Folder, "StoryMaker.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch (JsonException) { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    /// <summary>A user (or machine) environment variable; the user one wins, so keys set with setx work without a restart of Windows.</summary>
    public static string? Secret(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
             ?? Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
