using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace StoryMaker;

/// <summary>
/// The WowCharacters site's story API (see github.com/Lundqwist/WowCharacters, Program.cs):
///   GET  {site}/api/forever/story?c=&lt;key&gt;                      -> { markdown }
///   POST {site}/api/forever/story?c=&lt;key&gt;  { markdown }       -> { chapter }: saved as a new chapter
/// Both need "Authorization: Bearer &lt;STORY_TOKEN&gt;".
/// </summary>
public class SiteClient(string siteUrl, string? token)
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string PageUrl(string game, string key) => $"{siteUrl.TrimEnd('/')}/{game}/character.html?c={Uri.EscapeDataString(key)}";

    public string ChapterUrl(string game, string key, int chapter) => $"{siteUrl.TrimEnd('/')}/{game}/chapter.html?c={Uri.EscapeDataString(key)}&n={chapter}";

    string ApiUrl(string game, string key) => $"{siteUrl.TrimEnd('/')}/api/{game}/story?c={Uri.EscapeDataString(key)}";

    HttpRequestMessage Request(HttpMethod method, string url)
    {
        if (token is null) throw new AiException("No site token: set the user environment variable STORY_TOKEN (the same value as STORY_TOKEN in the site's .env).");
        var r = new HttpRequestMessage(method, url);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return r;
    }

    public async Task<string> Story(string game, string key, CancellationToken cancel = default)
    {
        using var response = await Send(Request(HttpMethod.Get, ApiUrl(game, key)), key, cancel);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancel);
        return (string?)json?["markdown"] ?? "";
    }

    /// <summary>Saves the text as a new chapter; returns its number.</summary>
    public async Task<int> Post(string game, string key, string markdown, CancellationToken cancel = default)
    {
        var request = Request(HttpMethod.Post, ApiUrl(game, key));
        request.Content = JsonContent.Create(new { markdown });
        using var response = await Send(request, key, cancel);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancel);
        return (int?)json?["chapter"] ?? 0;
    }

    static async Task<HttpResponseMessage> Send(HttpRequestMessage request, string key, CancellationToken cancel)
    {
        HttpResponseMessage response;
        try
        {
            using (request) response = await Http.SendAsync(request, cancel);
        }
        catch (HttpRequestException e)
        {
            throw new AiException($"Couldn't reach the website: {e.Message}");
        }
        if (response.IsSuccessStatusCode) return response;
        var body = await response.Content.ReadAsStringAsync(cancel);
        response.Dispose();
        throw new AiException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "The website didn't accept the token: STORY_TOKEN here must be the same as in the site's .env.",
            HttpStatusCode.NotFound => $"The website has no character '{key}' (add it to characters.json), or story posting is off there (no STORY_TOKEN in its .env).",
            _ => $"The website answered {(int)response.StatusCode}: {body}",
        });
    }
}
