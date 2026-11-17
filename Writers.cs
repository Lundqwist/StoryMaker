using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Beta.Messages;

namespace StoryMaker;

/// <summary>An AI that writes a chapter: gets the system prompt and the request, returns the text.</summary>
public interface IStoryWriter
{
    /// <summary><paramref name="partial"/>, when the AI can stream, gets the text so far as it is written.</summary>
    Task<string> Write(string system, string user, CancellationToken cancel, IProgress<string>? partial = null);
}

public class AiException(string message) : Exception(message);

public static class Writers
{
    public static IStoryWriter For(AiProfile p)
    {
        if (p.Kind == AiKind.Sample) return new SampleWriter();
        var key = Settings.Secret(p.KeyVariable);
        if (key is null && p.Kind is not (AiKind.OpenAICompatible or AiKind.Ollama))
            throw new AiException($"No API key: set the user environment variable {p.KeyVariable} (Settings shows which one is used), then try again.");
        if (string.IsNullOrWhiteSpace(p.Model)) throw new AiException("Pick a model first.");
        // Never send a secret that is clearly not this AI's key (for example the website's token) to the AI
        if (key is not null && key == Settings.Secret("STORY_TOKEN"))
            throw new AiException($"{p.KeyVariable} holds the website's STORY_TOKEN, not an AI key. Set {p.KeyVariable} to the API key from the AI's own website, then start StoryMaker again.");
        if (p.Kind == AiKind.Claude && key is not null && !key.StartsWith("sk-ant-", StringComparison.Ordinal))
            throw new AiException($"{p.KeyVariable} doesn't look like an Anthropic API key (those start with sk-ant-). Create one at console.anthropic.com → API keys, set it with setx {p.KeyVariable} \"...\", then start StoryMaker again.");
        return p.Kind switch
        {
            AiKind.Claude => new ClaudeWriter(key!, p.Model.Trim()),
            AiKind.Gemini => new GeminiWriter(key!, p.Model.Trim()),
            AiKind.Ollama => new OllamaWriter(p.Model.Trim(), string.IsNullOrWhiteSpace(p.BaseUrl) ? OllamaWriter.DefaultUrl : p.BaseUrl.Trim()),
            _ => new OpenAiWriter(key, p.Model.Trim(), string.IsNullOrWhiteSpace(p.BaseUrl) ? "https://api.openai.com/v1" : p.BaseUrl.Trim()),
        };
    }

    internal static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
}

/// <summary>Claude, through Anthropic's official C# SDK.</summary>
public class ClaudeWriter(string key, string model) : IStoryWriter
{
    // Models that can hand a refused request on to another model by themselves (server-side fallbacks)
    static readonly string[] WithFallbacks = ["claude-opus-5-5", "claude-opus-5", "claude-sonnet-5-5", "claude-fable-5-2", "claude-fable-5-1"];

    public async Task<string> Write(string system, string user, CancellationToken cancel, IProgress<string>? partial = null)
    {
        var client = new AnthropicClient { ApiKey = key };
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            System = system,
            OutputConfig = new BetaOutputConfig { Effort = Effort.High },
            Messages = [new BetaUserMessageParam { Content = user }],
        };
        if (WithFallbacks.Contains(model))
        {
            parameters = parameters with
            {
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
            };
        }
        BetaMessage response;
        try
        {
            response = await client.Beta.Messages.Create(parameters, cancel);
        }
        catch (Anthropic.Exceptions.AnthropicApiException e)
        {
            throw new AiException("Claude said no: " + e.Message);
        }
        if (response.StopReason == "refusal")
            throw new AiException("Claude declined to write this chapter" + (response.StopDetails is { } d ? $" ({d.Explanation})." : "."));
        var text = string.Concat(response.Content.Select(b => b.TryPickText(out var t) ? t.Text : ""));
        if (text.Trim().Length == 0) throw new AiException("Claude answered with no text.");
        return text;
    }
}

/// <summary>OpenAI, or anything that speaks its chat completions API (Ollama, LM Studio, OpenRouter, ...).</summary>
public class OpenAiWriter(string? key, string model, string baseUrl) : IStoryWriter
{
    public async Task<string> Write(string system, string user, CancellationToken cancel, IProgress<string>? partial = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
            }),
        };
        if (key is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var json = await Send(request, cancel);
        var text = (string?)json["choices"]?[0]?["message"]?["content"];
        if (string.IsNullOrWhiteSpace(text)) throw new AiException("The AI answered with no text.");
        return text;
    }

    internal static async Task<JsonNode> Send(HttpRequestMessage request, CancellationToken cancel)
    {
        HttpResponseMessage response;
        try
        {
            response = await Writers.Http.SendAsync(request, cancel);
        }
        catch (HttpRequestException e)
        {
            throw new AiException($"Couldn't reach {request.RequestUri?.Host}: {e.Message}");
        }
        var body = await response.Content.ReadAsStringAsync(cancel);
        if (!response.IsSuccessStatusCode)
            throw new AiException($"{request.RequestUri?.Host} answered {(int)response.StatusCode}: {Short(body)}");
        return JsonNode.Parse(body) ?? new JsonObject();
    }

    static string Short(string s) => s.Length > 400 ? s[..400] + "..." : s;
}

/// <summary>
/// A model running in Ollama on this PC: free and offline. Uses Ollama's own chat API rather than its
/// OpenAI-compatible one, because only the own API can set the context size (num_ctx) per request.
/// Ollama's default is small and it silently cuts what doesn't fit, which would drop most of a journal.
/// </summary>
public class OllamaWriter(string model, string baseUrl) : IStoryWriter
{
    public const string DefaultUrl = "http://localhost:11434";

    public async Task<string> Write(string system, string user, CancellationToken cancel, IProgress<string>? partial = null)
    {
        // About 3.5 characters per token, plus room for the chapter; rounded up to whole 8k steps
        var needed = (system.Length + user.Length) / 3.5 + 4000;
        var numCtx = (int)Math.Min(131072, Math.Ceiling(needed / 8192) * 8192);
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                model,
                stream = true,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                options = new { num_ctx = numCtx, temperature = 0.8 },
            }),
        };
        // Streamed: the chapter shows up as it is written, and there is no time limit as long as words
        // keep coming. A local model can be slow (for example while a game uses the graphics card); only
        // if nothing at all arrives for StallMinutes is it given up.
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        stall.CancelAfter(TimeSpan.FromMinutes(StallMinutes));
        HttpResponseMessage response;
        try
        {
            response = await Streaming.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stall.Token);
        }
        catch (HttpRequestException)
        {
            throw new AiException("Ollama isn't running. Start it from the Start menu (Ollama), then try again.");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new AiException(StalledMessage);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancel);
                throw new AiException(body.Contains("not found")
                    ? $"Ollama doesn't have the model {model}. Download it with: ollama pull {model}"
                    : $"Ollama answered {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
            }
            var text = new System.Text.StringBuilder();
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancel));
            try
            {
                while (await reader.ReadLineAsync(stall.Token) is { } line)
                {
                    if (line.Length == 0) continue;
                    var chunk = JsonNode.Parse(line);
                    if ((string?)chunk?["error"] is { } error) throw new AiException("Ollama: " + error);
                    var piece = (string?)chunk?["message"]?["content"];
                    if (!string.IsNullOrEmpty(piece))
                    {
                        text.Append(piece);
                        partial?.Report(text.ToString());
                        stall.CancelAfter(TimeSpan.FromMinutes(StallMinutes));   // words are coming: wait on
                    }
                    if (chunk?["done"]?.GetValue<bool>() == true) break;
                }
            }
            catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
            {
                throw new AiException(StalledMessage);
            }
            if (text.ToString().Trim().Length == 0) throw new AiException("Ollama answered with no text.");
            return text.ToString();
        }
    }

    const int StallMinutes = 5;
    const string StalledMessage =
        "Ollama stopped answering for 5 minutes, so the chapter was given up.\n\n" +
        "This usually means the graphics card is busy: World of Warcraft (or another game) uses most of its " +
        "memory, and the AI model no longer fits. Close the game and try again, or pick a smaller model.";

    // No overall time limit for a streamed answer (Stop and the stall check end it instead)
    static readonly HttpClient Streaming = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>The models Ollama has downloaded, or none when it isn't running.</summary>
    public static async Task<List<string>> Installed(string baseUrl)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var json = await Writers.Http.GetFromJsonAsync<JsonObject>(baseUrl.TrimEnd('/') + "/api/tags", cts.Token);
            return (json?["models"] as JsonArray ?? []).Select(m => (string?)m?["name"]).OfType<string>().Order().ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}

/// <summary>
/// No AI: hands back the chapter written in advance for the sample character (Sample\chapter.md), a few
/// words at a time like a streaming AI, so the whole program can be tried without an AI or the game.
/// </summary>
public class SampleWriter : IStoryWriter
{
    public async Task<string> Write(string system, string user, CancellationToken cancel, IProgress<string>? partial = null)
    {
        using var stream = typeof(SampleWriter).Assembly.GetManifestResourceStream("sample-chapter.md")!;
        var text = await new StreamReader(stream).ReadToEndAsync(cancel);
        var shown = new System.Text.StringBuilder();
        int words = 0;
        foreach (var word in System.Text.RegularExpressions.Regex.Split(text, @"(?<=\s)"))
        {
            shown.Append(word);
            if (++words % 5 == 0) { partial?.Report(shown.ToString()); await Task.Delay(30, cancel); }
        }
        return text;
    }
}

/// <summary>Google's Gemini, through its generateContent API.</summary>
public class GeminiWriter(string key, string model) : IStoryWriter
{
    public async Task<string> Write(string system, string user, CancellationToken cancel, IProgress<string>? partial = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = system } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
            }),
        };
        request.Headers.Add("x-goog-api-key", key);
        var json = await OpenAiWriter.Send(request, cancel);
        var parts = json["candidates"]?[0]?["content"]?["parts"] as JsonArray;
        var text = string.Concat((parts ?? []).Select(p => (string?)p?["text"] ?? ""));
        if (string.IsNullOrWhiteSpace(text))
            throw new AiException("Gemini answered with no text" + (json["candidates"]?[0]?["finishReason"] is { } r ? $" ({r})." : "."));
        return text;
    }
}
