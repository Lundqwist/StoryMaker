using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StoryMaker;

/// <summary>
/// Reads the files WoW writes for saved variables (Lua table literals) into JSON nodes.
/// WoW writes them in a fixed, simple form: ["key"] = value, nested { }, numbers, strings,
/// true/false/nil, and "-- [n]" comments after list items.
/// </summary>
public static partial class LuaReader
{
    [GeneratedRegex(@"\G(?:(?<ws>\s+|--[^\n]*)|(?<str>""(?:\\.|[^""\\])*"")|(?<num>-?(?:\d+\.?\d*(?:[eE][-+]?\d+)?|\.\d+|inf|nan))|(?<name>[A-Za-z_][A-Za-z0-9_]*)|(?<sym>[{}\[\]=,;]))", RegexOptions.Singleline)]
    private static partial Regex Token();

    record Tok(char Kind, string Text);   // s=string, n=number, w=word, y=symbol

    public static Dictionary<string, JsonNode?> ReadFile(string text)
    {
        var toks = Tokens(text);
        int i = 0;
        var result = new Dictionary<string, JsonNode?>();
        while (i < toks.Count)
        {
            var name = toks[i++].Text;
            i++; // =
            result[name] = Value(toks, ref i);
        }
        return result;
    }

    static List<Tok> Tokens(string text)
    {
        var list = new List<Tok>();
        int pos = 0;
        var re = Token();
        while (pos < text.Length)
        {
            var m = re.Match(text, pos);
            if (!m.Success || m.Length == 0)
                throw new FormatException($"Can't read the saved file at character {pos}: {text.Substring(pos, Math.Min(40, text.Length - pos))}");
            pos += m.Length;
            if (m.Groups["str"].Success) list.Add(new('s', Unquote(m.Groups["str"].Value)));
            else if (m.Groups["num"].Success) list.Add(new('n', m.Groups["num"].Value));
            else if (m.Groups["name"].Success) list.Add(new('w', m.Groups["name"].Value));
            else if (m.Groups["sym"].Success) list.Add(new('y', m.Groups["sym"].Value));
        }
        return list;
    }

    // The file is UTF-8 read as Latin-1, so every byte is one char; \ddd escapes are bytes too.
    // Turned back into real text at the end.
    static string Unquote(string s)
    {
        var bytes = new List<byte>();
        for (int i = 1; i < s.Length - 1; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length - 1)
            {
                char n = s[++i];
                if (char.IsDigit(n))
                {
                    int j = i, v = 0;
                    while (j < s.Length - 1 && j < i + 3 && char.IsDigit(s[j])) v = v * 10 + (s[j++] - '0');
                    bytes.Add((byte)v);
                    i = j - 1;
                    continue;
                }
                bytes.Add((byte)(n switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => n }));
                continue;
            }
            bytes.Add((byte)c);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    static JsonNode? Value(List<Tok> toks, ref int i)
    {
        var t = toks[i++];
        switch (t.Kind)
        {
            case 's': return JsonValue.Create(t.Text);
            case 'n':
                if (t.Text is "inf" or "nan" or "-inf") return null;
                if (long.TryParse(t.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return JsonValue.Create(l);
                return JsonValue.Create(double.Parse(t.Text, CultureInfo.InvariantCulture));
            case 'w': return t.Text switch { "true" => JsonValue.Create(true), "false" => JsonValue.Create(false), _ => null };
        }
        if (t.Text != "{") throw new FormatException($"Unexpected '{t.Text}' in the saved file.");

        var items = new List<KeyValuePair<object, JsonNode?>>();
        long n = 0;
        while (toks[i].Text != "}" || toks[i].Kind != 'y')
        {
            if (toks[i].Kind == 'y' && toks[i].Text == "[")
            {
                i++;
                var keyTok = toks[i];
                var key = Value(toks, ref i);
                i++; // ]
                i++; // =
                object k = keyTok.Kind == 'n' && long.TryParse(keyTok.Text, out var kn) ? kn : (key?.ToString() ?? "");
                items.Add(new(k, Value(toks, ref i)));
            }
            else if (toks[i].Kind == 'w' && toks[i + 1].Text == "=")
            {
                var k = toks[i].Text;
                i += 2;
                items.Add(new(k, Value(toks, ref i)));
            }
            else
            {
                items.Add(new(++n, Value(toks, ref i)));
            }
            if (toks[i].Kind == 'y' && toks[i].Text is "," or ";") i++;
        }
        i++; // }

        bool isList = items.Count > 0 && items.Select((kv, idx) => kv.Key is long k && k == idx + 1).All(x => x);
        if (isList)
        {
            var arr = new JsonArray();
            foreach (var kv in items) arr.Add(kv.Value);
            return arr;
        }
        var obj = new JsonObject();
        foreach (var kv in items) obj[Convert.ToString(kv.Key, CultureInfo.InvariantCulture)!] = kv.Value;
        return obj;
    }
}

/// <summary>One character's journal file, found under the game's WTF folder.</summary>
public record JournalFile(string Character, string Path)
{
    /// <summary>The character's key on the website: the folder name in lower case ("lunamal-sunshadow").</summary>
    public string Slug => Character.ToLowerInvariant();
    public override string ToString() => Character;
}

public class Journal
{
    public required JournalFile File { get; init; }
    public JsonObject Character { get; init; } = new();
    public JsonObject Quests { get; init; } = new();
    public List<JsonObject> Sessions { get; init; } = [];
    public DateTime SavedAt { get; init; }

    /// <summary>Every LunarJournal.lua under WTF\Account\&lt;account&gt;\&lt;realm&gt;\&lt;character&gt;\SavedVariables.</summary>
    public static List<JournalFile> Find(string wtfAccountFolder)
    {
        var found = new List<JournalFile>();
        if (!Directory.Exists(wtfAccountFolder)) return found;
        foreach (var account in Directory.GetDirectories(wtfAccountFolder))
            foreach (var realm in Directory.GetDirectories(account))
                foreach (var character in Directory.GetDirectories(realm))
                {
                    var file = System.IO.Path.Combine(character, "SavedVariables", "LunarJournal.lua");
                    if (System.IO.File.Exists(file)) found.Add(new(System.IO.Path.GetFileName(character), file));
                }
        return found.OrderBy(f => f.Character).ToList();
    }

    public static Journal Load(JournalFile file)
    {
        // Read as Latin-1 so each byte stays one char; LuaReader turns strings back into UTF-8 text
        var text = System.IO.File.ReadAllText(file.Path, Encoding.Latin1);
        var vars = LuaReader.ReadFile(text);
        var db = vars.GetValueOrDefault("LunarJournalDB") as JsonObject ?? new JsonObject();
        return new Journal
        {
            File = file,
            Character = db["character"] as JsonObject ?? new JsonObject(),
            Quests = db["quests"] as JsonObject ?? new JsonObject(),
            Sessions = (db["sessions"] as JsonArray ?? []).OfType<JsonObject>().ToList(),
            SavedAt = System.IO.File.GetLastWriteTime(file.Path),
        };
    }

    public static long Id(JsonObject session) => Num(session["id"]);

    public static DateTime When(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;

    public static long Num(JsonNode? n) =>
        n is not JsonValue v ? 0 :
        v.TryGetValue<long>(out var l) ? l :
        v.TryGetValue<double>(out var d) ? (long)d : 0;

    /// <summary>A one-line description of a session, for the list.</summary>
    public static string Describe(JsonObject s)
    {
        var start = When(Num(s["start"]));
        var minutes = (Num(s["stop"]) - Num(s["start"])) / 60;
        var events = s["events"] as JsonArray ?? [];
        int quests = events.OfType<JsonObject>().Count(e => (string?)e["k"] == "quest_done");
        var zones = (s["zones"] as JsonObject ?? []).OrderByDescending(z => Num(z.Value)).Take(2).Select(z => z.Key);
        return $"{start:yyyy-MM-dd HH:mm}  {minutes / 60}h{minutes % 60:00}m  {quests} quests  {string.Join(", ", zones)}";
    }

    /// <summary>
    /// The sessions as the AI gets them: times in minutes, dates readable, the details of every quest
    /// they touch filled in.
    /// </summary>
    public JsonObject Digest(IEnumerable<JsonObject> sessions)
    {
        var list = new JsonArray();
        foreach (var s in sessions)
        {
            var events = new JsonArray();
            var questIds = new HashSet<string>();
            foreach (var e in (s["events"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var copy = (JsonObject)e.DeepClone();
                if (copy["t"] is { } t) { copy.Remove("t"); copy["when"] = $"{When(Num(t)):yyyy-MM-dd HH:mm}"; }
                if (copy["t2"] is { } t2) { copy.Remove("t2"); copy["until"] = $"{When(Num(t2)):yyyy-MM-dd HH:mm}"; }
                if (((string?)copy["k"])?.StartsWith("quest_") == true && copy["id"] is { } id) questIds.Add(id.ToString());
                events.Add(copy);
            }
            var quests = new JsonObject();
            foreach (var id in questIds)
                if (Quests[id] is { } q) quests[id] = q.DeepClone();

            list.Add(new JsonObject
            {
                ["id"] = Num(s["id"]),
                ["start"] = $"{When(Num(s["start"])):yyyy-MM-dd HH:mm}",
                ["stop"] = $"{When(Num(s["stop"])):yyyy-MM-dd HH:mm}",
                ["realMinutes"] = (Num(s["stop"]) - Num(s["start"])) / 60,
                ["gameTimeAtStart"] = s["startGameTime"]?.DeepClone(),
                ["level"] = new JsonArray(s["levelStart"]?.DeepClone(), s["level"]?.DeepClone()),
                ["minutes"] = Minutes(s["time"]),
                ["yards"] = Rounded(s["dist"]),
                ["minutesPerZone"] = Minutes(s["zones"]),
                ["kills"] = s["kills"]?.DeepClone(),
                ["fishCaught"] = s["fish"]?.DeepClone(),
                ["crafted"] = s["crafted"]?.DeepClone(),
                ["gathered"] = s["gathered"]?.DeepClone(),
                ["npcsTalkedTo"] = s["npcs"]?.DeepClone(),
                ["playersMet"] = s["players"]?.DeepClone(),
                ["moneyGainedCopper"] = s["moneyGained"]?.DeepClone(),
                ["moneySpentCopper"] = s["moneySpent"]?.DeepClone(),
                ["questDetails"] = quests,
                ["events"] = events,
                ["eventsCut"] = s["truncated"]?.DeepClone() ?? false,
            });
        }
        return new JsonObject
        {
            ["character"] = File.Character,
            ["info"] = Character.DeepClone(),
            ["sessions"] = list,
        };
    }

    static JsonObject Minutes(JsonNode? map)
    {
        var o = new JsonObject();
        foreach (var kv in map as JsonObject ?? [])
            if (Num(kv.Value) >= 30) o[kv.Key] = Math.Round(Num(kv.Value) / 60.0, 1);
        return o;
    }

    static JsonObject Rounded(JsonNode? map)
    {
        var o = new JsonObject();
        foreach (var kv in map as JsonObject ?? []) o[kv.Key] = Num(kv.Value);
        return o;
    }
}
