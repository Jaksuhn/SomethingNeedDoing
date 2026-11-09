using Dalamud.Game.Text;
using ECommons.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SomethingNeedDoing.Core.Interfaces;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing;
/// <summary>
/// Configuration for the plugin.
/// </summary>
public class Config : IHostedService
{
    public int Version { get; set; } = 2;

    public static event Action? ConfigFileChanged;

    private FileSystemWatcher? _configWatcher;
    private DateTime _lastConfigChange = DateTime.MinValue;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dir = EzConfig.GetPluginConfigDirectory();
            _configWatcher = new FileSystemWatcher(dir, EzConfig.DefaultSerializationFactory.DefaultConfigFileName)
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Created += OnConfigFileChanged;
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Failed to initialize config file watcher");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_configWatcher is null)
            return Task.CompletedTask;

        _configWatcher.Changed -= OnConfigFileChanged;
        _configWatcher.Created -= OnConfigFileChanged;
        _configWatcher.Dispose();
        _configWatcher = null;
        return Task.CompletedTask;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        if ((DateTime.Now - _lastConfigChange).TotalMilliseconds < 500) // debounce rapid changes
            return;

        _lastConfigChange = DateTime.Now;
        Svc.Framework.RunOnTick(() => ConfigFileChanged?.Invoke());
    }

    #region General Settings
    public XivChatType ChatType { get; set; } = XivChatType.Debug;
    public XivChatType ErrorChatType { get; set; } = XivChatType.Urgent;

    /// <summary>
    /// Gets or sets whether pausing a macro should also pause its child macros.
    /// </summary>
    public bool PropagateControlsToChildren { get; set; } = true;
    public bool AutoOpenStatusWindow { get; set; } = false;

    public bool HasCompletedTutorial { get; set; } = false;
    public bool AcknowledgedLegacyWarning { get; set; } = false;
    #endregion

    #region Crafting Settings
    /// <summary>
    /// Gets or sets a value indicating whether to skip craft actions when not crafting.
    /// </summary>
    public bool CraftSkip { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to intelligently wait for crafting actions to complete instead of using wait modifiers.
    /// </summary>
    public bool SmartWait { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip quality increasing actions when at 100% HQ chance.
    /// </summary>
    public bool QualitySkip { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to count the /loop number as the total iterations, rather than the amount to loop.
    /// </summary>
    public bool LoopTotal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to always echo /loop commands.
    /// </summary>
    public bool LoopEcho { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the "CraftLoop" template.
    /// </summary>
    public bool UseCraftLoopTemplate { get; set; }

    /// <summary>
    /// Gets or sets the "CraftLoop" template.
    /// </summary>
    public string CraftLoopTemplate { get; set; } =
        "/craft {{count}}\n" +
        "/waitaddon \"RecipeNote\" <maxwait.5>\n" +
        "/click RecipeNote Synthesize\n" +
        "/waitaddon \"Synthesis\" <maxwait.5>\n" +
        "{{macro}}\n" +
        "/loop";

    /// <summary>
    /// Gets or sets a value indicating whether to start crafting loops from the recipe note window.
    /// </summary>
    public bool CraftLoopFromRecipeNote { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum wait value for the "CraftLoop" maxwait modifier.
    /// </summary>
    public int CraftLoopMaxWait { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether the "CraftLoop" loop should have an echo modifier.
    /// </summary>
    public bool CraftLoopEcho { get; set; }
    #endregion

    #region Error Handling
    /// <summary>
    /// Gets or sets a value indicating whether to stop the macro on error (only used for natives)
    /// </summary>
    public bool StopOnError { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries when an action does not receive a timely response.
    /// </summary>
    public int MaxTimeoutRetries { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether errors should be audible.
    /// </summary>
    public bool NoisyErrors { get; set; }

    /// <summary>
    /// Gets or sets the beep frequency.
    /// </summary>
    public int BeepFrequency { get; set; } = 900;

    /// <summary>
    /// Gets or sets the beep duration.
    /// </summary>
    public int BeepDuration { get; set; } = 250;

    /// <summary>
    /// Gets or sets the beep count.
    /// </summary>
    public int BeepCount { get; set; } = 3;
    #endregion

    #region AutoRetainer Integration
    public List<ulong> ARCharacterPostProcessExcludedCharacters { get; set; } = [];
    #endregion

    #region Lua Settings
    public string[] LuaRequirePaths { get; set; } = [];
    #endregion

    #region Changelog
    public string LastSeenVersion = string.Empty;
    #endregion

    public List<ConfigMacro> Macros { get; set; } = [];

    /// <summary>Explicit display order for the flat folder list. Missing entries use the legacy alphabetical order.</summary>
    public List<string> FolderOrder { get; set; } = [];

    public void Save() => EzConfig.Save();

    /// <summary>
    /// Gets all macros in a specific folder.
    /// </summary>
    public IEnumerable<ConfigMacro> GetMacrosInFolder(string folderPath) => (Macros ?? [])
        .Select((macro, index) => (macro, index))
        .Where(entry => entry.macro is not null && entry.macro.FolderPath == folderPath)
        .OrderBy(entry => entry.macro.SortOrder ?? int.MaxValue)
        .ThenBy(entry => entry.index)
        .Select(entry => entry.macro);

    /// <summary>
    /// Gets all folder paths.
    /// </summary>
    public IEnumerable<string> GetFolderPaths()
    {
        var folders = (Macros ?? [])
            .Where(macro => macro is not null)
            .Select(macro => macro.FolderPath)
            .Where(folder => !string.IsNullOrEmpty(folder))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Config files from older versions may omit this list, explicitly store null,
        // or contain duplicate entries. Keep the first valid position without mutating it.
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        if (FolderOrder is not null)
            for (var i = 0; i < FolderOrder.Count; i++)
                if (!string.IsNullOrEmpty(FolderOrder[i]))
                    order.TryAdd(FolderOrder[i], i);

        return folders.OrderBy(f => order.TryGetValue(f, out var index) ? index : int.MaxValue)
            .ThenBy(f => f, StringComparer.Ordinal);
    }

    /// <summary>Moves one macro to a folder and position without changing its content or settings.</summary>
    public bool MoveMacro(string macroId, string destinationFolder, int destinationIndex)
    {
        var macro = GetMacro(macroId);
        if (macro is null || macro.FolderPath == destinationFolder && destinationIndex < 0)
            return false;

        var sourceFolder = macro.FolderPath;
        var source = GetMacrosInFolder(sourceFolder).ToList();
        var destination = sourceFolder == destinationFolder ? source : GetMacrosInFolder(destinationFolder).ToList();
        var sourceIndex = source.IndexOf(macro);
        if (sourceFolder == destinationFolder && sourceIndex >= 0 && sourceIndex < destinationIndex)
            destinationIndex--;
        if (sourceFolder == destinationFolder && destinationIndex >= 0 && sourceIndex == destinationIndex)
            return false;

        macro.FolderPath = destinationFolder;
        destination.Remove(macro);
        var insertAt = destinationIndex < 0 ? destination.Count : Math.Clamp(destinationIndex, 0, destination.Count);
        destination.Insert(insertAt, macro);
        for (var i = 0; i < destination.Count; i++)
            destination[i].SortOrder = i;
        if (sourceFolder != destinationFolder)
            for (var i = 0; i < source.Count; i++)
                source[i].SortOrder = i;

        Save();
        return true;
    }

    /// <summary>Moves a folder within the flat folder list.</summary>
    public bool MoveFolder(string folderPath, int destinationIndex)
    {
        var folders = GetFolderPaths().ToList();
        var sourceIndex = folders.IndexOf(folderPath);
        if (sourceIndex < 0 || destinationIndex < 0 || destinationIndex > folders.Count)
            return false;

        folders.RemoveAt(sourceIndex);
        if (sourceIndex < destinationIndex)
            destinationIndex--;
        if (sourceIndex == destinationIndex)
            return false;
        folders.Insert(Math.Clamp(destinationIndex, 0, folders.Count), folderPath);
        FolderOrder = folders;
        Save();
        return true;
    }

    #region Helper Methods

    /// <summary>
    /// Gets a macro by its ID.
    /// </summary>
    public ConfigMacro? GetMacro(string macroId)
    {
        // ids never change, so an indexed hit is valid as long as the macro is still at that index
        var macros = Macros ?? [];
        if (_macroIndex.TryGetValue(macroId, out var i) && i < macros.Count && macros[i].Id == macroId)
            return macros[i];

        // misses fall back to a scan, and the index is only rebuilt when that finds the macro
        if (macros.FindIndex(m => m.Id == macroId) is not (>= 0 and var found))
            return null;

        var index = new Dictionary<string, int>(macros.Count);
        for (i = 0; i < macros.Count; i++)
            index.TryAdd(macros[i].Id, i);
        _macroIndex = index;
        return macros[found];
    }

    [JsonIgnore] private Dictionary<string, int> _macroIndex = [];

    /// <summary>
    /// Gets a macro by its name, optionally in a specific folder.
    /// </summary>
    public ConfigMacro? GetMacroByName(string name, string? folderPath = null)
        => Macros.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            (folderPath == null || m.FolderPath == folderPath));

    /// <summary>
    /// Validates if a macro name is valid for the given folder.
    /// </summary>
    public bool IsValidMacroName(string name, string? excludeMacroId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return !Macros.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && m.Id != excludeMacroId);
    }

    /// <summary>
    /// Gets the number of macros in a folder.
    /// </summary>
    public int GetMacroCount(string folderPath)
        => GetMacrosInFolder(folderPath).Count();

    /// <summary>
    /// Gets all macros that contain a specific text in their content.
    /// </summary>
    public IEnumerable<ConfigMacro> SearchMacros(string searchText, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return (Macros ?? []).Where(m => m is not null &&
            (m.Name.Contains(searchText, comparison) || m.Content.Contains(searchText, comparison)));
    }

    /// <summary>
    /// Gets a unique macro name by appending a number if the base name already exists.
    /// </summary>
    public string GetUniqueMacroName(string baseName, string? excludeMacroId = null)
    {
        var name = baseName;
        var counter = 1;
        while (!IsValidMacroName(name, excludeMacroId))
        {
            name = $"{baseName} ({counter})";
            counter++;
        }
        return name;
    }

    /// <summary>
    /// Renames a folder by updating all macros in that folder.
    /// </summary>
    public void RenameFolder(string oldFolderPath, string newFolderPath)
    {
        if (string.IsNullOrWhiteSpace(oldFolderPath) || string.IsNullOrWhiteSpace(newFolderPath))
            return;

        if (oldFolderPath == newFolderPath)
            return;

        if (GetFolderPaths().Any(f => f == newFolderPath))
            return;

        foreach (var macro in GetMacrosInFolder(oldFolderPath).ToList())
            macro.FolderPath = newFolderPath;

        if (FolderOrder is not null)
        {
            var folderIndex = FolderOrder.IndexOf(oldFolderPath);
            if (folderIndex >= 0)
                FolderOrder[folderIndex] = newFolderPath;
        }

        Save();
    }
    #endregion

    #region Migrations
    public static void Migrate(Config c)
    {
        if (c.Version is 1)
        {
            FrameworkLogger.Info("Migration config v1 -> v2");
            var oldTemplate =
                "/craft {{count}}\n" +
                "/waitaddon \"RecipeNote\" <maxwait.5>" +
                "/click \"RecipeNote Synthesize\"" +
                "/waitaddon \"Synthesis\" <maxwait.5>" +
                "{{macro}}" +
                "/loop";

            if (string.Join("", oldTemplate.Where(c => !char.IsWhiteSpace(c))) == string.Join("", c.CraftLoopTemplate.Where(c => !char.IsWhiteSpace(c))))
            {
                c.CraftLoopTemplate =
                    "/craft {{count}}\n" +
                    "/waitaddon \"RecipeNote\" <maxwait.5>\n" +
                    "/click RecipeNote Synthesize\n" +
                    "/waitaddon \"Synthesis\" <maxwait.5>\n" +
                    "{{macro}}\n" +
                    "/loop";
                FrameworkLogger.Info("Migrated broken craft loop template");
            }
            c.Version = 2;
        }
        c.Save();
    }
    #endregion
}

public class ConfigFactory : DefaultSerializationFactory, ISerializationFactory
{
    public new string DefaultConfigFileName => "ezSomethingNeedDoing.json";

    public new T? Deserialize<T>(string inputData)
        => JsonConvert.DeserializeObject<T>(inputData, JsonSerializerSettings);

    public new string? Serialize(object data, bool pretty = false)
        => JsonConvert.SerializeObject(data, JsonSerializerSettings);

    public static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        SerializationBinder = new CustomSerializationBinder(),
        Formatting = Formatting.Indented,
        Converters = [new IMacroDependencyConverter()]
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

    public class IMacroDependencyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(IMacroDependency);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null!;

            var jObject = JObject.Load(reader);
            var source = jObject["Source"]?.ToString() ?? string.Empty;

            Type concreteType = source switch
            {
                var s when s.StartsWith("git://") || s.Contains("github.com") => typeof(GitDependency),
                var s when Guid.TryParse(s, out _) => typeof(LocalMacroDependency),
                var s when s.StartsWith("http://") || s.StartsWith("https://") => typeof(HttpDependency),
                var s when !string.IsNullOrEmpty(s) && (s.Contains('\\') || s.Contains('/')) => typeof(LocalDependency),
                _ => throw new JsonException($"Unknown source type [{source}]. Please report to the author."),
            };

            jObject.Remove("$type");

            using var newReader = jObject.CreateReader();
            var inner = JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ContractResolver = serializer.ContractResolver,
                SerializationBinder = serializer.SerializationBinder,
                NullValueHandling = serializer.NullValueHandling,
                DefaultValueHandling = serializer.DefaultValueHandling,
                DateParseHandling = serializer.DateParseHandling,
                DateTimeZoneHandling = serializer.DateTimeZoneHandling,
                FloatParseHandling = serializer.FloatParseHandling,
                Culture = serializer.Culture,
                MaxDepth = serializer.MaxDepth,
            });
            return inner.Deserialize(newReader, concreteType)!;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => serializer.Serialize(writer, value);
    }
}
