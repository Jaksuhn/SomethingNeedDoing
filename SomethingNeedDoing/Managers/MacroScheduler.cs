using AutoRetainerAPI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using NLua;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro;
using SomethingNeedDoing.LuaMacro.Wrappers;
using SomethingNeedDoing.NativeMacro;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Managers;
/// <summary>
/// Manages and coordinates execution of multiple macros.
/// </summary>
public class MacroScheduler : IMacroScheduler, IDisposable
{
    private readonly ConcurrentDictionary<string, MacroExecutionState> _macroStates = [];
    private readonly ConcurrentDictionary<string, IMacroEngine> _enginesByMacroId = [];
    private readonly ConcurrentDictionary<string, AutoRetainerApi> _arApis = [];
    private readonly ConcurrentDictionary<string, AddonEventConfig> _addonEvents = [];
    private readonly ConcurrentDictionary<string, IDisableable> _disableablePlugins = [];
    private readonly ConcurrentDictionary<string, List<string>> _cachedFunctionNames = []; // avoid duplicate regex matching in rapid succession (OnUpdate, etc)

    private readonly NativeMacroEngine _nativeEngine;
    private readonly NLuaMacroEngine _luaEngine;
    private readonly TriggerEventManager _triggerEventManager;
    private readonly MacroHierarchyManager _hierarchyManager;

    private readonly HashSet<string> _functionTriggersRegistered = [];

    /// <inheritdoc/>
    public event EventHandler<MacroStateChangedEventArgs>? MacroStateChanged;

    /// <summary>
    /// Event raised when any macro encounters an error.
    /// </summary>
    public event EventHandler<MacroErrorEventArgs>? MacroError;

    public MacroScheduler(NativeMacroEngine nativeEngine, NLuaMacroEngine luaEngine, TriggerEventManager triggerEventManager, MacroHierarchyManager hierarchyManager, IEnumerable<IDisableable> disableablePlugins)
    {
        _nativeEngine = nativeEngine;
        _luaEngine = luaEngine;
        _triggerEventManager = triggerEventManager;
        _hierarchyManager = hierarchyManager;

        _nativeEngine.MacroError += OnEngineError;
        _luaEngine.MacroError += OnEngineError;
        _triggerEventManager.TriggerEventOccurred += OnTriggerEventOccurred;
        _triggerEventManager.FunctionExecutionRequested += OnFunctionExecutionRequested;

        _nativeEngine.MacroControlRequested += OnMacroControlRequested;
        _luaEngine.MacroControlRequested += OnMacroControlRequested;
        _nativeEngine.MacroStepCompleted += OnMacroStepCompleted;
        _luaEngine.MacroStepCompleted += OnMacroStepCompleted;

        _nativeEngine.MacroExecutionRequested += OnMacroExecutionRequested;
        _luaEngine.MacroExecutionRequested += OnMacroExecutionRequested;

        _nativeEngine.LoopControlRequested += OnLoopControlRequested;
        _luaEngine.LoopControlRequested += OnLoopControlRequested;

        foreach (var plugin in disableablePlugins)
            _disableablePlugins[plugin.InternalName] = plugin;

        SubscribeToTriggerEvents();
    }

    /// <inheritdoc/>
    public IEnumerable<IMacro> GetMacros() => _macroStates.Values.Select(s => s.Macro);

    /// <inheritdoc/>
    public MacroState GetMacroState(string macroId) => _macroStates.TryGetValue(macroId, out var state) ? state.Macro.State : MacroState.Unknown;

    /// <inheritdoc/>
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
                _triggerEventManager.RegisterTrigger(macro, triggerEvent);
                break;
            case TriggerEvent.OnAddonEvent:
                if (macro.Metadata.AddonEventConfig is { } cfg)
                {
                    if (!_addonEvents.ContainsKey(macro.Id))
                    {
                        _addonEvents.TryAdd(macro.Id, cfg);
                        Svc.AddonLifecycle.RegisterListener(cfg.EventType, cfg.AddonName, OnAddonEvent);
                    }
                }
                break;
            default:
                // For all other events, we just need to register with the trigger event manager
                _triggerEventManager.RegisterTrigger(macro, triggerEvent);
                break;
        }
    }

    /// <inheritdoc/>
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
                    _arApis.Remove(macro.Id, out _);
                }
                _triggerEventManager.UnregisterTrigger(macro, triggerEvent);
                break;
            case TriggerEvent.OnAddonEvent:
                if (_addonEvents.TryGetValue(macro.Id, out var cfg))
                {
                    Svc.AddonLifecycle.UnregisterListener(cfg.EventType, cfg.AddonName, OnAddonEvent);
                    _addonEvents.Remove(macro.Id, out _);
                }
                break;
            default:
                // For all other events, we just need to unregister from the trigger event manager
                _triggerEventManager.UnregisterTrigger(macro, triggerEvent);
                break;
        }
    }

    #region Controls
    public async Task StartMacro(IMacro macro) => await StartMacro(macro, null);

    /// <summary>
    /// Parses and caches function names from macro content to avoid regex on every start/stop.
    /// </summary>
    /// <param name="macro">The macro to parse function names for.</param>
    /// <returns>List of function names found in the macro.</returns>
    private List<string> ParseFunctionNames(IMacro macro)
    {
        if (_cachedFunctionNames.TryGetValue(macro.Id, out var cachedNames))
            return cachedNames;

        var functionNames = new List<string>();

        if (macro.Type == MacroType.Lua)
        {
            // match "function OnEventName()"
            var matches = Regex.Matches(macro.Content, @"function\s+(\w+)\s*\(");
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                functionNames.Add(functionName);
            }
        }
        else
        {
            // match "/OnEventName"
            var matches = Regex.Matches(macro.Content, @"^/\s*(\w+)", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                functionNames.Add(functionName);
            }
        }

        _cachedFunctionNames[macro.Id] = functionNames;
        return functionNames;
    }

    /// <summary>
    /// Registers function-level triggers for a macro using cached function names.
    /// </summary>
    /// <param name="macro">The macro to register function triggers for.</param>
    private void RegisterFunctionTriggers(IMacro macro)
    {
        if (macro is TemporaryMacro) return;
        if (_functionTriggersRegistered.Contains(macro.Id)) return;

        var functionNames = ParseFunctionNames(macro);
        foreach (var functionName in functionNames)
            _triggerEventManager.RegisterFunctionTrigger(macro, functionName);
        _functionTriggersRegistered.Add(macro.Id);
    }

    /// <summary>
    /// Unregisters function-level triggers for a macro using cached function names.
    /// </summary>
    /// <param name="macro">The macro to unregister function triggers for.</param>
    private void UnregisterFunctionTriggers(IMacro macro)
    {
        if (macro is TemporaryMacro) return;
        if (!_functionTriggersRegistered.Contains(macro.Id)) return;

        var functionNames = ParseFunctionNames(macro);
        foreach (var functionName in functionNames)
            _triggerEventManager.UnregisterFunctionTrigger(macro, functionName);
        _functionTriggersRegistered.Remove(macro.Id);
    }

    /// <inheritdoc/>
    public Task StartMacro(IMacro macro, int loopCount) => StartMacro(macro, loopCount, null);

    /// <inheritdoc/>
    public Task StartMacro(IMacro macro, TriggerEventArgs? triggerArgs = null) => StartMacro(macro, 0, triggerArgs);

    /// <inheritdoc/>
    public async Task StartMacro(IMacro macro, int loopCount, TriggerEventArgs? triggerArgs = null)
    {
        if (_macroStates.ContainsKey(macro.Id))
        {
            FrameworkLogger.Warning($"Macro {macro.Name} is already running.");
            return;
        }

        if (MissingRequiredPlugins(macro, out var missingPlugins))
        {
            FrameworkLogger.Error($"Cannot run {macro.Name}. The following plugins need to be installed: {string.Join(", ", missingPlugins)}");
            Svc.Chat.PrintErrorMsg($"Cannot run {macro.Name}. The following plugins need to be installed: {string.Join(", ", missingPlugins)}");
            return;
        }

        if (!macro.HasValidConfigs())
        {
            FrameworkLogger.Error($"Cannot run {macro.Name}. One or more of its configs failed to validate.");
            Svc.Chat.PrintErrorMsg($"Cannot run {macro.Name}. One or more of its configs failed to validate.");
            return;
        }

        macro.StateChanged += OnMacroStateChanged;
        macro.ContentChanged += OnMacroContentChanged;
        var state = new MacroExecutionState(macro);
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

                // Defer dependency checking to avoid blocking main thread when rapidly executing a macro
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (areAvailable, missingDependencies) = await AreDependenciesAvailableAsync(macro);
                        if (!areAvailable)
                        {
                            FrameworkLogger.Error($"Cannot run {macro.Name}. The following dependencies are not available: {string.Join(", ", missingDependencies)}");
                            Svc.Chat.PrintErrorMsg($"Cannot run {macro.Name}. The following dependencies are not available: {string.Join(", ", missingDependencies)}");
                            StopMacro(macro.Id);
                            return;
                        }

                        await SetPluginStates(macro, false);
                    }
                    catch (Exception ex)
                    {
                        FrameworkLogger.Error(ex, $"Error in deferred operations for macro {macro.Name}");
                        StopMacro(macro.Id);
                    }
                });

                await Svc.Framework.RunOnTick(async () =>
                {
                    try
                    {
                        FrameworkLogger.Verbose($"Setting macro {macro.Id} state to Running");
                        state.Macro.State = MacroState.Running;
                        await engine.StartMacro(macro, state.CancellationSource.Token, triggerArgs, loopCount);
                    }
                    catch (Exception ex)
                    {
                        FrameworkLogger.Error(ex, $"Error executing macro {macro.Name}");
                        state.Macro.State = MacroState.Error;
                        await SetPluginStates(macro, true);
                    }
                });
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error(ex, $"Error setting up macro {macro.Name}");
                state.Macro.State = MacroState.Error;
                await SetPluginStates(macro, true);
            }
        });

        await state.ExecutionTask;
        FrameworkLogger.Verbose($"Setting macro {macro.Id} state to Completed");
        state.Macro.State = MacroState.Completed;
        await SetPluginStates(macro, true);
    }

    /// <inheritdoc/>
    public async void PauseMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseEvent.Reset();
            state.Macro.State = MacroState.Paused;
            await SetPluginStates(state.Macro, true);

            if (C.PropagateControlsToChildren)
            {
                foreach (var child in _hierarchyManager.GetChildMacros(macroId))
                {
                    if (_macroStates.TryGetValue(child.Id, out var childState))
                    {
                        childState.PauseEvent.Reset();
                        child.State = MacroState.Paused;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async void ResumeMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseEvent.Set();
            state.Macro.State = MacroState.Running;
            await SetPluginStates(state.Macro, false);

            if (C.PropagateControlsToChildren)
            {
                foreach (var child in _hierarchyManager.GetChildMacros(macroId))
                {
                    if (_macroStates.TryGetValue(child.Id, out var childState))
                    {
                        childState.PauseEvent.Set();
                        child.State = MacroState.Running;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async void StopMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.CancellationSource.Cancel();
            state.Macro.State = MacroState.Completed;

            // rest of the cleanup will be handled by OnMacroStateChanged
            UnregisterFunctionTriggers(state.Macro);
            await SetPluginStates(state.Macro, true);

            if (C.PropagateControlsToChildren)
                foreach (var child in _hierarchyManager.GetChildMacros(macroId).ToList())
                    StopMacro(child.Id);
        }
    }

    /// <summary>
    /// Forces cleanup of a macro's state.
    /// </summary>
    /// <param name="macroId">The ID of the macro to clean up.</param>
    public void CleanupMacro(string macroId)
    {
        if (_macroStates.Remove(macroId, out var state))
        {
            if (state.Macro is ConfigMacro configMacro)
                _triggerEventManager.UnregisterAllTriggers(configMacro);

            UnregisterFunctionTriggers(state.Macro);
            state.CancellationSource.Cancel();
            state.CancellationSource.Dispose();
            state.Macro.StateChanged -= OnMacroStateChanged;
            state.Macro.ContentChanged -= OnMacroContentChanged;
        }

        _enginesByMacroId.Remove(macroId, out _);
        _cachedFunctionNames.Remove(macroId, out _);
    }

    /// <summary>
    /// Invalidates cached function names for a macro when its content changes.
    /// </summary>
    /// <param name="macroId">The ID of the macro to invalidate cache for.</param>
    public void InvalidateFunctionCache(string macroId) => _cachedFunctionNames.Remove(macroId, out _);

    /// <inheritdoc/>
    public void StopAllMacros() => _enginesByMacroId.Keys.Each(StopMacro);

    /// <inheritdoc/>
    public void CheckLoopPause(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state) && state.PauseAtLoop)
        {
            state.PauseAtLoop = false;
            state.PauseEvent.Reset();
            state.Macro.State = MacroState.Paused;
        }
    }

    /// <inheritdoc/>
    public void CheckLoopStop(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state) && state.StopAtLoop)
        {
            state.StopAtLoop = false;
            state.CancellationSource.Cancel();
            state.Macro.State = MacroState.Completed;
        }
    }

    /// <inheritdoc/>
    public void PauseAtNextLoop(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseAtLoop = true;
            state.StopAtLoop = false;
        }
    }

    /// <inheritdoc/>
    public void StopAtNextLoop(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            state.PauseAtLoop = false;
            state.StopAtLoop = true;
        }
    }
    #endregion

    private bool MissingRequiredPlugins(IMacro macro, out List<string> missingPlugins)
    {
        missingPlugins = [.. macro.Metadata.PluginDependecies.Where(dep => !dep.IsNullOrEmpty() && !Svc.PluginInterface.InstalledPlugins.Any(ip => ip.InternalName == dep && ip.IsLoaded))];
        return missingPlugins.Count > 0;
    }

    /// <summary>
    /// Checks if all dependencies for a macro are available and downloads them if needed.
    /// </summary>
    /// <param name="macro">The macro to check dependencies for.</param>
    /// <returns>A tuple containing whether all dependencies are available and a list of missing dependencies.</returns>
    private async Task<(bool areAvailable, List<string> missingDependencies)> AreDependenciesAvailableAsync(IMacro macro)
    {
        var missingDependencies = new List<string>();

        if (macro.Metadata.Dependencies.Count == 0)
            return (true, missingDependencies);

        // Process dependencies in parallel to reduce total time
        var dependencyTasks = macro.Metadata.Dependencies.Select(async dependency =>
        {
            try
            {
                if (!await dependency.IsAvailableAsync())
                {
                    FrameworkLogger.Info($"Dependency {dependency.Name} is not available, attempting to download...");
                    try
                    {
                        await dependency.GetContentAsync();
                        FrameworkLogger.Info($"Successfully downloaded dependency {dependency.Name}");
                    }
                    catch (Exception downloadEx)
                    {
                        FrameworkLogger.Error(downloadEx, $"Failed to download dependency {dependency.Name}");
                        return $"{dependency.Name} ({dependency.Source}) - Download failed: {downloadEx.Message}";
                    }
                }

                if (!await dependency.IsAvailableAsync())
                    return $"{dependency.Name} ({dependency.Source}) - Not available after download attempt";

                return null; // Success
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error(ex, $"Error checking availability of dependency {dependency.Name}");
                return $"{dependency.Name} ({dependency.Source}) - Error: {ex.Message}";
            }
        });

        var results = await Task.WhenAll(dependencyTasks);
        missingDependencies.AddRange(results.Where(r => r != null)!);

        return (missingDependencies.Count == 0, missingDependencies);
    }

    private async Task SetPluginStates(IMacro macro, bool state)
    {
        foreach (var name in macro.Metadata.PluginsToDisable)
        {
            if (_disableablePlugins.TryGetValue(name, out var plugin))
            {
                if (state)
                {
                    FrameworkLogger.Info($"[{macro.Name}] Re-enabling plugin {name}");
                    await plugin.EnableAsync();
                }
                else
                {
                    FrameworkLogger.Info($"[{macro.Name}] Disabling plugin {name}");
                    await plugin.DisableAsync();
                }
            }
            else
                FrameworkLogger.Warning($"Plugin {name} is not registered as disableable");
        }
    }

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
        MacroStateChanged?.Invoke(sender, e);

        if (e.NewState is MacroState.Completed or MacroState.Error)
        {
            if (sender is IMacro macro)
            {
                if (macro.Metadata.TriggerEvents.Contains(TriggerEvent.OnAutoRetainerCharacterPostProcess))
                {
                    if (_arApis.TryGetValue(macro.Id, out var arApi))
                    {
                        FrameworkLogger.Info($"{macro.Name} character post process finished, calling FinishCharacterPostProcess()");
                        arApi.FinishCharacterPostProcess();
                    }
                }
            }

            if (sender is TemporaryMacro temp)
            {
                if (e.NewState == MacroState.Error)
                {
                    var rootParent = _hierarchyManager.GetRootParentMacro(temp.Id);
                    if (rootParent is { } parentMacro)
                        parentMacro.State = MacroState.Error;
                }
                _hierarchyManager.UnregisterTemporaryMacro(e.MacroId);
                temp.StateChanged -= OnMacroStateChanged;
            }

            if (_macroStates.Remove(e.MacroId, out var state))
            {
                UnregisterFunctionTriggers(state.Macro);
                state.CancellationSource.Cancel();
                state.CancellationSource.Dispose();
                state.Macro.StateChanged -= OnMacroStateChanged;
                state.Macro.ContentChanged -= OnMacroContentChanged;
            }

            _enginesByMacroId.Remove(e.MacroId, out _);
        }
    }

    private void OnMacroContentChanged(object? sender, MacroContentChangedEventArgs e)
    {
        FrameworkLogger.Verbose($"Macro content changed for {e.MacroId}, invalidating function cache");
        InvalidateFunctionCache(e.MacroId);
    }

    #region Triggers
    private void OnTriggerEventOccurred(object? sender, TriggerEventArgs e)
    {
        if (sender is IMacro macro)
        {
            if (_macroStates.ContainsKey(macro.Id))
            {
                FrameworkLogger.Debug($"Skipping trigger event for macro {macro.Name} - cannot start");
                return;
            }

            if (macro is TemporaryMacro tempMacro)
            {
                FrameworkLogger.Verbose($"Processing temporary macro {macro.Id}");
                FrameworkLogger.Verbose($"Subscribing to state changes for temporary macro {macro.Id}");
                macro.StateChanged += OnMacroStateChanged;
                _ = StartMacro(macro, e);
            }
            else
                _ = StartMacro(macro, e);
        }
    }

    /// <summary>
    /// Handles function execution requests from trigger events.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="e">The function execution request event arguments.</param>
    private void OnFunctionExecutionRequested(object? sender, FunctionExecutionRequestedEventArgs e)
    {
        try
        {
            if (GetEngineForMacro(e.MacroId) is not NLuaMacroEngine nluaEngine || nluaEngine.GetLuaEnvironment(e.MacroId) is not Lua lua)
            {
                FrameworkLogger.Debug($"Skipping function {e.FunctionName} for macro {e.MacroId} - Lua environment not available"); // maybe error?
                return;
            }

            if (C.GetMacro(e.MacroId) is { State: MacroState.Running } macro)
            {
                // Check if the function exists in the Lua environment before trying to call it
                // This happens the first few frames if you have a trigger like OnUpdate
                try
                {
                    var exists = lua.DoString($"return {e.FunctionName} ~= nil")[0] as bool?;
                    if (exists != true)
                    {
                        FrameworkLogger.Debug($"Skipping function {e.FunctionName} for macro {e.MacroId} - function not yet defined in Lua environment");
                        return;
                    }
                }
                catch
                {
                    FrameworkLogger.Debug($"Skipping function {e.FunctionName} for macro {e.MacroId} - function not yet defined in Lua environment");
                    return;
                }

                FrameworkLogger.Verbose($"Executing function {e.FunctionName} in macro {macro.Name}");
                lua.SetTriggerEventData(e.TriggerArgs);
                lua.DoString($"{e.FunctionName}()"); // call in the parent's lua state
            }
            else
                FrameworkLogger.Debug($"Skipping function {e.FunctionName} for stopped macro {e.MacroId}");
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error($"Error executing function {e.FunctionName} for macro {e.MacroId}: {ex}");
        }
    }

    private void SubscribeToTriggerEvents()
    {
        foreach (var macro in C.Macros)
        {
            foreach (var triggerEvent in macro.Metadata.TriggerEvents)
                SubscribeToTriggerEvent(macro, triggerEvent);

            macro.ContentChanged += OnMacroContentChanged;
        }

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.Condition.ConditionChange += OnConditionChange;
        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
        Svc.Chat.ChatMessage += OnChatMessage;
        Svc.ClientState.Login += OnLogin;
        Svc.ClientState.Logout += OnLogout;
        Svc.DutyState.DutyStarted += OnDutyStarted;
        Svc.DutyState.DutyWiped += OnDutyWiped;
        Svc.DutyState.DutyCompleted += OnDutyCompleted;
    }

    private HashSet<string> _activePlugins = [];
    private long _combatStart = 0;
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (Svc.Condition[ConditionFlag.InCombat])
        {
            if (_combatStart == 0)
            {
                _combatStart = DateTime.Now.Ticks;
                var startTimestamp = _combatStart;
                var opponents = Svc.Objects.Where(o => o.TargetObjectId == Player.Object.GameObjectId).Select(o => new EntityWrapper(o));
                _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnCombatStart, new { startTimestamp, opponents });
                FrameworkLogger.Verbose($"Combat started against {string.Join(", ", opponents.Select(o => o.Name))} at {startTimestamp}");
            }
        }
        else
        {
            if (_combatStart != 0)
            {
                var endTimestamp = DateTime.Now.Ticks;
                var duration = TimeSpan.FromTicks(endTimestamp - _combatStart).TotalSeconds;
                _combatStart = 0;
                _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnCombatEnd, new { endTimestamp, duration });
                FrameworkLogger.Verbose($"Combat ended at {endTimestamp} in {duration:F2} seconds");
            }
        }

        var lastActivePlugins = _activePlugins;
        var currentActivePlugins = Svc.PluginInterface.InstalledPlugins.Where(p => p.IsLoaded).Select(p => p.InternalName).ToHashSet();
        lastActivePlugins.SymmetricExceptWith(currentActivePlugins);
        if (lastActivePlugins.Count > 0)
        {
            var diffs = new List<PluginWrapper>();
            lastActivePlugins.Where(currentActivePlugins.Contains).ToList().ForEach(plugin => diffs.Add(new PluginWrapper() { Name = plugin, IsLoaded = true }));
            lastActivePlugins.Where(plugin => !currentActivePlugins.Contains(plugin)).ToList().ForEach(plugin => diffs.Add(new PluginWrapper() { Name = plugin, IsLoaded = false }));
            var eventData = new Dictionary<string, object> { { "changedPlugins", diffs } };
            _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnActivePluginsChanged, new { eventData });
            FrameworkLogger.Verbose($"[{nameof(TriggerEvent.OnActivePluginsChanged)}] fired [{string.Join(", ", diffs)}]");
        }
        _activePlugins = currentActivePlugins;

        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnUpdate);
    }

    private record class PluginWrapper
    {
        public required string Name;
        public bool IsLoaded;
        public override string ToString() => $"{Name}: {IsLoaded}";
    }

    private void OnAddonEvent(AddonEvent type, AddonArgs args)
    {
        var eventData = new Dictionary<string, object> { { "type", type }, { "args", args } };
        FrameworkLogger.Verbose($"[{nameof(OnAddonEvent)}] fired [{type}, {args}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnAddonEvent, eventData);
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        var eventData = new Dictionary<string, object> { { "flag", flag }, { "value", value } };
        FrameworkLogger.Verbose($"[{nameof(OnConditionChange)}] fired [{flag}, {value}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnConditionChange, eventData);
    }

    private void OnTerritoryChanged(ushort territoryType)
    {
        var eventData = new Dictionary<string, object> { { "territoryType", territoryType } };
        FrameworkLogger.Verbose($"[{nameof(OnTerritoryChanged)}] fired [{territoryType}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnTerritoryChange, eventData);
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var eventData = new Dictionary<string, object> { { "type", type }, { "timestamp", timestamp }, { "sender", sender.TextValue }, { "message", message.TextValue }, { "isHandled", isHandled } };
        FrameworkLogger.Verbose($"[{nameof(OnChatMessage)}] fired [{type}, {timestamp}, {sender}, {message}, {isHandled}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnChatMessage, eventData);
    }

    private void OnLogin()
    {
        FrameworkLogger.Verbose($"[{nameof(OnLogin)}] fired");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnLogin);
    }

    private void OnLogout(int type, int code)
    {
        var eventData = new Dictionary<string, object> { { "type", type }, { "code", code } };
        FrameworkLogger.Verbose($"[{nameof(OnLogout)}] fired [{type}, {code}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnLogout, eventData);
    }

    private void OnDutyStarted(object? sender, ushort e)
    {
        FrameworkLogger.Verbose($"[{nameof(OnDutyStarted)}] fired [{e}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnDutyStarted);
    }

    private void OnDutyWiped(object? sender, ushort e)
    {
        FrameworkLogger.Verbose($"[{nameof(OnDutyWiped)}] fired [{e}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnDutyWiped);
    }

    private void OnDutyCompleted(object? sender, ushort e)
    {
        FrameworkLogger.Verbose($"[{nameof(OnDutyCompleted)}] fired [{e}]");
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnDutyCompleted);
    }

    private void CheckCharacterPostProcess(IMacro macro)
    {
        if (C.ARCharacterPostProcessExcludedCharacters.Any(x => x == Svc.ClientState.LocalContentId))
            FrameworkLogger.Info($"Skipping post process macro {macro.Name} for current character.");
        else
            _arApis[macro.Id].RequestCharacterPostprocess();
    }

    private void DoCharacterPostProcess(IMacro macro)
    {
        if (C.ARCharacterPostProcessExcludedCharacters.Any(x => x == Svc.ClientState.LocalContentId))
        {
            FrameworkLogger.Info($"Skipping post process macro {macro.Name} for current character.");
            return;
        }

        FrameworkLogger.Info($"Executing post process macro {macro.Name} for current character.");
        var eventData = new Dictionary<string, object> { { "Id", Svc.ClientState.LocalContentId }, { "Name", Svc.ClientState.LocalPlayer?.Name.TextValue ?? string.Empty } };
        _ = _triggerEventManager.RaiseTriggerEvent(TriggerEvent.OnAutoRetainerCharacterPostProcess, eventData);
    }

    private void OnMacroControlRequested(object? sender, MacroControlEventArgs e)
    {
        FrameworkLogger.Verbose($"Received MacroControlRequested event for macro {e.MacroId} with control type {e.ControlType}");

        if (e.ControlType == MacroControlType.Start)
        {
            if (_macroStates.ContainsKey(e.MacroId))
            {
                FrameworkLogger.Debug($"Skipping start request for macro {e.MacroId} - already running");
                return;
            }

            if (C.GetMacro(e.MacroId) is { } macro)
            {
                FrameworkLogger.Info($"Starting macro {e.MacroId}");
                _ = StartMacro(macro);
            }
            else if (sender is IMacroEngine engine && engine.GetTemporaryMacro(e.MacroId) is { } tempMacro)
            {
                FrameworkLogger.Verbose($"Starting temporary macro {e.MacroId}");
                FrameworkLogger.Verbose($"Subscribing to state changes for temporary macro {e.MacroId}");
                tempMacro.StateChanged += OnMacroStateChanged;
                _ = StartMacro(tempMacro);
            }
            else
                FrameworkLogger.Warning($"Could not find macro {e.MacroId} to start");
        }
        else if (e.ControlType == MacroControlType.Stop)
            StopMacro(e.MacroId);
    }

    private void OnMacroStepCompleted(object? sender, MacroStepCompletedEventArgs e)
        => FrameworkLogger.Verbose($"Macro step completed for {e.MacroId}: {e.StepIndex}/{e.TotalSteps}");

    private void OnLoopControlRequested(object? sender, LoopControlEventArgs e)
    {
        if (_macroStates.TryGetValue(e.MacroId, out var state))
        {
            if (e.ControlType == LoopControlType.Pause && state.PauseAtLoop)
            {
                state.PauseAtLoop = false;
                state.PauseEvent.Reset();
                state.Macro.State = MacroState.Paused;
            }
            else if (e.ControlType == LoopControlType.Stop && state.StopAtLoop)
            {
                state.StopAtLoop = false;
                state.CancellationSource.Cancel();
                state.Macro.State = MacroState.Completed;
            }
        }
    }

    private void OnMacroExecutionRequested(object? sender, MacroExecutionRequestedEventArgs e)
    {
        FrameworkLogger.Verbose($"Received macro execution request for {e.Macro.Name}");

        if (_macroStates.ContainsKey(e.Macro.Id))
        {
            FrameworkLogger.Debug($"Skipping execution request for macro {e.Macro.Name} - already running");
            return;
        }

        if (e.Macro is TemporaryMacro tempMacro)
        {
            FrameworkLogger.Verbose($"Subscribing to state changes for temporary macro {tempMacro.Id}");
            tempMacro.StateChanged += OnMacroStateChanged;
        }

        _ = StartMacro(e.Macro, e.LoopCount, e.TriggerArgs);
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

        _nativeEngine.MacroExecutionRequested -= OnMacroExecutionRequested;
        _luaEngine.MacroExecutionRequested -= OnMacroExecutionRequested;

        _nativeEngine.LoopControlRequested -= OnLoopControlRequested;
        _luaEngine.LoopControlRequested -= OnLoopControlRequested;

        _macroStates.Values.Each(s => s.Dispose());
        _macroStates.Clear();
        _enginesByMacroId.Clear();

        C.Macros.ForEach(m => m.ContentChanged -= OnMacroContentChanged);

        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Condition.ConditionChange -= OnConditionChange;
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Svc.Chat.ChatMessage -= OnChatMessage;
        Svc.ClientState.Login -= OnLogin;
        Svc.ClientState.Logout -= OnLogout;
        Svc.DutyState.DutyStarted -= OnDutyStarted;
        Svc.DutyState.DutyWiped -= OnDutyWiped;
        Svc.DutyState.DutyCompleted -= OnDutyCompleted;
        Svc.AddonLifecycle.UnregisterListener(OnAddonEvent);

        _nativeEngine.Dispose();
        _luaEngine.Dispose();
        _arApis.Values.Each(a => a.Dispose());
        _arApis.Clear();
        _addonEvents.Clear();

        _triggerEventManager.Dispose();
    }

    /// <inheritdoc/>
    public IMacroEngine? GetEngineForMacro(string macroId) => _enginesByMacroId.TryGetValue(macroId, out var engine) ? engine : null;
}
