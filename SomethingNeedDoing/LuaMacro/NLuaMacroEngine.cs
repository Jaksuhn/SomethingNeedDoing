using NLua;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Modules;
using SomethingNeedDoing.LuaMacro.Modules.Engines;
using SomethingNeedDoing.Managers;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.LuaMacro;

/// <summary>
/// Executes Lua script macros using NLua.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class NLuaMacroEngine : IMacroEngine
{
    private readonly LuaModuleManager _moduleManager;
    private readonly MacroHierarchyManager _macroHierarchy;

    /// <inheritdoc/>
    public event EventHandler<MacroExecutionRequestedEventArgs>? MacroExecutionRequested;

    private readonly ConcurrentDictionary<string, Lua> _activeLuaEnvironments = [];

    public Lua? GetLuaEnvironment(string macroId) => _activeLuaEnvironments.TryGetValue(macroId, out var lua) ? lua : null;

    /// <inheritdoc/>
    public async Task StartMacro(IMacro macro, IMacroInstance instance, CancellationToken token, TriggerEventArgs? triggerArgs = null, int loopCount = 0)
    {
        if (macro.Type != MacroType.Lua)
            throw new ArgumentException("This engine only supports Lua macros", nameof(macro));

        // captured now so cleanup matches the functions defined by the script that actually ran
        var cleanupFunctions = macro is TemporaryMacro ? [] : MacroFunctions.CleanupFunctions(macro.Content, macro.Type).ToList();

        Lua? lua = null;
        try
        {
            FrameworkLogger.Verbose($"Starting Lua macro execution for macro {macro.Id}");
            lua = CreateEnvironment(macro, triggerArgs);
            _activeLuaEnvironments[macro.Id] = lua; // for function triggers to access the same state

            await LoadDependenciesIntoScope(lua, macro);
            await Svc.Framework.RunOnTick(async () =>
            {
                try
                {
                    var results = lua.LoadEntryPointWrappedScript(macro.ContentSansMetadata());
                    if (results.Length == 0 || results[0] is not LuaFunction generator)
                        throw new LuaException("Failed to load Lua script: No function returned");

                    await RunGenerator(generator, macro, "native", null, instance.PauseEvent, token);
                }
                catch (OperationCanceledException)
                {
                    FrameworkLogger.Debug($"Operation cancelled for macro {macro.Id}");
                }
                catch (Exception ex)
                {
                    OnMacroError(macro, $"Error executing Lua function for macro {macro.Id}", ex);
                }
                finally
                {
                    await ExecuteCleanupFunctions(lua, macro, cleanupFunctions);
                }
            }, cancellationToken: token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            FrameworkLogger.Info($"Operation cancelled for macro {macro.Id}");
        }
        catch (Exception ex)
        {
            OnMacroError(macro, "Error executing macro", ex);
            throw;
        }
        finally
        {
            _activeLuaEnvironments.Remove(macro.Id, out var _);

            // Doing this manually since NLua is a very well written library
            try
            {
                lua?.Dispose();
            }
            catch (Exception ex)
            {
                FrameworkLogger.Warning($"Error disposing Lua environment for macro {macro.Id}: {ex.Message}");
            }
        }
    }

    private Lua CreateEnvironment(IMacro macro, TriggerEventArgs? triggerArgs)
    {
        var lua = new Lua();
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
        _moduleManager.RegisterAll(lua);

        var nativeEngine = new NativeEngine();
        nativeEngine.MacroExecutionRequested += (sender, e) => MacroExecutionRequested?.Invoke(this, e);
        var luaEngine = new LuaEngine();
        luaEngine.MacroExecutionRequested += (sender, e) => MacroExecutionRequested?.Invoke(this, e);

        new EnginesModule([nativeEngine, luaEngine]).Register(lua);
        new ConfigModule(macro).Register(lua);
        return lua;
    }

    /// <summary>
    /// Resumes a Lua generator until it finishes, running each yielded string as a native macro.
    /// </summary>
    /// <param name="generator">The generator returned by <see cref="LuaExtensions.LoadEntryPointWrappedScript"/>.</param>
    /// <param name="macro">The macro the generator belongs to.</param>
    /// <param name="idTag">Tag used in the ids of the native macros created.</param>
    /// <param name="commandName">Optional name for the native macros created.</param>
    /// <param name="pauseEvent">Optional pause event to wait on before each step.</param>
    /// <param name="token">A token to stop execution.</param>
    private async Task RunGenerator(LuaFunction generator, IMacro macro, string idTag, string? commandName, ManualResetEventSlim? pauseEvent, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (pauseEvent != null)
                await MacroPause.WaitAsync(pauseEvent, token);

            var result = await Svc.Framework.RunOnTick(() => generator.Call());
            if (result.Length == 0)
                return;

            if (result[0] is not string command)
                throw new MacroException($"Lua Macro yielded a non-string value [{result[0]?.GetType().Name ?? "null"}: {result[0]?.ToString() ?? "null"}]");

            if (MacroExecutionRequested is not { } executor)
            {
                FrameworkLogger.Warning($"Macro {macro.Name} yielded \"{command}\" but no executor is attached (plugin unloading?), stopping");
                return;
            }

            var tempMacro = new TemporaryMacro(macro, command, _macroHierarchy, $"{macro.Id}_{idTag}_{Guid.NewGuid()}");
            if (commandName != null)
                tempMacro.Name = commandName;
            await tempMacro.Run(executor);

            await Svc.Framework.DelayTicks(1);
        }
    }

    private void OnMacroError(IMacro macro, string message, Exception? ex = null)
    {
        Svc.Chat.PrintErrorMsg(message);
        FrameworkLogger.Error($"Error executing macro {macro.Name} [{macro.Id}]: {ex}");
    }

    /// <summary>
    /// Loads all dependencies into the Lua scope.
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="macro">The macro whose dependencies to load.</param>
    private static async Task LoadDependenciesIntoScope(Lua lua, IMacro macro)
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

    /// <summary>
    /// Runs the macro's cleanup functions (OnStop etc.) to completion, ignoring cancellation.
    /// </summary>
    private async Task ExecuteCleanupFunctions(Lua lua, IMacro macro, List<string> cleanupFunctions)
    {
        foreach (var functionName in cleanupFunctions)
        {
            try
            {
                var results = lua.LoadEntryPointWrappedScript($@"{functionName}()");
                if (results.Length == 0 || results[0] is not LuaFunction generator)
                {
                    FrameworkLogger.Error($"Failed to load cleanup function {functionName}");
                    continue;
                }

                await RunGenerator(generator, macro, "cleanup_cmd", $"{macro.Name} - Cleanup Command", null, CancellationToken.None);
                FrameworkLogger.Debug($"Cleanup function {functionName} completed");
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Error executing cleanup function {functionName} for macro {macro.Name}: {ex}");
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
