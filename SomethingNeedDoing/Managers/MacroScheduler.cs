using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.Windowing;
using ECommons;
using Microsoft.Extensions.Hosting;
using NLua;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Gui;
using SomethingNeedDoing.LuaMacro;
using SomethingNeedDoing.NativeMacro;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Managers;
/// <summary>
/// Manages and coordinates execution of multiple macros.
/// </summary>
[RegisterSingleton<IMacroScheduler>, AutoConstruct]
[RegisterSingleton<IHostedService>(Duplicate = DuplicateStrategy.Append, Factory = nameof(GetHostedService))]
public partial class MacroScheduler : IMacroScheduler, IHostedService, IDisposable
{
    public static IHostedService GetHostedService(IServiceProvider provider) => (MacroScheduler)provider.GetRequiredService<IMacroScheduler>();

    private readonly ConcurrentDictionary<string, MacroExecutionState> _macroStates = [];
    private readonly ConcurrentDictionary<string, AddonEventConfig> _addonEvents = [];
    private readonly ConcurrentDictionary<string, IReadOnlyList<FunctionInfo>> _registeredFunctions = []; // function triggers registered per running macro
    private readonly ConcurrentDictionary<string, long> _startBlockedUntil = []; // macros that recently failed validation
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _triggerRefreshDebounceCts = [];
    private readonly ConcurrentDictionary<Task, IMacro> _activeRuns = []; // every active StartMacro, awaited on shutdown
    private readonly ConcurrentDictionary<string, IDisableable> _disableablePlugins = [];
    private volatile bool _isShuttingDown;

    private const int FailedValidationBlockMs = 5000;
    private const int TriggerRefreshDebounceMs = 500;
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly NativeMacroEngine _nativeEngine;
    private readonly NLuaMacroEngine _luaEngine;
    private readonly TriggerEventManager _triggerEventManager;
    private readonly TriggerEventSources _eventSources;
    private readonly MacroHierarchyManager _hierarchyManager;
    private readonly WindowSystem _windowSystem;
    private readonly MetadataParser _metadataParser;
    private readonly IEnumerable<IDisableable> _disableablePluginSources;

    /// <inheritdoc/>
    public event EventHandler<MacroStateChangedEventArgs>? MacroStateChanged;

    [AutoPostConstruct]
    private void Initialize()
    {
        _triggerEventManager.TriggerEventOccurred += OnTriggerEventOccurred;
        _triggerEventManager.FunctionExecutionRequested += OnFunctionExecutionRequested;
        _nativeEngine.MacroExecutionRequested += OnMacroExecutionRequested;
        _luaEngine.MacroExecutionRequested += OnMacroExecutionRequested;
        _nativeEngine.LoopControlRequested += OnLoopControlRequested;

        foreach (var plugin in _disableablePluginSources)
            _disableablePlugins[plugin.InternalName] = plugin;

        foreach (var macro in C.Macros)
        {
            // Parse metadata from content to ensure filters and triggers are loaded
            macro.Metadata = _metadataParser.ParseMetadata(macro.Content, macro.Metadata);
            foreach (var triggerEvent in macro.Metadata.TriggerEvents)
                SubscribeToTriggerEvent(macro, triggerEvent);
            macro.ContentChanged += OnMacroContentChanged;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IMacro> GetMacros() => _macroStates.Values.Select(s => s.Macro);

    /// <inheritdoc/>
    public MacroState GetMacroState(string macroId) => _macroStates.TryGetValue(macroId, out var state) ? state.Macro.State : MacroState.Unknown;

    #region Macro-level triggers
    /// <inheritdoc/>
    public void SubscribeToTriggerEvent(IMacro macro, TriggerEvent triggerEvent)
    {
        ArgumentNullException.ThrowIfNull(macro);

        switch (triggerEvent)
        {
            case TriggerEvent.OnAutoRetainerCharacterPostProcess:
                _eventSources.AttachAutoRetainer(macro);
                break;
            case TriggerEvent.OnAddonEvent:
                if (macro.Metadata.AddonEventConfig is not { } cfg || !_addonEvents.TryAdd(macro.Id, cfg))
                    return;
                _eventSources.AcquireAddonListener(cfg.EventType, cfg.AddonName);
                break;
        }
        _triggerEventManager.RegisterTrigger(macro, triggerEvent);
    }

    /// <inheritdoc/>
    public void UnsubscribeFromTriggerEvent(IMacro macro, TriggerEvent triggerEvent)
    {
        ArgumentNullException.ThrowIfNull(macro);

        switch (triggerEvent)
        {
            case TriggerEvent.OnAutoRetainerCharacterPostProcess:
                _eventSources.DetachAutoRetainer(macro.Id);
                break;
            case TriggerEvent.OnAddonEvent:
                if (_addonEvents.TryRemove(macro.Id, out var cfg))
                    _eventSources.ReleaseAddonListener(cfg.EventType, cfg.AddonName);
                break;
        }
        _triggerEventManager.UnregisterTrigger(macro, triggerEvent);
    }

    /// <inheritdoc/>
    public void RefreshTriggersFromContent(ConfigMacro macro, bool refreshFunctionTriggersIfRunning = true)
    {
        ArgumentNullException.ThrowIfNull(macro);

        foreach (var triggerEvent in macro.Metadata.TriggerEvents.ToList())
            UnsubscribeFromTriggerEvent(macro, triggerEvent);

        _metadataParser.ParseMetadata(macro.Content, macro.Metadata);

        foreach (var triggerEvent in macro.Metadata.TriggerEvents)
            SubscribeToTriggerEvent(macro, triggerEvent);

        if (refreshFunctionTriggersIfRunning && _macroStates.TryGetValue(macro.Id, out var state))
        {
            UnregisterFunctionTriggers(state.Macro);
            RegisterFunctionTriggers(state.Macro);
        }
    }

    /// <inheritdoc/>
    public void UnregisterDeletedMacro(IMacro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        StopMacro(macro.Id);

        foreach (var triggerEvent in macro.Metadata.TriggerEvents.ToList())
            UnsubscribeFromTriggerEvent(macro, triggerEvent);

        _triggerEventManager.UnregisterAllTriggers(macro);
        UnregisterFunctionTriggers(macro);

        macro.ContentChanged -= OnMacroContentChanged;
        CancelTriggerRefresh(macro.Id);
        RemoveState(macro.Id);
        FrameworkLogger.Debug($"Unregistered triggers and cleaned up deleted macro {macro.Name} ({macro.Id})");
    }
    #endregion

    #region Function-level triggers
    private void RegisterFunctionTriggers(IMacro macro)
    {
        if (macro is TemporaryMacro) return;

        var functions = MacroFunctions.TriggerFunctions(macro.Content, macro.Type).ToList();
        if (!_registeredFunctions.TryAdd(macro.Id, functions)) return;

        foreach (var function in functions)
        {
            _triggerEventManager.RegisterFunctionTrigger(macro, function);
            if (function is { AddonName: { } addonName, AddonEvent: { } addonEvent })
                _eventSources.AcquireAddonListener(addonEvent, addonName);
        }
    }

    private void UnregisterFunctionTriggers(IMacro macro)
    {
        if (!_registeredFunctions.TryRemove(macro.Id, out var functions)) return;

        foreach (var function in functions)
            if (function is { AddonName: { } addonName, AddonEvent: { } addonEvent })
                _eventSources.ReleaseAddonListener(addonEvent, addonName);
        _triggerEventManager.UnregisterFunctionTriggers(macro);
    }
    #endregion

    #region Controls
    /// <inheritdoc/>
    public async Task StartMacro(IMacro macro, int loopCount = 0, TriggerEventArgs? triggerArgs = null)
    {
        // during shutdown only temporary macros may start, so cleanup functions of stopping macros can still run their commands
        if (_isShuttingDown && macro is not TemporaryMacro)
            return;

        var run = RunMacro(macro, loopCount, triggerArgs);
        _activeRuns.TryAdd(run, macro);
        try
        {
            await run;
        }
        finally
        {
            _activeRuns.TryRemove(run, out _);
        }
    }

    private async Task RunMacro(IMacro macro, int loopCount, TriggerEventArgs? triggerArgs)
    {
        if (_macroStates.ContainsKey(macro.Id))
        {
            FrameworkLogger.Verbose($"Macro {macro.Name} is already running, skipping start.");
            return;
        }

        // after failing validation, frequently triggered macros (e.g. OnUpdate) don't retry (and print errors) every frame
        var now = Environment.TickCount64;
        if (_startBlockedUntil.TryGetValue(macro.Id, out var blockedUntil) && now < blockedUntil)
        {
            FrameworkLogger.Verbose($"Macro {macro.Name} failed validation recently, skipping start");
            return;
        }

        if (!CanRun(macro, out var reason))
        {
            FrameworkLogger.Error($"Cannot run {macro.Name}. {reason}");
            Svc.Chat.PrintErrorMsg($"Cannot run {macro.Name}. {reason}");
            _startBlockedUntil[macro.Id] = now + FailedValidationBlockMs;
            return;
        }

        var state = new MacroExecutionState(macro);
        if (!_macroStates.TryAdd(macro.Id, state))
        {
            state.Dispose();
            return;
        }

        macro.StateChanged += OnMacroStateChanged;
        macro.ContentChanged += OnMacroContentChanged;
        RegisterFunctionTriggers(macro);

        IMacroEngine engine = macro.Type switch
        {
            MacroType.Native => _nativeEngine,
            MacroType.Lua => _luaEngine,
            _ => throw new NotSupportedException($"Macro type {macro.Type} is not supported.")
        };

        // Defer dependency checking to avoid blocking main thread when rapidly executing a macro
        _ = Task.Run(async () =>
        {
            try
            {
                var missingDependencies = await GetMissingDependenciesAsync(macro);
                if (missingDependencies.Count > 0)
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

        try
        {
            await Svc.Framework.RunOnTick(async () =>
            {
                FrameworkLogger.Verbose($"Setting macro {macro.Id} state to Running");
                macro.State = MacroState.Running;

                if (C.AutoOpenStatusWindow && _hierarchyManager.GetParentMacro(macro.Id) == null)
                    if (_windowSystem.GetWindow<StatusWindow>() is { IsOpen: false } statusWindow)
                        statusWindow.IsOpen = true;

                await engine.StartMacro(macro, state, state.CancellationSource.Token, triggerArgs, loopCount);
            });
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error(ex, $"Error executing macro {macro.Name}");
            macro.State = MacroState.Error;
        }

        FrameworkLogger.Verbose($"Setting macro {macro.Id} state to Completed");
        macro.State = MacroState.Completed;
        await SetPluginStates(macro, true);
    }

    /// <inheritdoc/>
    public async void SetPaused(string macroId, bool paused)
    {
        if (!_macroStates.TryGetValue(macroId, out var state))
            return;

        var targets = C.PropagateControlsToChildren ? _hierarchyManager.GetChildMacros(macroId).Select(c => c.Id).Prepend(macroId) : [macroId];
        foreach (var id in targets)
        {
            if (!_macroStates.TryGetValue(id, out var target)) continue;
            if (paused)
                target.PauseEvent.Reset();
            else
                target.PauseEvent.Set();
            target.Macro.State = paused ? MacroState.Paused : MacroState.Running;
        }

        await SetPluginStates(state.Macro, paused);
    }

    /// <inheritdoc/>
    public async void StopMacro(string macroId)
    {
        if (_macroStates.TryGetValue(macroId, out var state))
        {
            TryCancel(state);
            state.Macro.State = MacroState.Completed;

            // rest of the cleanup will be handled by OnMacroStateChanged
            UnregisterFunctionTriggers(state.Macro);
            await SetPluginStates(state.Macro, true);

            if (C.PropagateControlsToChildren)
                foreach (var child in _hierarchyManager.GetChildMacros(macroId).ToList())
                    StopMacro(child.Id);
        }
    }

    /// <inheritdoc/>
    public void StopAllMacros() => _macroStates.Keys.Each(StopMacro);

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

    private bool CanRun(IMacro macro, out string reason)
    {
        var missingPlugins = macro.Metadata.PluginDependecies.Where(dep => !dep.IsNullOrEmpty() && !Svc.PluginInterface.InstalledPlugins.Any(ip => ip.InternalName == dep && ip.IsLoaded)).ToList();
        reason = missingPlugins.Count > 0 ? $"The following plugins need to be installed: {string.Join(", ", missingPlugins)}"
            : !macro.HasValidConfigs() ? "One or more of its configs failed to validate."
            : string.Empty;
        return reason.Length == 0;
    }

    /// <summary>
    /// Checks if all dependencies for a macro are available and downloads them if needed.
    /// </summary>
    /// <param name="macro">The macro to check dependencies for.</param>
    /// <returns>A description of each dependency that is unavailable.</returns>
    private static async Task<List<string>> GetMissingDependenciesAsync(IMacro macro)
    {
        var results = await Task.WhenAll(macro.Metadata.Dependencies.Select(async dependency =>
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

                return null;
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error(ex, $"Error checking availability of dependency {dependency.Name}");
                return $"{dependency.Name} ({dependency.Source}) - Error: {ex.Message}";
            }
        }));
        return [.. results.OfType<string>()];
    }

    private async Task SetPluginStates(IMacro macro, bool enable)
    {
        foreach (var name in macro.Metadata.PluginsToDisable)
        {
            if (!_disableablePlugins.TryGetValue(name, out var plugin))
            {
                FrameworkLogger.Warning($"Plugin {name} is not registered as disableable");
                continue;
            }

            FrameworkLogger.Info($"[{macro.Name}] {(enable ? "Re-enabling" : "Disabling")} plugin {name}");
            await (enable ? plugin.EnableAsync() : plugin.DisableAsync());
        }
    }

    private class MacroExecutionState(IMacro macro) : IMacroInstance
    {
        public IMacro Macro { get; } = macro;
        public bool PauseAtLoop { get; set; }
        public bool StopAtLoop { get; set; }
        public CancellationTokenSource CancellationSource { get; } = new CancellationTokenSource();
        public ManualResetEventSlim PauseEvent { get; } = new ManualResetEventSlim(true);

        public void Dispose()
        {
            CancellationSource.Dispose();
            PauseEvent.Dispose();
        }
    }

    private static void TryCancel(MacroExecutionState state)
    {
        try
        {
            state.CancellationSource.Cancel();
        }
        catch (ObjectDisposedException) { } // already completed
    }

    /// <summary>
    /// Forgets a macro's execution state and function triggers (macro-level triggers are kept).
    /// </summary>
    private void RemoveState(string macroId)
    {
        if (!_macroStates.TryRemove(macroId, out var state))
            return;

        UnregisterFunctionTriggers(state.Macro);
        TryCancel(state);
        state.CancellationSource.Dispose();
        state.Macro.StateChanged -= OnMacroStateChanged;
        state.Macro.ContentChanged -= OnMacroContentChanged;
    }

    #region Event handlers
    private void OnMacroStateChanged(object? sender, MacroStateChangedEventArgs e)
    {
        MacroStateChanged?.Invoke(sender, e);

        if (e.NewState is not (MacroState.Completed or MacroState.Error))
            return;

        if (sender is IMacro macro && macro.Metadata.TriggerEvents.Contains(TriggerEvent.OnAutoRetainerCharacterPostProcess))
            _eventSources.FinishCharacterPostProcess(macro);

        if (sender is TemporaryMacro temp)
        {
            if (e.NewState == MacroState.Error && _hierarchyManager.GetRootParentMacro(temp.Id) is { } rootParent)
                rootParent.State = MacroState.Error;
            _hierarchyManager.UnregisterTemporaryMacro(e.MacroId);
            temp.StateChanged -= OnMacroStateChanged;
        }

        RemoveState(e.MacroId);
    }

    private void OnMacroContentChanged(object? sender, MacroContentChangedEventArgs e)
    {
        _startBlockedUntil.TryRemove(e.MacroId, out _);

        if (C.GetMacro(e.MacroId) is null)
            return;

        CancelTriggerRefresh(e.MacroId);
        var cts = new CancellationTokenSource();
        _triggerRefreshDebounceCts[e.MacroId] = cts;

        var macroId = e.MacroId;
        _ = Task.Delay(TriggerRefreshDebounceMs, cts.Token).ContinueWith(
            t =>
            {
                if (!t.IsCompletedSuccessfully || cts.Token.IsCancellationRequested)
                    return;

                Svc.Framework.RunOnTick(() =>
                {
                    if (!_triggerRefreshDebounceCts.TryGetValue(macroId, out var current) || !ReferenceEquals(current, cts))
                        return;

                    CancelTriggerRefresh(macroId);
                    if (C.GetMacro(macroId) is ConfigMacro configMacro)
                        RefreshTriggersFromContent(configMacro);
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void CancelTriggerRefresh(string macroId)
    {
        if (_triggerRefreshDebounceCts.TryRemove(macroId, out var cts))
        {
            cts.Cancel();
            try { cts.Dispose(); } catch { }
        }
    }

    private void OnTriggerEventOccurred(object? sender, TriggerEventArgs e)
    {
        if (sender is not IMacro macro)
            return;

        if (macro is ConfigMacro && C.GetMacro(macro.Id) is null) // orphaned ConfigMacro
        {
            // don't run triggers for orphaned macros, unregister them
            FrameworkLogger.Warning($"Skipping trigger event {e.EventType} for deleted macro {macro.Name} ({macro.Id}); unregistering orphaned triggers");
            _triggerEventManager.UnregisterAllTriggers(macro);
            return;
        }

        _ = StartMacro(macro, 0, e);
    }

    /// <summary>
    /// Calls a trigger function in the Lua state of its (running) macro.
    /// </summary>
    private void OnFunctionExecutionRequested(object? sender, FunctionExecutionRequestedEventArgs e)
    {
        try
        {
            if (!_macroStates.TryGetValue(e.MacroId, out var state) || state.Macro.State != MacroState.Running)
            {
                FrameworkLogger.Debug($"Skipping function {e.FunctionName} for stopped macro {e.MacroId}");
                return;
            }

            if (_luaEngine.GetLuaEnvironment(e.MacroId) is not Lua lua)
            {
                FrameworkLogger.Debug($"Skipping function {e.FunctionName} for macro {e.MacroId} - Lua environment not available");
                return;
            }

            // the function isn't defined for the first few frames, until the script body has run
            if (lua[e.FunctionName] is not LuaFunction function)
            {
                FrameworkLogger.Debug($"Skipping function {e.FunctionName} for macro {e.MacroId} - function not yet defined in Lua environment");
                return;
            }

            using (function)
            {
                FrameworkLogger.Verbose($"Executing function {e.FunctionName} in macro {state.Macro.Name}");
                lua.SetTriggerEventData(e.TriggerArgs);
                function.Call(); // call in the parent's lua state
            }
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error($"Error executing function {e.FunctionName} for macro {e.MacroId}: {ex}");
        }
    }

    private void OnLoopControlRequested(object? sender, LoopControlEventArgs e)
    {
        if (!_macroStates.TryGetValue(e.MacroId, out var state))
            return;

        if (e.ControlType == LoopControlType.Pause && state.PauseAtLoop)
        {
            state.PauseAtLoop = false;
            state.PauseEvent.Reset();
            state.Macro.State = MacroState.Paused;
        }
        else if (e.ControlType == LoopControlType.Stop && state.StopAtLoop)
        {
            state.StopAtLoop = false;
            TryCancel(state);
            state.Macro.State = MacroState.Completed;
        }
    }

    private void OnMacroExecutionRequested(object? sender, MacroExecutionRequestedEventArgs e)
    {
        FrameworkLogger.Verbose($"Received macro execution request for {e.Macro.Name}");
        _ = StartMacro(e.Macro, e.LoopCount, e.TriggerArgs);
    }
    #endregion

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Cancels all running macros and waits (bounded) for them to finish while plugin services are still alive.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _isShuttingDown = true;
        foreach (var state in _macroStates.Values)
            TryCancel(state);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ShutdownTimeout);
        var timeout = Task.Delay(Timeout.Infinite, timeoutCts.Token);

        // loop since cleanup functions can start new temporary macros while we wait
        while (_activeRuns.Keys.Where(t => !t.IsCompleted).ToArray() is { Length: > 0 } pending)
        {
            if (await Task.WhenAny(Task.WhenAll(pending), timeout) == timeout)
            {
                var stuck = _activeRuns.Where(r => !r.Key.IsCompleted).Select(r => r.Value.Name).ToList();
                FrameworkLogger.Warning($"{stuck.Count} macro(s) did not stop within {ShutdownTimeout.TotalSeconds}s of unload: {string.Join(", ", stuck)}");
                break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _isShuttingDown = true;
        _triggerEventManager.TriggerEventOccurred -= OnTriggerEventOccurred;
        _triggerEventManager.FunctionExecutionRequested -= OnFunctionExecutionRequested;
        _nativeEngine.MacroExecutionRequested -= OnMacroExecutionRequested;
        _luaEngine.MacroExecutionRequested -= OnMacroExecutionRequested;
        _nativeEngine.LoopControlRequested -= OnLoopControlRequested;

        // anything still running past StopAsync's timeout at least gets told to stop
        _macroStates.Values.Each(s =>
        {
            TryCancel(s);
            s.Dispose();
        });
        _macroStates.Clear();
        _startBlockedUntil.Clear();

        C.Macros.ForEach(m => m.ContentChanged -= OnMacroContentChanged);
        foreach (var macroId in _triggerRefreshDebounceCts.Keys.ToList())
            CancelTriggerRefresh(macroId);

        _addonEvents.Clear();
        _triggerEventManager.Dispose();
    }
}
