using SomethingNeedDoing.Core.Events;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.LuaMacro.Modules.Engines;

/// <summary>
/// Engine for executing Lua code.
/// </summary>
public class LuaEngine : IEngine
{
    public string Name => "NLua";

    /// <inheritdoc/>
    public event EventHandler<MacroExecutionRequestedEventArgs>? MacroExecutionRequested;

    public async Task ExecuteAsync(string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var tempMacro = new TemporaryMacro(content) { Type = MacroType.Lua };
            MacroExecutionRequested?.Invoke(this, new MacroExecutionRequestedEventArgs(tempMacro));
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error($"Error executing Lua code '{content}': {ex}");
            throw;
        }
    }

    public bool CanExecute(string content) => !string.IsNullOrWhiteSpace(content) && !content.StartsWith('/');
}
