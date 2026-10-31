using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Text;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.Managers;

/// <summary>
/// A macro, or a function within a macro, that handles a trigger event.
/// </summary>
/// <param name="Macro">The macro to start, or containing the function.</param>
/// <param name="FunctionName">The function to call, or empty for a macro-level trigger.</param>
/// <param name="EventType">The trigger event handled.</param>
/// <param name="AddonName">For <see cref="TriggerEvent.OnAddonEvent"/>, the addon to match.</param>
/// <param name="AddonEventType">For <see cref="TriggerEvent.OnAddonEvent"/>, the addon event to match.</param>
/// <param name="ChatMessageFilter">For <see cref="TriggerEvent.OnChatMessage"/>, the filter to match.</param>
public sealed record TriggerFunction(IMacro Macro, string FunctionName, TriggerEvent EventType, string? AddonName = null, AddonEvent? AddonEventType = null, ChatMessageFilterConfig? ChatMessageFilter = null)
{
    public bool Equals(TriggerFunction? other) => other is not null && Macro.Id == other.Macro.Id && FunctionName == other.FunctionName;
    public override int GetHashCode() => HashCode.Combine(Macro.Id, FunctionName);
}

/// <summary>
/// Manages trigger events for macros.
/// </summary>
[RegisterSingleton]
public class TriggerEventManager : IDisposable
{
    public Dictionary<TriggerEvent, List<TriggerFunction>> EventHandlers { get; } = [];

    /// <summary>
    /// Event raised when a macro-level trigger event occurs. The sender is the macro.
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
        => Add(eventType switch
        {
            TriggerEvent.OnChatMessage => new TriggerFunction(macro, string.Empty, eventType, ChatMessageFilter: macro.Metadata.ChatMessageFilter),
            TriggerEvent.OnAddonEvent => new TriggerFunction(macro, string.Empty, eventType, macro.Metadata.AddonEventConfig?.AddonName, macro.Metadata.AddonEventConfig?.EventType),
            _ => new TriggerFunction(macro, string.Empty, eventType),
        });

    /// <summary>
    /// Registers a function within a macro to handle the trigger event its name refers to.
    /// </summary>
    public void RegisterFunctionTrigger(IMacro macro, FunctionInfo function)
    {
        if (function.TriggerEvent is not { } eventType) return;
        var chatFilter = eventType == TriggerEvent.OnChatMessage ? macro.Metadata.FunctionChatFilters.GetValueOrDefault(function.Name) : null;
        Add(new TriggerFunction(macro, function.Name, eventType, function.AddonName, function.AddonEvent, chatFilter));
    }

    private void Add(TriggerFunction trigger)
    {
        var handlers = EventHandlers.TryGetValue(trigger.EventType, out var list) ? list : EventHandlers[trigger.EventType] = [];
        if (handlers.Contains(trigger)) return;
        FrameworkLogger.Debug($"Registering trigger event {trigger.EventType} for macro {trigger.Macro.Name} {trigger.FunctionName}");
        handlers.Add(trigger);
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
    /// Unregisters all function-level triggers of a macro.
    /// </summary>
    public void UnregisterFunctionTriggers(IMacro macro)
    {
        foreach (var handlers in EventHandlers.Values)
            handlers.RemoveAll(tf => tf.Macro.Id == macro.Id && !string.IsNullOrEmpty(tf.FunctionName));
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
    public void RaiseTriggerEvent(TriggerEvent eventType, object? data = null)
    {
        if (!EventHandlers.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
            return;

        var args = new TriggerEventArgs(eventType, data);
        foreach (var triggerFunction in handlers.ToList())
        {
            try
            {
                if (eventType == TriggerEvent.OnAddonEvent && data is Dictionary<string, object> addonEventData)
                {
                    if (addonEventData.GetValueOrDefault("AddonName") as string != triggerFunction.AddonName
                        || addonEventData.GetValueOrDefault("type") as AddonEvent? != triggerFunction.AddonEventType)
                        continue;
                }

                if (eventType == TriggerEvent.OnChatMessage && data is Dictionary<string, object> chatData)
                    if (!MatchesChatMessageFilter(chatData, triggerFunction.ChatMessageFilter))
                        continue;

                if (string.IsNullOrEmpty(triggerFunction.FunctionName))
                {
                    FrameworkLogger.Verbose($"Raising trigger event {eventType} for macro {triggerFunction.Macro.Name}");
                    TriggerEventOccurred?.Invoke(triggerFunction.Macro, args);
                }
                else
                {
                    // request function execution via event (doing this to avoid circular dependencies)
                    FrameworkLogger.Verbose($"Requesting function execution for {triggerFunction.FunctionName} in macro {triggerFunction.Macro.Name}");
                    FunctionExecutionRequested?.Invoke(this, new FunctionExecutionRequestedEventArgs(triggerFunction.Macro.Id, triggerFunction.FunctionName, args));
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

        if (filter.Channels is { Count: > 0 } && !(chatData.GetValueOrDefault("type") is XivChatType chatType && filter.Channels.Contains(chatType)))
            return false;

        var message = chatData.GetValueOrDefault("message") as string;
        var sender = chatData.GetValueOrDefault("sender") as string;

        if (!string.IsNullOrEmpty(filter.MessageContains) && message?.Contains(filter.MessageContains, StringComparison.OrdinalIgnoreCase) != true)
            return false;

        if (!string.IsNullOrEmpty(filter.SenderContains) && sender?.Contains(filter.SenderContains, StringComparison.OrdinalIgnoreCase) != true)
            return false;

        if (!string.IsNullOrEmpty(filter.MessageRegex))
        {
            if (message == null)
                return false;
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

        return true;
    }

    /// <summary>
    /// Disposes of the trigger event manager.
    /// </summary>
    public void Dispose() => EventHandlers.Clear();
}
