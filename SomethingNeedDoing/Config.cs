using Dalamud.Game.Text;
using ECommons.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;
using System.Text;

namespace SomethingNeedDoing;
/// <summary>
/// Configuration for the plugin.
/// </summary>
public class Config : IEzConfig
{
    public int Version { get; set; } = 2;

    #region General Settings
    public bool LockWindow { get; set; }
    public bool DisableMonospaced { get; set; }
    public XivChatType ChatType { get; set; } = XivChatType.Debug;
    public XivChatType ErrorChatType { get; set; } = XivChatType.Urgent;
    #endregion

    #region Macro Settings
    public List<ConfigMacro> Macros { get; set; } = [];
    public string DefaultFileName { get; set; } = "UntitledMacro";
    public string DefaultFileExtension { get; set; } = ".txt";
    #endregion

    #region Crafting Settings
    public bool CraftSkip { get; set; } = true;
    public bool SmartWait { get; set; }
    public bool QualitySkip { get; set; } = true;
    public bool LoopTotal { get; set; }
    public bool LoopEcho { get; set; }
    public bool UseCraftLoopTemplate { get; set; }
    public string CraftLoopTemplate { get; set; } =
        "/craft {{count}}\n" +
        "/waitaddon \"RecipeNote\" <maxwait.5>" +
        "/click \"RecipeNote Synthesize\"" +
        "/waitaddon \"Synthesis\" <maxwait.5>" +
        "{{macro}}" +
        "/loop";
    public bool CraftLoopFromRecipeNote { get; set; } = true;
    public int CraftLoopMaxWait { get; set; } = 5;
    public bool CraftLoopEcho { get; set; }
    #endregion

    #region Error Handling
    public int MaxTimeoutRetries { get; set; }
    public bool NoisyErrors { get; set; }
    public int BeepFrequency { get; set; } = 900;
    public int BeepDuration { get; set; } = 250;
    public int BeepCount { get; set; } = 3;
    #endregion

    #region Targeting
    public bool UseSNDTargeting { get; set; } = true;
    #endregion

    #region AutoRetainer Integration
    public ConfigMacro? ARCharacterPostProcessMacro { get; set; }
    public List<ulong> ARCharacterPostProcessExcludedCharacters { get; set; } = [];
    #endregion

    #region Error Conditions
    public bool StopMacroIfActionTimeout { get; set; } = true;
    public bool StopMacroIfItemNotFound { get; set; } = true;
    public bool StopMacroIfCantUseItem { get; set; } = true;
    public bool StopMacroIfTargetNotFound { get; set; } = true;
    public bool StopMacroIfAddonNotFound { get; set; } = true;
    public bool StopMacroIfAddonNotVisible { get; set; } = true;
    #endregion

    #region Lua Settings
    public string[] LuaRequirePaths { get; set; } = [];
    public bool UseMacroFileSystem { get; set; }
    #endregion

    #region Git Macro Settings
    public List<GitMacro> GitMacros { get; set; } = [];
    #endregion

    public bool HasCompletedTutorial { get; set; } = false;

    /// <summary>
    /// Migrates configuration from an older version.
    /// </summary>
    public void Migrate(dynamic oldConfig)
    {
        try
        {
            // Migrate from version 1
            if (oldConfig.Version == 1)
            {
                // Log the old config structure for debugging
                Svc.Log.Info($"Old config type: {oldConfig.GetType().Name}");
                foreach (var prop in oldConfig.GetType().GetProperties())
                {
                    Svc.Log.Info($"Property: {prop.Name} = {prop.GetValue(oldConfig)}");
                }

                // Migrate general settings
                LockWindow = oldConfig.LockWindow;
                DisableMonospaced = oldConfig.DisableMonospaced;
                ChatType = oldConfig.ChatType;
                ErrorChatType = oldConfig.ErrorChatType;

                // Migrate macros from old tree structure
                if (oldConfig.RootFolder != null)
                {
                    MigrateMacrosFromOldStructure(oldConfig.RootFolder);
                }

                // Migrate other settings
                CraftSkip = oldConfig.CraftSkip;
                SmartWait = oldConfig.SmartWait;
                QualitySkip = oldConfig.QualitySkip;
                LoopTotal = oldConfig.LoopTotal;
                LoopEcho = oldConfig.LoopEcho;
                UseCraftLoopTemplate = oldConfig.UseCraftLoopTemplate;
                CraftLoopTemplate = oldConfig.CraftLoopTemplate;
                CraftLoopFromRecipeNote = oldConfig.CraftLoopFromRecipeNote;
                CraftLoopMaxWait = oldConfig.CraftLoopMaxWait;
                CraftLoopEcho = oldConfig.CraftLoopEcho;

                // Migrate AR settings if they exist
                if (oldConfig.ARCharacterPostProcessMacro is { } arMacro)
                {
                    // Find the macro by name in our new structure
                    var migratedMacro = GetMacroByName(arMacro.Name);
                    if (migratedMacro != null)
                    {
                        migratedMacro.Metadata.RunDuringARPostProcess = true;
                    }
                }

                ARCharacterPostProcessExcludedCharacters = new List<ulong>(oldConfig.ARCharacterPostProcessExcludedCharacters);

                // Migrate error settings
                MaxTimeoutRetries = oldConfig.MaxTimeoutRetries;
                NoisyErrors = oldConfig.NoisyErrors;
                BeepFrequency = oldConfig.BeepFrequency;
                BeepDuration = oldConfig.BeepDuration;
                BeepCount = oldConfig.BeepCount;

                // Migrate stop conditions
                StopMacroIfActionTimeout = oldConfig.StopMacroIfActionTimeout;
                StopMacroIfItemNotFound = oldConfig.StopMacroIfItemNotFound;
                StopMacroIfCantUseItem = oldConfig.StopMacroIfCantUseItem;
                StopMacroIfTargetNotFound = oldConfig.StopMacroIfTargetNotFound;
                StopMacroIfAddonNotFound = oldConfig.StopMacroIfAddonNotFound;
                StopMacroIfAddonNotVisible = oldConfig.StopMacroIfAddonNotVisible;

                // Migrate Lua settings
                LuaRequirePaths = oldConfig.LuaRequirePaths;
                UseMacroFileSystem = oldConfig.UseMacroFileSystem;

                // Log migration results
                Svc.Log.Info($"Migration completed. Total macros migrated: {Macros.Count}");
                foreach (var macro in Macros)
                {
                    Svc.Log.Info($"Migrated macro: {macro.Name} in {macro.FolderPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Failed to migrate configuration");
        }
    }

    private void MigrateMacrosFromOldStructure(dynamic rootFolder)
    {
        // First, determine the root folder name
        string? rootFolderName;
        try
        {
            if (rootFolder.Name != null)
            {
                rootFolderName = rootFolder.Name.ToString();
                Svc.Log.Info($"Root folder name: {rootFolderName}");
            }
            else
            {
                Svc.Log.Warning("Root folder has no name, using default");
                rootFolderName = "Root";
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Error determining root folder name");
            rootFolderName = "Root";
        }

        void TraverseFolderStructure(dynamic folder, string currentPath, bool isRoot = false)
        {
            if (folder == null) return;

            // Log the current folder for debugging
            Svc.Log.Info($"Traversing folder: {currentPath}");

            try
            {
                // Get the Children property safely
                var children = folder.Children;
                if (children == null)
                {
                    Svc.Log.Warning($"No Children property found in folder: {currentPath}");
                    return;
                }

                foreach (dynamic node in children)
                {
                    try
                    {
                        // Check if this is a macro node by looking for Contents property
                        if (node.Contents != null)
                        {
                            // This is a macro node
                            var macro = new ConfigMacro
                            {
                                Name = node.Name ?? "Unknown",
                                Type = node.Language?.ToString() == "1" ? MacroType.Lua : MacroType.Native,
                                Content = node.Contents.ToString(),
                                FolderPath = isRoot ? "/" : currentPath,
                                Metadata = new MacroMetadata
                                {
                                    LastModified = DateTime.Now,
                                    CraftingLoop = node.CraftingLoop ?? false,
                                    CraftLoopCount = node.CraftLoopCount ?? 0,
                                    TriggerEvents = node.isPostProcess ? [TriggerEvent.OnAutoRetainerCharacterPostProcess] : [],
                                }
                            };

                            Svc.Log.Info($"Adding macro: {macro.Name} in {macro.FolderPath}");
                            Macros.Add(macro);
                        }
                        else if (node.Name != null)
                        {
                            // This is a folder node
                            var folderName = node.Name.ToString();

                            // If this is the root folder's children, use "/" as the path
                            if (isRoot)
                            {
                                TraverseFolderStructure(node, "/");
                            }
                            else
                            {
                                // For other folders, build the path normally
                                var newPath = Path.Combine(currentPath, folderName).Replace('\\', '/');
                                TraverseFolderStructure(node, newPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error(ex, $"Error processing node in folder {currentPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, $"Error traversing folder {currentPath}");
            }
        }

        try
        {
            // Start with the root folder, marking it as the root
            TraverseFolderStructure(rootFolder, rootFolderName, true);
            Svc.Log.Info($"Migration completed. Total macros migrated: {Macros.Count}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Failed to traverse folder structure");
        }
    }

    public void ValidateMigration()
    {
        Svc.Log.Info($"Configuration version: {Version}");
        Svc.Log.Info($"Total macros: {Macros.Count}");
        foreach (var macro in Macros)
            Svc.Log.Info($"Macro: {macro.Name} in {macro.FolderPath}");
    }

    public void Save() => EzConfig.Save();

    /// <summary>
    /// Gets all macros in a specific folder.
    /// </summary>
    public IEnumerable<ConfigMacro> GetMacrosInFolder(string folderPath) => Macros.Where(m => m.FolderPath == folderPath);

    /// <summary>
    /// Gets all folder paths.
    /// </summary>
    public IEnumerable<string> GetFolderPaths() => Macros.Select(m => m.FolderPath).Distinct();

    /// <summary>
    /// Moves a macro to a different folder.
    /// </summary>
    public void MoveMacro(string macroId, string newFolderPath)
    {
        var macro = Macros.FirstOrDefault(m => m.Id == macroId);
        if (macro != null)
        {
            macro.FolderPath = newFolderPath;
        }
    }

    /// <summary>
    /// Deletes a macro.
    /// </summary>
    public void DeleteMacro(string macroId) => Macros.RemoveAll(m => m.Id == macroId);

    #region Helper Methods

    /// <summary>
    /// Gets a macro by its ID.
    /// </summary>
    public ConfigMacro? GetMacro(string macroId)
        => Macros.FirstOrDefault(m => m.Id == macroId);

    /// <summary>
    /// Gets a macro by its name, optionally in a specific folder.
    /// </summary>
    public ConfigMacro? GetMacroByName(string name, string? folderPath = null)
        => Macros.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            (folderPath == null || m.FolderPath == folderPath));

    /// <summary>
    /// Gets all macros of a specific type.
    /// </summary>
    public IEnumerable<ConfigMacro> GetMacrosByType(MacroType type)
        => Macros.Where(m => m.Type == type);

    /// <summary>
    /// Gets all macros in a folder and its subfolders.
    /// </summary>
    public IEnumerable<ConfigMacro> GetMacrosInFolderRecursive(string folderPath)
        => Macros.Where(m => m.FolderPath.StartsWith(folderPath));

    /// <summary>
    /// Gets the immediate subfolders of a folder.
    /// </summary>
    public IEnumerable<string> GetSubfolders(string folderPath)
    {
        var prefix = folderPath == "/" ? "" : folderPath + "/";
        return Macros
            .Select(m => m.FolderPath)
            .Where(p => p.StartsWith(prefix))
            .Select(p => p.Split('/', StringSplitOptions.RemoveEmptyEntries)[0])
            .Distinct()
            .Select(f => prefix + f);
    }

    /// <summary>
    /// Gets the full path of a macro.
    /// </summary>
    public string GetMacroPath(string macroId)
    {
        var macro = GetMacro(macroId);
        return macro == null ? string.Empty : Path.Combine(macro.FolderPath, macro.Name);
    }

    /// <summary>
    /// Gets the parent folder path of a macro.
    /// </summary>
    public string GetMacroParentFolder(string macroId)
    {
        var macro = GetMacro(macroId);
        if (macro == null) return string.Empty;

        var lastSlash = macro.FolderPath.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : macro.FolderPath[..lastSlash];
    }

    /// <summary>
    /// Validates if a macro name is valid for the given folder.
    /// </summary>
    public bool IsValidMacroName(string name, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return !GetMacrosInFolder(folderPath)
            .Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the number of macros in a folder.
    /// </summary>
    public int GetMacroCount(string folderPath)
        => GetMacrosInFolder(folderPath).Count();

    /// <summary>
    /// Gets the total number of macros in a folder and its subfolders.
    /// </summary>
    public int GetTotalMacroCount(string folderPath)
        => GetMacrosInFolderRecursive(folderPath).Count();

    /// <summary>
    /// Gets all macros that contain a specific text in their content.
    /// </summary>
    public IEnumerable<ConfigMacro> SearchMacros(string searchText, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return Macros.Where(m =>
            m.Name.Contains(searchText, comparison) ||
            m.Content.Contains(searchText, comparison));
    }

    /// <summary>
    /// Gets all folder paths and their depths in the tree.
    /// </summary>
    public IEnumerable<(string Path, int Depth)> GetFolderTree()
    {
        // Get all unique folder paths from macros
        var paths = Macros
            .Select(m => m.FolderPath)
            .Distinct()
            .OrderBy(p => p);

        // Track processed paths to avoid duplicates
        var processedPaths = new HashSet<string>();

        foreach (var path in paths)
        {
            // Skip the root folder itself - we don't want to display it
            if (path == "/")
                continue;

            // Split path into parts and calculate depth
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // If this is a direct child of root (depth 1), yield it with depth 0
            if (parts.Length == 1)
            {
                yield return (path, 0);
                processedPaths.Add(path);
                continue;
            }

            // For deeper paths, build each part of the path
            var currentPath = "";
            for (var i = 0; i < parts.Length; i++)
            {
                currentPath = i == 0 ? "/" + parts[i] : currentPath + "/" + parts[i];
                if (!currentPath.StartsWith("/"))
                    currentPath = "/" + currentPath;

                // Only yield if we haven't processed this path yet
                if (!processedPaths.Contains(currentPath))
                {
                    // For paths with depth > 1, adjust the depth to be relative to the first level
                    var adjustedDepth = i;
                    yield return (currentPath, adjustedDepth);
                    processedPaths.Add(currentPath);
                }
            }
        }
    }

    /// <summary>
    /// Gets the folder structure as a tree with macro counts.
    /// </summary>
    public IEnumerable<(string Path, int Depth, int Count)> GetFolderTreeWithCounts()
    {
        var allFolders = GetFolderTree().ToList();
        var folderCounts = new Dictionary<string, int>();

        // First calculate counts for all folders
        foreach (var macro in Macros)
        {
            var path = macro.FolderPath;
            if (!folderCounts.ContainsKey(path))
                folderCounts[path] = 0;
            folderCounts[path]++;
        }

        // Return folder info with counts
        foreach (var (path, depth) in allFolders)
        {
            var count = folderCounts.TryGetValue(path, out var c) ? c : 0;
            yield return (path, depth, count);
        }
    }

    /// <summary>
    /// Gets all nodes in the configuration.
    /// </summary>
    public IEnumerable<ConfigMacro> GetAllNodes() => Macros;

    /// <summary>
    /// Sets a property value by name.
    /// </summary>
    public void SetProperty(string name, string value)
    {
        var property = GetType().GetProperty(name);
        if (property == null) return;

        try
        {
            var convertedValue = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(this, convertedValue);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Failed to set property {name} to {value}");
        }
    }

    #endregion
}

public class ConfigFactory : ISerializationFactory
{
    public string DefaultConfigFileName => "SomethingNeedDoing.json";

    public bool IsBinary => false;

    public T? Deserialize<T>(string inputData)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(inputData, JsonSerializerSettings);
        }
        catch
        {
            return JsonConvert.DeserializeObject<T>(inputData);
        }
    }

    public string? Serialize(object data, bool pretty = false)
        => JsonConvert.SerializeObject(data, JsonSerializerSettings);
    public string? Serialize(object config) => Serialize(config, false);
    public T? Deserialize<T>(byte[] inputData) => Deserialize<T>(Encoding.UTF8.GetString(inputData));
    public byte[]? SerializeAsBin(object config) => Serialize(config) is { } serialized ? Encoding.UTF8.GetBytes(serialized) : [];

    public static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        SerializationBinder = new CustomSerializationBinder(),
        Formatting = Formatting.Indented,
    };

    public class CustomSerializationBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string? assemblyName, string typeName)
        {
            try
            {
                return base.BindToType(assemblyName, typeName);
            }
            catch (Exception)
            {
                if (assemblyName == null || assemblyName == typeof(Plugin).Assembly.GetName().Name)
                    return typeof(Plugin).Assembly.GetTypes().First(x => x.FullName == typeName);
                throw;
            }
        }
    }
}

