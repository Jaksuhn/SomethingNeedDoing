using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Text;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.Core;
/// <summary>
/// Represents metadata for a macro.
/// </summary>
public class MacroMetadata
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
}

/// <summary>
/// Configuration for addon event triggers.
/// </summary>
public class AddonEventConfig
{
    /// <summary>
    /// Gets or sets the name of the addon to monitor.
    /// </summary>
    public string AddonName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of addon event to monitor.
    /// </summary>
    public AddonEvent EventType { get; set; } = AddonEvent.PostSetup;
}

/// <summary>
/// Configuration for filtering chat messages.
/// </summary>
public class ChatMessageFilterConfig
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
}
