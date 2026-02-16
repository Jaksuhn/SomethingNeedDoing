using Dalamud.Game.Text;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Managers;

/// <summary>
/// Represents a function that can be triggered by an event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TriggerFunction"/> class.
/// </remarks>
/// <param name="macro">The macro containing the function.</param>
/// <param name="functionName">The name of the function.</param>
/// <param name="eventType">The trigger event this function handles.</param>
/// <param name="chatMessageFilter">Optional chat message filter configuration.</param>
public class TriggerFunction(IMacro macro, string functionName, TriggerEvent eventType, ChatMessageFilterConfig? chatMessageFilter = null)
{
    /// <summary>
    /// Gets the macro containing this function.
    /// </summary>
    public IMacro Macro { get; } = macro;

    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    public string FunctionName { get; } = functionName;

    /// <summary>
    /// Gets the trigger event this function handles.
    /// </summary>
    public TriggerEvent EventType { get; } = eventType;

    /// <summary>
    /// Gets the addon name for OnAddonEvent triggers.
    /// </summary>
    public string? AddonName { get; } = eventType == TriggerEvent.OnAddonEvent && functionName.StartsWith("OnAddonEvent_")
        ? functionName.Split('_')[1]
        : null;

    /// <summary>
    /// Gets the event type for OnAddonEvent triggers.
    /// </summary>
    public string? AddonEventType { get; } = eventType == TriggerEvent.OnAddonEvent && functionName.StartsWith("OnAddonEvent_")
        ? functionName.Split('_')[2]
        : null;

    /// <summary>
    /// Gets the chat message filter configuration for OnChatMessage triggers.
    /// </summary>
    public ChatMessageFilterConfig? ChatMessageFilter { get; } = chatMessageFilter;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is TriggerFunction other && Macro.Id == other.Macro.Id && FunctionName == other.FunctionName;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Macro.Id, FunctionName);
}

/// <summary>
/// Manages trigger events for macros.
/// </summary>
public class TriggerEventManager : IDisposable
{
    public Dictionary<TriggerEvent, List<TriggerFunction>> EventHandlers { get; } = [];

    /// <summary>
    /// Event raised when a trigger event occurs.
    /// </summary>
    public event EventHandler<TriggerEventArgs>? TriggerEventOccurred;

    /// <summary>
    /// Event raised when a function execution is requested.
    /// </summary>
    public event EventHandler<FunctionExecutionRequestedEventArgs>? FunctionExecutionRequested;

    /// <summary>
    /// Registers a macro to handle a specific trigger event.
    /// </summary>
    /// <param name="macro">The macro to register.</param>
    /// <param name="eventType">The type of event to register for.</param>
    public void RegisterTrigger(IMacro macro, TriggerEvent eventType)
    {
        if (!EventHandlers.ContainsKey(eventType))
            EventHandlers[eventType] = [];

        ChatMessageFilterConfig? chatFilter = null;
        if (eventType == TriggerEvent.OnChatMessage && macro.Metadata.ChatMessageFilter != null)
            chatFilter = macro.Metadata.ChatMessageFilter;

        var triggerFunction = new TriggerFunction(macro, string.Empty, eventType, chatFilter);
        if (!EventHandlers[eventType].Contains(triggerFunction))
            EventHandlers[eventType].Add(triggerFunction);
    }

    /// <summary>
    /// Registers a function within a macro to handle a trigger event based on its name.
    /// </summary>
    /// <param name="macro">The macro containing the function.</param>
    /// <param name="functionName">The name of the function to register.</param>
    public void RegisterFunctionTrigger(IMacro macro, string functionName)
    {
        // Check for OnAddonEvent function name pattern
        if (functionName.StartsWith("OnAddonEvent_"))
        {
            var parts = functionName.Split('_');
            if (parts.Length >= 3) // OnAddonEvent_AddonName_EventType (anything after is ignored)
            {
                if (!EventHandlers.ContainsKey(TriggerEvent.OnAddonEvent))
                    EventHandlers[TriggerEvent.OnAddonEvent] = [];

                var triggerFunction = new TriggerFunction(macro, functionName, TriggerEvent.OnAddonEvent);
                if (EventHandlers[TriggerEvent.OnAddonEvent].Contains(triggerFunction))
                {
                    FrameworkLogger.Debug($"Function trigger already registered for macro {macro.Name} function {functionName} (Addon: {parts[1]}, Event: {parts[2]})");
                    return;
                }
                FrameworkLogger.Debug($"Registering OnAddonEvent trigger for macro {macro.Name} function {functionName} (Addon: {parts[1]}, Event: {parts[2]})");
                EventHandlers[TriggerEvent.OnAddonEvent].Add(triggerFunction);
                return;
            }
        }

        // Check if the function name starts with any trigger event name
        foreach (var eventType in Enum.GetValues<TriggerEvent>())
        {
            if (eventType == TriggerEvent.None) continue;

            if (functionName.StartsWith(eventType.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (!EventHandlers.ContainsKey(eventType))
                    EventHandlers[eventType] = [];

                ChatMessageFilterConfig? chatFilter = null;
                if (eventType == TriggerEvent.OnChatMessage && macro.Metadata.FunctionChatFilters.TryGetValue(functionName, out var filter))
                    chatFilter = filter;

                var triggerFunction = new TriggerFunction(macro, functionName, eventType, chatFilter);
                if (EventHandlers[eventType].Contains(triggerFunction))
                {
                    FrameworkLogger.Debug($"Function trigger already registered for macro {macro.Name} function {functionName} event {eventType}");
                    return;
                }
                FrameworkLogger.Debug($"Registering trigger event {eventType} for macro {macro.Name} function {functionName}");
                EventHandlers[eventType].Add(triggerFunction);
                return;
            }
        }
    }

    /// <summary>
    /// Unregisters a macro from handling a specific trigger event.
    /// </summary>
    /// <param name="macro">The macro to unregister.</param>
    /// <param name="eventType">The type of event to unregister from.</param>
    public void UnregisterTrigger(IMacro macro, TriggerEvent eventType)
    {
        if (EventHandlers.TryGetValue(eventType, out var value))
            value.RemoveAll(tf => tf.Macro.Id == macro.Id && string.IsNullOrEmpty(tf.FunctionName));
    }

    /// <summary>
    /// Unregisters a function within a macro from handling its trigger event.
    /// </summary>
    /// <param name="macro">The macro containing the function.</param>
    /// <param name="functionName">The name of the function to unregister.</param>
    public void UnregisterFunctionTrigger(IMacro macro, string functionName)
    {
        // Check for OnAddonEvent function name pattern
        if (functionName.StartsWith("OnAddonEvent_"))
        {
            var parts = functionName.Split('_');
            if (parts.Length >= 3) // OnAddonEvent_AddonName_EventType (anything after is ignored)
            {
                if (EventHandlers.TryGetValue(TriggerEvent.OnAddonEvent, out var value))
                {
                    var removed = value.RemoveAll(tf => tf.Macro.Id == macro.Id && tf.FunctionName == functionName);
                    if (removed > 0)
                        FrameworkLogger.Debug($"Unregistering OnAddonEvent trigger for macro {macro.Name} function {functionName}");
                }
                return;
            }
        }

        // Check if the function name starts with any trigger event name
        foreach (var eventType in Enum.GetValues<TriggerEvent>())
        {
            if (eventType == TriggerEvent.None) continue;

            if (functionName.StartsWith(eventType.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (EventHandlers.TryGetValue(eventType, out var value))
                {
                    var removed = value.RemoveAll(tf => tf.Macro.Id == macro.Id && tf.FunctionName == functionName);
                    if (removed > 0)
                        FrameworkLogger.Debug($"Unregistering trigger event {eventType} for macro {macro.Name} function {functionName}");
                }
                return;
            }
        }
    }

    /// <summary>
    /// Unregisters a macro from all trigger events.
    /// </summary>
    /// <param name="macro">The macro to unregister.</param>
    public void UnregisterAllTriggers(IMacro macro)
    {
        foreach (var handlers in EventHandlers.Values)
            handlers.RemoveAll(tf => tf.Macro.Id == macro.Id);
    }

    /// <summary>
    /// Raises a trigger event to all registered macros and functions.
    /// </summary>
    /// <param name="eventType">The type of event to raise.</param>
    /// <param name="data">Optional data associated with the event.</param>
    public async Task RaiseTriggerEvent(TriggerEvent eventType, object? data = null)
    {
        if (!EventHandlers.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
            return;

        var args = new TriggerEventArgs(eventType, data);
        foreach (var triggerFunction in handlers.ToList())
        {
            try
            {
                // For OnAddonEvent, check if the addon name and event type match
                if (eventType == TriggerEvent.OnAddonEvent && data is Dictionary<string, object> addonEventData)
                {
                    addonEventData.TryGetValue("AddonName", out var addonNameObj);
                    addonEventData.TryGetValue("EventType", out var addonEventTypeObj);
                    var addonName = addonNameObj as string;
                    var addonEventType = addonEventTypeObj as string;

                    if (addonName != triggerFunction.AddonName || addonEventType != triggerFunction.AddonEventType)
                        continue;
                }

                // For OnChatMessage, check if the message matches the filter
                if (eventType == TriggerEvent.OnChatMessage && data is Dictionary<string, object> chatData)
                    if (!MatchesChatMessageFilter(chatData, triggerFunction.ChatMessageFilter))
                        continue;

                if (string.IsNullOrEmpty(triggerFunction.FunctionName))
                {
                    // Macro-level trigger: raise the event for the entire macro
                    FrameworkLogger.Verbose($"Raising trigger event {eventType} for macro {triggerFunction.Macro.Name}");
                    TriggerEventOccurred?.Invoke(triggerFunction.Macro, args);
                }
                else
                {
                    // For function-level triggers, request function execution via event (doing this to avoid circular dependencies)
                    if (triggerFunction.Macro.Type == MacroType.Lua)
                    {
                        FrameworkLogger.Verbose($"Requesting function execution for {triggerFunction.FunctionName} in macro {triggerFunction.Macro.Name}");
                        FunctionExecutionRequested?.Invoke(this, new FunctionExecutionRequestedEventArgs(triggerFunction.Macro.Id, triggerFunction.FunctionName, args));
                    }
                    else
                        // I could technically support this, but I don't think it's necessary
                        throw new NotSupportedException($"[{nameof(TriggerEventManager)}] Trigger event handling for {triggerFunction.Macro.Type} macros is not supported.");
                }
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Error handling trigger event {eventType} for macro {triggerFunction.Macro.Name} function {triggerFunction.FunctionName}: {ex}");
            }
        }
    }

    /// <summary>
    /// Checks if a chat message matches the given filter configuration.
    /// </summary>
    /// <param name="chatData">The chat message data dictionary.</param>
    /// <param name="filter">The filter configuration to check against.</param>
    /// <returns>True if the message matches the filter (or if no filter is specified), false otherwise.</returns>
    private static bool MatchesChatMessageFilter(Dictionary<string, object> chatData, ChatMessageFilterConfig? filter)
    {
        if (filter == null)
            return true;

        if (filter.Channels != null && filter.Channels.Count > 0)
        {
            if (chatData.TryGetValue("type", out var typeObj) && typeObj is XivChatType chatType)
            {
                if (!filter.Channels.Contains(chatType))
                    return false;
            }
            else
                return false;
        }

        if (!string.IsNullOrEmpty(filter.MessageContains))
        {
            if (chatData.TryGetValue("message", out var messageObj) && messageObj is string message)
            {
                if (!message.Contains(filter.MessageContains, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
                return false;
        }

        if (!string.IsNullOrEmpty(filter.SenderContains))
        {
            if (chatData.TryGetValue("sender", out var senderObj) && senderObj is string sender)
            {
                if (!sender.Contains(filter.SenderContains, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
                return false;
        }

        if (!string.IsNullOrEmpty(filter.MessageRegex))
        {
            if (chatData.TryGetValue("message", out var messageObj) && messageObj is string message)
            {
                try
                {
                    if (!Regex.IsMatch(message, filter.MessageRegex, RegexOptions.IgnoreCase))
                        return false;
                }
                catch (ArgumentException)
                {
                    FrameworkLogger.Warning($"Invalid regex pattern in chat message filter: {filter.MessageRegex}");
                    return false;
                }
            }
            else
                return false;
        }

        return true;
    }

    /// <summary>
    /// Disposes of the trigger event manager.
    /// </summary>
    public void Dispose() => EventHandlers.Clear();
}
