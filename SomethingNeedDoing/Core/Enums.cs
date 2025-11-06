namespace SomethingNeedDoing.Core;
/// <summary>
/// Defines the type of macro.
/// </summary>
public enum MacroType
{
    /// <summary>
    /// A native macro using the game's macro-like syntax.
    /// </summary>
    Native,

    /// <summary>
    /// A Lua script macro.
    /// </summary>
    Lua
}

/// <summary>
/// Defines the possible states of a macro.
/// </summary>
public enum MacroState
{
    /// <summary>
    /// The macro is either uninitialised or the state has not been properly tracked
    /// </summary>
    Unknown,

    /// <summary>
    /// The macro is ready to run but not yet started.
    /// </summary>
    Ready,

    /// <summary>
    /// The macro is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// The macro is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// The macro has completed execution.
    /// </summary>
    Completed,

    /// <summary>
    /// The macro has encountered an error.
    /// </summary>
    Error
}

/// <summary>
/// Defines the events that can trigger a macro
/// </summary>
public enum TriggerEvent
{
    None,
    OnAutoRetainerCharacterPostProcess,
    OnUpdate,
    OnConditionChange,
    OnCombatStart,
    OnCombatEnd,
    OnLogin,
    OnLogout,
    OnTerritoryChange,
    OnChatMessage,
    OnAddonEvent,
    OnActivePluginsChanged,
    OnDutyStarted,
    OnDutyWiped,
    OnDutyCompleted,
    OnEmote,
}

/// <summary>
/// Defines the types of control operations that can be performed on a macro.
/// </summary>
public enum MacroControlType
{
    /// <summary>
    /// Start the macro.
    /// </summary>
    Start,

    /// <summary>
    /// Pause the macro.
    /// </summary>
    Pause,

    /// <summary>
    /// Resume the macro.
    /// </summary>
    Resume,

    /// <summary>
    /// Stop the macro.
    /// </summary>
    Stop
}
