using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Text;
using SomethingNeedDoing.Core.Interfaces;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SomethingNeedDoing.Core;

/// <summary>
/// Parser for macro metadata using YAML format.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class MetadataParser
{
    private readonly DependencyFactory _dependencyFactory;

    public static readonly Regex MetadataBlockRegex = new(
        @"(?:--\[=====\[|/\*).*?\[\[SND\s*Metadata\]\](.*?)\[\[End\s*Metadata\]\].*?(?:\]=====\]|\*/)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    #region YAML schema
    // Loosely typed scalars are object so any YAML scalar deserializes (as a string) and is parsed leniently below.
    private sealed class MetadataYaml
    {
        public object? Author { get; set; }
        public object? Version { get; set; }
        public object? Description { get; set; }
        public List<string>? Triggers { get; set; }
        public List<string>? PluginDependencies { get; set; }
        public List<string>? PluginsToDisable { get; set; }
        public object? CraftingLoop { get; set; }
        public object? CraftLoopCount { get; set; }
        public AddonEventYaml? AddonEvent { get; set; }
        public ChatFilterYaml? ChatMessageFilter { get; set; }
        public Dictionary<string, ChatFilterYaml>? FunctionChatFilters { get; set; }
        public List<DependencyYaml>? Dependencies { get; set; }
        public Dictionary<string, object?>? Configs { get; set; } // a config is either a scalar default value or a map
    }

    private sealed class AddonEventYaml
    {
        public string? AddonName { get; set; }
        public string? EventType { get; set; }
    }

    private sealed class ChatFilterYaml
    {
        public List<string>? Channels { get; set; }
        public string? MessageContains { get; set; }
        public string? SenderContains { get; set; }
        public string? MessageRegex { get; set; }

        public ChatMessageFilterConfig ToConfig() => new()
        {
            Channels = Channels?.Select(c => Enum.TryParse<XivChatType>(c, true, out var type) ? type : (XivChatType?)null).OfType<XivChatType>().ToList(),
            MessageContains = MessageContains,
            SenderContains = SenderContains,
            MessageRegex = MessageRegex,
        };

        public static ChatFilterYaml From(ChatMessageFilterConfig filter) => new()
        {
            Channels = filter.Channels is { Count: > 0 } ? [.. filter.Channels.Select(c => c.ToString().ToLower())] : null,
            MessageContains = NullIfEmpty(filter.MessageContains),
            SenderContains = NullIfEmpty(filter.SenderContains),
            MessageRegex = NullIfEmpty(filter.MessageRegex),
        };
    }

    private sealed class DependencyYaml
    {
        public string? Source { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    private static List<T>? NullIfEmpty<T>(IEnumerable<T> values) => values.Any() ? [.. values] : null;
    #endregion

    /// <summary>
    /// Parses metadata from macro content.
    /// </summary>
    /// <param name="content">The macro content to parse.</param>
    /// <param name="previousMetadata">Optional: previous MacroMetadata to preserve config values.</param>
    /// <returns>The parsed metadata.</returns>
    public MacroMetadata ParseMetadata(string content, MacroMetadata? previousMetadata = null)
    {
        var match = MetadataBlockRegex.Match(content);
        if (!match.Success) return previousMetadata ?? new MacroMetadata(); // keep existing if available

        var metadataContent = match.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(metadataContent)) return previousMetadata ?? new MacroMetadata(); // keep existing if empty

        var metadata = previousMetadata ?? new MacroMetadata(); // use previous as a base so we're only overriding explicitly present fields

        try
        {
            if (_deserializer.Deserialize<MetadataYaml>(metadataContent) is not { } yaml) return metadata;

            if (yaml.Author != null)
                metadata.Author = yaml.Author.ToString() ?? string.Empty;

            if (yaml.Version != null)
                metadata.Version = yaml.Version.ToString() ?? "1.0.0";

            if (yaml.Description != null)
                metadata.Description = yaml.Description.ToString() ?? string.Empty;

            if (yaml.Triggers != null)
                metadata.TriggerEvents = [.. yaml.Triggers.Select(t => Enum.TryParse<TriggerEvent>(t, true, out var trigger) ? trigger : TriggerEvent.None).Where(t => t != TriggerEvent.None)];

            if (yaml.PluginDependencies != null)
                metadata.PluginDependecies = [.. yaml.PluginDependencies.Where(p => !string.IsNullOrEmpty(p))];

            if (yaml.PluginsToDisable != null)
                metadata.PluginsToDisable = [.. yaml.PluginsToDisable.Where(p => !string.IsNullOrEmpty(p))];

            if (yaml.CraftingLoop != null)
                metadata.CraftingLoop = yaml.CraftingLoop.ToString()?.ToLower() == "true";

            if (int.TryParse(yaml.CraftLoopCount?.ToString(), out var count))
                metadata.CraftLoopCount = count;

            if (yaml.AddonEvent != null)
            {
                metadata.AddonEventConfig = new AddonEventConfig { AddonName = yaml.AddonEvent.AddonName ?? string.Empty };
                if (Enum.TryParse<AddonEvent>(yaml.AddonEvent.EventType, true, out var eventType))
                    metadata.AddonEventConfig.EventType = eventType;
            }

            if (yaml.ChatMessageFilter != null)
                metadata.ChatMessageFilter = yaml.ChatMessageFilter.ToConfig();

            if (yaml.FunctionChatFilters != null)
                foreach (var (functionName, filter) in yaml.FunctionChatFilters)
                    if (!string.IsNullOrEmpty(functionName) && filter != null)
                        metadata.FunctionChatFilters[functionName] = filter.ToConfig();

            if (yaml.Dependencies != null)
                metadata.Dependencies = [.. yaml.Dependencies.Where(d => !string.IsNullOrEmpty(d?.Source)).Select(d => _dependencyFactory.CreateDependency(d.Source!))];

            if (yaml.Configs != null)
            {
                var previousConfigs = metadata.Configs; // metadata may be previousMetadata, so capture before replacing
                metadata.Configs = ParseConfigs(yaml.Configs);
                if (previousMetadata != null)
                    foreach (var (name, config) in metadata.Configs)
                        if (previousConfigs.TryGetValue(name, out var previous))
                            config.Value = previous.Value; // keep the user's value
            }
            else if (previousMetadata != null)
                // no configs in content = keep whatever was already in metadata
                metadata.Configs = [with(previousMetadata.Configs)];
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error(ex, "Failed to parse metadata YAML");
        }

        return metadata;
    }

    /// <summary>
    /// Writes metadata to macro content.
    /// </summary>
    /// <param name="macro">The macro to write metadata to.</param>
    /// <param name="onContentUpdated">Optional callback to call when content is updated.</param>
    /// <returns>True if the metadata was written successfully.</returns>
    public bool WriteMetadata(ConfigMacro macro, Action? onContentUpdated = null)
    {
        if (macro == null) return false;

        var metadata = macro.Metadata;
        var yamlModel = new MetadataYaml
        {
            Author = NullIfEmpty(metadata.Author),
            Version = NullIfEmpty(metadata.Version),
            Description = NullIfEmpty(metadata.Description),
            Triggers = NullIfEmpty(metadata.TriggerEvents.Select(t => t.ToString().ToLower())),
            PluginDependencies = NullIfEmpty(metadata.PluginDependecies),
            PluginsToDisable = NullIfEmpty(metadata.PluginsToDisable),
            CraftingLoop = metadata.CraftingLoop ? true : null,
            CraftLoopCount = metadata.CraftLoopCount > 0 ? metadata.CraftLoopCount : null,
            AddonEvent = metadata.AddonEventConfig is { } addon ? new AddonEventYaml { AddonName = addon.AddonName, EventType = addon.EventType.ToString().ToLower() } : null,
            ChatMessageFilter = metadata.ChatMessageFilter is { } filter ? ChatFilterYaml.From(filter) : null,
            FunctionChatFilters = metadata.FunctionChatFilters.Count > 0 ? metadata.FunctionChatFilters.ToDictionary(kvp => kvp.Key, kvp => ChatFilterYaml.From(kvp.Value)) : null,
            Dependencies = NullIfEmpty(metadata.Dependencies.Select(dep => new DependencyYaml
            {
                Source = dep.Source,
                Name = dep.Name,
                Type = dep switch
                {
                    GitDependency => "git",
                    LocalMacroDependency => "macro",
                    LocalDependency => "file",
                    _ => "unknown"
                }
            })),
            Configs = metadata.Configs.Count > 0 ? metadata.Configs.ToDictionary(kvp => kvp.Key, kvp => SerializeConfig(kvp.Value)) : null,
        };

        var yaml = _serializer.Serialize(yamlModel);
        var (startComment, endComment) = GetCommentSyntax(macro.Type);
        var metadataBlock = $"{startComment}\n[[SND Metadata]]\n{yaml}\n[[End Metadata]]\n{endComment}";

        var match = MetadataBlockRegex.Match(macro.Content);
        if (match.Success)
        {
            var afterMetadata = macro.Content[(match.Index + match.Length)..]; // for ensuring there's a new line after the end
            macro.Content = macro.Content[..match.Index] + metadataBlock + (afterMetadata.StartsWith('\n') || afterMetadata.StartsWith('\r') ? "" : "\n") + afterMetadata;
        }
        else
            macro.Content = metadataBlock + (macro.Content.StartsWith('\n') ? "" : "\n") + macro.Content;

        C.Save();
        onContentUpdated?.Invoke(); // there's probably a better way
        return true;
    }

    /// <summary>
    /// Gets the appropriate comment syntax for the given macro type.
    /// </summary>
    /// <param name="macroType">The type of macro.</param>
    /// <returns>Tuple of (startComment, endComment) strings.</returns>
    private static (string startComment, string endComment) GetCommentSyntax(MacroType macroType)
        => macroType switch
        {
            MacroType.Lua => ("--[=====[", "--]=====]"),
            _ => ("", "")
        };

    #region Configs
    private static object? SerializeConfig(MacroConfigItem config)
    {
        var isSimpleConfig = string.IsNullOrEmpty(config.Description) &&
                             string.IsNullOrEmpty(config.Section) &&
                             config.MinValue == null &&
                             config.MaxValue == null &&
                             string.IsNullOrEmpty(config.ValidationPattern) &&
                             string.IsNullOrEmpty(config.ValidationMessage) &&
                             config.Choices.Count == 0 &&
                             config.Type != typeof(List<string>);
        if (isSimpleConfig)
            return config.Value;

        var configDict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(config.DefaultValue?.ToString()))
            configDict["default"] = config.DefaultValue;

        if (!string.IsNullOrEmpty(config.Description))
            configDict["description"] = config.Description;

        if (!string.IsNullOrEmpty(config.Section))
            configDict["section"] = config.Section;

        if (config.Type != typeof(string))
            configDict["type"] = config.TypeName;

        if (config.MinValue != null)
            configDict["min"] = config.MinValue;

        if (config.MaxValue != null)
            configDict["max"] = config.MaxValue;

        if (!string.IsNullOrEmpty(config.ValidationPattern))
            configDict["validation_pattern"] = config.ValidationPattern;

        if (!string.IsNullOrEmpty(config.ValidationMessage))
            configDict["validation_message"] = config.ValidationMessage;

        if (config.Type == typeof(List<string>) || (config.IsChoice && config.Type == typeof(string)))
        {
            if (config.Choices.Count > 0)
                configDict["choices"] = config.Choices;

            if (config.IsChoice)
                configDict["is_choice"] = true;
        }

        return configDict;
    }

    private static Dictionary<string, MacroConfigItem> ParseConfigs(Dictionary<string, object?> configDict)
    {
        var result = new Dictionary<string, MacroConfigItem>();

        foreach (var (configName, value) in configDict)
        {
            if (string.IsNullOrEmpty(configName)) continue;

            var configItem = new MacroConfigItem();

            if (value is Dictionary<object, object> configData)
            {
                if (configData.TryGetValue("default", out var defaultValue))
                    configItem.DefaultValue = defaultValue ?? string.Empty;
                else if (configData.ContainsKey("choices"))
                    configItem.DefaultValue = new List<object>(); // if no default for lists, we want the default value to be a list so the type can be inferred properly

                configItem.Description = configData.GetValueOrDefault("description")?.ToString() ?? string.Empty;
                configItem.Section = configData.GetValueOrDefault("section")?.ToString() ?? string.Empty;

                if (configData.TryGetValue("type", out var type))
                    configItem.TypeName = type?.ToString() ?? "string";
                else
                    configItem.Type = InferTypeFromValue(configItem.DefaultValue);

                if (configData.TryGetValue("min", out var min))
                    configItem.MinValue = min;

                if (configData.TryGetValue("max", out var max))
                    configItem.MaxValue = max;

                if (configData.TryGetValue("validation_pattern", out var pattern))
                    configItem.ValidationPattern = pattern?.ToString();

                if (configData.TryGetValue("validation_message", out var message))
                    configItem.ValidationMessage = message?.ToString();

                if (configData.GetValueOrDefault("choices") is List<object> choiceList)
                    configItem.Choices = [.. choiceList.Select(c => c?.ToString() ?? string.Empty).Where(c => !string.IsNullOrEmpty(c))];

                if (configData.TryGetValue("is_choice", out var isChoice))
                    configItem.IsChoice = isChoice?.ToString()?.ToLower() == "true";
            }
            else
            {
                configItem.DefaultValue = value ?? string.Empty;
                configItem.Description = configName;
                configItem.Type = InferTypeFromValue(configItem.DefaultValue);
            }

            if (configItem.IsChoice && (configItem.Type == typeof(List<string>) || configItem.Type == typeof(string)))
            {
                var defaultValue = configItem.DefaultValue?.ToString() ?? string.Empty;
                configItem.Value = configItem.Choices.Contains(defaultValue) ? defaultValue : configItem.Choices.FirstOrDefault() ?? string.Empty;
            }
            else if (configItem.Type == typeof(List<string>))
                configItem.Value = configItem.DefaultValue is List<object> defaultList ? [.. defaultList.Select(x => x?.ToString() ?? string.Empty)] : new List<string>();
            else
                configItem.Value = configItem.DefaultValue;

            result[configName] = configItem;
        }

        return result;
    }

    private static Type InferTypeFromValue(object? value)
    {
        if (value == null) return typeof(string);

        if (value is bool) return typeof(bool);
        if (value is int or long) return typeof(int);
        if (value is float or double) return typeof(float);
        if (value is List<object> or List<string>) return typeof(List<string>);

        if (value is string stringValue) // in case someone does "bool" or whatever
        {
            if (bool.TryParse(stringValue, out _))
                return typeof(bool);
            if (int.TryParse(stringValue, out _))
                return typeof(int);
            if (float.TryParse(stringValue, out _))
                return typeof(float);
        }

        return typeof(string);
    }
    #endregion
}
