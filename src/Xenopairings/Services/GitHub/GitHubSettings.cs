namespace Xenopairings.Services.GitHub;

public sealed class GitHubSettings
{
    public const string SectionName = "GitHub";

    /// <summary>
    /// GitHub repository owner (e.g. "fauh").
    /// </summary>
    public string Owner { get; init; } = "fauh";

    /// <summary>
    /// GitHub repository name (e.g. "xenopairings").
    /// </summary>
    public string Repo { get; init; } = "xenopairings";

    /// <summary>
    /// Personal Access Token with issues:write scope.
    /// Set via the GitHub__Token environment variable / app setting.
    /// Never commit a real token here.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);
}
