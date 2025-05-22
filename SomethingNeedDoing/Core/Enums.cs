namespace SomethingNeedDoing.Framework;
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

/// <summary>
/// Defines addon lifecycle events that can trigger macros
/// </summary>
public enum MacroAddonEvent
{
    /// <summary>
    /// Triggered when the addon starts being set up
    /// </summary>
    PreSetup,
    
    /// <summary>
    /// Triggered when the addon has been set up
    /// </summary>
    PostSetup,
    
    /// <summary>
    /// Triggered before the addon is drawn
    /// </summary>
    PreDraw,
    
    /// <summary>
    /// Triggered after the addon is drawn
    /// </summary>
    PostDraw,
    
    /// <summary>
    /// Triggered before the addon is updated
    /// </summary>
    PreUpdate,
    
    /// <summary>
    /// Triggered after the addon is updated
    /// </summary>
    PostUpdate,
    
    /// <summary>
    /// Triggered before the addon is finalized/closed
    /// </summary>
    PreFinalize,
    
    /// <summary>
    /// Triggered after the addon is finalized/closed
    /// </summary>
    PostFinalize,
    
    /// <summary>
    /// Triggered when the addon receives focus
    /// </summary>
    PreRequestFocus,
    
    /// <summary>
    /// Triggered after the addon receives focus
    /// </summary>
    PostRequestFocus
}
