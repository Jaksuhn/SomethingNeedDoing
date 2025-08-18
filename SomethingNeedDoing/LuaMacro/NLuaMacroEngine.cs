using NLua;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Modules;
using SomethingNeedDoing.LuaMacro.Modules.Engines;
using SomethingNeedDoing.Managers;
using SomethingNeedDoing.NativeMacro;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.LuaMacro;

/// <summary>
/// Executes Lua script macros using NLua.
/// </summary>
public class NLuaMacroEngine(LuaModuleManager moduleManager, CleanupManager cleanupManager, MacroHierarchyManager macroHierarchy, MacroParser parser) : IMacroEngine
{
    /// <inheritdoc/>
    public event EventHandler<MacroErrorEventArgs>? MacroError;

    /// <inheritdoc/>
    public event EventHandler<MacroControlEventArgs>? MacroControlRequested;

    /// <inheritdoc/>
    public event EventHandler<MacroStepCompletedEventArgs>? MacroStepCompleted;

    /// <inheritdoc/>
    public event EventHandler<MacroExecutionRequestedEventArgs>? MacroExecutionRequested;

    /// <summary>
    /// Event raised when loop control is requested.
    /// </summary>
    public event EventHandler<LoopControlEventArgs>? LoopControlRequested;

    private readonly ConcurrentDictionary<string, TemporaryMacro> _temporaryMacros = [];
    private readonly ConcurrentDictionary<string, Lua> _activeLuaEnvironments = [];

    /// <inheritdoc/>
    public IMacro? GetTemporaryMacro(string macroId) => _temporaryMacros.TryGetValue(macroId, out var macro) ? macro : null;
    public Lua? GetLuaEnvironment(string macroId) => _activeLuaEnvironments.TryGetValue(macroId, out var lua) ? lua : null;

    /// <summary>
    /// Represents the current state of a macro execution.
    /// </summary>
    private class MacroInstance(IMacro macro) : IDisposable
    {
        public IMacro Macro { get; } = macro;
        public LuaFunction? LuaGenerator { get; set; }
        public CancellationTokenSource CancellationSource { get; } = new();
        public ManualResetEventSlim PauseEvent { get; } = new(true);
        public Task? ExecutionTask { get; set; }

        public void Dispose()
        {
            CancellationSource.Dispose();
            PauseEvent.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task StartMacro(IMacro macro, CancellationToken token, TriggerEventArgs? triggerArgs = null, int _ = 0)
    {
        if (macro.Type != MacroType.Lua)
            throw new ArgumentException("This engine only supports Lua macros", nameof(macro));

        if (macro is not TemporaryMacro)
            cleanupManager.RegisterCleanupFunctions(macro);

        var state = new MacroInstance(macro);

        try
        {
            state.ExecutionTask = ExecuteMacro(state, token, triggerArgs);
            await state.ExecutionTask;
        }
        catch (Exception ex)
        {
            OnMacroError(macro.Id, "Macro execution failed", ex);
            throw;
        }
    }

    private async Task ExecuteMacro(MacroInstance macro, CancellationToken externalToken, TriggerEventArgs? triggerArgs = null, int _ = 0)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, macro.CancellationSource.Token);
        var token = linkedCts.Token;

        try
        {
            FrameworkLogger.Debug($"Starting Lua macro execution for macro {macro.Macro.Id}");
            using var lua = new Lua();
            lua.State.Encoding = Encoding.UTF8;

            lua.LoadCLRPackage();
            lua.LoadFStrings();
            lua.LoadPackageSearcherSnippet();
            lua.LoadRequirePaths();
            lua.LoadErrorHandler();
            lua.ApplyPrintOverride();
            lua.RegisterInternalFunctions();
            lua.SetTriggerEventData(triggerArgs);
            lua.RegisterClass<Svc>();
            moduleManager.RegisterAll(lua);

            var nativeEngine = new NativeEngine();
            nativeEngine.MacroExecutionRequested += (sender, e) =>
                MacroExecutionRequested?.Invoke(this, e);

            nativeEngine.LoopControlRequested += (sender, e) =>
                LoopControlRequested?.Invoke(this, e);

            var luaEngine = new LuaEngine();
            luaEngine.MacroExecutionRequested += (sender, e) =>
                MacroExecutionRequested?.Invoke(this, e);

            var engines = new List<IEngine>
            {
                nativeEngine,
                luaEngine
            };

            var enginesModule = new EnginesModule(engines);
            enginesModule.Register(lua);
            new ConfigModule(macro.Macro).Register(lua);

            _activeLuaEnvironments[macro.Macro.Id] = lua; // for function triggers to access the same state

            await LoadDependenciesIntoScope(lua, macro.Macro);
            await Svc.Framework.RunOnTick(async () =>
            {
                try
                {
                    // Execute the script
                    var results = lua.LoadEntryPointWrappedScript(macro.Macro.ContentSansMetadata());
                    if (results.Length == 0 || results[0] is not LuaFunction func)
                        throw new LuaException("Failed to load Lua script: No function returned");

                    macro.LuaGenerator = func;

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            // Wait if paused
                            macro.PauseEvent.Wait(token);

                            if (macro.LuaGenerator == null)
                                break;

                            var (macroComplete, result) = await Svc.Framework.RunOnTick(() =>
                            {
                                var result = macro.LuaGenerator.Call();
                                return (result.Length == 0, result);
                            });

                            if (macroComplete)
                                break;

                            if (result.First() is not string text)
                            {
                                var valueType = result.First()?.GetType().Name ?? "null";
                                var valueStr = result.First()?.ToString() ?? "null";
                                throw new MacroException($"Lua Macro yielded a non-string value [{valueType}: {valueStr}]");
                            }

                            if (MacroExecutionRequested is { })
                            {
                                var tempId = $"{macro.Macro.Id}_native_{Guid.NewGuid()}";
                                var tempMacro = new TemporaryMacro(macro.Macro, text, macroHierarchy, tempId);
                                _temporaryMacros[tempId] = tempMacro;
                                await tempMacro.Run(MacroExecutionRequested);
                                _temporaryMacros.Remove(tempId, out var _);
                            }

                            MacroStepCompleted?.Invoke(this, new MacroStepCompletedEventArgs(macro.Macro.Id, 1, 1));

                            await Svc.Framework.DelayTicks(1);
                        }
                        catch (OperationCanceledException)
                        {
                            FrameworkLogger.Debug($"Operation cancelled for macro {macro.Macro.Id}");
                            break;
                        }
                        catch (LuaException ex)
                        {
                            OnMacroError(macro.Macro.Id, $"Error executing Lua function for macro {macro.Macro.Id}", ex);
                            break;
                        }
                        catch (Exception ex)
                        {
                            var errorDetails = "Unknown error";
                            OnMacroError(macro.Macro.Id, $"Error executing Lua function for macro {macro.Macro.Id}: {errorDetails}", ex);
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    FrameworkLogger.Debug($"Operation cancelled for macro {macro.Macro.Id}");
                }
                catch (Exception ex)
                {
                    OnMacroError(macro.Macro.Id, "Error executing macro", ex);
                }
                finally
                {
                    await ExecuteCleanupFunctions(lua, macro.Macro);
                }
            }, cancellationToken: token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            FrameworkLogger.Info($"Operation cancelled for macro {macro.Macro.Id}");
        }
        catch (Exception ex)
        {
            OnMacroError(macro.Macro.Id, "Error executing macro", ex);
            throw;
        }
        finally
        {
            _activeLuaEnvironments.Remove(macro.Macro.Id, out var _);
            macro.Dispose();
        }
    }

    protected virtual void OnMacroError(string macroId, string message, Exception? ex = null)
    {
        Svc.Chat.PrintErrorMsg(message);
        FrameworkLogger.Error($"Error executing macro {macroId}: {ex}");
        MacroError?.Invoke(this, new MacroErrorEventArgs(macroId, message, ex));
    }

    /// <summary>
    /// Loads all dependencies into the Lua scope.
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="macro">The macro whose dependencies to load.</param>
    private async Task LoadDependenciesIntoScope(Lua lua, IMacro macro)
    {
        if (macro.Metadata.Dependencies.Count == 0)
            return;

        FrameworkLogger.Debug($"Loading {macro.Metadata.Dependencies.Count} dependencies for macro {macro.Name}");

        foreach (var dependency in macro.Metadata.Dependencies)
        {
            try
            {
                var content = await dependency.GetContentAsync();
                lua.DoString(content);
                FrameworkLogger.Debug($"Loaded dependency {dependency.Name} into Lua scope for macro {macro.Name}");
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error(ex, $"Failed to load dependency {dependency.Name} for macro {macro.Name}");
                throw new MacroException($"Failed to load dependency {dependency.Name}: {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() { }

    /// <summary>
    /// Executes cleanup functions for a macro.
    /// </summary>
    /// <param name="lua">The Lua environment.</param>
    /// <param name="macro">The macro to execute cleanup for.</param>
    private async Task ExecuteCleanupFunctions(Lua lua, IMacro macro)
    {
        if (macro is TemporaryMacro) return;

        var cleanupFunctions = GetCleanupFunctions(macro.Id);
        if (cleanupFunctions.Count == 0) return;

        foreach (var functionName in cleanupFunctions)
        {
            try
            {
                var results = lua.LoadEntryPointWrappedScript($@"{functionName}()");
                if (results.Length == 0 || results[0] is not LuaFunction func)
                {
                    FrameworkLogger.Error($"Failed to load cleanup function {functionName}");
                    continue;
                }

                try
                {
                    // TODO: don't duplicate logic from executemacro?
                    while (true)
                    {
                        if (func == null)
                        {
                            FrameworkLogger.Debug($"Cleanup function {functionName} completed (func is null)");
                            break;
                        }

                        var result = func.Call();
                        if (result.Length == 0)
                        {
                            FrameworkLogger.Debug($"Cleanup function {functionName} completed");
                            break;
                        }

                        if (result[0] is string command && MacroExecutionRequested is { })
                        {
                            var tempMacro = new TemporaryMacro(macro, command, macroHierarchy, $"{macro.Id}_cleanup_cmd_{Guid.NewGuid()}")
                            {
                                Name = $"{macro.Name} - Cleanup Command",
                                Type = MacroType.Native
                            };

                            await tempMacro.Run(MacroExecutionRequested);
                        }
                        else
                        {
                            FrameworkLogger.Warning($"Cleanup function {functionName} completed with non-string result");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    FrameworkLogger.Error($"Error during cleanup function {functionName} execution: {ex}");
                }
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Error executing cleanup function {functionName} for macro {macro.Name}: {ex}");
            }
        }

        if (macro is not TemporaryMacro)
        {
            cleanupManager.UnregisterCleanupFunctions(macro);
            FrameworkLogger.Debug($"Unregistered cleanup functions for macro {macro.Name} after execution");
        }
    }

    /// <summary>
    /// Gets cleanup functions for a macro from the cleanup manager.
    /// </summary>
    /// <param name="macroId">The macro ID.</param>
    /// <returns>List of cleanup function names.</returns>
    private List<string> GetCleanupFunctions(string macroId)
        => cleanupManager.HasCleanupFunctions(macroId) ? [.. cleanupManager.GetCleanupFunctions(macroId)] : [];
}

/// <summary>
/// Exception thrown by Lua code.
/// </summary>
public class LuaException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LuaException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public LuaException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LuaException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public LuaException(string message, Exception innerException) : base(message, innerException) { }
}
