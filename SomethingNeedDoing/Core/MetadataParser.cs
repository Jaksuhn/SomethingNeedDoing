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
public class MetadataParser(DependencyFactory dependencyFactory)
{
    public static readonly Regex MetadataBlockRegex = new(
        @"(?:--\[=====\[|/\*).*?\[\[SND\s*Metadata\]\](.*?)\[\[End\s*Metadata\]\].*?(?:\]=====\]|\*/)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

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
            if (_deserializer.Deserialize<Dictionary<string, object>>(metadataContent) is not { } yaml) return metadata;

            if (yaml.TryGetValue("author", out var author))
                metadata.Author = author?.ToString() ?? string.Empty;

            if (yaml.TryGetValue("version", out var version))
                metadata.Version = version?.ToString() ?? "1.0.0";

            if (yaml.TryGetValue("description", out var description))
                metadata.Description = description?.ToString() ?? string.Empty;

            if (yaml.TryGetValue("triggers", out var triggers))
            {
                if (triggers is List<object> triggerList)
                {
                    metadata.TriggerEvents = [.. triggerList
                        .Select(t => t.ToString())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Select(t => Enum.TryParse<TriggerEvent>(t, true, out var trigger) ? trigger : TriggerEvent.None)
                        .Where(t => t != TriggerEvent.None)];
                }
            }

            if (yaml.TryGetValue("plugin_dependencies", out var pluginDeps))
                if (pluginDeps is List<object> pluginList)
                    metadata.PluginDependecies = [.. pluginList.Select(p => p.ToString() ?? string.Empty).Where(p => !string.IsNullOrEmpty(p))];

            if (yaml.TryGetValue("plugins_to_disable", out var pluginsToDisable))
                if (pluginsToDisable is List<object> disableList)
                    metadata.PluginsToDisable = [.. disableList.Select(p => p.ToString() ?? string.Empty).Where(p => !string.IsNullOrEmpty(p))];

            if (yaml.TryGetValue("crafting_loop", out var craftingLoop))
                metadata.CraftingLoop = craftingLoop?.ToString()?.ToLower() == "true";

            if (yaml.TryGetValue("craft_loop_count", out var craftLoopCount))
                if (int.TryParse(craftLoopCount?.ToString(), out var count))
                    metadata.CraftLoopCount = count;

            if (yaml.TryGetValue("addon_event", out var addonEvent) && addonEvent is Dictionary<object, object> addonDict)
            {
                metadata.AddonEventConfig = new AddonEventConfig();

                if (addonDict.TryGetValue("addon_name", out var addonName))
                    metadata.AddonEventConfig.AddonName = addonName?.ToString() ?? string.Empty;

                if (addonDict.TryGetValue("event_type", out var eventType))
                    if (Enum.TryParse<AddonEvent>(eventType?.ToString(), true, out var parsedEventType))
                        metadata.AddonEventConfig.EventType = parsedEventType;
            }

            if (yaml.TryGetValue("chat_message_filter", out var chatFilter) && chatFilter is Dictionary<object, object> chatFilterDict)
                metadata.ChatMessageFilter = ParseChatMessageFilter(chatFilterDict);

            if (yaml.TryGetValue("function_chat_filters", out var functionChatFilters) && functionChatFilters is Dictionary<object, object> functionFiltersDict)
            {
                foreach (var kvp in functionFiltersDict)
                {
                    var functionName = kvp.Key?.ToString();
                    if (string.IsNullOrEmpty(functionName)) continue;

                    if (kvp.Value is Dictionary<object, object> filterDict)
                        metadata.FunctionChatFilters[functionName] = ParseChatMessageFilter(filterDict);
                }
            }

            if (yaml.TryGetValue("dependencies", out var dependencies))
                metadata.Dependencies = ParseDependencies(dependencies);

            if (yaml.TryGetValue("configs", out var configs) && configs is Dictionary<object, object> configDict)
            {
                var parsedConfigs = ParseConfigs(configDict);
                if (previousMetadata != null)
                {
                    var preservedValues = new Dictionary<string, object>(); // cache old
                    foreach (var prev in previousMetadata.Configs)
                        preservedValues[prev.Key] = prev.Value.Value;

                    metadata.Configs = new Dictionary<string, MacroConfigItem>(parsedConfigs); // make new

                    foreach (var prev in previousMetadata.Configs) // add any not present in new
                        if (!metadata.Configs.ContainsKey(prev.Key))
                            metadata.Configs[prev.Key] = prev.Value;

                    foreach (var kvp in metadata.Configs) // overwrite with cached if they exist
                        if (preservedValues.TryGetValue(kvp.Key, out var preservedValue))
                            kvp.Value.Value = preservedValue;
                }
                else
                    metadata.Configs = parsedConfigs;
            }
            else if (previousMetadata != null)
                // no configs in content = keep whatever was already in metadata
                metadata.Configs = new Dictionary<string, MacroConfigItem>(previousMetadata.Configs);
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

        var metadataDict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(macro.Metadata.Author))
            metadataDict["author"] = macro.Metadata.Author;

        if (!string.IsNullOrEmpty(macro.Metadata.Version))
            metadataDict["version"] = macro.Metadata.Version;

        if (!string.IsNullOrEmpty(macro.Metadata.Description))
            metadataDict["description"] = macro.Metadata.Description;

        if (macro.Metadata.TriggerEvents.Any())
            metadataDict["triggers"] = macro.Metadata.TriggerEvents.Select(t => t.ToString().ToLower()).ToList();

        if (macro.Metadata.PluginDependecies.Any())
            metadataDict["plugin_dependencies"] = macro.Metadata.PluginDependecies.ToList();

        if (macro.Metadata.PluginsToDisable.Any())
            metadataDict["plugins_to_disable"] = macro.Metadata.PluginsToDisable.ToList();

        if (macro.Metadata.CraftingLoop)
            metadataDict["crafting_loop"] = true;

        if (macro.Metadata.CraftLoopCount > 0)
            metadataDict["craft_loop_count"] = macro.Metadata.CraftLoopCount;

        if (macro.Metadata.AddonEventConfig != null)
        {
            metadataDict["addon_event"] = new Dictionary<string, object>
            {
                ["addon_name"] = macro.Metadata.AddonEventConfig.AddonName,
                ["event_type"] = macro.Metadata.AddonEventConfig.EventType.ToString().ToLower()
            };
        }

        if (macro.Metadata.ChatMessageFilter != null)
            metadataDict["chat_message_filter"] = SerializeChatMessageFilter(macro.Metadata.ChatMessageFilter);

        if (macro.Metadata.FunctionChatFilters.Any())
        {
            metadataDict["function_chat_filters"] = macro.Metadata.FunctionChatFilters.ToDictionary(
                kvp => kvp.Key,
                kvp => SerializeChatMessageFilter(kvp.Value)
            );
        }

        if (macro.Metadata.Dependencies.Any())
        {
            metadataDict["dependencies"] = macro.Metadata.Dependencies.Select(dep => new Dictionary<string, object>
            {
                ["source"] = dep.Source,
                ["name"] = dep.Name,
                ["type"] = dep switch
                {
                    GitDependency => "git",
                    LocalMacroDependency => "macro",
                    LocalDependency => "file",
                    _ => "unknown"
                }
            }).ToList();
        }

        if (macro.Metadata.Configs.Any())
        {
            metadataDict["configs"] = macro.Metadata.Configs.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var isSimpleConfig = string.IsNullOrEmpty(kvp.Value.Description) &&
                                        kvp.Value.MinValue == null &&
                                        kvp.Value.MaxValue == null &&
                                        string.IsNullOrEmpty(kvp.Value.ValidationPattern) &&
                                        string.IsNullOrEmpty(kvp.Value.ValidationMessage) &&
                                        !kvp.Value.Choices.Any() &&
                                        kvp.Value.Type != typeof(List<string>);

                    if (isSimpleConfig)
                        return kvp.Value.Value;

                    var configDict = new Dictionary<string, object>();

                    if (!string.IsNullOrEmpty(kvp.Value.DefaultValue?.ToString()))
                        configDict["default"] = kvp.Value.DefaultValue;

                    if (!string.IsNullOrEmpty(kvp.Value.Description))
                        configDict["description"] = kvp.Value.Description;

                    if (kvp.Value.Type != typeof(string))
                        configDict["type"] = kvp.Value.TypeName;

                    if (kvp.Value.MinValue != null)
                        configDict["min"] = kvp.Value.MinValue;

                    if (kvp.Value.MaxValue != null)
                        configDict["max"] = kvp.Value.MaxValue;

                    if (!string.IsNullOrEmpty(kvp.Value.ValidationPattern))
                        configDict["validation_pattern"] = kvp.Value.ValidationPattern;

                    if (!string.IsNullOrEmpty(kvp.Value.ValidationMessage))
                        configDict["validation_message"] = kvp.Value.ValidationMessage;

                    if (kvp.Value.Type == typeof(List<string>))
                    {
                        if (kvp.Value.Choices.Any())
                            configDict["choices"] = kvp.Value.Choices;

                        if (kvp.Value.IsChoice)
                            configDict["is_choice"] = true;
                    }
                    else if (kvp.Value.IsChoice && kvp.Value.Type == typeof(string))
                    {
                        if (kvp.Value.Choices.Any())
                            configDict["choices"] = kvp.Value.Choices;

                        configDict["is_choice"] = true;
                    }

                    return configDict;
                }
            );
        }

        var yaml = _serializer.Serialize(metadataDict);
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

    private List<IMacroDependency> ParseDependencies(object dependencies)
    {
        var result = new List<IMacroDependency>();

        if (dependencies is List<object> depList)
        {
            foreach (var dep in depList)
            {
                if (dep is Dictionary<object, object> depDict)
                {
                    var source = depDict.TryGetValue("source", out var s) ? s?.ToString() : string.Empty;
                    if (!string.IsNullOrEmpty(source))
                        result.Add(dependencyFactory.CreateDependency(source));
                }
            }
        }
        return result;
    }

    private Dictionary<string, MacroConfigItem> ParseConfigs(Dictionary<object, object> configDict)
    {
        var result = new Dictionary<string, MacroConfigItem>();

        foreach (var kvp in configDict)
        {
            var configName = kvp.Key.ToString();
            if (string.IsNullOrEmpty(configName)) continue;

            var configItem = new MacroConfigItem();

            if (kvp.Value is Dictionary<object, object> configData)
            {
                if (configData.TryGetValue("default", out var defaultValue))
                    configItem.DefaultValue = defaultValue ?? string.Empty;
                else if (configData.ContainsKey("choices"))
                    configItem.DefaultValue = new List<object>(); // if no default for lists, we want the default value to be a list so the type can be inferred properly

                if (configData.TryGetValue("description", out var description))
                    configItem.Description = description?.ToString() ?? string.Empty;

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

                if (configData.TryGetValue("choices", out var choices))
                    if (choices is List<object> choiceList)
                        configItem.Choices = [.. choiceList.Select(c => c?.ToString() ?? string.Empty).Where(c => !string.IsNullOrEmpty(c))];

                if (configData.TryGetValue("is_choice", out var isChoiceList))
                    configItem.IsChoice = isChoiceList?.ToString()?.ToLower() == "true";
            }
            else
            {
                configItem.DefaultValue = kvp.Value ?? string.Empty;
                configItem.Description = configName;
                configItem.Type = InferTypeFromValue(configItem.DefaultValue);
            }

            if (configItem.Type == typeof(List<string>))
            {
                if (configItem.IsChoice)
                {
                    var defaultValue = configItem.DefaultValue?.ToString() ?? string.Empty;
                    if (configItem.Choices.Contains(defaultValue))
                        configItem.Value = defaultValue;
                    else
                        configItem.Value = configItem.Choices.Any() ? configItem.Choices.First() : string.Empty;
                }
                else
                    configItem.Value = configItem.DefaultValue is List<object> defaultList
                        ? [.. defaultList.Select(x => x?.ToString() ?? string.Empty)]
                        : new List<string>();
            }
            else if (configItem.IsChoice && configItem.Type == typeof(string))
            {
                var defaultValue = configItem.DefaultValue?.ToString() ?? string.Empty;
                if (configItem.Choices.Contains(defaultValue))
                    configItem.Value = defaultValue;
                else
                    configItem.Value = configItem.Choices.Any() ? configItem.Choices.First() : string.Empty;
            }
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
        if (value is int) return typeof(int);
        if (value is long) return typeof(int);
        if (value is float) return typeof(float);
        if (value is double) return typeof(float);
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

    private static ChatMessageFilterConfig ParseChatMessageFilter(Dictionary<object, object> filterDict)
    {
        var filter = new ChatMessageFilterConfig();

        if (filterDict.TryGetValue("channels", out var channels))
        {
            if (channels is List<object> channelList)
            {
                filter.Channels = [];
                foreach (var channel in channelList)
                    if (Enum.TryParse<XivChatType>(channel?.ToString(), true, out var chatType))
                        filter.Channels.Add(chatType);
            }
        }

        if (filterDict.TryGetValue("message_contains", out var messageContains))
            filter.MessageContains = messageContains?.ToString();

        if (filterDict.TryGetValue("sender_contains", out var senderContains))
            filter.SenderContains = senderContains?.ToString();

        if (filterDict.TryGetValue("message_regex", out var messageRegex))
            filter.MessageRegex = messageRegex?.ToString();

        return filter;
    }

    private static Dictionary<string, object> SerializeChatMessageFilter(ChatMessageFilterConfig filter)
    {
        var dict = new Dictionary<string, object>();

        if (filter.Channels != null && filter.Channels.Count > 0)
            dict["channels"] = filter.Channels.Select(c => c.ToString().ToLower()).ToList();

        if (!string.IsNullOrEmpty(filter.MessageContains))
            dict["message_contains"] = filter.MessageContains;

        if (!string.IsNullOrEmpty(filter.SenderContains))
            dict["sender_contains"] = filter.SenderContains;

        if (!string.IsNullOrEmpty(filter.MessageRegex))
            dict["message_regex"] = filter.MessageRegex;

        return dict;
    }
}
