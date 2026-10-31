namespace SomethingNeedDoing.Core.Github;

/// <summary>
/// A reference to a file (or repository) on GitHub.
/// </summary>
public sealed record GitHubRef(string Owner, string Repo, string Branch, string Path)
{
    /// <summary>
    /// Parses <c>https://github.com/owner/repo[/blob/branch/path]</c> or <c>git://owner/repo[/branch[/path]]</c>.
    /// </summary>
    public static bool TryParse(string? url, out GitHubRef result)
    {
        result = null!;
        if (string.IsNullOrWhiteSpace(url)) return false;

        string[] segments;
        bool isGitScheme;
        if (url.StartsWith("git://"))
        {
            segments = url[6..].Split('/');
            isGitScheme = true;
        }
        else
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            isGitScheme = false;
        }

        if (segments.Length < 2 || string.IsNullOrEmpty(segments[0]) || string.IsNullOrEmpty(segments[1]))
            return false;

        var repo = segments[1].EndsWith(".git") ? segments[1][..^4] : segments[1];
        var (branch, path) = isGitScheme
            ? (segments.Length > 2 && segments[2] != string.Empty ? segments[2] : "main", string.Join("/", segments.Skip(3)))
            : segments.Length > 3 && segments[2] == "blob"
                ? (segments[3], Uri.UnescapeDataString(string.Join("/", segments.Skip(4))))
                : ("main", string.Empty);

        result = new GitHubRef(segments[0], repo, branch, path);
        return true;
    }

    public string RepositoryUrl => $"https://github.com/{Owner}/{Repo}";
    public string ToGitSource() => $"git://{Owner}/{Repo}/{Branch}/{Path}";
    public string ToRawUrl(string? commitOrBranch = null) => $"https://raw.githubusercontent.com/{Owner}/{Repo}/{commitOrBranch ?? Branch}/{Path}";
    public string ToApiContentsUrl() => $"https://api.github.com/repos/{Owner}/{Repo}/contents/{Path}?ref={Branch}";
    public string ToApiCommitsUrl(int? perPage = null)
        => $"https://api.github.com/repos/{Owner}/{Repo}/commits?path={Uri.EscapeDataString(Path)}" + (perPage is { } n ? $"&per_page={n}" : string.Empty);
}

/// <summary>
/// A commit that touched a file.
/// </summary>
public sealed record GitCommit(string Hash, string Message, string Author, DateTime Date, string HtmlUrl);
