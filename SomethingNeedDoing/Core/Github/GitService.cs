using SomethingNeedDoing.Core.Interfaces;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Core.Github;

/// <summary>
/// Implementation of the Git service using the GitHub API.
/// </summary>
[RegisterSingleton<IGitService>, AutoConstruct]
public partial class GitService : IGitService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "SomethingNeedDoing" }
        }
    };

    /// <inheritdoc/>
    public async Task<string> GetFileContentAsync(string repositoryUrl, string branch, string path)
    {
        var response = await _httpClient.GetAsync(GetRef(repositoryUrl, branch, path).ToApiContentsUrl());
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var fileInfo = JsonSerializer.Deserialize<GitHubFileInfo>(content, JsonOptions);
        if (fileInfo == null)
        {
            FrameworkLogger.Error($"Failed to deserialize GitHub API response");
            return string.Empty;
        }

        FrameworkLogger.Debug($"Encoding: '{fileInfo.Encoding}', Content length: {fileInfo.Content.Length}");

        if (fileInfo.Encoding == "base64" && !string.IsNullOrEmpty(fileInfo.Content))
        {
            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(fileInfo.Content));
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Failed to decode base64 content: {ex.Message}");
                return string.Empty;
            }
        }

        FrameworkLogger.Warning($"Unknown encoding: '{fileInfo.Encoding}'");
        return fileInfo.Content;
    }

    /// <inheritdoc/>
    public async Task<bool> FileExistsAsync(string repositoryUrl, string branch, string path)
    {
        try
        {
            var response = await _httpClient.GetAsync(GetRef(repositoryUrl, branch, path).ToApiContentsUrl());
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<GitCommit>> GetCommitsAsync(GitHubRef file, int? perPage = null)
    {
        var url = file.ToApiCommitsUrl(perPage);
        FrameworkLogger.Debug($"Requesting: {url}");

        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            FrameworkLogger.Error($"Failed to get commits from [{url}]: {response.StatusCode} - {content}");
            throw new Exception($"Failed to get commits: {response.StatusCode}");
        }

        var commits = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? [];
        return [.. commits.Select(c => new GitCommit(
            c.GetProperty("sha").GetString() ?? string.Empty,
            c.GetProperty("commit").GetProperty("message").GetString() ?? string.Empty,
            c.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object ? author.GetProperty("login").GetString() ?? string.Empty : string.Empty,
            c.GetProperty("commit").GetProperty("author").GetProperty("date").GetDateTime(),
            c.GetProperty("html_url").GetString() ?? string.Empty))];
    }

    /// <inheritdoc/>
    public async Task<string> DownloadAsync(string url)
    {
        FrameworkLogger.Debug($"Downloading file from: {url}");
        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            FrameworkLogger.Error($"Failed to download file: {response.StatusCode} - {content} from url [{url}]");
            throw new Exception($"Failed to download file: {response.StatusCode}");
        }
        return content;
    }

    private static GitHubRef GetRef(string repositoryUrl, string branch, string path)
        => GitHubRef.TryParse(repositoryUrl, out var repo)
            ? repo with { Branch = branch, Path = path }
            : throw new ArgumentException($"Invalid GitHub repository URL: {repositoryUrl}");

    /// <inheritdoc/>
    public void Dispose() => _httpClient.Dispose();

    private class GitHubFileInfo
    {
        public string Content { get; set; } = string.Empty;
        public string Encoding { get; set; } = string.Empty;
    }
}
