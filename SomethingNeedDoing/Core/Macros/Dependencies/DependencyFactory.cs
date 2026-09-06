using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.Core;

/// <summary>
/// Factory for creating dependencies.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class DependencyFactory
{
    private readonly IGitService _gitService;

    /// <summary>
    /// Creates a dependency from a source string, automatically detecting the type.
    /// </summary>
    /// <param name="source">The source URL or path.</param>
    /// <returns>The created dependency.</returns>
    public IMacroDependency CreateDependency(string source)
    {
        var normalizedSource = NormalizeSource(source);

        if (normalizedSource.StartsWith("git://"))
        {
            var parts = normalizedSource[6..].Split('/');
            if (parts.Length >= 2)
            {
                var owner = parts[0];
                var repo = parts[1];
                var branch = parts.Length > 2 ? parts[2] : "main";
                var path = parts.Length > 3 ? string.Join("/", parts.Skip(3)) : null;

                var gitDep = new GitDependency
                {
                    Name = repo,
                    Source = normalizedSource,
                    GitInfo = new GitInfo
                    {
                        RepositoryUrl = $"https://github.com/{owner}/{repo}",
                        Branch = branch,
                        FilePath = path ?? string.Empty
                    }
                };
                gitDep.SetGitService(_gitService);
                return gitDep;
            }
        }
        else if (IsMacroId(source))
        {
            var macro = C.GetMacro(source);
            if (macro != null)
            {
                return new LocalMacroDependency
                {
                    Name = macro.Name,
                    Source = source
                };
            }
        }
        else if (System.IO.File.Exists(source.NormalizeFilePath()))
        {
            var normalizedPath = source.NormalizeFilePath();
            return new LocalDependency
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(normalizedPath),
                Source = normalizedPath
            };
        }

        var httpDep = new HttpDependency()
        {
            Name = "latest",
            Source = source
        };
        return httpDep;
    }

    /// <summary>
    /// Checks if a string looks like a macro ID (GUID format).
    /// </summary>
    private static bool IsMacroId(string source) => !string.IsNullOrWhiteSpace(source) && Guid.TryParse(source, out _);

    /// <summary>
    /// Normalizes a source URL to a consistent format.
    /// </summary>
    private string NormalizeSource(string source)
    {
        if (source.Contains("github.com"))
        {
            var uri = new Uri(source);
            var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length >= 2)
            {
                var owner = pathParts[0];
                var repo = pathParts[1];
                var branch = pathParts.Length > 3 && pathParts[2] == "blob" ? pathParts[3] : "main";
                var path = pathParts.Length > 4 && pathParts[2] == "blob" ? string.Join("/", pathParts.Skip(4)) : null;

                if (path != null)
                    path = Uri.UnescapeDataString(path);

                return $"git://{owner}/{repo}/{branch}/{path}";
            }
        }

        return source;
    }
}
