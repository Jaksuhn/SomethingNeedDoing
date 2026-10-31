using SomethingNeedDoing.Core.Github;

namespace SomethingNeedDoing.Core;

/// <summary>
/// Contains information about a macro's Git repository source.
/// </summary>
public class GitInfo
{
    /// <summary>
    /// Gets or sets the Git repository URL.
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Git branch name.
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Gets or sets the file path within the repository.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current commit hash.
    /// </summary>
    public string CommitHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to automatically update this macro.
    /// </summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether there is an update available.
    /// </summary>
    public bool HasUpdate { get; set; }

    /// <summary>
    /// Gets or sets the last time this macro was checked for updates.
    /// </summary>
    public DateTime LastUpdateCheck { get; set; }

    /// <summary>
    /// Resolves the repository, branch and file path into a <see cref="GitHubRef"/>.
    /// </summary>
    public bool TryGetRef(out GitHubRef file)
    {
        if (!GitHubRef.TryParse(RepositoryUrl, out file))
            return false;
        file = file with { Branch = Branch, Path = FilePath };
        return true;
    }

    /// <summary>
    /// Returns a string representation of the Git information.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();

        if (CommitHash.Length >= 7)
            parts.Add($"({CommitHash[..7]})");

        if (GitHubRef.TryParse(RepositoryUrl, out var repo))
            parts.Add($"{repo.Owner}/{repo.Repo}");

        return string.Join(" | ", parts);
    }
}
