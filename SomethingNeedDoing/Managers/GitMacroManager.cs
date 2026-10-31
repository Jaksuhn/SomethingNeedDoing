using SomethingNeedDoing.Core.Github;
using SomethingNeedDoing.Core.Interfaces;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Managers;

/// <summary>
/// Manages Git macros, including downloading, updating, and version control.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class GitMacroManager
{
    private readonly IMacroScheduler _scheduler;
    private readonly IGitService _gitService;
    private readonly TimeSpan _updateCooldown = TimeSpan.FromMinutes(5);

    [AutoPostConstruct]
    private void Initialize()
    {
        _ = UpdateAllMacros();

        // TODO: This is like this because having a parametered construct meant that IMacroDependencies couldn't be deserialized by the json deserializer
        foreach (var macro in C.Macros)
            foreach (var dependency in macro.Metadata.Dependencies)
                if (dependency is GitDependency gitDependency)
                    gitDependency.SetGitService(_gitService);
    }

    /// <summary>
    /// Checks all Git macros for updates and applies them to those with auto update enabled.
    /// </summary>
    public async Task UpdateAllMacros()
    {
        await CheckForUpdates();
        await Task.WhenAll([.. C.Macros.Where(m => m.GitInfo.HasUpdate && m.GitInfo.AutoUpdate).Select(m => UpdateMacro(m, null))]);
    }

    /// <summary>
    /// Imports a macro from a full GitHub URL, either into a new macro or into an existing one.
    /// </summary>
    /// <param name="githubUrl">The full GitHub URL (e.g., https://github.com/owner/repo/blob/branch/path)</param>
    /// <param name="macro">An existing macro to attach the Git info to, or null to create a new macro.</param>
    /// <returns>The imported macro.</returns>
    public async Task<ConfigMacro> Import(string githubUrl, ConfigMacro? macro = null)
    {
        if (!GitHubRef.TryParse(githubUrl, out var file) || string.IsNullOrEmpty(file.Path) || githubUrl.StartsWith("git://"))
            throw new ArgumentException("Invalid GitHub URL. Please use format: https://github.com/owner/repo/blob/branch/path");

        var isNew = macro == null;
        macro ??= new ConfigMacro();
        macro.GitInfo = new GitInfo
        {
            RepositoryUrl = githubUrl, // Store the full URL for UI consistency
            FilePath = file.Path,
            Branch = file.Branch
        };

        await UpdateMacro(macro);
        if (isNew)
            C.Macros.Add(macro);
        C.Save();
        return macro;
    }

    /// <summary>
    /// Checks for updates to a macro.
    /// </summary>
    /// <param name="macro">The macro to check.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckForUpdates(ConfigMacro macro)
    {
        if (!macro.IsGitMacro) return;
        if (DateTime.Now - macro.GitInfo.LastUpdateCheck < _updateCooldown)
        {
            FrameworkLogger.Info($"Skipping update check for {macro.Name} (last checked {macro.GitInfo.LastUpdateCheck})");
            return;
        }

        if (!macro.GitInfo.TryGetRef(out var file))
            return;

        FrameworkLogger.Debug($"Checking for updates for {macro.Name} ({file.Path})");
        List<GitCommit> commits;
        try
        {
            commits = await _gitService.GetCommitsAsync(file, 1);
        }
        catch
        {
            return;
        }

        if (commits.Count > 0)
        {
            var latestCommit = commits[0].Hash;
            macro.GitInfo.HasUpdate = latestCommit != macro.GitInfo.CommitHash;
            FrameworkLogger.Info(macro.GitInfo.HasUpdate
                ? $"Update available for {macro.Name} ({macro.GitInfo.CommitHash} → {latestCommit})"
                : $"No updates available for {macro.Name} ({macro.GitInfo.CommitHash} == {latestCommit})");
        }

        macro.GitInfo.LastUpdateCheck = DateTime.Now;
    }

    /// <summary>
    /// Checks for updates to all macros.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckForUpdates()
    {
        foreach (var macro in C.Macros.Where(m => m.IsGitMacro))
            await CheckForUpdates(macro);
    }

    public async Task UpdateMacro(ConfigMacro macro)
    {
        await CheckForUpdates(macro);
        if (macro.GitInfo.HasUpdate)
            await UpdateMacro(macro, null);
        macro.GitInfo.HasUpdate = false;
    }

    public async Task<List<GitCommit>> GetCommitHistory(ConfigMacro macro)
    {
        if (!macro.IsGitMacro)
            return [];

        if (!macro.GitInfo.TryGetRef(out var file))
        {
            FrameworkLogger.Error($"Invalid repository URL: {macro.GitInfo.RepositoryUrl}");
            return [];
        }

        var commits = await _gitService.GetCommitsAsync(file);
        FrameworkLogger.Debug($"Found {commits.Count} commits for file {file.Path}");
        return commits;
    }

    /// <summary>
    /// Updates a macro to a specific commit.
    /// </summary>
    /// <param name="macro">The macro to update.</param>
    /// <param name="commitHash">The commit hash to update to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateToCommit(ConfigMacro macro, string commitHash)
    {
        try
        {
            await UpdateMacro(macro, commitHash);
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error(ex, $"Failed to update macro {macro.Name} to commit {commitHash}");
        }
    }

    /// <summary>
    /// Downloads the macro content at <paramref name="specificCommit"/>, or at the latest commit if null.
    /// </summary>
    private async Task UpdateMacro(ConfigMacro macro, string? specificCommit)
    {
        if (!macro.IsGitMacro || !macro.GitInfo.TryGetRef(out var file))
            return;

        FrameworkLogger.Debug($"Updating macro {macro.Name} ({file.Path})");

        var commitHash = specificCommit;
        if (commitHash == null)
        {
            var commits = await _gitService.GetCommitsAsync(file, 1);
            if (commits.Count == 0)
            {
                FrameworkLogger.Error("No commits found for file");
                throw new Exception("No commits found for file");
            }
            commitHash = commits[0].Hash;
        }

        var fileContent = await _gitService.DownloadAsync(file.ToRawUrl(commitHash));

        _scheduler.StopMacro(macro.Id);

        macro.Content = fileContent;
        macro.GitInfo.CommitHash = commitHash;
        macro.GitInfo.HasUpdate = false;
        macro.GitInfo.LastUpdateCheck = DateTime.Now;
        _scheduler.RefreshTriggersFromContent(macro, refreshFunctionTriggersIfRunning: false);

        C.Save();
        FrameworkLogger.Info($"Successfully updated macro {macro.Name} to commit {commitHash}");
    }
}
