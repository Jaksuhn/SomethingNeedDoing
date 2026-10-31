using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.Core.Events;

/// <summary>
/// Event arguments for macro state changes.
/// </summary>
public class MacroStateChangedEventArgs(string macroId, MacroState newState, MacroState previousState) : EventArgs
{
    /// <summary>
    /// Gets the ID of the macro whose state changed.
    /// </summary>
    public string MacroId { get; } = macroId;

    /// <summary>
    /// Gets the new state of the macro.
    /// </summary>
    public MacroState NewState { get; } = newState;

    /// <summary>
    /// Gets the previous state of the macro.
    /// </summary>
    public MacroState PreviousState { get; } = previousState;
}

/// <summary>
/// Event arguments for when a macro's content changes.
/// </summary>
/// <param name="macroId">The ID of the macro whose content changed.</param>
/// <param name="newContent">The new content of the macro.</param>
/// <param name="oldContent">The previous content of the macro.</param>
public class MacroContentChangedEventArgs(string macroId, string newContent, string oldContent) : EventArgs
{
    /// <summary>
    /// Gets the ID of the macro whose content changed.
    /// </summary>
    public string MacroId { get; } = macroId;

    /// <summary>
    /// Gets the new content of the macro.
    /// </summary>
    public string NewContent { get; } = newContent;

    /// <summary>
    /// Gets the previous content of the macro.
    /// </summary>
    public string OldContent { get; } = oldContent;
}

/// <summary>
/// Event arguments for when a macro execution is requested.
/// </summary>
/// <remarks>
/// For engine to scheduler communication
/// </remarks>
/// <param name="macro">The macro to execute.</param>
/// <param name="triggerArgs">Optional trigger event arguments.</param>
/// <param name="loopCount">The loop count for the macro execution.</param>
public class MacroExecutionRequestedEventArgs(IMacro macro, TriggerEventArgs? triggerArgs = null, int loopCount = 0) : EventArgs
{
    /// <summary>
    /// Gets the macro to execute.
    /// </summary>
    public IMacro Macro { get; } = macro;

    /// <summary>
    /// Gets the trigger event arguments, if any.
    /// </summary>
    public TriggerEventArgs? TriggerArgs { get; } = triggerArgs;

    /// <summary>
    /// Gets the loop count for the macro execution.
    /// </summary>
    public int LoopCount { get; } = loopCount;
}

/// <summary>
/// Event arguments for loop control requests.
/// </summary>
/// <param name="macroId">The ID of the macro requesting loop control.</param>
/// <param name="controlType">The type of loop control being requested.</param>
public class LoopControlEventArgs(string macroId, LoopControlType controlType) : EventArgs
{
    /// <summary>
    /// Gets the ID of the macro requesting loop control.
    /// </summary>
    public string MacroId { get; } = macroId;

    /// <summary>
    /// Gets the type of loop control being requested.
    /// </summary>
    public LoopControlType ControlType { get; } = controlType;
}

/// <summary>
/// Types of loop control that can be requested.
/// </summary>
public enum LoopControlType
{
    /// <summary>
    /// Request to pause at the next loop.
    /// </summary>
    Pause,

    /// <summary>
    /// Request to stop at the next loop.
    /// </summary>
    Stop
}

/// <summary>
/// Event arguments for function execution requests.
/// </summary>
/// <param name="macroId">The ID of the macro containing the function.</param>
/// <param name="functionName">The name of the function to execute.</param>
/// <param name="triggerArgs">The trigger event arguments that caused the function execution.</param>
public class FunctionExecutionRequestedEventArgs(string macroId, string functionName, TriggerEventArgs triggerArgs) : EventArgs
{
    /// <summary>
    /// Gets the ID of the macro containing the function.
    /// </summary>
    public string MacroId { get; } = macroId;

    /// <summary>
    /// Gets the name of the function to execute.
    /// </summary>
    public string FunctionName { get; } = functionName;

    /// <summary>
    /// Gets the trigger event arguments that caused the function execution.
    /// </summary>
    public TriggerEventArgs TriggerArgs { get; } = triggerArgs;
}

/// <summary>
/// Arguments passed to macros when triggered by events.
/// </summary>
/// <param name="eventType">The type of event that triggered the macro.</param>
/// <param name="data">The data associated with the event.</param>
public class TriggerEventArgs(TriggerEvent eventType, object? data = null)
{
    /// <summary>
    /// Gets the type of event that triggered the macro.
    /// </summary>
    public TriggerEvent EventType { get; } = eventType;

    /// <summary>
    /// Gets the data associated with the event.
    /// </summary>
    public object? Data { get; } = data;

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>
    /// Gets the data associated with the event as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the data to.</typeparam>
    /// <returns>The data cast to the specified type, or null if the cast fails.</returns>
    public T? GetData<T>() => Data is T value ? value : default;
}
