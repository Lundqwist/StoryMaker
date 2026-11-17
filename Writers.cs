using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Beta.Messages;

namespace StoryMaker;

/// <summary>An AI that writes a chapter: gets the system prompt and the request, returns the text.</summary>
public interface IStoryWriter
{
    Task<string> Write(string system, string user, CancellationToken cancel);
}

public class AiException(string message) : Exception(message);

public static class Writers
{
    public static IStoryWriter For(AiProfile p)
    {
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

    public async Task<string> Write(string system, string user, CancellationToken cancel)
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
    public async Task<string> Write(string system, string user, CancellationToken cancel)
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

    public async Task<string> Write(string system, string user, CancellationToken cancel)
    {
        // About 3.5 characters per token, plus room for the chapter; rounded up to whole 8k steps
        var needed = (system.Length + user.Length) / 3.5 + 4000;
        var numCtx = (int)Math.Min(131072, Math.Ceiling(needed / 8192) * 8192);
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                model,
                stream = false,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                options = new { num_ctx = numCtx, temperature = 0.8 },
            }),
        };
        JsonNode json;
        try
        {
            json = await OpenAiWriter.Send(request, cancel);
        }
        catch (AiException e) when (e.Message.StartsWith("Couldn't reach"))
        {
            throw new AiException("Ollama isn't running. Start it from the Start menu (Ollama), then try again.");
        }
        catch (AiException e) when (e.Message.Contains("not found"))
        {
            throw new AiException($"Ollama doesn't have the model {model}. Download it with: ollama pull {model}");
        }
        var text = (string?)json["message"]?["content"];
        if (string.IsNullOrWhiteSpace(text)) throw new AiException("Ollama answered with no text.");
        return text;
    }

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

/// <summary>Google's Gemini, through its generateContent API.</summary>
public class GeminiWriter(string key, string model) : IStoryWriter
{
    public async Task<string> Write(string system, string user, CancellationToken cancel)
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
