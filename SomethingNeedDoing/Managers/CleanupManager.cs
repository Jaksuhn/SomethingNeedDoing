using SomethingNeedDoing.Core.Interfaces;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.Managers;

/// <summary>
/// Manages cleanup functions for macros when they stop execution.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class CleanupManager : IDisposable
{
    private readonly Dictionary<string, List<string>> _cleanupFunctionsByMacroId = [];
    private readonly Dictionary<string, IMacro> _macrosById = [];

    /// <summary>
    /// Event raised when a cleanup function should be executed.
    /// </summary>
    public event EventHandler<CleanupFunctionEventArgs>? CleanupFunctionRequested;

    /// <summary>
    /// Registers cleanup functions for a macro.
    /// </summary>
    public void RegisterCleanupFunctions(IMacro macro)
    {
        if (macro is TemporaryMacro) return;

        var cleanupFunctions = new List<string>();

        if (macro.Type == MacroType.Lua)
        {
            var patterns = new[]
            {
                @"function\s+(OnCleanup|OnDispose|OnStop|OnEnd)\s*\(",
                @"function\s+(Cleanup|Dispose|Stop|End)\s*\("
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(macro.Content, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var functionName = match.Groups[1].Value;
                    cleanupFunctions.Add(functionName);
                    FrameworkLogger.Debug($"Found cleanup function {functionName} in macro {macro.Name}");
                }
            }
        }

        if (cleanupFunctions.Count > 0)
        {
            _cleanupFunctionsByMacroId[macro.Id] = cleanupFunctions;
            _macrosById[macro.Id] = macro;
            FrameworkLogger.Debug($"Registered {cleanupFunctions.Count} cleanup functions for macro {macro.Name}");
        }
    }

    /// <summary>
    /// Unregisters cleanup functions for a macro.
    /// </summary>
    public void UnregisterCleanupFunctions(IMacro macro)
    {
        if (macro is TemporaryMacro) return;

        if (_cleanupFunctionsByMacroId.Remove(macro.Id))
        {
            _macrosById.Remove(macro.Id);
            FrameworkLogger.Debug($"Unregistered cleanup functions for macro {macro.Name}");
        }
    }

    /// <summary>
    /// Executes cleanup functions for a macro.
    /// </summary>
    public void ExecuteCleanup(string macroId, string reason = "Stopped")
    {
        if (!_cleanupFunctionsByMacroId.TryGetValue(macroId, out var cleanupFunctions) || !_macrosById.TryGetValue(macroId, out var macro))
            return;

        FrameworkLogger.Info($"Executing {cleanupFunctions.Count} cleanup functions for macro {macro.Name} (reason: {reason})");

        foreach (var functionName in cleanupFunctions)
        {
            try
            {
                if (macro.Type == MacroType.Lua)
                {
                    FrameworkLogger.Verbose($"Requesting cleanup function execution for {functionName} in macro {macro.Name}");
                    CleanupFunctionRequested?.Invoke(this, new CleanupFunctionEventArgs(macro.Id, functionName, reason));
                }
                else
                    throw new NotSupportedException($"[{nameof(CleanupManager)}] Cleanup function {functionName} is not supported for macro type {macro.Type}");
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error(ex, $"Error executing cleanup function {functionName} for macro {macro.Name}");
            }
        }
    }

    /// <summary>
    /// Gets all registered cleanup functions for a macro.
    /// </summary>
    public IReadOnlyList<string> GetCleanupFunctions(string macroId)
        => _cleanupFunctionsByMacroId.TryGetValue(macroId, out var functions) ? functions.AsReadOnly() : Array.Empty<string>();

    /// <summary>
    /// Checks if a macro has any registered cleanup functions.
    /// </summary>
    public bool HasCleanupFunctions(string macroId) => _cleanupFunctionsByMacroId.ContainsKey(macroId);

    /// <summary>
    /// Disposes of the cleanup manager.
    /// </summary>
    public void Dispose()
    {
        _cleanupFunctionsByMacroId.Clear();
        _macrosById.Clear();
    }
}

/// <summary>
/// Event arguments for cleanup function execution.
/// </summary>
/// <param name="tempMacro">The temporary macro containing the cleanup function.</param>
/// <param name="functionName">The name of the cleanup function.</param>
/// <param name="reason">The reason for cleanup execution.</param>
public class CleanupFunctionEventArgs(string macroId, string functionName, string reason) : EventArgs
{
    /// <summary>
    /// Gets the temporary macro containing the cleanup function.
    /// </summary>
    public string MacroId { get; } = macroId;

    /// <summary>
    /// Gets the name of the cleanup function.
    /// </summary>
    public string FunctionName { get; } = functionName;

    /// <summary>
    /// Gets the reason for cleanup execution.
    /// </summary>
    public string Reason { get; } = reason;
}
