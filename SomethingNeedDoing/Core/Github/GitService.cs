using SomethingNeedDoing.Core.Interfaces;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Core.Github;

/// <summary>
/// Implementation of the Git service using the GitHub API.
/// </summary>
[RegisterSingleton<IGitService>, AutoConstruct]
public partial class GitService : IGitService
{
    private readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "SomethingNeedDoing" }
        }
    };
    private const string GitHubApiBaseUrl = "https://api.github.com";

    /// <inheritdoc/>
    public async Task<string> GetFileContentAsync(string repositoryUrl, string branch, string path)
    {
        var apiUrl = GetGitHubApiUrl(repositoryUrl, branch, path);

        var response = await _httpClient.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        FrameworkLogger.Debug($"Raw response length: {content.Length}");

        var fileInfo = JsonSerializer.Deserialize<GitHubFileInfo>(content);

        if (fileInfo == null)
        {
            FrameworkLogger.Error($"Failed to deserialize GitHub API response");
            return string.Empty;
        }

        FrameworkLogger.Debug($"Encoding: '{fileInfo.Encoding}', Content length: {fileInfo.Content?.Length ?? 0}");

        // If the automatic deserialization didn't work, try manual JSON parsing
        if (string.IsNullOrEmpty(fileInfo.Encoding) && string.IsNullOrEmpty(fileInfo.Content))
        {
            FrameworkLogger.Debug("Automatic deserialization failed, trying manual JSON parsing");
            try
            {
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("encoding", out var encodingElement) &&
                    root.TryGetProperty("content", out var contentElement))
                {
                    var encoding = encodingElement.GetString();
                    var jsonContent = contentElement.GetString();

                    FrameworkLogger.Debug($"Manual parsing - Encoding: '{encoding}', Content length: {jsonContent?.Length ?? 0}");

                    if (encoding == "base64" && !string.IsNullOrEmpty(jsonContent))
                    {
                        var decodedContent = Convert.FromBase64String(jsonContent);
                        var result = System.Text.Encoding.UTF8.GetString(decodedContent);
                        FrameworkLogger.Debug($"Manual parsing - Decoded content length: {result.Length}");
                        return result;
                    }
                }
                else
                    FrameworkLogger.Error($"Manual parsing failed - 'encoding' or 'content' properties not found in JSON");
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Manual JSON parsing failed: {ex.Message}");
            }
        }

        if (fileInfo.Encoding == "base64" && !string.IsNullOrEmpty(fileInfo.Content))
        {
            try
            {
                var decodedContent = Convert.FromBase64String(fileInfo.Content);
                var result = System.Text.Encoding.UTF8.GetString(decodedContent);
                FrameworkLogger.Debug($"Decoded content length: {result.Length}");
                return result;
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Failed to decode base64 content: {ex.Message}");
                return string.Empty;
            }
        }

        FrameworkLogger.Warning($"Unknown encoding: '{fileInfo.Encoding}'");
        return fileInfo.Content ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<bool> FileExistsAsync(string repositoryUrl, string branch, string path)
    {
        try
        {
            var apiUrl = GetGitHubApiUrl(repositoryUrl, branch, path);
            var response = await _httpClient.GetAsync(apiUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string GetGitHubApiUrl(string repositoryUrl, string branch, string path)
    {
        // Convert repository URL to GitHub API URL
        var uri = new Uri(repositoryUrl);
        var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var owner = pathParts[0];
        var repo = pathParts[1].Replace(".git", "");

        return $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/contents/{path}?ref={branch}";
    }

    private class GitHubFileInfo
    {
        public string Content { get; set; } = string.Empty;
        public string Encoding { get; set; } = string.Empty;
    }
}
