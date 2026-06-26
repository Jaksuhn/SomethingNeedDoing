using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Text;
using Newtonsoft.Json;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.Core;
/// <summary>
/// Represents metadata for a macro.
/// </summary>
public class MacroMetadata : IEquatable<MacroMetadata>
{
    /// <summary>
    /// Gets or sets any <see cref="TriggerEvent"/> that determine when a macro can automatically be triggered.
    /// </summary>
    public List<TriggerEvent> TriggerEvents { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this macro should loop automatically during crafting.
    /// </summary>
    public bool CraftingLoop { get; set; }

    /// <summary>
    /// Gets or sets how many loops this macro should run if crafting loop is enabled.
    /// </summary>
    public int CraftLoopCount { get; set; }

    /// <summary>
    /// Gets or sets the description of the macro.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the macro.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the macro.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the last modified date of the macro.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets any additional metadata as a dictionary.
    /// </summary>
    public Dictionary<string, string> AdditionalData { get; set; } = [];

    /// <summary>
    /// Gets or sets the macro configuration items.
    /// </summary>
    public Dictionary<string, MacroConfigItem> Configs { get; set; } = [];

    /// <summary>
    /// Gets or sets the addon event configuration for this macro.
    /// </summary>
    public AddonEventConfig? AddonEventConfig { get; set; }

    /// <summary>
    /// Gets or sets the plugin dependencies for this macro.
    /// </summary>
    /// <remarks>string is the InternalName of a plugin</remarks>
    public string[] PluginDependecies { get; set; } = [];

    /// <summary>
    /// Gets or sets the plugins that should be disabled while this macro is running.
    /// </summary>
    /// <remarks>string is the InternalName of a plugin that implements IDisableable</remarks>
    public string[] PluginsToDisable { get; set; } = [];

    /// <summary>
    /// Gets or sets the macro dependencies.
    /// </summary>
    [JsonProperty(ItemConverterType = typeof(ConfigFactory.IMacroDependencyConverter))]
    public List<IMacroDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the chat message filter configuration for macro-level OnChatMessage triggers.
    /// </summary>
    public ChatMessageFilterConfig? ChatMessageFilter { get; set; }

    /// <summary>
    /// Gets or sets chat message filter configurations for function-level triggers.
    /// Key is the function name (e.g., "OnChatMessage"), value is the filter configuration.
    /// </summary>
    public Dictionary<string, ChatMessageFilterConfig> FunctionChatFilters { get; set; } = [];

    public bool Equals(MacroMetadata? other)
    {
        if (other is null)
            return false;

        return Author == other.Author &&
               Version == other.Version &&
               Description == other.Description &&
               CraftingLoop == other.CraftingLoop &&
               CraftLoopCount == other.CraftLoopCount &&
               TriggerEvents.SequenceEqual(other.TriggerEvents) &&
               PluginDependecies.SequenceEqual(other.PluginDependecies) &&
               PluginsToDisable.SequenceEqual(other.PluginsToDisable) &&
               ConfigsEqual(Configs, other.Configs) &&
               Equals(ChatMessageFilter, other.ChatMessageFilter) &&
               FunctionChatFiltersEqual(FunctionChatFilters, other.FunctionChatFilters) &&
               Equals(AddonEventConfig, other.AddonEventConfig);
    }

    public override bool Equals(object? obj) => obj is MacroMetadata other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Author, StringComparer.Ordinal);
        hash.Add(Version, StringComparer.Ordinal);
        hash.Add(Description, StringComparer.Ordinal);
        hash.Add(CraftingLoop);
        hash.Add(CraftLoopCount);
        foreach (var trigger in TriggerEvents)
            hash.Add(trigger);
        foreach (var dep in PluginDependecies)
            hash.Add(dep, StringComparer.Ordinal);
        foreach (var plugin in PluginsToDisable)
            hash.Add(plugin, StringComparer.Ordinal);
        foreach (var cfg in Configs.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(cfg.Key, StringComparer.Ordinal);
            hash.Add(cfg.Value);
        }
        hash.Add(ChatMessageFilter);
        foreach (var filter in FunctionChatFilters.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(filter.Key, StringComparer.Ordinal);
            hash.Add(filter.Value);
        }
        hash.Add(AddonEventConfig);
        return hash.ToHashCode();
    }

    private static bool ConfigsEqual(Dictionary<string, MacroConfigItem> a, Dictionary<string, MacroConfigItem> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var other) || !Equals(kvp.Value, other))
                return false;
        }

        return true;
    }

    private static bool FunctionChatFiltersEqual(Dictionary<string, ChatMessageFilterConfig> a, Dictionary<string, ChatMessageFilterConfig> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var other) || !Equals(kvp.Value, other))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Configuration for addon event triggers.
/// </summary>
public class AddonEventConfig : IEquatable<AddonEventConfig>
{
    /// <summary>
    /// Gets or sets the name of the addon to monitor.
    /// </summary>
    public string AddonName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of addon event to monitor.
    /// </summary>
    public AddonEvent EventType { get; set; } = AddonEvent.PostSetup;

    public bool Equals(AddonEventConfig? other)
        => other is not null && AddonName == other.AddonName && EventType == other.EventType;

    public override bool Equals(object? obj) => obj is AddonEventConfig other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(AddonName, EventType);
}

/// <summary>
/// Configuration for filtering chat messages.
/// </summary>
public class ChatMessageFilterConfig : IEquatable<ChatMessageFilterConfig>
{
    /// <summary>
    /// Gets or sets the chat channels to filter by. If null or empty, all channels are allowed.
    /// </summary>
    public List<XivChatType>? Channels { get; set; }

    /// <summary>
    /// Gets or sets a string that the message must contain. If null or empty, no message content filter is applied.
    /// </summary>
    public string? MessageContains { get; set; }

    /// <summary>
    /// Gets or sets a string that the sender must contain. If null or empty, no sender filter is applied.
    /// </summary>
    public string? SenderContains { get; set; }

    /// <summary>
    /// Gets or sets a regex pattern that the message must match. If null or empty, no regex filter is applied.
    /// </summary>
    public string? MessageRegex { get; set; }

    public bool Equals(ChatMessageFilterConfig? other)
    {
        if (other is null)
            return false;

        var channels = Channels?.OrderBy(c => c).ToList() ?? [];
        var otherChannels = other.Channels?.OrderBy(c => c).ToList() ?? [];

        return channels.SequenceEqual(otherChannels) &&
               MessageContains == other.MessageContains &&
               SenderContains == other.SenderContains &&
               MessageRegex == other.MessageRegex;
    }

    public override bool Equals(object? obj) => obj is ChatMessageFilterConfig other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var channel in (Channels ?? []).OrderBy(c => c))
            hash.Add(channel);
        hash.Add(MessageContains, StringComparer.Ordinal);
        hash.Add(SenderContains, StringComparer.Ordinal);
        hash.Add(MessageRegex, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
