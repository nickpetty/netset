using System.Net;
using System.Reflection;
using System.Text.Json;

namespace NetSet;

public record UpdateInfo(string Tag, Version Version, string Url);

/// <summary>Looks for a newer GitHub release. Only reads the public releases API; never downloads or runs anything.</summary>
public static class UpdateChecker
{
    public const string Repo = "nickpetty/netset";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("NetSet-UpdateCheck");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version Current => Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0));

    public static string CurrentText => Current.ToString(3);

    // 1.1.0 parses as 1.1.0.-1 and would sort below 1.1.0.0, so pad the missing parts.
    static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>Returns the newer release (null if up to date) or an error message if the check failed.</summary>
    public static async Task<(UpdateInfo? Update, string? Error)> CheckAsync()
    {
        try
        {
            using var resp = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (resp.StatusCode == HttpStatusCode.NotFound) return (null, "No releases found.");
            if (!resp.IsSuccessStatusCode) return (null, $"GitHub returned {(int)resp.StatusCode}.");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            string url = doc.RootElement.GetProperty("html_url").GetString() ?? "";

            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return (null, $"Unrecognised release tag \"{tag}\".");
            // The URL comes from the network and gets opened in a browser, so only accept this repo's pages.
            if (!url.StartsWith($"https://github.com/{Repo}/", StringComparison.OrdinalIgnoreCase)) return (null, "Unexpected release URL.");

            var latest = Normalize(parsed);
            return latest > Current ? (new UpdateInfo(tag, latest, url), null) : (null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return (null, "Could not reach GitHub.");
        }
    }
}
