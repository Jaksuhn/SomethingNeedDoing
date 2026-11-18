using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.Core;
/// <summary>
/// Represents a macro that is stored in the config.
/// </summary>
public class ConfigMacro : MacroBase
{
    public const string Root = "";

    /// <inheritdoc/>
    public override string Id { get; } = Guid.NewGuid().ToString();

    /// <inheritdoc/>
    public override string Name { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override MacroType Type { get; set; }

    /// <inheritdoc/>
    public override MacroMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the folder path of the macro.
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Git repository information for this macro, if it is sourced from Git.
    /// </summary>
    public GitInfo GitInfo { get; set; } = new();

    public bool IsGitMacro => !GitInfo.RepositoryUrl.IsNullOrEmpty();

    /// <inheritdoc/>
    public override void Delete()
    {
        // Remove this macro from the configuration
        C.Macros.RemoveAll(m => m.Id == Id);
        C.Save();
    }
}
