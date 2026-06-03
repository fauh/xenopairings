using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Xenopairings.Services.GitHub;

public sealed class GitHubIssueService(
    HttpClient http,
    IOptions<GitHubSettings> settings,
    ILogger<GitHubIssueService> logger)
{
    private readonly GitHubSettings _cfg = settings.Value;

    /// <summary>
    /// Creates a GitHub issue and returns its URL, or null if not configured / on error.
    /// </summary>
    public async Task<string?> CreateIssueAsync(
        string title,
        string body,
        IEnumerable<string> labels)
    {
        if (!_cfg.IsConfigured)
        {
            logger.LogWarning("GitHub issue creation skipped: GitHub:Token is not configured.");
            return null;
        }

        var url = $"https://api.github.com/repos/{_cfg.Owner}/{_cfg.Repo}/issues";
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            labels = labels.ToArray(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.Token);
        request.Headers.UserAgent.ParseAdd("Xenopairings-App/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("html_url").GetString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create GitHub issue for repo {Owner}/{Repo}.",
                _cfg.Owner, _cfg.Repo);
            return null;
        }
    }
}
