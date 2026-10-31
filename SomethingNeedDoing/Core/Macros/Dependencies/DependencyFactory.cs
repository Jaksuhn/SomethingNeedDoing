using SomethingNeedDoing.Core.Github;
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
        if ((source.StartsWith("git://") || source.Contains("github.com")) && GitHubRef.TryParse(source, out var file))
        {
            var gitDep = new GitDependency
            {
                Name = file.Repo,
                Source = source.StartsWith("git://") ? source : file.ToGitSource(),
                GitInfo = new GitInfo
                {
                    RepositoryUrl = file.RepositoryUrl,
                    Branch = file.Branch,
                    FilePath = file.Path
                }
            };
            gitDep.SetGitService(_gitService);
            return gitDep;
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

        return new HttpDependency()
        {
            Name = "latest",
            Source = source
        };
    }

    /// <summary>
    /// Checks if a string looks like a macro ID (GUID format).
    /// </summary>
    private static bool IsMacroId(string source) => !string.IsNullOrWhiteSpace(source) && Guid.TryParse(source, out _);
}
