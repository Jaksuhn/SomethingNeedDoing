using AutoRetainerAPI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.NativeMacro;
using SomethingNeedDoing.LuaMacro;

namespace SomethingNeedDoing.Scheduler;
/// <summary>
/// Manages and coordinates execution of multiple macros.
/// </summary>
public class MacroScheduler : IMacroScheduler, IDisposable
{
    private readonly ConcurrentDictionary<string, IMacroEngine> _enginesByMacroId = [];
    private readonly ConcurrentDictionary<string, MacroExecutionState> _macroStates = [];
    private readonly Dictionary<string, AutoRetainerApi> _arApis = [];
    private readonly Dictionary<string, AddonEventConfig> _addonEvents = [];
    private readonly MacroHierarchyManager _macroHierarchy = new();

    private readonly NativeMacroEngine _nativeEngine;
    private readonly NLuaMacroEngine _luaEngine;
    private readonly TriggerEventManager _triggerEventManager;

    /// <summary>
    /// Event raised when any macro's state changes.
    /// </summary>
    public event EventHandler<MacroStateChangedEventArgs>? MacroStateChanged;

    /// <summary>
    /// Event raised when any macro encounters an error.
    /// </summary>
    public event EventHandler<MacroErrorEventArgs>? MacroError;

    public MacroScheduler(NativeMacroEngine nativeEngine, NLuaMacroEngine luaEngine, TriggerEventManager triggerEventManager)
    {
        _nativeEngine = nativeEngine;
        _luaEngine = luaEngine;
        _triggerEventManager = triggerEventManager;

        // Set the scheduler on the engines
        _nativeEngine.Scheduler = this;
        _luaEngine.Scheduler = this;

        _nativeEngine.MacroError += OnEngineError;
        _luaEngine.MacroError += OnEngineError;
        _triggerEventManager.TriggerEventOccurred += OnTriggerEventOccurred;

        _nativeEngine.MacroControlRequested += OnMacroControlRequested;
        _luaEngine.MacroControlRequested += OnMacroControlRequested;
        _nativeEngine.MacroStepCompleted += OnMacroStepCompleted;
        _luaEngine.MacroStepCompleted += OnMacroStepCompleted;

        SubscribeToTriggerEvents();
    }

    /// <summary>
    /// Gets all currently running macros.
    /// </summary>
    public IEnumerable<IMacro> GetMacros() => _macroStates.Values.Select(s => s.Macro);

    /// <summary>
    /// Gets the current state of a macro.
    /// </summary>
    public MacroState GetMacroState(string macroId) => _macroStates.TryGetValue(macroId, out var state) ? state.Macro.State : MacroState.Unknown;

    /// <summary>
    /// Subscribes a macro to a trigger event.
    /// </summary>
    /// <param name="macro">The macro to subscribe.</param>
    /// <param name="triggerEvent">The trigger event to subscribe to.</param>
    public void SubscribeToTriggerEvent(IMacro macro, TriggerEvent triggerEvent)
    {
        ArgumentNullException.ThrowIfNull(macro);

        switch (triggerEvent)
        {
            case TriggerEvent.OnAutoRetainerCharacterPostProcess:
                if (!_arApis.ContainsKey(macro.Id))
                {
                    _arApis.TryAdd(macro.Id, new AutoRetainerApi());
                    _arApis[macro.Id].OnCharacterPostprocessStep += () => CheckCharacterPostProcess(macro);
                    _arApis[macro.Id].OnCharacterReadyToPostProcess += () => DoCharacterPostProcess(macro);
                }
                break;
            case TriggerEvent.OnAddonEvent:
                if (macro.Metadata.AddonEventConfig is { } cfg)
                {
                    if (!_addonEvents.ContainsKey(macro.Id))
                    {
                        _addonEvents.TryAdd(macro.Id, cfg);
                        Svc.AddonLifecycle.RegisterListener(
                            (Dalamud.Game.Addon.Lifecycle.AddonEvent)((int)cfg.EventType), 
                            cfg.AddonName, 
                            OnAddonEvent);
                    }
                }
                break;
            default:
                throw new ArgumentException($"Unsupported trigger event: {triggerEvent}", nameof(triggerEvent));
        }
    }

    /// <summary>
    /// Unsubscribes a macro from a trigger event.
    /// </summary>
    /// <param name="macro">The macro to unsubscribe.</param>
    /// <param name="triggerEvent">The trigger event to unsubscribe from.</param>
    public void UnsubscribeFromTriggerEvent(IMacro macro, TriggerEvent triggerEvent)
    {
        ArgumentNullException.ThrowIfNull(macro);

        switch (triggerEvent)
        {
            case TriggerEvent.OnAutoRetainerCharacterPostProcess:
                if (_arApis.TryGetValue(macro.Id, out var arApi))
                {
                    arApi.OnCharacterPostprocessStep -= () => CheckCharacterPostProcess(macro);
                    arApi.OnCharacterReadyToPostProcess -= () => DoCharacterPostProcess(macro);
                    arApi.Dispose();
                    _arApis.Remove(macro.Id);
                }
                break;
            case TriggerEvent.OnAddonEvent:
                if (_addonEvents.TryGetValue(macro.Id, out var cfg))
                {
                    Svc.AddonLifecycle.UnregisterListener(
                        (Dalamud.Game.Addon.Lifecycle.AddonEvent)((int)cfg.EventType), 
                        cfg.AddonName, 
                        OnAddonEvent);
                    _addonEvents.Remove(macro.Id);
                }
                break;
            default:
                throw new ArgumentException($"Unsupported trigger event: {triggerEvent}", nameof(triggerEvent));
        }
    }

    #region Controls
    public async Task StartMacro(IMacro macro) => await StartMacro(macro, null);

    /// <summary>
    /// Registers function-level triggers for a macro.
    /// </summary>
    /// <param name="macro">The macro to register function triggers for.</param>
    private void RegisterFunctionTriggers(IMacro macro)
    {
        if (macro.Type == MacroType.Lua)
        {
            // Look for function definitions in the format "function OnEventName()"
            var matches = Regex.Matches(macro.Content, @"function\s+(\w+)\s*\(");
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                _triggerEventManager.RegisterFunctionTrigger(macro, functionName);
            }
        }
        else
        {
            // Look for commands in the format "/OnEventName"
            var matches = Regex.Matches(macro.Content, @"^/\s*(\w+)", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                _triggerEventManager.RegisterFunctionTrigger(macro, functionName);
            }
        }
    }

    /// <summary>
    /// Unregisters function-level triggers for a macro.
    /// </summary>
    /// <param name="macro">The macro to unregister function triggers for.</param>
    private void UnregisterFunctionTriggers(IMacro macro)
    {
        if (macro.Type == MacroType.Lua)
        {
            // Look for function definitions in the format "function OnEventName()"
            var matches = Regex.Matches(macro.Content, @"function\s+(\w+)\s*\(");
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                _triggerEventManager.UnregisterFunctionTrigger(macro, functionName);
            }
        }
        else
        {
            // Look for commands in the format "/OnEventName"
            var matches = Regex.Matches(macro.Content, @"^/\s*(\w+)", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                _triggerEventManager.UnregisterFunctionTrigger(macro, functionName);
            }
        }
    }

    /// <summary>
    /// Starts execution of a macro.
    /// </summary>
    /// <param name="macro">The macro to execute.</param>
    /// <param name="triggerArgs">Optional trigger event arguments.</param>
    public async Task StartMacro(IMacro macro, TriggerEventArgs? triggerArgs = null)
    {
        if (_macroStates.ContainsKey(macro.Id))
        {
            Svc.Log.Warning($"Macro {macro.Name} is already running.");
            return;
        }

        // Subscribe to state changes before creating the state
        macro.StateChanged += OnMacroStateChanged;
        var state = new MacroExecutionState(macro);

        // Register function-level triggers when macro starts
        RegisterFunctionTriggers(macro);

        state.ExecutionTask = Task.Run(async () =>
        {
            try
            {
                IMacroEngine engine = state.Macro.Type switch
                {
                    MacroType.Native => _nativeEngine,
                    MacroType.Lua => _luaEngine,
                    _ => throw new NotSupportedException($"Macro type {state.Macro.Type} is not supported.")
                };

                _enginesByMacroId[macro.Id] = engine;
                _macroStates[macro.Id] = state;

                await Svc.Framework.RunOnTick(async () =>
                {
                    try
                    {
                        Svc.Log.Verbose($"Setting macro {macro.Id} state to Running");
                        state.Macro.State = MacroState.Running;
                        await engine.StartMacro(macro, state.CancellationSource.Token, triggerArgs);
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error(ex, $"Error executing macro {macro.Name}");
                        state.Macro.State = MacroState.Error;
                    }
                });
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, $"Error setting up macro {macro.Name}");
                state.Macro.State = MacroState.Error;
            }
        });

        await state.ExecutionTask;
        Svc.Log.Verbose($"Setting macro {macro.Id} state to Completed");
        state.Macro.State = MacroState.Completed;
    }

    /// <summary>
    /// Pauses execution of a macro.
    /// </summary>
    /// <param name="macroId">The ID of the macro to pause.</param>
    public void PauseMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseEvent.Reset();
            state.Macro.State = MacroState.Paused;
        }
    }

    /// <summary>
    /// Resumes execution of a paused macro.
    /// </summary>
    /// <param name="macroId">The ID of the macro to resume.</param>
    public void ResumeMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseEvent.Set();
            state.Macro.State = MacroState.Running;
        }
    }

    /// <summary>
    /// Stops execution of a macro.
    /// </summary>
    /// <param name="macroId">The ID of the macro to stop.</param>
    public void StopMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.CancellationSource.Cancel();
            state.Macro.StateChanged -= OnMacroStateChanged;
            state.Macro.State = MacroState.Completed;

            // Unregister function-level triggers when macro stops
            UnregisterFunctionTriggers(state.Macro);

            if (_macroStates.TryRemove(macroId, out var removedState))
                removedState.Dispose();
            _enginesByMacroId.TryRemove(macroId, out _);
        }
    }

    /// <summary>
    /// Forces cleanup of a macro's state.
    /// </summary>
    /// <param name="macroId">The ID of the macro to clean up.</param>
    public void CleanupMacro(string macroId)
    {
        if (_macroStates.TryRemove(macroId, out var state))
        {
            if (state.Macro is ConfigMacro configMacro)
                _triggerEventManager.UnregisterAllTriggers(configMacro);

            UnregisterFunctionTriggers(state.Macro);

            state.CancellationSource.Cancel();
            state.CancellationSource.Dispose();
            state.Macro.StateChanged -= OnMacroStateChanged;
        }

        _enginesByMacroId.TryRemove(macroId, out _);
    }

    /// <summary>
    /// Stops all running macros.
    /// </summary>
    public void StopAllMacros() => _enginesByMacroId.Keys.Each(StopMacro);

    /// <summary>
    /// Checks if the macro should pause at the current loop point.
    /// </summary>
    public void CheckLoopPause(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state) && state.PauseAtLoop)
        {
            state.PauseAtLoop = false;
            state.PauseEvent.Reset();
            state.Macro.State = MacroState.Paused;
        }
    }

    /// <summary>
    /// Checks if the macro should stop at the current loop point.
    /// </summary>
    public void CheckLoopStop(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state) && state.StopAtLoop)
        {
            state.StopAtLoop = false;
            state.CancellationSource.Cancel();
            state.Macro.State = MacroState.Completed;
        }
    }

    /// <summary>
    /// Sets a macro to pause at the next loop point.
    /// </summary>
    public Task PauseAtNextLoop(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseAtLoop = true;
            state.StopAtLoop = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets a macro to stop at the next loop point.
    /// </summary>
    public Task StopAtNextLoop(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseAtLoop = false;
            state.StopAtLoop = true;
        }
        return Task.CompletedTask;
    }
    #endregion

    private class MacroExecutionState(IMacro macro)
    {
        public IMacro Macro { get; } = macro;
        public bool PauseAtLoop { get; set; }
        public bool StopAtLoop { get; set; }
        public CancellationTokenSource CancellationSource { get; } = new CancellationTokenSource();
        public ManualResetEventSlim PauseEvent { get; } = new ManualResetEventSlim(true);
        public Task? ExecutionTask { get; set; }

        public void Dispose()
        {
            CancellationSource.Dispose();
            PauseEvent.Dispose();
        }
    }

    private void OnEngineError(object? sender, MacroErrorEventArgs e) => MacroError?.Invoke(this, e);

    private void OnMacroStateChanged(object? sender, MacroStateChangedEventArgs e)
    {
        Svc.Log.Verbose($"[{nameof(MacroScheduler)}] Macro state changed for {e.MacroId}: {e.NewState}");

        // If this is a temporary macro, find its parent
        var parts = e.MacroId.Split("_");
        if (parts.Length > 1 && C.GetMacro(parts[0]) is { } parentMacro)
        {
            // Propagate error state to parent
            if (e.NewState == MacroState.Error)
            {
                parentMacro.State = MacroState.Error;
            }
        }

        // Raise the event for all subscribers
        MacroStateChanged?.Invoke(sender, e);

        if (e.NewState is MacroState.Completed or MacroState.Error)
        {
            // If this is a temporary macro, unregister it and clean up
            if (parts.Length > 1 && C.GetMacro(parts[0]) is { } parentMacro2)
            {
                _macroHierarchy.UnregisterTemporaryMacro(e.MacroId);
                if (sender is IMacro tempMacro)
                {
                    tempMacro.StateChanged -= OnMacroStateChanged;
                }
            }

            // Unregister function-level triggers before stopping the macro
            if (sender is IMacro macro)
                UnregisterFunctionTriggers(macro);

            StopMacro(e.MacroId);
            CleanupMacro(e.MacroId);
        }
    }

    #region Triggers
    private void OnTriggerEventOccurred(object? sender, TriggerEventArgs e)
    {
        if (sender is IMacro macro)
        {
            _ = StartMacro(macro, e);
        }
    }

    private void SubscribeToTriggerEvents()
    {
        C.Macros.ForEach(SubscribeCustomTriggers);

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.Condition.ConditionChange += OnConditionChange;
        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
        Svc.Chat.ChatMessage += OnChatMessage;
        Svc.ClientState.Login += OnLogin;
        Svc.ClientState.Logout += OnLogout;
    }

    private void SubscribeCustomTriggers(IMacro macro)
    {
        foreach (var triggerEvent in macro.Metadata.TriggerEvents)
        {
            switch (triggerEvent)
            {
                case TriggerEvent.OnAutoRetainerCharacterPostProcess:
                    if (!_arApis.ContainsKey(macro.Id))
                    {
                        _arApis.TryAdd(macro.Id, new AutoRetainerApi());
                        _arApis[macro.Id].OnCharacterPostprocessStep += () => CheckCharacterPostProcess(macro);
                        _arApis[macro.Id].OnCharacterReadyToPostProcess += () => DoCharacterPostProcess(macro);
                    }
                    break;
                case TriggerEvent.OnAddonEvent:
                    if (macro.Metadata.AddonEventConfig is { } cfg)
                    {
                        if (!_addonEvents.ContainsKey(macro.Id))
                        {
                            _addonEvents.TryAdd(macro.Id, cfg);
                            Svc.AddonLifecycle.RegisterListener(
                                (Dalamud.Game.Addon.Lifecycle.AddonEvent)((int)cfg.EventType), 
                                cfg.AddonName, 
                                OnAddonEvent);
                        }
                    }
                    break;
            }
        }
    }

    private void OnAddonEvent(Dalamud.Game.Addon.Lifecycle.AddonEvent type, AddonArgs args)
    {
        foreach (var kvp in _addonEvents)
            if (kvp.Value is { } cfg && 
                (Dalamud.Game.Addon.Lifecycle.AddonEvent)((int)cfg.EventType) == type && 
                cfg.AddonName == args.AddonName && 
                C.GetMacro(kvp.Key) is { } macro)
                _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnAddonEvent, new { type, args });
    }

    private void OnFrameworkUpdate(IFramework framework) => C.Macros.Where(m => m.Metadata.TriggerEvents.Contains(TriggerEvent.OnUpdate)).Each(m => _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnUpdate));

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        var eventData = new Dictionary<string, object>
        {
            ["flag"] = flag,
            ["value"] = value
        };

        var args = new TriggerEventArgs(TriggerEvent.OnConditionChange, eventData);
        C.Macros.Where(m => m.Metadata.TriggerEvents.Contains(TriggerEvent.OnConditionChange)).Each(m => _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnConditionChange, eventData));
    }

    private void OnTerritoryChanged(ushort territoryType)
    {
        var args = new TriggerEventArgs(TriggerEvent.OnTerritoryChange, territoryType);
        C.Macros.Where(m => m.Metadata.TriggerEvents.Contains(TriggerEvent.OnTerritoryChange)).Each(m => _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnTerritoryChange, territoryType));
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var eventData = new Dictionary<string, object>
        {
            ["type"] = type,
            ["timestamp"] = timestamp,
            ["sender"] = sender,
            ["message"] = message,
            ["isHandled"] = isHandled
        };

        var args = new TriggerEventArgs(TriggerEvent.OnChatMessage, eventData);
        C.Macros.Where(m => m.Metadata.TriggerEvents.Contains(TriggerEvent.OnChatMessage)).Each(m => _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnChatMessage, eventData));
    }

    private void OnLogin()
        => C.Macros.Where(m => m.Metadata.TriggerEvents.Contains(TriggerEvent.OnLogin)).Each(m => _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnLogin));

    private void OnLogout(int type, int code)
    {
        var eventData = new Dictionary<string, object>
        {
            ["type"] = type,
            ["code"] = code
        };

        var args = new TriggerEventArgs(TriggerEvent.OnLogout, eventData);
        C.Macros.Where(m => m.Metadata.TriggerEvents.Contains(TriggerEvent.OnLogout)).Each(m => _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnLogout, eventData));
    }

    private void CheckCharacterPostProcess(IMacro macro)
    {
        if (C.ARCharacterPostProcessExcludedCharacters.Any(x => x == Svc.ClientState.LocalContentId))
            Svc.Log.Info($"Skipping post process macro {macro.Name} for current character.");
        else
            _arApis[macro.Id].RequestCharacterPostprocess();
    }

    private void DoCharacterPostProcess(IMacro macro)
    {
        if (C.ARCharacterPostProcessExcludedCharacters.Any(x => x == Svc.ClientState.LocalContentId))
            Svc.Log.Info($"Skipping post process macro {macro.Name} for current character.");
        else
            _arApis[macro.Id].RequestCharacterPostprocess();
    }

    private void OnMacroControlRequested(object? sender, MacroControlEventArgs e)
    {
        Svc.Log.Verbose($"[{nameof(MacroScheduler)}] Received MacroControlRequested event for macro {e.MacroId} with control type {e.ControlType}");

        if (e.ControlType == MacroControlType.Start)
        {
            if (C.GetMacro(e.MacroId) is { } macro)
            {
                Svc.Log.Debug($"[{nameof(MacroScheduler)}] Starting macro {e.MacroId}");
                _ = StartMacro(macro);
            }
            else if (sender is IMacroEngine engine && engine.GetTemporaryMacro(e.MacroId) is { } tempMacro)
            {
                Svc.Log.Debug($"[{nameof(MacroScheduler)}] Starting temporary macro {e.MacroId}");
                // Find the parent macro by looking at the ID prefix
                var parentId = e.MacroId.Split("_")[0];
                if (C.GetMacro(parentId) is { } parentMacro)
                {
                    // Register the temporary macro with its parent
                    _macroHierarchy.RegisterTemporaryMacro(parentMacro, tempMacro);

                    // Subscribe to state changes for the temporary macro
                    Svc.Log.Verbose($"[{nameof(MacroScheduler)}] Subscribing to state changes for temporary macro {e.MacroId}");
                    tempMacro.StateChanged += OnMacroStateChanged;

                    // Start the temporary macro
                    _ = StartMacro(tempMacro);
                }
                else
                {
                    Svc.Log.Warning($"[{nameof(MacroScheduler)}] Could not find parent macro {parentId} for temporary macro {e.MacroId}");
                }
            }
            else
            {
                Svc.Log.Warning($"[{nameof(MacroScheduler)}] Could not find macro {e.MacroId} to start");
            }
        }
        else if (e.ControlType == MacroControlType.Stop)
        {
            StopMacro(e.MacroId);
        }
    }

    private void OnMacroStepCompleted(object? sender, MacroStepCompletedEventArgs e)
    {
        Svc.Log.Verbose($"Macro step completed for {e.MacroId}: {e.StepIndex}/{e.TotalSteps}");
    }
    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        _nativeEngine.MacroError -= OnEngineError;
        _luaEngine.MacroError -= OnEngineError;
        _triggerEventManager.TriggerEventOccurred -= OnTriggerEventOccurred;

        _nativeEngine.MacroControlRequested -= OnMacroControlRequested;
        _luaEngine.MacroControlRequested -= OnMacroControlRequested;
        _nativeEngine.MacroStepCompleted -= OnMacroStepCompleted;
        _luaEngine.MacroStepCompleted -= OnMacroStepCompleted;

        _macroStates.Values.Each(s => s.Dispose());
        _macroStates.Clear();
        _enginesByMacroId.Clear();

        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Condition.ConditionChange -= OnConditionChange;
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Svc.Chat.ChatMessage -= OnChatMessage;
        Svc.ClientState.Login -= OnLogin;
        Svc.ClientState.Logout -= OnLogout;
        Svc.AddonLifecycle.UnregisterListener(OnAddonEvent);

        _nativeEngine.Dispose();
        _luaEngine.Dispose();
        _arApis.Values.Each(a => a.Dispose());
        _arApis.Clear();
        _addonEvents.Clear();

        _triggerEventManager.Dispose();
    }
}
