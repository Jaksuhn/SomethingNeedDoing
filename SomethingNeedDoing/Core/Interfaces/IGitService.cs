using SomethingNeedDoing.Core.Github;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Core.Interfaces;

/// <summary>
/// Interface for Git operations.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Gets the content of a file from a Git repository.
    /// </summary>
    /// <param name="repositoryUrl">The repository URL.</param>
    /// <param name="branch">The branch name.</param>
    /// <param name="path">The path within the repository.</param>
    /// <returns>The file content.</returns>
    Task<string> GetFileContentAsync(string repositoryUrl, string branch, string path);

    /// <summary>
    /// Checks if a file exists in a Git repository.
    /// </summary>
    /// <param name="repositoryUrl">The repository URL.</param>
    /// <param name="branch">The branch name.</param>
    /// <param name="path">The path within the repository.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    Task<bool> FileExistsAsync(string repositoryUrl, string branch, string path);

    /// <summary>
    /// Gets the commits that touched a file, newest first.
    /// </summary>
    /// <param name="file">The file to get commits for.</param>
    /// <param name="perPage">Optional maximum number of commits to return.</param>
    Task<List<GitCommit>> GetCommitsAsync(GitHubRef file, int? perPage = null);

    /// <summary>
    /// Downloads the content at a URL as a string.
    /// </summary>
    Task<string> DownloadAsync(string url);
}
